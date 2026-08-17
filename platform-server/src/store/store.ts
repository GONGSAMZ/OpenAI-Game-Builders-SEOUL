import { randomUUID } from "node:crypto";
import {
  DynamoDBClient,
  type DynamoDBClientConfig
} from "@aws-sdk/client-dynamodb";
import {
  DynamoDBDocumentClient,
  GetCommand,
  QueryCommand,
  TransactWriteCommand
} from "@aws-sdk/lib-dynamodb";
import type { AppConfig } from "../config.js";
import type { StoreProduct } from "./catalog.js";

export interface InventoryEntry {
  itemId: string;
  quantity: number;
}

export interface StorePurchase {
  purchaseId: string;
  provider: "mock" | "dev-tools" | "hive-web-shop";
  productId: string;
  itemId: string;
  quantity: number;
  createdAt: string;
}

export interface GrantResult {
  purchase: StorePurchase;
  inventory: InventoryEntry[];
  duplicate: boolean;
}

export interface MarketStore {
  getInventory(subject: string): Promise<InventoryEntry[]>;
  grantMockPurchase(
    subject: string,
    product: StoreProduct,
    idempotencyKey: string
  ): Promise<GrantResult>;
  grantPurchase(
    subject: string,
    product: StoreProduct,
    input: GrantPurchaseInput
  ): Promise<GrantResult>;
}

export interface GrantPurchaseInput {
  provider: StorePurchase["provider"];
  transactionId: string;
  quantity?: number;
}

function sortedInventory(entries: Iterable<InventoryEntry>): InventoryEntry[] {
  return [...entries].sort((left, right) => left.itemId.localeCompare(right.itemId));
}

export class InMemoryMarketStore implements MarketStore {
  private readonly inventories = new Map<string, Map<string, InventoryEntry>>();
  private readonly purchases = new Map<string, { subject: string; purchase: StorePurchase }>();

  public async getInventory(subject: string): Promise<InventoryEntry[]> {
    return sortedInventory(this.inventories.get(subject)?.values() ?? []);
  }

  public async grantMockPurchase(
    subject: string,
    product: StoreProduct,
    idempotencyKey: string
  ): Promise<GrantResult> {
    return this.grantPurchase(subject, product, {
      provider: "mock",
      transactionId: idempotencyKey
    });
  }

  public async grantPurchase(
    subject: string,
    product: StoreProduct,
    input: GrantPurchaseInput
  ): Promise<GrantResult> {
    const receiptKey = `${input.provider}:${input.transactionId}`;
    const existing = this.purchases.get(receiptKey);
    if (existing) {
      if (
        existing.subject !== subject ||
        existing.purchase.productId !== product.id ||
        existing.purchase.quantity !== product.grant.quantity * (input.quantity ?? 1)
      ) {
        throw new Error("이미 다른 구매에 사용된 transactionId입니다.");
      }
      return {
        purchase: existing.purchase,
        inventory: await this.getInventory(subject),
        duplicate: true
      };
    }

    const inventory = this.inventories.get(subject) ?? new Map<string, InventoryEntry>();
    const current = inventory.get(product.grant.itemId);
    inventory.set(product.grant.itemId, {
      itemId: product.grant.itemId,
      quantity: (current?.quantity ?? 0) + product.grant.quantity * (input.quantity ?? 1)
    });
    this.inventories.set(subject, inventory);

    const purchase: StorePurchase = {
      purchaseId: randomUUID(),
      provider: input.provider,
      productId: product.id,
      itemId: product.grant.itemId,
      quantity: product.grant.quantity * (input.quantity ?? 1),
      createdAt: new Date().toISOString()
    };
    this.purchases.set(receiptKey, { subject, purchase });

    return { purchase, inventory: await this.getInventory(subject), duplicate: false };
  }
}

interface DynamoReceiptItem extends StorePurchase {
  PK: string;
  SK: "RECEIPT";
  subject: string;
}

export class DynamoDbMarketStore implements MarketStore {
  private readonly client: DynamoDBDocumentClient;

  public constructor(
    private readonly tableName: string,
    clientConfig: DynamoDBClientConfig = {}
  ) {
    this.client = DynamoDBDocumentClient.from(new DynamoDBClient(clientConfig), {
      marshallOptions: { removeUndefinedValues: true }
    });
  }

  public async getInventory(subject: string): Promise<InventoryEntry[]> {
    const result = await this.client.send(
      new QueryCommand({
        TableName: this.tableName,
        KeyConditionExpression: "PK = :pk AND begins_with(SK, :itemPrefix)",
        ExpressionAttributeValues: {
          ":pk": `PLAYER#${subject}`,
          ":itemPrefix": "ITEM#"
        },
        ProjectionExpression: "itemId, quantity"
      })
    );

    return sortedInventory(
      (result.Items ?? []).map((item) => ({
        itemId: String(item.itemId),
        quantity: Number(item.quantity)
      }))
    );
  }

  public async grantMockPurchase(
    subject: string,
    product: StoreProduct,
    idempotencyKey: string
  ): Promise<GrantResult> {
    return this.grantPurchase(subject, product, {
      provider: "mock",
      transactionId: idempotencyKey
    });
  }

  public async grantPurchase(
    subject: string,
    product: StoreProduct,
    input: GrantPurchaseInput
  ): Promise<GrantResult> {
    const receiptKey = `RECEIPT#${input.provider.toUpperCase()}#${input.transactionId}`;
    const existing = await this.readReceipt(receiptKey);
    if (existing) return this.duplicateResult(existing, subject, product, input.quantity ?? 1);

    const purchase: StorePurchase = {
      purchaseId: randomUUID(),
      provider: input.provider,
      productId: product.id,
      itemId: product.grant.itemId,
      quantity: product.grant.quantity * (input.quantity ?? 1),
      createdAt: new Date().toISOString()
    };

    try {
      await this.client.send(
        new TransactWriteCommand({
          TransactItems: [
            {
              Put: {
                TableName: this.tableName,
                Item: { PK: receiptKey, SK: "RECEIPT", subject, ...purchase },
                ConditionExpression: "attribute_not_exists(PK)"
              }
            },
            {
              Update: {
                TableName: this.tableName,
                Key: { PK: `PLAYER#${subject}`, SK: `ITEM#${product.grant.itemId}` },
                UpdateExpression:
                  "SET itemId = :itemId, updatedAt = :updatedAt ADD quantity :quantity",
                ExpressionAttributeValues: {
                  ":itemId": product.grant.itemId,
                  ":updatedAt": purchase.createdAt,
                  ":quantity": purchase.quantity
                }
              }
            }
          ]
        })
      );
    } catch (error) {
      const racedReceipt = await this.readReceipt(receiptKey);
      if (racedReceipt) {
        return this.duplicateResult(racedReceipt, subject, product, input.quantity ?? 1);
      }
      throw error;
    }

    return { purchase, inventory: await this.getInventory(subject), duplicate: false };
  }

  private async readReceipt(receiptKey: string): Promise<DynamoReceiptItem | undefined> {
    const result = await this.client.send(
      new GetCommand({
        TableName: this.tableName,
        Key: { PK: receiptKey, SK: "RECEIPT" },
        ConsistentRead: true
      })
    );
    return result.Item as DynamoReceiptItem | undefined;
  }

  private async duplicateResult(
    existing: DynamoReceiptItem,
    subject: string,
    product: StoreProduct,
    quantity: number
  ): Promise<GrantResult> {
    if (
      existing.subject !== subject ||
      existing.productId !== product.id ||
      existing.quantity !== product.grant.quantity * quantity
    ) {
      throw new Error("이미 다른 구매에 사용된 transactionId입니다.");
    }

    const { PK: _pk, SK: _sk, subject: _subject, ...purchase } = existing;
    return { purchase, inventory: await this.getInventory(subject), duplicate: true };
  }
}

export function createMarketStore(config: AppConfig): MarketStore {
  if (config.store.dataStore === "dynamodb") {
    if (!config.store.dynamodbTable) throw new Error("DynamoDB 테이블 설정이 없습니다.");
    return new DynamoDbMarketStore(config.store.dynamodbTable);
  }
  return new InMemoryMarketStore();
}

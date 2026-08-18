import { randomUUID } from "node:crypto";
import {
  DynamoDBClient,
  type DynamoDBClientConfig
} from "@aws-sdk/client-dynamodb";
import {
  DynamoDBDocumentClient,
  GetCommand,
  PutCommand,
  QueryCommand,
  TransactWriteCommand
} from "@aws-sdk/lib-dynamodb";
import type { AppConfig } from "../config.js";
import type { StoreProduct } from "./catalog.js";

export interface InventoryEntry {
  itemId: string;
  quantity: number;
}

export interface StoreEquipment {
  moldSkin: "golden-pan" | null;
}

export interface StoreWallet {
  testPoints: number;
}

export interface PlayerStoreState {
  inventory: InventoryEntry[];
  equipment: StoreEquipment;
  wallet: StoreWallet;
}

export interface StorePurchase {
  purchaseId: string;
  provider: "mock" | "dev-tools" | "nicepay-test" | "hive-web-shop";
  productId: string;
  itemId: string;
  quantity: number;
  createdAt: string;
}

export interface GrantResult {
  purchase: StorePurchase;
  inventory: InventoryEntry[];
  wallet: StoreWallet;
  duplicate: boolean;
}

export interface TestPointCreditResult {
  wallet: StoreWallet;
  duplicate: boolean;
}

export const initialTestPointBalance = 10_000;

export class InsufficientTestPointsError extends Error {
  public constructor(
    public readonly balance: number,
    public readonly required: number
  ) {
    super(`테스트 포인트가 부족합니다. 필요 ${required}P, 보유 ${balance}P`);
    this.name = "InsufficientTestPointsError";
  }
}

export interface MarketStore {
  getInventory(subject: string): Promise<InventoryEntry[]>;
  getEquipment(subject: string): Promise<StoreEquipment>;
  getWallet(subject: string): Promise<StoreWallet>;
  getPlayerState(subject: string): Promise<PlayerStoreState>;
  setMoldSkin(subject: string, itemId: StoreEquipment["moldSkin"]): Promise<PlayerStoreState>;
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
  creditTestPoints(
    subject: string,
    amount: number,
    idempotencyKey: string
  ): Promise<TestPointCreditResult>;
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
  private readonly equipment = new Map<string, StoreEquipment>();
  private readonly wallets = new Map<string, StoreWallet>();
  private readonly purchases = new Map<string, { subject: string; purchase: StorePurchase }>();
  private readonly testPointCredits = new Map<string, { subject: string; amount: number }>();

  public async getInventory(subject: string): Promise<InventoryEntry[]> {
    return sortedInventory(this.inventories.get(subject)?.values() ?? []);
  }

  public async getEquipment(subject: string): Promise<StoreEquipment> {
    return this.equipment.get(subject) ?? { moldSkin: null };
  }

  public async getWallet(subject: string): Promise<StoreWallet> {
    return { ...this.getOrCreateWallet(subject) };
  }

  public async getPlayerState(subject: string): Promise<PlayerStoreState> {
    const [inventory, equipment, wallet] = await Promise.all([
      this.getInventory(subject),
      this.getEquipment(subject),
      this.getWallet(subject)
    ]);
    return { inventory, equipment, wallet };
  }

  public async setMoldSkin(
    subject: string,
    itemId: StoreEquipment["moldSkin"]
  ): Promise<PlayerStoreState> {
    this.equipment.set(subject, { moldSkin: itemId });
    return this.getPlayerState(subject);
  }

  public async grantMockPurchase(
    subject: string,
    product: StoreProduct,
    idempotencyKey: string
  ): Promise<GrantResult> {
    const receiptKey = `mock:${idempotencyKey}`;
    const existing = this.purchases.get(receiptKey);
    if (existing) {
      if (
        existing.subject !== subject ||
        existing.purchase.productId !== product.id ||
        existing.purchase.quantity !== product.grant.quantity
      ) {
        throw new Error("이미 다른 구매에 사용된 transactionId입니다.");
      }
      return {
        purchase: existing.purchase,
        inventory: await this.getInventory(subject),
        wallet: await this.getWallet(subject),
        duplicate: true
      };
    }

    const wallet = this.getOrCreateWallet(subject);
    if (wallet.testPoints < product.testPointPrice) {
      throw new InsufficientTestPointsError(wallet.testPoints, product.testPointPrice);
    }
    wallet.testPoints -= product.testPointPrice;

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
        wallet: await this.getWallet(subject),
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

    return {
      purchase,
      inventory: await this.getInventory(subject),
      wallet: await this.getWallet(subject),
      duplicate: false
    };
  }

  public async creditTestPoints(
    subject: string,
    amount: number,
    idempotencyKey: string
  ): Promise<TestPointCreditResult> {
    const creditKey = `dev-test-points:${idempotencyKey}`;
    const existing = this.testPointCredits.get(creditKey);
    if (existing) {
      if (existing.subject !== subject || existing.amount !== amount) {
        throw new Error("이미 다른 충전에 사용된 idempotencyKey입니다.");
      }
      return { wallet: await this.getWallet(subject), duplicate: true };
    }

    this.getOrCreateWallet(subject).testPoints += amount;
    this.testPointCredits.set(creditKey, { subject, amount });
    return { wallet: await this.getWallet(subject), duplicate: false };
  }

  private getOrCreateWallet(subject: string): StoreWallet {
    const existing = this.wallets.get(subject);
    if (existing) return existing;
    const wallet = { testPoints: initialTestPointBalance };
    this.wallets.set(subject, wallet);
    return wallet;
  }
}

interface DynamoReceiptItem extends StorePurchase {
  PK: string;
  SK: "RECEIPT";
  subject: string;
}

interface DynamoTestPointCreditItem {
  PK: string;
  SK: "CREDIT";
  subject: string;
  amount: number;
  createdAt: string;
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

  public async getEquipment(subject: string): Promise<StoreEquipment> {
    const result = await this.client.send(
      new GetCommand({
        TableName: this.tableName,
        Key: { PK: `PLAYER#${subject}`, SK: "EQUIPMENT#MOLD" },
        ConsistentRead: true
      })
    );
    return {
      moldSkin: result.Item?.itemId === "golden-pan" ? "golden-pan" : null
    };
  }

  public async getWallet(subject: string): Promise<StoreWallet> {
    const key = { PK: `PLAYER#${subject}`, SK: "WALLET#TEST_POINTS" };
    const existing = await this.client.send(
      new GetCommand({ TableName: this.tableName, Key: key, ConsistentRead: true })
    );
    if (existing.Item) {
      return { testPoints: Math.max(0, Number(existing.Item.balance) || 0) };
    }

    try {
      await this.client.send(
        new PutCommand({
          TableName: this.tableName,
          Item: {
            ...key,
            balance: initialTestPointBalance,
            updatedAt: new Date().toISOString()
          },
          ConditionExpression: "attribute_not_exists(PK)"
        })
      );
      return { testPoints: initialTestPointBalance };
    } catch (error) {
      if ((error as { name?: string }).name !== "ConditionalCheckFailedException") throw error;
      const raced = await this.client.send(
        new GetCommand({ TableName: this.tableName, Key: key, ConsistentRead: true })
      );
      return { testPoints: Math.max(0, Number(raced.Item?.balance) || 0) };
    }
  }

  public async getPlayerState(subject: string): Promise<PlayerStoreState> {
    const [inventory, equipment, wallet] = await Promise.all([
      this.getInventory(subject),
      this.getEquipment(subject),
      this.getWallet(subject)
    ]);
    return { inventory, equipment, wallet };
  }

  public async setMoldSkin(
    subject: string,
    itemId: StoreEquipment["moldSkin"]
  ): Promise<PlayerStoreState> {
    await this.client.send(
      new PutCommand({
        TableName: this.tableName,
        Item: {
          PK: `PLAYER#${subject}`,
          SK: "EQUIPMENT#MOLD",
          itemId,
          updatedAt: new Date().toISOString()
        }
      })
    );
    return this.getPlayerState(subject);
  }

  public async grantMockPurchase(
    subject: string,
    product: StoreProduct,
    idempotencyKey: string
  ): Promise<GrantResult> {
    await this.getWallet(subject);
    const receiptKey = `RECEIPT#MOCK#${idempotencyKey}`;
    const existing = await this.readReceipt(receiptKey);
    if (existing) return this.duplicateResult(existing, subject, product, 1);

    const purchase: StorePurchase = {
      purchaseId: randomUUID(),
      provider: "mock",
      productId: product.id,
      itemId: product.grant.itemId,
      quantity: product.grant.quantity,
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
                Key: { PK: `PLAYER#${subject}`, SK: "WALLET#TEST_POINTS" },
                UpdateExpression: "SET balance = balance - :cost, updatedAt = :updatedAt",
                ConditionExpression: "balance >= :cost",
                ExpressionAttributeValues: {
                  ":cost": product.testPointPrice,
                  ":updatedAt": purchase.createdAt
                }
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
      if (racedReceipt) return this.duplicateResult(racedReceipt, subject, product, 1);
      const wallet = await this.getWallet(subject);
      if (wallet.testPoints < product.testPointPrice) {
        throw new InsufficientTestPointsError(wallet.testPoints, product.testPointPrice);
      }
      throw error;
    }

    return {
      purchase,
      inventory: await this.getInventory(subject),
      wallet: await this.getWallet(subject),
      duplicate: false
    };
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

    return {
      purchase,
      inventory: await this.getInventory(subject),
      wallet: await this.getWallet(subject),
      duplicate: false
    };
  }

  public async creditTestPoints(
    subject: string,
    amount: number,
    idempotencyKey: string
  ): Promise<TestPointCreditResult> {
    await this.getWallet(subject);
    const receiptKey = `RECEIPT#DEV-TEST-POINTS#${idempotencyKey}`;
    const existing = await this.readTestPointCredit(receiptKey);
    if (existing) return this.duplicateCreditResult(existing, subject, amount);

    const createdAt = new Date().toISOString();
    try {
      await this.client.send(
        new TransactWriteCommand({
          TransactItems: [
            {
              Put: {
                TableName: this.tableName,
                Item: {
                  PK: receiptKey,
                  SK: "CREDIT",
                  subject,
                  amount,
                  createdAt
                },
                ConditionExpression: "attribute_not_exists(PK)"
              }
            },
            {
              Update: {
                TableName: this.tableName,
                Key: { PK: `PLAYER#${subject}`, SK: "WALLET#TEST_POINTS" },
                UpdateExpression: "SET updatedAt = :updatedAt ADD balance :amount",
                ExpressionAttributeValues: { ":updatedAt": createdAt, ":amount": amount }
              }
            }
          ]
        })
      );
    } catch (error) {
      const raced = await this.readTestPointCredit(receiptKey);
      if (raced) return this.duplicateCreditResult(raced, subject, amount);
      throw error;
    }

    return { wallet: await this.getWallet(subject), duplicate: false };
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

  private async readTestPointCredit(
    receiptKey: string
  ): Promise<DynamoTestPointCreditItem | undefined> {
    const result = await this.client.send(
      new GetCommand({
        TableName: this.tableName,
        Key: { PK: receiptKey, SK: "CREDIT" },
        ConsistentRead: true
      })
    );
    return result.Item as DynamoTestPointCreditItem | undefined;
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
    return {
      purchase,
      inventory: await this.getInventory(subject),
      wallet: await this.getWallet(subject),
      duplicate: true
    };
  }

  private async duplicateCreditResult(
    existing: DynamoTestPointCreditItem,
    subject: string,
    amount: number
  ): Promise<TestPointCreditResult> {
    if (existing.subject !== subject || existing.amount !== amount) {
      throw new Error("이미 다른 충전에 사용된 idempotencyKey입니다.");
    }
    return { wallet: await this.getWallet(subject), duplicate: true };
  }
}

export function createMarketStore(config: AppConfig): MarketStore {
  if (config.store.dataStore === "dynamodb") {
    if (!config.store.dynamodbTable) throw new Error("DynamoDB 테이블 설정이 없습니다.");
    return new DynamoDbMarketStore(config.store.dynamodbTable);
  }
  return new InMemoryMarketStore();
}

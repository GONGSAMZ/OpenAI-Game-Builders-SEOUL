import { randomUUID } from "node:crypto";
import { DynamoDBClient, type DynamoDBClientConfig } from "@aws-sdk/client-dynamodb";
import {
  DynamoDBDocumentClient,
  GetCommand,
  PutCommand,
  UpdateCommand
} from "@aws-sdk/lib-dynamodb";
import type { AppConfig } from "../../config.js";
import type { StoreProduct } from "../../store/catalog.js";

export interface NicePayOrder {
  orderId: string;
  subject: string;
  playerId?: string;
  productId: string;
  productSnapshot?: StoreProduct;
  goodsName: string;
  amount: number;
  status: "pending" | "paid";
  tid?: string;
  createdAt: string;
  expiresAtEpoch: number;
}

export interface CreateNicePayOrderInput {
  subject: string;
  playerId?: string;
  productId: string;
  productSnapshot: StoreProduct;
  goodsName: string;
  amount: number;
}

export interface NicePayOrderStore {
  create(input: CreateNicePayOrderInput): Promise<NicePayOrder>;
  get(orderId: string): Promise<NicePayOrder | undefined>;
  markPaid(orderId: string, tid: string): Promise<NicePayOrder>;
}

function newOrder(input: CreateNicePayOrderInput): NicePayOrder {
  return {
    orderId: `NP_${randomUUID().replaceAll("-", "")}`,
    ...input,
    status: "pending",
    createdAt: new Date().toISOString(),
    expiresAtEpoch: Math.floor(Date.now() / 1000) + 15 * 60
  };
}

export class InMemoryNicePayOrderStore implements NicePayOrderStore {
  private readonly orders = new Map<string, NicePayOrder>();

  public async create(input: CreateNicePayOrderInput): Promise<NicePayOrder> {
    const order = newOrder(input);
    this.orders.set(order.orderId, order);
    return { ...order };
  }

  public async get(orderId: string): Promise<NicePayOrder | undefined> {
    const order = this.orders.get(orderId);
    return order ? { ...order } : undefined;
  }

  public async markPaid(orderId: string, tid: string): Promise<NicePayOrder> {
    const order = this.orders.get(orderId);
    if (!order) throw new Error("NICEPAY 주문을 찾을 수 없습니다.");
    if (order.status === "paid") {
      if (order.tid !== tid) throw new Error("주문에 다른 거래번호가 이미 연결되어 있습니다.");
      return { ...order };
    }
    const updated: NicePayOrder = { ...order, status: "paid", tid };
    this.orders.set(orderId, updated);
    return { ...updated };
  }
}

interface DynamoNicePayOrder extends NicePayOrder {
  PK: string;
  SK: "ORDER";
}

export class DynamoDbNicePayOrderStore implements NicePayOrderStore {
  private readonly client: DynamoDBDocumentClient;

  public constructor(
    private readonly tableName: string,
    clientConfig: DynamoDBClientConfig = {}
  ) {
    this.client = DynamoDBDocumentClient.from(new DynamoDBClient(clientConfig), {
      marshallOptions: { removeUndefinedValues: true }
    });
  }

  public async create(input: CreateNicePayOrderInput): Promise<NicePayOrder> {
    const order = newOrder(input);
    await this.client.send(
      new PutCommand({
        TableName: this.tableName,
        Item: { PK: `NICEPAY_ORDER#${order.orderId}`, SK: "ORDER", ...order },
        ConditionExpression: "attribute_not_exists(PK)"
      })
    );
    return order;
  }

  public async get(orderId: string): Promise<NicePayOrder | undefined> {
    const result = await this.client.send(
      new GetCommand({
        TableName: this.tableName,
        Key: { PK: `NICEPAY_ORDER#${orderId}`, SK: "ORDER" },
        ConsistentRead: true
      })
    );
    if (!result.Item) return undefined;
    const { PK: _pk, SK: _sk, ...order } = result.Item as DynamoNicePayOrder;
    return order;
  }

  public async markPaid(orderId: string, tid: string): Promise<NicePayOrder> {
    try {
      await this.client.send(
        new UpdateCommand({
          TableName: this.tableName,
          Key: { PK: `NICEPAY_ORDER#${orderId}`, SK: "ORDER" },
          UpdateExpression: "SET #status = :paid, tid = :tid, paidAt = :paidAt",
          ConditionExpression: "#status = :pending",
          ExpressionAttributeNames: { "#status": "status" },
          ExpressionAttributeValues: {
            ":paid": "paid",
            ":pending": "pending",
            ":tid": tid,
            ":paidAt": new Date().toISOString()
          }
        })
      );
    } catch (error) {
      if ((error as { name?: string }).name !== "ConditionalCheckFailedException") throw error;
    }

    const order = await this.get(orderId);
    if (!order) throw new Error("NICEPAY 주문을 찾을 수 없습니다.");
    if (order.status !== "paid" || order.tid !== tid) {
      throw new Error("주문에 다른 거래번호가 이미 연결되어 있습니다.");
    }
    return order;
  }
}

export function createNicePayOrderStore(config: AppConfig): NicePayOrderStore {
  if (config.store.dataStore === "dynamodb") {
    if (!config.store.dynamodbTable) throw new Error("DynamoDB 테이블 설정이 없습니다.");
    return new DynamoDbNicePayOrderStore(config.store.dynamodbTable);
  }
  return new InMemoryNicePayOrderStore();
}

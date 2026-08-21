import { createHmac, randomUUID, timingSafeEqual } from "node:crypto";
import { DynamoDBClient, type DynamoDBClientConfig } from "@aws-sdk/client-dynamodb";
import {
  DynamoDBDocumentClient,
  PutCommand,
  QueryCommand,
  UpdateCommand
} from "@aws-sdk/lib-dynamodb";
import type { AppConfig } from "../config.js";
import type { StoreProduct } from "./catalog.js";

export type PurchaseProvider = "mock" | "nicepay-test" | "hive-web-shop";
export type PurchaseStatus = "pending" | "succeeded" | "failed" | "cancelled" | "expired";

export interface PurchaseHistoryEntry {
  purchaseId: string;
  provider: PurchaseProvider;
  productId: string;
  productName: string;
  itemId: string;
  quantity: number;
  amount: number;
  currency: "KRW";
  status: PurchaseStatus;
  createdAt: string;
  updatedAt: string;
  errorCode?: string;
}

export interface StartPurchaseAttemptInput {
  provider: PurchaseProvider;
  attemptId: string;
  product: StoreProduct;
  quantity?: number;
  expiresAtEpoch?: number;
}

export interface PurchaseHistoryPage {
  purchases: PurchaseHistoryEntry[];
  nextCursor: string | null;
}

export interface PurchaseHistoryStore {
  start(subject: string, input: StartPurchaseAttemptInput): Promise<PurchaseHistoryEntry>;
  finish(
    subject: string,
    provider: PurchaseProvider,
    attemptId: string,
    status: Exclude<PurchaseStatus, "pending">,
    errorCode?: string
  ): Promise<void>;
  list(subject: string, limit: number, cursor?: string): Promise<PurchaseHistoryPage>;
}

interface StoredAttempt extends PurchaseHistoryEntry {
  attemptId: string;
  subject: string;
  expiresAtEpoch?: number;
}

function publicEntry(stored: StoredAttempt): PurchaseHistoryEntry {
  const { attemptId: _attemptId, subject: _subject, expiresAtEpoch, ...entry } = stored;
  if (entry.status === "pending" && expiresAtEpoch && expiresAtEpoch <= Math.floor(Date.now() / 1000)) {
    return { ...entry, status: "expired", updatedAt: new Date(expiresAtEpoch * 1000).toISOString() };
  }
  return entry;
}

function createAttempt(subject: string, input: StartPurchaseAttemptInput): StoredAttempt {
  const createdAt = new Date().toISOString();
  const quantity = input.product.grant.quantity * (input.quantity ?? 1);
  return {
    purchaseId: randomUUID(),
    attemptId: input.attemptId,
    subject,
    provider: input.provider,
    productId: input.product.id,
    productName: input.product.name,
    itemId: input.product.grant.itemId,
    quantity,
    amount: input.product.priceKrw * (input.quantity ?? 1),
    currency: "KRW",
    status: "pending",
    createdAt,
    updatedAt: createdAt,
    expiresAtEpoch: input.expiresAtEpoch
  };
}

function attemptKey(provider: PurchaseProvider, attemptId: string): string {
  return `${provider}:${attemptId}`;
}

function assertSameAttempt(existing: StoredAttempt, subject: string, input: StartPurchaseAttemptInput): void {
  if (
    existing.subject !== subject ||
    existing.provider !== input.provider ||
    existing.productId !== input.product.id ||
    existing.quantity !== input.product.grant.quantity * (input.quantity ?? 1)
  ) {
    throw new Error("이미 다른 구매 시도에 사용된 식별자입니다.");
  }
}

interface CursorPayload {
  subject: string;
  offset: number;
}

function encodeCursor(payload: unknown, secret: string): string {
  const body = Buffer.from(JSON.stringify(payload), "utf8").toString("base64url");
  const signature = createHmac("sha256", secret).update(body).digest("base64url");
  return `${body}.${signature}`;
}

function decodeCursor<T>(cursor: string, secret: string): T {
  const [body, signature, ...extra] = cursor.split(".");
  if (!body || !signature || extra.length > 0) throw new Error("invalid cursor");
  const expected = createHmac("sha256", secret).update(body).digest();
  const received = Buffer.from(signature, "base64url");
  if (received.length !== expected.length || !timingSafeEqual(received, expected)) {
    throw new Error("invalid cursor");
  }
  return JSON.parse(Buffer.from(body, "base64url").toString("utf8")) as T;
}

function decodeMemoryCursor(cursor: string | undefined, subject: string, secret: string): number {
  if (!cursor) return 0;
  try {
    const parsed = decodeCursor<CursorPayload>(cursor, secret);
    if (parsed.subject !== subject || !Number.isSafeInteger(parsed.offset) || parsed.offset < 0) {
      throw new Error("invalid cursor");
    }
    return parsed.offset;
  } catch {
    throw new Error("구매 내역 cursor가 올바르지 않습니다.");
  }
}

export class InMemoryPurchaseHistoryStore implements PurchaseHistoryStore {
  private readonly attempts = new Map<string, StoredAttempt>();

  public constructor(private readonly cursorSigningSecret = "purchase-history-test-secret") {}

  public async start(subject: string, input: StartPurchaseAttemptInput): Promise<PurchaseHistoryEntry> {
    const key = attemptKey(input.provider, input.attemptId);
    const existing = this.attempts.get(key);
    if (existing) {
      assertSameAttempt(existing, subject, input);
      return publicEntry(existing);
    }
    const created = createAttempt(subject, input);
    this.attempts.set(key, created);
    return publicEntry(created);
  }

  public async finish(
    subject: string,
    provider: PurchaseProvider,
    attemptId: string,
    status: Exclude<PurchaseStatus, "pending">,
    errorCode?: string
  ): Promise<void> {
    const existing = this.attempts.get(attemptKey(provider, attemptId));
    if (!existing || existing.subject !== subject) return;
    if (existing.status === "succeeded" && status !== "succeeded") return;
    existing.status = status;
    existing.errorCode = errorCode;
    existing.updatedAt = new Date().toISOString();
  }

  public async list(subject: string, limit: number, cursor?: string): Promise<PurchaseHistoryPage> {
    const offset = decodeMemoryCursor(cursor, subject, this.cursorSigningSecret);
    const all = [...this.attempts.values()]
      .filter((entry) => entry.subject === subject)
      .sort((left, right) => right.createdAt.localeCompare(left.createdAt));
    const purchases = all.slice(offset, offset + limit).map(publicEntry);
    const nextOffset = offset + purchases.length;
    return {
      purchases,
      nextCursor: nextOffset < all.length
        ? encodeCursor({ subject, offset: nextOffset }, this.cursorSigningSecret)
        : null
    };
  }
}

interface DynamoCursorPayload {
  subject: string;
  key: Record<string, unknown>;
}

interface DynamoAttemptItem extends StoredAttempt {
  PK: string;
  SK: "PURCHASE_ATTEMPT";
  recordType: "PURCHASE_ATTEMPT";
}

function encodeDynamoCursor(
  subject: string,
  key: Record<string, unknown>,
  secret: string
): string {
  return encodeCursor({ subject, key } satisfies DynamoCursorPayload, secret);
}

function decodeDynamoCursor(
  cursor: string | undefined,
  subject: string,
  secret: string
): Record<string, unknown> | undefined {
  if (!cursor) return undefined;
  try {
    const parsed = decodeCursor<DynamoCursorPayload>(cursor, secret);
    if (parsed.subject !== subject || !parsed.key || typeof parsed.key !== "object") {
      throw new Error("invalid cursor");
    }
    return parsed.key;
  } catch {
    throw new Error("구매 내역 cursor가 올바르지 않습니다.");
  }
}

export class DynamoDbPurchaseHistoryStore implements PurchaseHistoryStore {
  private readonly client: DynamoDBDocumentClient;

  public constructor(
    private readonly tableName: string,
    private readonly cursorSigningSecret = "purchase-history-test-secret",
    clientConfig: DynamoDBClientConfig = {}
  ) {
    this.client = DynamoDBDocumentClient.from(new DynamoDBClient(clientConfig), {
      marshallOptions: { removeUndefinedValues: true }
    });
  }

  public async start(subject: string, input: StartPurchaseAttemptInput): Promise<PurchaseHistoryEntry> {
    const created = createAttempt(subject, input);
    const item: DynamoAttemptItem = {
      PK: `PURCHASE_ATTEMPT#${input.provider}#${input.attemptId}`,
      SK: "PURCHASE_ATTEMPT",
      recordType: "PURCHASE_ATTEMPT",
      ...created
    };
    try {
      await this.client.send(new PutCommand({
        TableName: this.tableName,
        Item: item,
        ConditionExpression: "attribute_not_exists(PK)"
      }));
      return publicEntry(created);
    } catch (error) {
      if ((error as { name?: string }).name !== "ConditionalCheckFailedException") throw error;
      const existing = await this.read(input.provider, input.attemptId);
      if (!existing) throw error;
      assertSameAttempt(existing, subject, input);
      return publicEntry(existing);
    }
  }

  public async finish(
    subject: string,
    provider: PurchaseProvider,
    attemptId: string,
    status: Exclude<PurchaseStatus, "pending">,
    errorCode?: string
  ): Promise<void> {
    try {
      await this.client.send(new UpdateCommand({
        TableName: this.tableName,
        Key: { PK: `PURCHASE_ATTEMPT#${provider}#${attemptId}`, SK: "PURCHASE_ATTEMPT" },
        UpdateExpression: "SET #status = :status, updatedAt = :updatedAt, errorCode = :errorCode",
        ConditionExpression: "subject = :subject AND (#status <> :succeeded OR :status = :succeeded)",
        ExpressionAttributeNames: { "#status": "status" },
        ExpressionAttributeValues: {
          ":subject": subject,
          ":status": status,
          ":succeeded": "succeeded",
          ":updatedAt": new Date().toISOString(),
          ":errorCode": errorCode ?? null
        }
      }));
    } catch (error) {
      if ((error as { name?: string }).name !== "ConditionalCheckFailedException") throw error;
    }
  }

  public async list(subject: string, limit: number, cursor?: string): Promise<PurchaseHistoryPage> {
    const result = await this.client.send(new QueryCommand({
      TableName: this.tableName,
      IndexName: "SubjectCreatedAtIndex",
      KeyConditionExpression: "subject = :subject",
      FilterExpression:
        "recordType = :attempt OR (#sk = :receipt AND attribute_not_exists(historyVersion))",
      ExpressionAttributeNames: { "#sk": "SK" },
      ExpressionAttributeValues: {
        ":subject": subject,
        ":attempt": "PURCHASE_ATTEMPT",
        ":receipt": "RECEIPT"
      },
      ExclusiveStartKey: decodeDynamoCursor(cursor, subject, this.cursorSigningSecret),
      ScanIndexForward: false,
      Limit: limit
    }));

    const purchases = (result.Items ?? []).map((raw) => {
      if (raw.recordType === "PURCHASE_ATTEMPT") {
        const { PK: _pk, SK: _sk, recordType: _recordType, ...stored } = raw as unknown as DynamoAttemptItem;
        return publicEntry(stored);
      }
      const createdAt = String(raw.createdAt ?? "");
      return {
        purchaseId: String(raw.purchaseId ?? randomUUID()),
        provider: String(raw.provider ?? "mock") as PurchaseProvider,
        productId: String(raw.productId ?? "unknown"),
        productName: String(raw.productName ?? raw.productId ?? "이전 구매"),
        itemId: String(raw.itemId ?? "unknown"),
        quantity: Number(raw.quantity) || 0,
        amount: Number(raw.amount) || 0,
        currency: "KRW" as const,
        status: "succeeded" as const,
        createdAt,
        updatedAt: String(raw.updatedAt ?? createdAt)
      };
    });

    return {
      purchases,
      nextCursor: result.LastEvaluatedKey
        ? encodeDynamoCursor(subject, result.LastEvaluatedKey, this.cursorSigningSecret)
        : null
    };
  }

  private async read(provider: PurchaseProvider, attemptId: string): Promise<StoredAttempt | undefined> {
    const result = await this.client.send(new QueryCommand({
      TableName: this.tableName,
      KeyConditionExpression: "PK = :pk AND SK = :sk",
      ExpressionAttributeValues: {
        ":pk": `PURCHASE_ATTEMPT#${provider}#${attemptId}`,
        ":sk": "PURCHASE_ATTEMPT"
      },
      ConsistentRead: true,
      Limit: 1
    }));
    if (!result.Items?.[0]) return undefined;
    const { PK: _pk, SK: _sk, recordType: _recordType, ...stored } = result.Items[0] as unknown as DynamoAttemptItem;
    return stored;
  }
}

export function createPurchaseHistoryStore(config: AppConfig): PurchaseHistoryStore {
  if (config.store.dataStore === "dynamodb") {
    if (!config.store.dynamodbTable) throw new Error("DynamoDB 테이블 설정이 없습니다.");
    return new DynamoDbPurchaseHistoryStore(
      config.store.dynamodbTable,
      config.store.cursorSigningSecret
    );
  }
  return new InMemoryPurchaseHistoryStore(config.store.cursorSigningSecret);
}

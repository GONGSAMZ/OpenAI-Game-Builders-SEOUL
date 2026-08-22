import { DynamoDBClient } from "@aws-sdk/client-dynamodb";
import {
  DynamoDBDocumentClient,
  GetCommand,
  PutCommand,
  TransactWriteCommand
} from "@aws-sdk/lib-dynamodb";
import type { AppConfig } from "../config.js";

export interface PlayerSaveProfile {
  schemaVersion: number;
  revision: number;
  updatedAt: string;
  run: Record<string, unknown>;
  account: Record<string, unknown>;
  settings?: Record<string, unknown>;
}

export class SaveRevisionConflictError extends Error {
  public constructor(public readonly current?: PlayerSaveProfile) {
    super("다른 기기에서 저장 데이터가 변경되었습니다.");
    this.name = "SaveRevisionConflictError";
  }
}

export class SaveIdempotencyConflictError extends Error {
  public constructor() {
    super("이미 다른 요청에 사용된 Idempotency-Key입니다.");
    this.name = "SaveIdempotencyConflictError";
  }
}

export interface PlayerSaveMutation {
  operation: string;
  idempotencyKey: string;
  fingerprint: string;
  mutate(current?: PlayerSaveProfile): PlayerSaveProfile;
}

export interface PlayerSaveMutationResult {
  profile: PlayerSaveProfile;
  duplicate: boolean;
}

export interface PlayerSaveStore {
  get(subject: string): Promise<PlayerSaveProfile | undefined>;
  put(
    subject: string,
    expectedRevision: number,
    profile: PlayerSaveProfile
  ): Promise<PlayerSaveProfile>;
  mutate(
    subject: string,
    expectedRevision: number,
    mutation: PlayerSaveMutation
  ): Promise<PlayerSaveMutationResult>;
}

function nextProfile(
  expectedRevision: number,
  profile: PlayerSaveProfile
): PlayerSaveProfile {
  return {
    ...profile,
    revision: expectedRevision + 1,
    updatedAt: new Date().toISOString()
  };
}

export class InMemoryPlayerSaveStore implements PlayerSaveStore {
  private readonly profiles = new Map<string, PlayerSaveProfile>();
  private readonly mutations = new Map<
    string,
    { subject: string; fingerprint: string; resultRevision: number }
  >();

  public async get(subject: string): Promise<PlayerSaveProfile | undefined> {
    const profile = this.profiles.get(subject);
    return profile ? structuredClone(profile) : undefined;
  }

  public async put(
    subject: string,
    expectedRevision: number,
    profile: PlayerSaveProfile
  ): Promise<PlayerSaveProfile> {
    const current = this.profiles.get(subject);
    if ((current?.revision ?? 0) !== expectedRevision) {
      throw new SaveRevisionConflictError(current ? structuredClone(current) : undefined);
    }

    const saved = nextProfile(expectedRevision, profile);
    this.profiles.set(subject, structuredClone(saved));
    return structuredClone(saved);
  }

  public async mutate(
    subject: string,
    expectedRevision: number,
    mutation: PlayerSaveMutation
  ): Promise<PlayerSaveMutationResult> {
    const receiptKey = `${mutation.operation}:${mutation.idempotencyKey}`;
    const existing = this.mutations.get(receiptKey);
    if (existing) {
      if (existing.subject !== subject || existing.fingerprint !== mutation.fingerprint) {
        throw new SaveIdempotencyConflictError();
      }
      const current = this.profiles.get(subject);
      if (!current) throw new Error("멱등 처리된 저장 프로필을 찾을 수 없습니다.");
      return { profile: structuredClone(current), duplicate: true };
    }

    const current = this.profiles.get(subject);
    if ((current?.revision ?? 0) !== expectedRevision) {
      throw new SaveRevisionConflictError(current ? structuredClone(current) : undefined);
    }

    const saved = nextProfile(
      expectedRevision,
      mutation.mutate(current ? structuredClone(current) : undefined)
    );
    this.profiles.set(subject, structuredClone(saved));
    this.mutations.set(receiptKey, {
      subject,
      fingerprint: mutation.fingerprint,
      resultRevision: saved.revision
    });
    return { profile: structuredClone(saved), duplicate: false };
  }
}

interface DynamoPlayerSaveItem {
  PK: string;
  SK: "SAVE#MAIN";
  revision: number;
  updatedAt: string;
  profile: PlayerSaveProfile;
}

interface DynamoSaveMutationReceipt {
  PK: string;
  SK: "RECEIPT";
  ownerSubject: string;
  operation: string;
  fingerprint: string;
  resultRevision: number;
  auditCreatedAt: string;
}

export class DynamoDbPlayerSaveStore implements PlayerSaveStore {
  private readonly client: DynamoDBDocumentClient;

  public constructor(private readonly tableName: string) {
    this.client = DynamoDBDocumentClient.from(new DynamoDBClient({}));
  }

  public async get(subject: string): Promise<PlayerSaveProfile | undefined> {
    const result = await this.client.send(
      new GetCommand({
        TableName: this.tableName,
        Key: { PK: `PLAYER#${subject}`, SK: "SAVE#MAIN" },
        ConsistentRead: true
      })
    );
    return (result.Item as DynamoPlayerSaveItem | undefined)?.profile;
  }

  public async put(
    subject: string,
    expectedRevision: number,
    profile: PlayerSaveProfile
  ): Promise<PlayerSaveProfile> {
    const saved = nextProfile(expectedRevision, profile);
    const condition = expectedRevision === 0
      ? "attribute_not_exists(PK)"
      : "revision = :expectedRevision";

    try {
      await this.client.send(
        new PutCommand({
          TableName: this.tableName,
          Item: {
            PK: `PLAYER#${subject}`,
            SK: "SAVE#MAIN",
            revision: saved.revision,
            updatedAt: saved.updatedAt,
            profile: saved
          } satisfies DynamoPlayerSaveItem,
          ConditionExpression: condition,
          ExpressionAttributeValues: expectedRevision === 0
            ? undefined
            : { ":expectedRevision": expectedRevision }
        })
      );
    } catch (error) {
      if (error instanceof Error && error.name === "ConditionalCheckFailedException") {
        throw new SaveRevisionConflictError(await this.get(subject));
      }
      throw error;
    }

    return saved;
  }

  public async mutate(
    subject: string,
    expectedRevision: number,
    mutation: PlayerSaveMutation
  ): Promise<PlayerSaveMutationResult> {
    const receiptKey = `RUN_OPERATION#${mutation.operation}#${mutation.idempotencyKey}`;
    const existing = await this.readMutationReceipt(receiptKey);
    if (existing) return this.duplicateMutationResult(existing, subject, mutation);

    const current = await this.get(subject);
    if ((current?.revision ?? 0) !== expectedRevision) {
      throw new SaveRevisionConflictError(current);
    }

    const saved = nextProfile(expectedRevision, mutation.mutate(current));
    const createdAt = new Date().toISOString();
    const condition = expectedRevision === 0
      ? "attribute_not_exists(PK)"
      : "revision = :expectedRevision";

    try {
      await this.client.send(new TransactWriteCommand({
        TransactItems: [
          {
            Put: {
              TableName: this.tableName,
              Item: {
                PK: receiptKey,
                SK: "RECEIPT",
                // subject/createdAt GSI에는 넣지 않아 HIVE 구매 내역 UI와 섞이지 않게 한다.
                ownerSubject: subject,
                operation: mutation.operation,
                fingerprint: mutation.fingerprint,
                resultRevision: saved.revision,
                // 일반 상점·정산 감사와 장기 재시도 중복 방지를 위해 영구 보존한다.
                auditCreatedAt: createdAt
              } satisfies DynamoSaveMutationReceipt,
              ConditionExpression: "attribute_not_exists(PK)"
            }
          },
          {
            Put: {
              TableName: this.tableName,
              Item: {
                PK: `PLAYER#${subject}`,
                SK: "SAVE#MAIN",
                revision: saved.revision,
                updatedAt: saved.updatedAt,
                profile: saved
              } satisfies DynamoPlayerSaveItem,
              ConditionExpression: condition,
              ExpressionAttributeValues: expectedRevision === 0
                ? undefined
                : { ":expectedRevision": expectedRevision }
            }
          }
        ]
      }));
    } catch (error) {
      const racedReceipt = await this.readMutationReceipt(receiptKey);
      if (racedReceipt) {
        return this.duplicateMutationResult(racedReceipt, subject, mutation);
      }
      if ((error as { name?: string }).name === "TransactionCanceledException") {
        throw new SaveRevisionConflictError(await this.get(subject));
      }
      throw error;
    }

    return { profile: saved, duplicate: false };
  }

  private async readMutationReceipt(
    receiptKey: string
  ): Promise<DynamoSaveMutationReceipt | undefined> {
    const result = await this.client.send(new GetCommand({
      TableName: this.tableName,
      Key: { PK: receiptKey, SK: "RECEIPT" },
      ConsistentRead: true
    }));
    return result.Item as DynamoSaveMutationReceipt | undefined;
  }

  private async duplicateMutationResult(
    existing: DynamoSaveMutationReceipt,
    subject: string,
    mutation: PlayerSaveMutation
  ): Promise<PlayerSaveMutationResult> {
    if (existing.ownerSubject !== subject || existing.fingerprint !== mutation.fingerprint) {
      throw new SaveIdempotencyConflictError();
    }
    const profile = await this.get(subject);
    if (!profile) throw new Error("멱등 처리된 저장 프로필을 찾을 수 없습니다.");
    return { profile, duplicate: true };
  }
}

export function createPlayerSaveStore(config: AppConfig): PlayerSaveStore {
  if (config.store.dataStore === "dynamodb") {
    if (!config.store.dynamodbTable) throw new Error("DynamoDB 테이블 설정이 없습니다.");
    return new DynamoDbPlayerSaveStore(config.store.dynamodbTable);
  }
  return new InMemoryPlayerSaveStore();
}

import { DynamoDBClient } from "@aws-sdk/client-dynamodb";
import {
  DynamoDBDocumentClient,
  GetCommand,
  PutCommand
} from "@aws-sdk/lib-dynamodb";
import type { AppConfig } from "../config.js";

export interface PlayerSaveProfile {
  schemaVersion: number;
  revision: number;
  updatedAt: string;
  run: Record<string, unknown>;
  account: Record<string, unknown>;
}

export class SaveRevisionConflictError extends Error {
  public constructor(public readonly current?: PlayerSaveProfile) {
    super("다른 기기에서 저장 데이터가 변경되었습니다.");
    this.name = "SaveRevisionConflictError";
  }
}

export interface PlayerSaveStore {
  get(subject: string): Promise<PlayerSaveProfile | undefined>;
  put(
    subject: string,
    expectedRevision: number,
    profile: PlayerSaveProfile
  ): Promise<PlayerSaveProfile>;
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
}

interface DynamoPlayerSaveItem {
  PK: string;
  SK: "SAVE#MAIN";
  revision: number;
  updatedAt: string;
  profile: PlayerSaveProfile;
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
}

export function createPlayerSaveStore(config: AppConfig): PlayerSaveStore {
  if (config.store.dataStore === "dynamodb") {
    if (!config.store.dynamodbTable) throw new Error("DynamoDB 테이블 설정이 없습니다.");
    return new DynamoDbPlayerSaveStore(config.store.dynamodbTable);
  }
  return new InMemoryPlayerSaveStore();
}

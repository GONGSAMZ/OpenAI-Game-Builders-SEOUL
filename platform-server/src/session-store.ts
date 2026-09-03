import { randomBytes } from "node:crypto";
import {
  DynamoDBClient,
  type DynamoDBClientConfig
} from "@aws-sdk/client-dynamodb";
import {
  DynamoDBDocumentClient,
  GetCommand,
  PutCommand,
  UpdateCommand
} from "@aws-sdk/lib-dynamodb";
import type { AppConfig } from "./config.js";

type DynamoDocumentSender = Pick<DynamoDBDocumentClient, "send">;

export interface SessionIdentity {
  subject: string;
  provider: "hive" | "mock-hive";
  playerId?: string;
  idpIndex?: number;
  idpUserId?: string;
}

export interface GameSession extends SessionIdentity {
  token: string;
  createdAt: string;
  expiresAt: string;
}

export interface SessionStore {
  create(identity: SessionIdentity): Promise<GameSession>;
  get(token: string): Promise<GameSession | undefined>;
  delete(token: string): Promise<void>;
}

function createGameSession(
  identity: SessionIdentity,
  ttlSeconds: number,
  now = Date.now()
): GameSession {
  return {
    ...identity,
    token: randomBytes(32).toString("base64url"),
    createdAt: new Date(now).toISOString(),
    expiresAt: new Date(now + ttlSeconds * 1000).toISOString()
  };
}

export class InMemorySessionStore implements SessionStore {
  private readonly sessions = new Map<string, GameSession>();

  public constructor(
    private readonly ttlSeconds: number,
    private readonly now: () => number = Date.now
  ) {}

  public async create(identity: SessionIdentity): Promise<GameSession> {
    const session = createGameSession(identity, this.ttlSeconds, this.now());
    this.sessions.set(session.token, session);
    return session;
  }

  public async get(token: string): Promise<GameSession | undefined> {
    const session = this.sessions.get(token);
    if (!session) return undefined;

    const now = this.now();
    if (Date.parse(session.expiresAt) <= now) {
      this.sessions.delete(token);
      return undefined;
    }

    if (Date.parse(session.expiresAt) - now > this.ttlSeconds * 500) return session;

    const refreshed = {
      ...session,
      expiresAt: new Date(now + this.ttlSeconds * 1000).toISOString()
    };
    this.sessions.set(token, refreshed);
    return refreshed;
  }

  public async delete(token: string): Promise<void> {
    this.sessions.delete(token);
  }
}

interface DynamoSessionItem extends GameSession {
  PK: string;
  SK: "SESSION";
  expiresAtEpoch: number;
}

export class DynamoDbSessionStore implements SessionStore {
  private readonly client: DynamoDocumentSender;

  public constructor(
    private readonly tableName: string,
    private readonly ttlSeconds: number,
    clientConfig: DynamoDBClientConfig = {},
    private readonly now: () => number = Date.now,
    client?: DynamoDocumentSender
  ) {
    this.client = client ?? DynamoDBDocumentClient.from(new DynamoDBClient(clientConfig), {
      marshallOptions: { removeUndefinedValues: true }
    });
  }

  public async create(identity: SessionIdentity): Promise<GameSession> {
    const session = createGameSession(identity, this.ttlSeconds, this.now());
    const item: DynamoSessionItem = {
      PK: `SESSION#${session.token}`,
      SK: "SESSION",
      expiresAtEpoch: Math.floor(Date.parse(session.expiresAt) / 1000),
      ...session
    };
    await this.client.send(new PutCommand({ TableName: this.tableName, Item: item }));
    return session;
  }

  public async get(token: string): Promise<GameSession | undefined> {
    const result = await this.client.send(
      new GetCommand({
        TableName: this.tableName,
        Key: { PK: `SESSION#${token}`, SK: "SESSION" },
        ConsistentRead: true
      })
    );
    const item = result.Item as DynamoSessionItem | undefined;
    const now = this.now();
    const nowEpoch = Math.floor(now / 1000);
    if (!item || item.expiresAtEpoch <= nowEpoch) return undefined;

    if (item.expiresAtEpoch - nowEpoch <= Math.floor(this.ttlSeconds / 2)) {
      const expiresAt = new Date(now + this.ttlSeconds * 1000).toISOString();
      const expiresAtEpoch = Math.floor(Date.parse(expiresAt) / 1000);
      let refreshed;
      try {
        refreshed = await this.client.send(new UpdateCommand({
          TableName: this.tableName,
          Key: { PK: `SESSION#${token}`, SK: "SESSION" },
          UpdateExpression: "SET expiresAt = :expiresAt, expiresAtEpoch = :expiresAtEpoch",
          ConditionExpression: "expiresAtEpoch > :now",
          ExpressionAttributeValues: {
            ":expiresAt": expiresAt,
            ":expiresAtEpoch": expiresAtEpoch,
            ":now": nowEpoch
          },
          ReturnValues: "ALL_NEW"
        }));
      } catch (error) {
        // Expiration and refresh can race across ECS tasks. Treat that race as an
        // expired session instead of surfacing a 500 from authenticated routes.
        if ((error as { name?: string }).name === "ConditionalCheckFailedException") {
          return undefined;
        }
        throw error;
      }
      const refreshedItem = refreshed.Attributes as DynamoSessionItem | undefined;
      if (!refreshedItem) return undefined;
      const {
        PK: _refreshedPk,
        SK: _refreshedSk,
        expiresAtEpoch: _refreshedExpiresAtEpoch,
        ...refreshedSession
      } = refreshedItem;
      return refreshedSession;
    }

    const { PK: _pk, SK: _sk, expiresAtEpoch: _expiresAtEpoch, ...session } = item;
    return session;
  }

  public async delete(token: string): Promise<void> {
    // PutItem is deliberately used instead of DeleteItem so the existing least-privilege
    // ECS task role can revoke a session immediately. DynamoDB TTL removes the tombstone.
    await this.client.send(
      new PutCommand({
        TableName: this.tableName,
        Item: {
          PK: `SESSION#${token}`,
          SK: "SESSION",
          expiresAtEpoch: Math.floor(Date.now() / 1000) - 1
        }
      })
    );
  }
}

export function createSessionStore(config: AppConfig): SessionStore {
  if (config.store.dataStore === "dynamodb") {
    if (!config.store.dynamodbTable) throw new Error("DynamoDB 테이블 설정이 없습니다.");
    return new DynamoDbSessionStore(config.store.dynamodbTable, config.sessionTtlSeconds);
  }
  return new InMemorySessionStore(config.sessionTtlSeconds);
}

export class OneTimeAttemptStore {
  private readonly attempts = new Map<string, number>();

  public constructor(
    private readonly ttlMilliseconds = 10 * 60 * 1000,
    private readonly now: () => number = Date.now
  ) {}

  public async create(): Promise<string> {
    this.removeExpired();
    const nonce = randomBytes(24).toString("base64url");
    this.attempts.set(nonce, this.now() + this.ttlMilliseconds);
    return nonce;
  }

  public async consume(nonce: string | undefined): Promise<boolean> {
    if (!nonce) return false;
    const expiresAt = this.attempts.get(nonce);
    this.attempts.delete(nonce);
    return typeof expiresAt === "number" && expiresAt > this.now();
  }

  private removeExpired(): void {
    const now = this.now();
    for (const [nonce, expiresAt] of this.attempts) {
      if (expiresAt <= now) this.attempts.delete(nonce);
    }
  }
}

export interface LoginAttemptStore {
  create(): Promise<string>;
  consume(nonce: string | undefined): Promise<boolean>;
}

interface DynamoLoginAttemptItem {
  PK: string;
  SK: "LOGIN_ATTEMPT";
  expiresAtEpoch: number;
  consumedAt?: string;
}

export class DynamoDbOneTimeAttemptStore implements LoginAttemptStore {
  private readonly client: DynamoDocumentSender;

  public constructor(
    private readonly tableName: string,
    private readonly ttlMilliseconds = 10 * 60 * 1000,
    clientConfig: DynamoDBClientConfig = {},
    private readonly now: () => number = Date.now,
    client?: DynamoDocumentSender
  ) {
    this.client = client ?? DynamoDBDocumentClient.from(new DynamoDBClient(clientConfig), {
      marshallOptions: { removeUndefinedValues: true }
    });
  }

  public async create(): Promise<string> {
    const nonce = randomBytes(24).toString("base64url");
    await this.client.send(new PutCommand({
      TableName: this.tableName,
      Item: {
        PK: `LOGIN_ATTEMPT#${nonce}`,
        SK: "LOGIN_ATTEMPT",
        expiresAtEpoch: Math.floor((this.now() + this.ttlMilliseconds) / 1000)
      } satisfies DynamoLoginAttemptItem,
      ConditionExpression: "attribute_not_exists(PK)"
    }));
    return nonce;
  }

  public async consume(nonce: string | undefined): Promise<boolean> {
    if (!nonce) return false;
    const now = this.now();
    try {
      await this.client.send(new UpdateCommand({
        TableName: this.tableName,
        Key: { PK: `LOGIN_ATTEMPT#${nonce}`, SK: "LOGIN_ATTEMPT" },
        UpdateExpression: "SET consumedAt = :consumedAt",
        ConditionExpression: "attribute_exists(PK) AND attribute_not_exists(consumedAt) AND expiresAtEpoch > :now",
        ExpressionAttributeValues: {
          ":consumedAt": new Date(now).toISOString(),
          ":now": Math.floor(now / 1000)
        }
      }));
      return true;
    } catch (error) {
      if ((error as { name?: string }).name === "ConditionalCheckFailedException") return false;
      throw error;
    }
  }
}

export function createLoginAttemptStore(config: AppConfig): LoginAttemptStore {
  if (config.store.dataStore === "dynamodb") {
    if (!config.store.dynamodbTable) throw new Error("DynamoDB 테이블 설정이 없습니다.");
    return new DynamoDbOneTimeAttemptStore(config.store.dynamodbTable);
  }
  return new OneTimeAttemptStore();
}

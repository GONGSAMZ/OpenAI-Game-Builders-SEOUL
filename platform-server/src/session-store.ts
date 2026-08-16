import { randomBytes } from "node:crypto";
import {
  DynamoDBClient,
  type DynamoDBClientConfig
} from "@aws-sdk/client-dynamodb";
import {
  DynamoDBDocumentClient,
  GetCommand,
  PutCommand
} from "@aws-sdk/lib-dynamodb";
import type { AppConfig } from "./config.js";

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

function createGameSession(identity: SessionIdentity, ttlSeconds: number): GameSession {
  const now = Date.now();
  return {
    ...identity,
    token: randomBytes(32).toString("base64url"),
    createdAt: new Date(now).toISOString(),
    expiresAt: new Date(now + ttlSeconds * 1000).toISOString()
  };
}

export class InMemorySessionStore implements SessionStore {
  private readonly sessions = new Map<string, GameSession>();

  public constructor(private readonly ttlSeconds: number) {}

  public async create(identity: SessionIdentity): Promise<GameSession> {
    const session = createGameSession(identity, this.ttlSeconds);
    this.sessions.set(session.token, session);
    return session;
  }

  public async get(token: string): Promise<GameSession | undefined> {
    const session = this.sessions.get(token);
    if (!session) return undefined;

    if (Date.parse(session.expiresAt) <= Date.now()) {
      this.sessions.delete(token);
      return undefined;
    }

    return session;
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
  private readonly client: DynamoDBDocumentClient;

  public constructor(
    private readonly tableName: string,
    private readonly ttlSeconds: number,
    clientConfig: DynamoDBClientConfig = {}
  ) {
    this.client = DynamoDBDocumentClient.from(new DynamoDBClient(clientConfig), {
      marshallOptions: { removeUndefinedValues: true }
    });
  }

  public async create(identity: SessionIdentity): Promise<GameSession> {
    const session = createGameSession(identity, this.ttlSeconds);
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
    if (!item || item.expiresAtEpoch <= Math.floor(Date.now() / 1000)) return undefined;

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

  public constructor(private readonly ttlMilliseconds = 10 * 60 * 1000) {}

  public create(): string {
    this.removeExpired();
    const nonce = randomBytes(24).toString("base64url");
    this.attempts.set(nonce, Date.now() + this.ttlMilliseconds);
    return nonce;
  }

  public consume(nonce: string | undefined): boolean {
    if (!nonce) return false;
    const expiresAt = this.attempts.get(nonce);
    this.attempts.delete(nonce);
    return typeof expiresAt === "number" && expiresAt > Date.now();
  }

  private removeExpired(): void {
    const now = Date.now();
    for (const [nonce, expiresAt] of this.attempts) {
      if (expiresAt <= now) this.attempts.delete(nonce);
    }
  }
}

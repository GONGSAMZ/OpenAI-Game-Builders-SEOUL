import { randomBytes } from "node:crypto";

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

export class InMemorySessionStore {
  private readonly sessions = new Map<string, GameSession>();

  public constructor(private readonly ttlSeconds: number) {}

  public create(identity: SessionIdentity): GameSession {
    const now = Date.now();
    const token = randomBytes(32).toString("base64url");
    const session: GameSession = {
      ...identity,
      token,
      createdAt: new Date(now).toISOString(),
      expiresAt: new Date(now + this.ttlSeconds * 1000).toISOString()
    };

    this.sessions.set(token, session);
    return session;
  }

  public get(token: string): GameSession | undefined {
    const session = this.sessions.get(token);
    if (!session) return undefined;

    if (Date.parse(session.expiresAt) <= Date.now()) {
      this.sessions.delete(token);
      return undefined;
    }

    return session;
  }

  public delete(token: string): void {
    this.sessions.delete(token);
  }
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

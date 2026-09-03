import { GetCommand, PutCommand, UpdateCommand } from "@aws-sdk/lib-dynamodb";
import { describe, expect, it } from "vitest";
import {
  DynamoDbOneTimeAttemptStore,
  DynamoDbSessionStore,
  InMemorySessionStore,
  OneTimeAttemptStore
} from "../src/session-store.js";

describe("session stores", () => {
  it("활동 중인 메모리 세션은 절반 시점부터 연장하고 로그아웃 뒤에는 되살리지 않는다", async () => {
    let now = Date.parse("2026-08-26T00:00:00.000Z");
    const sessions = new InMemorySessionStore(100, () => now);
    const created = await sessions.create({ subject: "player-a", provider: "hive" });

    now += 49_000;
    expect((await sessions.get(created.token))?.expiresAt).toBe(created.expiresAt);

    now += 2_000;
    const refreshed = await sessions.get(created.token);
    expect(Date.parse(String(refreshed?.expiresAt))).toBe(now + 100_000);

    await sessions.delete(created.token);
    expect(await sessions.get(created.token)).toBeUndefined();
  });

  it("메모리 로그인 nonce는 한 번만 소비되고 만료된다", async () => {
    let now = 1_000;
    const attempts = new OneTimeAttemptStore(500, () => now);
    const nonce = await attempts.create();
    expect(await attempts.consume(nonce)).toBe(true);
    expect(await attempts.consume(nonce)).toBe(false);

    const expired = await attempts.create();
    now += 501;
    expect(await attempts.consume(expired)).toBe(false);
  });

  it("DynamoDB 세션도 활동 시 만료 시각을 원자적으로 연장한다", async () => {
    let stored: Record<string, unknown> | undefined;
    const client = {
      async send(command: unknown) {
        if (command instanceof PutCommand) {
          stored = { ...(command.input.Item as Record<string, unknown>) };
          return {};
        }
        if (command instanceof GetCommand) return { Item: stored ? { ...stored } : undefined };
        if (command instanceof UpdateCommand) {
          if (!stored) throw new Error("missing session");
          stored.expiresAt = command.input.ExpressionAttributeValues?.[":expiresAt"];
          stored.expiresAtEpoch = command.input.ExpressionAttributeValues?.[":expiresAtEpoch"];
          return { Attributes: { ...stored } };
        }
        throw new Error("unexpected command");
      }
    };
    let now = Date.parse("2026-08-26T00:00:00.000Z");
    const sessions = new DynamoDbSessionStore("test", 100, {}, () => now, client as never);
    const created = await sessions.create({ subject: "player-a", provider: "hive" });
    now += 51_000;

    const refreshed = await sessions.get(created.token);

    expect(Date.parse(String(refreshed?.expiresAt))).toBe(now + 100_000);
    expect(stored?.expiresAtEpoch).toBe(Math.floor((now + 100_000) / 1000));
  });

  it("DynamoDB 세션 연장 중 만료 경합은 500 대신 인증 만료로 처리한다", async () => {
    let stored: Record<string, unknown> | undefined;
    const client = {
      async send(command: unknown) {
        if (command instanceof PutCommand) {
          stored = { ...(command.input.Item as Record<string, unknown>) };
          return {};
        }
        if (command instanceof GetCommand) return { Item: stored ? { ...stored } : undefined };
        if (command instanceof UpdateCommand) {
          const error = new Error("expired during refresh");
          error.name = "ConditionalCheckFailedException";
          throw error;
        }
        throw new Error("unexpected command");
      }
    };
    let now = Date.parse("2026-08-26T00:00:00.000Z");
    const sessions = new DynamoDbSessionStore("test", 100, {}, () => now, client as never);
    const created = await sessions.create({ subject: "player-a", provider: "hive" });
    now += 51_000;

    expect(await sessions.get(created.token)).toBeUndefined();
  });

  it("서로 다른 ECS 인스턴스가 같은 DynamoDB nonce를 공유하고 단 한 번만 소비한다", async () => {
    const items = new Map<string, Record<string, unknown>>();
    const client = {
      async send(command: unknown) {
        if (command instanceof PutCommand) {
          const input = command.input;
          const item = input.Item as Record<string, unknown>;
          const key = `${String(item.PK)}|${String(item.SK)}`;
          if (items.has(key)) {
            const error = new Error("duplicate");
            error.name = "ConditionalCheckFailedException";
            throw error;
          }
          items.set(key, { ...item });
          return {};
        }
        if (command instanceof UpdateCommand) {
          const input = command.input;
          const key = `${String(input.Key?.PK)}|${String(input.Key?.SK)}`;
          const item = items.get(key);
          const nowEpoch = Number(input.ExpressionAttributeValues?.[":now"]);
          if (!item || item.consumedAt || Number(item.expiresAtEpoch) <= nowEpoch) {
            const error = new Error("conditional");
            error.name = "ConditionalCheckFailedException";
            throw error;
          }
          item.consumedAt = input.ExpressionAttributeValues?.[":consumedAt"];
          return {};
        }
        throw new Error("unexpected command");
      }
    };
    const now = () => Date.parse("2026-08-26T00:00:00.000Z");
    const firstTask = new DynamoDbOneTimeAttemptStore("test", 60_000, {}, now, client as never);
    const secondTask = new DynamoDbOneTimeAttemptStore("test", 60_000, {}, now, client as never);

    const nonce = await firstTask.create();
    expect(await secondTask.consume(nonce)).toBe(true);
    expect(await firstTask.consume(nonce)).toBe(false);
  });
});

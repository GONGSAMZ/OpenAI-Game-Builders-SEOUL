import request from "supertest";
import { describe, expect, it } from "vitest";
import { createApp } from "../src/app.js";
import { InMemorySessionStore } from "../src/session-store.js";
import { storeCatalog } from "../src/store/catalog.js";
import { InMemoryPurchaseHistoryStore } from "../src/store/purchase-history.js";
import { createTestConfig } from "./helpers.js";

describe("purchase history", () => {
  it("상태와 계정을 격리하고 멱등 시도를 한 건으로 유지한다", async () => {
    const history = new InMemoryPurchaseHistoryStore("test-secret");
    const product = storeCatalog[0]!;
    const statuses = ["succeeded", "failed", "cancelled", "expired"] as const;

    for (let index = 0; index < statuses.length; index++) {
      const attemptId = `attempt-${index}`;
      await history.start("player-a", { provider: "mock", attemptId, product });
      await history.finish("player-a", "mock", attemptId, statuses[index]!);
    }
    const duplicate = await history.start("player-a", {
      provider: "mock",
      attemptId: "attempt-0",
      product
    });
    expect(duplicate.status).toBe("succeeded");
    expect((await history.list("player-a", 20)).purchases).toHaveLength(4);
    expect((await history.list("player-b", 20)).purchases).toEqual([]);
    await expect(history.start("player-b", {
      provider: "mock",
      attemptId: "attempt-0",
      product
    })).rejects.toThrow(/식별자/);
  });

  it("페이지 cursor의 위조와 다른 계정 재사용을 차단한다", async () => {
    const history = new InMemoryPurchaseHistoryStore("test-secret");
    for (let index = 0; index < 3; index++) {
      await history.start("player-a", {
        provider: "mock",
        attemptId: `page-${index}`,
        product: storeCatalog[index]!
      });
    }

    const first = await history.list("player-a", 2);
    expect(first.purchases).toHaveLength(2);
    expect(first.nextCursor).toBeTruthy();
    const second = await history.list("player-a", 2, first.nextCursor!);
    expect(second.purchases).toHaveLength(1);
    expect(second.nextCursor).toBeNull();

    await expect(history.list("player-b", 2, first.nextCursor!)).rejects.toThrow(/cursor/);
    const [body, signature] = first.nextCursor!.split(".");
    const forged = `${body![0] === "A" ? "B" : "A"}${body!.slice(1)}.${signature}`;
    await expect(history.list("player-a", 2, forged)).rejects.toThrow(/cursor/);
  });

  it("인증 API는 subject를 노출하지 않고 계정 소유 cursor만 허용한다", async () => {
    const sessions = new InMemorySessionStore(3600);
    const first = await sessions.create({ subject: "player-a", provider: "mock-hive", playerId: "a" });
    const second = await sessions.create({ subject: "player-b", provider: "mock-hive", playerId: "b" });
    const history = new InMemoryPurchaseHistoryStore("test-secret");
    for (let index = 0; index < 3; index++) {
      await history.start("player-a", {
        provider: "mock",
        attemptId: `api-${index}`,
        product: storeCatalog[index]!
      });
    }
    const app = createApp({ config: createTestConfig(), sessions, purchaseHistory: history });

    await request(app).get("/api/v1/store/purchases").expect(401);
    const page = await request(app)
      .get("/api/v1/store/purchases?limit=2")
      .set("Authorization", `Bearer ${first.token}`)
      .expect(200)
      .expect("cache-control", /no-store/);
    expect(page.body.purchases).toHaveLength(2);
    expect(JSON.stringify(page.body)).not.toContain("player-a");

    await request(app)
      .get(`/api/v1/store/purchases?limit=2&cursor=${encodeURIComponent(page.body.nextCursor)}`)
      .set("Authorization", `Bearer ${second.token}`)
      .expect(400);
    await request(app)
      .get("/api/v1/store/purchases?limit=51")
      .set("Authorization", `Bearer ${first.token}`)
      .expect(400);
  });

  it("Mock 포인트 구매 성공과 잔액 부족 실패를 API 원장에 기록한다", async () => {
    const sessions = new InMemorySessionStore(3600);
    const session = await sessions.create({ subject: "player-a", provider: "mock-hive", playerId: "a" });
    const history = new InMemoryPurchaseHistoryStore("test-secret");
    const app = createApp({ config: createTestConfig(), sessions, purchaseHistory: history });
    const authorization = { Authorization: `Bearer ${session.token}` };

    await request(app)
      .post("/api/v1/store/mock-purchases")
      .set(authorization)
      .send({ productId: "red-bean-550", idempotencyKey: "1edb189b-cf32-4a4e-a3ed-c1ca01e518a1" })
      .expect(201);
    await request(app)
      .post("/api/v1/store/mock-purchases")
      .set(authorization)
      .send({ productId: "red-bean-550", idempotencyKey: "fb7fb694-4633-47f7-af89-475746c4f96d" })
      .expect(409);

    const purchases = await request(app)
      .get("/api/v1/store/purchases")
      .set(authorization)
      .expect(200);
    expect(purchases.body.purchases.map((entry: { status: string }) => entry.status).sort())
      .toEqual(["failed", "succeeded"]);
  });
});

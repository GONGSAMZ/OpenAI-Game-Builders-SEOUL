import request from "supertest";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { createApp } from "../src/app.js";
import type { HiveBillingGateway } from "../src/integrations/hive/billing-client.js";
import { InMemoryMarketStore } from "../src/store/store.js";
import { createTestConfig } from "./helpers.js";

function extractSessionToken(html: string): string {
  const match = html.match(/"sessionToken":"([^"]+)"/);
  if (!match?.[1]) throw new Error("인증 브리지 HTML에서 세션 토큰을 찾지 못했습니다.");
  return match[1];
}

async function login(app: ReturnType<typeof createApp>): Promise<string> {
  const start = await request(app).get("/api/v1/auth/hive/login").expect(200);
  const cookie = start.headers["set-cookie"]?.[0];
  if (!cookie) throw new Error("로그인 시도 쿠키가 없습니다.");

  const callback = await request(app)
    .get("/api/v1/auth/hive/mock/complete")
    .set("Cookie", cookie)
    .expect(200);
  return extractSessionToken(callback.text);
}

describe("integration API", () => {
  it("health와 공개 설정을 반환한다", async () => {
    const app = createApp({ config: createTestConfig() });
    const health = await request(app).get("/api/v1/health").expect(200);
    expect(health.body).toEqual(
      expect.objectContaining({ status: "ok", revision: "test-revision" })
    );
    await request(app)
      .get("/api/v1/version")
      .expect(200, { revision: "test-revision" })
      .expect("cache-control", /no-store/);
    await request(app)
      .get("/api/v1/config/public")
      .expect(200, {
        hiveMode: "mock",
        storeMode: "mock",
        storeDevTools: true,
        hiveWebShopUrl: null,
        openaiMode: "mock",
        openaiModel: "mock"
      });
    const page = await request(app).get("/").expect(200);
    expect(page.text).toContain("붕어빵 타이쿤");
    expect(page.text).toContain('src="/game/index.html?v=');
    expect(page.text).not.toContain("장인 상점");
    expect(page.text).not.toContain("OPENAI LAB");
    expect(page.text).not.toContain("개발 진단 로그");
    expect(page.text).toContain('id="store-dev-tools"');
    expect(page.text).toContain('id="dev-grant-button"');
    expect(page.text).toContain('id="account-menu"');
    expect(page.text).toContain('id="logout-button"');
    expect(page.headers["content-security-policy"]).toContain("'unsafe-inline'");
    expect(page.headers["content-security-policy"]).toContain("'wasm-unsafe-eval'");
    expect(page.headers["content-security-policy"]).toContain("blob:");
    expect(page.headers["cache-control"]).toContain("no-store");
  });

  it("mock Hive 로그인 후 게임 세션을 조회한다", async () => {
    const app = createApp({ config: createTestConfig() });
    const token = await login(app);

    const session = await request(app)
      .get("/api/v1/auth/session")
      .set("Authorization", `Bearer ${token}`)
      .expect(200);
    expect(session.body.session).toEqual(
      expect.objectContaining({ provider: "mock-hive", playerId: "local-player" })
    );
  });

  it("팝업 연결이 끊겨도 HttpOnly 쿠키로 로그인 세션을 복구하고 로그아웃한다", async () => {
    const app = createApp({ config: createTestConfig() });
    const start = await request(app).get("/api/v1/auth/hive/login").expect(200);
    const loginCookie = start.headers["set-cookie"]?.[0];
    if (!loginCookie) throw new Error("로그인 시도 쿠키가 없습니다.");

    const callback = await request(app)
      .get("/api/v1/auth/hive/mock/complete")
      .set("Cookie", loginCookie)
      .expect(200);
    const callbackSetCookie = callback.headers["set-cookie"];
    const callbackCookies = Array.isArray(callbackSetCookie)
      ? callbackSetCookie
      : callbackSetCookie
        ? [callbackSetCookie]
        : [];
    const sessionCookie = callbackCookies.find((cookie: string) =>
      cookie.startsWith("game_session=")
    );
    if (!sessionCookie) throw new Error("게임 세션 쿠키가 없습니다.");

    const session = await request(app)
      .get("/api/v1/auth/session")
      .set("Cookie", sessionCookie)
      .expect(200);
    expect(session.body.session).toEqual(
      expect.objectContaining({ provider: "mock-hive", playerId: "local-player" })
    );

    const logout = await request(app)
      .delete("/api/v1/auth/session")
      .set("Cookie", sessionCookie)
      .expect(204);
    const logoutSetCookie = logout.headers["set-cookie"];
    const logoutCookies = Array.isArray(logoutSetCookie)
      ? logoutSetCookie
      : logoutSetCookie
        ? [logoutSetCookie]
        : [];
    expect(logoutCookies.join(";")).toContain("game_session=");
    await request(app).get("/api/v1/auth/session").set("Cookie", sessionCookie).expect(401);
  });

  it("인증 콜백은 메인 창의 수신 확인까지 결과를 재전송한다", async () => {
    const app = createApp({ config: createTestConfig() });
    const start = await request(app).get("/api/v1/auth/hive/login").expect(200);
    const cookie = start.headers["set-cookie"]?.[0];
    if (!cookie) throw new Error("로그인 시도 쿠키가 없습니다.");

    const callback = await request(app)
      .get("/api/v1/auth/hive/mock/complete")
      .set("Cookie", cookie)
      .expect(200);

    expect(callback.text).toContain("HIVE_AUTH_ACK");
    expect(callback.text).toContain("setInterval(notifyOpener, 250)");
  });

  it("짧은 HIVE 콜백 경로에서도 로그인 시도 쿠키를 전달한다", async () => {
    const app = createApp({ config: createTestConfig() });
    const start = await request(app).get("/api/v1/auth/hive/login").expect(200);
    const cookie = start.headers["set-cookie"]?.[0];

    expect(cookie).toContain("Path=/");
    if (!cookie) throw new Error("로그인 시도 쿠키가 없습니다.");

    const callback = await request(app).get("/hive/cb").set("Cookie", cookie).expect(400);
    expect(callback.text).toContain("HIVE_AUTH_ERROR");
    expect(callback.text).toContain("실제 연동이 비활성화");
  });

  it("로그인한 사용자에게 mock NPC 반응을 반환한다", async () => {
    const app = createApp({ config: createTestConfig() });
    const token = await login(app);

    const response = await request(app)
      .post("/api/v1/ai/npc-reaction")
      .set("Authorization", `Bearer ${token}`)
      .send({ situation: "손님이 기다린다", playerAction: "팥을 넣었다", locale: "ko" })
      .expect(200);

    expect(response.body).toEqual(expect.objectContaining({ source: "mock", model: "mock" }));
    expect(response.body.text).toContain("팥을 넣었다");
  });

  it("인증 없는 AI 요청을 거부한다", async () => {
    const app = createApp({ config: createTestConfig() });
    await request(app)
      .post("/api/v1/ai/npc-reaction")
      .send({ situation: "test", playerAction: "test", locale: "ko" })
      .expect(401);
  });

  it("mock 상품을 한 번만 지급하고 보유 아이템을 반환한다", async () => {
    const app = createApp({ config: createTestConfig() });
    const token = await login(app);
    const idempotencyKey = "12e68262-ff70-42b7-ae95-18e89b7bbbd8";

    const first = await request(app)
      .post("/api/v1/store/mock-purchases")
      .set("Authorization", `Bearer ${token}`)
      .send({ productId: "red-bean-100", idempotencyKey })
      .expect(201);
    expect(first.body).toEqual(expect.objectContaining({ duplicate: false }));
    expect(first.body.inventory).toContainEqual({ itemId: "red-bean-coin", quantity: 100 });
    expect(first.body.equipment).toEqual({ moldSkin: null });

    const duplicate = await request(app)
      .post("/api/v1/store/mock-purchases")
      .set("Authorization", `Bearer ${token}`)
      .send({ productId: "red-bean-100", idempotencyKey })
      .expect(200);
    expect(duplicate.body).toEqual(expect.objectContaining({ duplicate: true }));
    expect(duplicate.body.inventory).toContainEqual({ itemId: "red-bean-coin", quantity: 100 });
  });

  it("황금 틀 장착 상태를 로그인 사용자별로 저장하고 해제한다", async () => {
    const marketStore = new InMemoryMarketStore();
    const app = createApp({ config: createTestConfig(), marketStore });
    const token = await login(app);

    await request(app)
      .put("/api/v1/store/equipment/mold")
      .set("Authorization", `Bearer ${token}`)
      .send({ itemId: "golden-pan" })
      .expect(409);

    await request(app)
      .post("/api/v1/store/mock-purchases")
      .set("Authorization", `Bearer ${token}`)
      .send({
        productId: "golden-pan",
        idempotencyKey: "a6d36fc4-c6dd-4a0c-9002-5a64051e1c32"
      })
      .expect(201);

    const equipped = await request(app)
      .put("/api/v1/store/equipment/mold")
      .set("Authorization", `Bearer ${token}`)
      .send({ itemId: "golden-pan" })
      .expect(200);
    expect(equipped.body.inventory).toContainEqual({ itemId: "golden-pan", quantity: 1 });
    expect(equipped.body.equipment).toEqual({ moldSkin: "golden-pan" });

    const restored = await request(app)
      .get("/api/v1/store/me")
      .set("Authorization", `Bearer ${token}`)
      .expect(200);
    expect(restored.body.equipment).toEqual({ moldSkin: "golden-pan" });
    expect(await marketStore.getEquipment("another-player")).toEqual({ moldSkin: null });

    const unequipped = await request(app)
      .put("/api/v1/store/equipment/mold")
      .set("Authorization", `Bearer ${token}`)
      .send({ itemId: null })
      .expect(200);
    expect(unequipped.body.equipment).toEqual({ moldSkin: null });

    await request(app)
      .put("/api/v1/store/equipment/mold")
      .set("Authorization", `Bearer ${token}`)
      .send({ itemId: "unsupported-pan" })
      .expect(400);
    await request(app)
      .put("/api/v1/store/equipment/mold")
      .send({ itemId: null })
      .expect(401);
  });

  it("개발 도구 지급은 로그인 사용자 인벤토리와 게임 재화에만 반영한다", async () => {
    const marketStore = new InMemoryMarketStore();
    const app = createApp({ config: createTestConfig(), marketStore });
    const token = await login(app);

    const response = await request(app)
      .post("/api/v1/store/dev-grants")
      .set("Authorization", `Bearer ${token}`)
      .send({
        productId: "red-bean-100",
        idempotencyKey: "33a1454b-180b-4ae1-b92a-cae426265b87"
      })
      .expect(201);

    expect(response.body.inventory).toContainEqual({ itemId: "red-bean-coin", quantity: 100 });
    expect(response.body.equipment).toEqual({ moldSkin: null });
    expect(await marketStore.getInventory("another-player")).toEqual([]);
  });

  it("비활성화된 개발 도구 지급 API를 노출하지 않는다", async () => {
    const app = createApp({
      config: createTestConfig({ store: { mode: "mock", dataStore: "memory", devToolsEnabled: false } })
    });
    const token = await login(app);

    await request(app)
      .post("/api/v1/store/dev-grants")
      .set("Authorization", `Bearer ${token}`)
      .send({
        productId: "red-bean-100",
        idempotencyKey: "f4adcf39-b63d-499f-8bda-a6721bcfd654"
      })
      .expect(404);
  });

  it("HIVE 웹 상점용 게임 사용자 정보를 반환한다", async () => {
    const app = createApp({ config: createTestConfig() });
    const response = await request(app)
      .post("/api/v1/hive/web-shop/in-game-info")
      .send({ cs_code: 100001234 })
      .expect(200);
    expect(response.body).toEqual(
      expect.objectContaining({ result_code: 200, cs_code: 100001234 })
    );
  });

  it("검증된 HIVE 웹 상점 결제를 사용자별로 한 번만 지급하고 완료 처리한다", async () => {
    const marketStore = new InMemoryMarketStore();
    const deliveries: unknown[] = [];
    const billingClient: HiveBillingGateway = {
      async findUnconsumedPurchase() {
        return {
          marketId: "15",
          marketPid: "com.gongsamz.bungeoppang.redbean100",
          orderId: "ORDER-100",
          serverId: "global",
          playerId: "20000011337",
          quantity: 1,
          purchaseBypassInfo: "verified-bypass"
        };
      },
      async verifyReceipt() {
        return {
          transactionId: "HS_TEST_100",
          marketId: "15",
          marketPid: "com.gongsamz.bungeoppang.redbean100",
          marketTransactionId: "ORDER-100",
          quantity: 1,
          purchaseTest: "Y"
        };
      },
      async confirmDelivery(input) {
        deliveries.push(input);
      }
    };
    const app = createApp({
      config: createTestConfig({
        hive: {
          mode: "sandbox",
          country: "KR",
          language: "ko",
          billingAppId: "com.gongsamz.webshop",
          billingAuthKey: "test-key"
        },
        store: {
          mode: "hive-web-shop",
          devToolsEnabled: true,
          dataStore: "memory"
        }
      }),
      marketStore,
      billingClient
    });
    const notification = {
      type: "paid",
      market_id: "15",
      order_id: "ORDER-100",
      market_pid: "com.gongsamz.bungeoppang.redbean100",
      vid: "20000011337",
      vid_type: "v4",
      server_id: "global",
      appid: "com.gongsamz.webshop",
      quantity: "1",
      purchase_bypass_info: "verified-bypass"
    };

    const firstNotification = await request(app)
      .post("/api/v1/hive/web-shop/payment-notifications")
      .send(notification)
      .expect(200);
    expect(firstNotification.body).toEqual(expect.objectContaining({ result: 0, duplicate: false }));

    const duplicateNotification = await request(app)
      .post("/api/v1/hive/web-shop/payment-notifications")
      .send(notification)
      .expect(200);
    expect(duplicateNotification.body).toEqual(
      expect.objectContaining({ result: 0, duplicate: true })
    );

    expect(await marketStore.getInventory("20000011337")).toContainEqual({
      itemId: "red-bean-coin",
      quantity: 100
    });
    expect(await marketStore.getInventory("20000011338")).toEqual([]);
    expect(deliveries).toHaveLength(2);
  });

  it("Unity 압축 파일에 올바른 전송 헤더를 설정한다", async () => {
    const app = createApp({
      config: createTestConfig({
        gameBuildDirectory: path.resolve(process.cwd(), "tests/fixtures/game")
      })
    });
    const response = await request(app).head("/game/assets/sample.wasm.unityweb").expect(200);
    expect(response.headers["content-encoding"]).toBe("gzip");
    expect(response.headers["content-type"]).toContain("application/wasm");
    const page = await request(app).get("/game/").expect(200);
    expect(page.text).toContain("Unity test fixture");
    expect(page.headers["cache-control"]).toContain("no-store");
  });
});

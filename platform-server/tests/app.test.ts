import request from "supertest";
import path from "node:path";
import { createHash } from "node:crypto";
import { describe, expect, it } from "vitest";
import { createApp } from "../src/app.js";
import type { HiveBillingGateway } from "../src/integrations/hive/billing-client.js";
import type { NicePayGateway } from "../src/integrations/nicepay/client.js";
import { InMemoryMarketStore } from "../src/store/store.js";
import { InMemoryPlayerSaveStore } from "../src/save/save-store.js";
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

function sha256(value: string): string {
  return createHash("sha256").update(value).digest("hex");
}

describe("integration API", () => {
  it("로그인 계정 저장을 생성하고 revision 충돌을 막는다", async () => {
    const playerSaves = new InMemoryPlayerSaveStore();
    const app = createApp({ config: createTestConfig(), playerSaves });
    const token = await login(app);
    const auth = { Authorization: `Bearer ${token}` };
    const profile = {
      schemaVersion: 2,
      revision: 0,
      updatedAt: "",
      run: {
        nextDay: 1,
        money: 5000,
        unlockedFillingIds: ["red-bean", "custard", "nutella", "cream-cheese"],
        ownedGameplayItemIds: []
      },
      account: { customers: [], discoveredSouls: [], achievements: [] }
    };

    await request(app)
      .get("/api/v1/save/profile")
      .set(auth)
      .expect(200, { profile: null });

    const created = await request(app)
      .put("/api/v1/save/profile")
      .set(auth)
      .send({ expectedRevision: 0, profile })
      .expect(200);
    expect(created.body.profile).toEqual(expect.objectContaining({ revision: 1 }));

    const conflict = await request(app)
      .put("/api/v1/save/profile")
      .set(auth)
      .send({ expectedRevision: 0, profile })
      .expect(409);
    expect(conflict.body).toEqual(expect.objectContaining({
      error: expect.objectContaining({ code: "SAVE_CONFLICT" }),
      profile: expect.objectContaining({ revision: 1 })
    }));
  });

  it("로그인하지 않은 사용자의 저장 API 접근을 거부한다", async () => {
    const app = createApp({ config: createTestConfig() });
    await request(app).get("/api/v1/save/profile").expect(401);
  });

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
        storeCatalogSource: "static",
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
    expect(page.text).toContain('id="dev-test-point-button"');
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
    expect(first.body.wallet).toEqual({ testPoints: 8900 });

    const duplicate = await request(app)
      .post("/api/v1/store/mock-purchases")
      .set("Authorization", `Bearer ${token}`)
      .send({ productId: "red-bean-100", idempotencyKey })
      .expect(200);
    expect(duplicate.body).toEqual(expect.objectContaining({ duplicate: true }));
    expect(duplicate.body.inventory).toContainEqual({ itemId: "red-bean-coin", quantity: 100 });
    expect(duplicate.body.wallet).toEqual({ testPoints: 8900 });
  });

  it("NICEPAY 테스트 승인 결제를 로그인 사용자에게 한 번만 지급한다", async () => {
    const marketStore = new InMemoryMarketStore();
    const clientId = "S2_test-client";
    const secretKey = "test-secret";
    let approvalCalls = 0;
    const nicePayGateway: NicePayGateway = {
      async approvePayment({ tid, amount }) {
        approvalCalls += 1;
        const ediDate = "20260819123000";
        return {
          resultCode: "0000",
          status: "paid",
          tid,
          orderId: activeOrderId,
          amount,
          ediDate,
          signature: sha256(`${tid}${amount}${ediDate}${secretKey}`)
        };
      }
    };
    let activeOrderId = "";
    const app = createApp({
      config: createTestConfig({
        store: { mode: "nicepay-test", devToolsEnabled: true, dataStore: "memory" },
        nicepay: { clientId, secretKey, apiBaseUrl: "https://sandbox-api.nicepay.co.kr" }
      }),
      marketStore,
      nicePayGateway
    });
    const token = await login(app);
    const created = await request(app)
      .post("/api/v1/store/nicepay/orders")
      .set("Authorization", `Bearer ${token}`)
      .send({ productId: "red-bean-100" })
      .expect(201);
    activeOrderId = created.body.orderId;
    expect(created.body).toEqual(expect.objectContaining({ amount: 1100 }));

    const checkout = await request(app)
      .get(created.body.checkoutUrl)
      .expect(200);
    expect(checkout.text).toContain("https://pay.nicepay.co.kr/v1/js/");
    expect(checkout.text).toContain(activeOrderId);
    expect(checkout.text).not.toContain(secretKey);

    const tid = "nicepay-test-tid-100";
    const authToken = "nicepay-auth-token";
    const callback = {
      authResultCode: "0000",
      authResultMsg: "success",
      tid,
      clientId,
      orderId: activeOrderId,
      amount: "1100",
      authToken,
      signature: sha256(`${authToken}${clientId}1100${secretKey}`)
    };
    const first = await request(app)
      .post("/api/v1/store/nicepay/callback")
      .set("Origin", "https://web.nicepay.co.kr")
      .type("form")
      .send(callback)
      .expect(200);
    expect(first.text).toContain("NICEPAY_PAYMENT_SUCCESS");
    expect(first.text).toContain('href="http://localhost:3000"');
    expect(first.text).toContain("window.location.replace(origin)");

    const duplicate = await request(app)
      .post("/api/v1/store/nicepay/callback")
      .type("form")
      .send(callback)
      .expect(200);
    expect(duplicate.text).toContain("NICEPAY_PAYMENT_SUCCESS");
    expect(approvalCalls).toBe(1);

    const goldenOrder = await request(app)
      .post("/api/v1/store/nicepay/orders")
      .set("Authorization", `Bearer ${token}`)
      .send({ productId: "golden-pan" })
      .expect(201);
    activeOrderId = goldenOrder.body.orderId;
    const goldenTid = "nicepay-test-tid-golden";
    const goldenAuthToken = "nicepay-golden-auth-token";
    await request(app)
      .post("/api/v1/store/nicepay/callback")
      .type("form")
      .send({
        ...callback,
        tid: goldenTid,
        orderId: activeOrderId,
        amount: "3300",
        authToken: goldenAuthToken,
        signature: sha256(`${goldenAuthToken}${clientId}3300${secretKey}`)
      })
      .expect(200);
    expect(approvalCalls).toBe(2);

    const playerInventory = await marketStore.getInventory("mock-hive:local-player");
    expect(playerInventory).toContainEqual({
      itemId: "red-bean-coin",
      quantity: 100
    });
    expect(playerInventory).toContainEqual({ itemId: "golden-pan", quantity: 1 });
    expect(await marketStore.getInventory("another-player")).toEqual([]);
  });

  it("NICEPAY 외부 Origin 콜백만 허용하고 일반 웹게임 요청은 계속 차단한다", async () => {
    const app = createApp({ config: createTestConfig() });

    await request(app)
      .get("/api/v1/store/catalog")
      .set("Origin", "https://untrusted.example")
      .expect(403);
  });

  it("NICEPAY 콜백 금액이나 서명이 주문과 다르면 승인·지급하지 않는다", async () => {
    const marketStore = new InMemoryMarketStore();
    let approvalCalls = 0;
    const nicePayGateway: NicePayGateway = {
      async approvePayment() {
        approvalCalls += 1;
        throw new Error("호출되면 안 됩니다.");
      }
    };
    const app = createApp({
      config: createTestConfig({
        store: { mode: "nicepay-test", devToolsEnabled: true, dataStore: "memory" },
        nicepay: {
          clientId: "S2_test-client",
          secretKey: "test-secret",
          apiBaseUrl: "https://sandbox-api.nicepay.co.kr"
        }
      }),
      marketStore,
      nicePayGateway
    });
    const token = await login(app);
    const created = await request(app)
      .post("/api/v1/store/nicepay/orders")
      .set("Authorization", `Bearer ${token}`)
      .send({ productId: "golden-pan" })
      .expect(201);
    const response = await request(app)
      .post("/api/v1/store/nicepay/callback")
      .type("form")
      .send({
        authResultCode: "0000",
        tid: "forged-tid",
        clientId: "S2_test-client",
        orderId: created.body.orderId,
        amount: "1",
        authToken: "forged-token",
        signature: "0".repeat(64)
      })
      .expect(200);
    expect(response.text).toContain("NICEPAY_PAYMENT_ERROR");
    expect(approvalCalls).toBe(0);
    expect(await marketStore.getInventory("mock-hive:local-player")).toEqual([]);
  });

  it("mock 결제는 사용자별 테스트 포인트를 차감하고 부족하면 지급하지 않는다", async () => {
    const marketStore = new InMemoryMarketStore();
    const app = createApp({ config: createTestConfig(), marketStore });
    const token = await login(app);

    const first = await request(app)
      .post("/api/v1/store/mock-purchases")
      .set("Authorization", `Bearer ${token}`)
      .send({
        productId: "red-bean-550",
        idempotencyKey: "51258ebc-5c6d-4264-b546-cf06c9b2e78e"
      })
      .expect(201);
    expect(first.body.wallet).toEqual({ testPoints: 4500 });

    await request(app)
      .post("/api/v1/store/mock-purchases")
      .set("Authorization", `Bearer ${token}`)
      .send({
        productId: "red-bean-550",
        idempotencyKey: "6bf48fc0-00c3-45b7-b07c-ea87245b9852"
      })
      .expect(409);

    expect(await marketStore.getInventory("mock-hive:local-player")).toContainEqual({
      itemId: "red-bean-coin",
      quantity: 550
    });
    expect(await marketStore.getWallet("another-player")).toEqual({ testPoints: 10000 });
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

  it("개발 도구 테스트 포인트 충전은 사용자별로 한 번만 반영한다", async () => {
    const marketStore = new InMemoryMarketStore();
    const app = createApp({ config: createTestConfig(), marketStore });
    const token = await login(app);
    const body = {
      amount: 10000,
      idempotencyKey: "86031de5-0cf2-45e9-b0f4-a2e00defc307"
    };

    const first = await request(app)
      .post("/api/v1/store/dev-test-points")
      .set("Authorization", `Bearer ${token}`)
      .send(body)
      .expect(201);
    expect(first.body.wallet).toEqual({ testPoints: 20000 });

    const duplicate = await request(app)
      .post("/api/v1/store/dev-test-points")
      .set("Authorization", `Bearer ${token}`)
      .send(body)
      .expect(200);
    expect(duplicate.body).toEqual(
      expect.objectContaining({ duplicate: true, wallet: { testPoints: 20000 } })
    );
    expect(await marketStore.getWallet("another-player")).toEqual({ testPoints: 10000 });
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
    await request(app)
      .post("/api/v1/store/dev-test-points")
      .set("Authorization", `Bearer ${token}`)
      .send({
        amount: 10000,
        idempotencyKey: "5bd82299-d664-4470-9c8a-4d1c612e002e"
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

import request from "supertest";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { createApp } from "../src/app.js";
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
    expect(health.body).toEqual(expect.objectContaining({ status: "ok" }));
    await request(app)
      .get("/api/v1/config/public")
      .expect(200, {
        hiveMode: "mock",
        storeMode: "mock",
        hiveWebShopUrl: null,
        openaiMode: "mock",
        openaiModel: "mock"
      });
    const page = await request(app).get("/").expect(200);
    expect(page.text).toContain("붕어빵 타이쿤");
    expect(page.text).toContain('src="/game/index.html"');
    expect(page.text).not.toContain("장인 상점");
    expect(page.text).not.toContain("OPENAI LAB");
    expect(page.text).not.toContain("개발 진단 로그");
    expect(page.headers["content-security-policy"]).toContain("'unsafe-inline'");
    expect(page.headers["content-security-policy"]).toContain("'wasm-unsafe-eval'");
    expect(page.headers["content-security-policy"]).toContain("blob:");
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

    const duplicate = await request(app)
      .post("/api/v1/store/mock-purchases")
      .set("Authorization", `Bearer ${token}`)
      .send({ productId: "red-bean-100", idempotencyKey })
      .expect(200);
    expect(duplicate.body).toEqual(expect.objectContaining({ duplicate: true }));
    expect(duplicate.body.inventory).toContainEqual({ itemId: "red-bean-coin", quantity: 100 });
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
  });
});

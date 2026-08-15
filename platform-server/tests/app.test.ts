import request from "supertest";
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
      .expect(200, { hiveMode: "mock", openaiMode: "mock", openaiModel: "mock" });
    const page = await request(app).get("/").expect(200);
    expect(page.text).toContain("게임 연동 베이스캠프");
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
});

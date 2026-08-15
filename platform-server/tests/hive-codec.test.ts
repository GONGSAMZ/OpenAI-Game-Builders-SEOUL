import { describe, expect, it } from "vitest";
import { HiveWebLoginClient } from "../src/integrations/hive/client.js";
import { decodeHivePayload, encodeHivePayload } from "../src/integrations/hive/codec.js";

describe("Hive payload codec", () => {
  it("URL 인코딩과 Base64 인코딩을 왕복한다", () => {
    const payload = { code: "100", state: "상태 값 + / =" };
    expect(decodeHivePayload(encodeHivePayload(payload))).toEqual(payload);
  });

  it("공식 Web Login param 구조로 Sandbox URL을 만든다", () => {
    const client = new HiveWebLoginClient({
      mode: "sandbox",
      appId: "com.example.game.web",
      clientId: "client-id",
      clientSecret: "secret",
      redirectUri: "https://game.example.com/api/v1/auth/hive/callback",
      country: "KR",
      language: "ko"
    });

    const loginUrl = new URL(client.buildLoginUrl());
    expect(loginUrl.origin).toBe("https://sandbox-weblogin.withhive.com");
    expect(loginUrl.pathname).toBe("/login");
    expect(decodeHivePayload(loginUrl.searchParams.get("param") ?? "")).toEqual({
      appid: "com.example.game.web",
      url: "https://game.example.com/api/v1/auth/hive/callback",
      client_id: "client-id",
      response_type: "code",
      country: "KR",
      language: "ko"
    });
  });
});

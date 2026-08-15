import { describe, expect, it } from "vitest";
import { loadConfig } from "../src/config.js";

describe("loadConfig", () => {
  it("외부 키가 없어도 기본 mock 모드로 시작한다", () => {
    const config = loadConfig({});
    expect(config.hive.mode).toBe("mock");
    expect(config.openai.mode).toBe("mock");
    expect(config.port).toBe(3000);
  });

  it("빈 선택 환경변수는 미설정으로 처리한다", () => {
    const config = loadConfig({ HIVE_WEB_SHOP_URL: "", DYNAMODB_TABLE: "" });
    expect(config.hive.webShopUrl).toBeUndefined();
    expect(config.store.dynamodbTable).toBeUndefined();
  });

  it("OpenAI live 모드는 API 키를 요구한다", () => {
    expect(() => loadConfig({ OPENAI_MODE: "live" })).toThrow("OPENAI_API_KEY");
  });

  it("Hive sandbox 모드는 Console 설정값을 요구한다", () => {
    expect(() => loadConfig({ HIVE_MODE: "sandbox" })).toThrow("HIVE_APP_ID");
  });

  it("HIVE 웹 상점 모드는 상점 URL을 요구한다", () => {
    expect(() => loadConfig({ STORE_MODE: "hive-web-shop" })).toThrow("HIVE_WEB_SHOP_URL");
  });

  it("DynamoDB 저장소는 테이블 이름을 요구한다", () => {
    expect(() => loadConfig({ DATA_STORE: "dynamodb" })).toThrow("DYNAMODB_TABLE");
  });
});

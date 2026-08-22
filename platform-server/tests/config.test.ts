import { describe, expect, it } from "vitest";
import { loadConfig } from "../src/config.js";

describe("loadConfig", () => {
  it("외부 키가 없어도 기본 mock 모드로 시작한다", () => {
    const config = loadConfig({});
    expect(config.hive.mode).toBe("mock");
    expect(config.openai.mode).toBe("mock");
    expect(config.port).toBe(3000);
    expect(config.revision).toBe("development");
    expect(config.store.catalogSource).toBe("static");
    expect(config.store.productImageBaseUrl).toBe("http://localhost:3000/store-products");
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

  it("HIVE 카탈로그는 Billing 설정과 실제 HIVE 모드를 요구한다", () => {
    expect(() => loadConfig({ STORE_CATALOG_SOURCE: "hive" })).toThrow("HIVE_MODE");
    expect(() =>
      loadConfig({
        STORE_CATALOG_SOURCE: "hive",
        HIVE_MODE: "sandbox",
        HIVE_APP_ID: "app-id",
        HIVE_CLIENT_ID: "client-id",
        HIVE_CLIENT_SECRET: "client-secret",
        HIVE_REDIRECT_URI: "http://localhost:3000/hive/cb",
        HIVE_BILLING_APP_ID: "billing-app-id"
      })
    ).toThrow("HIVE_BILLING_AUTH_KEY");

    const config = loadConfig({
      STORE_CATALOG_SOURCE: "hive",
      HIVE_MODE: "sandbox",
      HIVE_APP_ID: "app-id",
      HIVE_CLIENT_ID: "client-id",
      HIVE_CLIENT_SECRET: "client-secret",
      HIVE_REDIRECT_URI: "http://localhost:3000/hive/cb",
      HIVE_BILLING_APP_ID: "billing-app-id",
      HIVE_BILLING_AUTH_KEY: "billing-key"
    });
    expect(config.store.catalogSource).toBe("hive");
    expect(config.store.catalogCacheSeconds).toBe(300);
  });

  it("NICEPAY 테스트 모드는 샌드박스 키를 요구한다", () => {
    expect(() => loadConfig({ STORE_MODE: "nicepay-test" })).toThrow("NICEPAY_CLIENT_ID");
    const config = loadConfig({
      STORE_MODE: "nicepay-test",
      NICEPAY_CLIENT_ID: "S2_test-client",
      NICEPAY_SECRET_KEY: "test-secret"
    });
    expect(config.store.mode).toBe("nicepay-test");
    expect(config.nicepay.apiBaseUrl).toBe("https://sandbox-api.nicepay.co.kr");
  });

  it("DynamoDB 저장소는 테이블 이름을 요구한다", () => {
    expect(() => loadConfig({ DATA_STORE: "dynamodb" })).toThrow("DYNAMODB_TABLE");
  });
});

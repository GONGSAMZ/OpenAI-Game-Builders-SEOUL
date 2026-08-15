import { describe, expect, it } from "vitest";
import { loadConfig } from "../src/config.js";

describe("loadConfig", () => {
  it("외부 키가 없어도 기본 mock 모드로 시작한다", () => {
    const config = loadConfig({});
    expect(config.hive.mode).toBe("mock");
    expect(config.openai.mode).toBe("mock");
    expect(config.port).toBe(3000);
  });

  it("OpenAI live 모드는 API 키를 요구한다", () => {
    expect(() => loadConfig({ OPENAI_MODE: "live" })).toThrow("OPENAI_API_KEY");
  });

  it("Hive sandbox 모드는 Console 설정값을 요구한다", () => {
    expect(() => loadConfig({ HIVE_MODE: "sandbox" })).toThrow("HIVE_APP_ID");
  });
});

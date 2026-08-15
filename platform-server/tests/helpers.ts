import type { AppConfig } from "../src/config.js";

export function createTestConfig(overrides: Partial<AppConfig> = {}): AppConfig {
  const base: AppConfig = {
    nodeEnv: "test",
    port: 3000,
    publicBaseUrl: "http://localhost:3000",
    gameOrigin: "http://localhost:3000",
    sessionTtlSeconds: 3600,
    hive: {
      mode: "mock",
      country: "KR",
      language: "ko"
    },
    openai: {
      mode: "mock",
      model: "gpt-5.6-luna"
    }
  };

  return {
    ...base,
    ...overrides,
    hive: { ...base.hive, ...overrides.hive },
    openai: { ...base.openai, ...overrides.openai }
  };
}

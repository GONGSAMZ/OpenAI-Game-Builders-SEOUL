import path from "node:path";
import type { AppConfig } from "../src/config.js";

export function createTestConfig(overrides: Partial<AppConfig> = {}): AppConfig {
  const base: AppConfig = {
    nodeEnv: "test",
    port: 3000,
    publicBaseUrl: "http://localhost:3000",
    gameOrigin: "http://localhost:3000",
    gameBuildDirectory: path.resolve(process.cwd(), "game-dist"),
    sessionTtlSeconds: 3600,
    revision: "test-revision",
    hive: {
      mode: "mock",
      country: "KR",
      language: "ko"
    },
    store: {
      mode: "mock",
      devToolsEnabled: true,
      dataStore: "memory"
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
    store: { ...base.store, ...overrides.store },
    openai: { ...base.openai, ...overrides.openai }
  };
}

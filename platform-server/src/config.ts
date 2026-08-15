import path from "node:path";
import { z } from "zod";

const optionalUrl = z.preprocess(
  (value) => (value === "" ? undefined : value),
  z.string().url().optional()
);
const optionalNonEmptyString = z.preprocess(
  (value) => (value === "" ? undefined : value),
  z.string().min(1).optional()
);

const environmentSchema = z.object({
  NODE_ENV: z.enum(["development", "test", "production"]).default("development"),
  PORT: z.coerce.number().int().min(1).max(65_535).default(3000),
  PUBLIC_BASE_URL: z.string().url().default("http://localhost:3000"),
  GAME_ORIGIN: z.string().url().default("http://localhost:3000"),
  GAME_BUILD_DIR: z.string().optional(),
  SESSION_TTL_SECONDS: z.coerce.number().int().min(60).max(86_400).default(3600),
  HIVE_MODE: z.enum(["mock", "sandbox", "production"]).default("mock"),
  HIVE_APP_ID: z.string().optional(),
  HIVE_CLIENT_ID: z.string().optional(),
  HIVE_CLIENT_SECRET: z.string().optional(),
  HIVE_REDIRECT_URI: z.string().url().optional(),
  HIVE_COUNTRY: z.string().length(2).default("KR"),
  HIVE_LANGUAGE: z.string().min(2).max(8).default("ko"),
  HIVE_WEB_SHOP_URL: optionalUrl,
  STORE_MODE: z.enum(["mock", "hive-web-shop"]).default("mock"),
  DATA_STORE: z.enum(["memory", "dynamodb"]).default("memory"),
  DYNAMODB_TABLE: optionalNonEmptyString,
  OPENAI_MODE: z.enum(["mock", "live"]).default("mock"),
  OPENAI_API_KEY: z.string().optional(),
  OPENAI_MODEL: z.string().min(1).default("gpt-5.6-luna")
});

export type HiveMode = "mock" | "sandbox" | "production";
export type OpenAiMode = "mock" | "live";
export type StoreMode = "mock" | "hive-web-shop";

export interface AppConfig {
  nodeEnv: "development" | "test" | "production";
  port: number;
  publicBaseUrl: string;
  gameOrigin: string;
  gameBuildDirectory: string;
  sessionTtlSeconds: number;
  hive: {
    mode: HiveMode;
    appId?: string;
    clientId?: string;
    clientSecret?: string;
    redirectUri?: string;
    country: string;
    language: string;
    webShopUrl?: string;
  };
  store: {
    mode: StoreMode;
    dataStore: "memory" | "dynamodb";
    dynamodbTable?: string;
  };
  openai: {
    mode: OpenAiMode;
    apiKey?: string;
    model: string;
  };
}

export function loadConfig(environment: NodeJS.ProcessEnv = process.env): AppConfig {
  const parsed = environmentSchema.parse(environment);

  if (parsed.HIVE_MODE !== "mock") {
    const requiredHiveValues = [
      parsed.HIVE_APP_ID,
      parsed.HIVE_CLIENT_ID,
      parsed.HIVE_CLIENT_SECRET,
      parsed.HIVE_REDIRECT_URI
    ];

    if (requiredHiveValues.some((value) => !value)) {
      throw new Error(
        "HIVE_MODE이 mock이 아니면 HIVE_APP_ID, HIVE_CLIENT_ID, " +
          "HIVE_CLIENT_SECRET, HIVE_REDIRECT_URI가 모두 필요합니다."
      );
    }
  }

  if (parsed.OPENAI_MODE === "live" && !parsed.OPENAI_API_KEY) {
    throw new Error("OPENAI_MODE=live이면 OPENAI_API_KEY가 필요합니다.");
  }

  if (parsed.STORE_MODE === "hive-web-shop" && !parsed.HIVE_WEB_SHOP_URL) {
    throw new Error("STORE_MODE=hive-web-shop이면 HIVE_WEB_SHOP_URL이 필요합니다.");
  }

  if (parsed.DATA_STORE === "dynamodb" && !parsed.DYNAMODB_TABLE) {
    throw new Error("DATA_STORE=dynamodb이면 DYNAMODB_TABLE이 필요합니다.");
  }

  return {
    nodeEnv: parsed.NODE_ENV,
    port: parsed.PORT,
    publicBaseUrl: parsed.PUBLIC_BASE_URL.replace(/\/$/, ""),
    gameOrigin: new URL(parsed.GAME_ORIGIN).origin,
    gameBuildDirectory: path.resolve(parsed.GAME_BUILD_DIR ?? path.join(process.cwd(), "game-dist")),
    sessionTtlSeconds: parsed.SESSION_TTL_SECONDS,
    hive: {
      mode: parsed.HIVE_MODE,
      appId: parsed.HIVE_APP_ID,
      clientId: parsed.HIVE_CLIENT_ID,
      clientSecret: parsed.HIVE_CLIENT_SECRET,
      redirectUri: parsed.HIVE_REDIRECT_URI,
      country: parsed.HIVE_COUNTRY.toUpperCase(),
      language: parsed.HIVE_LANGUAGE,
      webShopUrl: parsed.HIVE_WEB_SHOP_URL
    },
    store: {
      mode: parsed.STORE_MODE,
      dataStore: parsed.DATA_STORE,
      dynamodbTable: parsed.DYNAMODB_TABLE
    },
    openai: {
      mode: parsed.OPENAI_MODE,
      apiKey: parsed.OPENAI_API_KEY,
      model: parsed.OPENAI_MODEL
    }
  };
}

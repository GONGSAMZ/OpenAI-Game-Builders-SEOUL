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
  SESSION_TTL_SECONDS: z.coerce.number().int().min(60).max(2_592_000).default(604_800),
  HIVE_MODE: z.enum(["mock", "sandbox", "production"]).default("mock"),
  HIVE_APP_ID: z.string().optional(),
  HIVE_CLIENT_ID: z.string().optional(),
  HIVE_CLIENT_SECRET: z.string().optional(),
  HIVE_BILLING_APP_ID: optionalNonEmptyString,
  HIVE_BILLING_AUTH_KEY: optionalNonEmptyString,
  HIVE_REDIRECT_URI: z.string().url().optional(),
  HIVE_COUNTRY: z.string().length(2).default("KR"),
  HIVE_LANGUAGE: z.string().min(2).max(8).default("ko"),
  HIVE_WEB_SHOP_URL: optionalUrl,
  STORE_MODE: z.enum(["mock", "nicepay-test", "hive-web-shop"]).default("mock"),
  STORE_CATALOG_SOURCE: z.enum(["static", "hive"]).default("static"),
  STORE_CATALOG_CACHE_SECONDS: z.coerce.number().int().min(30).max(86_400).default(300),
  STORE_PRODUCT_IMAGE_BASE_URL: optionalUrl,
  STORE_DEV_TOOLS: z.stringbool().default(false),
  NICEPAY_CLIENT_ID: optionalNonEmptyString,
  NICEPAY_SECRET_KEY: optionalNonEmptyString,
  PURCHASE_CURSOR_SECRET: optionalNonEmptyString,
  DATA_STORE: z.enum(["memory", "dynamodb"]).default("memory"),
  DYNAMODB_TABLE: optionalNonEmptyString,
  OPENAI_MODE: z.enum(["mock", "live"]).default("mock"),
  OPENAI_API_KEY: z.string().optional(),
  OPENAI_MODEL: z.string().min(1).default("gpt-5.6-luna"),
  APP_REVISION: z.string().min(1).max(200).default("development")
});

export type HiveMode = "mock" | "sandbox" | "production";
export type OpenAiMode = "mock" | "live";
export type StoreMode = "mock" | "nicepay-test" | "hive-web-shop";

export interface AppConfig {
  nodeEnv: "development" | "test" | "production";
  port: number;
  publicBaseUrl: string;
  gameOrigin: string;
  gameBuildDirectory: string;
  sessionTtlSeconds: number;
  revision: string;
  hive: {
    mode: HiveMode;
    appId?: string;
    clientId?: string;
    clientSecret?: string;
    redirectUri?: string;
    country: string;
    language: string;
    webShopUrl?: string;
    billingAppId?: string;
    billingAuthKey?: string;
  };
  store: {
    mode: StoreMode;
    catalogSource: "static" | "hive";
    catalogCacheSeconds: number;
    productImageBaseUrl: string;
    devToolsEnabled: boolean;
    dataStore: "memory" | "dynamodb";
    dynamodbTable?: string;
    cursorSigningSecret: string;
  };
  nicepay: {
    clientId?: string;
    secretKey?: string;
    apiBaseUrl: string;
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

  if (parsed.STORE_MODE === "hive-web-shop") {
    if (!parsed.HIVE_WEB_SHOP_URL) {
      throw new Error("STORE_MODE=hive-web-shop이면 HIVE_WEB_SHOP_URL이 필요합니다.");
    }
    if (!parsed.HIVE_BILLING_AUTH_KEY) {
      throw new Error("STORE_MODE=hive-web-shop이면 HIVE_BILLING_AUTH_KEY가 필요합니다.");
    }
    if (!(parsed.HIVE_BILLING_APP_ID ?? parsed.HIVE_APP_ID)) {
      throw new Error("STORE_MODE=hive-web-shop이면 HIVE_BILLING_APP_ID 또는 HIVE_APP_ID가 필요합니다.");
    }
  }

  if (parsed.STORE_CATALOG_SOURCE === "hive") {
    if (parsed.HIVE_MODE === "mock") {
      throw new Error("STORE_CATALOG_SOURCE=hive이면 HIVE_MODE은 sandbox 또는 production이어야 합니다.");
    }
    if (!parsed.HIVE_BILLING_AUTH_KEY) {
      throw new Error("STORE_CATALOG_SOURCE=hive이면 HIVE_BILLING_AUTH_KEY가 필요합니다.");
    }
    if (!(parsed.HIVE_BILLING_APP_ID ?? parsed.HIVE_APP_ID)) {
      throw new Error("STORE_CATALOG_SOURCE=hive이면 HIVE_BILLING_APP_ID 또는 HIVE_APP_ID가 필요합니다.");
    }
  }

  if (parsed.STORE_MODE === "nicepay-test") {
    if (!parsed.NICEPAY_CLIENT_ID || !parsed.NICEPAY_SECRET_KEY) {
      throw new Error(
        "STORE_MODE=nicepay-test이면 NICEPAY_CLIENT_ID와 NICEPAY_SECRET_KEY가 필요합니다."
      );
    }
  }

  if (parsed.DATA_STORE === "dynamodb" && !parsed.DYNAMODB_TABLE) {
    throw new Error("DATA_STORE=dynamodb이면 DYNAMODB_TABLE이 필요합니다.");
  }

  const cursorSigningSecret =
    parsed.PURCHASE_CURSOR_SECRET ??
    parsed.NICEPAY_SECRET_KEY ??
    parsed.HIVE_BILLING_AUTH_KEY;
  if (parsed.NODE_ENV === "production" && !cursorSigningSecret) {
    throw new Error(
      "운영 환경의 구매 내역 cursor 서명을 위해 PURCHASE_CURSOR_SECRET 또는 결제 비밀키가 필요합니다."
    );
  }

  return {
    nodeEnv: parsed.NODE_ENV,
    port: parsed.PORT,
    publicBaseUrl: parsed.PUBLIC_BASE_URL.replace(/\/$/, ""),
    gameOrigin: new URL(parsed.GAME_ORIGIN).origin,
    gameBuildDirectory: path.resolve(parsed.GAME_BUILD_DIR ?? path.join(process.cwd(), "game-dist")),
    sessionTtlSeconds: parsed.SESSION_TTL_SECONDS,
    revision: parsed.APP_REVISION,
    hive: {
      mode: parsed.HIVE_MODE,
      appId: parsed.HIVE_APP_ID,
      clientId: parsed.HIVE_CLIENT_ID,
      clientSecret: parsed.HIVE_CLIENT_SECRET,
      redirectUri: parsed.HIVE_REDIRECT_URI,
      country: parsed.HIVE_COUNTRY.toUpperCase(),
      language: parsed.HIVE_LANGUAGE,
      webShopUrl: parsed.HIVE_WEB_SHOP_URL,
      billingAppId: parsed.HIVE_BILLING_APP_ID ?? parsed.HIVE_APP_ID,
      billingAuthKey: parsed.HIVE_BILLING_AUTH_KEY
    },
    store: {
      mode: parsed.STORE_MODE,
      catalogSource: parsed.STORE_CATALOG_SOURCE,
      catalogCacheSeconds: parsed.STORE_CATALOG_CACHE_SECONDS,
      productImageBaseUrl: (
        parsed.STORE_PRODUCT_IMAGE_BASE_URL ??
        `${parsed.PUBLIC_BASE_URL.replace(/\/$/, "")}/store-products`
      ).replace(/\/$/, ""),
      devToolsEnabled: parsed.STORE_DEV_TOOLS,
      dataStore: parsed.DATA_STORE,
      dynamodbTable: parsed.DYNAMODB_TABLE,
      cursorSigningSecret: cursorSigningSecret ?? "openai-game-builders-local-cursor-key"
    },
    nicepay: {
      clientId: parsed.NICEPAY_CLIENT_ID,
      secretKey: parsed.NICEPAY_SECRET_KEY,
      apiBaseUrl: "https://sandbox-api.nicepay.co.kr"
    },
    openai: {
      mode: parsed.OPENAI_MODE,
      apiKey: parsed.OPENAI_API_KEY,
      model: parsed.OPENAI_MODEL
    }
  };
}

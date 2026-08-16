import path from "node:path";
import cors from "cors";
import express, { type NextFunction, type Request, type Response } from "express";
import rateLimit from "express-rate-limit";
import helmet from "helmet";
import { z, ZodError } from "zod";
import { sendAuthBridgePage } from "./auth-page.js";
import type { AppConfig } from "./config.js";
import { HttpError, readCookie, requireSession, type AuthenticatedLocals } from "./http.js";
import { AiService } from "./integrations/openai/service.js";
import { HiveWebLoginClient } from "./integrations/hive/client.js";
import { decodeHivePayload } from "./integrations/hive/codec.js";
import { InMemorySessionStore, OneTimeAttemptStore } from "./session-store.js";
import { findProduct, storeCatalog } from "./store/catalog.js";
import { createMarketStore, type MarketStore } from "./store/store.js";

const publicDirectory = path.resolve(process.cwd(), "public");
const loginCookieName = "hive_login_attempt";
const loginCookiePath = "/";

const npcReactionSchema = z.object({
  situation: z.string().trim().min(1).max(500),
  playerAction: z.string().trim().min(1).max(500),
  locale: z.enum(["ko", "en"]).default("ko")
});

const mockPurchaseSchema = z.object({
  productId: z.string().trim().min(1).max(100),
  idempotencyKey: z.string().uuid()
});

const hiveWebShopProfileSchema = z.object({
  cs_code: z.coerce.number().int().positive().safe()
});

interface AppDependencies {
  config: AppConfig;
  sessions?: InMemorySessionStore;
  loginAttempts?: OneTimeAttemptStore;
  hiveClient?: HiveWebLoginClient;
  aiService?: AiService;
  marketStore?: MarketStore;
}

function setUnityAssetHeaders(response: Response, filePath: string): void {
  if (filePath.endsWith(".html")) {
    response.setHeader("Cache-Control", "no-store");
    return;
  }

  if (!filePath.endsWith(".unityweb")) return;

  response.setHeader("Content-Encoding", "gzip");
  if (filePath.endsWith(".wasm.unityweb")) {
    response.setHeader("Content-Type", "application/wasm");
  } else if (filePath.endsWith(".js.unityweb")) {
    response.setHeader("Content-Type", "text/javascript; charset=utf-8");
  } else {
    response.setHeader("Content-Type", "application/octet-stream");
  }
}

function setPortalAssetHeaders(response: Response, filePath: string): void {
  if (path.basename(filePath) === "index.html") {
    response.setHeader("Cache-Control", "no-store");
  }
}

function sessionResponse(session: ReturnType<InMemorySessionStore["create"]>) {
  return {
    subject: session.subject,
    provider: session.provider,
    playerId: session.playerId,
    expiresAt: session.expiresAt
  };
}

export function createApp(dependencies: AppDependencies) {
  const { config } = dependencies;
  const sessions = dependencies.sessions ?? new InMemorySessionStore(config.sessionTtlSeconds);
  const loginAttempts = dependencies.loginAttempts ?? new OneTimeAttemptStore();
  const hiveClient = dependencies.hiveClient ?? new HiveWebLoginClient(config.hive);
  const aiService = dependencies.aiService ?? new AiService(config.openai);
  const marketStore = dependencies.marketStore ?? createMarketStore(config);
  const app = express();

  app.disable("x-powered-by");
  app.use(
    helmet({
      crossOriginOpenerPolicy: { policy: "same-origin-allow-popups" },
      contentSecurityPolicy: {
        directives: {
          // Unity's generated WebGL template bootstraps the loader with an
          // inline script. The auth callback page still supplies its own
          // stricter nonce-based policy in auth-page.ts.
          "script-src": ["'self'", "'unsafe-inline'", "'wasm-unsafe-eval'", "blob:"],
          "worker-src": ["'self'", "blob:"],
          "img-src": ["'self'", "data:", "blob:"],
          "connect-src": ["'self'"],
          "frame-src": ["'self'"]
        }
      }
    })
  );
  app.use(
    cors({
      origin(origin, callback) {
        const serverOrigin = new URL(config.publicBaseUrl).origin;
        if (!origin || origin === config.gameOrigin || origin === serverOrigin) {
          callback(null, true);
          return;
        }
        callback(new HttpError(403, "허용되지 않은 웹게임 Origin입니다."));
      },
      credentials: true
    })
  );
  app.use(express.json({ limit: "32kb" }));

  app.get("/api/v1/health", (_request, response) => {
    response.json({ status: "ok", timestamp: new Date().toISOString() });
  });

  app.get("/api/v1/config/public", (_request, response) => {
    response.json({
      hiveMode: config.hive.mode,
      storeMode: config.store.mode,
      hiveWebShopUrl: config.hive.webShopUrl ?? null,
      openaiMode: config.openai.mode,
      openaiModel: config.openai.mode === "live" ? config.openai.model : "mock"
    });
  });

  app.get("/api/v1/auth/hive/login", (_request, response) => {
    const attempt = loginAttempts.create();
    response.cookie(loginCookieName, attempt, {
      httpOnly: true,
      secure: config.nodeEnv === "production",
      sameSite: config.nodeEnv === "production" ? "none" : "lax",
      maxAge: 10 * 60 * 1000,
      path: loginCookiePath
    });

    const loginUrl =
      config.hive.mode === "mock"
        ? `${config.publicBaseUrl}/api/v1/auth/hive/mock/complete`
        : hiveClient.buildLoginUrl();

    response.set("cache-control", "no-store").json({ loginUrl });
  });

  app.get("/api/v1/auth/hive/mock/complete", (request, response) => {
    if (config.hive.mode !== "mock") {
      sendAuthBridgePage(response, config.gameOrigin, {
        type: "HIVE_AUTH_ERROR",
        message: "Mock Hive 로그인이 비활성화되어 있습니다."
      });
      return;
    }

    const attempt = readCookie(request, loginCookieName);
    if (!loginAttempts.consume(attempt)) {
      sendAuthBridgePage(response, config.gameOrigin, {
        type: "HIVE_AUTH_ERROR",
        message: "로그인 요청이 없거나 만료되었습니다."
      });
      return;
    }

    response.clearCookie(loginCookieName, { path: loginCookiePath });
    const session = sessions.create({
      subject: "mock-hive:local-player",
      provider: "mock-hive",
      playerId: "local-player",
      idpIndex: 1,
      idpUserId: "local-player"
    });

    sendAuthBridgePage(response, config.gameOrigin, {
      type: "HIVE_AUTH_SUCCESS",
      sessionToken: session.token
    });
  });

  app.get(["/hive/cb", "/api/v1/auth/hive/callback"], async (request, response) => {
    const attempt = readCookie(request, loginCookieName);
    if (!loginAttempts.consume(attempt)) {
      sendAuthBridgePage(response, config.gameOrigin, {
        type: "HIVE_AUTH_ERROR",
        message: "로그인 요청이 없거나 만료되었습니다."
      });
      return;
    }

    try {
      if (config.hive.mode === "mock") throw new Error("Hive 실제 연동이 비활성화되어 있습니다.");
      const encodedResult = z.string().min(1).parse(request.query.res);
      const callbackPayload = decodeHivePayload<{ code: string; state?: string }>(encodedResult);
      const verified = await hiveClient.verifyCallback(callbackPayload);
      const playerId = verified.user_info?.user_id?.toString();
      const session = sessions.create({
        subject: playerId ?? `${verified.idp_index}:${verified.idp_user_id}`,
        provider: "hive",
        playerId,
        idpIndex: verified.idp_index,
        idpUserId: verified.idp_user_id
      });

      response.clearCookie(loginCookieName, { path: loginCookiePath });
      sendAuthBridgePage(response, config.gameOrigin, {
        type: "HIVE_AUTH_SUCCESS",
        sessionToken: session.token
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : "Hive 로그인 처리에 실패했습니다.";
      sendAuthBridgePage(response, config.gameOrigin, {
        type: "HIVE_AUTH_ERROR",
        message
      });
    }
  });

  app.get(
    "/api/v1/auth/session",
    requireSession(sessions),
    (_request: Request, response: Response) => {
      const { session } = response.locals as AuthenticatedLocals;
      response.json({ session: sessionResponse(session) });
    }
  );

  app.delete(
    "/api/v1/auth/session",
    requireSession(sessions),
    (_request: Request, response: Response) => {
      const { session } = response.locals as AuthenticatedLocals;
      sessions.delete(session.token);
      response.status(204).end();
    }
  );

  app.get("/api/v1/store/catalog", (_request, response) => {
    response.json({ mode: config.store.mode, products: storeCatalog });
  });

  app.get(
    "/api/v1/store/me",
    requireSession(sessions),
    async (_request: Request, response: Response) => {
      const { session } = response.locals as AuthenticatedLocals;
      response.json({ inventory: await marketStore.getInventory(session.subject) });
    }
  );

  const purchaseLimiter = rateLimit({
    windowMs: 60_000,
    limit: 10,
    standardHeaders: "draft-8",
    legacyHeaders: false,
    message: { error: { code: "RATE_LIMITED", message: "구매 요청이 너무 많습니다." } }
  });

  app.post(
    "/api/v1/store/mock-purchases",
    purchaseLimiter,
    requireSession(sessions),
    async (request: Request, response: Response) => {
      if (config.store.mode !== "mock") {
        throw new HttpError(403, "실제 HIVE 웹 상점 모드에서는 mock 구매를 사용할 수 없습니다.");
      }

      const input = mockPurchaseSchema.parse(request.body);
      const product = findProduct(input.productId);
      if (!product) throw new HttpError(404, "존재하지 않는 상품입니다.");

      const { session } = response.locals as AuthenticatedLocals;
      const result = await marketStore.grantMockPurchase(
        session.subject,
        product,
        input.idempotencyKey
      );
      response.status(result.duplicate ? 200 : 201).json(result);
    }
  );

  app.post("/api/v1/hive/web-shop/in-game-info", (request, response) => {
    const { cs_code: csCode } = hiveWebShopProfileSchema.parse(request.body);
    response.json({
      result_code: 200,
      result_message: "success",
      cs_code: csCode,
      data: [
        {
          server_id: "global",
          server_name: "붕어빵 마을",
          channels: [
            {
              channel_id: "0",
              channel_name: "-",
              characters: [
                {
                  character_id: String(csCode),
                  character_name: `붕어빵 장인 ${csCode}`,
                  character_level: "1"
                }
              ]
            }
          ]
        }
      ]
    });
  });

  const aiLimiter = rateLimit({
    windowMs: 60_000,
    limit: 20,
    standardHeaders: "draft-8",
    legacyHeaders: false,
    message: { error: { code: "RATE_LIMITED", message: "AI 요청이 너무 많습니다." } }
  });

  app.post(
    "/api/v1/ai/npc-reaction",
    aiLimiter,
    requireSession(sessions),
    async (request, response) => {
      const input = npcReactionSchema.parse(request.body);
      const result = await aiService.createNpcReaction(input);
      response.json(result);
    }
  );

  app.use(
    "/game",
    express.static(config.gameBuildDirectory, {
      index: "index.html",
      maxAge: config.nodeEnv === "production" ? "1h" : 0,
      setHeaders: setUnityAssetHeaders
    })
  );

  app.use(
    express.static(publicDirectory, {
      extensions: ["html"],
      maxAge: config.nodeEnv === "production" ? "1h" : 0,
      setHeaders: setPortalAssetHeaders
    })
  );

  app.use((_request, response) => {
    response.status(404).json({ error: { code: "NOT_FOUND", message: "요청한 경로가 없습니다." } });
  });

  app.use((error: unknown, _request: Request, response: Response, _next: NextFunction) => {
    if (error instanceof ZodError) {
      response.status(400).json({
        error: { code: "INVALID_REQUEST", message: "요청 데이터 형식이 올바르지 않습니다.", details: error.issues }
      });
      return;
    }

    const statusCode = error instanceof HttpError ? error.statusCode : 500;
    const message =
      error instanceof Error && (statusCode < 500 || config.nodeEnv !== "production")
        ? error.message
        : "서버 내부 오류가 발생했습니다.";
    response.status(statusCode).json({ error: { code: "REQUEST_FAILED", message } });
  });

  return app;
}

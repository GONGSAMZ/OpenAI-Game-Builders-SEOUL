import { randomUUID } from "node:crypto";
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
import {
  HiveBillingClient,
  type HiveBillingGateway
} from "./integrations/hive/billing-client.js";
import { decodeHivePayload } from "./integrations/hive/codec.js";
import {
  createSessionStore,
  OneTimeAttemptStore,
  type GameSession,
  type SessionStore
} from "./session-store.js";
import { findProduct, findProductByMarketPid, storeCatalog } from "./store/catalog.js";
import {
  createMarketStore,
  InsufficientTestPointsError,
  type MarketStore
} from "./store/store.js";

const publicDirectory = path.resolve(process.cwd(), "public");
const loginCookieName = "hive_login_attempt";
const loginCookiePath = "/";
const sessionCookieName = "game_session";
const sessionCookiePath = "/";

const npcReactionSchema = z.object({
  situation: z.string().trim().min(1).max(500),
  playerAction: z.string().trim().min(1).max(500),
  locale: z.enum(["ko", "en"]).default("ko")
});

const mockPurchaseSchema = z.object({
  productId: z.string().trim().min(1).max(100),
  idempotencyKey: z.string().uuid()
});

const devTestPointCreditSchema = z.object({
  amount: z.number().int().min(1).max(100_000),
  idempotencyKey: z.string().uuid()
});

const moldEquipmentSchema = z.object({
  itemId: z.literal("golden-pan").nullable()
});

const hiveWebShopProfileSchema = z.object({
  cs_code: z.coerce.number().int().positive().safe()
});

const hivePaymentNotificationSchema = z.object({
  type: z.enum(["paid", "cancelled"]),
  market_id: z.coerce.string().min(1).max(20),
  order_id: z.string().trim().min(1).max(200),
  market_pid: z.string().trim().min(1).max(300),
  vid: z.coerce.string().regex(/^\d+$/).max(20),
  vid_type: z.string().optional(),
  server_id: z.string().trim().min(1).max(100),
  appid: z.string().trim().min(1).max(300),
  quantity: z.coerce.number().int().min(1).max(100),
  purchase_bypass_info: z.string().min(1).max(32_000)
});

interface AppDependencies {
  config: AppConfig;
  sessions?: SessionStore;
  loginAttempts?: OneTimeAttemptStore;
  hiveClient?: HiveWebLoginClient;
  billingClient?: HiveBillingGateway;
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

function sessionResponse(session: GameSession) {
  return {
    subject: session.subject,
    provider: session.provider,
    playerId: session.playerId,
    expiresAt: session.expiresAt
  };
}

function setSessionCookie(response: Response, config: AppConfig, token: string): void {
  response.cookie(sessionCookieName, token, {
    httpOnly: true,
    secure: config.nodeEnv === "production",
    sameSite: "lax",
    maxAge: config.sessionTtlSeconds * 1000,
    path: sessionCookiePath
  });
}

function clearSessionCookie(response: Response, config: AppConfig): void {
  response.clearCookie(sessionCookieName, {
    httpOnly: true,
    secure: config.nodeEnv === "production",
    sameSite: "lax",
    path: sessionCookiePath
  });
}

export function createApp(dependencies: AppDependencies) {
  const { config } = dependencies;
  const sessions = dependencies.sessions ?? createSessionStore(config);
  const loginAttempts = dependencies.loginAttempts ?? new OneTimeAttemptStore();
  const hiveClient = dependencies.hiveClient ?? new HiveWebLoginClient(config.hive);
  const billingClient = dependencies.billingClient ?? new HiveBillingClient(config.hive);
  const aiService = dependencies.aiService ?? new AiService(config.openai);
  const marketStore = dependencies.marketStore ?? createMarketStore(config);
  const app = express();
  const requireGameSession = requireSession(sessions, sessionCookieName);

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

  app.use((request, response, next) => {
    const requestId = request.header("x-request-id")?.slice(0, 128) ?? randomUUID();
    const startedAt = performance.now();
    response.setHeader("x-request-id", requestId);
    response.on("finish", () => {
      if (config.nodeEnv !== "production") return;
      console.log(
        JSON.stringify({
          type: "http_request",
          requestId,
          method: request.method,
          path: request.path,
          status: response.statusCode,
          durationMs: Math.round((performance.now() - startedAt) * 100) / 100,
          revision: config.revision
        })
      );
    });
    next();
  });

  app.get("/api/v1/health", (_request, response) => {
    response.json({ status: "ok", revision: config.revision, timestamp: new Date().toISOString() });
  });

  app.get("/api/v1/version", (_request, response) => {
    response.set("cache-control", "no-store").json({ revision: config.revision });
  });

  app.get("/api/v1/config/public", (_request, response) => {
    response.json({
      hiveMode: config.hive.mode,
      storeMode: config.store.mode,
      storeDevTools: config.store.devToolsEnabled,
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

  app.get("/api/v1/auth/hive/mock/complete", async (request, response) => {
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
    const session = await sessions.create({
      subject: "mock-hive:local-player",
      provider: "mock-hive",
      playerId: "local-player",
      idpIndex: 1,
      idpUserId: "local-player"
    });
    setSessionCookie(response, config, session.token);

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
      const session = await sessions.create({
        subject: playerId ?? `${verified.idp_index}:${verified.idp_user_id}`,
        provider: "hive",
        playerId,
        idpIndex: verified.idp_index,
        idpUserId: verified.idp_user_id
      });

      setSessionCookie(response, config, session.token);
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
    requireGameSession,
    (_request: Request, response: Response) => {
      const { session } = response.locals as AuthenticatedLocals;
      response.json({ session: sessionResponse(session) });
    }
  );

  app.delete(
    "/api/v1/auth/session",
    requireGameSession,
    async (_request: Request, response: Response) => {
      const { session } = response.locals as AuthenticatedLocals;
      await sessions.delete(session.token);
      clearSessionCookie(response, config);
      response.status(204).end();
    }
  );

  app.get("/api/v1/store/catalog", (_request, response) => {
    response.json({ mode: config.store.mode, products: storeCatalog });
  });

  app.get(
    "/api/v1/store/me",
    requireGameSession,
    async (_request: Request, response: Response) => {
      const { session } = response.locals as AuthenticatedLocals;
      response.json(await marketStore.getPlayerState(session.subject));
    }
  );

  app.put(
    "/api/v1/store/equipment/mold",
    requireGameSession,
    async (request: Request, response: Response) => {
      const input = moldEquipmentSchema.parse(request.body);
      const { session } = response.locals as AuthenticatedLocals;

      if (input.itemId) {
        const inventory = await marketStore.getInventory(session.subject);
        const ownsItem = inventory.some(
          (entry) => entry.itemId === input.itemId && entry.quantity > 0
        );
        if (!ownsItem) throw new HttpError(409, "보유하지 않은 황금 붕어빵 틀입니다.");
      }

      response.json(await marketStore.setMoldSkin(session.subject, input.itemId));
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
    requireGameSession,
    async (request: Request, response: Response) => {
      if (config.store.mode !== "mock") {
        throw new HttpError(403, "실제 HIVE 웹 상점 모드에서는 mock 구매를 사용할 수 없습니다.");
      }

      const input = mockPurchaseSchema.parse(request.body);
      const product = findProduct(input.productId);
      if (!product) throw new HttpError(404, "존재하지 않는 상품입니다.");

      const { session } = response.locals as AuthenticatedLocals;
      let result;
      try {
        result = await marketStore.grantMockPurchase(
          session.subject,
          product,
          input.idempotencyKey
        );
      } catch (error) {
        if (error instanceof InsufficientTestPointsError) {
          throw new HttpError(409, error.message);
        }
        throw error;
      }
      response.status(result.duplicate ? 200 : 201).json({
        ...result,
        equipment: await marketStore.getEquipment(session.subject)
      });
    }
  );

  app.post(
    "/api/v1/store/dev-test-points",
    purchaseLimiter,
    requireGameSession,
    async (request: Request, response: Response) => {
      if (!config.store.devToolsEnabled) {
        throw new HttpError(404, "개발용 테스트 포인트 기능이 비활성화되어 있습니다.");
      }

      const input = devTestPointCreditSchema.parse(request.body);
      const { session } = response.locals as AuthenticatedLocals;
      const result = await marketStore.creditTestPoints(
        session.subject,
        input.amount,
        input.idempotencyKey
      );
      response.status(result.duplicate ? 200 : 201).json({
        ...result,
        inventory: await marketStore.getInventory(session.subject),
        equipment: await marketStore.getEquipment(session.subject)
      });
    }
  );

  app.post(
    "/api/v1/store/dev-grants",
    purchaseLimiter,
    requireGameSession,
    async (request: Request, response: Response) => {
      if (!config.store.devToolsEnabled) {
        throw new HttpError(404, "개발용 재화 지급 기능이 비활성화되어 있습니다.");
      }

      const input = mockPurchaseSchema.parse(request.body);
      const product = findProduct(input.productId);
      if (!product) throw new HttpError(404, "존재하지 않는 상품입니다.");
      if (product.grant.itemId !== "red-bean-coin") {
        throw new HttpError(400, "개발 도구에서는 인게임 재화만 지급할 수 있습니다.");
      }

      const { session } = response.locals as AuthenticatedLocals;
      const result = await marketStore.grantPurchase(session.subject, product, {
        provider: "dev-tools",
        transactionId: input.idempotencyKey
      });
      response.status(result.duplicate ? 200 : 201).json({
        ...result,
        equipment: await marketStore.getEquipment(session.subject)
      });
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

  app.post("/api/v1/hive/web-shop/payment-notifications", async (request, response) => {
    if (config.store.mode !== "hive-web-shop") {
      throw new HttpError(404, "HIVE 웹 상점 결제 연동이 비활성화되어 있습니다.");
    }

    const input = hivePaymentNotificationSchema.parse(request.body);
    if (input.type === "cancelled") {
      response.json({ result: 0, result_msg: "cancelled payment acknowledged" });
      return;
    }
    if (input.market_id !== "15") {
      throw new HttpError(400, "HIVE 웹 PG 결제(market_id=15)가 아닙니다.");
    }
    if (input.appid !== config.hive.billingAppId) {
      throw new HttpError(400, "결제 알림 AppID가 서버 설정과 일치하지 않습니다.");
    }
    if (input.vid_type && input.vid_type !== "v4") {
      throw new HttpError(400, "HIVE Authentication v4 PlayerID 결제가 아닙니다.");
    }

    const product = findProductByMarketPid(input.market_pid);
    if (!product) throw new HttpError(400, "등록되지 않은 HIVE 상품 PID입니다.");

    const unconsumed = await billingClient.findUnconsumedPurchase({
      playerId: input.vid,
      serverId: input.server_id,
      orderId: input.order_id
    });
    if (
      unconsumed.marketId !== input.market_id ||
      unconsumed.marketPid !== input.market_pid ||
      unconsumed.orderId !== input.order_id ||
      unconsumed.serverId !== input.server_id ||
      unconsumed.playerId !== input.vid ||
      unconsumed.quantity !== input.quantity ||
      unconsumed.purchaseBypassInfo !== input.purchase_bypass_info
    ) {
      throw new HttpError(400, "HIVE 미소비 주문과 결제 알림 정보가 일치하지 않습니다.");
    }

    const verified = await billingClient.verifyReceipt(unconsumed.purchaseBypassInfo);
    if (
      verified.marketId !== input.market_id ||
      verified.marketPid !== input.market_pid ||
      (verified.marketTransactionId && verified.marketTransactionId !== input.order_id) ||
      verified.quantity !== input.quantity
    ) {
      throw new HttpError(400, "HIVE 영수증과 결제 알림 정보가 일치하지 않습니다.");
    }

    const result = await marketStore.grantPurchase(input.vid, product, {
      provider: "hive-web-shop",
      transactionId: verified.transactionId,
      quantity: verified.quantity
    });
    await billingClient.confirmDelivery({
      transactionId: verified.transactionId,
      playerId: input.vid,
      itemId: product.grant.itemId,
      itemName: product.name,
      quantity: product.grant.quantity * verified.quantity
    });

    response.json({
      result: 0,
      result_msg: "success",
      duplicate: result.duplicate
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
    requireGameSession,
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

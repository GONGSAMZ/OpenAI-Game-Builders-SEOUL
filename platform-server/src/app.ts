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

const publicDirectory = path.resolve(process.cwd(), "public");
const loginCookieName = "hive_login_attempt";

const npcReactionSchema = z.object({
  situation: z.string().trim().min(1).max(500),
  playerAction: z.string().trim().min(1).max(500),
  locale: z.enum(["ko", "en"]).default("ko")
});

interface AppDependencies {
  config: AppConfig;
  sessions?: InMemorySessionStore;
  loginAttempts?: OneTimeAttemptStore;
  hiveClient?: HiveWebLoginClient;
  aiService?: AiService;
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
  const app = express();

  app.disable("x-powered-by");
  app.use(helmet());
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
      path: "/api/v1/auth/hive"
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

    response.clearCookie(loginCookieName, { path: "/api/v1/auth/hive" });
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

  app.get("/api/v1/auth/hive/callback", async (request, response) => {
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

      response.clearCookie(loginCookieName, { path: "/api/v1/auth/hive" });
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

  app.use(express.static(publicDirectory, { extensions: ["html"], maxAge: config.nodeEnv === "production" ? "1h" : 0 }));

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

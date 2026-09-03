import type { NextFunction, Request, Response } from "express";
import type { GameSession, SessionStore } from "./session-store.js";

export class HttpError extends Error {
  public constructor(
    public readonly statusCode: number,
    message: string
  ) {
    super(message);
  }
}

export function readCookie(request: Request, name: string): string | undefined {
  const cookieHeader = request.headers.cookie;
  if (!cookieHeader) return undefined;

  for (const part of cookieHeader.split(";")) {
    const [key, ...valueParts] = part.trim().split("=");
    if (key === name) return decodeURIComponent(valueParts.join("="));
  }

  return undefined;
}

export function getBearerToken(request: Request): string | undefined {
  const authorization = request.header("authorization");
  if (!authorization?.startsWith("Bearer ")) return undefined;
  const token = authorization.slice("Bearer ".length).trim();
  return token || undefined;
}

export interface AuthenticatedLocals {
  session: GameSession;
}

export function requireSession(
  store: SessionStore,
  cookieName?: string,
  onAuthenticated?: (response: Response, session: GameSession) => void
) {
  return async (request: Request, response: Response, next: NextFunction): Promise<void> => {
    try {
      const token =
        getBearerToken(request) ?? (cookieName ? readCookie(request, cookieName) : undefined);
      const session = token ? await store.get(token) : undefined;

      if (!session) {
        response.status(401).json({
          error: { code: "AUTH_REQUIRED", message: "유효한 게임 세션이 필요합니다." }
        });
        return;
      }

      (response.locals as AuthenticatedLocals).session = session;
      onAuthenticated?.(response, session);
      next();
    } catch (error) {
      next(error);
    }
  };
}

import { randomBytes } from "node:crypto";
import type { Response } from "express";

interface AuthBridgeMessage {
  type: "HIVE_AUTH_SUCCESS" | "HIVE_AUTH_ERROR";
  sessionToken?: string;
  message?: string;
}

function safeJson(value: unknown): string {
  return JSON.stringify(value).replaceAll("<", "\\u003c");
}

export function sendAuthBridgePage(
  response: Response,
  gameOrigin: string,
  message: AuthBridgeMessage
): void {
  const nonce = randomBytes(16).toString("base64");
  const serializedMessage = safeJson(message);
  const serializedOrigin = safeJson(gameOrigin);

  response
    .status(message.type === "HIVE_AUTH_SUCCESS" ? 200 : 400)
    .set({
      "content-type": "text/html; charset=utf-8",
      "content-security-policy": `default-src 'none'; script-src 'nonce-${nonce}'; style-src 'unsafe-inline'`,
      "cache-control": "no-store",
      "referrer-policy": "no-referrer"
    })
    .send(`<!doctype html>
<html lang="ko">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Hive 로그인</title>
    <style>body{font-family:system-ui,sans-serif;padding:2rem;line-height:1.5}</style>
  </head>
  <body>
    <p>${message.type === "HIVE_AUTH_SUCCESS" ? "로그인이 완료되었습니다." : "로그인에 실패했습니다."}</p>
    <script nonce="${nonce}">
      const targetOrigin = ${serializedOrigin};
      const message = ${serializedMessage};
      const notifyOpener = () => {
        if (window.opener && !window.opener.closed) {
          window.opener.postMessage(message, targetOrigin);
        }
      };
      const retryTimer = window.setInterval(notifyOpener, 250);
      const closeTimer = window.setTimeout(() => {
        window.clearInterval(retryTimer);
        window.close();
      }, 3000);

      window.addEventListener("message", (event) => {
        if (event.origin !== targetOrigin || event.data?.type !== "HIVE_AUTH_ACK") return;
        window.clearInterval(retryTimer);
        window.clearTimeout(closeTimer);
        window.close();
      });

      notifyOpener();
    </script>
  </body>
</html>`);
}

import { randomBytes } from "node:crypto";
import type { Response } from "express";
import type { NicePayOrder } from "./integrations/nicepay/order-store.js";

function jsonForScript(value: unknown): string {
  return JSON.stringify(value).replaceAll("<", "\\u003c");
}

export function sendNicePayCheckoutPage(
  response: Response,
  input: {
    order: NicePayOrder;
    clientId: string;
    returnUrl: string;
  }
): void {
  const nonce = randomBytes(18).toString("base64");
  response
    .status(200)
    .set({
      "cache-control": "no-store",
      "content-type": "text/html; charset=utf-8",
      "content-security-policy": [
        "default-src 'none'",
        `script-src 'nonce-${nonce}' https://pay.nicepay.co.kr`,
        "connect-src https:",
        "frame-src 'self' https:",
        `style-src 'nonce-${nonce}'`,
        "img-src data: https:",
        "form-action https:",
        "base-uri 'none'"
      ].join("; ")
    })
    .send(`<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>NICEPAY 테스트 결제</title>
<style nonce="${nonce}">body{font-family:system-ui,sans-serif;background:#fff8eb;color:#50392d;display:grid;place-items:center;min-height:100vh;margin:0}main{text-align:center;padding:32px}small{display:block;margin-top:10px;color:#7d675b}</style>
<script src="https://pay.nicepay.co.kr/v1/js/"></script></head>
<body><main><h1>NICEPAY 테스트 결제</h1><p>결제창을 준비하고 있습니다…</p><small>테스트 환경에서는 실제 결제가 발생하지 않습니다.</small></main>
<script nonce="${nonce}">
const checkout=${jsonForScript({
      clientId: input.clientId,
      method: "card",
      orderId: input.order.orderId,
      amount: input.order.amount,
      goodsName: input.order.goodsName,
      returnUrl: input.returnUrl
    })};
checkout.fnError=()=>{
  const payload={type:"NICEPAY_PAYMENT_ERROR",message:"NICEPAY 테스트 결제창을 열지 못했습니다."};
  if(window.opener&&!window.opener.closed)window.opener.postMessage(payload,${jsonForScript(new URL(input.returnUrl).origin)});
  document.querySelector("p").textContent=payload.message;
};
window.addEventListener("load",()=>{
  if(!window.AUTHNICE){document.querySelector("p").textContent="NICEPAY 결제 모듈을 불러오지 못했습니다.";return;}
  window.AUTHNICE.requestPay(checkout);
});
</script></body></html>`);
}

export function sendNicePayResultPage(
  response: Response,
  origin: string,
  result: { success: boolean; message: string; orderId?: string }
): void {
  const nonce = randomBytes(18).toString("base64");
  const payload = result.success
    ? { type: "NICEPAY_PAYMENT_SUCCESS", orderId: result.orderId, message: result.message }
    : { type: "NICEPAY_PAYMENT_ERROR", message: result.message };
  response
    .status(200)
    .set({
      "cache-control": "no-store",
      "content-type": "text/html; charset=utf-8",
      "content-security-policy": `default-src 'none'; script-src 'nonce-${nonce}'; style-src 'nonce-${nonce}'; base-uri 'none'`
    })
    .send(`<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>NICEPAY 테스트 결제 결과</title>
<style nonce="${nonce}">body{font-family:system-ui,sans-serif;padding:30px;text-align:center}</style></head>
<body><p>${result.success ? "테스트 결제가 완료됐습니다. 게임으로 돌아갑니다." : "테스트 결제를 완료하지 못했습니다. 창을 닫고 다시 시도해 주세요."}</p>
<p><a href="${origin}">게임으로 돌아가기</a></p>
<script nonce="${nonce}">const payload=${jsonForScript(payload)};const origin=${jsonForScript(origin)};
function notify(){if(window.opener&&!window.opener.closed)window.opener.postMessage(payload,origin)}
notify();setInterval(notify,250);setTimeout(()=>{window.close();setTimeout(()=>window.location.replace(origin),200)},1500);</script></body></html>`);
}

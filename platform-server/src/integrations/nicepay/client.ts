import { createHash, timingSafeEqual } from "node:crypto";
import type { AppConfig } from "../../config.js";

export interface NicePayApproval {
  resultCode: string;
  status: string;
  tid: string;
  orderId: string;
  amount: number;
  ediDate: string;
  signature: string;
}

export interface NicePayGateway {
  approvePayment(input: { tid: string; amount: number }): Promise<NicePayApproval>;
}

function sha256(value: string): string {
  return createHash("sha256").update(value, "utf8").digest("hex");
}

function secureHexEqual(left: string, right: string): boolean {
  if (!/^[a-f\d]{64}$/i.test(left) || !/^[a-f\d]{64}$/i.test(right)) return false;
  return timingSafeEqual(Buffer.from(left.toLowerCase(), "hex"), Buffer.from(right.toLowerCase(), "hex"));
}

export function verifyNicePayAuthenticationSignature(input: {
  authToken: string;
  clientId: string;
  amount: number;
  secretKey: string;
  signature: string;
}): boolean {
  return secureHexEqual(
    input.signature,
    sha256(`${input.authToken}${input.clientId}${input.amount}${input.secretKey}`)
  );
}

export function verifyNicePayApprovalSignature(
  approval: NicePayApproval,
  secretKey: string
): boolean {
  return secureHexEqual(
    approval.signature,
    sha256(`${approval.tid}${approval.amount}${approval.ediDate}${secretKey}`)
  );
}

export class NicePayClient implements NicePayGateway {
  public constructor(private readonly config: AppConfig["nicepay"]) {}

  public async approvePayment(input: { tid: string; amount: number }): Promise<NicePayApproval> {
    if (!this.config.clientId || !this.config.secretKey) {
      throw new Error("NICEPAY 테스트 키가 설정되지 않았습니다.");
    }

    const authorization = Buffer.from(
      `${this.config.clientId}:${this.config.secretKey}`,
      "utf8"
    ).toString("base64");
    const endpoint = `${this.config.apiBaseUrl}/v1/payments/${encodeURIComponent(input.tid)}`;
    let response: globalThis.Response;
    try {
      response = await fetch(endpoint, {
        method: "POST",
        headers: {
          authorization: `Basic ${authorization}`,
          "content-type": "application/json"
        },
        body: JSON.stringify({ amount: input.amount }),
        signal: AbortSignal.timeout(10_000)
      });
    } catch (error) {
      return this.readApprovedPayment(endpoint, authorization, error);
    }

    if (!response.ok) {
      return this.readApprovedPayment(endpoint, authorization, new Error(`HTTP ${response.status}`));
    }
    return this.parseApproval(await response.json());
  }

  private async readApprovedPayment(
    endpoint: string,
    authorization: string,
    originalError: unknown
  ): Promise<NicePayApproval> {
    try {
      const response = await fetch(endpoint, {
        headers: { authorization: `Basic ${authorization}` },
        signal: AbortSignal.timeout(10_000)
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const approval = this.parseApproval(await response.json());
      if (approval.status !== "paid") throw new Error("결제 완료 상태가 아닙니다.");
      return approval;
    } catch {
      const message = originalError instanceof Error ? originalError.message : "승인 요청 실패";
      throw new Error(`NICEPAY 테스트 승인 결과를 확인하지 못했습니다: ${message}`);
    }
  }

  private parseApproval(value: unknown): NicePayApproval {
    const input = value as Record<string, unknown>;
    return {
      resultCode: String(input.resultCode ?? ""),
      status: String(input.status ?? ""),
      tid: String(input.tid ?? ""),
      orderId: String(input.orderId ?? ""),
      amount: Number(input.amount),
      ediDate: String(input.ediDate ?? ""),
      signature: String(input.signature ?? "")
    };
  }
}

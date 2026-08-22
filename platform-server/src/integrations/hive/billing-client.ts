import type { AppConfig } from "../../config.js";

export interface HivePaymentNotification {
  type: "paid" | "cancelled";
  marketId: string;
  orderId: string;
  marketPid: string;
  playerId: string;
  serverId: string;
  appId: string;
  quantity: number;
  purchaseBypassInfo: string;
}

export interface HiveReceiptVerification {
  transactionId: string;
  marketId: string;
  marketPid: string;
  marketTransactionId?: string;
  quantity: number;
  purchaseTest?: "Y" | "N";
}

export interface HiveUnconsumedPurchase {
  marketId: string;
  marketPid: string;
  orderId: string;
  serverId: string;
  playerId: string;
  quantity: number;
  purchaseBypassInfo: string;
}

export interface HiveBillingGateway {
  findUnconsumedPurchase(input: {
    playerId: string;
    serverId: string;
    orderId: string;
  }): Promise<HiveUnconsumedPurchase>;
  verifyReceipt(purchaseBypassInfo: string): Promise<HiveReceiptVerification>;
  confirmDelivery(input: {
    transactionId: string;
    playerId: string;
    itemId: string;
    itemName: string;
    quantity: number;
  }): Promise<void>;
}

interface HiveVerifyResponse {
  result?: number;
  result_msg?: string;
  hiveiap_transaction_id?: string;
  hiveiap_market_id?: string | number;
  hiveiap_market_pid?: string;
  hiveiap_market_transaction_id?: string;
  hiveiap_quantity?: string | number;
  hiveiap_purchase_test?: "Y" | "N";
}

interface HiveDeliveryResponse {
  result?: number;
  result_msg?: string;
}

interface HiveUnconsumedResponse {
  result?: number;
  result_msg?: string;
  unconsumed_lists?: Array<{
    market_id?: string | number;
    market_pid?: string;
    order_id?: string;
    server_id?: string;
    vid?: string | number;
    quantity?: string | number;
    purchase_bypass_info?: string;
  }>;
}

function requireValue(value: string | undefined, name: string): string {
  if (!value) throw new Error(`${name} 설정이 없습니다.`);
  return value;
}

function safePlayerId(value: string): number {
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed <= 0) {
    throw new Error("HIVE PlayerID를 안전한 정수로 변환할 수 없습니다.");
  }
  return parsed;
}

export class HiveBillingClient implements HiveBillingGateway {
  public constructor(
    private readonly config: AppConfig["hive"],
    private readonly request: typeof fetch = fetch
  ) {}

  public async findUnconsumedPurchase(input: {
    playerId: string;
    serverId: string;
    orderId: string;
  }): Promise<HiveUnconsumedPurchase> {
    const host =
      this.config.mode === "production"
        ? "https://hiveiap.qpyou.cn"
        : "https://sandbox-hiveiap.qpyou.cn";
    const result = await this.post<HiveUnconsumedResponse>(
      `${host}/api_v4/purchases/unconsumed`,
      {
        appid: requireValue(this.config.billingAppId, "HIVE_BILLING_APP_ID"),
        market_id: 15,
        server_id: input.serverId,
        user_id_type: "player_id",
        user_id: safePlayerId(input.playerId)
      },
      "HIVE 미소비 결제 조회"
    );

    if (result.result !== 0 || !Array.isArray(result.unconsumed_lists)) {
      throw new Error(`HIVE 미소비 결제 조회에 실패했습니다. result=${result.result ?? "unknown"}`);
    }

    const purchase = result.unconsumed_lists.find(
      (candidate) => String(candidate.order_id ?? "") === input.orderId
    );
    if (
      !purchase?.market_pid ||
      !purchase.server_id ||
      !purchase.purchase_bypass_info ||
      !purchase.vid
    ) {
      throw new Error("해당 PlayerID의 미소비 결제 목록에서 주문을 찾지 못했습니다.");
    }

    const quantity = Number(purchase.quantity ?? 1);
    if (!Number.isSafeInteger(quantity) || quantity <= 0) {
      throw new Error("HIVE 미소비 결제의 구매 수량이 올바르지 않습니다.");
    }

    return {
      marketId: String(purchase.market_id ?? ""),
      marketPid: purchase.market_pid,
      orderId: input.orderId,
      serverId: purchase.server_id,
      playerId: String(purchase.vid),
      quantity,
      purchaseBypassInfo: purchase.purchase_bypass_info
    };
  }

  public async verifyReceipt(purchaseBypassInfo: string): Promise<HiveReceiptVerification> {
    const host =
      this.config.mode === "production"
        ? "https://hiveiap-verify.qpyou.cn"
        : "https://sandbox-hiveiap-verify.qpyou.cn";
    const result = await this.post<HiveVerifyResponse>(
      `${host}/api_v4/verify`,
      { purchase_bypass_info: purchaseBypassInfo },
      "HIVE 영수증 검증"
    );

    if (
      result.result !== 0 ||
      !result.hiveiap_transaction_id ||
      !result.hiveiap_market_pid
    ) {
      throw new Error(`HIVE 영수증 검증에 실패했습니다. result=${result.result ?? "unknown"}`);
    }

    const quantity = Number(result.hiveiap_quantity ?? 1);
    if (!Number.isSafeInteger(quantity) || quantity <= 0) {
      throw new Error("HIVE 영수증의 구매 수량이 올바르지 않습니다.");
    }

    return {
      transactionId: result.hiveiap_transaction_id,
      marketId: String(result.hiveiap_market_id ?? ""),
      marketPid: result.hiveiap_market_pid,
      marketTransactionId: result.hiveiap_market_transaction_id,
      quantity,
      purchaseTest: result.hiveiap_purchase_test
    };
  }

  public async confirmDelivery(input: {
    transactionId: string;
    playerId: string;
    itemId: string;
    itemName: string;
    quantity: number;
  }): Promise<void> {
    const host =
      this.config.mode === "production"
        ? "https://hiveiap.qpyou.cn"
        : "https://sandbox-hiveiap.qpyou.cn";
    const result = await this.post<HiveDeliveryResponse>(
      `${host}/api_v4/item_result`,
      {
        hiveiap_transaction_id: input.transactionId,
        result_status: 1,
        user_id_type: "player_id",
        user_id: safePlayerId(input.playerId),
        asset: [
          {
            asset_id: input.itemId,
            asset_name: input.itemName,
            quantity: input.quantity
          }
        ]
      },
      "HIVE 아이템 지급 완료"
    );

    if (result.result !== 0) {
      throw new Error(`HIVE 지급 완료 처리에 실패했습니다. result=${result.result ?? "unknown"}`);
    }
  }

  private async post<T>(url: string, body: unknown, operation: string): Promise<T> {
    const response = await this.request(url, {
      method: "POST",
      headers: {
        authorization: `Bearer ${requireValue(this.config.billingAuthKey, "HIVE_BILLING_AUTH_KEY")}`,
        "content-type": "application/json"
      },
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(10_000)
    });

    if (!response.ok) {
      throw new Error(`${operation} 요청이 HTTP ${response.status}로 실패했습니다.`);
    }
    return (await response.json()) as T;
  }
}

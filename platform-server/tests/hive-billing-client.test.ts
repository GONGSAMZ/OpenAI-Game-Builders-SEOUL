import { describe, expect, it, vi } from "vitest";
import { HiveBillingClient } from "../src/integrations/hive/billing-client.js";
import { createTestConfig } from "./helpers.js";

describe("HiveBillingClient", () => {
  it("PlayerID와 주문번호로 HIVE 미소비 결제를 찾는다", async () => {
    const request = vi.fn(async () =>
      new Response(
        JSON.stringify({
          result: 0,
          unconsumed_lists: [
            {
              market_id: "15",
              market_pid: "product.pid",
              order_id: "ORDER-1",
              server_id: "global",
              vid: "20000011337",
              quantity: 2,
              purchase_bypass_info: "canonical-bypass"
            }
          ]
        }),
        { status: 200, headers: { "content-type": "application/json" } }
      )
    );
    const config = createTestConfig({
      hive: {
        mode: "sandbox",
        country: "KR",
        language: "ko",
        billingAppId: "com.gongsamz.webshop",
        billingAuthKey: "billing-secret"
      }
    });
    const client = new HiveBillingClient(config.hive, request as typeof fetch);

    await expect(
      client.findUnconsumedPurchase({
        playerId: "20000011337",
        serverId: "global",
        orderId: "ORDER-1"
      })
    ).resolves.toEqual(
      expect.objectContaining({ playerId: "20000011337", orderId: "ORDER-1", quantity: 2 })
    );
    expect(request).toHaveBeenCalledWith(
      "https://sandbox-hiveiap.qpyou.cn/api_v4/purchases/unconsumed",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({
          appid: "com.gongsamz.webshop",
          market_id: 15,
          server_id: "global",
          user_id_type: "player_id",
          user_id: 20000011337
        })
      })
    );
  });

  it("sandbox 영수증을 서버 키로 검증한다", async () => {
    const request = vi.fn(async () =>
      new Response(
        JSON.stringify({
          result: 0,
          hiveiap_transaction_id: "HS_1",
          hiveiap_market_id: 15,
          hiveiap_market_pid: "product.pid",
          hiveiap_market_transaction_id: "ORDER-1",
          hiveiap_quantity: 2,
          hiveiap_purchase_test: "Y"
        }),
        { status: 200, headers: { "content-type": "application/json" } }
      )
    );
    const config = createTestConfig({
      hive: {
        mode: "sandbox",
        country: "KR",
        language: "ko",
        billingAuthKey: "billing-secret"
      }
    });
    const client = new HiveBillingClient(config.hive, request as typeof fetch);

    await expect(client.verifyReceipt("opaque-value")).resolves.toEqual(
      expect.objectContaining({ transactionId: "HS_1", marketId: "15", quantity: 2 })
    );
    expect(request).toHaveBeenCalledWith(
      "https://sandbox-hiveiap-verify.qpyou.cn/api_v4/verify",
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({ authorization: "Bearer billing-secret" })
      })
    );
  });
});

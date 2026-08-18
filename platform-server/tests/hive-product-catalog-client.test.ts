import { describe, expect, it, vi } from "vitest";
import { HiveProductCatalogClient } from "../src/integrations/hive/product-catalog-client.js";
import { createTestConfig } from "./helpers.js";

describe("HiveProductCatalogClient", () => {
  it("인증키를 서버 헤더로 사용해 샌드박스 상품 목록을 조회한다", async () => {
    const request = vi.fn<typeof fetch>(async () =>
      new Response(
        JSON.stringify({
          result: 0,
          result_msg: "success",
          product_list: [
            {
              market_pid: "com.gongsamz.bungeoppang.redbean100",
              price: 1100,
              display_price: "₩1,100",
              title: "팥 코인 100개",
              description: "팥 코인",
              product_type: "consumable"
            }
          ]
        }),
        { status: 200, headers: { "content-type": "application/json" } }
      )
    );
    const config = createTestConfig({
      hive: {
        mode: "sandbox",
        billingAppId: "billing-app",
        billingAuthKey: "billing-secret",
        country: "KR",
        language: "ko"
      }
    });
    const client = new HiveProductCatalogClient(config.hive, request);

    await expect(client.getProducts("30000012345")).resolves.toEqual([
      {
        marketPid: "com.gongsamz.bungeoppang.redbean100",
        price: 1100,
        displayPrice: "₩1,100",
        title: "팥 코인 100개",
        description: "팥 코인",
        productType: "consumable"
      }
    ]);
    expect(request).toHaveBeenCalledOnce();
    const call = request.mock.calls[0];
    expect(call).toBeDefined();
    const [url, options] = call!;
    expect(url).toBe("https://sandbox-store.withhive.com/external/api/product");
    expect(options?.headers).toEqual(
      expect.objectContaining({
        authorization: "Bearer billing-secret",
        "content-type": "application/json; charset=utf-8"
      })
    );
    expect(JSON.parse(String(options?.body))).toEqual(
      expect.objectContaining({
        api: "product",
        market_id: 15,
        appid: "billing-app",
        vid: "30000012345",
        vid_type: "v4",
        market_pid_type: "consumable"
      })
    );
  });

  it("숫자가 아닌 PlayerID는 외부 요청 전에 거부한다", async () => {
    const request = vi.fn<typeof fetch>();
    const config = createTestConfig({
      hive: {
        mode: "sandbox",
        billingAppId: "billing-app",
        billingAuthKey: "billing-secret"
      }
    });
    const client = new HiveProductCatalogClient(config.hive, request);

    await expect(client.getProducts("not-a-player")).rejects.toThrow("PlayerID");
    expect(request).not.toHaveBeenCalled();
  });

  it("JavaScript 안전 정수보다 큰 PlayerID도 문자열 그대로 전달한다", async () => {
    const request = vi.fn<typeof fetch>(async (_url, options) => {
      expect(JSON.parse(String(options?.body)).vid).toBe("9223372036854775807");
      return new Response(
        JSON.stringify({ result: 0, result_msg: "success", product_list: [] }),
        { status: 200, headers: { "content-type": "application/json" } }
      );
    });
    const config = createTestConfig({
      hive: {
        mode: "sandbox",
        billingAppId: "billing-app",
        billingAuthKey: "billing-secret"
      }
    });
    const client = new HiveProductCatalogClient(config.hive, request);

    await client.getProducts("9223372036854775807");
    expect(request).toHaveBeenCalledOnce();
  });
});

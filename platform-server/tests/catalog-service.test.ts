import { describe, expect, it } from "vitest";
import type { HiveProductCatalogGateway } from "../src/integrations/hive/product-catalog-client.js";
import { mapHiveMarketProduct } from "../src/store/catalog.js";
import { StoreCatalogService } from "../src/store/catalog-service.js";
import { createTestConfig } from "./helpers.js";

describe("HIVE store catalog", () => {
  it("기존 PID와 규칙형 PID를 안전한 지급 상품으로 변환한다", () => {
    const legacy = mapHiveMarketProduct(
      {
        marketPid: "com.gongsamz.bungeoppang.redbean100",
        price: 1200,
        displayPrice: "₩1,200",
        title: "팥 코인 100개 새 이름",
        description: "HIVE에서 갱신됨",
        productType: "consumable"
      },
      "https://game.example/store-products"
    );
    expect(legacy).toEqual(
      expect.objectContaining({
        id: "red-bean-100",
        priceKrw: 1200,
        grant: { itemId: "red-bean-coin", quantity: 100 },
        imageUrl:
          "https://game.example/store-products/com.gongsamz.bungeoppang.redbean100.png"
      })
    );

    const automatic = mapHiveMarketProduct(
      {
        marketPid: "com.gongsamz.bungeoppang.coin.red-bean-coin.1000",
        price: 9900,
        displayPrice: "₩9,900",
        title: "팥 코인 1,000개",
        description: "대용량 팥 코인",
        productType: "consumable"
      },
      "https://game.example/store-products/"
    );
    expect(automatic).toEqual(
      expect.objectContaining({
        id: "com.gongsamz.bungeoppang.coin.red-bean-coin.1000",
        grant: { itemId: "red-bean-coin", quantity: 1000 }
      })
    );
  });

  it("지원하지 않는 PID나 잘못된 지급 수량은 상점에서 제외한다", () => {
    const base = {
      price: 1000,
      displayPrice: "₩1,000",
      title: "지원하지 않는 상품",
      description: "",
      productType: "consumable"
    };
    expect(
      mapHiveMarketProduct(
        { ...base, marketPid: "com.gongsamz.bungeoppang.unknown" },
        "https://game.example/store-products"
      )
    ).toBeUndefined();
    expect(
      mapHiveMarketProduct(
        { ...base, marketPid: "com.gongsamz.bungeoppang.equipment.golden-pan.2" },
        "https://game.example/store-products"
      )
    ).toBeUndefined();
  });

  it("로그인 후 HIVE 목록을 캐시하고 장애 시 마지막 정상 목록을 유지한다", async () => {
    let now = Date.parse("2026-08-19T00:00:00.000Z");
    let calls = 0;
    const hiveCatalog: HiveProductCatalogGateway = {
      async getProducts() {
        calls += 1;
        if (calls > 1) throw new Error("temporary outage");
        return [
          {
            marketPid: "com.gongsamz.bungeoppang.redbean100",
            price: 1100,
            displayPrice: "₩1,100",
            title: "팥 코인 100개",
            description: "팥 코인",
            productType: "consumable"
          },
          {
            marketPid: "com.gongsamz.bungeoppang.coin.red-bean-coin.1000",
            price: 9900,
            displayPrice: "₩9,900",
            title: "팥 코인 1,000개",
            description: "대용량 팥 코인",
            productType: "consumable"
          },
          {
            marketPid: "com.gongsamz.bungeoppang.unsupported",
            price: 100,
            displayPrice: "₩100",
            title: "미지원",
            description: "",
            productType: "consumable"
          }
        ];
      }
    };
    const service = new StoreCatalogService(
      createTestConfig({
        hive: {
          mode: "sandbox",
          billingAppId: "billing-app",
          billingAuthKey: "billing-key"
        },
        store: { catalogSource: "hive", catalogCacheSeconds: 60 }
      }),
      hiveCatalog,
      () => now
    );

    const loggedOut = await service.getCatalog();
    expect(loggedOut.source).toBe("static-fallback");
    expect(calls).toBe(0);

    const synced = await service.getCatalog("30000012345");
    expect(synced.source).toBe("hive");
    expect(synced.products).toHaveLength(2);
    expect(synced.ignoredProductCount).toBe(1);
    expect(calls).toBe(1);

    const cached = await service.getCatalog("30000012345");
    expect(cached.source).toBe("hive-cache");
    expect(calls).toBe(1);

    now += 61_000;
    const stale = await service.getCatalog("30000012345");
    expect(stale.source).toBe("hive-stale-cache");
    expect(stale.products).toEqual(synced.products);
    expect(calls).toBe(2);
  });

  it("사용자마다 HIVE 카탈로그 캐시를 분리한다", async () => {
    const calls: string[] = [];
    const hiveCatalog: HiveProductCatalogGateway = {
      async getProducts(playerId) {
        calls.push(playerId);
        return [
          {
            marketPid: "com.gongsamz.bungeoppang.redbean100",
            price: playerId === "100" ? 1100 : 1200,
            displayPrice: playerId === "100" ? "₩1,100" : "₩1,200",
            title: "팥 코인 100개",
            description: "팥 코인",
            productType: "consumable"
          }
        ];
      }
    };
    const service = new StoreCatalogService(
      createTestConfig({
        hive: {
          mode: "sandbox",
          billingAppId: "billing-app",
          billingAuthKey: "billing-key"
        },
        store: { catalogSource: "hive", catalogCacheSeconds: 60 }
      }),
      hiveCatalog
    );

    expect((await service.getCatalog("100")).products[0]?.priceKrw).toBe(1100);
    expect((await service.getCatalog("200")).products[0]?.priceKrw).toBe(1200);
    expect((await service.getCatalog("100")).source).toBe("hive-cache");
    expect(calls).toEqual(["100", "200"]);
  });
});

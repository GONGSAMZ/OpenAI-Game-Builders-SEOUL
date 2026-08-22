import type { AppConfig } from "../../config.js";
import type { HiveMarketProduct } from "../../store/catalog.js";

export interface HiveProductCatalogGateway {
  getProducts(playerId: string): Promise<HiveMarketProduct[]>;
}

interface HiveProductCatalogResponse {
  result?: number;
  result_msg?: string;
  product_list?: Array<{
    market_pid?: string;
    price?: string | number;
    display_price?: string;
    title?: string;
    description?: string;
    product_type?: string;
  }>;
  update_date?: string;
}

function requireValue(value: string | undefined, name: string): string {
  if (!value) throw new Error(`${name} 설정이 없습니다.`);
  return value;
}

function safePlayerId(value: string): string {
  if (!/^[1-9]\d{0,31}$/.test(value)) {
    throw new Error("HIVE 상품 조회용 PlayerID 형식이 올바르지 않습니다.");
  }
  return value;
}

export class HiveProductCatalogClient implements HiveProductCatalogGateway {
  public constructor(
    private readonly config: AppConfig["hive"],
    private readonly request: typeof fetch = fetch
  ) {}

  public async getProducts(playerId: string): Promise<HiveMarketProduct[]> {
    const host =
      this.config.mode === "production"
        ? "https://store.withhive.com"
        : "https://sandbox-store.withhive.com";
    const response = await this.request(`${host}/external/api/product`, {
      method: "POST",
      headers: {
        authorization: `Bearer ${requireValue(
          this.config.billingAuthKey,
          "HIVE_BILLING_AUTH_KEY"
        )}`,
        "content-type": "application/json; charset=utf-8"
      },
      body: JSON.stringify({
        api: "product",
        market_id: 15,
        appid: requireValue(this.config.billingAppId, "HIVE_BILLING_APP_ID"),
        hive_country: this.config.country,
        game_language: this.config.language,
        vid: safePlayerId(playerId),
        vid_type: "v4",
        market_pid_type: "consumable"
      }),
      signal: AbortSignal.timeout(10_000)
    });

    if (!response.ok) {
      throw new Error(`HIVE 상품 목록 요청이 HTTP ${response.status}로 실패했습니다.`);
    }

    const result = (await response.json()) as HiveProductCatalogResponse;
    if (result.result !== 0 || !Array.isArray(result.product_list)) {
      throw new Error(
        `HIVE 상품 목록 조회에 실패했습니다. result=${result.result ?? "unknown"}`
      );
    }

    return result.product_list.flatMap((entry) => {
      const price = Number(entry.price);
      if (
        !entry.market_pid ||
        !Number.isSafeInteger(price) ||
        price <= 0 ||
        !entry.product_type
      ) {
        return [];
      }

      return [
        {
          marketPid: entry.market_pid,
          price,
          displayPrice: entry.display_price ?? "",
          title: entry.title ?? "",
          description: entry.description ?? "",
          productType: entry.product_type
        }
      ];
    });
  }
}

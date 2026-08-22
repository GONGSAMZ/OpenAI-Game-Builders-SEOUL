import type { AppConfig } from "../config.js";
import {
  HiveProductCatalogClient,
  type HiveProductCatalogGateway
} from "../integrations/hive/product-catalog-client.js";
import {
  findProduct,
  findProductByMarketPid,
  mapHiveMarketProduct,
  storeCatalog,
  type StoreProduct
} from "./catalog.js";

export type StoreCatalogSource =
  | "static"
  | "hive"
  | "hive-cache"
  | "hive-stale-cache"
  | "static-fallback";

export interface StoreCatalogSnapshot {
  products: readonly StoreProduct[];
  source: StoreCatalogSource;
  updatedAt: string | null;
  ignoredProductCount: number;
}

export interface StoreCatalogGateway {
  getCatalog(playerId?: string): Promise<StoreCatalogSnapshot>;
  findById(productId: string, playerId?: string): Promise<StoreProduct | undefined>;
  findByMarketPid(marketPid: string, playerId?: string): Promise<StoreProduct | undefined>;
}

interface CatalogCache {
  products: readonly StoreProduct[];
  updatedAt: string;
  ignoredProductCount: number;
  expiresAt: number;
}

function isUsablePlayerId(playerId: string | undefined): playerId is string {
  return Boolean(playerId && /^[1-9]\d{0,31}$/.test(playerId));
}

export class StoreCatalogService implements StoreCatalogGateway {
  private readonly caches = new Map<string, CatalogCache>();

  public constructor(
    private readonly config: AppConfig,
    private readonly hiveCatalog: HiveProductCatalogGateway = new HiveProductCatalogClient(
      config.hive
    ),
    private readonly now: () => number = Date.now
  ) {}

  public async getCatalog(playerId?: string): Promise<StoreCatalogSnapshot> {
    if (this.config.store.catalogSource === "static") return this.staticSnapshot("static");

    if (!isUsablePlayerId(playerId)) {
      return this.staticSnapshot("static-fallback");
    }

    const cache = this.caches.get(playerId);
    if (cache && cache.expiresAt > this.now()) {
      return this.cacheSnapshot(cache, "hive-cache");
    }

    try {
      const hiveProducts = await this.hiveCatalog.getProducts(playerId);
      const products = hiveProducts.flatMap((product) => {
        const mapped = mapHiveMarketProduct(product, this.config.store.productImageBaseUrl);
        return mapped ? [mapped] : [];
      });
      const uniqueProducts = [
        ...new Map(products.map((product) => [product.marketPid, product])).values()
      ];

      if (uniqueProducts.length === 0) {
        throw new Error("지급 규칙에 맞는 HIVE 상품이 없습니다.");
      }

      const updatedAt = new Date(this.now()).toISOString();
      const refreshed: CatalogCache = {
        products: uniqueProducts,
        updatedAt,
        ignoredProductCount: hiveProducts.length - uniqueProducts.length,
        expiresAt: this.now() + this.config.store.catalogCacheSeconds * 1000
      };
      this.caches.set(playerId, refreshed);
      return this.cacheSnapshot(refreshed, "hive");
    } catch (error) {
      if (this.config.nodeEnv !== "test") {
        const message = error instanceof Error ? error.message : "unknown";
        console.warn(
          JSON.stringify({
            type: "hive_catalog_sync_error",
            message,
            revision: this.config.revision
          })
        );
      }
      return cache
        ? this.cacheSnapshot(cache, "hive-stale-cache")
        : this.staticSnapshot("static-fallback");
    }
  }

  public async findById(
    productId: string,
    playerId?: string
  ): Promise<StoreProduct | undefined> {
    const snapshot = await this.getCatalog(playerId);
    return snapshot.products.find((product) => product.id === productId) ?? findProduct(productId);
  }

  public async findByMarketPid(
    marketPid: string,
    playerId?: string
  ): Promise<StoreProduct | undefined> {
    const snapshot = await this.getCatalog(playerId);
    return (
      snapshot.products.find((product) => product.marketPid === marketPid) ??
      findProductByMarketPid(marketPid)
    );
  }

  private staticSnapshot(source: "static" | "static-fallback"): StoreCatalogSnapshot {
    return {
      products: storeCatalog,
      source,
      updatedAt: null,
      ignoredProductCount: 0
    };
  }

  private cacheSnapshot(
    cache: CatalogCache,
    source: "hive" | "hive-cache" | "hive-stale-cache"
  ): StoreCatalogSnapshot {
    return {
      products: cache.products,
      source,
      updatedAt: cache.updatedAt,
      ignoredProductCount: cache.ignoredProductCount
    };
  }
}

export function createStoreCatalogService(config: AppConfig): StoreCatalogGateway {
  return new StoreCatalogService(config);
}

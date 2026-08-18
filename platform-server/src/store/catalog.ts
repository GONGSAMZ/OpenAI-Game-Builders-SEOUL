export interface StoreProduct {
  id: string;
  name: string;
  description: string;
  priceLabel: string;
  priceKrw: number;
  testPointPrice: number;
  marketPid: string;
  imageUrl?: string;
  grant: {
    itemId: string;
    quantity: number;
  };
}

export interface HiveMarketProduct {
  marketPid: string;
  price: number;
  displayPrice: string;
  title: string;
  description: string;
  productType: string;
}

export const storeCatalog: readonly StoreProduct[] = [
  {
    id: "red-bean-100",
    name: "팥 코인 100개",
    description: "가게 업그레이드에 쓰는 데모 재화입니다.",
    priceLabel: "₩1,100",
    priceKrw: 1100,
    testPointPrice: 1100,
    marketPid: "com.gongsamz.bungeoppang.redbean100",
    grant: { itemId: "red-bean-coin", quantity: 100 }
  },
  {
    id: "red-bean-550",
    name: "팥 코인 550개",
    description: "보너스 50개가 포함된 데모 재화 묶음입니다.",
    priceLabel: "₩5,500",
    priceKrw: 5500,
    testPointPrice: 5500,
    marketPid: "com.gongsamz.bungeoppang.redbean550",
    grant: { itemId: "red-bean-coin", quantity: 550 }
  },
  {
    id: "golden-pan",
    name: "황금 붕어빵 틀",
    description: "가게를 빛내는 영구 소장형 데모 아이템입니다.",
    priceLabel: "₩3,300",
    priceKrw: 3300,
    testPointPrice: 3300,
    marketPid: "com.gongsamz.bungeoppang.goldenpan",
    grant: { itemId: "golden-pan", quantity: 1 }
  }
];

export function findProduct(productId: string): StoreProduct | undefined {
  return storeCatalog.find((product) => product.id === productId);
}

export function findProductByMarketPid(marketPid: string): StoreProduct | undefined {
  return storeCatalog.find((product) => product.marketPid === marketPid);
}

function buildProductImageUrl(imageBaseUrl: string, marketPid: string): string {
  return `${imageBaseUrl.replace(/\/$/, "")}/${encodeURIComponent(marketPid)}.png`;
}

function parseGrantFromMarketPid(
  marketPid: string
): StoreProduct["grant"] | undefined {
  const match = marketPid.match(
    /(?:^|\.)(coin|equipment|item)\.([a-z0-9][a-z0-9-]{0,63})\.(\d{1,9})$/
  );
  if (!match) return undefined;

  const kind = match[1];
  const itemId = match[2];
  const quantityText = match[3];
  if (!kind || !itemId || !quantityText) return undefined;
  const quantity = Number(quantityText);
  if (!Number.isSafeInteger(quantity) || quantity <= 0) return undefined;
  if (kind === "coin" && !itemId.endsWith("-coin")) return undefined;
  if (kind === "equipment" && quantity !== 1) return undefined;

  return { itemId, quantity };
}

export function mapHiveMarketProduct(
  product: HiveMarketProduct,
  imageBaseUrl: string
): StoreProduct | undefined {
  if (
    !product.marketPid ||
    !Number.isSafeInteger(product.price) ||
    product.price <= 0 ||
    product.productType !== "consumable"
  ) {
    return undefined;
  }

  const legacy = findProductByMarketPid(product.marketPid);
  const grant = legacy?.grant ?? parseGrantFromMarketPid(product.marketPid);
  if (!grant) return undefined;

  return {
    id: legacy?.id ?? product.marketPid,
    name: product.title.trim() || legacy?.name || product.marketPid,
    description:
      product.description.trim() || legacy?.description || "HIVE 콘솔 등록 상품입니다.",
    priceLabel: product.displayPrice.trim() || `₩${product.price.toLocaleString("ko-KR")}`,
    priceKrw: product.price,
    testPointPrice: product.price,
    marketPid: product.marketPid,
    imageUrl: buildProductImageUrl(imageBaseUrl, product.marketPid),
    grant: { ...grant }
  };
}

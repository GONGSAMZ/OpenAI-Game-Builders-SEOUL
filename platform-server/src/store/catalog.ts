export interface StoreProduct {
  id: string;
  name: string;
  description: string;
  priceLabel: string;
  testPointPrice: number;
  marketPid: string;
  grant: {
    itemId: string;
    quantity: number;
  };
}

export const storeCatalog: readonly StoreProduct[] = [
  {
    id: "red-bean-100",
    name: "팥 코인 100개",
    description: "가게 업그레이드에 쓰는 데모 재화입니다.",
    priceLabel: "₩1,100",
    testPointPrice: 1100,
    marketPid: "com.gongsamz.bungeoppang.redbean100",
    grant: { itemId: "red-bean-coin", quantity: 100 }
  },
  {
    id: "red-bean-550",
    name: "팥 코인 550개",
    description: "보너스 50개가 포함된 데모 재화 묶음입니다.",
    priceLabel: "₩5,500",
    testPointPrice: 5500,
    marketPid: "com.gongsamz.bungeoppang.redbean550",
    grant: { itemId: "red-bean-coin", quantity: 550 }
  },
  {
    id: "golden-pan",
    name: "황금 붕어빵 틀",
    description: "가게를 빛내는 영구 소장형 데모 아이템입니다.",
    priceLabel: "₩3,300",
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

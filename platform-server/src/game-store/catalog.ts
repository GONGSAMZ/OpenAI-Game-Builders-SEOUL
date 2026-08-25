export type GameStoreCategory = "filling" | "item";
export type GameStoreOwnership = "run-permanent" | "daily-selection" | "next-day-consumable";
export type GameStoreAvailability = "available" | "coming-soon";

export interface GameStoreEffect {
  code: "select-filling" | "paired-mold" | "paired-batter-pour" | "cook-time-multiplier";
  fillingId?: string;
  multiplier?: number;
  durationSeconds?: number;
}

export interface GameStoreProduct {
  productId: string;
  category: GameStoreCategory;
  displayName: string;
  description: string;
  price: number;
  currency: "game-money";
  ownership: GameStoreOwnership;
  availability: GameStoreAvailability;
  effect: GameStoreEffect;
}

export const gameStoreCatalogVersion = "2026-08-25.2";

export const gameStoreProducts: readonly GameStoreProduct[] = [
  {
    productId: "filling-red-bean",
    category: "filling",
    displayName: "팥",
    description: "포근하고 진한 기본 단맛",
    price: 1200,
    currency: "game-money",
    ownership: "daily-selection",
    availability: "available",
    effect: { code: "select-filling", fillingId: "red-bean" }
  },
  {
    productId: "filling-custard",
    category: "filling",
    displayName: "슈크림",
    description: "부드럽고 달콤한 크림",
    price: 1400,
    currency: "game-money",
    ownership: "daily-selection",
    availability: "available",
    effect: { code: "select-filling", fillingId: "custard" }
  },
  {
    productId: "filling-nutella",
    category: "filling",
    displayName: "초코",
    description: "진한 초콜릿의 달콤함",
    price: 1600,
    currency: "game-money",
    ownership: "daily-selection",
    availability: "available",
    effect: { code: "select-filling", fillingId: "nutella" }
  },
  {
    productId: "filling-cream-cheese",
    category: "filling",
    displayName: "크림치즈",
    description: "추후 해금되는 붕어빵 속",
    price: 0,
    currency: "game-money",
    ownership: "daily-selection",
    availability: "coming-soon",
    effect: { code: "select-filling", fillingId: "cream-cheese" }
  },
  {
    productId: "filling-pizza",
    category: "filling",
    displayName: "피자",
    description: "추후 해금되는 붕어빵 속",
    price: 0,
    currency: "game-money",
    ownership: "daily-selection",
    availability: "coming-soon",
    effect: { code: "select-filling", fillingId: "pizza" }
  },
  {
    productId: "filling-mint",
    category: "filling",
    displayName: "민트",
    description: "추후 해금되는 붕어빵 속",
    price: 0,
    currency: "game-money",
    ownership: "daily-selection",
    availability: "coming-soon",
    effect: { code: "select-filling", fillingId: "mint" }
  },
  {
    productId: "filling-sweet-potato",
    category: "filling",
    displayName: "고구마",
    description: "추후 해금되는 붕어빵 속",
    price: 0,
    currency: "game-money",
    ownership: "daily-selection",
    availability: "coming-soon",
    effect: { code: "select-filling", fillingId: "sweet-potato" }
  },
  {
    productId: "filling-green-tea",
    category: "filling",
    displayName: "녹차",
    description: "향긋하고 쌉싸름한 맛",
    price: 1800,
    currency: "game-money",
    ownership: "daily-selection",
    availability: "available",
    effect: { code: "select-filling", fillingId: "green-tea" }
  },
  {
    productId: "item-double-golden-mold",
    category: "item",
    displayName: "황금 2구 틀",
    description: "두 마리를 한 번에 구울 수 있는 틀",
    price: 4800,
    currency: "game-money",
    ownership: "run-permanent",
    availability: "available",
    effect: { code: "paired-mold" }
  },
  {
    productId: "item-dual-pour",
    category: "item",
    displayName: "동시 붓기",
    description: "두 칸에 반죽을 한 번에 붓기",
    price: 3200,
    currency: "game-money",
    ownership: "run-permanent",
    availability: "available",
    effect: { code: "paired-batter-pour" }
  },
  {
    productId: "item-cooking-fever",
    category: "item",
    displayName: "조리 피버",
    description: "다음 영업일 첫 30초 동안 굽기 속도 20% 증가",
    price: 2800,
    currency: "game-money",
    ownership: "next-day-consumable",
    availability: "available",
    effect: { code: "cook-time-multiplier", multiplier: 0.8, durationSeconds: 30 }
  }
];

export function findGameStoreProduct(productId: string): GameStoreProduct | undefined {
  return gameStoreProducts.find((product) => product.productId === productId);
}

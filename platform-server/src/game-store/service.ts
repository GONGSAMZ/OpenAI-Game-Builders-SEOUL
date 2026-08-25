import { createHash } from "node:crypto";
import {
  createDefaultPlayerSaveProfile,
  normalizePlayerSaveProfile
} from "../save/player-profile-service.js";
import {
  SaveRevisionConflictError,
  type PlayerSaveProfile,
  type PlayerSaveStore
} from "../save/save-store.js";
import {
  findGameStoreProduct,
  gameStoreCatalogVersion,
  gameStoreProducts,
  type GameStoreProduct
} from "./catalog.js";

type JsonRecord = Record<string, unknown>;

export type GameStoreProductStatus =
  | "owned"
  | "purchasable"
  | "locked"
  | "insufficient-funds";

export interface GameStoreProductState {
  productId: string;
  status: GameStoreProductStatus;
}

export interface GameStoreMe {
  revision: number;
  money: number;
  unlockedFillingIds: string[];
  ownedGameplayItemIds: string[];
  queuedDayEffects: JsonRecord[];
  products: GameStoreProductState[];
}

export class GameEconomyError extends Error {
  public constructor(
    public readonly statusCode: number,
    public readonly code: string,
    message: string,
    public readonly details?: Record<string, unknown>
  ) {
    super(message);
    this.name = "GameEconomyError";
  }
}

export interface PurchaseGameStoreProductInput {
  productId: string;
  expectedRevision: number;
  idempotencyKey: string;
}

export interface SettleDayInput {
  day: number;
  revenue: number;
  ingredientCost: number;
  sold: number;
  customers: number;
  expectedRevision: number;
  idempotencyKey: string;
}

export interface ResetRunInput {
  expectedRevision: number;
  idempotencyKey: string;
}

function record(value: unknown): JsonRecord {
  return value && typeof value === "object" && !Array.isArray(value)
    ? value as JsonRecord
    : {};
}

function stringArray(value: unknown): string[] {
  return Array.isArray(value)
    ? [...new Set(value.filter((entry): entry is string => typeof entry === "string"))]
    : [];
}

function effectArray(value: unknown): JsonRecord[] {
  return Array.isArray(value)
    ? value.filter((entry): entry is JsonRecord => Boolean(entry) && typeof entry === "object" && !Array.isArray(entry))
    : [];
}

function fingerprint(value: unknown): string {
  return createHash("sha256").update(JSON.stringify(value)).digest("hex");
}

function isOwned(profile: PlayerSaveProfile, product: GameStoreProduct): boolean {
  const run = record(profile.run);
  if (product.effect.code === "unlock-filling") {
    return stringArray(run.unlockedFillingIds).includes(product.effect.fillingId ?? "");
  }
  if (product.ownership === "run-permanent") {
    return stringArray(run.ownedGameplayItemIds).includes(product.productId);
  }
  const nextDay = Math.max(1, Number(run.nextDay) || 1);
  return effectArray(run.queuedDayEffects).some((effect) =>
    effect.productId === product.productId && Number(effect.targetDay) === nextDay
  );
}

function productStatus(profile: PlayerSaveProfile, product: GameStoreProduct): GameStoreProductStatus {
  if (product.availability !== "available") return "locked";
  if (isOwned(profile, product)) return "owned";
  return Number(record(profile.run).money) < product.price
    ? "insufficient-funds"
    : "purchasable";
}

function toMe(profile: PlayerSaveProfile): GameStoreMe {
  const normalized = normalizePlayerSaveProfile(profile);
  const run = record(normalized.run);
  return {
    revision: normalized.revision,
    money: Number(run.money),
    unlockedFillingIds: stringArray(run.unlockedFillingIds),
    ownedGameplayItemIds: stringArray(run.ownedGameplayItemIds),
    queuedDayEffects: structuredClone(effectArray(run.queuedDayEffects)),
    products: gameStoreProducts.map((product) => ({
      productId: product.productId,
      status: productStatus(normalized, product)
    }))
  };
}

export class GameStoreService {
  public constructor(private readonly saves: PlayerSaveStore) {}

  public getCatalog() {
    return {
      catalogVersion: gameStoreCatalogVersion,
      currency: "game-money" as const,
      products: gameStoreProducts
    };
  }

  public async getMe(subject: string): Promise<GameStoreMe> {
    return toMe(await this.getOrCreate(subject));
  }

  public async purchase(subject: string, input: PurchaseGameStoreProductInput) {
    const product = findGameStoreProduct(input.productId);
    if (!product) {
      throw new GameEconomyError(404, "PRODUCT_NOT_FOUND", "일반 상점 상품을 찾을 수 없습니다.");
    }
    if (product.availability !== "available") {
      throw new GameEconomyError(409, "PRODUCT_LOCKED", "아직 구매할 수 없는 상품입니다.");
    }

    const result = await this.saves.mutate(subject, input.expectedRevision, {
      operation: "game-store-purchase",
      idempotencyKey: input.idempotencyKey,
      fingerprint: fingerprint({ productId: input.productId }),
      mutate: (current) => {
        const profile = normalizePlayerSaveProfile(current ?? createDefaultPlayerSaveProfile());
        const run = record(profile.run);
        if (isOwned(profile, product)) {
          throw new GameEconomyError(
            409,
            product.ownership === "next-day-consumable" ? "EFFECT_ALREADY_QUEUED" : "ALREADY_OWNED",
            product.ownership === "next-day-consumable"
              ? "다음 영업일에 이미 적용할 효과입니다."
              : "이미 보유한 상품입니다."
          );
        }
        const money = Number(run.money);
        if (money < product.price) {
          throw new GameEconomyError(409, "INSUFFICIENT_FUNDS", "보유금이 부족합니다.", {
            balance: money,
            required: product.price
          });
        }

        run.money = money - product.price;
        if (product.effect.code === "unlock-filling") {
          run.unlockedFillingIds = [
            ...new Set([...stringArray(run.unlockedFillingIds), product.effect.fillingId!])
          ];
        } else if (product.ownership === "run-permanent") {
          run.ownedGameplayItemIds = [
            ...new Set([...stringArray(run.ownedGameplayItemIds), product.productId])
          ];
        } else {
          run.queuedDayEffects = [
            ...effectArray(run.queuedDayEffects),
            {
              productId: product.productId,
              effectCode: product.effect.code,
              targetDay: Math.max(1, Number(run.nextDay) || 1),
              durationSeconds: product.effect.durationSeconds,
              multiplier: product.effect.multiplier
            }
          ];
        }
        profile.run = run;
        return normalizePlayerSaveProfile(profile);
      }
    });

    return { duplicate: result.duplicate, profile: result.profile, store: toMe(result.profile) };
  }

  public async settleDay(subject: string, input: SettleDayInput) {
    const result = await this.saves.mutate(subject, input.expectedRevision, {
      operation: "game-run-settle",
      idempotencyKey: input.idempotencyKey,
      fingerprint: fingerprint({
        day: input.day,
        revenue: input.revenue,
        ingredientCost: input.ingredientCost,
        sold: input.sold,
        customers: input.customers
      }),
      mutate: (current) => {
        const profile = normalizePlayerSaveProfile(current ?? createDefaultPlayerSaveProfile());
        const run = record(profile.run);
        const nextDay = Math.max(1, Number(run.nextDay) || 1);
        if (input.day < nextDay) {
          throw new GameEconomyError(409, "DAY_ALREADY_SETTLED", "이미 정산된 영업일입니다.");
        }
        if (input.day !== nextDay) {
          throw new GameEconomyError(409, "DAY_MISMATCH", "정산할 영업일이 서버 진행과 일치하지 않습니다.", {
            expectedDay: nextDay,
            receivedDay: input.day
          });
        }

        run.money = Number(run.money) + input.revenue - input.ingredientCost;
        run.nextDay = input.day + 1;
        run.queuedDayEffects = effectArray(run.queuedDayEffects).filter(
          (effect) => Number(effect.targetDay) > input.day
        );
        profile.run = run;

        const account = record(profile.account);
        const stats = record(account.lifetimeStats);
        stats.totalSales = Math.max(0, Number(stats.totalSales) || 0) + input.sold;
        stats.totalCustomers = Math.max(0, Number(stats.totalCustomers) || 0) + input.customers;
        stats.totalRevenue = Math.max(0, Number(stats.totalRevenue) || 0) + input.revenue;
        stats.bestDailyProfit = Math.max(
          Number(stats.bestDailyProfit) || 0,
          input.revenue - input.ingredientCost
        );
        account.lifetimeStats = stats;
        profile.account = account;
        return normalizePlayerSaveProfile(profile);
      }
    });
    return { duplicate: result.duplicate, profile: result.profile };
  }

  public async resetRun(subject: string, input: ResetRunInput) {
    const result = await this.saves.mutate(subject, input.expectedRevision, {
      operation: "save-reset-run",
      idempotencyKey: input.idempotencyKey,
      fingerprint: fingerprint({ reset: "run" }),
      mutate: (current) => {
        const profile = normalizePlayerSaveProfile(current ?? createDefaultPlayerSaveProfile());
        profile.run = structuredClone(createDefaultPlayerSaveProfile().run);
        return normalizePlayerSaveProfile(profile);
      }
    });
    return { duplicate: result.duplicate, profile: result.profile, store: toMe(result.profile) };
  }

  private async getOrCreate(subject: string): Promise<PlayerSaveProfile> {
    for (let attempt = 0; attempt < 4; attempt++) {
      const current = await this.saves.get(subject);
      if (current) return normalizePlayerSaveProfile(current);
      try {
        return await this.saves.put(subject, 0, createDefaultPlayerSaveProfile());
      } catch (error) {
        if (!(error instanceof SaveRevisionConflictError)) throw error;
      }
    }
    throw new SaveRevisionConflictError(await this.saves.get(subject));
  }
}

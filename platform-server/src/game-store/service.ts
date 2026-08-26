import { createHash, randomUUID } from "node:crypto";
import {
  createDefaultPlayerSaveProfile,
  normalizePlayerSaveProfile,
  recomputeServerAchievements
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
  | "selected"
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
  selectedFillingIds: string[];
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
  runId: string;
  revenue: number;
  ingredientCost: number;
  sold: number;
  customers: number;
  batterUses: number;
  salesByFilling: FillingCountInput[];
  fillingUses: FillingCountInput[];
  expectedRevision: number;
  idempotencyKey: string;
}

export interface StartDayInput {
  day: number;
  expectedRevision: number;
  idempotencyKey: string;
}

export interface FillingCountInput {
  fillingId: string;
  count: number;
}

export interface GameDayCheckpointInput {
  runId: string;
  day: number;
  elapsedSeconds: number;
  money: number;
  openingMoney: number;
  revenue: number;
  ingredientCost: number;
  sold: number;
  customers: number;
  batterUses: number;
  salesByFilling: FillingCountInput[];
  fillingUses: FillingCountInput[];
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

function numberOr(value: unknown, fallback: number): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function fingerprint(value: unknown): string {
  return createHash("sha256").update(JSON.stringify(value)).digest("hex");
}

const fillingPrices: Readonly<Record<string, number>> = {
  "red-bean": 500,
  custard: 500,
  nutella: 700,
  "cream-cheese": 800,
  pizza: 900,
  mint: 1_000,
  "sweet-potato": 1_100,
  "green-tea": 1_200
};
const batterCost = 100;
const fillingCostRate = 0.2;
const maxBunsPerDay = 1_000;
const maxCustomersPerDay = 64;
const initialCustomerConcurrencyAllowance = 4;
const minimumCustomerCycleMilliseconds = 1_500;

function activeDay(profile: PlayerSaveProfile): JsonRecord {
  return record(record(profile.run).activeDay);
}

function normalizedCounts(entries: FillingCountInput[]): Map<string, number> {
  const result = new Map<string, number>();
  for (const entry of entries) {
    result.set(entry.fillingId, (result.get(entry.fillingId) ?? 0) + entry.count);
  }
  return result;
}

function sumCounts(counts: Map<string, number>): number {
  return [...counts.values()].reduce((sum, count) => sum + count, 0);
}

function validateAndPriceSettlement(
  profile: PlayerSaveProfile,
  input: SettleDayInput,
  now = Date.now()
) {
  const runState = activeDay(profile);
  if (!runState.runId || runState.runId !== input.runId || Number(runState.day) !== input.day) {
    throw new GameEconomyError(409, "ACTIVE_RUN_MISMATCH", "서버가 발급한 현재 영업일과 정산 요청이 일치하지 않습니다.");
  }
  const selected = new Set(stringArray(runState.selectedFillingIds));
  const sales = normalizedCounts(input.salesByFilling);
  const uses = normalizedCounts(input.fillingUses);
  for (const fillingId of new Set([...sales.keys(), ...uses.keys()])) {
    if (!(fillingId in fillingPrices) || !selected.has(fillingId)) {
      throw new GameEconomyError(400, "INVALID_FILLING", "선택하지 않은 소의 영업 기록이 포함되어 있습니다.");
    }
  }

  const sold = sumCounts(sales);
  const fillingUseCount = sumCounts(uses);
  const startedAt = Date.parse(String(runState.startedAt ?? ""));
  const elapsedMilliseconds = Number.isFinite(startedAt) ? Math.max(0, now - startedAt) : 0;
  const customerLimitAtElapsed = Math.min(
    maxCustomersPerDay,
    initialCustomerConcurrencyAllowance +
      Math.floor(elapsedMilliseconds / minimumCustomerCycleMilliseconds)
  );
  if (
    sold !== input.sold ||
    sold > fillingUseCount ||
    fillingUseCount > input.batterUses ||
    input.batterUses > maxBunsPerDay ||
    input.customers > customerLimitAtElapsed ||
    sold > input.customers * 3
  ) {
    throw new GameEconomyError(400, "IMPOSSIBLE_RUN_TOTALS", "판매량·손님·재료 사용량의 조합이 게임 규칙과 맞지 않습니다.");
  }
  for (const [fillingId, count] of sales) {
    if (count > (uses.get(fillingId) ?? 0)) {
      throw new GameEconomyError(400, "IMPOSSIBLE_RUN_TOTALS", "판매량이 사용한 소의 수량보다 많습니다.");
    }
  }

  const revenue = [...sales].reduce(
    (sum, [fillingId, count]) => sum + fillingPrices[fillingId]! * count,
    0
  );
  const ingredientCost = input.batterUses * batterCost + [...uses].reduce(
    (sum, [fillingId, count]) =>
      sum + Math.trunc(fillingPrices[fillingId]! * fillingCostRate) * count,
    0
  );
  if (input.revenue !== revenue || input.ingredientCost !== ingredientCost) {
    throw new GameEconomyError(400, "RUN_TOTAL_MISMATCH", "클라이언트 정산액이 서버 계산 결과와 일치하지 않습니다.", {
      expectedRevenue: revenue,
      expectedIngredientCost: ingredientCost
    });
  }
  return { sold, revenue, ingredientCost };
}

function countRecord(entries: FillingCountInput[]): JsonRecord[] {
  return [...normalizedCounts(entries)].map(([fillingId, count]) => ({ fillingId, count }));
}

function isOwned(profile: PlayerSaveProfile, product: GameStoreProduct): boolean {
  const run = record(profile.run);
  if (product.effect.code === "select-filling") {
    return stringArray(run.selectedFillingIds).includes(product.effect.fillingId ?? "");
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
  if (isOwned(profile, product)) {
    return product.effect.code === "select-filling" ? "selected" : "owned";
  }
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
    selectedFillingIds: stringArray(run.selectedFillingIds),
    ownedGameplayItemIds: stringArray(run.ownedGameplayItemIds),
    queuedDayEffects: structuredClone(effectArray(run.queuedDayEffects)),
    products: gameStoreProducts.map((product) => ({
      productId: product.productId,
      status: productStatus(normalized, product)
    }))
  };
}

export class GameStoreService {
  public constructor(
    private readonly saves: PlayerSaveStore,
    private readonly now: () => number = Date.now
  ) {}

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
        if (activeDay(profile).runId) {
          throw new GameEconomyError(
            409,
            "RUN_IN_PROGRESS",
            "진행 중인 영업일을 정산한 뒤 다음 날 상품을 선택해 주세요."
          );
        }
        if (isOwned(profile, product)) {
          throw new GameEconomyError(
            409,
            product.effect.code === "select-filling"
              ? "FILLING_ALREADY_SELECTED"
              : product.ownership === "next-day-consumable" ? "EFFECT_ALREADY_QUEUED" : "ALREADY_OWNED",
            product.effect.code === "select-filling"
              ? "다음 영업일에 이미 판매할 소입니다."
              : product.ownership === "next-day-consumable"
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
        if (product.effect.code === "select-filling") {
          run.unlockedFillingIds = [
            ...new Set([...stringArray(run.unlockedFillingIds), product.effect.fillingId!])
          ];
          run.selectedFillingIds = [
            ...new Set([...stringArray(run.selectedFillingIds), product.effect.fillingId!])
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

  public async startDay(subject: string, input: StartDayInput) {
    const result = await this.saves.mutate(subject, input.expectedRevision, {
      operation: "game-run-start",
      idempotencyKey: input.idempotencyKey,
      fingerprint: fingerprint({ day: input.day }),
      mutate: (stored) => {
        const profile = normalizePlayerSaveProfile(stored ?? createDefaultPlayerSaveProfile());
        const run = record(profile.run);
        if (activeDay(profile).runId) {
          throw new GameEconomyError(
            409,
            "RUN_ALREADY_ACTIVE",
            "영업일이 이미 진행 중입니다. 저장된 영업일을 먼저 복원해 주세요."
          );
        }
        const nextDay = Math.max(1, Number(run.nextDay) || 1);
        if (input.day !== nextDay) {
          throw new GameEconomyError(409, "DAY_MISMATCH", "시작할 영업일이 서버 진행과 일치하지 않습니다.", {
            expectedDay: nextDay,
            receivedDay: input.day
          });
        }
        const selectedFillingIds = [
          ...new Set([
            "red-bean",
            "custard",
            "nutella",
            "cream-cheese",
            ...stringArray(run.selectedFillingIds)
          ])
        ];
        run.selectedFillingIds = selectedFillingIds;
        const startedAt = new Date(this.now()).toISOString();
        run.activeDay = {
          runId: randomUUID(),
          day: input.day,
          startedAt,
          selectedFillingIds
        };
        profile.run = run;
        return normalizePlayerSaveProfile(profile);
      }
    });
    return {
      duplicate: result.duplicate,
      profile: result.profile,
      activeDay: activeDay(result.profile)
    };
  }

  public async settleDay(subject: string, input: SettleDayInput) {
    const result = await this.saves.mutate(subject, input.expectedRevision, {
      operation: "game-run-settle",
      idempotencyKey: input.idempotencyKey,
      fingerprint: fingerprint({
        day: input.day,
        runId: input.runId,
        revenue: input.revenue,
        ingredientCost: input.ingredientCost,
        sold: input.sold,
        customers: input.customers,
        batterUses: input.batterUses,
        salesByFilling: input.salesByFilling,
        fillingUses: input.fillingUses
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

        const totals = validateAndPriceSettlement(profile, input, this.now());
        run.money = Number(run.money) + totals.revenue - totals.ingredientCost;
        run.nextDay = input.day + 1;
        // 정산 뒤에도 기본 4종은 남기고, 이번 영업에 고른 추가 소만 비운다.
        run.selectedFillingIds = ["red-bean", "custard", "nutella", "cream-cheese"];
        delete run.activeDay;
        run.queuedDayEffects = effectArray(run.queuedDayEffects).filter(
          (effect) => Number(effect.targetDay) > input.day
        );
        profile.run = run;

        const account = record(profile.account);
        const stats = record(account.lifetimeStats);
        stats.totalSales = Math.max(0, Number(stats.totalSales) || 0) + totals.sold;
        stats.totalCustomers = Math.max(0, Number(stats.totalCustomers) || 0) + input.customers;
        stats.totalRevenue = Math.max(0, Number(stats.totalRevenue) || 0) + totals.revenue;
        stats.bestDailyProfit = Math.max(
          Number(stats.bestDailyProfit) || 0,
          totals.revenue - totals.ingredientCost
        );
        account.lifetimeStats = stats;
        profile.account = account;
        return recomputeServerAchievements(profile);
      }
    });
    return { duplicate: result.duplicate, profile: result.profile };
  }

  public async checkpointDay(subject: string, input: GameDayCheckpointInput) {
    const result = await this.saves.mutate(subject, input.expectedRevision, {
      operation: "game-run-checkpoint",
      idempotencyKey: input.idempotencyKey,
      fingerprint: fingerprint(input),
      mutate: (current) => {
        const profile = normalizePlayerSaveProfile(current ?? createDefaultPlayerSaveProfile());
        const run = record(profile.run);
        const totals = validateAndPriceSettlement(profile, input, this.now());
        const openingMoney = Number(run.money);
        if (
          input.openingMoney !== openingMoney ||
          input.money !== openingMoney + totals.revenue ||
          input.elapsedSeconds < 0 ||
          input.elapsedSeconds > 180
        ) {
          throw new GameEconomyError(400, "INVALID_CHECKPOINT", "영업 체크포인트가 서버 진행과 일치하지 않습니다.");
        }

        const currentActiveDay = activeDay(profile);
        const previous = record(currentActiveDay.checkpoint);
        if (
          numberOr(previous.elapsedSeconds, 0) > input.elapsedSeconds ||
          numberOr(previous.sold, 0) > totals.sold ||
          numberOr(previous.customers, 0) > input.customers ||
          numberOr(previous.batterUses, 0) > input.batterUses
        ) {
          throw new GameEconomyError(409, "CHECKPOINT_ROLLBACK", "이전 체크포인트보다 오래된 영업 상태입니다.");
        }

        currentActiveDay.checkpoint = {
          schemaVersion: 1,
          elapsedSeconds: input.elapsedSeconds,
          money: input.money,
          openingMoney,
          revenue: totals.revenue,
          ingredientCost: totals.ingredientCost,
          sold: totals.sold,
          customers: input.customers,
          batterUses: input.batterUses,
          salesByFilling: countRecord(input.salesByFilling),
          fillingUses: countRecord(input.fillingUses),
          capturedAt: new Date(this.now()).toISOString()
        };
        run.activeDay = currentActiveDay;
        profile.run = run;
        return normalizePlayerSaveProfile(profile);
      }
    });
    return {
      duplicate: result.duplicate,
      profile: result.profile,
      activeDay: activeDay(result.profile)
    };
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

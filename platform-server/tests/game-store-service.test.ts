import { describe, expect, it } from "vitest";
import {
  createDefaultPlayerSaveProfile,
  normalizePlayerSaveProfile
} from "../src/save/player-profile-service.js";
import {
  InMemoryPlayerSaveStore,
  SaveRevisionConflictError
} from "../src/save/save-store.js";
import { GameStoreService } from "../src/game-store/service.js";

const ids = {
  purchaseA: "00000000-0000-4000-8000-000000000001",
  purchaseB: "00000000-0000-4000-8000-000000000002",
  purchaseC: "00000000-0000-4000-8000-000000000006",
  purchaseD: "00000000-0000-4000-8000-000000000007",
  settleA: "00000000-0000-4000-8000-000000000003",
  settleB: "00000000-0000-4000-8000-000000000004",
  reset: "00000000-0000-4000-8000-000000000005",
  selectNextDay: "00000000-0000-4000-8000-000000000008"
};

async function seed(saves: InMemoryPlayerSaveStore, subject: string, money = 5000) {
  const profile = createDefaultPlayerSaveProfile();
  profile.run = { ...profile.run, money };
  return saves.put(subject, 0, profile);
}

describe("GameStoreService", () => {
  it("현재 Figma 가격과 잠금 상태를 서버 카탈로그로 제공한다", () => {
    const service = new GameStoreService(new InMemoryPlayerSaveStore());
    const catalog = service.getCatalog();
    expect(catalog.currency).toBe("game-money");
    expect(catalog.products).toEqual(expect.arrayContaining([
      expect.objectContaining({ productId: "filling-red-bean", price: 1200 }),
      expect.objectContaining({ productId: "filling-custard", price: 1400 }),
      expect.objectContaining({ productId: "filling-green-tea", price: 1800, availability: "available" }),
      expect.objectContaining({ productId: "filling-cream-cheese", availability: "coming-soon" }),
      expect.objectContaining({ productId: "item-double-golden-mold", price: 4800 }),
      expect.objectContaining({
        productId: "item-cooking-fever",
        price: 2800,
        effect: expect.objectContaining({ multiplier: 0.8, durationSeconds: 30 })
      }),
      expect.objectContaining({ productId: "filling-pizza", availability: "coming-soon" })
    ]));
  });

  it("신규 첫날은 팥만 선택하고 기존 영구 해금과 일일 선택을 분리한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new GameStoreService(saves);
    const created = await service.getMe("new-player");
    expect(created.unlockedFillingIds).toEqual(["red-bean"]);
    expect(created.selectedFillingIds).toEqual(["red-bean"]);

    const legacy = createDefaultPlayerSaveProfile();
    legacy.schemaVersion = 5;
    legacy.run = {
      ...legacy.run,
      unlockedFillingIds: ["red-bean", "custard", "nutella", "cream-cheese"]
    };
    delete legacy.run.selectedFillingIds;
    const migrated = normalizePlayerSaveProfile(legacy);
    expect(migrated.run.unlockedFillingIds).toEqual([
      "red-bean", "custard", "nutella", "cream-cheese"
    ]);
    expect(migrated.run.selectedFillingIds).toEqual(["red-bean"]);

    legacy.run.nextDay = 2;
    delete legacy.run.selectedFillingIds;
    expect(normalizePlayerSaveProfile(legacy).run.selectedFillingIds).toEqual([]);
  });

  it("계정별 구매를 격리하고 멱등 재시도·잔액·잠금·revision을 검증한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new GameStoreService(saves);
    const first = await seed(saves, "player-a", 5000);
    await seed(saves, "player-b", 5000);

    const purchased = await service.purchase("player-a", {
      productId: "filling-custard",
      expectedRevision: first.revision,
      idempotencyKey: ids.purchaseA
    });
    expect(purchased.duplicate).toBe(false);
    expect(purchased.store.money).toBe(3600);
    expect(purchased.store.unlockedFillingIds).toContain("custard");
    expect(purchased.store.selectedFillingIds).toEqual(["red-bean", "custard"]);
    expect(purchased.store.products).toContainEqual({
      productId: "filling-custard",
      status: "selected"
    });

    const duplicate = await service.purchase("player-a", {
      productId: "filling-custard",
      expectedRevision: first.revision,
      idempotencyKey: ids.purchaseA
    });
    expect(duplicate.duplicate).toBe(true);
    expect(duplicate.store.money).toBe(3600);
    expect((await service.getMe("player-b")).unlockedFillingIds).not.toContain("custard");

    await expect(service.purchase("player-a", {
      productId: "filling-custard",
      expectedRevision: purchased.profile.revision,
      idempotencyKey: ids.purchaseC
    })).rejects.toMatchObject({ code: "FILLING_ALREADY_SELECTED" });

    await expect(service.purchase("player-b", {
      productId: "filling-custard",
      expectedRevision: 1,
      idempotencyKey: ids.purchaseA
    })).rejects.toMatchObject({ name: "SaveIdempotencyConflictError" });

    await expect(service.purchase("player-a", {
      productId: "item-double-golden-mold",
      expectedRevision: purchased.profile.revision,
      idempotencyKey: ids.purchaseB
    })).rejects.toMatchObject({ code: "INSUFFICIENT_FUNDS" });

    await expect(service.purchase("player-a", {
      productId: "filling-pizza",
      expectedRevision: purchased.profile.revision,
      idempotencyKey: ids.purchaseB
    })).rejects.toMatchObject({ code: "PRODUCT_LOCKED" });

    await expect(service.purchase("player-a", {
      productId: "item-dual-pour",
      expectedRevision: first.revision,
      idempotencyKey: ids.purchaseB
    })).rejects.toBeInstanceOf(SaveRevisionConflictError);
  });

  it("하루 정산을 한 번만 반영하고 피버 예약을 사용한 날짜 뒤 제거한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new GameStoreService(saves);
    const seeded = await seed(saves, "player-a", 10_000);
    const fever = await service.purchase("player-a", {
      productId: "item-cooking-fever",
      expectedRevision: seeded.revision,
      idempotencyKey: ids.purchaseA
    });
    expect(fever.store.queuedDayEffects).toEqual([
      expect.objectContaining({ targetDay: 1, durationSeconds: 30, multiplier: 0.8 })
    ]);
    await expect(service.purchase("player-a", {
      productId: "item-cooking-fever",
      expectedRevision: fever.profile.revision,
      idempotencyKey: ids.purchaseD
    })).rejects.toMatchObject({ code: "EFFECT_ALREADY_QUEUED" });

    const settled = await service.settleDay("player-a", {
      day: 1,
      revenue: 5000,
      ingredientCost: 1000,
      sold: 7,
      customers: 3,
      expectedRevision: fever.profile.revision,
      idempotencyKey: ids.settleA
    });
    expect(settled.profile.run).toEqual(expect.objectContaining({
      nextDay: 2,
      money: 11_200,
      selectedFillingIds: [],
      queuedDayEffects: []
    }));
    expect(settled.profile.account.lifetimeStats).toEqual(expect.objectContaining({
      totalSales: 7,
      totalCustomers: 3,
      totalRevenue: 5000,
      bestDailyProfit: 4000
    }));

    const duplicate = await service.settleDay("player-a", {
      day: 1,
      revenue: 5000,
      ingredientCost: 1000,
      sold: 7,
      customers: 3,
      expectedRevision: fever.profile.revision,
      idempotencyKey: ids.settleA
    });
    expect(duplicate.duplicate).toBe(true);
    expect(duplicate.profile.run.money).toBe(11_200);

    await expect(service.settleDay("player-a", {
      day: 1,
      revenue: 5000,
      ingredientCost: 1000,
      sold: 7,
      customers: 3,
      expectedRevision: settled.profile.revision,
      idempotencyKey: ids.settleB
    })).rejects.toMatchObject({ code: "DAY_ALREADY_SETTLED" });
  });

  it("소 선택은 정산 뒤 초기화되고 다음 영업일에 다시 선택할 수 있다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new GameStoreService(saves);
    const seeded = await seed(saves, "player-a", 10_000);
    const firstSelection = await service.purchase("player-a", {
      productId: "filling-custard",
      expectedRevision: seeded.revision,
      idempotencyKey: ids.purchaseA
    });
    expect(firstSelection.store.selectedFillingIds).toEqual(["red-bean", "custard"]);

    const settled = await service.settleDay("player-a", {
      day: 1,
      revenue: 0,
      ingredientCost: 0,
      sold: 0,
      customers: 0,
      expectedRevision: firstSelection.profile.revision,
      idempotencyKey: ids.settleA
    });
    expect(settled.profile.run.selectedFillingIds).toEqual([]);

    const nextSelection = await service.purchase("player-a", {
      productId: "filling-custard",
      expectedRevision: settled.profile.revision,
      idempotencyKey: ids.selectNextDay
    });
    expect(nextSelection.store.money).toBe(7_200);
    expect(nextSelection.store.selectedFillingIds).toEqual(["custard"]);
    expect(nextSelection.store.products).toContainEqual({
      productId: "filling-custard",
      status: "selected"
    });
  });

  it("진행 초기화는 계정 데이터·설정을 보존하고 일반 상점만 초기화한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new GameStoreService(saves);
    const profile = createDefaultPlayerSaveProfile();
    profile.run = {
      ...profile.run,
      money: 20_000,
      unlockedFillingIds: ["red-bean", "custard"],
      ownedGameplayItemIds: ["item-dual-pour"],
      queuedDayEffects: [{
        productId: "item-cooking-fever",
        effectCode: "cook-time-multiplier",
        targetDay: 1,
        durationSeconds: 30,
        multiplier: 0.8
      }]
    };
    profile.account = {
      ...profile.account,
      customers: [{ customerId: "jeonghyeon", met: true }]
    };
    profile.settings = { masterVolume: 0.3, keyboardHintsEnabled: false, tutorialCompleted: true };
    const saved = await saves.put("player-a", 0, profile);

    const reset = await service.resetRun("player-a", {
      expectedRevision: saved.revision,
      idempotencyKey: ids.reset
    });
    expect(reset.profile.run).toEqual(expect.objectContaining({
      nextDay: 1,
      money: 5000,
      unlockedFillingIds: ["red-bean"],
      selectedFillingIds: ["red-bean"],
      ownedGameplayItemIds: [],
      queuedDayEffects: []
    }));
    expect(reset.profile.account.customers).toEqual([
      expect.objectContaining({ customerId: "jeonghyeon", met: true })
    ]);
    expect(reset.profile.settings).toEqual(profile.settings);
  });
});

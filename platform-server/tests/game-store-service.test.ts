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
  selectNextDay: "00000000-0000-4000-8000-000000000008",
  startA: "00000000-0000-4000-8000-000000000009",
  startB: "00000000-0000-4000-8000-000000000010",
  checkpoint: "00000000-0000-4000-8000-000000000011"
};

const defaultFillings = ["red-bean", "custard", "nutella", "cream-cheese"];

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
      expect.objectContaining({ productId: "filling-green-tea", price: 15000, availability: "available" }),
      expect.objectContaining({ productId: "filling-cream-cheese", availability: "coming-soon" }),
      expect.objectContaining({ productId: "item-double-golden-mold", price: 4800 }),
      expect.objectContaining({
        productId: "item-cooking-fever",
        price: 2800,
        effect: expect.objectContaining({ multiplier: 0.8, durationSeconds: 30 })
      }),
      expect.objectContaining({ productId: "filling-pizza", price: 6000, availability: "available" })
    ]));
  });

  it("신규 첫날은 기본 4종을 선택하고 기존 영구 해금과 일일 선택을 분리한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new GameStoreService(saves);
    const created = await service.getMe("new-player");
    expect(created.unlockedFillingIds).toEqual(defaultFillings);
    expect(created.selectedFillingIds).toEqual(defaultFillings);

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
    expect(migrated.run.selectedFillingIds).toEqual(defaultFillings);

    legacy.run.nextDay = 2;
    delete legacy.run.selectedFillingIds;
    expect(normalizePlayerSaveProfile(legacy).run.selectedFillingIds).toEqual(defaultFillings);
  });

  it("계정별 구매를 격리하고 멱등 재시도·잔액·잠금·revision을 검증한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new GameStoreService(saves);
    const first = await seed(saves, "player-a", 10000);
    await seed(saves, "player-b", 10000);

    const purchased = await service.purchase("player-a", {
      productId: "filling-pizza",
      expectedRevision: first.revision,
      idempotencyKey: ids.purchaseA
    });
    expect(purchased.duplicate).toBe(false);
    expect(purchased.store.money).toBe(4000);
    expect(purchased.store.unlockedFillingIds).toEqual([...defaultFillings, "pizza"]);
    expect(purchased.store.selectedFillingIds).toEqual([...defaultFillings, "pizza"]);
    expect(purchased.store.products).toContainEqual({
      productId: "filling-pizza",
      status: "selected"
    });

    const duplicate = await service.purchase("player-a", {
      productId: "filling-pizza",
      expectedRevision: first.revision,
      idempotencyKey: ids.purchaseA
    });
    expect(duplicate.duplicate).toBe(true);
    expect(duplicate.store.money).toBe(4000);
    expect((await service.getMe("player-b")).unlockedFillingIds).not.toContain("pizza");

    await expect(service.purchase("player-a", {
      productId: "filling-pizza",
      expectedRevision: purchased.profile.revision,
      idempotencyKey: ids.purchaseC
    })).rejects.toMatchObject({ code: "FILLING_ALREADY_SELECTED" });

    await expect(service.purchase("player-b", {
      productId: "filling-pizza",
      expectedRevision: 1,
      idempotencyKey: ids.purchaseA
    })).rejects.toMatchObject({ name: "SaveIdempotencyConflictError" });

    await expect(service.purchase("player-a", {
      productId: "item-double-golden-mold",
      expectedRevision: purchased.profile.revision,
      idempotencyKey: ids.purchaseB
    })).rejects.toMatchObject({ code: "INSUFFICIENT_FUNDS" });

    await expect(service.purchase("player-a", {
      productId: "filling-green-tea",
      expectedRevision: purchased.profile.revision,
      idempotencyKey: ids.purchaseB
    })).rejects.toMatchObject({ code: "INSUFFICIENT_FUNDS" });

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

    const started = await service.startDay("player-a", {
      day: 1,
      expectedRevision: fever.profile.revision,
      idempotencyKey: ids.startA
    });
    const runId = String(started.activeDay.runId);

    const settled = await service.settleDay("player-a", {
      day: 1,
      runId,
      revenue: 3500,
      ingredientCost: 1400,
      sold: 7,
      customers: 3,
      batterUses: 7,
      salesByFilling: [{ fillingId: "red-bean", count: 7 }],
      fillingUses: [{ fillingId: "red-bean", count: 7 }],
      expectedRevision: started.profile.revision,
      idempotencyKey: ids.settleA
    });
    expect(settled.profile.run).toEqual(expect.objectContaining({
      nextDay: 2,
      money: 9_300,
      selectedFillingIds: defaultFillings,
      queuedDayEffects: []
    }));
    expect(settled.profile.account.lifetimeStats).toEqual(expect.objectContaining({
      totalSales: 7,
      totalCustomers: 3,
      totalRevenue: 3500,
      bestDailyProfit: 2100
    }));

    const duplicate = await service.settleDay("player-a", {
      day: 1,
      runId,
      revenue: 3500,
      ingredientCost: 1400,
      sold: 7,
      customers: 3,
      batterUses: 7,
      salesByFilling: [{ fillingId: "red-bean", count: 7 }],
      fillingUses: [{ fillingId: "red-bean", count: 7 }],
      expectedRevision: started.profile.revision,
      idempotencyKey: ids.settleA
    });
    expect(duplicate.duplicate).toBe(true);
    expect(duplicate.profile.run.money).toBe(9_300);

    await expect(service.settleDay("player-a", {
      day: 1,
      runId,
      revenue: 3500,
      ingredientCost: 1400,
      sold: 7,
      customers: 3,
      batterUses: 7,
      salesByFilling: [{ fillingId: "red-bean", count: 7 }],
      fillingUses: [{ fillingId: "red-bean", count: 7 }],
      expectedRevision: settled.profile.revision,
      idempotencyKey: ids.settleB
    })).rejects.toMatchObject({ code: "DAY_ALREADY_SETTLED" });
  });

  it("추가 소 선택은 정산 뒤 초기화되고 기본 4종은 유지한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new GameStoreService(saves);
    const seeded = await seed(saves, "player-a", 16_000);
    const firstSelection = await service.purchase("player-a", {
      productId: "filling-pizza",
      expectedRevision: seeded.revision,
      idempotencyKey: ids.purchaseA
    });
    expect(firstSelection.store.selectedFillingIds).toEqual([...defaultFillings, "pizza"]);

    const started = await service.startDay("player-a", {
      day: 1,
      expectedRevision: firstSelection.profile.revision,
      idempotencyKey: ids.startA
    });

    const settled = await service.settleDay("player-a", {
      day: 1,
      runId: String(started.activeDay.runId),
      revenue: 0,
      ingredientCost: 0,
      sold: 0,
      customers: 0,
      batterUses: 0,
      salesByFilling: [],
      fillingUses: [],
      expectedRevision: started.profile.revision,
      idempotencyKey: ids.settleA
    });
    expect(settled.profile.run.selectedFillingIds).toEqual(defaultFillings);

    const nextSelection = await service.purchase("player-a", {
      productId: "filling-pizza",
      expectedRevision: settled.profile.revision,
      idempotencyKey: ids.selectNextDay
    });
    expect(nextSelection.store.money).toBe(4_000);
    expect(nextSelection.store.selectedFillingIds).toEqual([...defaultFillings, "pizza"]);
    expect(nextSelection.store.products).toContainEqual({
      productId: "filling-pizza",
      status: "selected"
    });
  });

  it("서버 발급 영업일과 규칙 기반 합계 없이는 정산할 수 없다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new GameStoreService(saves);
    const seeded = await seed(saves, "player-a", 5000);

    await expect(service.settleDay("player-a", {
      day: 1,
      runId: "00000000-0000-4000-8000-000000000099",
      revenue: 1_000_000,
      ingredientCost: 0,
      sold: 1000,
      customers: 1,
      batterUses: 1000,
      salesByFilling: [{ fillingId: "red-bean", count: 1000 }],
      fillingUses: [{ fillingId: "red-bean", count: 1000 }],
      expectedRevision: seeded.revision,
      idempotencyKey: ids.settleA
    })).rejects.toMatchObject({ code: "ACTIVE_RUN_MISMATCH" });

    const started = await service.startDay("player-a", {
      day: 1,
      expectedRevision: seeded.revision,
      idempotencyKey: ids.startA
    });
    await expect(service.purchase("player-a", {
      productId: "filling-custard",
      expectedRevision: started.profile.revision,
      idempotencyKey: ids.purchaseA
    })).rejects.toMatchObject({ code: "RUN_IN_PROGRESS" });
    await expect(service.settleDay("player-a", {
      day: 1,
      runId: String(started.activeDay.runId),
      revenue: 5000,
      ingredientCost: 0,
      sold: 1,
      customers: 1,
      batterUses: 1,
      salesByFilling: [{ fillingId: "red-bean", count: 1 }],
      fillingUses: [{ fillingId: "red-bean", count: 1 }],
      expectedRevision: started.profile.revision,
      idempotencyKey: ids.settleB
    })).rejects.toMatchObject({ code: "RUN_TOTAL_MISMATCH" });
  });

  it("영업 시작 직후 시간상 불가능한 대량 손님·판매 정산을 거부한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    let now = Date.parse("2026-08-26T00:00:00.000Z");
    const service = new GameStoreService(saves, () => now);
    const seeded = await seed(saves, "player-a", 5000);
    const started = await service.startDay("player-a", {
      day: 1,
      expectedRevision: seeded.revision,
      idempotencyKey: ids.startA
    });

    await expect(service.settleDay("player-a", {
      day: 1,
      runId: String(started.activeDay.runId),
      revenue: 192_000,
      ingredientCost: 76_800,
      sold: 384,
      customers: 128,
      batterUses: 384,
      salesByFilling: [{ fillingId: "red-bean", count: 384 }],
      fillingUses: [{ fillingId: "red-bean", count: 384 }],
      expectedRevision: started.profile.revision,
      idempotencyKey: ids.settleA
    })).rejects.toMatchObject({ code: "IMPOSSIBLE_RUN_TOTALS" });

    now += 90_000;
    await expect(service.settleDay("player-a", {
      day: 1,
      runId: String(started.activeDay.runId),
      revenue: 192_000,
      ingredientCost: 76_800,
      sold: 384,
      customers: 128,
      batterUses: 384,
      salesByFilling: [{ fillingId: "red-bean", count: 384 }],
      fillingUses: [{ fillingId: "red-bean", count: 384 }],
      expectedRevision: started.profile.revision,
      idempotencyKey: ids.settleB
    })).rejects.toMatchObject({ code: "IMPOSSIBLE_RUN_TOTALS" });
  });

  it("안전 체크포인트를 계정별·단조롭게 저장하고 정산 때 제거한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new GameStoreService(saves);
    const seeded = await seed(saves, "player-a", 5000);
    const started = await service.startDay("player-a", {
      day: 1,
      expectedRevision: seeded.revision,
      idempotencyKey: ids.startA
    });
    const runId = String(started.activeDay.runId);
    const checkpoint = await service.checkpointDay("player-a", {
      runId,
      day: 1,
      elapsedSeconds: 60,
      money: 6000,
      openingMoney: 5000,
      revenue: 1000,
      ingredientCost: 400,
      sold: 2,
      customers: 1,
      batterUses: 2,
      salesByFilling: [{ fillingId: "red-bean", count: 2 }],
      fillingUses: [{ fillingId: "red-bean", count: 2 }],
      expectedRevision: started.profile.revision,
      idempotencyKey: ids.checkpoint
    });
    expect(checkpoint.activeDay.checkpoint).toEqual(expect.objectContaining({
      elapsedSeconds: 60,
      money: 6000,
      sold: 2
    }));
    expect((await service.getMe("player-a")).money).toBe(5000);

    const replayedStart = await service.startDay("player-a", {
      day: 1,
      expectedRevision: seeded.revision,
      idempotencyKey: ids.startA
    });
    expect(replayedStart.duplicate).toBe(true);
    expect(replayedStart.profile.revision).toBe(checkpoint.profile.revision);
    expect(replayedStart.activeDay.runId).toBe(runId);

    await expect(service.checkpointDay("player-a", {
      runId,
      day: 1,
      elapsedSeconds: 30,
      money: 5500,
      openingMoney: 5000,
      revenue: 500,
      ingredientCost: 200,
      sold: 1,
      customers: 1,
      batterUses: 1,
      salesByFilling: [{ fillingId: "red-bean", count: 1 }],
      fillingUses: [{ fillingId: "red-bean", count: 1 }],
      expectedRevision: checkpoint.profile.revision,
      idempotencyKey: ids.startB
    })).rejects.toMatchObject({ code: "CHECKPOINT_ROLLBACK" });
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
      unlockedFillingIds: defaultFillings,
      selectedFillingIds: defaultFillings,
      ownedGameplayItemIds: [],
      queuedDayEffects: []
    }));
    expect(reset.profile.account.customers).toEqual([
      expect.objectContaining({ customerId: "jeonghyeon", met: true })
    ]);
    expect(reset.profile.settings).toEqual(profile.settings);
  });
});

import { describe, expect, it } from "vitest";
import { InMemoryPlayerProgressStore } from "../src/progress/store.js";
import { PlayerProfileService } from "../src/save/player-profile-service.js";
import {
  InMemoryPlayerSaveStore,
  SaveRevisionConflictError,
  type PlayerSaveProfile
} from "../src/save/save-store.js";

function profile(schemaVersion = 3): PlayerSaveProfile {
  return {
    schemaVersion,
    revision: 0,
    updatedAt: "",
    run: { nextDay: 1, money: 5000, unlockedFillingIds: [], ownedGameplayItemIds: [] },
    account: { customers: [], discoveredSouls: [], achievements: [], lifetimeStats: {} },
    ...(schemaVersion >= 3
      ? { settings: { masterVolume: 0.4, keyboardHintsEnabled: false, tutorialCompleted: true } }
      : {})
  };
}

describe("PlayerProfileService", () => {
  it("설정·서버 산출 업적·진행을 계정별 SAVE#MAIN에 격리한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new PlayerProfileService(saves, new InMemoryPlayerProgressStore());
    await service.put("player-a", 0, {
      ...profile(),
      account: { ...profile().account, achievements: [{ achievementId: "first-sale", unlocked: true }] }
    });
    await service.put("player-b", 0, profile());

    expect((await service.get("player-a"))?.settings).toEqual({
      masterVolume: 0.4,
      keyboardHintsEnabled: false,
      tutorialCompleted: true
    });
    const firstAchievements = (await service.get("player-a"))?.account.achievements as Array<Record<string, unknown>>;
    const secondAchievements = (await service.get("player-b"))?.account.achievements as Array<Record<string, unknown>>;
    expect(firstAchievements).toHaveLength(8);
    expect(secondAchievements).toHaveLength(8);
    expect(firstAchievements.find((entry) => entry.achievementId === "first-sale"))
      .toEqual(expect.objectContaining({ progress: 0, unlocked: false }));
  });

  it("일반 프로필 PUT이 누적 통계와 업적을 덮어쓰지 못한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new PlayerProfileService(saves, new InMemoryPlayerProgressStore());
    const injected = profile();
    injected.account = {
      ...injected.account,
      lifetimeStats: {
        totalSales: 999_999,
        totalCustomers: 999_999,
        totalRevenue: 999_999_999,
        bestDailyProfit: 999_999_999
      },
      achievements: [{
        achievementId: "first-sale",
        progress: 999_999,
        unlocked: true,
        unlockedAt: "2000-01-01T00:00:00.000Z"
      }]
    };

    const saved = await service.put("player-a", 0, injected);
    expect(saved.account.lifetimeStats).toEqual({
      totalSales: 0,
      totalCustomers: 0,
      totalRevenue: 0,
      bestDailyProfit: 0
    });
    expect((saved.account.achievements as Array<Record<string, unknown>>)
      .find((entry) => entry.achievementId === "first-sale"))
      .toEqual(expect.objectContaining({ progress: 0, unlocked: false, unlockedAt: "" }));
  });

  it("첫 전체 저장에서도 서버 경제 기본값을 클라이언트 입력보다 우선한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new PlayerProfileService(saves, new InMemoryPlayerProgressStore());
    const injected = profile();
    injected.run = {
      nextDay: 99,
      money: 999_999,
      unlockedFillingIds: ["red-bean", "custard", "nutella"],
      ownedGameplayItemIds: ["item-double-golden-mold"],
      queuedDayEffects: [{
        productId: "item-cooking-fever",
        effectCode: "cook-time-multiplier",
        targetDay: 99,
        durationSeconds: 30,
        multiplier: 0.8
      }]
    };

    const saved = await service.put("player-a", 0, injected);
    expect(saved.run).toEqual(expect.objectContaining({
      nextDay: 1,
      money: 5000,
      unlockedFillingIds: ["red-bean"],
      ownedGameplayItemIds: [],
      queuedDayEffects: []
    }));
  });

  it("jeonghyun 데이터를 jeonghyeon으로 손실 없이 단조 병합한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new PlayerProfileService(saves, new InMemoryPlayerProgressStore());
    const input = profile();
    input.account.customers = [
      { customerId: "jeonghyeon", met: true, visitCount: 1, completedTopicIds: ["topic-1"] },
      { customerId: "jeonghyun", storyCompleted: true, visitCount: 3, completedTopicIds: ["topic-2"] }
    ];
    const saved = await service.put("player-a", 0, input);
    const customers = saved.account.customers as Array<Record<string, unknown>>;
    expect(customers).toHaveLength(1);
    expect(customers[0]).toEqual(expect.objectContaining({
      customerId: "jeonghyeon",
      met: true,
      storyCompleted: true,
      visitCount: 3,
      completedTopicIds: ["topic-1", "topic-2"]
    }));
  });

  it("알 수 없는 손님·영혼 ID를 버리고 계정 통계 없이 진행 업적을 열지 않는다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new PlayerProfileService(saves, new InMemoryPlayerProgressStore());
    const input = profile();
    input.account.customers = [
      { customerId: "not-a-customer", met: true, storyCompleted: true },
      { customerId: "jeonghyeon", met: true, storyCompleted: true }
    ];
    input.account.discoveredSouls = [
      { soulId: "not-a-soul" },
      { soulId: "soul:red-bean:perfect" }
    ];

    const saved = await service.put("player-a", 0, input);
    expect(saved.account.customers).toEqual([
      expect.objectContaining({ customerId: "jeonghyeon", met: true, storyCompleted: true })
    ]);
    expect(saved.account.discoveredSouls).toEqual([
      expect.objectContaining({ soulId: "soul:red-bean:perfect" })
    ]);
    const achievements = saved.account.achievements as Array<Record<string, unknown>>;
    expect(achievements.find((entry) => entry.achievementId === "meet-all-customers"))
      .toEqual(expect.objectContaining({ progress: 0, unlocked: false }));
    expect(achievements.find((entry) => entry.achievementId === "first-story"))
      .toEqual(expect.objectContaining({ progress: 0, unlocked: false }));
    expect(achievements.find((entry) => entry.achievementId === "soul-collector-8"))
      .toEqual(expect.objectContaining({ progress: 0, unlocked: false }));
  });

  it("구버전 클라이언트가 특별 주문 상태를 생략해도 서버의 retry를 보존한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new PlayerProfileService(saves, new InMemoryPlayerProgressStore());
    const initial = profile();
    initial.run.customerStories = [{
      customerId: "jeonghyeon",
      currentDialogIndex: 2,
      specialOrderState: "retry"
    }];
    const stored = await service.put("player-a", 0, initial);
    const legacyUpdate = structuredClone(stored);
    delete (legacyUpdate.run.customerStories as Array<Record<string, unknown>>)[0]!.specialOrderState;

    const saved = await service.put("player-a", stored.revision, legacyUpdate);
    expect(saved.run.customerStories).toEqual([
      expect.objectContaining({ customerId: "jeonghyeon", specialOrderState: "retry" })
    ]);
  });

  it("구버전 설정은 클라이언트 이관 전까지 v2로 유지하고 PUT에서 v7이 된다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const service = new PlayerProfileService(saves, new InMemoryPlayerProgressStore());
    await saves.put("player-a", 0, profile(2));

    const legacy = await service.get("player-a");
    expect(legacy?.schemaVersion).toBe(2);
    expect(legacy?.settings).toBeUndefined();
    const upgraded = await service.put("player-a", legacy!.revision, {
      ...legacy!,
      settings: { masterVolume: 0.25, keyboardHintsEnabled: false, tutorialCompleted: true }
    });
    expect(upgraded.schemaVersion).toBe(8);
    expect((await service.get("player-a"))?.settings).toEqual(upgraded.settings);
  });

  it("/progress 호환 변경을 SAVE#MAIN에 반영하고 revision 충돌을 보존한다", async () => {
    const saves = new InMemoryPlayerSaveStore();
    const legacy = new InMemoryPlayerProgressStore();
    await legacy.mergeStoryProgress("player-a", "jeonghyeon", {
      completedTopicIndexes: [0],
      storyCompleted: false
    });
    const service = new PlayerProfileService(saves, legacy);
    const progress = await service.mergeStoryProgress("player-a", "jeonghyeon", {
      completedTopicIndexes: [1],
      storyCompleted: true
    });
    expect(progress.customers[0]).toEqual(expect.objectContaining({
      completedTopicIndexes: [0, 1],
      storyCompleted: true
    }));
    const stored = await saves.get("player-a");
    expect(stored).toBeDefined();
    await expect(service.put("player-a", 0, profile())).rejects.toBeInstanceOf(SaveRevisionConflictError);
  });
});

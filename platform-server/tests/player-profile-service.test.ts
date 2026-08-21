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
  it("설정·업적·진행을 계정별 SAVE#MAIN에 격리한다", async () => {
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
    expect(((await service.get("player-a"))?.account.achievements as unknown[])).toHaveLength(1);
    expect(((await service.get("player-b"))?.account.achievements as unknown[])).toHaveLength(0);
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

  it("구버전 설정은 클라이언트 이관 전까지 v2로 유지하고 PUT에서 v3가 된다", async () => {
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
    expect(upgraded.schemaVersion).toBe(3);
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

import { describe, expect, it } from "vitest";
import { InMemoryPlayerProgressStore } from "../src/progress/store.js";

describe("player progress store", () => {
  it("사용자별 도감과 스토리 진행을 격리한다", async () => {
    const store = new InMemoryPlayerProgressStore();

    await store.markCustomerMet("player-a", "jeonghyeon");
    await store.mergeStoryProgress("player-a", "jeonghyeon", {
      completedTopicIndexes: [0, 2],
      storyCompleted: false
    });

    expect(await store.getPlayerProgress("player-a")).toEqual({
      schemaVersion: 1,
      customers: [
        {
          customerId: "jeonghyeon",
          met: true,
          completedTopicIndexes: [0, 2],
          storyCompleted: false
        }
      ]
    });
    expect(await store.getPlayerProgress("player-b")).toEqual({
      schemaVersion: 1,
      customers: []
    });
  });

  it("서로 다른 기기의 진행을 단조롭게 병합해 완료 상태가 되돌아가지 않는다", async () => {
    const store = new InMemoryPlayerProgressStore();

    await store.mergeStoryProgress("player-a", "jeonghyeon", {
      completedTopicIndexes: [0, 2, 2],
      storyCompleted: true
    });
    await store.mergeStoryProgress("player-a", "jeonghyeon", {
      completedTopicIndexes: [1],
      storyCompleted: false
    });

    expect((await store.getPlayerProgress("player-a")).customers[0]).toEqual({
      customerId: "jeonghyeon",
      met: true,
      completedTopicIndexes: [0, 1, 2],
      storyCompleted: true
    });
  });
});

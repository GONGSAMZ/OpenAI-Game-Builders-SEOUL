import { describe, expect, it } from "vitest";
import { runAccountScopeSmoke } from "../scripts/account-scope-smoke.js";

describe("account scope smoke tool", () => {
  it("두 계정의 저장·경제·프리미엄 데이터를 끝까지 격리한다", async () => {
    const report = await runAccountScopeSmoke();

    expect(report.ok, JSON.stringify(report.checks, null, 2)).toBe(true);
    expect(report.checks).toHaveLength(8);
    expect(report.checks.every((check) => check.status === "pass")).toBe(true);
    expect(report.accounts?.accountA).toEqual(expect.objectContaining({
      gameMoney: 5000,
      unlockedFillingIds: ["red-bean"],
      moldSkin: "golden-pan",
      purchaseCount: 1
    }));
    expect(report.accounts?.accountB).toEqual(expect.objectContaining({
      gameMoney: 5000,
      unlockedFillingIds: ["red-bean"],
      premiumInventory: [],
      moldSkin: null,
      purchaseCount: 0
    }));
  });
});

import path from "node:path";
import { isDeepStrictEqual } from "node:util";
import { pathToFileURL } from "node:url";
import request from "supertest";
import { createApp } from "../src/app.js";
import type { AppConfig } from "../src/config.js";
import { InMemoryPlayerSaveStore } from "../src/save/save-store.js";
import { InMemorySessionStore } from "../src/session-store.js";
import { InMemoryPurchaseHistoryStore } from "../src/store/purchase-history.js";
import { InMemoryMarketStore } from "../src/store/store.js";

type JsonRecord = Record<string, unknown>;

const serverAchievementIds = [
  "first-sale",
  "sales-50",
  "customers-30",
  "revenue-50000",
  "daily-profit-10000",
  "meet-all-customers",
  "first-story",
  "soul-collector-8"
];
const defaultFillings = ["red-bean", "custard", "nutella", "cream-cheese"];

export interface AccountScopeSmokeCheck {
  name: string;
  status: "pass" | "fail";
  details?: string;
}

export interface AccountScopeSummary {
  saveRevision: number;
  gameMoney: number;
  nextDay: number;
  unlockedFillingIds: string[];
  selectedFillingIds: string[];
  premiumInventory: Array<{ itemId: string; quantity: number }>;
  moldSkin: string | null;
  purchaseCount: number;
}

export interface AccountScopeSmokeReport {
  ok: boolean;
  mode: "in-memory-api";
  startedAt: string;
  durationMs: number;
  checks: AccountScopeSmokeCheck[];
  accounts?: {
    accountA: AccountScopeSummary;
    accountB: AccountScopeSummary;
  };
}

interface HttpResult {
  status: number;
  body: any;
  text: string;
}

class StopSmokeTest extends Error {}

function createSmokeConfig(): AppConfig {
  return {
    nodeEnv: "test",
    port: 3000,
    publicBaseUrl: "http://localhost:3000",
    gameOrigin: "http://localhost:3000",
    gameBuildDirectory: path.resolve(process.cwd(), "game-dist"),
    sessionTtlSeconds: 3600,
    revision: "account-scope-smoke",
    hive: {
      mode: "mock",
      country: "KR",
      language: "ko"
    },
    store: {
      mode: "mock",
      catalogSource: "static",
      catalogCacheSeconds: 300,
      productImageBaseUrl: "http://localhost:3000/store-products",
      devToolsEnabled: true,
      dataStore: "memory",
      cursorSigningSecret: "account-scope-smoke-cursor-secret"
    },
    nicepay: {
      apiBaseUrl: "https://sandbox-api.nicepay.co.kr"
    },
    openai: {
      mode: "mock",
      model: "gpt-5.6-luna"
    }
  };
}

function jsonRecord(value: unknown): JsonRecord {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error("예상한 JSON 객체가 아닙니다.");
  }
  return value as JsonRecord;
}

function stringArray(value: unknown): string[] {
  if (!Array.isArray(value) || value.some((entry) => typeof entry !== "string")) {
    throw new Error("예상한 문자열 배열이 아닙니다.");
  }
  return value as string[];
}

function expectHttp(result: HttpResult, expected: number): void {
  if (result.status === expected) return;
  const payload = result.text || JSON.stringify(result.body);
  throw new Error(`HTTP ${expected}을 기대했지만 ${result.status}입니다: ${payload.slice(0, 500)}`);
}

function expectValue(actual: unknown, expected: unknown, message: string): void {
  if (isDeepStrictEqual(actual, expected)) return;
  throw new Error(
    `${message} (기대값=${JSON.stringify(expected)}, 실제값=${JSON.stringify(actual)})`
  );
}

function expectTrue(value: unknown, message: string): void {
  if (value === true) return;
  throw new Error(message);
}

function inventoryQuantity(inventory: unknown, itemId: string): number {
  if (!Array.isArray(inventory)) throw new Error("인벤토리 응답이 배열이 아닙니다.");
  const entry = inventory.find((candidate) =>
    candidate && typeof candidate === "object" && (candidate as JsonRecord).itemId === itemId
  ) as JsonRecord | undefined;
  return Number(entry?.quantity ?? 0);
}

function achievementIds(profile: unknown): string[] {
  const account = jsonRecord(jsonRecord(profile).account);
  const achievements = account.achievements;
  if (!Array.isArray(achievements)) return [];
  return achievements.flatMap((entry) => {
    if (!entry || typeof entry !== "object") return [];
    const id = (entry as JsonRecord).achievementId;
    return typeof id === "string" ? [id] : [];
  });
}

function achievementUnlocked(profile: unknown, achievementId: string): boolean {
  const account = jsonRecord(jsonRecord(profile).account);
  const achievements = account.achievements;
  if (!Array.isArray(achievements)) return false;
  const entry = achievements.find((candidate) =>
    candidate && typeof candidate === "object" &&
    (candidate as JsonRecord).achievementId === achievementId
  );
  return Boolean(entry && (entry as JsonRecord).unlocked === true);
}

function profileRun(profile: unknown): JsonRecord {
  return jsonRecord(jsonRecord(profile).run);
}

function profileSettings(profile: unknown): JsonRecord {
  return jsonRecord(jsonRecord(profile).settings);
}

function auth(token: string): { Authorization: string } {
  return { Authorization: `Bearer ${token}` };
}

export async function runAccountScopeSmoke(): Promise<AccountScopeSmokeReport> {
  const started = Date.now();
  const report: AccountScopeSmokeReport = {
    ok: false,
    mode: "in-memory-api",
    startedAt: new Date(started).toISOString(),
    durationMs: 0,
    checks: []
  };

  const sessions = new InMemorySessionStore(3600);
  const playerSaves = new InMemoryPlayerSaveStore();
  const marketStore = new InMemoryMarketStore();
  const purchaseHistory = new InMemoryPurchaseHistoryStore(
    "account-scope-smoke-cursor-secret"
  );
  const app = createApp({
    config: createSmokeConfig(),
    sessions,
    playerSaves,
    marketStore,
    purchaseHistory
  });

  const sessionA = await sessions.create({
    subject: "account-scope-smoke:A",
    provider: "mock-hive",
    playerId: "account-a"
  });
  const sessionB = await sessions.create({
    subject: "account-scope-smoke:B",
    provider: "mock-hive",
    playerId: "account-b"
  });
  let authA = auth(sessionA.token);
  const authB = auth(sessionB.token);
  let revisionA = 0;

  async function step(name: string, action: () => Promise<string | undefined>): Promise<void> {
    try {
      const details = await action();
      report.checks.push({ name, status: "pass", details });
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      report.checks.push({ name, status: "fail", details: message });
      throw new StopSmokeTest(message);
    }
  }

  try {
    await step("공개 카탈로그와 인증 경계", async () => {
      const catalog = await request(app).get("/api/v1/game-store/catalog");
      expectHttp(catalog, 200);
      expectTrue(
        Array.isArray(catalog.body.products) && catalog.body.products.length >= 7,
        "일반 상점 카탈로그 상품이 충분히 노출되지 않았습니다."
      );
      const anonymous = await request(app).get("/api/v1/game-store/me");
      expectHttp(anonymous, 401);
      return `상품 ${catalog.body.products.length}개, 비로그인 경제 조회 차단`;
    });

    await step("서로 다른 두 세션의 계정 식별", async () => {
      const [identityA, identityB] = await Promise.all([
        request(app).get("/api/v1/auth/session").set(authA),
        request(app).get("/api/v1/auth/session").set(authB)
      ]);
      expectHttp(identityA, 200);
      expectHttp(identityB, 200);
      expectValue(identityA.body.session.subject, "account-scope-smoke:A", "A 계정 subject가 다릅니다.");
      expectValue(identityB.body.session.subject, "account-scope-smoke:B", "B 계정 subject가 다릅니다.");

      const [stateA, stateB] = await Promise.all([
        request(app).get("/api/v1/game-store/me").set(authA),
        request(app).get("/api/v1/game-store/me").set(authB)
      ]);
      expectHttp(stateA, 200);
      expectHttp(stateB, 200);
      expectValue(stateA.body.money, 5000, "A 신규 계정 일반 돈 초기값이 다릅니다.");
      expectValue(stateB.body.money, 5000, "B 신규 계정 일반 돈 초기값이 다릅니다.");
      expectValue(stateA.body.unlockedFillingIds, defaultFillings, "A 신규 계정 기본 재료가 다릅니다.");
      expectValue(stateB.body.unlockedFillingIds, defaultFillings, "B 신규 계정 기본 재료가 다릅니다.");
      expectValue(stateA.body.selectedFillingIds, defaultFillings, "A 첫날 기본 재료 선택이 없습니다.");
      expectValue(stateB.body.selectedFillingIds, defaultFillings, "B 첫날 기본 재료 선택이 없습니다.");
      revisionA = Number(stateA.body.revision);
      return "A/B subject 분리, 신규 프로필 각각 생성";
    });

    await step("계정 설정·업적 저장과 경제 필드 보호", async () => {
      const [savedA, savedB] = await Promise.all([
        request(app).get("/api/v1/save/profile").set(authA),
        request(app).get("/api/v1/save/profile").set(authB)
      ]);
      expectHttp(savedA, 200);
      expectHttp(savedB, 200);
      const sourceProfile = jsonRecord(savedA.body.profile);
      revisionA = Number(sourceProfile.revision);
      const sourceRun = jsonRecord(sourceProfile.run);
      const sourceAccount = jsonRecord(sourceProfile.account);
      const updated = await request(app)
        .put("/api/v1/save/profile")
        .set(authA)
        .send({
          expectedRevision: revisionA,
          profile: {
            ...sourceProfile,
            run: {
              ...sourceRun,
              money: 999_999,
              unlockedFillingIds: ["red-bean", "custard", "nutella"],
              selectedFillingIds: ["red-bean", "custard", "nutella"],
              ownedGameplayItemIds: ["item-dual-pour"]
            },
            account: {
              ...sourceAccount,
              achievements: [{ achievementId: "smoke-account-a" }]
            },
            settings: {
              masterVolume: 0.37,
              keyboardHintsEnabled: false,
              tutorialCompleted: true
            }
          }
        });
      expectHttp(updated, 200);
      revisionA = Number(updated.body.profile.revision);
      const run = profileRun(updated.body.profile);
      expectValue(run.money, 5000, "전체 저장 요청이 서버 권한 일반 돈을 덮어썼습니다.");
      expectValue(run.unlockedFillingIds, defaultFillings, "전체 저장 요청이 재료 해금을 덮어썼습니다.");
      expectValue(run.selectedFillingIds, defaultFillings, "전체 저장 요청이 일일 소 선택을 덮어썼습니다.");
      expectValue(run.ownedGameplayItemIds, [], "전체 저장 요청이 도구 보유를 덮어썼습니다.");
      expectValue(
        achievementIds(updated.body.profile),
        serverAchievementIds,
        "서버 업적 카탈로그가 유지되지 않았습니다."
      );
      expectTrue(
        !achievementIds(updated.body.profile).includes("smoke-account-a"),
        "클라이언트가 임의 업적 ID를 주입했습니다."
      );
      expectValue(profileSettings(updated.body.profile), {
        masterVolume: 0.37,
        keyboardHintsEnabled: false,
        tutorialCompleted: true
      }, "A 설정이 저장되지 않았습니다.");
      expectValue(achievementIds(savedB.body.profile), serverAchievementIds, "B 서버 업적 카탈로그가 다릅니다.");
      return "설정 저장, 서버 산출 업적 유지, 임의 업적·일반 돈·해금 조작 차단";
    });

    await step("일반 상점 구매·멱등성과 B 계정 격리", async () => {
      const purchaseKey = "31000000-0000-4000-8000-000000000001";
      const purchased = await request(app)
        .post("/api/v1/game-store/purchases")
        .set(authA)
        .set("Idempotency-Key", purchaseKey)
        .send({ productId: "item-dual-pour", expectedRevision: revisionA });
      expectHttp(purchased, 200);
      expectValue(purchased.body.store.money, 1800, "A 구매 후 일반 돈이 다릅니다.");
      expectValue(purchased.body.store.unlockedFillingIds, defaultFillings, "A 기본 재료가 변경됐습니다.");
      expectValue(purchased.body.store.selectedFillingIds, defaultFillings, "A 기본 재료 선택이 변경됐습니다.");
      expectValue(purchased.body.store.ownedGameplayItemIds, ["item-dual-pour"], "A 동시 붓기 보유가 없습니다.");
      revisionA = Number(purchased.body.profile.revision);

      const duplicate = await request(app)
        .post("/api/v1/game-store/purchases")
        .set(authA)
        .set("Idempotency-Key", purchaseKey)
        .send({ productId: "item-dual-pour", expectedRevision: revisionA - 1 });
      expectHttp(duplicate, 200);
      expectValue(duplicate.body.duplicate, true, "같은 구매 키가 중복 처리되지 않았습니다.");
      expectValue(duplicate.body.store.money, 1800, "중복 구매가 일반 돈을 다시 차감했습니다.");

      const stateB = await request(app).get("/api/v1/game-store/me").set(authB);
      expectHttp(stateB, 200);
      expectValue(stateB.body.money, 5000, "A 구매가 B 일반 돈을 변경했습니다.");
      expectValue(stateB.body.unlockedFillingIds, defaultFillings, "A 구매가 B 재료를 변경했습니다.");
      expectValue(stateB.body.selectedFillingIds, defaultFillings, "A 선택이 B 일일 소에 섞였습니다.");
      expectValue(stateB.body.ownedGameplayItemIds, [], "A 도구 구매가 B 보유품에 섞였습니다.");
      return "A 동시 붓기 구매 1회 반영, 동일 키 재시도 무차감, B 불변";
    });

    await step("하루 정산과 누적 통계의 계정 격리", async () => {
      const startedDay = await request(app)
        .post("/api/v1/game-run/start-day")
        .set(authA)
        .set("Idempotency-Key", "31500000-0000-4000-8000-000000000001")
        .send({ day: 1, expectedRevision: revisionA });
      expectHttp(startedDay, 200);
      revisionA = Number(startedDay.body.profile.revision);
      const runId = String(startedDay.body.activeDay.runId);

      const settled = await request(app)
        .post("/api/v1/game-run/settle-day")
        .set(authA)
        .set("Idempotency-Key", "32000000-0000-4000-8000-000000000001")
        .send({
          day: 1,
          runId,
          revenue: 2000,
          ingredientCost: 800,
          sold: 4,
          customers: 2,
          batterUses: 4,
          salesByFilling: [{ fillingId: "red-bean", count: 4 }],
          fillingUses: [{ fillingId: "red-bean", count: 4 }],
          expectedRevision: revisionA
        });
      expectHttp(settled, 200);
      revisionA = Number(settled.body.profile.revision);
      const runA = profileRun(settled.body.profile);
      expectValue(runA.money, 3000, "A 정산 잔액이 다릅니다.");
      expectValue(runA.nextDay, 2, "A 정산 후 날짜가 다릅니다.");
      expectValue(runA.selectedFillingIds, [], "A 정산 후 일일 소 선택이 초기화되지 않았습니다.");
      const lifetime = jsonRecord(jsonRecord(jsonRecord(settled.body.profile).account).lifetimeStats);
      expectValue(lifetime.totalSales, 4, "A 누적 판매량이 반영되지 않았습니다.");
      expectTrue(achievementUnlocked(settled.body.profile, "first-sale"), "서버가 첫 판매 업적을 계산하지 않았습니다.");

      const profileB = await request(app).get("/api/v1/save/profile").set(authB);
      expectHttp(profileB, 200);
      const runB = profileRun(profileB.body.profile);
      expectValue(runB.money, 5000, "A 정산이 B 일반 돈을 변경했습니다.");
      expectValue(runB.nextDay, 1, "A 정산이 B 날짜를 변경했습니다.");
      expectValue(runB.selectedFillingIds, defaultFillings, "A 정산이 B 일일 소 선택을 변경했습니다.");
      const lifetimeB = jsonRecord(jsonRecord(jsonRecord(profileB.body.profile).account).lifetimeStats);
      expectValue(Number(lifetimeB.totalSales ?? 0), 0, "A 누적 통계가 B 계정에 섞였습니다.");
      expectTrue(!achievementUnlocked(profileB.body.profile, "first-sale"), "A 업적 해제가 B 계정에 섞였습니다.");
      return "A 1일차 정산·누적 통계 반영, B 진행·통계 불변";
    });

    await step("프리미엄 구매·황금 틀 장착·구매 내역 격리", async () => {
      const purchased = await request(app)
        .post("/api/v1/store/mock-purchases")
        .set(authA)
        .send({
          productId: "golden-pan",
          idempotencyKey: "33000000-0000-4000-8000-000000000001"
        });
      expectHttp(purchased, 201);
      expectValue(inventoryQuantity(purchased.body.inventory, "golden-pan"), 1, "A 황금 틀이 지급되지 않았습니다.");
      expectValue(purchased.body.wallet.testPoints, 6700, "A 테스트 포인트 차감액이 다릅니다.");

      const equipped = await request(app)
        .put("/api/v1/store/equipment/mold")
        .set(authA)
        .send({ itemId: "golden-pan" });
      expectHttp(equipped, 200);
      expectValue(equipped.body.equipment.moldSkin, "golden-pan", "A 황금 틀이 장착되지 않았습니다.");

      const forbiddenB = await request(app)
        .put("/api/v1/store/equipment/mold")
        .set(authB)
        .send({ itemId: "golden-pan" });
      expectHttp(forbiddenB, 409);

      const [premiumB, historyA, historyB] = await Promise.all([
        request(app).get("/api/v1/store/me").set(authB),
        request(app).get("/api/v1/store/purchases").set(authA),
        request(app).get("/api/v1/store/purchases").set(authB)
      ]);
      expectHttp(premiumB, 200);
      expectHttp(historyA, 200);
      expectHttp(historyB, 200);
      expectValue(premiumB.body.wallet.testPoints, 10_000, "A 결제가 B 테스트 포인트를 변경했습니다.");
      expectValue(premiumB.body.inventory, [], "A 황금 틀이 B 인벤토리에 섞였습니다.");
      expectValue(premiumB.body.equipment.moldSkin, null, "A 장착 상태가 B 계정에 섞였습니다.");
      expectValue(historyA.body.purchases.length, 1, "A 구매 내역 수가 다릅니다.");
      expectValue(historyB.body.purchases.length, 0, "A 구매 내역이 B 계정에 노출됐습니다.");
      return "A 황금 틀 지급·장착·내역 생성, B 미보유 장착 거부 및 데이터 불변";
    });

    await step("진행 초기화의 보존 경계", async () => {
      const reset = await request(app)
        .post("/api/v1/save/reset-run")
        .set(authA)
        .set("Idempotency-Key", "34000000-0000-4000-8000-000000000001")
        .send({ expectedRevision: revisionA });
      expectHttp(reset, 200);
      revisionA = Number(reset.body.profile.revision);
      const run = profileRun(reset.body.profile);
      expectValue(run.money, 5000, "초기화 후 일반 돈이 초기값이 아닙니다.");
      expectValue(run.nextDay, 1, "초기화 후 날짜가 1일차가 아닙니다.");
      expectValue(run.unlockedFillingIds, defaultFillings, "초기화 후 기본 재료가 다릅니다.");
      expectValue(run.selectedFillingIds, defaultFillings, "초기화 후 첫날 기본 재료 선택이 없습니다.");
      expectTrue(achievementUnlocked(reset.body.profile, "first-sale"), "초기화가 계정 업적을 제거했습니다.");
      expectValue(profileSettings(reset.body.profile), {
        masterVolume: 0.37,
        keyboardHintsEnabled: false,
        tutorialCompleted: true
      }, "초기화가 계정 설정을 제거했습니다.");

      const premiumA = await request(app).get("/api/v1/store/me").set(authA);
      expectHttp(premiumA, 200);
      expectValue(inventoryQuantity(premiumA.body.inventory, "golden-pan"), 1, "초기화가 프리미엄 보유품을 제거했습니다.");
      expectValue(premiumA.body.equipment.moldSkin, "golden-pan", "초기화가 프리미엄 장착 상태를 제거했습니다.");
      return "run만 초기화, 업적·설정·프리미엄 보유품·장착 유지";
    });

    await step("로그아웃·재로그인 후 계정 복원", async () => {
      const loggedOut = await request(app).delete("/api/v1/auth/session").set(authA);
      expectHttp(loggedOut, 204);
      const revoked = await request(app).get("/api/v1/save/profile").set(authA);
      expectHttp(revoked, 401);

      const newSessionA = await sessions.create({
        subject: "account-scope-smoke:A",
        provider: "mock-hive",
        playerId: "account-a"
      });
      authA = auth(newSessionA.token);
      const [profileA, generalA, premiumA, historyA, profileB] = await Promise.all([
        request(app).get("/api/v1/save/profile").set(authA),
        request(app).get("/api/v1/game-store/me").set(authA),
        request(app).get("/api/v1/store/me").set(authA),
        request(app).get("/api/v1/store/purchases").set(authA),
        request(app).get("/api/v1/save/profile").set(authB)
      ]);
      for (const response of [profileA, generalA, premiumA, historyA, profileB]) {
        expectHttp(response, 200);
      }
      expectValue(Number(profileA.body.profile.revision), revisionA, "재로그인 후 A 저장 revision이 다릅니다.");
      expectTrue(achievementUnlocked(profileA.body.profile, "first-sale"), "재로그인 후 A 업적이 복원되지 않았습니다.");
      expectValue(generalA.body.money, 5000, "재로그인 후 A 일반 돈이 복원되지 않았습니다.");
      expectValue(inventoryQuantity(premiumA.body.inventory, "golden-pan"), 1, "재로그인 후 A 황금 틀이 복원되지 않았습니다.");
      expectValue(premiumA.body.equipment.moldSkin, "golden-pan", "재로그인 후 A 장착 상태가 복원되지 않았습니다.");
      expectValue(historyA.body.purchases.length, 1, "재로그인 후 A 구매 내역이 복원되지 않았습니다.");
      expectTrue(!achievementUnlocked(profileB.body.profile, "first-sale"), "재로그인 과정에서 A 업적이 B에 섞였습니다.");
      return "기존 세션 폐기 확인, 같은 subject의 새 세션에서 A 데이터 복원";
    });

    const [profileA, generalA, premiumA, historyA, profileB, generalB, premiumB, historyB] =
      await Promise.all([
        request(app).get("/api/v1/save/profile").set(authA),
        request(app).get("/api/v1/game-store/me").set(authA),
        request(app).get("/api/v1/store/me").set(authA),
        request(app).get("/api/v1/store/purchases").set(authA),
        request(app).get("/api/v1/save/profile").set(authB),
        request(app).get("/api/v1/game-store/me").set(authB),
        request(app).get("/api/v1/store/me").set(authB),
        request(app).get("/api/v1/store/purchases").set(authB)
      ]);
    const summary = (
      profileResult: HttpResult,
      generalResult: HttpResult,
      premiumResult: HttpResult,
      historyResult: HttpResult
    ): AccountScopeSummary => {
      for (const response of [profileResult, generalResult, premiumResult, historyResult]) {
        expectHttp(response, 200);
      }
      const profile = jsonRecord(profileResult.body.profile);
      const run = profileRun(profile);
      const inventory = premiumResult.body.inventory as Array<{ itemId: string; quantity: number }>;
      return {
        saveRevision: Number(profile.revision),
        gameMoney: Number(generalResult.body.money),
        nextDay: Number(run.nextDay),
        unlockedFillingIds: stringArray(generalResult.body.unlockedFillingIds),
        selectedFillingIds: stringArray(generalResult.body.selectedFillingIds),
        premiumInventory: inventory,
        moldSkin: premiumResult.body.equipment.moldSkin as string | null,
        purchaseCount: Number(historyResult.body.purchases.length)
      };
    };
    report.accounts = {
      accountA: summary(profileA, generalA, premiumA, historyA),
      accountB: summary(profileB, generalB, premiumB, historyB)
    };
    report.ok = true;
  } catch (error) {
    if (!(error instanceof StopSmokeTest)) throw error;
  } finally {
    report.durationMs = Date.now() - started;
  }

  return report;
}

function renderHumanReport(report: AccountScopeSmokeReport): string {
  const lines = [
    "계정 단위 데이터 스모크 테스트",
    `실행 방식: ${report.mode} (AWS/HIVE 실데이터 미사용)`,
    ""
  ];
  for (const check of report.checks) {
    lines.push(`${check.status === "pass" ? "PASS" : "FAIL"}  ${check.name}`);
    if (check.details) lines.push(`      ${check.details}`);
  }
  lines.push("");
  if (report.accounts) {
    for (const [label, account] of Object.entries(report.accounts)) {
      lines.push(
        `${label}: rev=${account.saveRevision}, 일반 돈=${account.gameMoney}, ` +
        `다음 날=${account.nextDay}, 해금=${account.unlockedFillingIds.join(",")}, ` +
        `선택=${account.selectedFillingIds.join(",") || "없음"}, ` +
        `황금 틀=${inventoryQuantity(account.premiumInventory, "golden-pan")}, ` +
        `장착=${account.moldSkin ?? "없음"}, 구매 내역=${account.purchaseCount}`
      );
    }
    lines.push("");
  }
  lines.push(
    `${report.ok ? "전체 통과" : "실패"}: ` +
    `${report.checks.filter((check) => check.status === "pass").length}/${report.checks.length} ` +
    `(${report.durationMs}ms)`
  );
  return lines.join("\n");
}

const entryPath = process.argv[1]
  ? pathToFileURL(path.resolve(process.argv[1])).href
  : undefined;

if (entryPath === import.meta.url) {
  const report = await runAccountScopeSmoke();
  const useJson = process.argv.includes("--json");
  process.stdout.write(`${useJson ? JSON.stringify(report, null, 2) : renderHumanReport(report)}\n`);
  if (!report.ok) process.exitCode = 1;
}

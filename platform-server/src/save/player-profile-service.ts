import type {
  PlayerProgress,
  PlayerProgressCustomerId,
  PlayerProgressStore,
  StoryProgressInput
} from "../progress/store.js";
import { playerProgressCustomerIds } from "../progress/store.js";
import {
  SaveRevisionConflictError,
  type PlayerSaveProfile,
  type PlayerSaveStore
} from "./save-store.js";

export const currentSchemaVersion = 8;
const progressMigrationId = "progress-v1";
const requiredDefaultFillings = ["red-bean"];
const validCustomerIds = new Set<string>(playerProgressCustomerIds);
const validSoulIdPattern = /^soul:(red-bean|custard|nutella|cream-cheese|pizza|mint|sweet-potato|green-tea):(soft|perfect|crisp)$/;
const serverAchievementDefinitions = [
  { achievementId: "first-sale", target: 1, progress: (profile: PlayerSaveProfile) => lifetimeStat(profile, "totalSales") },
  { achievementId: "sales-50", target: 50, progress: (profile: PlayerSaveProfile) => lifetimeStat(profile, "totalSales") },
  { achievementId: "customers-30", target: 30, progress: (profile: PlayerSaveProfile) => lifetimeStat(profile, "totalCustomers") },
  { achievementId: "revenue-50000", target: 50_000, progress: (profile: PlayerSaveProfile) => lifetimeStat(profile, "totalRevenue") },
  { achievementId: "daily-profit-10000", target: 10_000, progress: (profile: PlayerSaveProfile) => lifetimeStat(profile, "bestDailyProfit") },
  {
    achievementId: "meet-all-customers",
    target: 8,
    progress: (profile: PlayerSaveProfile) => Math.min(
      lifetimeStat(profile, "totalCustomers"),
      normalizedCustomers(record(profile.account).customers)
        .filter((customer) => customer.met === true).length
    )
  },
  {
    achievementId: "first-story",
    target: 1,
    progress: (profile: PlayerSaveProfile) => Math.min(
      Math.floor(lifetimeStat(profile, "totalCustomers") / 3),
      normalizedCustomers(record(profile.account).customers)
        .filter((customer) => customer.storyCompleted === true).length
    )
  },
  {
    achievementId: "soul-collector-8",
    target: 8,
    progress: (profile: PlayerSaveProfile) => Math.min(
      lifetimeStat(profile, "totalSales"),
      normalizedSouls(record(profile.account).discoveredSouls).length
    )
  }
] as const;

type JsonRecord = Record<string, unknown>;

function record(value: unknown): JsonRecord {
  return value && typeof value === "object" && !Array.isArray(value)
    ? value as JsonRecord
    : {};
}

function hasCompleteSettings(value: unknown): boolean {
  const settings = record(value);
  return typeof settings.masterVolume === "number" &&
    typeof settings.keyboardHintsEnabled === "boolean" &&
    typeof settings.tutorialCompleted === "boolean";
}

function stringArray(value: unknown): string[] {
  return Array.isArray(value)
    ? [...new Set(value.filter((entry): entry is string => typeof entry === "string" && entry.length > 0))]
    : [];
}

function numberOr(value: unknown, fallback: number): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function normalizedQueuedDayEffects(value: unknown): JsonRecord[] {
  const byKey = new Map<string, JsonRecord>();
  for (const raw of Array.isArray(value) ? value : []) {
    const effect = record(raw);
    const productId = String(effect.productId ?? "");
    const effectCode = String(effect.effectCode ?? "");
    const targetDay = Math.max(1, Math.trunc(numberOr(effect.targetDay, 1)));
    if (!productId || !effectCode) continue;
    const key = `${productId}:${targetDay}`;
    byKey.set(key, {
      productId,
      effectCode,
      targetDay,
      durationSeconds: Math.max(0, numberOr(effect.durationSeconds, 0)),
      multiplier: Math.min(1, Math.max(0.01, numberOr(effect.multiplier, 1)))
    });
  }
  return [...byKey.values()].sort((left, right) =>
    numberOr(left.targetDay, 0) - numberOr(right.targetDay, 0) ||
    String(left.productId).localeCompare(String(right.productId))
  );
}

function canonicalCustomerId(value: unknown): string {
  const canonical = value === "jeonghyun" ? "jeonghyeon" : String(value ?? "");
  return validCustomerIds.has(canonical) ? canonical : "";
}

function normalizedSouls(value: unknown): JsonRecord[] {
  const byId = new Map<string, JsonRecord>();
  for (const raw of Array.isArray(value) ? value : []) {
    const soul = structuredClone(record(raw));
    const soulId = String(soul.soulId ?? "").trim();
    if (!validSoulIdPattern.test(soulId) || byId.has(soulId)) continue;
    soul.soulId = soulId;
    byId.set(soulId, soul);
  }
  return [...byId.values()].sort((left, right) =>
    String(left.soulId).localeCompare(String(right.soulId))
  );
}

function normalizedLifetimeStats(value: unknown): JsonRecord {
  const stats = record(value);
  return {
    totalSales: Math.max(0, Math.trunc(numberOr(stats.totalSales, 0))),
    totalCustomers: Math.max(0, Math.trunc(numberOr(stats.totalCustomers, 0))),
    totalRevenue: Math.max(0, Math.trunc(numberOr(stats.totalRevenue, 0))),
    bestDailyProfit: Math.trunc(numberOr(stats.bestDailyProfit, 0))
  };
}

function lifetimeStat(profile: PlayerSaveProfile, field: string): number {
  return numberOr(normalizedLifetimeStats(record(profile.account).lifetimeStats)[field], 0);
}

export function recomputeServerAchievements(
  profile: PlayerSaveProfile,
  now = new Date().toISOString()
): PlayerSaveProfile {
  const normalized = normalizePlayerSaveProfile(profile);
  const account = record(normalized.account);
  const previous = new Map(
    (Array.isArray(account.achievements) ? account.achievements : [])
      .map((entry) => record(entry))
      .filter((entry) => typeof entry.achievementId === "string")
      .map((entry) => [String(entry.achievementId), entry])
  );
  account.achievements = serverAchievementDefinitions.map((definition) => {
    const progress = Math.max(0, Math.trunc(definition.progress(normalized)));
    const unlocked = progress >= definition.target;
    const old = previous.get(definition.achievementId);
    return {
      achievementId: definition.achievementId,
      progress,
      unlocked,
      unlockedAt: unlocked
        ? typeof old?.unlockedAt === "string" && old.unlockedAt ? old.unlockedAt : now
        : ""
    };
  });
  normalized.account = account;
  return normalized;
}

function mergeCustomer(target: JsonRecord, source: JsonRecord): void {
  target.met = target.met === true || source.met === true;
  target.visitCount = Math.max(numberOr(target.visitCount, 0), numberOr(source.visitCount, 0));
  target.lastTalkDay = Math.max(numberOr(target.lastTalkDay, -1), numberOr(source.lastTalkDay, -1));
  target.specialOrderDueDay = Math.max(
    numberOr(target.specialOrderDueDay, -1),
    numberOr(source.specialOrderDueDay, -1)
  );
  target.retryAvailableDay = Math.max(
    numberOr(target.retryAvailableDay, -1),
    numberOr(source.retryAvailableDay, -1)
  );
  target.storyCompleted = target.storyCompleted === true || source.storyCompleted === true;
  for (const field of [
    "completedTopicIds",
    "discoveredNormalDialogueIds",
    "attemptedSoulIds"
  ]) {
    target[field] = [...new Set([...stringArray(target[field]), ...stringArray(source[field])])];
  }
}

function normalizedCustomers(value: unknown): JsonRecord[] {
  const byId = new Map<string, JsonRecord>();
  for (const raw of Array.isArray(value) ? value : []) {
    const source = structuredClone(record(raw));
    const customerId = canonicalCustomerId(source.customerId);
    if (!customerId) continue;
    source.customerId = customerId;
    const existing = byId.get(customerId);
    if (existing) mergeCustomer(existing, source);
    else {
      source.completedTopicIds = stringArray(source.completedTopicIds);
      source.discoveredNormalDialogueIds = stringArray(source.discoveredNormalDialogueIds);
      source.attemptedSoulIds = stringArray(source.attemptedSoulIds);
      byId.set(customerId, source);
    }
  }
  return [...byId.values()].sort((left, right) =>
    String(left.customerId).localeCompare(String(right.customerId))
  );
}

export function normalizePlayerSaveProfile(profile: PlayerSaveProfile): PlayerSaveProfile {
  const normalized = structuredClone(profile);
  const sourceSchemaVersion = Number(normalized.schemaVersion) || 0;
  const run = record(normalized.run);
  run.nextDay = Math.max(1, Number(run.nextDay) || 1);
  run.money = Number.isFinite(Number(run.money)) ? Number(run.money) : 5000;
  run.unlockedFillingIds = [
    ...new Set([...requiredDefaultFillings, ...stringArray(run.unlockedFillingIds)])
  ];
  const hadSelectedFillings = Object.prototype.hasOwnProperty.call(run, "selectedFillingIds");
  run.selectedFillingIds = hadSelectedFillings
    ? stringArray(run.selectedFillingIds)
    : sourceSchemaVersion < 7 && Number(run.nextDay) <= 1
      ? [...requiredDefaultFillings]
      : [];
  run.ownedGameplayItemIds = stringArray(run.ownedGameplayItemIds);
  run.queuedDayEffects = normalizedQueuedDayEffects(run.queuedDayEffects);

  const account = record(normalized.account);
  account.customers = normalizedCustomers(account.customers);
  account.discoveredSouls = normalizedSouls(account.discoveredSouls);
  account.achievements = Array.isArray(account.achievements) ? account.achievements : [];
  account.lifetimeStats = normalizedLifetimeStats(account.lifetimeStats);
  delete account.purchasedAccountItemIds;

  const settings = record(normalized.settings);
  settings.masterVolume = Math.min(1, Math.max(0, Number(settings.masterVolume) || 0));
  if (!("masterVolume" in record(normalized.settings))) settings.masterVolume = 1;
  settings.keyboardHintsEnabled = settings.keyboardHintsEnabled !== false;
  settings.tutorialCompleted = settings.tutorialCompleted === true;

  return {
    ...normalized,
    schemaVersion: currentSchemaVersion,
    run,
    account,
    settings
  };
}

export function createDefaultPlayerSaveProfile(): PlayerSaveProfile {
  return recomputeServerAchievements(normalizePlayerSaveProfile({
    schemaVersion: currentSchemaVersion,
    revision: 0,
    updatedAt: "",
    run: {
      nextDay: 1,
      money: 5000,
      unlockedFillingIds: requiredDefaultFillings,
      selectedFillingIds: requiredDefaultFillings,
      ownedGameplayItemIds: [],
      queuedDayEffects: []
    },
    account: {
      customers: [],
      discoveredSouls: [],
      achievements: [],
      lifetimeStats: {}
    },
    settings: {
      masterVolume: 1,
      keyboardHintsEnabled: true,
      tutorialCompleted: false
    }
  }));
}

function preserveLegacySpecialOrderStates(
  nextRun: JsonRecord,
  authoritativeRun: JsonRecord
): void {
  const authoritative = new Map(
    (Array.isArray(authoritativeRun.customerStories) ? authoritativeRun.customerStories : [])
      .map((entry) => record(entry))
      .filter((entry) => typeof entry.customerId === "string")
      .map((entry) => [String(entry.customerId), entry])
  );
  if (!Array.isArray(nextRun.customerStories)) return;
  for (const raw of nextRun.customerStories) {
    const state = record(raw);
    if (Object.prototype.hasOwnProperty.call(state, "specialOrderState")) continue;
    const previous = authoritative.get(String(state.customerId ?? ""));
    if (typeof previous?.specialOrderState === "string") {
      state.specialOrderState = previous.specialOrderState;
    }
  }
}

function mergeLegacyProgress(profile: PlayerSaveProfile, progress: PlayerProgress): boolean {
  const account = record(profile.account);
  const customers = normalizedCustomers(account.customers);
  const byId = new Map(customers.map((customer) => [String(customer.customerId), customer]));
  let changed = false;

  for (const legacy of progress.customers) {
    const customerId = canonicalCustomerId(legacy.customerId);
    const target = byId.get(customerId) ?? {
      customerId,
      met: false,
      visitCount: 0,
      lastTalkDay: -1,
      completedTopicIds: [],
      discoveredNormalDialogueIds: [],
      attemptedSoulIds: [],
      specialOrderDueDay: -1,
      retryAvailableDay: -1,
      storyCompleted: false
    };
    const before = JSON.stringify(target);
    mergeCustomer(target, {
      customerId,
      met: legacy.met,
      completedTopicIds: legacy.completedTopicIndexes.map((index) => `topic-${index + 1}`),
      storyCompleted: legacy.storyCompleted
    });
    if (!byId.has(customerId)) {
      byId.set(customerId, target);
      changed = true;
    } else if (JSON.stringify(target) !== before) {
      changed = true;
    }
  }

  const migrations = stringArray(account.migrations);
  if (!migrations.includes(progressMigrationId)) {
    migrations.push(progressMigrationId);
    changed = true;
  }
  account.migrations = migrations;
  account.customers = [...byId.values()];
  profile.account = account;
  return changed;
}

function toProgress(profile: PlayerSaveProfile): PlayerProgress {
  const account = record(profile.account);
  const customers = normalizedCustomers(account.customers).map((customer) => ({
    customerId: String(customer.customerId) as PlayerProgressCustomerId,
    met: customer.met === true,
    completedTopicIndexes: stringArray(customer.completedTopicIds)
      .map((topic) => /^topic-(\d+)$/.exec(topic))
      .filter((match): match is RegExpExecArray => match !== null)
      .map((match) => Number(match[1]) - 1)
      .filter((index) => Number.isInteger(index) && index >= 0 && index <= 63)
      .sort((left, right) => left - right),
    storyCompleted: customer.storyCompleted === true
  }));
  return { schemaVersion: 1, customers };
}

export class PlayerProfileService {
  public constructor(
    private readonly saves: PlayerSaveStore,
    private readonly legacyProgress: PlayerProgressStore
  ) {}

  public async get(subject: string): Promise<PlayerSaveProfile | undefined> {
    for (let attempt = 0; attempt < 4; attempt++) {
      const current = await this.saves.get(subject);
      if (!current) return undefined;
      const normalized = normalizePlayerSaveProfile(current);
      // v2 clients need to see the old schema once so they can migrate local-only
      // PlayerPrefs into the new account settings. The first v3 PUT finalizes it.
      const needsClientSettingsMigration =
        current.schemaVersion < currentSchemaVersion && !hasCompleteSettings(current.settings);
      if (needsClientSettingsMigration) {
        normalized.schemaVersion = current.schemaVersion;
        normalized.settings = current.settings;
      }
      const legacy = await this.legacyProgress.getPlayerProgress(subject);
      const legacyChanged = mergeLegacyProgress(normalized, legacy);
      const changed = legacyChanged || JSON.stringify(current) !== JSON.stringify(normalized);
      if (!changed) return normalized;
      try {
        return await this.saves.put(subject, current.revision, normalized);
      } catch (error) {
        if (!(error instanceof SaveRevisionConflictError)) throw error;
      }
    }
    throw new SaveRevisionConflictError(await this.saves.get(subject));
  }

  public async put(
    subject: string,
    expectedRevision: number,
    profile: PlayerSaveProfile
  ): Promise<PlayerSaveProfile> {
    const normalized = normalizePlayerSaveProfile(profile);
    const current = await this.saves.get(subject);
    if ((current?.revision ?? 0) !== expectedRevision) {
      throw new SaveRevisionConflictError(current);
    }
    const authoritativeRun = record(normalizePlayerSaveProfile(
      current ?? createDefaultPlayerSaveProfile()
    ).run);
    const nextRun = record(normalized.run);
    preserveLegacySpecialOrderStates(nextRun, authoritativeRun);
    // 일반 돈과 일반 상점 보유 상태는 첫 저장을 포함해 전용 트랜잭션 API만 변경한다.
    for (const protectedField of [
      "nextDay",
      "money",
      "unlockedFillingIds",
      "selectedFillingIds",
      "ownedGameplayItemIds",
      "queuedDayEffects",
      "activeDay"
    ]) {
      nextRun[protectedField] = structuredClone(authoritativeRun[protectedField]);
    }
    normalized.run = nextRun;
    const authoritativeAccount = record(normalizePlayerSaveProfile(
      current ?? createDefaultPlayerSaveProfile()
    ).account);
    const submittedAccount = record(normalized.account);
    authoritativeAccount.customers = normalizedCustomers([
      ...normalizedCustomers(authoritativeAccount.customers),
      ...normalizedCustomers(submittedAccount.customers)
    ]);
    authoritativeAccount.discoveredSouls = normalizedSouls([
      ...normalizedSouls(authoritativeAccount.discoveredSouls),
      ...normalizedSouls(submittedAccount.discoveredSouls)
    ]);
    // 누적 통계와 업적은 정산/서버 규칙으로만 변경한다. 클라이언트는 표시용 사본을
    // 보내더라도 기존 서버 값을 덮어쓸 수 없다.
    authoritativeAccount.lifetimeStats = normalizedLifetimeStats(
      authoritativeAccount.lifetimeStats
    );
    normalized.account = authoritativeAccount;
    const serverDerived = recomputeServerAchievements(normalized);
    const legacy = await this.legacyProgress.getPlayerProgress(subject);
    mergeLegacyProgress(serverDerived, legacy);
    return this.saves.put(
      subject,
      expectedRevision,
      recomputeServerAchievements(serverDerived)
    );
  }

  public async getProgress(subject: string): Promise<PlayerProgress> {
    const profile = await this.get(subject);
    if (profile) return toProgress(profile);
    return this.legacyProgress.getPlayerProgress(subject);
  }

  public async markCustomerMet(
    subject: string,
    customerId: PlayerProgressCustomerId
  ): Promise<PlayerProgress> {
    return this.updateProgress(subject, customerId, (customer) => {
      customer.met = true;
    });
  }

  public async mergeStoryProgress(
    subject: string,
    customerId: PlayerProgressCustomerId,
    input: StoryProgressInput
  ): Promise<PlayerProgress> {
    return this.updateProgress(subject, customerId, (customer) => {
      customer.met = true;
      customer.storyCompleted = customer.storyCompleted === true || input.storyCompleted;
      customer.completedTopicIds = [
        ...new Set([
          ...stringArray(customer.completedTopicIds),
          ...input.completedTopicIndexes.map((index) => `topic-${index + 1}`)
        ])
      ];
    });
  }

  private async updateProgress(
    subject: string,
    customerId: PlayerProgressCustomerId,
    mutate: (customer: JsonRecord) => void
  ): Promise<PlayerProgress> {
    for (let attempt = 0; attempt < 5; attempt++) {
      const current = await this.saves.get(subject);
      const profile = normalizePlayerSaveProfile(current ?? createDefaultPlayerSaveProfile());
      mergeLegacyProgress(profile, await this.legacyProgress.getPlayerProgress(subject));
      const account = record(profile.account);
      const customers = normalizedCustomers(account.customers);
      let customer = customers.find((entry) => entry.customerId === customerId);
      if (!customer) {
        customer = { customerId, completedTopicIds: [] };
        customers.push(customer);
      }
      mutate(customer);
      account.customers = customers;
      profile.account = account;
      try {
        return toProgress(await this.saves.put(subject, current?.revision ?? 0, profile));
      } catch (error) {
        if (!(error instanceof SaveRevisionConflictError)) throw error;
      }
    }
    throw new SaveRevisionConflictError(await this.saves.get(subject));
  }
}

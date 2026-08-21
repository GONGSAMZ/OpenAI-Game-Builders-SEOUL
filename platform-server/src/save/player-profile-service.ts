import type {
  PlayerProgress,
  PlayerProgressCustomerId,
  PlayerProgressStore,
  StoryProgressInput
} from "../progress/store.js";
import {
  SaveRevisionConflictError,
  type PlayerSaveProfile,
  type PlayerSaveStore
} from "./save-store.js";

const currentSchemaVersion = 4;
const progressMigrationId = "progress-v1";
const defaultFillings = ["red-bean", "custard", "nutella", "cream-cheese"];

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

function canonicalCustomerId(value: unknown): string {
  return value === "jeonghyun" ? "jeonghyeon" : String(value ?? "");
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
  const run = record(normalized.run);
  run.nextDay = Math.max(1, Number(run.nextDay) || 1);
  run.money = Number.isFinite(Number(run.money)) ? Number(run.money) : 5000;
  run.unlockedFillingIds = [
    ...new Set([...defaultFillings, ...stringArray(run.unlockedFillingIds)])
  ];
  run.ownedGameplayItemIds = stringArray(run.ownedGameplayItemIds);

  const account = record(normalized.account);
  account.customers = normalizedCustomers(account.customers);
  account.discoveredSouls = Array.isArray(account.discoveredSouls) ? account.discoveredSouls : [];
  account.achievements = Array.isArray(account.achievements) ? account.achievements : [];
  account.lifetimeStats = record(account.lifetimeStats);
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

function defaultProfile(): PlayerSaveProfile {
  return normalizePlayerSaveProfile({
    schemaVersion: currentSchemaVersion,
    revision: 0,
    updatedAt: "",
    run: {
      nextDay: 1,
      money: 5000,
      unlockedFillingIds: defaultFillings,
      ownedGameplayItemIds: []
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
  });
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
    const legacy = await this.legacyProgress.getPlayerProgress(subject);
    mergeLegacyProgress(normalized, legacy);
    return this.saves.put(subject, expectedRevision, normalized);
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
      const profile = normalizePlayerSaveProfile(current ?? defaultProfile());
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

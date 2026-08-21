using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SaveGameData
{
    public int schemaVersion = SaveDataFactory.CurrentSchemaVersion;
    public long revision;
    public string updatedAt = string.Empty;
    public RunProgressData run = new();
    public AccountProgressData account = new();
    public UserSettingsData settings = new();
}

[Serializable]
public sealed class RunProgressData
{
    public int nextDay = 1;
    public int money = SaveDataFactory.InitialMoney;
    public List<string> unlockedFillingIds = new();
    // 날짜 기반 이야기 상태는 새 게임마다 함께 초기화된다.
    public List<CustomerStoryRunState> customerStories = new();
    // 서버 상점 인벤토리로 완전히 이전되기 전까지 기존 저장을 읽기 위한 호환 필드다.
    public List<string> ownedGameplayItemIds = new();
}

[Serializable]
public sealed class AccountProgressData
{
    public List<CustomerProgressData> customers = new();
    public List<SoulDiscoveryData> discoveredSouls = new();
    public List<AchievementProgressData> achievements = new();
    public LifetimeStatsData lifetimeStats = new();
}

[Serializable]
public sealed class UserSettingsData
{
    public float masterVolume = 1f;
    public bool keyboardHintsEnabled = true;
    public bool tutorialCompleted;
}

[Serializable]
public sealed class CustomerProgressData
{
    public string customerId;
    public bool met;
    public int visitCount;
    public int lastTalkDay = -1;
    public List<string> completedTopicIds = new();
    public List<string> discoveredNormalDialogueIds = new();
    public List<string> attemptedSoulIds = new();
    public int specialOrderDueDay = -1;
    public int retryAvailableDay = -1;
    public bool storyCompleted;
}

[Serializable]
public sealed class CustomerStoryRunState
{
    public string customerId;
    public int lastTalkDay = -1;
    public int nextSpecialOrderDay = -1;
}

public static class CustomerStorySchedule
{
    public static int RetryDayAfterFailure(int currentDay) => Mathf.Max(1, currentDay) + 2;

    // 예정일을 놓쳐도 특별 주문이 영구히 사라지지 않게 한다.
    public static bool IsOrderDue(int nextSpecialOrderDay, int currentDay) =>
        nextSpecialOrderDay > 0 && currentDay >= nextSpecialOrderDay;
}

[Serializable]
public sealed class SoulDiscoveryData
{
    public string soulId;
    public string fillingId;
    public string bakeStateId;
    public int firstDiscoveredDay;
    public string linkedCustomerId;
}

[Serializable]
public sealed class AchievementProgressData
{
    public string achievementId;
    public long progress;
    public bool unlocked;
    public string unlockedAt = string.Empty;
}

[Serializable]
public sealed class LifetimeStatsData
{
    public long totalSales;
    public long totalCustomers;
    public long totalRevenue;
    public int bestDailyProfit;
}

public static class SaveIds
{
    public static string Customer(CustomerType type) => type switch
    {
        CustomerType.JeongHyun => "jeonghyeon",
        CustomerType.HaYoung => "hajin",
        CustomerType.MiJu => "miju",
        CustomerType.Sunja => "sunja",
        CustomerType.Geonwoo => "geonwoo",
        CustomerType.Taesu => "taesu",
        CustomerType.Nari => "nari",
        CustomerType.Junho => "junho",
        _ => type.ToString().ToLowerInvariant(),
    };

    public static string Filling(FillingType type) => type switch
    {
        FillingType.redBean => "red-bean",
        FillingType.custard => "custard",
        FillingType.nutella => "nutella",
        FillingType.creamCheese => "cream-cheese",
        FillingType.pizza => "pizza",
        FillingType.mint => "mint",
        FillingType.sweetPotato => "sweet-potato",
        FillingType.greenTea => "green-tea",
        _ => type.ToString(),
    };

    public static string Bake(QualityStatus status) => status switch
    {
        QualityStatus.soft => "soft",
        QualityStatus.perfect => "perfect",
        QualityStatus.crisp => "crisp",
        QualityStatus.insufficient => "insufficient",
        QualityStatus.excessive => "excessive",
        _ => "none",
    };

    public static string Topic(int index) => $"topic-{index + 1}";
    public static string Soul(FillingType filling, QualityStatus bake) => $"soul:{Filling(filling)}:{Bake(bake)}";
}

public static class SaveDataFactory
{
    public const int CurrentSchemaVersion = 4;
    public const int InitialMoney = 5000;
    public const string LegacyVolumeKey = "settings_master_volume_v1";
    public const string LegacyKeyboardHintsKey = "settings_keyboard_hints_enabled_v1";
    public const string LegacyTutorialCompletedKey = "tutorial_completed_v1";

    private static readonly string[] DefaultFillingIds =
    {
        "red-bean", "custard", "nutella", "cream-cheese"
    };

    public static SaveGameData CreateDefault()
    {
        SaveGameData data = new();
        ResetRun(data);
        Normalize(data);
        return data;
    }

    public static void ResetRun(SaveGameData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        data.run = new RunProgressData
        {
            nextDay = 1,
            money = InitialMoney,
            unlockedFillingIds = new List<string>(DefaultFillingIds),
            customerStories = new List<CustomerStoryRunState>(),
            ownedGameplayItemIds = new List<string>()
        };
    }

    public static void Normalize(SaveGameData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        int sourceSchemaVersion = data.schemaVersion;
        data.schemaVersion = CurrentSchemaVersion;
        data.updatedAt ??= string.Empty;
        data.run ??= new RunProgressData();
        data.run.nextDay = Mathf.Max(1, data.run.nextDay);
        data.run.unlockedFillingIds ??= new List<string>();
        data.run.customerStories ??= new List<CustomerStoryRunState>();
        data.run.ownedGameplayItemIds ??= new List<string>();
        foreach (string id in DefaultFillingIds)
            if (!data.run.unlockedFillingIds.Contains(id)) data.run.unlockedFillingIds.Add(id);
        NormalizeStoryStates(data.run.customerStories);

        data.account ??= new AccountProgressData();
        data.account.customers ??= new List<CustomerProgressData>();
        data.account.discoveredSouls ??= new List<SoulDiscoveryData>();
        data.account.achievements ??= new List<AchievementProgressData>();
        data.account.lifetimeStats ??= new LifetimeStatsData();

        foreach (CustomerProgressData customer in data.account.customers)
        {
            if (customer == null) continue;
            customer.completedTopicIds ??= new List<string>();
            customer.discoveredNormalDialogueIds ??= new List<string>();
            customer.attemptedSoulIds ??= new List<string>();
        }
        NormalizeCustomerIds(data.account.customers);
        if (sourceSchemaVersion < 4 || HasLegacyStoryDates(data.account.customers))
            MigrateLegacyStoryDatesToRun(data);

        data.settings ??= new UserSettingsData();
        if (sourceSchemaVersion < 3)
        {
            data.settings.masterVolume = PlayerPrefs.GetFloat(LegacyVolumeKey, data.settings.masterVolume);
            data.settings.keyboardHintsEnabled = PlayerPrefs.GetInt(LegacyKeyboardHintsKey, data.settings.keyboardHintsEnabled ? 1 : 0) == 1;
            data.settings.tutorialCompleted = PlayerPrefs.GetInt(LegacyTutorialCompletedKey, data.settings.tutorialCompleted ? 1 : 0) == 1;
        }
        data.settings.masterVolume = Mathf.Clamp01(data.settings.masterVolume);

        AchievementCatalog.EnsureEntries(data);
    }

    private static void NormalizeCustomerIds(List<CustomerProgressData> customers)
    {
        CustomerProgressData canonical = customers.Find(value => value.customerId == "jeonghyeon");
        for (int index = customers.Count - 1; index >= 0; index--)
        {
            CustomerProgressData candidate = customers[index];
            if (candidate == null || candidate.customerId != "jeonghyun") continue;
            if (canonical == null)
            {
                candidate.customerId = "jeonghyeon";
                canonical = candidate;
                continue;
            }

            MergeCustomer(canonical, candidate);
            customers.RemoveAt(index);
        }
    }

    public static CustomerStoryRunState FindOrCreateCustomerStoryState(
        SaveGameData data,
        CustomerType customerType)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        data.run ??= new RunProgressData();
        data.run.customerStories ??= new List<CustomerStoryRunState>();

        string customerId = SaveIds.Customer(customerType);
        CustomerStoryRunState state = data.run.customerStories.Find(value => value.customerId == customerId);
        if (state != null) return state;

        state = new CustomerStoryRunState { customerId = customerId };
        data.run.customerStories.Add(state);
        return state;
    }

    private static void MigrateLegacyStoryDatesToRun(SaveGameData data)
    {
        foreach (CustomerProgressData customer in data.account.customers)
        {
            if (customer == null || string.IsNullOrWhiteSpace(customer.customerId)) continue;
            if (customer.lastTalkDay < 0 && customer.specialOrderDueDay < 0 && customer.retryAvailableDay < 0)
                continue;

            CustomerStoryRunState state = data.run.customerStories.Find(value => value.customerId == customer.customerId);
            if (state == null)
            {
                state = new CustomerStoryRunState { customerId = customer.customerId };
                data.run.customerStories.Add(state);
            }

            state.lastTalkDay = Mathf.Max(state.lastTalkDay, customer.lastTalkDay);
            int legacyNextOrderDay = customer.specialOrderDueDay > 0
                ? customer.specialOrderDueDay
                : customer.retryAvailableDay;
            state.nextSpecialOrderDay = Mathf.Max(state.nextSpecialOrderDay, legacyNextOrderDay);
            customer.lastTalkDay = -1;
            customer.specialOrderDueDay = -1;
            customer.retryAvailableDay = -1;
        }
        NormalizeStoryStates(data.run.customerStories);
    }

    private static bool HasLegacyStoryDates(List<CustomerProgressData> customers)
    {
        foreach (CustomerProgressData customer in customers)
        {
            if (customer != null &&
                (customer.lastTalkDay >= 0 || customer.specialOrderDueDay >= 0 || customer.retryAvailableDay >= 0))
                return true;
        }
        return false;
    }

    private static void NormalizeStoryStates(List<CustomerStoryRunState> states)
    {
        for (int index = states.Count - 1; index >= 0; index--)
        {
            CustomerStoryRunState state = states[index];
            if (state == null || string.IsNullOrWhiteSpace(state.customerId))
            {
                states.RemoveAt(index);
                continue;
            }

            if (state.customerId == "jeonghyun") state.customerId = "jeonghyeon";
            state.lastTalkDay = Mathf.Max(-1, state.lastTalkDay);
            state.nextSpecialOrderDay = Mathf.Max(-1, state.nextSpecialOrderDay);
            int firstIndex = states.FindIndex(value => value != null && value != state && value.customerId == state.customerId);
            if (firstIndex < 0) continue;

            CustomerStoryRunState target = states[firstIndex];
            target.lastTalkDay = Mathf.Max(target.lastTalkDay, state.lastTalkDay);
            target.nextSpecialOrderDay = Mathf.Max(target.nextSpecialOrderDay, state.nextSpecialOrderDay);
            states.RemoveAt(index);
        }
    }

    private static void MergeCustomer(CustomerProgressData target, CustomerProgressData source)
    {
        target.completedTopicIds ??= new List<string>();
        target.discoveredNormalDialogueIds ??= new List<string>();
        target.attemptedSoulIds ??= new List<string>();
        target.met |= source.met;
        target.visitCount = Math.Max(target.visitCount, source.visitCount);
        target.lastTalkDay = Math.Max(target.lastTalkDay, source.lastTalkDay);
        target.specialOrderDueDay = Math.Max(target.specialOrderDueDay, source.specialOrderDueDay);
        target.retryAvailableDay = Math.Max(target.retryAvailableDay, source.retryAvailableDay);
        target.storyCompleted |= source.storyCompleted;
        MergeUnique(target.completedTopicIds, source.completedTopicIds);
        MergeUnique(target.discoveredNormalDialogueIds, source.discoveredNormalDialogueIds);
        MergeUnique(target.attemptedSoulIds, source.attemptedSoulIds);
    }

    /// <summary>
    /// 동시 저장 충돌에서는 영업 회차(run)는 서버 값을 기본으로 유지하고,
    /// 계정 해금·수집·업적은 합쳐서 어느 한쪽의 진행도가 사라지지 않게 한다.
    /// 새 게임 버튼처럼 사용자의 의도가 명확한 경우에만 로컬 회차를 우선한다.
    /// </summary>
    public static SaveGameData MergeAfterRemoteConflict(
        SaveGameData remote,
        SaveGameData local,
        bool preferLocalRun = false)
    {
        if (remote == null) throw new ArgumentNullException(nameof(remote));
        if (local == null) throw new ArgumentNullException(nameof(local));

        SaveGameData merged = Clone(remote);
        SaveGameData localCopy = Clone(local);
        if (preferLocalRun)
            merged.run = CopyRun(localCopy.run);

        MergeAccount(merged.account, localCopy.account);
        merged.settings = new UserSettingsData
        {
            masterVolume = localCopy.settings.masterVolume,
            keyboardHintsEnabled = localCopy.settings.keyboardHintsEnabled,
            tutorialCompleted = localCopy.settings.tutorialCompleted
        };
        merged.revision = remote.revision;
        merged.updatedAt = remote.updatedAt;
        Normalize(merged);
        return merged;
    }

    private static RunProgressData CopyRun(RunProgressData source) => new()
    {
        nextDay = source.nextDay,
        money = source.money,
        unlockedFillingIds = new List<string>(source.unlockedFillingIds ?? new List<string>()),
        customerStories = CopyStoryStates(source.customerStories),
        ownedGameplayItemIds = new List<string>(source.ownedGameplayItemIds ?? new List<string>())
    };

    private static List<CustomerStoryRunState> CopyStoryStates(List<CustomerStoryRunState> source)
    {
        List<CustomerStoryRunState> copied = new();
        if (source == null) return copied;
        foreach (CustomerStoryRunState state in source)
        {
            if (state == null) continue;
            copied.Add(new CustomerStoryRunState
            {
                customerId = state.customerId,
                lastTalkDay = state.lastTalkDay,
                nextSpecialOrderDay = state.nextSpecialOrderDay
            });
        }
        return copied;
    }

    private static void MergeAccount(AccountProgressData target, AccountProgressData source)
    {
        target.customers ??= new List<CustomerProgressData>();
        source.customers ??= new List<CustomerProgressData>();
        foreach (CustomerProgressData localCustomer in source.customers)
        {
            if (localCustomer == null || string.IsNullOrWhiteSpace(localCustomer.customerId)) continue;
            CustomerProgressData remoteCustomer = target.customers.Find(
                value => value != null && value.customerId == localCustomer.customerId);
            if (remoteCustomer == null)
            {
                target.customers.Add(CloneCustomer(localCustomer));
                continue;
            }
            MergeCustomer(remoteCustomer, localCustomer);
        }

        target.discoveredSouls ??= new List<SoulDiscoveryData>();
        source.discoveredSouls ??= new List<SoulDiscoveryData>();
        foreach (SoulDiscoveryData localSoul in source.discoveredSouls)
        {
            if (localSoul == null || string.IsNullOrWhiteSpace(localSoul.soulId)) continue;
            SoulDiscoveryData remoteSoul = target.discoveredSouls.Find(value => value?.soulId == localSoul.soulId);
            if (remoteSoul == null)
            {
                target.discoveredSouls.Add(CloneSoul(localSoul));
                continue;
            }
            if (string.IsNullOrEmpty(remoteSoul.fillingId)) remoteSoul.fillingId = localSoul.fillingId;
            if (string.IsNullOrEmpty(remoteSoul.bakeStateId)) remoteSoul.bakeStateId = localSoul.bakeStateId;
            if (string.IsNullOrEmpty(remoteSoul.linkedCustomerId)) remoteSoul.linkedCustomerId = localSoul.linkedCustomerId;
            if (remoteSoul.firstDiscoveredDay <= 0 ||
                (localSoul.firstDiscoveredDay > 0 && localSoul.firstDiscoveredDay < remoteSoul.firstDiscoveredDay))
                remoteSoul.firstDiscoveredDay = localSoul.firstDiscoveredDay;
        }

        target.achievements ??= new List<AchievementProgressData>();
        source.achievements ??= new List<AchievementProgressData>();
        foreach (AchievementProgressData localAchievement in source.achievements)
        {
            if (localAchievement == null || string.IsNullOrWhiteSpace(localAchievement.achievementId)) continue;
            AchievementProgressData remoteAchievement = target.achievements.Find(
                value => value?.achievementId == localAchievement.achievementId);
            if (remoteAchievement == null)
            {
                target.achievements.Add(CloneAchievement(localAchievement));
                continue;
            }
            remoteAchievement.progress = Math.Max(remoteAchievement.progress, localAchievement.progress);
            remoteAchievement.unlocked |= localAchievement.unlocked;
            if (string.IsNullOrEmpty(remoteAchievement.unlockedAt)) remoteAchievement.unlockedAt = localAchievement.unlockedAt;
        }

        target.lifetimeStats ??= new LifetimeStatsData();
        source.lifetimeStats ??= new LifetimeStatsData();
        target.lifetimeStats.totalSales = Math.Max(target.lifetimeStats.totalSales, source.lifetimeStats.totalSales);
        target.lifetimeStats.totalCustomers = Math.Max(target.lifetimeStats.totalCustomers, source.lifetimeStats.totalCustomers);
        target.lifetimeStats.totalRevenue = Math.Max(target.lifetimeStats.totalRevenue, source.lifetimeStats.totalRevenue);
        target.lifetimeStats.bestDailyProfit = Math.Max(target.lifetimeStats.bestDailyProfit, source.lifetimeStats.bestDailyProfit);
    }

    private static CustomerProgressData CloneCustomer(CustomerProgressData source) => new()
    {
        customerId = source.customerId,
        met = source.met,
        visitCount = source.visitCount,
        lastTalkDay = source.lastTalkDay,
        completedTopicIds = new List<string>(source.completedTopicIds ?? new List<string>()),
        discoveredNormalDialogueIds = new List<string>(source.discoveredNormalDialogueIds ?? new List<string>()),
        attemptedSoulIds = new List<string>(source.attemptedSoulIds ?? new List<string>()),
        specialOrderDueDay = source.specialOrderDueDay,
        retryAvailableDay = source.retryAvailableDay,
        storyCompleted = source.storyCompleted
    };

    private static SoulDiscoveryData CloneSoul(SoulDiscoveryData source) => new()
    {
        soulId = source.soulId,
        fillingId = source.fillingId,
        bakeStateId = source.bakeStateId,
        firstDiscoveredDay = source.firstDiscoveredDay,
        linkedCustomerId = source.linkedCustomerId
    };

    private static AchievementProgressData CloneAchievement(AchievementProgressData source) => new()
    {
        achievementId = source.achievementId,
        progress = source.progress,
        unlocked = source.unlocked,
        unlockedAt = source.unlockedAt
    };

    private static void MergeUnique(List<string> target, List<string> source)
    {
        if (source == null) return;
        foreach (string value in source)
            if (!string.IsNullOrWhiteSpace(value) && !target.Contains(value)) target.Add(value);
    }

    public static SaveGameData Clone(SaveGameData source)
    {
        SaveGameData clone = JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(source));
        Normalize(clone);
        return clone;
    }
}

public sealed class AchievementDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public long Target { get; }

    public AchievementDefinition(string id, string displayName, string description, long target)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        Target = target;
    }
}

public static class AchievementCatalog
{
    public static readonly AchievementDefinition[] Entries =
    {
        new("first-sale", "첫 붕어빵", "정상 판매 1회", 1),
        new("sales-50", "겨울 단골집", "누적 판매 50개", 50),
        new("customers-30", "어서 오세요", "누적 손님 30명", 30),
        new("revenue-50000", "차곡차곡", "누적 매출 50,000원", 50000),
        new("daily-profit-10000", "오늘 장사 성공", "하루 순이익 10,000원", 10000),
        new("meet-all-customers", "골목의 이웃들", "손님 8명 모두 만나기", 8),
        new("first-story", "마음을 듣는 가게", "손님 이야기 1개 해금", 1),
        new("soul-collector-8", "붕어빵 영혼 수집가", "영혼 8종 발견", 8),
    };

    public static void EnsureEntries(SaveGameData data)
    {
        foreach (AchievementDefinition definition in Entries)
        {
            if (data.account.achievements.Exists(value => value.achievementId == definition.Id)) continue;
            data.account.achievements.Add(new AchievementProgressData { achievementId = definition.Id });
        }
    }

    public static void Evaluate(SaveGameData data)
    {
        EnsureEntries(data);
        LifetimeStatsData stats = data.account.lifetimeStats;
        int metCustomers = data.account.customers.FindAll(value => value.met).Count;
        int completedStories = data.account.customers.FindAll(value => value.storyCompleted).Count;

        Set(data, "first-sale", stats.totalSales);
        Set(data, "sales-50", stats.totalSales);
        Set(data, "customers-30", stats.totalCustomers);
        Set(data, "revenue-50000", stats.totalRevenue);
        Set(data, "daily-profit-10000", stats.bestDailyProfit);
        Set(data, "meet-all-customers", metCustomers);
        Set(data, "first-story", completedStories);
        Set(data, "soul-collector-8", data.account.discoveredSouls.Count);
    }

    private static void Set(SaveGameData data, string id, long progress)
    {
        AchievementDefinition definition = Array.Find(Entries, value => value.Id == id);
        AchievementProgressData state = data.account.achievements.Find(value => value.achievementId == id);
        if (definition == null || state == null) return;
        state.progress = Math.Max(state.progress, progress);
        if (state.unlocked || state.progress < definition.Target) return;
        state.unlocked = true;
        state.unlockedAt = DateTime.UtcNow.ToString("O");
    }
}

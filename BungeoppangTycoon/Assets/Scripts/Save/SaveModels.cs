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
}

[Serializable]
public sealed class RunProgressData
{
    public int nextDay = 1;
    public int money = SaveDataFactory.InitialMoney;
    public List<string> unlockedFillingIds = new();
    public List<string> ownedGameplayItemIds = new();
}

[Serializable]
public sealed class AccountProgressData
{
    public List<CustomerProgressData> customers = new();
    public List<SoulDiscoveryData> discoveredSouls = new();
    public List<AchievementProgressData> achievements = new();
    public LifetimeStatsData lifetimeStats = new();
    public List<string> purchasedAccountItemIds = new();
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
        CustomerType.JeongHyun => "jeonghyun",
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
    public const int CurrentSchemaVersion = 2;
    public const int InitialMoney = 5000;

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
            ownedGameplayItemIds = new List<string>()
        };
    }

    public static void Normalize(SaveGameData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        data.schemaVersion = CurrentSchemaVersion;
        data.updatedAt ??= string.Empty;
        data.run ??= new RunProgressData();
        data.run.nextDay = Mathf.Max(1, data.run.nextDay);
        data.run.unlockedFillingIds ??= new List<string>();
        data.run.ownedGameplayItemIds ??= new List<string>();
        foreach (string id in DefaultFillingIds)
            if (!data.run.unlockedFillingIds.Contains(id)) data.run.unlockedFillingIds.Add(id);

        data.account ??= new AccountProgressData();
        data.account.customers ??= new List<CustomerProgressData>();
        data.account.discoveredSouls ??= new List<SoulDiscoveryData>();
        data.account.achievements ??= new List<AchievementProgressData>();
        data.account.lifetimeStats ??= new LifetimeStatsData();
        data.account.purchasedAccountItemIds ??= new List<string>();

        foreach (CustomerProgressData customer in data.account.customers)
        {
            customer.completedTopicIds ??= new List<string>();
            customer.discoveredNormalDialogueIds ??= new List<string>();
            customer.attemptedSoulIds ??= new List<string>();
        }
        AchievementCatalog.EnsureEntries(data);
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

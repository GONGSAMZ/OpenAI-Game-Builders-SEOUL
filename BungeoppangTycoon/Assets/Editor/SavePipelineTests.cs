#if UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SavePipelineTests
{
    [Test]
    public void ResetRun_PreservesAccountProgress()
    {
        SaveGameData data = SaveDataFactory.CreateDefault();
        data.run.nextDay = 7;
        data.run.money = 12345;
        data.run.customerStories.Add(new CustomerStoryRunState
        {
            customerId = "jeonghyeon",
            lastTalkDay = 3,
            nextSpecialOrderDay = 4,
            specialOrderState = CustomerStorySchedule.Scheduled
        });
        data.account.discoveredSouls.Add(new SoulDiscoveryData { soulId = "soul:red-bean:soft" });
        data.account.lifetimeStats.totalSales = 50;

        SaveDataFactory.ResetRun(data);

        Assert.That(data.run.nextDay, Is.EqualTo(1));
        Assert.That(data.run.money, Is.EqualTo(SaveDataFactory.InitialMoney));
        Assert.That(data.run.unlockedFillingIds, Is.EqualTo(new[] { "red-bean" }));
        Assert.That(data.run.selectedFillingIds, Is.EqualTo(new[] { "red-bean" }));
        Assert.That(data.run.customerStories, Is.Empty);
        Assert.That(data.run.queuedDayEffects, Is.Empty);
        Assert.That(data.account.discoveredSouls.Count, Is.EqualTo(1));
        Assert.That(data.account.lifetimeStats.totalSales, Is.EqualTo(50));
    }

    [Test]
    public void AchievementEvaluation_UnlocksReachedTargetsOnly()
    {
        SaveGameData data = SaveDataFactory.CreateDefault();
        data.account.lifetimeStats.totalSales = 50;
        data.account.lifetimeStats.totalCustomers = 29;

        AchievementCatalog.Evaluate(data);

        Assert.That(data.account.achievements.Find(value => value.achievementId == "first-sale").unlocked, Is.True);
        Assert.That(data.account.achievements.Find(value => value.achievementId == "sales-50").unlocked, Is.True);
        Assert.That(data.account.achievements.Find(value => value.achievementId == "customers-30").unlocked, Is.False);
    }

    [Test]
    public void LocalStore_WhenActiveCopyIsCorrupted_LoadsPreviousCopy()
    {
        const string scope = "editmode_fallback_test";
        ClearScope(scope);
        PlayerPrefsLocalSaveStore store = new();
        SaveGameData first = SaveDataFactory.CreateDefault();
        first.run.money = 6000;
        store.Save(scope, first);
        SaveGameData second = SaveDataFactory.Clone(first);
        second.run.money = 9000;
        store.Save(scope, second);

        string active = PlayerPrefs.GetString($"game_save_v2_{scope}_active");
        PlayerPrefs.SetString($"game_save_v2_{scope}_{active}", "{broken-json");
        PlayerPrefs.Save();

        Assert.That(store.TryLoad(scope, out SaveGameData recovered), Is.True);
        Assert.That(recovered.run.money, Is.EqualTo(6000));
        ClearScope(scope);
    }

    [Test]
    public void LocalStore_PendingRemoteFlag_FollowsTheRecoverableSaveSlot()
    {
        const string scope = "editmode_pending_remote_test";
        ClearScope(scope);
        PlayerPrefsLocalSaveStore store = new();
        SaveGameData synced = SaveDataFactory.CreateDefault();
        synced.run.money = 6000;
        store.Save(scope, synced, false);
        SaveGameData pending = SaveDataFactory.Clone(synced);
        pending.run.money = 9000;
        store.Save(scope, pending, true);

        Assert.That(store.TryLoad(scope, out SaveGameData latest, out bool latestPending), Is.True);
        Assert.That(latest.run.money, Is.EqualTo(9000));
        Assert.That(latestPending, Is.True);

        string active = PlayerPrefs.GetString($"game_save_v2_{scope}_active");
        PlayerPrefs.SetString($"game_save_v2_{scope}_{active}", "{broken-json");
        PlayerPrefs.Save();

        Assert.That(store.TryLoad(scope, out SaveGameData recovered, out bool recoveredPending), Is.True);
        Assert.That(recovered.run.money, Is.EqualTo(6000));
        Assert.That(recoveredPending, Is.False);
        ClearScope(scope);
    }

    [Test]
    public void Normalize_V2Settings_MigratesLegacyPlayerPrefsOnce()
    {
        PlayerPrefs.SetFloat(SaveDataFactory.LegacyVolumeKey, 0.35f);
        PlayerPrefs.SetInt(SaveDataFactory.LegacyKeyboardHintsKey, 0);
        PlayerPrefs.SetInt(SaveDataFactory.LegacyTutorialCompletedKey, 1);
        try
        {
            SaveGameData legacy = new() { schemaVersion = 2 };
            SaveDataFactory.Normalize(legacy);

            Assert.That(legacy.schemaVersion, Is.EqualTo(8));
            Assert.That(legacy.settings.masterVolume, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(legacy.settings.keyboardHintsEnabled, Is.False);
            Assert.That(legacy.settings.tutorialCompleted, Is.True);

            PlayerPrefs.SetFloat(SaveDataFactory.LegacyVolumeKey, 0.9f);
            SaveDataFactory.Normalize(legacy);
            Assert.That(legacy.settings.masterVolume, Is.EqualTo(0.35f).Within(0.001f));
        }
        finally
        {
            PlayerPrefs.DeleteKey(SaveDataFactory.LegacyVolumeKey);
            PlayerPrefs.DeleteKey(SaveDataFactory.LegacyKeyboardHintsKey);
            PlayerPrefs.DeleteKey(SaveDataFactory.LegacyTutorialCompletedKey);
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void Normalize_V3Settings_PrefersAccountValuesOverLegacyPlayerPrefs()
    {
        PlayerPrefs.SetFloat(SaveDataFactory.LegacyVolumeKey, 0.95f);
        try
        {
            SaveGameData account = SaveDataFactory.CreateDefault();
            account.settings.masterVolume = 0.2f;
            account.settings.keyboardHintsEnabled = false;
            account.settings.tutorialCompleted = true;
            SaveDataFactory.Normalize(account);

            Assert.That(account.settings.masterVolume, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(account.settings.keyboardHintsEnabled, Is.False);
            Assert.That(account.settings.tutorialCompleted, Is.True);
        }
        finally
        {
            PlayerPrefs.DeleteKey(SaveDataFactory.LegacyVolumeKey);
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void Normalize_MergesLegacyJeonghyunIntoCanonicalJeonghyeon()
    {
        SaveGameData data = SaveDataFactory.CreateDefault();
        data.account.customers.Add(new CustomerProgressData
        {
            customerId = "jeonghyeon",
            met = true,
            completedTopicIds = new() { "topic-1" }
        });
        data.account.customers.Add(new CustomerProgressData
        {
            customerId = "jeonghyun",
            storyCompleted = true,
            visitCount = 3,
            completedTopicIds = new() { "topic-2" }
        });

        SaveDataFactory.Normalize(data);

        CustomerProgressData merged = data.account.customers.Find(value => value.customerId == "jeonghyeon");
        Assert.That(data.account.customers.FindAll(value => value.customerId.StartsWith("jeonghy")).Count, Is.EqualTo(1));
        Assert.That(merged.met, Is.True);
        Assert.That(merged.storyCompleted, Is.True);
        Assert.That(merged.visitCount, Is.EqualTo(3));
        Assert.That(merged.completedTopicIds, Is.EquivalentTo(new[] { "topic-1", "topic-2" }));
    }

    [Test]
    public void Normalize_V3StoryDates_MovesDatesIntoRunState()
    {
        SaveGameData data = SaveDataFactory.CreateDefault();
        data.schemaVersion = 3;
        data.account.customers.Add(new CustomerProgressData
        {
            customerId = "jeonghyeon",
            lastTalkDay = 2,
            retryAvailableDay = 5
        });

        SaveDataFactory.Normalize(data);

        CustomerStoryRunState state = data.run.customerStories.Find(
            value => value.customerId == "jeonghyeon");
        CustomerProgressData customer = data.account.customers.Find(
            value => value.customerId == "jeonghyeon");
        Assert.That(state, Is.Not.Null);
        Assert.That(state.lastTalkDay, Is.EqualTo(2));
        Assert.That(state.nextSpecialOrderDay, Is.EqualTo(5));
        Assert.That(state.specialOrderState, Is.EqualTo(CustomerStorySchedule.Retry));
        Assert.That(customer.lastTalkDay, Is.EqualTo(-1));
        Assert.That(customer.retryAvailableDay, Is.EqualTo(-1));
    }

    [Test]
    public void Normalize_V4ScheduledOrder_AddsScheduledState()
    {
        SaveGameData data = SaveDataFactory.CreateDefault();
        data.schemaVersion = 4;
        data.run.customerStories.Add(new CustomerStoryRunState
        {
            customerId = "jeonghyeon",
            nextSpecialOrderDay = 3
        });

        SaveDataFactory.Normalize(data);

        CustomerStoryRunState state = data.run.customerStories.Find(
            value => value.customerId == "jeonghyeon");
        Assert.That(data.schemaVersion, Is.EqualTo(8));
        Assert.That(state.specialOrderState, Is.EqualTo(CustomerStorySchedule.Scheduled));
    }

    [Test]
    public void Normalize_V5Account_PreservesGrandfatheredFillingsAndQueuedEffects()
    {
        SaveGameData legacy = SaveDataFactory.CreateDefault();
        legacy.schemaVersion = 5;
        legacy.run.unlockedFillingIds = new() { "red-bean", "custard", "nutella", "cream-cheese" };
        legacy.run.queuedDayEffects = new()
        {
            new QueuedDayEffectData
            {
                productId = "item-cooking-fever",
                effectCode = "cook-time-multiplier",
                targetDay = 3,
                durationSeconds = 30f,
                multiplier = 0.8f
            }
        };

        SaveDataFactory.Normalize(legacy);

        Assert.That(legacy.schemaVersion, Is.EqualTo(8));
        Assert.That(legacy.run.unlockedFillingIds, Is.EquivalentTo(new[]
        {
            "red-bean", "custard", "nutella", "cream-cheese"
        }));
        Assert.That(legacy.run.queuedDayEffects, Has.Count.EqualTo(1));
        Assert.That(legacy.run.queuedDayEffects[0].targetDay, Is.EqualTo(3));
    }

    [Test]
    public void Normalize_V6Fillings_DoesNotTreatPermanentUnlocksAsDailySelections()
    {
        SaveGameData legacy = SaveDataFactory.CreateDefault();
        legacy.schemaVersion = 6;
        legacy.run.nextDay = 2;
        legacy.run.unlockedFillingIds = new() { "red-bean", "custard", "nutella", "green-tea" };
        legacy.run.selectedFillingIds = null;

        SaveDataFactory.Normalize(legacy);

        Assert.That(legacy.schemaVersion, Is.EqualTo(8));
        Assert.That(legacy.run.unlockedFillingIds, Has.Count.EqualTo(4));
        Assert.That(legacy.run.selectedFillingIds, Is.Empty);
    }

    [Test]
    public void BakingMultiplier_PremiumAndFeverStackMultiplicativelyAndSnapshotInputs()
    {
        SaveGameData data = SaveDataFactory.CreateDefault();
        data.run.queuedDayEffects.Add(new QueuedDayEffectData
        {
            productId = GameplayItemEffects.CookingFeverProductId,
            effectCode = GameplayItemEffects.CookingTimeMultiplierEffectCode,
            targetDay = 2,
            durationSeconds = 30f,
            multiplier = 0.8f
        });

        Assert.That(
            GameplayItemEffects.CalculateBakingTimeMultiplier(data, 2, 29.9f, 0.8f),
            Is.EqualTo(0.64f).Within(0.0001f));
        Assert.That(
            GameplayItemEffects.CalculateBakingTimeMultiplier(data, 2, 30f, 0.8f),
            Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(
            GameplayItemEffects.CalculateBakingTimeMultiplier(data, 3, 0f, 1f),
            Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void CustomerStorySchedule_FailureRetriesInTwoDaysAndStaysDue()
    {
        int retryDay = CustomerStorySchedule.RetryDayAfterFailure(3);

        Assert.That(retryDay, Is.EqualTo(5));
        Assert.That(CustomerStorySchedule.IsOrderDue(retryDay, 4), Is.False);
        Assert.That(CustomerStorySchedule.IsOrderDue(retryDay, 5), Is.True);
        Assert.That(CustomerStorySchedule.IsOrderDue(retryDay, 6), Is.True);
    }

    [TestCase(false, false, true, -1, true)]
    [TestCase(true, false, true, -1, false)]
    [TestCase(false, true, true, -1, false)]
    [TestCase(false, false, false, -1, false)]
    [TestCase(false, false, true, 3, false)]
    public void CustomerStorySchedule_OnlyRestoresMissingPendingOrder(
        bool storyCompleted,
        bool hasRemainingTopics,
        bool fillingAvailable,
        int scheduledDay,
        bool expected)
    {
        Assert.That(
            CustomerStorySchedule.ShouldRestorePendingOrder(
                storyCompleted, hasRemainingTopics, fillingAvailable, scheduledDay),
            Is.EqualTo(expected));
    }

    [Test]
    public void CustomerStorySchedule_FirstOrderDay_UsesFirstDayForResetRun()
    {
        Assert.That(CustomerStorySchedule.FirstOrderDay(0), Is.EqualTo(1));
        Assert.That(CustomerStorySchedule.FirstOrderDay(4), Is.EqualTo(5));
    }

    [Test]
    public void MergeAfterRemoteConflict_PreservesAccountUnlocksAndUsesRemoteRun()
    {
        SaveGameData remote = SaveDataFactory.CreateDefault();
        remote.revision = 7;
        remote.run.nextDay = 8;
        remote.run.money = 17000;
        remote.account.discoveredSouls.Add(new SoulDiscoveryData { soulId = "soul:remote" });
        remote.account.lifetimeStats.totalSales = 20;

        SaveGameData local = SaveDataFactory.CreateDefault();
        local.run.nextDay = 3;
        local.run.money = 8000;
        local.account.discoveredSouls.Add(new SoulDiscoveryData { soulId = "soul:local" });
        local.account.customers.Add(new CustomerProgressData
        {
            customerId = "jeonghyeon",
            storyCompleted = true,
            completedTopicIds = new() { "topic-1" }
        });
        local.account.lifetimeStats.totalSales = 25;

        SaveGameData merged = SaveDataFactory.MergeAfterRemoteConflict(remote, local);

        Assert.That(merged.revision, Is.EqualTo(7));
        Assert.That(merged.run.nextDay, Is.EqualTo(8));
        Assert.That(merged.run.money, Is.EqualTo(17000));
        Assert.That(merged.account.discoveredSouls.Exists(value => value.soulId == "soul:remote"), Is.True);
        Assert.That(merged.account.discoveredSouls.Exists(value => value.soulId == "soul:local"), Is.True);
        Assert.That(merged.account.customers.Find(value => value.customerId == "jeonghyeon").storyCompleted, Is.True);
        Assert.That(merged.account.lifetimeStats.totalSales, Is.EqualTo(25));
    }

    [Test]
    public void MergeAfterRemoteConflict_PreservesRetryStateAtTheSameDueDay()
    {
        SaveGameData remote = SaveDataFactory.CreateDefault();
        remote.run.customerStories.Add(new CustomerStoryRunState
        {
            customerId = "jeonghyeon",
            nextSpecialOrderDay = 5,
            specialOrderState = CustomerStorySchedule.Scheduled
        });
        SaveGameData local = SaveDataFactory.CreateDefault();
        local.run.customerStories.Add(new CustomerStoryRunState
        {
            customerId = "jeonghyeon",
            nextSpecialOrderDay = 5,
            specialOrderState = CustomerStorySchedule.Retry
        });

        SaveGameData merged = SaveDataFactory.MergeAfterRemoteConflict(remote, local);

        CustomerStoryRunState state = merged.run.customerStories.Find(
            value => value.customerId == "jeonghyeon");
        Assert.That(state.specialOrderState, Is.EqualTo(CustomerStorySchedule.Retry));
    }

    [TestCase(0, false)]
    [TestCase(500, false)]
    [TestCase(503, false)]
    [TestCase(401, true)]
    public void PlatformSession_OnlyExplicitUnauthorizedClearsAccount(long status, bool expected)
    {
        MethodInfo method = typeof(GamePlatformClient).GetMethod(
            "IsAuthoritativeLogoutStatus",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        Assert.That(method.Invoke(null, new object[] { status }), Is.EqualTo(expected));
    }

    [Test]
    public void InAppMarketPrefab_ContainsPurchaseHistoryStatesAndPagination()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/UI/UI_InAppMarket.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(FindChild(prefab.transform, "ProductTabButton"), Is.Not.Null);
        Assert.That(FindChild(prefab.transform, "PurchaseHistoryTabButton"), Is.Not.Null);
        Assert.That(FindChild(prefab.transform, "PurchaseHistoryScrollView"), Is.Not.Null);
        Assert.That(FindChild(prefab.transform, "PurchaseHistoryRowTemplate"), Is.Not.Null);
        Assert.That(FindChild(prefab.transform, "LoadMorePurchasesButton"), Is.Not.Null);

        MethodInfo statusLabel = typeof(UI_InAppMarket).GetMethod(
            "StatusLabel", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(statusLabel, Is.Not.Null);
        Assert.That(statusLabel.Invoke(null, new object[] { "pending" }), Is.EqualTo("결제 대기"));
        Assert.That(statusLabel.Invoke(null, new object[] { "succeeded" }), Is.EqualTo("결제 완료"));
        Assert.That(statusLabel.Invoke(null, new object[] { "failed" }), Is.EqualTo("결제 실패"));
        Assert.That(statusLabel.Invoke(null, new object[] { "cancelled" }), Is.EqualTo("결제 취소"));
        Assert.That(statusLabel.Invoke(null, new object[] { "expired" }), Is.EqualTo("시간 만료"));
    }

    private static void ClearScope(string scope)
    {
        foreach (string suffix in new[]
                 {
                     "active", "a", "a_checksum", "a_pending_remote",
                     "b", "b_checksum", "b_pending_remote"
                 })
            PlayerPrefs.DeleteKey($"game_save_v2_{scope}_{suffix}");
        PlayerPrefs.Save();
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root.name == childName) return root;
        foreach (Transform child in root)
        {
            Transform match = FindChild(child, childName);
            if (match != null) return match;
        }
        return null;
    }
}
#endif

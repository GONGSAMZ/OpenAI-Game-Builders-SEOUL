#if UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class SavePipelineTests
{
    [Test]
    public void GameInit_AfterSettlementWaitsForStore()
    {
        SaveGameData data = SaveDataFactory.CreateDefault();
        data.run.nextDay = 2;
        data.run.activeDay = null;

        Assert.That(GameManagerEx.ShouldOpenStoreOnGameInit(data), Is.True);
    }

    [Test]
    public void GameInit_FirstOrActiveDayStartsGameplay()
    {
        SaveGameData firstDay = SaveDataFactory.CreateDefault();
        Assert.That(GameManagerEx.ShouldOpenStoreOnGameInit(firstDay), Is.False);

        SaveGameData activeDay = SaveDataFactory.CreateDefault();
        activeDay.run.nextDay = 2;
        activeDay.run.activeDay = new ActiveDayData { day = 2, runId = "active-run" };
        Assert.That(GameManagerEx.ShouldOpenStoreOnGameInit(activeDay), Is.False);
    }

    [Test]
    public void CustomerStory_TopicCompletionUsesRequestedCustomer()
    {
        CustomerProgressData jeongHyun = SaveService.Service.GetCustomer(CustomerType.JeongHyun);
        CustomerProgressData haJin = SaveService.Service.GetCustomer(CustomerType.HaJin);
        var jeongHyunBackup = new System.Collections.Generic.List<string>(jeongHyun.completedTopicIds);
        var haJinBackup = new System.Collections.Generic.List<string>(haJin.completedTopicIds);

        try
        {
            jeongHyun.completedTopicIds.Clear();
            haJin.completedTopicIds.Clear();
            jeongHyun.completedTopicIds.Add(SaveIds.Topic(0));

            Assert.That(CustomerStoryProgress.IsTopicCompleted(CustomerType.JeongHyun, 0), Is.True);
            Assert.That(CustomerStoryProgress.IsTopicCompleted(CustomerType.HaJin, 0), Is.False);
        }
        finally
        {
            jeongHyun.completedTopicIds = jeongHyunBackup;
            haJin.completedTopicIds = haJinBackup;
        }
    }

    [Test]
    public void UIManager_ClosingEmptyPopupDoesNotThrow()
    {
        UIManager manager = new();
        LogAssert.Expect(LogType.Warning, "[UI] 닫을 팝업이 없습니다.");

        Assert.DoesNotThrow(() => manager.CloseUI(false));
    }

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
        Assert.That(data.run.unlockedFillingIds, Is.EquivalentTo(new[]
        {
            "red-bean", "custard", "nutella", "cream-cheese"
        }));
        Assert.That(data.run.selectedFillingIds, Is.EquivalentTo(new[]
        {
            "red-bean", "custard", "nutella", "cream-cheese"
        }));
        Assert.That(data.run.customerStories, Is.Empty);
        Assert.That(data.run.queuedDayEffects, Is.Empty);
        Assert.That(data.account.discoveredSouls.Count, Is.EqualTo(1));
        Assert.That(data.account.lifetimeStats.totalSales, Is.EqualTo(50));
    }

    [Test]
    public void Normalize_DayOneLegacySelection_RestoresAllDefaultFillings()
    {
        SaveGameData data = SaveDataFactory.CreateDefault();
        data.run.nextDay = 1;
        data.run.selectedFillingIds = new() { "red-bean" };

        SaveDataFactory.Normalize(data);

        Assert.That(data.run.selectedFillingIds, Is.EquivalentTo(new[]
        {
            "red-bean", "custard", "nutella", "cream-cheese"
        }));
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
        Assert.That(legacy.run.unlockedFillingIds, Has.Count.EqualTo(5));
        Assert.That(legacy.run.unlockedFillingIds, Does.Contain("cream-cheese"));
        Assert.That(legacy.run.selectedFillingIds, Is.Empty);
    }

    [Test]
    public void RestoreSelectedFillings_UsesOnlyDefaultFillings_NotAllUnlockedFillings()
    {
        RunProgressData run = new()
        {
            unlockedFillingIds = new() { "red-bean", "green-tea", "sweet-potato" },
            selectedFillingIds = new()
        };
        MethodInfo restorer = typeof(SaveService).GetMethod(
            "RestoreSelectedFillingIdsIfEmpty", BindingFlags.NonPublic | BindingFlags.Static);

        bool restored = (bool)restorer.Invoke(null, new object[] { run });

        Assert.That(restored, Is.True);
        Assert.That(run.selectedFillingIds, Is.EquivalentTo(new[]
        {
            "red-bean", "custard", "nutella", "cream-cheese"
        }));
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
    public void LocalGameStore_CatalogMatchesServerFillingIdsAndPrices()
    {
        GameStoreCatalogData catalog = LocalGameStore.CreateCatalog();

        Assert.That(catalog.catalogVersion, Is.EqualTo(LocalGameStore.CatalogVersion));
        Assert.That(catalog.Find("filling-pizza").price, Is.EqualTo(6000));
        Assert.That(catalog.Find("filling-mint").price, Is.EqualTo(9000));
        Assert.That(catalog.Find("filling-sweet-potato").price, Is.EqualTo(12000));
        Assert.That(catalog.Find("filling-green-tea").price, Is.EqualTo(15000));
        Assert.That(catalog.Find("filling-green-tea").displayName, Is.EqualTo("녹차"));
        Assert.That(catalog.Find("filling-green-tea").effect.fillingId, Is.EqualTo("green-tea"));
    }

    [Test]
    public void LocalGameStore_PurchaseDeductsMoneyUnlocksAndSelectsFilling()
    {
        SaveGameData data = SaveDataFactory.CreateDefault();
        data.run.money = 20000;

        bool success = LocalGameStore.TryPurchaseFilling(
            data, LocalGameStore.CreateCatalog(), "filling-mint", out string message);

        Assert.That(success, Is.True, message);
        Assert.That(data.run.money, Is.EqualTo(11000));
        Assert.That(data.run.unlockedFillingIds, Does.Contain("mint"));
        Assert.That(data.run.selectedFillingIds, Does.Contain("mint"));
    }

    [Test]
    public void LocalGameStore_RejectsInsufficientFundsDuplicateAndItemPurchase()
    {
        GameStoreCatalogData catalog = LocalGameStore.CreateCatalog();
        SaveGameData poor = SaveDataFactory.CreateDefault();
        poor.run.money = 100;
        Assert.That(LocalGameStore.TryPurchaseFilling(
            poor, catalog, "filling-mint", out _), Is.False);
        Assert.That(poor.run.money, Is.EqualTo(100));

        SaveGameData duplicate = SaveDataFactory.CreateDefault();
        duplicate.run.money = 20000;
        duplicate.run.selectedFillingIds.Add("pizza");
        int originalMoney = duplicate.run.money;
        Assert.That(LocalGameStore.TryPurchaseFilling(
            duplicate, catalog, "filling-pizza", out _), Is.False);
        Assert.That(duplicate.run.money, Is.EqualTo(originalMoney));

        Assert.That(LocalGameStore.TryPurchaseFilling(
            duplicate, catalog, "item-dual-pour", out string itemMessage), Is.False);
        Assert.That(itemMessage, Does.Contain("로그인"));
    }

    [Test]
    public void LocalGameStore_PurchasedSelectionSurvivesLocalSaveReload()
    {
        const string scope = "editmode_guest_store_test";
        ClearScope(scope);
        try
        {
            GameStoreCatalogData catalog = LocalGameStore.CreateCatalog();
            SaveGameData data = SaveDataFactory.CreateDefault();
            data.run.money = 20000;
            Assert.That(LocalGameStore.TryPurchaseFilling(
                data, catalog, "filling-green-tea", out _), Is.True);

            PlayerPrefsLocalSaveStore store = new();
            store.Save(scope, data);

            Assert.That(store.TryLoad(scope, out SaveGameData loaded), Is.True);
            GameStoreStateData state = LocalGameStore.CreateState(loaded, catalog);
            Assert.That(state.selectedFillingIds, Does.Contain("green-tea"));
            Assert.That(state.Find("filling-green-tea").status, Is.EqualTo("selected"));
        }
        finally
        {
            ClearScope(scope);
        }
    }

    [Test]
    public void CustomerStory_DaySelectionUsesCurrentlySelectedFilling()
    {
        SaveGameData data = SaveDataFactory.CreateDefault();
        data.run.selectedFillingIds = new() { "nutella" };
        MethodInfo selector = typeof(CustomerStoryProgress).GetMethod(
            "FindActiveStory", BindingFlags.NonPublic | BindingFlags.Static);

        CustomerStoryData story = (CustomerStoryData)selector.Invoke(null, new object[] { data });

        Assert.That(story, Is.Not.Null);
        Assert.That(story.CustomerType, Is.EqualTo(CustomerType.Geonwoo));
    }

    [Test]
    public void CustomerStory_SpecialOrderMatchesFillingAndIgnoresBakeState()
    {
        CustomerStoryData story = CustomerStoryCatalog.Get(CustomerType.JeongHyun);
        MethodInfo matcher = typeof(CustomerStoryProgress).GetMethod(
            "IsSpecialOrderMatch", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(matcher.Invoke(null, new object[] { story, FillingType.custard, QualityStatus.soft }), Is.True);
        Assert.That(matcher.Invoke(null, new object[] { story, FillingType.custard, QualityStatus.crisp }), Is.True);
        Assert.That(matcher.Invoke(null, new object[] { story, FillingType.nutella, QualityStatus.soft }), Is.False);
    }

    [Test]
    public void CustomerOrder_QualityReductionIsAppliedOncePerFishBun()
    {
        MethodInfo calculator = typeof(CustomerController).GetMethod(
            "CalculateAngerReduction", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(calculator.Invoke(null, new object[] { 1, QualityStatus.perfect }), Is.EqualTo(100));
        Assert.That(calculator.Invoke(null, new object[] { 1, QualityStatus.soft }), Is.EqualTo(80));
        Assert.That(calculator.Invoke(null, new object[] { 4, QualityStatus.crisp }), Is.EqualTo(20));
    }

    [Test]
    public void CustomerStoryCutscene_MissingPlayerInvokesFinishedExactlyOnce()
    {
        MethodInfo fallback = typeof(CustomerStoryCutscenePlayer).GetMethod(
            "OpenOrFinish",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(CustomerStoryCutscenePlayer), typeof(CustomerType), typeof(Action) },
            null);
        int calls = 0;

        fallback.Invoke(null, new object[] { null, CustomerType.JeongHyun, (Action)(() => calls++) });

        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void SaveService_ServiceReturnsInitializedInstance()
    {
        SaveService original = SaveService.Instance;
        SaveService service = SaveService.Service;
        try
        {
            Assert.That(service, Is.Not.Null);
            Assert.That(service.Current, Is.Not.Null);
        }
        finally
        {
            if (original == null && service != null)
                UnityEngine.Object.DestroyImmediate(service.gameObject);
        }
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

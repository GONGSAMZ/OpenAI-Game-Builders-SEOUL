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
        data.account.discoveredSouls.Add(new SoulDiscoveryData { soulId = "soul:red-bean:soft" });
        data.account.lifetimeStats.totalSales = 50;

        SaveDataFactory.ResetRun(data);

        Assert.That(data.run.nextDay, Is.EqualTo(1));
        Assert.That(data.run.money, Is.EqualTo(SaveDataFactory.InitialMoney));
        Assert.That(data.run.unlockedFillingIds.Count, Is.EqualTo(4));
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
    public void Normalize_V2Settings_MigratesLegacyPlayerPrefsOnce()
    {
        PlayerPrefs.SetFloat(SaveDataFactory.LegacyVolumeKey, 0.35f);
        PlayerPrefs.SetInt(SaveDataFactory.LegacyKeyboardHintsKey, 0);
        PlayerPrefs.SetInt(SaveDataFactory.LegacyTutorialCompletedKey, 1);
        try
        {
            SaveGameData legacy = new() { schemaVersion = 2 };
            SaveDataFactory.Normalize(legacy);

            Assert.That(legacy.schemaVersion, Is.EqualTo(3));
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
        foreach (string suffix in new[] { "active", "a", "a_checksum", "b", "b_checksum" })
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

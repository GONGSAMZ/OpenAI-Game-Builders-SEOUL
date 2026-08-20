#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
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

    private static void ClearScope(string scope)
    {
        foreach (string suffix in new[] { "active", "a", "a_checksum", "b", "b_checksum" })
            PlayerPrefs.DeleteKey($"game_save_v2_{scope}_{suffix}");
        PlayerPrefs.Save();
    }
}
#endif

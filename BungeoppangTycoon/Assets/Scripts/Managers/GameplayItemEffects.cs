using System;
using UnityEngine;

public static class GameplayItemEffects
{
    public const string DoubleGoldenMoldItemId = "item-double-golden-mold";
    public const string DualPourItemId = "item-dual-pour";
    public const string CookingFeverProductId = "item-cooking-fever";
    public const string CookingTimeMultiplierEffectCode = "cook-time-multiplier";

    public static bool HasItem(SaveGameData data, string itemId) =>
        data?.run?.ownedGameplayItemIds?.Contains(itemId) == true;

    public static float CalculateBakingTimeMultiplier(
        SaveGameData data,
        int day,
        float elapsedGameSeconds,
        float premiumMultiplier)
    {
        float multiplier = Mathf.Clamp(premiumMultiplier, 0.01f, 1f);
        QueuedDayEffectData[] effects = data?.run?.queuedDayEffects?.ToArray() ??
            Array.Empty<QueuedDayEffectData>();
        foreach (QueuedDayEffectData effect in effects)
        {
            if (effect == null || effect.productId != CookingFeverProductId ||
                effect.effectCode != CookingTimeMultiplierEffectCode || effect.targetDay != day)
                continue;
            if (elapsedGameSeconds < 0f || elapsedGameSeconds >= effect.durationSeconds)
                continue;
            multiplier *= Mathf.Clamp(effect.multiplier, 0.01f, 1f);
        }
        return multiplier;
    }

    public static float CurrentBakingTimeMultiplier()
    {
        float premium = GamePlatformClient.Instance?.BakingTimeMultiplier ?? 1f;
        return CalculateBakingTimeMultiplier(
            SaveService.Data,
            Managers.Game.Day,
            Managers.Game.delta,
            premium);
    }

    public static MoldController FindAdjacentMold(MoldController source)
    {
        if (source == null || source.transform.parent == null)
            return null;

        MoldController nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (MoldController candidate in source.transform.parent.GetComponentsInChildren<MoldController>(true))
        {
            if (candidate == null || candidate == source)
                continue;
            float distance = (candidate.transform.position - source.transform.position).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;
            nearest = candidate;
            nearestDistance = distance;
        }
        return nearest;
    }
}

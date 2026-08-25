using System;
using System.Collections.Generic;

[Serializable]
public sealed class GameStoreCatalogData
{
    public string catalogVersion = string.Empty;
    public string currency = string.Empty;
    public GameStoreProductData[] products = Array.Empty<GameStoreProductData>();

    public GameStoreProductData Find(string productId) =>
        Array.Find(products ?? Array.Empty<GameStoreProductData>(), value => value?.productId == productId);
}

[Serializable]
public sealed class GameStoreProductData
{
    public string productId = string.Empty;
    public string category = string.Empty;
    public string displayName = string.Empty;
    public string description = string.Empty;
    public int price;
    public string currency = string.Empty;
    public string ownership = string.Empty;
    public string availability = string.Empty;
    public GameStoreEffectData effect = new();
}

[Serializable]
public sealed class GameStoreEffectData
{
    public string code = string.Empty;
    public string fillingId = string.Empty;
    public float multiplier = 1f;
    public float durationSeconds;
}

[Serializable]
public sealed class GameStoreStateData
{
    public long revision;
    public int money;
    public string[] unlockedFillingIds = Array.Empty<string>();
    public string[] selectedFillingIds = Array.Empty<string>();
    public string[] ownedGameplayItemIds = Array.Empty<string>();
    public QueuedDayEffectData[] queuedDayEffects = Array.Empty<QueuedDayEffectData>();
    public GameStoreProductStateData[] products = Array.Empty<GameStoreProductStateData>();

    public GameStoreProductStateData Find(string productId) =>
        Array.Find(products ?? Array.Empty<GameStoreProductStateData>(), value => value?.productId == productId);
}

[Serializable]
public sealed class GameStoreProductStateData
{
    public string productId = string.Empty;
    public string status = string.Empty;
}

[Serializable]
public sealed class GameStoreMutationEnvelope
{
    public bool duplicate;
    public SaveGameData profile;
    public GameStoreStateData store;
}

[Serializable]
public sealed class GameRunMutationEnvelope
{
    public bool duplicate;
    public SaveGameData profile;
}

[Serializable]
public sealed class ApiErrorEnvelope
{
    public ApiErrorData error;
    public SaveGameData profile;
}

[Serializable]
public sealed class ApiErrorData
{
    public string code = string.Empty;
    public string message = string.Empty;
}

/// <summary>
/// 서버에 로그인하지 않은 플레이에서도 재료를 고를 수 있도록 사용하는 로컬 상점 규칙입니다.
/// 서버 카탈로그의 ID와 가격을 그대로 사용하며, 아이템은 로그인 전용으로 남겨 둡니다.
/// </summary>
public static class LocalGameStore
{
    public const string CatalogVersion = "2026-08-26.1";

public static GameStoreCatalogData CreateCatalog() => new()
    {
        catalogVersion = CatalogVersion,
        currency = "game-money",
        products = new[]
        {
            Product("filling-red-bean", "filling", "크림치즈", "고소하고 부드러운 크림치즈 붕어빵", 6000,
                "daily-selection", "select-filling", "cream-cheese"),
            Product("filling-custard", "filling", "피자", "치즈와 토핑을 담은 이색 붕어빵", 9000,
                "daily-selection", "select-filling", "pizza"),
            Product("filling-nutella", "filling", "민트", "시원하고 독특한 민트 붕어빵", 12000,
                "daily-selection", "select-filling", "mint"),
            Product("filling-green-tea", "filling", "고구마", "달콤하고 포근한 고구마 붕어빵", 15000,
                "daily-selection", "select-filling", "sweet-potato"),
            Product("item-double-golden-mold", "item", "황금 2구 틀", "두 마리를 한 번에 구울 수 있는 틀", 4800,
                "run-permanent", "paired-mold"),
            Product("item-dual-pour", "item", "동시 붓기", "두 칸에 반죽을 한 번에 붓기", 3200,
                "run-permanent", "paired-batter-pour"),
            Product("item-cooking-fever", "item", "조리 피버", "다음 영업일 첫 30초 동안 굽기 속도 20% 증가", 2800,
                "next-day-consumable", "cook-time-multiplier", multiplier: 0.8f, durationSeconds: 30f),
        }
    };

    public static bool TryPurchaseFilling(
        SaveGameData data,
        GameStoreCatalogData catalog,
        string productId,
        out string message)
    {
        message = string.Empty;
        if (data?.run == null || string.IsNullOrWhiteSpace(productId))
        {
            message = "구매할 상품 정보가 없습니다.";
            return false;
        }

        GameStoreProductData product = catalog?.Find(productId);
        if (product == null)
        {
            message = "상품을 찾을 수 없습니다.";
            return false;
        }
        if (product.category != "filling" || product.effect?.code != "select-filling")
        {
            message = "아이템 구매는 로그인 후 이용할 수 있습니다.";
            return false;
        }
        if (product.availability != "available")
        {
            message = "아직 구매할 수 없는 재료입니다.";
            return false;
        }

        data.run.unlockedFillingIds ??= new List<string>();
        data.run.selectedFillingIds ??= new List<string>();
        string fillingId = product.effect.fillingId;
        if (data.run.selectedFillingIds.Contains(fillingId))
        {
            message = "이미 선택한 재료입니다.";
            return false;
        }
        if (data.run.money < product.price)
        {
            message = "보유금이 부족합니다.";
            return false;
        }

        data.run.money -= product.price;
        if (!data.run.unlockedFillingIds.Contains(fillingId))
            data.run.unlockedFillingIds.Add(fillingId);
        data.run.selectedFillingIds.Add(fillingId);
        return true;
    }

    public static GameStoreStateData CreateState(SaveGameData data, GameStoreCatalogData catalog)
    {
        SaveDataFactory.Normalize(data);
        List<GameStoreProductStateData> states = new();
        foreach (GameStoreProductData product in catalog?.products ?? Array.Empty<GameStoreProductData>())
        {
            bool isFilling = product.effect?.code == "select-filling";
            bool selected = isFilling && data.run.selectedFillingIds.Contains(product.effect.fillingId);
            bool ownedItem = !isFilling && data.run.ownedGameplayItemIds.Contains(product.productId);
            string status = product.availability != "available"
                ? "locked"
                : selected
                    ? "selected"
                    : ownedItem
                        ? "owned"
                        : isFilling
                            ? data.run.money >= product.price ? "purchasable" : "insufficient-funds"
                            : "login-required";
            states.Add(new GameStoreProductStateData { productId = product.productId, status = status });
        }

        return new GameStoreStateData
        {
            revision = data.revision,
            money = data.run.money,
            unlockedFillingIds = data.run.unlockedFillingIds.ToArray(),
            selectedFillingIds = data.run.selectedFillingIds.ToArray(),
            ownedGameplayItemIds = data.run.ownedGameplayItemIds.ToArray(),
            queuedDayEffects = data.run.queuedDayEffects.ToArray(),
            products = states.ToArray()
        };
    }

    private static GameStoreProductData Product(
        string productId,
        string category,
        string displayName,
        string description,
        int price,
        string ownership,
        string effectCode,
        string fillingId = "",
        float multiplier = 1f,
        float durationSeconds = 0f) => new()
    {
        productId = productId,
        category = category,
        displayName = displayName,
        description = description,
        price = price,
        currency = "game-money",
        ownership = ownership,
        availability = "available",
        effect = new GameStoreEffectData
        {
            code = effectCode,
            fillingId = fillingId,
            multiplier = multiplier,
            durationSeconds = durationSeconds
        }
    };
}

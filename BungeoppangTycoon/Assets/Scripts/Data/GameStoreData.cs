using System;

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

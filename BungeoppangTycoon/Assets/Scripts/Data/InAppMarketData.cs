using System;

[Serializable]
public sealed class InAppMarketPublicConfig
{
    public string storeMode;
    public string hiveWebShopUrl;
}

[Serializable]
public sealed class InAppMarketCatalog
{
    public string mode;
    public InAppMarketProduct[] products;
}

[Serializable]
public sealed class InAppMarketProduct
{
    public string id;
    public string name;
    public string description;
    public string priceLabel;
    public int priceKrw;
    public int testPointPrice;
    public string marketPid;
    public InAppMarketGrant grant;

    public bool IsPermanent
    {
        get
        {
            return grant != null &&
                !string.IsNullOrWhiteSpace(grant.itemId) &&
                !grant.itemId.EndsWith("-coin", StringComparison.OrdinalIgnoreCase);
        }
    }
}

[Serializable]
public sealed class InAppMarketGrant
{
    public string itemId;
    public int quantity;
}

[Serializable]
public sealed class InAppMarketInventoryResponse
{
    public InAppMarketInventoryEntry[] inventory;
    public InAppMarketEquipment equipment;
    public InAppMarketWallet wallet;
}

[Serializable]
public sealed class InAppMarketPurchaseResponse
{
    public InAppMarketInventoryEntry[] inventory;
    public InAppMarketEquipment equipment;
    public InAppMarketWallet wallet;
    public bool duplicate;
}

[Serializable]
public sealed class InAppMarketInventoryEntry
{
    public string itemId;
    public int quantity;
}

[Serializable]
public sealed class InAppMarketEquipment
{
    public string moldSkin;
}

[Serializable]
public sealed class InAppMarketWallet
{
    public int testPoints;
}

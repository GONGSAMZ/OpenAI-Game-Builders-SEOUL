using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Replaces the legacy filling-card hierarchies in UI_Store with instances of
/// RedBeanCard while preserving each card's content and its slot position.
/// Run once from Tools/GONGSAMZ/Migrate Store Filling Cards.
/// </summary>
public static class MigrateStoreFillingCards
{
    private const string StorePrefabPath = "Assets/Resources/Prefabs/UI/UI_Store.prefab";
    private const string CardPrefabPath = "Assets/Resources/Prefabs/RedBeanCard.prefab";
    private const string FillingContainerName = "FillingCards";

    private sealed class CardData
    {
        public string Name;
        public int SiblingIndex;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
        public Vector2 Pivot;
        public Vector3 LocalScale;
        public string ProductName;
        public string Description;
        public string Price;
        public Texture ProductTexture;
        public Rect ProductUvRect;
    }

    [MenuItem("Tools/GONGSAMZ/Migrate Store Filling Cards")]
    public static void Migrate()
    {
        var sourceCard = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (sourceCard == null)
            throw new InvalidOperationException($"Missing card template: {CardPrefabPath}");

        var storeRoot = PrefabUtility.LoadPrefabContents(StorePrefabPath);
        try
        {
            var fillingCards = FindChild(storeRoot.transform, FillingContainerName);
            if (fillingCards == null)
                throw new InvalidOperationException($"Missing {FillingContainerName} in {StorePrefabPath}");

            var legacyCards = new List<Transform>();
            for (var i = 0; i < fillingCards.childCount; i++)
            {
                var child = fillingCards.GetChild(i);
                legacyCards.Add(child);
            }

            foreach (var legacyCard in legacyCards)
            {
                var data = ReadCardData(legacyCard);
                var replacement = (GameObject)PrefabUtility.InstantiatePrefab(sourceCard, fillingCards);
                replacement.name = data.Name;
                replacement.transform.SetSiblingIndex(data.SiblingIndex);
                ApplyCardData(replacement.transform, data);
                UnityEngine.Object.DestroyImmediate(legacyCard.gameObject);
            }

            PrefabUtility.SaveAsPrefabAsset(storeRoot, StorePrefabPath);
            Debug.Log($"Migrated {legacyCards.Count} filling cards to {CardPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(storeRoot);
        }
    }

    private static CardData ReadCardData(Transform card)
    {
        var rect = (RectTransform)card;
        var art = FindDirectChild(card, "ProductArt")?.GetComponent<RawImage>();
        return new CardData
        {
            Name = card.name,
            SiblingIndex = card.GetSiblingIndex(),
            AnchorMin = rect.anchorMin,
            AnchorMax = rect.anchorMax,
            AnchoredPosition = rect.anchoredPosition,
            SizeDelta = rect.sizeDelta,
            Pivot = rect.pivot,
            LocalScale = rect.localScale,
            ProductName = ReadText(card, "ProductNameText"),
            Description = ReadText(card, "ProductDescriptionText"),
            Price = ReadText(card, "PriceText"),
            ProductTexture = art != null ? art.texture : null,
            ProductUvRect = art != null ? art.uvRect : new Rect(0f, 0f, 1f, 1f),
        };
    }

    private static void ApplyCardData(Transform card, CardData data)
    {
        var rect = (RectTransform)card;
        rect.anchorMin = data.AnchorMin;
        rect.anchorMax = data.AnchorMax;
        rect.anchoredPosition = data.AnchoredPosition;
        rect.sizeDelta = data.SizeDelta;
        rect.pivot = data.Pivot;
        rect.localScale = data.LocalScale;

        WriteText(card, "ProductNameText", data.ProductName);
        WriteText(card, "ProductDescriptionText", data.Description);
        WriteText(card, "PriceText", data.Price);

        var art = FindDirectChild(card, "ProductArt")?.GetComponent<RawImage>();
        if (art != null)
        {
            art.texture = data.ProductTexture;
            art.uvRect = data.ProductUvRect;
        }
    }

    private static string ReadText(Transform card, string childName)
    {
        return FindDirectChild(card, childName)?.GetComponent<TextMeshProUGUI>()?.text ?? string.Empty;
    }

    private static void WriteText(Transform card, string childName, string value)
    {
        var text = FindDirectChild(card, childName)?.GetComponent<TextMeshProUGUI>();
        if (text != null)
            text.text = value;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }

        return null;
    }


    private static Transform FindDirectChild(Transform parent, string name)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i).name == name)
                return parent.GetChild(i);
        }

        return null;
    }
}

using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// UI_Store를 피그마 시안의 큰 제목, 이중 보유 재화, 탭, 상품 카드 구조로 다시 만듭니다.
/// Unity가 닫힌 상태에서는 -executeMethod StorePrefabBuilder.BuildFromCommandLine으로도 실행할 수 있습니다.
/// </summary>
public static class StorePrefabBuilder
{
    private const string StorePrefabPath = "Assets/Resources/Prefabs/UI/UI_Store.prefab";

    private static readonly Color32 Ink = new(52, 43, 30, 255);
    private static readonly Color32 Teal = new(24, 91, 97, 255);
    private static readonly Color32 TealDark = new(16, 66, 72, 255);
    private static readonly Color32 Paper = new(255, 247, 226, 255);
    private static readonly Color32 PaperDark = new(238, 220, 179, 255);
    private static readonly Color32 Orange = new(196, 71, 30, 255);
    private static readonly Color32 OrangeDark = new(150, 46, 21, 255);
    private static readonly Color32 Muted = new(108, 91, 64, 255);
    private static TMP_FontAsset cachedFont;

    [MenuItem("Tools/GONGSAMZ/Rebuild Store UI")]
    public static void BuildAll()
    {
        GameObject root = BuildStorePrefab();
        PrefabUtility.SaveAsPrefabAsset(root, StorePrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidatePrefab();
        Debug.Log("UI_Store 프리팹을 장사 준비 상점 시안으로 다시 만들었습니다.");
    }

    public static void BuildFromCommandLine()
    {
        BuildAll();
    }

    public static void CapturePreviewFromCommandLine()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StorePrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("미리보기용 UI_Store 프리팹을 찾지 못했습니다.");

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        instance.GetComponent<UI_Store>().enabled = false;
        Canvas canvas = instance.GetComponent<Canvas>();

        GameObject cameraObject = new("StorePreviewCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(18, 50, 57, 255);
        camera.transform.position = new Vector3(0, 0, -10);
        camera.cullingMask = 1 << LayerMask.NameToLayer("UI");

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;

        string outputDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "resources", "ui-qa");
        Directory.CreateDirectory(outputDirectory);
        CaptureAtSize(camera, instance.GetComponent<RectTransform>(), 1920, 1080,
            Path.Combine(outputDirectory, "store-1920x1080.png"));
        CaptureAtSize(camera, instance.GetComponent<RectTransform>(), 2560, 1080,
            Path.Combine(outputDirectory, "store-2560x1080.png"));

        Object.DestroyImmediate(instance);
        Object.DestroyImmediate(cameraObject);
        Debug.Log($"상점 UI 미리보기를 저장했습니다: {outputDirectory}");
    }

    private static GameObject BuildStorePrefab()
    {
        GameObject root = CreateUiObject("UI_Store", null);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        root.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.45f;
        root.AddComponent<UI_Store>();
        Stretch(root.GetComponent<RectTransform>());

        Image backdrop = CreatePanel("Backdrop", root.transform, new Color32(18, 50, 57, 235));
        Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;

        GameObject safeArea = CreateUiObject("SafeAreaPanel", root.transform);
        Stretch(safeArea.GetComponent<RectTransform>());
        safeArea.AddComponent<UI_SafeArea>();

        Image shadow = CreatePanel("PaperShadow", safeArea.transform, new Color32(8, 35, 39, 130));
        Center(shadow.rectTransform, new Vector2(1570, 910), new Vector2(12, -14));
        shadow.raycastTarget = false;

        Image frame = CreatePanel("StoreFrame", safeArea.transform, TealDark);
        Center(frame.rectTransform, new Vector2(1570, 910), Vector2.zero);
        AddOutline(frame, PaperDark, new Vector2(3, -3));

        Image paperPanel = CreatePanel("StorePaperPanel", frame.transform, Color.white);
        Sprite storeBackground = Resources.Load<Sprite>("Sprites/UI/storeBackground");
        if (storeBackground != null)
            paperPanel.sprite = storeBackground;
        else
            paperPanel.color = Paper;
        Stretch(paperPanel.rectTransform, 16, 16, 16, 16);
        AddOutline(paperPanel, Teal, new Vector2(2, -2));

        CreateHeader(paperPanel.transform);
        CreateTabs(paperPanel.transform);
        CreateCards(paperPanel.transform);
        CreateFooter(paperPanel.transform);

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            SetLayerRecursively(root, uiLayer);
        return root;
    }

    private static void CreateHeader(Transform parent)
    {
        TextMeshProUGUI title = CreateText("TitleText", parent, "내일 장사 준비", 56, Ink, TextAlignmentOptions.MidlineLeft);
        TopLeft(title.rectTransform, new Vector2(620, 70), new Vector2(92, -48));

        TextMeshProUGUI subtitle = CreateText("SubtitleText", parent,
            "팔고 싶은 붕어빵 소를 골라 보세요.", 24, Muted, TextAlignmentOptions.MidlineLeft);
        TopLeft(subtitle.rectTransform, new Vector2(650, 40), new Vector2(96, -126));

        CreateBalanceChip(parent, "MoneyPanel", "MoneyText", "MoneyNum", "보유금", "12,800원", new Vector2(-430, -54), null);
        CreateBalanceChip(parent, "BeanCoinPanel", "BeanCoinText", "BeanCoinNum", "팥코인", "0개", new Vector2(-44, -54),
            Resources.Load<Sprite>("Sprites/UI/coin"));
    }

    private static void CreateBalanceChip(
        Transform parent,
        string panelName,
        string labelName,
        string valueName,
        string label,
        string value,
        Vector2 topRightPosition,
        Sprite iconSprite)
    {
        Image chip = CreatePanel(panelName, parent, Teal);
        TopRight(chip.rectTransform, new Vector2(360, 104), topRightPosition);
        AddOutline(chip, PaperDark, new Vector2(2, -2));

        if (iconSprite != null)
        {
            Image icon = CreatePanel("Icon", chip.transform, Color.white);
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            SetAnchoredRect(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(30, -24), new Vector2(78, 24));
        }

        float left = iconSprite == null ? 34 : 94;
        // 칩 안에서 설명과 수치를 각기 다른 세로 줄에 두어 서로 겹치지 않게 합니다.
        TextMeshProUGUI labelText = CreateText(labelName, chip.transform, label, 18, PaperDark, TextAlignmentOptions.MidlineLeft);
        SetAnchoredRect(labelText.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(left, 61), new Vector2(-26, -12));

        TextMeshProUGUI valueText = CreateText(valueName, chip.transform, value, 29, Paper, TextAlignmentOptions.MidlineLeft);
        SetAnchoredRect(valueText.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(left, 14), new Vector2(-26, -45));
    }

    private static void CreateTabs(Transform parent)
    {
        Button fillingButton = CreateButton("FillingButton", parent, "붕어빵 소", Teal, Paper, 29, out TextMeshProUGUI fillingLabel);
        fillingLabel.name = "Text (TMP)";
        TopCenter(fillingButton.GetComponent<RectTransform>(), new Vector2(380, 76), new Vector2(-206, -196));
        AddOutline(fillingButton.GetComponent<Image>(), PaperDark, new Vector2(2, -2));

        Button itemButton = CreateButton("SkillButton", parent, "아이템", PaperDark, Ink, 29, out TextMeshProUGUI itemLabel);
        itemLabel.name = "Text (TMP)";
        TopCenter(itemButton.GetComponent<RectTransform>(), new Vector2(380, 76), new Vector2(206, -196));
        AddOutline(itemButton.GetComponent<Image>(), Teal, new Vector2(2, -2));
    }

    private static void CreateCards(Transform parent)
    {
        Sprite[] fillingSprites = Resources.LoadAll<Sprite>("Sprites/fillings");
        GameObject fillingCards = CreateCardRow("FillingCards", parent);
        CreateProductCard(fillingCards.transform, "팥", "고소하고 달콤한 기본 붕어빵 소", "1,200원", SpriteAt(fillingSprites, 0));
        CreateProductCard(fillingCards.transform, "슈크림", "부드러운 우유 향이 나는 인기 소", "1,500원", SpriteAt(fillingSprites, 1));
        CreateProductCard(fillingCards.transform, "초콜릿", "진한 달콤함을 좋아하는 손님용", "1,700원", SpriteAt(fillingSprites, 2));
        CreateProductCard(fillingCards.transform, "크림치즈", "새콤달콤하게 녹아드는 특별한 소", "1,800원", SpriteAt(fillingSprites, 3));

        GameObject itemCards = CreateCardRow("ItemCards", parent);
        CreateProductCard(itemCards.transform, "두 칸 반죽통", "한 번에 두 마리의 붕어빵을 만들어요.", "준비 중", null);
        CreateProductCard(itemCards.transform, "조리 피버", "짧은 시간 동안 조리 속도가 빨라져요.", "준비 중", null);
        CreateProductCard(itemCards.transform, "따뜻한 보온등", "진열대 붕어빵을 더 오래 따뜻하게 지켜요.", "준비 중", null);
        CreateProductCard(itemCards.transform, "손님 메모판", "손님의 취향을 더 쉽게 확인할 수 있어요.", "준비 중", null);
        itemCards.SetActive(false);
    }

    private static GameObject CreateCardRow(string name, Transform parent)
    {
        GameObject row = CreateUiObject(name, parent);
        Center(row.GetComponent<RectTransform>(), new Vector2(1350, 440), new Vector2(0, -28));
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 22;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    private static void CreateProductCard(Transform parent, string name, string description, string price, Sprite artSprite)
    {
        Image card = CreatePanel($"{name}Card", parent, Paper);
        card.rectTransform.sizeDelta = new Vector2(321, 440);
        AddOutline(card, Teal, new Vector2(2, -2));

        Image artPanel = CreatePanel("ArtPanel", card.transform, new Color32(247, 230, 192, 255));
        SetAnchoredRect(artPanel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(-138, -190), new Vector2(138, -20));
        AddOutline(artPanel, PaperDark, new Vector2(1, -1));

        if (artSprite != null)
        {
            Image art = CreatePanel("ProductArt", artPanel.transform, Color.white);
            art.sprite = artSprite;
            art.preserveAspect = true;
            Stretch(art.rectTransform, 14, 14, 12, 12);
        }
        else
        {
            TextMeshProUGUI placeholder = CreateText("ItemMark", artPanel.transform, "준비 중", 25, Muted, TextAlignmentOptions.Center);
            Stretch(placeholder.rectTransform, 14, 14, 14, 14);
        }

        TextMeshProUGUI productName = CreateText("ProductNameText", card.transform, name, 28, Ink, TextAlignmentOptions.Center);
        SetAnchoredRect(productName.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(18, -238), new Vector2(-18, -196));

        TextMeshProUGUI productDescription = CreateText("ProductDescriptionText", card.transform, description, 18, Muted, TextAlignmentOptions.Center);
        productDescription.enableWordWrapping = true;
        SetAnchoredRect(productDescription.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(22, -304), new Vector2(-22, -244));

        TextMeshProUGUI priceText = CreateText("PriceText", card.transform, price, 24, OrangeDark, TextAlignmentOptions.Center);
        SetAnchoredRect(priceText.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(18, 86), new Vector2(-18, 122));

        Button purchaseButton = CreateButton("PurchaseButton", card.transform, price == "준비 중" ? "준비 중" : "구매 가능", Orange, Paper, 22, out TextMeshProUGUI purchaseLabel);
        purchaseLabel.name = "Text (TMP)";
        SetAnchoredRect(purchaseButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(18, 20), new Vector2(-18, 72));
        AddOutline(purchaseButton.GetComponent<Image>(), OrangeDark, new Vector2(1, -1));
    }

    private static void CreateFooter(Transform parent)
    {
        TextMeshProUGUI note = CreateText("StoreNoteText", parent,
            "새로운 소와 아이템은 다음 영업일부터 사용할 수 있어요.", 19, Muted, TextAlignmentOptions.MidlineLeft);
        SetAnchoredRect(note.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 0),
            new Vector2(96, 34), new Vector2(500, 68));

        Button nextDayButton = CreateButton("NextDayButton", parent, "구매를 마치고 다음 영업일", Orange, Paper, 29, out TextMeshProUGUI nextDayLabel);
        nextDayLabel.name = "NextDayButtonText";
        Center(nextDayButton.GetComponent<RectTransform>(), new Vector2(480, 94), new Vector2(396, -360));
        AddOutline(nextDayButton.GetComponent<Image>(), OrangeDark, new Vector2(2, -2));
    }

    private static Sprite SpriteAt(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject value = new(name, typeof(RectTransform));
        if (parent != null)
            value.transform.SetParent(parent, false);
        return value;
    }

    private static Image CreatePanel(string name, Transform parent, Color color)
    {
        GameObject value = CreateUiObject(name, parent);
        Image image = value.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = Font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color background, Color foreground, float fontSize, out TextMeshProUGUI labelText)
    {
        Image image = CreatePanel(name, parent, background);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.93f, 0.8f, 1f);
        colors.selectedColor = new Color(1f, 0.93f, 0.8f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        labelText = CreateText("Label", image.transform, label, fontSize, foreground, TextAlignmentOptions.Center);
        Stretch(labelText.rectTransform, 12, 12, 6, 6);
        return button;
    }

    private static TMP_FontAsset Font
    {
        get
        {
            if (cachedFont == null)
                cachedFont = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset;
            return cachedFont;
        }
    }

    private static void AddOutline(Graphic graphic, Color color, Vector2 distance)
    {
        Outline outline = graphic.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static void TopLeft(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = Vector2.up;
        rect.anchorMax = Vector2.up;
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void TopRight(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void TopCenter(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 1);
        rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Center(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect, float left = 0, float right = 0, float bottom = 0, float top = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static void CaptureAtSize(Camera camera, RectTransform root, int width, int height, string outputPath)
    {
        RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = renderTexture;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D screenshot = new(width, height, TextureFormat.RGBA32, false);
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();
        File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());

        RenderTexture.active = previous;
        camera.targetTexture = null;
        Object.DestroyImmediate(screenshot);
        Object.DestroyImmediate(renderTexture);
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }
        return null;
    }

    private static void ValidatePrefab()
    {
        GameObject store = AssetDatabase.LoadAssetAtPath<GameObject>(StorePrefabPath);
        if (store == null)
            throw new InvalidOperationException("UI_Store 프리팹을 저장하지 못했습니다.");
        if (store.GetComponent<UI_Store>() == null)
            throw new InvalidOperationException("UI_Store 스크립트가 프리팹 루트에 없습니다.");
        if (FindChild(store.transform, "NextDayButton")?.GetComponent<Button>() == null)
            throw new InvalidOperationException("다음 영업일 버튼 연결이 없습니다.");
        if (FindChild(store.transform, "FillingButton")?.GetComponent<Button>() == null ||
            FindChild(store.transform, "SkillButton")?.GetComponent<Button>() == null)
            throw new InvalidOperationException("상점 탭 버튼 연결이 없습니다.");
        if (FindChild(store.transform, "FillingCards") == null || FindChild(store.transform, "ItemCards") == null)
            throw new InvalidOperationException("상점 카드 목록을 찾지 못했습니다.");
    }
}

using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 피그마 Store UI · PC 시안을 기준으로 UI_Store를 빈 루트부터 다시 생성합니다.
/// 실행 전 기존 프리팹은 UI_Store_Legacy로 백업하고, 백업본에서는 UI_Store를 제거합니다.
/// </summary>
public static class StorePrefabBuilder
{
    private const string StorePrefabPath = "Assets/Resources/Prefabs/UI/UI_Store.prefab";
    private const string LegacyPrefabPath = "Assets/Resources/Prefabs/UI/UI_Store_Legacy.prefab";
    private const string HandoffManifestPath = "ui-handoff/ui-store/v1/handoff-manifest.json";
    private const string VerificationDirectory = "ui-handoff/ui-store/v1/verification";
    private const string AssetRoot = "Sprites/UI/StoreV2/";
    private const string TitleFontPath = "Assets/Resources/Fonts/StoreV2/GowunBatang-Bold.ttf";
    private const string BodyFontPath = "Assets/Resources/Fonts/StoreV2/GowunDodum-Regular.ttf";
    private const string TitleFontAssetPath = "Assets/Resources/Fonts/StoreV2/GowunBatang-Bold SDF.asset";
    private const string BodyFontAssetPath = "Assets/Resources/Fonts/StoreV2/GowunDodum-Regular SDF.asset";
    private const string StoreGlyphs = "내일 장사 준비 도구 붕어빵 소 아이템 보유금 팥코인 팥 슈크림 초코 크림치즈 황금 틀 동시 붓기 조리 피버 다음 상품 구매 가능 조건 필요 잠김 포근하고 진한 기본 단맛 부드럽고 달콤한 크림 짭짤하고 두 마리를 한 번에 구울 수 있는 한 번에 두 칸 반죽 잠시 동안 굽는 속도가 빨라짐 새 조리 아이템을 위한 확장 슬롯 골라 보세요 흐름을 바꾸는 일시 효과를 구매한 속은 주문에 등장합니다 도구는 영업일부터 사용할 수 있습니다 마치고 영업일₩0123456789,·—";

    private static readonly Color Ink = new(0.114f, 0.157f, 0.165f, 1f);
    private static readonly Color Muted = new(0.459f, 0.420f, 0.365f, 1f);
    private static readonly Color Paper = new(1f, 0.969f, 0.886f, 1f);
    private static readonly Color Inverse = Color.white;
    private static TMP_FontAsset titleFont;
    private static TMP_FontAsset bodyFont;

    [MenuItem("Tools/GONGSAMZ/Rebuild Store UI from Figma")]
    public static void BuildAll()
    {
        titleFont = null;
        bodyFont = null;
        BackupLegacyPrefab();
        EnsureStoreGlyphs(TitleFont);
        EnsureStoreGlyphs(BodyFont);

        GameObject root = BuildStorePrefab();
        PrefabUtility.SaveAsPrefabAsset(root, StorePrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidatePrefab();
        Debug.Log("피그마 시안 기준의 새 UI_Store 프리팹을 만들었습니다.");
    }

    /// <summary>
    /// CI 또는 닫힌 Unity Editor에서 호출하는 진입점입니다.
    /// 승인된 전달 패키지가 아닐 때는 프리팹을 바꾸지 않습니다.
    /// </summary>
    public static void BuildFromCommandLine()
    {
        ValidateHandoffReady();
        BuildAll();
    }

    /// <summary>
    /// 최종 프로젝트 안에서 프리팹을 생성한 뒤 두 탭 상태를 1920x1080 PNG로 렌더합니다.
    /// </summary>
    public static void BuildAndCaptureFromCommandLine()
    {
        BuildFromCommandLine();
        CaptureStoreTab("fillings", true);
        CaptureStoreTab("items", false);
    }

    private static void BackupLegacyPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(StorePrefabPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(LegacyPrefabPath) != null)
            return;

        if (AssetDatabase.CopyAsset(StorePrefabPath, LegacyPrefabPath) == false)
            throw new InvalidOperationException("기존 UI_Store 프리팹을 백업하지 못했습니다.");

        GameObject legacyRoot = PrefabUtility.LoadPrefabContents(LegacyPrefabPath);
        try
        {
            UI_Store storeController = legacyRoot.GetComponent<UI_Store>();
            if (storeController != null)
                Object.DestroyImmediate(storeController);

            PrefabUtility.SaveAsPrefabAsset(legacyRoot, LegacyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(legacyRoot);
        }
    }

    private static void ValidateHandoffReady()
    {
        string manifestPath = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, HandoffManifestPath);
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException($"상점 UI 전달 패키지를 찾지 못했습니다: {manifestPath}");

        string manifest = File.ReadAllText(manifestPath);
        if (!manifest.Contains("\"status\": \"HANDOFF_READY\"", StringComparison.Ordinal))
            throw new InvalidOperationException("상점 UI 전달 패키지가 HANDOFF_READY 상태가 아닙니다.");
    }

    private static GameObject BuildStorePrefab()
    {
        GameObject root = CreateObject("UI_Store", null);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        root.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        root.AddComponent<UI_Store>();
        Stretch(root.GetComponent<RectTransform>());

        RawImage sceneBackground = CreateRaw("StoreSceneBackground", root.transform, "store-scene-fillings");
        Stretch(sceneBackground.rectTransform);

        RawImage panel = CreateRaw("StorePanel", root.transform, "panel-skin");
        Center(panel.rectTransform, new Vector2(1560, 896), Vector2.zero);

        CreateHeader(panel.transform);
        CreateTabs(panel.transform);
        CreateFillings(panel.transform);
        CreateItems(panel.transform);
        CreateFooter(panel.transform);

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            SetLayerRecursively(root, uiLayer);
        return root;
    }

    private static void CreateHeader(Transform parent)
    {
        TextMeshProUGUI title = CreateText("TitleText", parent, "내일 장사 준비", 56, Ink, TextAlignmentOptions.Left, true);
        TopLeft(title.rectTransform, new Vector2(560, 70), new Vector2(142, -85));

        TextMeshProUGUI subtitle = CreateText("SubtitleText", parent, "팔고 싶은 붕어빵 소를 골라 보세요.", 24, Muted, TextAlignmentOptions.Left, false);
        TopLeft(subtitle.rectTransform, new Vector2(650, 36), new Vector2(150, -140));

        CreateBalanceChip(parent, "MoneyPanel", "MoneyText", "MoneyNum", "₩", "보유금", "12,800원", new Vector2(740, -54));
        CreateBalanceChip(parent, "BeanCoinPanel", "BeanCoinText", "BeanCoinNum", "●", "팥코인", "24개", new Vector2(1120, -54));
    }

    private static void CreateBalanceChip(Transform parent, string panelName, string labelName, string valueName, string icon, string label, string value, Vector2 position)
    {
        GameObject chip = CreateObject(panelName, parent);
        TopLeft(chip.GetComponent<RectTransform>(), new Vector2(360, 104), position);
        RawImage surface = CreateRaw("Surface", chip.transform, "button-paper");
        Stretch(surface.rectTransform);

        TextMeshProUGUI iconText = CreateText("Icon", chip.transform, icon, 28, Paper, TextAlignmentOptions.Center, true);
        SetRect(iconText.rectTransform, new Vector2(26, 32), new Vector2(22, 35));

        TextMeshProUGUI labelText = CreateText(labelName, chip.transform, label, 20, new Color(1, 1, 1, .72f), TextAlignmentOptions.Left, false);
        SetRect(labelText.rectTransform, new Vector2(222, 28), new Vector2(82, 58));

        TextMeshProUGUI valueText = CreateText(valueName, chip.transform, value, 28, Paper, TextAlignmentOptions.Left, true);
        SetRect(valueText.rectTransform, new Vector2(222, 40), new Vector2(82, 18));
    }

    private static void CreateTabs(Transform parent)
    {
        CreateTab("FillingButton", "FillingTabSurface", parent, "붕어빵 소", new Vector2(320, -158));
        CreateTab("SkillButton", "ItemTabSurface", parent, "아이템", new Vector2(740, -158));
    }

    private static void CreateTab(string name, string surfaceName, Transform parent, string label, Vector2 position)
    {
        Button tab = CreateButton(name, parent, new Vector2(380, 76), position, out TextMeshProUGUI tabLabel);
        RawImage surface = CreateRaw(surfaceName, tab.transform, "button-paper");
        surface.rectTransform.SetAsFirstSibling();
        Stretch(surface.rectTransform);
        tabLabel.name = name == "FillingButton" ? "FillingTabLabel" : "ItemTabLabel";
        tabLabel.text = label;
        tabLabel.fontSize = 28;
        tabLabel.font = BodyFont;
        tabLabel.fontStyle = FontStyles.Bold;
        tabLabel.color = name == "FillingButton" ? Inverse : Ink;
        surface.color = name == "FillingButton" ? Color.white : new Color(1, 1, 1, .48f);
    }

    private static void CreateFillings(Transform parent)
    {
        GameObject row = CreateObject("FillingCards", parent);
        Stretch(row.GetComponent<RectTransform>());

        CreateProductCard(row.transform, "RedBeanCard", "팥", "포근하고 진한 기본 단맛", "1,200원", 0, null, false, new Vector2(150, -272));
        CreateProductCard(row.transform, "CustardCard", "슈크림", "부드럽고 달콤한 크림", "1,400원", 1, null, false, new Vector2(475, -272));
        CreateProductCard(row.transform, "ChocolateCard", "초코", "진한 초콜릿의 달콤함", "1,600원", 2, null, false, new Vector2(800, -272));
        CreateProductCard(row.transform, "CreamCheeseCard", "크림치즈", "짭짤하고 부드러운 맛", "1,800원", 3, null, false, new Vector2(1125, -272));
    }

    private static void CreateItems(Transform parent)
    {
        GameObject row = CreateObject("ItemCards", parent);
        Stretch(row.GetComponent<RectTransform>());

        CreateProductCard(row.transform, "GoldenPanCard", "황금 붕어빵 틀", "두 마리를 한 번에 구울 수 있는 틀", "4,800원", -1, "item-golden-pan", false, new Vector2(110, -258));
        CreateProductCard(row.transform, "DualPourCard", "동시 붓기", "두 칸에 반죽을 한 번에 붓기", "3,200원", -1, "item-dual-pour", false, new Vector2(450, -258));
        CreateProductCard(row.transform, "CookingFeverCard", "조리 피버", "잠시 동안 굽는 속도가 빨라짐", "2,800원", -1, "item-cooking-fever", false, new Vector2(790, -258));
        CreateProductCard(row.transform, "NextItemCard", "다음 아이템", "새 조리 아이템을 위한 확장 슬롯", "조건 필요", -1, "item-golden-pan", true, new Vector2(1130, -258));
        row.SetActive(false);
    }

    private static void CreateProductCard(Transform parent, string cardName, string productName, string description, string price, int fillingIndex, string itemArt, bool locked, Vector2 position)
    {
        GameObject card = CreateObject(cardName, parent);
        TopLeft(card.GetComponent<RectTransform>(), new Vector2(280, 420), position);

        RawImage cardSurface = CreateRaw("CardSurface", card.transform, "card-surface");
        Stretch(cardSurface.rectTransform);

        if (fillingIndex >= 0)
        {
            RawImage fillingArt = CreateRaw("ProductArt", card.transform, "filling-art-sheet");
            // 소 주머니 원본은 세로로 긴 비율입니다. 카드 폭에 맞춰 늘리지 않고
            // 중앙의 세로 슬롯 안에 두어 원본 비율에 가깝게 표시합니다.
            // 카드 안쪽 여백과 피그마 시안의 상단 정렬을 맞춘 좌표입니다.
            SetRect(fillingArt.rectTransform, new Vector2(60, 172), new Vector2(120, 224));
            // 4열×2행 원본의 윗줄만 사용합니다. 높이를 1로 두면 두 개의 소가 한 카드에 함께 보입니다.
            fillingArt.uvRect = new Rect(fillingIndex * .25f, .5f, .25f, .5f);
        }
        else
        {
            RawImage itemImage = CreateRaw("ProductArt", card.transform, itemArt);
            SetRect(itemImage.rectTransform, new Vector2(180, 180), new Vector2(70, 244));
        }

        TextMeshProUGUI title = CreateText("ProductNameText", card.transform, productName, 28, Ink, TextAlignmentOptions.Left, true);
        SetRect(title.rectTransform, new Vector2(232, 40), new Vector2(24, 184));

        TextMeshProUGUI body = CreateText("ProductDescriptionText", card.transform, description, 19, Muted, TextAlignmentOptions.TopLeft, false);
        body.textWrappingMode = TextWrappingModes.Normal;
        SetRect(body.rectTransform, new Vector2(232, 58), new Vector2(24, 120));

        TextMeshProUGUI priceText = CreateText("PriceText", card.transform, price, 24, Ink, TextAlignmentOptions.Left, true);
        SetRect(priceText.rectTransform, new Vector2(232, 34), new Vector2(24, 76));

        Button purchase = CreateButton("PurchaseButton", card.transform, new Vector2(232, 52), new Vector2(24, 20), out TextMeshProUGUI purchaseLabel);
        // CreateButton은 상단 기준 버튼(탭·다음 영업일)에 쓰입니다.
        // 카드의 구매 버튼만 하단 기준이므로 좌표계를 여기서 명시적으로 바꿉니다.
        SetRect(purchase.GetComponent<RectTransform>(), new Vector2(232, 52), new Vector2(24, 16));
        RawImage purchaseSurface = CreateRaw("PurchaseSurface", purchase.transform, locked ? "button-paper" : "button-primary");
        purchaseSurface.rectTransform.SetAsFirstSibling();
        Stretch(purchaseSurface.rectTransform);
        purchaseSurface.color = locked ? new Color(1, 1, 1, .45f) : Color.white;
        purchaseLabel.text = locked ? "잠김" : "구매 가능";
        purchaseLabel.font = BodyFont;
        purchaseLabel.fontSize = 20;
        purchaseLabel.color = locked ? Ink : Inverse;
    }

    private static void CreateFooter(Transform parent)
    {
        TextMeshProUGUI note = CreateText("StoreNote", parent, "구매한 속은 내일부터 주문에 등장합니다.", 19, Muted, TextAlignmentOptions.Left, false);
        TopLeft(note.rectTransform, new Vector2(900, 29), new Vector2(150, -726));

        Button nextDay = CreateButton("NextDayButton", parent, new Vector2(480, 94), new Vector2(540, -758), out TextMeshProUGUI label);
        RawImage surface = CreateRaw("NextDaySurface", nextDay.transform, "button-primary");
        surface.rectTransform.SetAsFirstSibling();
        Stretch(surface.rectTransform);
        label.name = "NextDayButtonText";
        label.text = "구매를 마치고 다음 영업일";
        label.font = BodyFont;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 28;
        label.color = Inverse;
    }

    private static Button CreateButton(string name, Transform parent, Vector2 size, Vector2 position, out TextMeshProUGUI label)
    {
        GameObject buttonObject = CreateObject(name, parent);
        TopLeft(buttonObject.GetComponent<RectTransform>(), size, position);

        Image hitTarget = buttonObject.AddComponent<Image>();
        hitTarget.color = new Color(1, 1, 1, 0);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = hitTarget;

        label = CreateText("Label", buttonObject.transform, string.Empty, 28, Inverse, TextAlignmentOptions.Center, true);
        Stretch(label.rectTransform, 12, 12, 6, 6);
        return button;
    }

    private static RawImage CreateRaw(string name, Transform parent, string resourceName)
    {
        GameObject value = CreateObject(name, parent);
        RawImage image = value.AddComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>(AssetRoot + resourceName);
        if (image.texture == null)
            throw new InvalidOperationException($"상점 UI 이미지가 없습니다: {AssetRoot}{resourceName}");
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment, bool isTitle)
    {
        GameObject textObject = CreateObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        // Gowun Batang은 프리팹 편집 화면에서 일부 한글이 그려지지 않는 문제가 있어,
        // 상점의 모든 동적 문구를 검증된 Gowun Dodum으로 통일하고 제목만 굵게 처리합니다.
        text.font = BodyFont;
        text.fontStyle = isTitle ? FontStyles.Bold : FontStyles.Normal;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static TMP_FontAsset TitleFont => titleFont ??= LoadFont(TitleFontPath, TitleFontAssetPath);
    private static TMP_FontAsset BodyFont => bodyFont ??= LoadFont(BodyFontPath, BodyFontAssetPath);

    private static TMP_FontAsset LoadFont(string sourcePath, string fontAssetPath)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
        if (IsUsableFont(existing))
            return existing;

        if (existing != null)
        {
            Debug.LogWarning(
                $"상점 TMP 폰트의 atlas/material 참조가 없어 안전한 기본 폰트로 대체합니다: {fontAssetPath}");
        }
        else if (AssetDatabase.LoadAssetAtPath<Font>(sourcePath) == null)
        {
            Debug.LogWarning($"상점 원본 폰트가 없어 안전한 기본 폰트로 대체합니다: {sourcePath}");
        }

        TMP_FontAsset fallback = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset;
        if (!IsUsableFont(fallback))
            throw new InvalidOperationException("상점 UI에서 사용할 수 있는 TMP 폰트와 재질을 찾지 못했습니다.");

        return fallback;
    }

    private static bool IsUsableFont(TMP_FontAsset font)
    {
        return font != null &&
               font.material != null &&
               font.atlasTextures != null &&
               font.atlasTextures.Length > 0 &&
               font.atlasTextures[0] != null;
    }

    private static void EnsureStoreGlyphs(TMP_FontAsset font)
    {
        if (!font.TryAddCharacters(StoreGlyphs, out string missingCharacters))
            Debug.LogWarning($"상점 폰트에서 만들지 못한 글자: {missingCharacters}");

        EditorUtility.SetDirty(font);
    }

    private static GameObject CreateObject(string name, Transform parent)
    {
        GameObject value = new(name, typeof(RectTransform));
        if (parent != null)
            value.transform.SetParent(parent, false);
        return value;
    }

    private static void TopLeft(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Center(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
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

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static void ValidatePrefab()
    {
        GameObject store = AssetDatabase.LoadAssetAtPath<GameObject>(StorePrefabPath);
        if (store == null || store.GetComponent<UI_Store>() == null)
            throw new InvalidOperationException("새 UI_Store 프리팹에 UI_Store 스크립트가 없습니다.");

        string[] requiredNames = { "NextDayButton", "FillingButton", "SkillButton", "FillingCards", "ItemCards", "MoneyNum", "BeanCoinNum" };
        foreach (string requiredName in requiredNames)
        {
            if (FindChild(store.transform, requiredName) == null)
                throw new InvalidOperationException($"새 UI_Store 프리팹에서 {requiredName}을(를) 찾지 못했습니다.");
        }

        foreach (TextMeshProUGUI text in store.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (!IsUsableFont(text.font) || text.fontSharedMaterial == null)
                throw new InvalidOperationException($"새 UI_Store 프리팹의 {text.name}에 TMP 폰트 재질이 없습니다.");
        }
    }

    private static void CaptureStoreTab(string tabName, bool showFillings)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StorePrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("렌더링할 UI_Store 프리팹을 찾지 못했습니다.");

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            throw new InvalidOperationException("UI_Store 프리팹 인스턴스를 만들지 못했습니다.");

        try
        {
            ConfigureCaptureTab(instance, showFillings);
            Canvas canvas = instance.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;

            GameObject cameraObject = new("StoreUiVerificationCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.106f, 0.149f, 1f);
            camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
            camera.transform.position = new Vector3(0, 0, -10);
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;

            string outputDirectory = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, VerificationDirectory);
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, $"unity-{tabName}-1920x1080.png");
            CaptureAtSize(camera, instance.GetComponent<RectTransform>(), 1920, 1080, outputPath);
            Object.DestroyImmediate(cameraObject);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static void ConfigureCaptureTab(GameObject instance, bool showFillings)
    {
        Transform fillingCards = FindChild(instance.transform, "FillingCards");
        Transform itemCards = FindChild(instance.transform, "ItemCards");
        if (fillingCards != null)
            fillingCards.gameObject.SetActive(showFillings);
        if (itemCards != null)
            itemCards.gameObject.SetActive(!showFillings);

        SetCaptureText(instance.transform, "TitleText", showFillings ? "내일 장사 준비" : "내일 장사 도구");
        SetCaptureText(instance.transform, "SubtitleText", showFillings
            ? "팔고 싶은 붕어빵 소를 골라 보세요."
            : "조리 흐름을 바꾸는 도구와 일시 효과를 골라 보세요.");
        SetCaptureText(instance.transform, "StoreNote", showFillings
            ? "카드를 눌러 구매 · 상품이 늘어나면 카드 영역만 세로로 스크롤"
            : "영구 도구와 소모성 효과를 같은 카드 목록으로 확장 · 카드 영역만 세로로 스크롤");

        RectTransform titleRect = FindChild(instance.transform, "TitleText")?.GetComponent<RectTransform>();
        if (titleRect != null)
            titleRect.anchoredPosition = showFillings ? new Vector2(142f, -85f) : new Vector2(150f, -85f);

        RectTransform noteRect = FindChild(instance.transform, "StoreNote")?.GetComponent<RectTransform>();
        if (noteRect != null)
            noteRect.sizeDelta = new Vector2(showFillings ? 800f : 900f, noteRect.sizeDelta.y);

        RawImage fillingSurface = FindChild(instance.transform, "FillingTabSurface")?.GetComponent<RawImage>();
        RawImage itemSurface = FindChild(instance.transform, "ItemTabSurface")?.GetComponent<RawImage>();
        TextMeshProUGUI fillingLabel = FindChild(instance.transform, "FillingTabLabel")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI itemLabel = FindChild(instance.transform, "ItemTabLabel")?.GetComponent<TextMeshProUGUI>();
        SetCaptureTabStyle(fillingSurface, fillingLabel, showFillings);
        SetCaptureTabStyle(itemSurface, itemLabel, !showFillings);
    }

    private static void SetCaptureTabStyle(RawImage surface, TextMeshProUGUI label, bool selected)
    {
        if (surface != null)
            surface.color = selected ? Color.white : new Color(1f, 1f, 1f, .48f);
        if (label != null)
            label.color = selected ? Inverse : Ink;
    }

    private static void SetCaptureText(Transform root, string objectName, string value)
    {
        TextMeshProUGUI text = FindChild(root, objectName)?.GetComponent<TextMeshProUGUI>();
        if (text != null)
            text.text = value;
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

    private static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
                return child;
        }
        return null;
    }
}

using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class InAppMarketPrefabBuilder
{
    private const string MarketPrefabPath = "Assets/Resources/Prefabs/UI/UI_InAppMarket.prefab";
    private const string GamePrefabPath = "Assets/Resources/Prefabs/UI/UI_Game.prefab";

    private static readonly Color32 Ink = new(58, 38, 29, 255);
    private static readonly Color32 Paper = new(249, 238, 211, 255);
    private static readonly Color32 Card = new(255, 248, 231, 255);
    private static readonly Color32 Brown = new(112, 68, 42, 255);
    private static readonly Color32 Orange = new(220, 129, 48, 255);
    private static readonly Color32 Green = new(105, 137, 73, 255);
    private static TMP_FontAsset cachedFont;

    [MenuItem("Tools/GONGSAMZ/Rebuild In-App Market UI")]
    public static void BuildAll()
    {
        BuildMarketPrefab();
        AddMarketButtonToGameHud();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidatePrefabs();
        Debug.Log("UI_InAppMarket 프리팹과 UI_Game 진입 버튼을 생성했습니다.");
    }

    public static void BuildFromCommandLine()
    {
        BuildAll();
    }

    public static void CapturePreviewFromCommandLine()
    {
        CapturePreview();
    }

    private static void BuildMarketPrefab()
    {
        GameObject root = CreateUiObject("UI_InAppMarket", null);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        root.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.3f;
        root.AddComponent<UI_InAppMarket>();
        Stretch(root.GetComponent<RectTransform>());

        Image dim = CreatePanel("DimBackground", root.transform, new Color32(25, 17, 13, 215));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        GameObject safeArea = CreateUiObject("SafeAreaPanel", root.transform);
        Stretch(safeArea.GetComponent<RectTransform>());
        safeArea.AddComponent<UI_SafeArea>();

        Image shadow = CreatePanel("ModalShadow", safeArea.transform, new Color32(20, 12, 8, 110));
        Center(shadow.rectTransform, new Vector2(1550, 900), new Vector2(12, -14));
        shadow.raycastTarget = false;

        Image modal = CreatePanel("MarketPanel", safeArea.transform, Paper);
        Center(modal.rectTransform, new Vector2(1550, 900), Vector2.zero);
        modal.raycastTarget = true;

        Image header = CreatePanel("Header", modal.transform, Ink);
        SetAnchoredRect(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -116), new Vector2(0, 0));

        TextMeshProUGUI title = CreateText("TitleText", header.transform, "장인 마켓", 48, Paper, TextAlignmentOptions.MidlineLeft);
        SetAnchoredRect(title.rectTransform, new Vector2(0, 0), new Vector2(0.55f, 1),
            new Vector2(42, 8), new Vector2(-10, -8));

        TextMeshProUGUI subtitle = CreateText("SubtitleText", header.transform,
            "계정에 보관되는 특별 상품", 25, new Color32(224, 204, 166, 255), TextAlignmentOptions.MidlineLeft);
        SetAnchoredRect(subtitle.rectTransform, new Vector2(0.45f, 0), new Vector2(0.86f, 1),
            new Vector2(0, 8), new Vector2(0, -8));

        Button closeButton = CreateButton("CloseButton", header.transform, "×", Brown, Paper, out _);
        SetAnchoredRect(closeButton.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-96, -38), new Vector2(-28, 38));

        Image accountBar = CreatePanel("AccountBar", modal.transform, new Color32(235, 219, 184, 255));
        SetAnchoredRect(accountBar.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(24, -206), new Vector2(-24, -128));

        TextMeshProUGUI loginStatus = CreateText("LoginStatusText", accountBar.transform,
            "로그인하지 않음", 26, Brown, TextAlignmentOptions.MidlineLeft);
        SetAnchoredRect(loginStatus.rectTransform, new Vector2(0, 0), new Vector2(0.55f, 1),
            new Vector2(28, 0), new Vector2(-10, 0));

        Button loginButton = CreateButton("LoginButton", accountBar.transform, "HIVE 로그인", Green, Color.white, out TextMeshProUGUI loginButtonText);
        loginButtonText.name = "LoginButtonText";
        SetAnchoredRect(loginButton.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-310, -29), new Vector2(-22, 29));

        GameObject scrollObject = CreateUiObject("ProductScrollView", modal.transform);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        SetAnchoredRect(scrollRectTransform, new Vector2(0, 0), new Vector2(1, 1),
            new Vector2(36, 116), new Vector2(-36, -222));
        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 32;

        Image viewport = CreatePanel("Viewport", scrollObject.transform, new Color32(255, 255, 255, 1));
        Stretch(viewport.rectTransform);
        viewport.gameObject.AddComponent<RectMask2D>();
        scrollRect.viewport = viewport.rectTransform;

        GameObject contentObject = CreateUiObject("ProductContent", viewport.transform);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 470);
        GridLayoutGroup grid = contentObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(30, 30, 12, 28);
        grid.cellSize = new Vector2(430, 430);
        grid.spacing = new Vector2(28, 28);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = content;

        TextMeshProUGUI emptyState = CreateText("EmptyStateText", viewport.transform,
            "상점 정보를 불러오는 중입니다…", 30, Brown, TextAlignmentOptions.Center);
        Stretch(emptyState.rectTransform, 80, 80, 60, 60);

        CreateProductCardTemplate(contentObject.transform);

        Image footer = CreatePanel("Footer", modal.transform, new Color32(241, 226, 195, 255));
        SetAnchoredRect(footer.rectTransform, new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(24, 22), new Vector2(-24, 102));

        TextMeshProUGUI status = CreateText("StatusText", footer.transform,
            "상점 정보를 불러오는 중입니다…", 23, Brown, TextAlignmentOptions.MidlineLeft);
        SetAnchoredRect(status.rectTransform, new Vector2(0, 0), new Vector2(0.78f, 1),
            new Vector2(24, 0), new Vector2(-12, 0));

        Button retry = CreateButton("RetryButton", footer.transform, "다시 시도", Orange, Color.white, out _);
        SetAnchoredRect(retry.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-246, -28), new Vector2(-22, 28));

        SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
        PrefabUtility.SaveAsPrefabAsset(root, MarketPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateProductCardTemplate(Transform parent)
    {
        Image cardImage = CreatePanel("ProductCardTemplate", parent, Card);
        RectTransform cardRect = cardImage.rectTransform;
        cardRect.sizeDelta = new Vector2(430, 430);

        Image accent = CreatePanel("Accent", cardImage.transform, Orange);
        SetAnchoredRect(accent.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -10), Vector2.zero);

        TextMeshProUGUI badge = CreateText("BadgeText", cardImage.transform, "장인 상품", 20, Green, TextAlignmentOptions.Center);
        SetAnchoredRect(badge.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(24, -54), new Vector2(-24, -20));

        TextMeshProUGUI productName = CreateText("ProductNameText", cardImage.transform, "상품 이름", 34, Ink, TextAlignmentOptions.Center);
        SetAnchoredRect(productName.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(24, -116), new Vector2(-24, -58));

        TextMeshProUGUI description = CreateText("ProductDescriptionText", cardImage.transform,
            "서버에서 불러온 상품 설명이 표시됩니다.", 23, Brown, TextAlignmentOptions.TopLeft);
        description.enableWordWrapping = true;
        SetAnchoredRect(description.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(34, -230), new Vector2(-34, -128));

        TextMeshProUGUI price = CreateText("PriceText", cardImage.transform, "₩0", 31, Orange, TextAlignmentOptions.MidlineLeft);
        SetAnchoredRect(price.rectTransform, new Vector2(0, 0), new Vector2(0.56f, 0),
            new Vector2(34, 112), new Vector2(-8, 158));

        TextMeshProUGUI owned = CreateText("OwnedText", cardImage.transform, "미보유", 22, Green, TextAlignmentOptions.MidlineRight);
        SetAnchoredRect(owned.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 0),
            new Vector2(8, 112), new Vector2(-34, 158));

        Button purchase = CreateButton("PurchaseButton", cardImage.transform, "로그인 후 구매", Brown, Color.white, out TextMeshProUGUI buttonText);
        buttonText.name = "PurchaseButtonText";
        SetAnchoredRect(purchase.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(34, 28), new Vector2(-34, 94));

        UI_InAppMarketProductCard card = cardImage.gameObject.AddComponent<UI_InAppMarketProductCard>();
        card.SetReferences(productName, description, price, owned, buttonText, purchase);
        cardImage.gameObject.SetActive(false);
    }

    private static void AddMarketButtonToGameHud()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GamePrefabPath);
        try
        {
            Transform existing = FindChild(root.transform, "inAppMarketButton");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            Button button = CreateButton("inAppMarketButton", root.transform, "장인 마켓", Brown, Paper, out _);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 0.5f);
            rect.anchoredPosition = new Vector2(-146, -50);
            rect.sizeDelta = new Vector2(188, 72);
            SetLayerRecursively(button.gameObject, LayerMask.NameToLayer("UI"));

            PrefabUtility.SaveAsPrefabAsset(root, GamePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = Font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(16, fontSize - 8);
        text.fontSizeMax = fontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Color background,
        Color foreground,
        out TextMeshProUGUI labelText)
    {
        Image image = CreatePanel(name, parent, background);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.82f, 1);
        colors.selectedColor = new Color(1f, 0.94f, 0.82f, 1);
        colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1);
        colors.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.55f);
        colors.colorMultiplier = 1;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };

        labelText = CreateText("Label", image.transform, label, 26, foreground, TextAlignmentOptions.Center);
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

    private static void SetAnchoredRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
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

    private static void ValidatePrefabs()
    {
        GameObject market = AssetDatabase.LoadAssetAtPath<GameObject>(MarketPrefabPath);
        GameObject gameHud = AssetDatabase.LoadAssetAtPath<GameObject>(GamePrefabPath);
        if (market == null || gameHud == null)
            throw new InvalidOperationException("인앱 마켓 또는 게임 HUD 프리팹을 찾지 못했습니다.");
        if (market.GetComponent<UI_InAppMarket>() == null)
            throw new InvalidOperationException("UI_InAppMarket 컴포넌트가 프리팹 루트에 없습니다.");
        if (FindChild(market.transform, "ProductCardTemplate")?.GetComponent<UI_InAppMarketProductCard>() == null)
            throw new InvalidOperationException("상품 카드 템플릿 연결이 없습니다.");
        if (FindChild(gameHud.transform, "inAppMarketButton")?.GetComponent<Button>() == null)
            throw new InvalidOperationException("UI_Game에 인앱 마켓 진입 버튼이 없습니다.");
    }

    private static void CapturePreview()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarketPrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("미리보기용 인앱 마켓 프리팹을 찾지 못했습니다.");

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        instance.GetComponent<UI_InAppMarket>().enabled = false;
        Canvas canvas = instance.GetComponent<Canvas>();

        GameObject cameraObject = new("PreviewCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(38, 29, 24, 255);
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.cullingMask = 1 << LayerMask.NameToLayer("UI");

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;

        Transform content = FindChild(instance.transform, "ProductContent");
        GameObject template = FindChild(instance.transform, "ProductCardTemplate").gameObject;
        InAppMarketProduct[] samples =
        {
            CreateSample("red-bean-100", "팥 코인 100개", "가게 업그레이드에 쓰는 데모 재화입니다.", "₩1,100", "red-bean-coin", 100),
            CreateSample("red-bean-550", "팥 코인 550개", "보너스 50개가 포함된 데모 재화 묶음입니다.", "₩5,500", "red-bean-coin", 550),
            CreateSample("golden-pan", "황금 붕어빵 틀", "가게를 빛내는 영구 소장형 데모 아이템입니다.", "₩3,300", "golden-pan", 1)
        };

        foreach (InAppMarketProduct sample in samples)
        {
            GameObject cardObject = Object.Instantiate(template, content);
            cardObject.SetActive(true);
            cardObject.GetComponent<UI_InAppMarketProductCard>()
                .SetData(sample, sample.id == "golden-pan" ? 1 : 0, true, false, _ => { });
        }
        FindChild(instance.transform, "EmptyStateText").gameObject.SetActive(false);
        FindChild(instance.transform, "LoginStatusText").GetComponent<TextMeshProUGUI>().text = "HIVE 로그인됨";
        FindChild(instance.transform, "LoginButtonText").GetComponent<TextMeshProUGUI>().text = "보유품 새로고침";
        FindChild(instance.transform, "StatusText").GetComponent<TextMeshProUGUI>().text = "상품을 선택해 주세요.";
        FindChild(instance.transform, "RetryButton").gameObject.SetActive(false);

        string workspaceRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
        string outputDirectory = Path.Combine(workspaceRoot, "resources", "ui-qa");
        Directory.CreateDirectory(outputDirectory);
        CaptureAtSize(camera, content.GetComponent<RectTransform>(), 1920, 1080,
            Path.Combine(outputDirectory, "in-app-market-1920x1080.png"));
        CaptureAtSize(camera, content.GetComponent<RectTransform>(), 1280, 720,
            Path.Combine(outputDirectory, "in-app-market-1280x720.png"));
        CaptureAtSize(camera, content.GetComponent<RectTransform>(), 2560, 1080,
            Path.Combine(outputDirectory, "in-app-market-2560x1080.png"));

        Object.DestroyImmediate(instance);
        Object.DestroyImmediate(cameraObject);
        Debug.Log($"인앱 마켓 미리보기 3종을 저장했습니다: {outputDirectory}");
    }

    private static void CaptureAtSize(Camera camera, RectTransform content, int width, int height, string outputPath)
    {
        RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = renderTexture;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
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

    private static InAppMarketProduct CreateSample(
        string id,
        string productName,
        string description,
        string price,
        string itemId,
        int quantity)
    {
        return new InAppMarketProduct
        {
            id = id,
            name = productName,
            description = description,
            priceLabel = price,
            grant = new InAppMarketGrant { itemId = itemId, quantity = quantity }
        };
    }
}

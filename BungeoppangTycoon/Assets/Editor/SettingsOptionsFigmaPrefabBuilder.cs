using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Figma Settings content V4(247:2)를 UI_SettingsOptions 프리팹으로 조립하고 검수 이미지를 만듭니다.</summary>
public static class SettingsOptionsFigmaPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/Prefabs/UI/UI_SettingsOptions.prefab";
    private const string AssetFolder = "Assets/Resources/Sprites/UI/SettingsV4";
    private const string ResourceFolder = "Sprites/UI/SettingsV4/";
    private const string GeneratedFolder = AssetFolder + "/Generated";
    private const float ContentScale = 980f / 900f;

    private static readonly Color Ink = Hex("1C2929");
    private static readonly Color BodyInk = Hex("453B2E");
    private static readonly Color Teal = Hex("2E6B70");
    private static readonly Color Paper = Hex("FFEDC2");
    private static readonly Color DangerInk = Hex("8C1F14");

    [MenuItem("Tools/Bungeoppang/Rebuild Settings Options UI from Figma")]
    public static void BuildAll()
    {
        BuildOptions();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Settings UI] Figma 247:2 기반 V4 설정 프리팹을 생성했습니다.");
    }

    [MenuItem("Tools/Bungeoppang/Capture Settings Options UI Preview")]
    public static void CapturePreview()
    {
        BuildOptions();
        string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../resources/ui-qa"));
        Directory.CreateDirectory(outputDirectory);
        Capture(new Vector2Int(1920, 1080), Path.Combine(outputDirectory, "settings-options-v4-1920x1080.png"));
        Capture(new Vector2Int(1366, 768), Path.Combine(outputDirectory, "settings-options-v4-1366x768.png"));
        CaptureContentReference(Path.Combine(outputDirectory, "settings-options-v4-content-1260x900.png"));
        AssetDatabase.Refresh();
        Debug.Log("[Settings UI] V4 검수 캡처를 저장했습니다: " + outputDirectory);
    }

    public static void CapturePreviewFromCommandLine() => CapturePreview();

    public static void BuildOptions()
    {
        PrepareAssets();

        GameObject root = new(
            "UI_SettingsOptions",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(UI_SettingsOptions));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RawImage backdrop = Raw("Backdrop", root.transform, "Sprites/Environment/background", Color.white);
        Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = false;

        Image dim = Image("BackdropDim", root.transform, new Color(0.02f, 0.06f, 0.12f, 0.22f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        RawImage panel = Raw("PaperPanel", root.transform, "Sprites/UI/StoreV2/panel-skin", Color.white);
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        panel.rectTransform.sizeDelta = new Vector2(1450f, 980f);
        panel.raycastTarget = true;

        RectTransform content = Rect("ContentRoot", panel.transform);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(1260f, 900f);
        content.localScale = Vector3.one * ContentScale;

        CreateHeader(content);
        CreateVolumeCard(content);
        CreateKeyboardCard(content);
        CreateResetZone(content);
        CreateFooter(content);
        ConfigureNavigation(root);
        ValidateHierarchy(root);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateHeader(Transform root)
    {
        SpriteGraphic("SettingsIcon", root, "settings-icon-watercolor", new RectSpec(84, 74, 78, 78), false);
        Text("TitleText", root, "가게 설정", 48, Ink, TextAlignmentOptions.Left, true, new RectSpec(184, 62, 450, 70));
        Text("SubtitleText", root, "소리와 조작 방법을 여기서 바꿀 수 있어요.", 20, Hex("574A3B"), TextAlignmentOptions.Left, false, new RectSpec(187, 120, 600, 32));

        Image divider = Image("HeaderDivider", root, new Color(0.18f, 0.42f, 0.44f, 0.42f));
        SetTopLeft(divider.rectTransform, new RectSpec(90, 164, 1080, 3));
        divider.raycastTarget = false;

        Button close = SpriteButton("CloseButton", root, "close-button-watercolor");
        SetTopLeft(close.GetComponent<RectTransform>(), new RectSpec(1150, 82, 76, 76));
        ApplyButtonColors(close);
    }

    private static void CreateVolumeCard(Transform root)
    {
        Transform card = RoundedPanel(
            "VolumeCard",
            root,
            "card-surface-v4",
            new RectSpec(80, 190, 520, 340));

        SpriteGraphic("VolumeIconBadge", card, "sound-icon-badge", new RectSpec(30, 28, 72, 72), false);
        SpriteGraphic("VolumeIcon", card, "sound-icon", new RectSpec(47, 45, 38, 38), false);
        Text("VolumeLabel", card, "전체 음량", 32, Ink, TextAlignmentOptions.Left, true, new RectSpec(122, 24, 250, 52));
        Text("VolumeValueText", card, "100%", 32, Teal, TextAlignmentOptions.Right, true, new RectSpec(380, 24, 110, 52));
        Text("VolumeHelpText", card, "모든 소리의 크기를 조절해요.", 19, BodyInk, TextAlignmentOptions.Left, false, new RectSpec(122, 74, 350, 32));

        Button minus = SpriteTextButton("VolumeMinusButton", card, "volume-down", "−", 34, Hex("403324"));
        SetTopLeft(minus.GetComponent<RectTransform>(), new RectSpec(28, 157, 60, 60));
        ApplyButtonColors(minus);

        Slider slider = CreateSlider(card);
        SetTopLeft(slider.GetComponent<RectTransform>(), new RectSpec(92, 164, 336, 46));

        Button plus = SpriteTextButton("VolumePlusButton", card, "volume-down", "+", 34, Hex("403324"));
        SetTopLeft(plus.GetComponent<RectTransform>(), new RectSpec(432, 157, 60, 60));
        ApplyButtonColors(plus);

        Text("VolumeStateText", card, "현재 소리: 가장 크게", 20, Teal, TextAlignmentOptions.Center, true, new RectSpec(80, 244, 360, 46));
    }

    private static void CreateKeyboardCard(Transform root)
    {
        Transform card = RoundedPanel(
            "KeyboardCard",
            root,
            "card-surface-v4",
            new RectSpec(660, 190, 520, 340));

        SpriteGraphic("KeyboardIconBadge", card, "keyboard-icon-badge", new RectSpec(30, 28, 72, 72), false);
        SpriteGraphic("KeyboardIcon", card, "keyboard-icon", new RectSpec(46, 44, 40, 40), false);
        Text("KeyboardLabel", card, "키보드 안내", 32, Ink, TextAlignmentOptions.Left, true, new RectSpec(122, 24, 250, 52));
        Text("KeyboardHintDescriptionText", card, "단축키를 화면에 표시해요.", 19, BodyInk, TextAlignmentOptions.Left, false, new RectSpec(122, 74, 245, 32));

        Button toggle = RoundedButton("KeyboardHintToggle", card, "toggle-track-v4");
        SetTopLeft(toggle.GetComponent<RectTransform>(), new RectSpec(362, 32, 128, 58));
        Text("KeyboardHintStateText", toggle.transform, "켜짐", 19, Hex("FFF0C7"), TextAlignmentOptions.Center, true, new RectSpec(8, 11, 64, 36));
        Image thumb = SpriteGraphic("KeyboardHintToggleThumb", toggle.transform, "toggle-thumb", new RectSpec(76, 6, 46, 46), false);
        thumb.rectTransform.anchorMin = thumb.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        thumb.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        thumb.rectTransform.anchoredPosition = new Vector2(-29f, 0f);
        ApplyButtonColors(toggle);

        Keycap(card, "Keycap_Space", "Space", "화면 전환", new RectSpec(30, 132, 214, 62));
        Keycap(card, "Keycap_1–8", "1–8", "재료 선택", new RectSpec(276, 132, 214, 62));

        Button help = SpriteTextButton("KeyboardHelpButton", card, "button-controls-watercolor", "조작법 다시 보기", 22, Hex("FFF0C7"));
        SetTopLeft(help.GetComponent<RectTransform>(), new RectSpec(110, 232, 300, 78));
        ApplyButtonColors(help);
    }

    private static void CreateResetZone(Transform root)
    {
        Transform zone = RoundedPanel(
            "ResetZone",
            root,
            "danger-zone-v4",
            new RectSpec(80, 560, 1100, 145));

        SpriteGraphic("WarningIcon", zone, "warning-icon", new RectSpec(30, 42, 54, 54), false);
        Text("ResetTitle", zone, "게임 플레이 초기화", 29, DangerInk, TextAlignmentOptions.Left, true, new RectSpec(108, 20, 420, 50));
        Text("ResetDescription", zone, "1일차 · 보유금 · 일반 재료 진행이 초기화됩니다.", 19, Hex("47382B"), TextAlignmentOptions.Left, false, new RectSpec(108, 66, 640, 32));
        Text("PreservedDataText", zone, "스토리 · 도감 · 업적 · 구매품은 그대로 유지됩니다.", 19, Teal, TextAlignmentOptions.Left, true, new RectSpec(108, 98, 650, 30));

        Button reset = SpriteTextButton("ResetGameButton", zone, "button-reset-watercolor", "초기화하기", 23, Hex("FFF0C7"));
        SetTopLeft(reset.GetComponent<RectTransform>(), new RectSpec(800, 32, 270, 82));
        ApplyButtonColors(reset);
    }

    private static void CreateFooter(Transform root)
    {
        RectTransform footer = Rect("FooterActions", root);
        SetTopLeft(footer, new RectSpec(80, 735, 1100, 92));

        Transform esc = RoundedPanel("FooterEscKeycap", footer, "keycap-v4", new RectSpec(238, 24, 70, 46));
        Text("Label", esc, "Esc", 19, Hex("332E24"), TextAlignmentOptions.Center, true, new RectSpec(0, 0, 70, 46));
        Text("FooterCloseHint", footer, "닫기", 18, Hex("473B2E"), TextAlignmentOptions.Left, false, new RectSpec(318, 24, 90, 46));

        Button close = SpriteTextButton("FooterCloseButton", footer, "button-controls-watercolor", "닫기", 24, Hex("FFF0C7"));
        SetTopLeft(close.GetComponent<RectTransform>(), new RectSpec(400, 4, 300, 82));
        ApplyButtonColors(close);

        SpriteGraphic("AutoSaveStatusIcon", footer, "autosave-status", new RectSpec(760, 38, 18, 18), false);
        Text("AutoSaveLabel", footer, "자동 저장됨", 18, Hex("473B2E"), TextAlignmentOptions.Left, false, new RectSpec(790, 24, 180, 46));
    }

    private static Slider CreateSlider(Transform parent)
    {
        GameObject root = new("VolumeSlider", typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);

        Image background = RoundedImage("Background", root.transform, "slider-track-v4");
        background.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        background.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        background.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        background.rectTransform.anchoredPosition = Vector2.zero;
        background.rectTransform.sizeDelta = new Vector2(0f, 22f);
        background.raycastTarget = false;

        RectTransform fillArea = Rect("Fill Area", root.transform);
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(0f, 12f);
        fillArea.offsetMax = new Vector2(-23f, -12f);
        Image fill = RoundedImage("Fill", fillArea, "slider-active-v4");
        Stretch(fill.rectTransform);
        fill.raycastTarget = false;

        RectTransform handleArea = Rect("Handle Slide Area", root.transform);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(0f, 0f);
        handleArea.offsetMax = new Vector2(0f, 0f);
        Image handle = SpriteGraphic("Handle", handleArea, "volume-knob", new RectSpec(0, 0, 46, 46), true);
        handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        handle.rectTransform.anchoredPosition = Vector2.zero;

        Slider slider = root.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.transition = Selectable.Transition.ColorTint;
        ApplySelectableColors(slider);
        return slider;
    }

    private static void Keycap(Transform parent, string name, string key, string description, RectSpec rect)
    {
        Transform cap = RoundedPanel(name, parent, "keycap-v4", rect);
        Text("Key", cap, key, 21, Hex("29332E"), TextAlignmentOptions.Center, true, new RectSpec(0, 2, rect.Width, 30));
        Text("Description", cap, description, 16, Hex("5E4F3D"), TextAlignmentOptions.Center, false, new RectSpec(0, 32, rect.Width, 28));
    }

    private static Transform RoundedPanel(string name, Transform parent, string asset, RectSpec rect)
    {
        Image image = RoundedImage(name, parent, asset);
        SetTopLeft(image.rectTransform, rect);
        image.raycastTarget = false;
        return image.transform;
    }

    private static Button RoundedButton(string name, Transform parent, string asset)
    {
        Image image = RoundedImage(name, parent, asset);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Image RoundedImage(string name, Transform parent, string asset)
    {
        Image image = Image(name, parent, Color.white);
        image.sprite = LoadRequiredSprite(ResourceFolder + "Generated/" + asset);
        image.type = UnityEngine.UI.Image.Type.Sliced;
        return image;
    }

    private static Button SpriteTextButton(string name, Transform parent, string asset, string label, int size, Color color)
    {
        Image image = SpriteGraphic(name, parent, asset, new RectSpec(0, 0, 100, 100), true);
        image.preserveAspect = false;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Text("Label", image.transform, label, size, color, TextAlignmentOptions.Center, true, new RectSpec(0, 0, 100, 100), true);
        return button;
    }

    private static Button SpriteButton(string name, Transform parent, string asset)
    {
        Image image = SpriteGraphic(name, parent, asset, new RectSpec(0, 0, 100, 100), true);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Image SpriteGraphic(string name, Transform parent, string asset, RectSpec rect, bool raycast)
    {
        Image image = Image(name, parent, Color.white);
        image.sprite = LoadRequiredSprite(ResourceFolder + asset);
        image.preserveAspect = true;
        image.raycastTarget = raycast;
        SetTopLeft(image.rectTransform, rect);
        return image;
    }

    private static Sprite LoadRequiredSprite(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
            throw new InvalidOperationException("[Settings UI] Sprite 연결 실패: " + resourcePath);
        return sprite;
    }

    private static RawImage Raw(string name, Transform parent, string path, Color color)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage image = go.GetComponent<RawImage>();
        image.texture = Resources.Load<Texture>(path);
        if (image.texture == null)
            throw new InvalidOperationException("[Settings UI] Texture 연결 실패: " + path);
        image.color = color;
        return image;
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI Text(
        string name,
        Transform parent,
        string value,
        int size,
        Color color,
        TextAlignmentOptions alignment,
        bool titleFont,
        RectSpec rect,
        bool stretch = false)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        // 프로젝트의 Gowun TMP 자산은 atlas/material 참조가 깨져 한글이 손상됩니다.
        // 기존 게임에서 정상 출력이 검증된 폰트를 사용하고 제목만 굵게 처리합니다.
        text.font = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset;
        text.fontStyle = titleFont ? FontStyles.Bold : FontStyles.Normal;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        if (stretch)
            Stretch(text.rectTransform);
        else
            SetTopLeft(text.rectTransform, rect);
        return text;
    }

    private static void ConfigureNavigation(GameObject root)
    {
        Selectable[] order =
        {
            Find<Selectable>(root, "CloseButton"),
            Find<Selectable>(root, "VolumeSlider"),
            Find<Selectable>(root, "VolumeMinusButton"),
            Find<Selectable>(root, "VolumePlusButton"),
            Find<Selectable>(root, "KeyboardHintToggle"),
            Find<Selectable>(root, "KeyboardHelpButton"),
            Find<Selectable>(root, "ResetGameButton"),
            Find<Selectable>(root, "FooterCloseButton")
        };

        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == null)
                continue;
            Navigation navigation = new() { mode = Navigation.Mode.Explicit };
            navigation.selectOnUp = order[(i - 1 + order.Length) % order.Length];
            navigation.selectOnLeft = navigation.selectOnUp;
            navigation.selectOnDown = order[(i + 1) % order.Length];
            navigation.selectOnRight = navigation.selectOnDown;
            order[i].navigation = navigation;
        }
    }

    private static T Find<T>(GameObject root, string name) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
            if (component.name == name)
                return component;
        return null;
    }

    private static void ValidateHierarchy(GameObject root)
    {
        if (root.GetComponent<UI_SettingsOptions>() == null)
            throw new InvalidOperationException("[Settings UI] UI_SettingsOptions 스크립트 누락");

        string[] required =
        {
            "TitleText", "CloseButton", "VolumeSlider", "VolumeValueText",
            "VolumeMinusButton", "VolumePlusButton", "KeyboardHintToggle",
            "KeyboardHintStateText", "KeyboardHelpButton", "ResetGameButton",
            "FooterCloseButton"
        };
        foreach (string name in required)
            if (Find<RectTransform>(root, name) == null)
                throw new InvalidOperationException("[Settings UI] 필수 오브젝트 누락: " + name);

        string[] spriteObjects =
        {
            "SettingsIcon", "CloseButton", "VolumeCard", "VolumeIconBadge",
            "VolumeIcon", "VolumeMinusButton", "Background", "Fill", "Handle",
            "VolumePlusButton", "KeyboardCard", "KeyboardIconBadge", "KeyboardIcon",
            "KeyboardHintToggle", "KeyboardHintToggleThumb", "Keycap_Space", "Keycap_1–8",
            "KeyboardHelpButton", "ResetZone", "WarningIcon", "ResetGameButton",
            "FooterEscKeycap", "FooterCloseButton", "AutoSaveStatusIcon"
        };
        foreach (string name in spriteObjects)
        {
            Image image = Find<Image>(root, name);
            if (image == null || image.sprite == null)
                throw new InvalidOperationException("[Settings UI] Sprite 누락: " + name);
        }

        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.font == null || text.fontSharedMaterial == null)
                throw new InvalidOperationException("[Settings UI] TMP 폰트 또는 재질 누락: " + text.name);
        }
    }

    private static void ApplyButtonColors(Button button) => ApplySelectableColors(button);

    private static void ApplySelectableColors(Selectable selectable)
    {
        ColorBlock colors = selectable.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.86f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = new Color(1f, 0.93f, 0.72f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        selectable.colors = colors;
    }

    private static void SetTopLeft(RectTransform rect, RectSpec value)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(value.X, -value.Y);
        rect.sizeDelta = new Vector2(value.Width, value.Height);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void PrepareAssets()
    {
        Directory.CreateDirectory(FullAssetPath(GeneratedFolder));
        GenerateRoundedSprite("card-surface-v4", 128, 128, 24, 3, new Color32(255, 246, 219, 133), new Color32(74, 48, 31, 115), new Vector4(30, 30, 30, 30));
        GenerateRoundedSprite("danger-zone-v4", 128, 128, 22, 3, new Color32(250, 209, 178, 173), new Color32(158, 41, 26, 166), new Vector4(28, 28, 28, 28));
        GenerateRoundedSprite("keycap-v4", 64, 64, 14, 3, new Color32(255, 237, 194, 255), new Color32(74, 48, 31, 140), new Vector4(18, 18, 18, 18));
        GenerateRoundedSprite("toggle-track-v4", 128, 58, 29, 3, new Color32(46, 107, 112, 255), new Color32(31, 74, 77, 255), new Vector4(30, 30, 30, 30));
        GenerateRoundedSprite("slider-track-v4", 64, 22, 11, 0, new Color32(140, 115, 79, 82), new Color32(0, 0, 0, 0), new Vector4(12, 12, 12, 12));
        GenerateRoundedSprite("slider-active-v4", 64, 22, 11, 0, new Color32(46, 107, 112, 255), new Color32(0, 0, 0, 0), new Vector4(12, 12, 12, 12));
        AssetDatabase.Refresh();

        ConfigureSprite(AssetFolder + "/button-controls-watercolor.png", 4096, Vector4.zero);
        ConfigureSprite(AssetFolder + "/button-reset-watercolor.png", 4096, Vector4.zero);
        ConfigureSprite(AssetFolder + "/settings-icon-watercolor.png", 2048, Vector4.zero);
        ConfigureSprite(AssetFolder + "/close-button-watercolor.png", 2048, Vector4.zero);

        string[] small =
        {
            "sound-icon-badge", "sound-icon", "volume-down", "volume-knob",
            "keyboard-icon-badge", "keyboard-icon", "toggle-thumb", "warning-icon",
            "autosave-status"
        };
        foreach (string name in small)
            ConfigureSprite(AssetFolder + "/" + name + ".png", 512, Vector4.zero);
    }

    private static void GenerateRoundedSprite(
        string name,
        int width,
        int height,
        float radius,
        float border,
        Color32 fill,
        Color32 stroke,
        Vector4 spriteBorder)
    {
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float outer = Coverage(x, y, width, height, radius, 0f);
            float inner = border > 0f
                ? Coverage(x, y, width, height, Mathf.Max(0f, radius - border), border)
                : outer;
            float strokeCoverage = Mathf.Max(0f, outer - inner);
            float alpha = (fill.a / 255f) * inner + (stroke.a / 255f) * strokeCoverage;
            if (alpha <= 0.001f)
            {
                pixels[y * width + x] = new Color32(0, 0, 0, 0);
                continue;
            }
            float fillWeight = (fill.a / 255f) * inner;
            float strokeWeight = (stroke.a / 255f) * strokeCoverage;
            float total = Mathf.Max(0.001f, fillWeight + strokeWeight);
            pixels[y * width + x] = new Color32(
                (byte)((fill.r * fillWeight + stroke.r * strokeWeight) / total),
                (byte)((fill.g * fillWeight + stroke.g * strokeWeight) / total),
                (byte)((fill.b * fillWeight + stroke.b * strokeWeight) / total),
                (byte)(Mathf.Clamp01(alpha) * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        byte[] png = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);

        string assetPath = GeneratedFolder + "/" + name + ".png";
        string fullPath = FullAssetPath(assetPath);
        if (!File.Exists(fullPath) || !BytesEqual(File.ReadAllBytes(fullPath), png))
            File.WriteAllBytes(fullPath, png);
        ConfigureSprite(assetPath, 512, spriteBorder);
    }

    private static float Coverage(int x, int y, int width, int height, float radius, float inset)
    {
        int hits = 0;
        const int samples = 4;
        for (int sy = 0; sy < samples; sy++)
        for (int sx = 0; sx < samples; sx++)
        {
            float px = x + (sx + 0.5f) / samples;
            float py = y + (sy + 0.5f) / samples;
            float left = inset;
            float right = width - inset;
            float bottom = inset;
            float top = height - inset;
            float cx = Mathf.Clamp(px, left + radius, right - radius);
            float cy = Mathf.Clamp(py, bottom + radius, top - radius);
            float dx = px - cx;
            float dy = py - cy;
            if (px >= left && px <= right && py >= bottom && py <= top && dx * dx + dy * dy <= radius * radius)
                hits++;
        }
        return hits / (float)(samples * samples);
    }

    private static void ConfigureSprite(string path, int maxSize, Vector4 border)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("[Settings UI] Sprite importer 없음: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
            if (left[i] != right[i])
                return false;
        return true;
    }

    private static string FullAssetPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity 프로젝트 루트를 찾을 수 없습니다.");
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static Color Hex(string value)
    {
        if (ColorUtility.TryParseHtmlString("#" + value, out Color color))
            return color;
        throw new ArgumentException("잘못된 색상: " + value);
    }

    private static void Capture(Vector2Int size, string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Camera camera = new GameObject("SettingsPreviewCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.1f, 0.2f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;

        RenderTexture renderTexture = new(size.x, size.y, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = renderTexture;
        Canvas canvas = instance.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        Canvas.ForceUpdateCanvases();
        camera.Render();

        RenderTexture.active = renderTexture;
        Texture2D screenshot = new(size.x, size.y, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
        screenshot.Apply();
        File.WriteAllBytes(path, screenshot.EncodeToPNG());

        RenderTexture.active = null;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(screenshot);
        UnityEngine.Object.DestroyImmediate(renderTexture);
        UnityEngine.Object.DestroyImmediate(camera.gameObject);
        UnityEngine.Object.DestroyImmediate(instance);
    }

    private static void CaptureContentReference(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        RectTransform content = Find<RectTransform>(instance, "ContentRoot");
        if (content == null)
            throw new InvalidOperationException("[Settings UI] ContentRoot를 찾을 수 없습니다.");

        RawImage paperPanel = Find<RawImage>(instance, "PaperPanel");
        if (paperPanel == null)
            throw new InvalidOperationException("[Settings UI] PaperPanel을 찾을 수 없습니다.");
        paperPanel.enabled = false;
        paperPanel.rectTransform.sizeDelta = new Vector2(1260f, 900f);

        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(1260f, 900f);
        content.localScale = Vector3.one;

        RawImage backdrop = Find<RawImage>(instance, "Backdrop");
        if (backdrop != null)
            backdrop.enabled = false;
        UnityEngine.UI.Image dim = Find<UnityEngine.UI.Image>(instance, "BackdropDim");
        if (dim != null)
            dim.enabled = false;

        CanvasScaler scaler = instance.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1260f, 900f);
        scaler.matchWidthOrHeight = 0.5f;

        Camera camera = new GameObject("SettingsContentPreviewCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;

        RenderTexture renderTexture = new(1260, 900, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = renderTexture;
        Canvas canvas = instance.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        Canvas.ForceUpdateCanvases();
        camera.Render();

        RenderTexture.active = renderTexture;
        Texture2D screenshot = new(1260, 900, TextureFormat.RGBA32, false);
        screenshot.ReadPixels(new Rect(0, 0, 1260, 900), 0, 0);
        screenshot.Apply();
        File.WriteAllBytes(path, screenshot.EncodeToPNG());

        RenderTexture.active = null;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(screenshot);
        UnityEngine.Object.DestroyImmediate(renderTexture);
        UnityEngine.Object.DestroyImmediate(camera.gameObject);
        UnityEngine.Object.DestroyImmediate(instance);
    }

    private readonly struct RectSpec
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public RectSpec(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}

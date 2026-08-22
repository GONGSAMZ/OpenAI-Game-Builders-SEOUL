using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 승인된 Settings v6 Figma 시안을 UGUI 프리팹으로 반복 생성합니다.
/// 동작은 UI_Settings와 UI_SettingsOptions가 담당하고, 이 클래스는 배치·에셋·배치만 책임집니다.
/// </summary>
public static class SettingsV6PrefabBuilder
{
    private const string PrefabFolder = "Assets/Resources/Prefabs/UI/";
    private const string SettingsV4 = "Sprites/UI/SettingsV4/";
    private const string SettingsV6 = "Sprites/UI/SettingsV6/";
    private static readonly Color Ink = Hex("45301F");
    private static readonly Color BodyInk = Hex("6B5842");
    private static readonly Color Teal = Hex("2E6B70");
    private static readonly Color Cream = Hex("FFF2D4");
    private static readonly Color Danger = Hex("FFD0B6");
    private static readonly Color DangerInk = Hex("A63420");
    private static readonly Color Border = Hex("806947");

    [MenuItem("Tools/Bungeoppang/Build Settings UI V6")]
    public static void BuildAll()
    {
        BuildMain();
        BuildOptions();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Settings V6] UI_Settings와 UI_SettingsOptions 프리팹을 생성했습니다.");
    }

    /// <summary>
    /// 배치 실행에서 최종 프로젝트의 두 프리팹을 같은 1920×1080 조건으로 렌더링합니다.
    /// UI를 직접 조작하지 않고 기준 Figma 이미지와 비교하기 위한 검수 진입점입니다.
    /// </summary>
    public static void CapturePreviewFromCommandLine()
    {
        BuildAll();
        string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../resources/ui-qa"));
        Directory.CreateDirectory(outputDirectory);
        Capture(PrefabFolder + "UI_Settings.prefab", new Vector2Int(1920, 1080), Path.Combine(outputDirectory, "settings-v6-main-1920x1080.png"));
        Capture(PrefabFolder + "UI_SettingsOptions.prefab", new Vector2Int(1920, 1080), Path.Combine(outputDirectory, "settings-v6-options-1920x1080.png"));
        AssetDatabase.Refresh();
        Debug.Log("[Settings V6] 검수 렌더를 저장했습니다: " + outputDirectory);
    }

    public static void BuildMain()
    {
        GameObject root = CreateCanvasRoot("UI_Settings", typeof(UI_Settings));
        Transform panel = CreatePanel(root.transform);
        CreateHeader(panel, "가게 메뉴", "가게 이용과 기록을 여기서 확인하세요.");

        Divider(panel, 60, 210, 1140);
        MenuCard(panel, "SettingBtn", "가게 설정", "소리와 조작 방식을 바꿔요.", SettingsV4 + "settings-icon-watercolor", 60, 250, false);
        MenuCard(panel, "DocumentsButton", "도감", "만난 손님과 발견한 영혼을 봐요.", SettingsV6 + "icon-collection-book", 660, 250, false);
        MenuCard(panel, "AchivementButton", "업적", "달성한 기록과 목표를 확인해요.", SettingsV6 + "icon-achievement-trophy", 60, 410, false);
        MenuCard(panel, "ResetButton", "게임 플레이 초기화", "1일차부터 영업을 다시 시작해요.", SettingsV6 + "icon-warning-triangle", 660, 410, true);
        SpriteTextButton("QuitBtn", panel, "계속하기", SettingsV4 + "button-controls-watercolor", 480, 580, 300, 82, 24, false);
        Text("ShortcutHint", panel, "Esc 키를 눌러도 게임 화면으로 돌아갈 수 있어요.", 14, BodyInk, TextAlignmentOptions.Center, 420, 24, 420, 684, false);
        Button exit = SpriteButton("ExitBtn", panel, SettingsV4 + "close-button-watercolor", 1116, 92, 56, 56);
        exit.targetGraphic.raycastTarget = true;
        GameObject help = new("HelpButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        help.transform.SetParent(panel, false);
        help.SetActive(false);

        Save(root, PrefabFolder + "UI_Settings.prefab");
    }

    public static void BuildOptions()
    {
        GameObject root = CreateCanvasRoot("UI_SettingsOptions", typeof(UI_SettingsOptions));
        Transform panel = CreatePanel(root.transform);
        CreateHeader(panel, "가게 설정", "소리와 조작 방식을 여기서 바꿀 수 있어요.");
        Divider(panel, 60, 210, 1140);

        Transform volume = Card("VolumeCard", panel, 60, 250, 540, 220, false);
        SpriteGraphic("VolumeIcon", volume, SettingsV4 + "settings-icon-watercolor", 32, 30, 48, 48);
        Text("VolumeLabel", volume, "전체 음량", 26, Ink, TextAlignmentOptions.Left, 300, 36, 96, 24, true);
        Text("VolumeHelpText", volume, "모든 소리의 크기를 조절해요.", 16, BodyInk, TextAlignmentOptions.Left, 360, 24, 96, 70, false);
        Text("VolumeValueText", volume, "100%", 24, Teal, TextAlignmentOptions.Right, 100, 32, 390, 26, true);
        CreateSlider(volume);
        Text("VolumeStateText", volume, string.Empty, 14, Teal, TextAlignmentOptions.Center, 360, 24, 90, 184, false).gameObject.SetActive(false);

        const float keyboardCardWidth = 540f;
        const float keyboardCardPadding = 72f;
        const float keycapWidth = 170f;
        Transform keyboard = Card("KeyboardCard", panel, 660, 250, keyboardCardWidth, 220, false);
        Text("KeyboardLabel", keyboard, "키보드 안내", 26, Ink, TextAlignmentOptions.Left, 230, 36, keyboardCardPadding, 32, true);
        Text("KeyboardHintDescriptionText", keyboard, "단축키 안내를 화면에 표시해요.", 16, BodyInk, TextAlignmentOptions.Left, 270, 24, keyboardCardPadding, 82, false);
        Button toggle = SlicedSpriteButton("KeyboardHintToggle", keyboard, SettingsV4 + "Generated/toggle-track-v4", keyboardCardWidth - keyboardCardPadding - 118, 28, 118, 48);
        Text("KeyboardHintStateText", toggle.transform, "켜짐", 18, Hex("FFF0C7"), TextAlignmentOptions.Center, 72, 28, 10, 10, true);
        Image thumb = SpriteGraphic("KeyboardHintToggleThumb", toggle.transform, SettingsV4 + "toggle-thumb", 80, 10, 28, 28);
        thumb.rectTransform.anchorMin = thumb.rectTransform.anchorMax = new Vector2(1f, .5f);
        thumb.rectTransform.pivot = new Vector2(.5f, .5f);
        thumb.rectTransform.anchoredPosition = new Vector2(-29f, 0f);
        Keycap(keyboard, "Keycap_Space", "Space", keyboardCardPadding, 142, keycapWidth);
        Keycap(keyboard, "Keycap_1–8", "1–8", keyboardCardWidth - keyboardCardPadding - keycapWidth, 142, keycapWidth);

        Transform reset = Card("ResetZone", panel, 60, 504, 1140, 138, true);
        SpriteGraphic("WarningIcon", reset, SettingsV6 + "icon-warning-triangle", 32, 43, 52, 52);
        Text("ResetTitle", reset, "게임 플레이 초기화", 26, DangerInk, TextAlignmentOptions.Left, 560, 36, 96, 32, true);
        Text("ResetDescription", reset, "1일차·보유금·일반 재료 진행이 초기화됩니다.", 16, Hex("734331"), TextAlignmentOptions.Left, 600, 24, 96, 78, false);
        SpriteTextButton("ResetGameButton", reset, "초기화하기", SettingsV4 + "button-reset-watercolor", 818, 28, 270, 82, 22, true);

        Save(root, PrefabFolder + "UI_SettingsOptions.prefab");
    }

    private static GameObject CreateCanvasRoot(string name, Type uiType)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), uiType);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        RawImage background = Raw("Backdrop", root.transform, "Sprites/Environment/background");
        Stretch(background.rectTransform);
        background.raycastTarget = false;
        Image dim = Image("BackdropDim", root.transform, new Color(.02f, .06f, .12f, .18f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        return root;
    }

    private static Transform CreatePanel(Transform parent)
    {
        RawImage panel = Raw("PaperPanel", parent, "Sprites/UI/StoreV2/panel-skin");
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(1260, 840);
        return panel.transform;
    }

    private static void CreateHeader(Transform parent, string title, string subtitle)
    {
        SpriteGraphic("SettingsIcon", parent, SettingsV4 + "settings-icon-watercolor", 60, 92, 56, 56);
        Text("TitleText", parent, title, 36, Ink, TextAlignmentOptions.Left, 700, 44, 144, 78, true);
        Text("SubtitleText", parent, subtitle, 16, BodyInk, TextAlignmentOptions.Left, 700, 24, 146, 132, false);
        SpriteButton("CloseButton", parent, SettingsV4 + "close-button-watercolor", 1116, 92, 56, 56);
    }

    private static void Divider(Transform parent, float x, float y, float width)
    {
        Image line = Image("HeaderDivider", parent, new Color(Teal.r, Teal.g, Teal.b, .4f));
        SetTopLeft(line.rectTransform, x, y, width, 2);
        line.raycastTarget = false;
    }

    private static void MenuCard(Transform parent, string name, string title, string description, string iconPath, float x, float y, bool danger)
    {
        Button card = SlicedSpriteButton(name, parent, danger ? SettingsV4 + "Generated/danger-zone-v4" : SettingsV4 + "Generated/card-surface-v4", x, y, 540, 132);
        SpriteGraphic("Icon", card.transform, iconPath, 28, 36, 60, 60);
        Text("Title", card.transform, title, 26, danger ? DangerInk : Ink, TextAlignmentOptions.Left, 300, 36, 112, 24, true);
        Text("Description", card.transform, description, 16, danger ? Hex("734331") : BodyInk, TextAlignmentOptions.Left, 320, 24, 112, 70, false);
        SpriteGraphic("Chevron", card.transform, SettingsV6 + "icon-chevron-right", 420, 50, 32, 32);
    }

    private static Transform Card(string name, Transform parent, float x, float y, float width, float height, bool danger)
    {
        Image image = SlicedSpriteImage(name, parent, danger ? SettingsV4 + "Generated/danger-zone-v4" : SettingsV4 + "Generated/card-surface-v4");
        SetTopLeft(image.rectTransform, x, y, width, height);
        image.raycastTarget = false;
        return image.transform;
    }

    private static void CreateSlider(Transform parent)
    {
        GameObject root = new("VolumeSlider", typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        SetTopLeft(root.GetComponent<RectTransform>(), 52, 128, 430, 34);
        Image track = SlicedSpriteImage("Background", root.transform, SettingsV4 + "Generated/slider-track-v4");
        SetAnchored(track.rectTransform, 0, .5f, 1, .5f, new Vector2(0, 18));
        track.raycastTarget = false;

        GameObject fillArea = new("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        Stretch(fillArea.GetComponent<RectTransform>());
        fillArea.GetComponent<RectTransform>().offsetMin = new Vector2(0, 8);
        fillArea.GetComponent<RectTransform>().offsetMax = new Vector2(-22, -8);
        Image fill = SlicedSpriteImage("Fill", fillArea.transform, SettingsV4 + "Generated/slider-active-v4");
        Stretch(fill.rectTransform);
        fill.raycastTarget = false;

        GameObject handleArea = new("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(root.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>());
        Image handle = SpriteGraphic("Handle", handleArea.transform, SettingsV4 + "volume-knob", 0, 0, 38, 38);
        handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(1f, .5f);
        handle.rectTransform.pivot = new Vector2(.5f, .5f);
        handle.rectTransform.anchoredPosition = Vector2.zero;
        Slider slider = root.GetComponent<Slider>();
        slider.minValue = 0; slider.maxValue = 1; slider.value = 1; slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
    }

    private static void Keycap(Transform parent, string name, string label, float x, float y, float width)
    {
        Image cap = SlicedSpriteImage(name, parent, SettingsV4 + "Generated/keycap-v4");
        SetTopLeft(cap.rectTransform, x, y, width, 46);
        Text("Label", cap.transform, label, 16, Ink, TextAlignmentOptions.Center, width, 26, 0, 10, true);
    }

    private static Button RoundedButton(string name, Transform parent, float x, float y, float width, float height, Color fill, Color outline)
    {
        Image image = Image(name, parent, fill);
        image.gameObject.AddComponent<Outline>().effectColor = outline;
        image.GetComponent<Outline>().effectDistance = new Vector2(2, -2);
        SetTopLeft(image.rectTransform, x, y, width, height);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Button SlicedSpriteButton(string name, Transform parent, string spritePath, float x, float y, float width, float height)
    {
        Image image = SlicedSpriteImage(name, parent, spritePath);
        SetTopLeft(image.rectTransform, x, y, width, height);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Image SlicedSpriteImage(string name, Transform parent, string spritePath)
    {
        Image image = Image(name, parent, Color.white);
        image.sprite = LoadSprite(spritePath);
        image.type = UnityEngine.UI.Image.Type.Sliced;
        image.preserveAspect = false;
        return image;
    }

    private static Button SpriteButton(string name, Transform parent, string spritePath, float x, float y, float width, float height)
    {
        Image image = Image(name, parent, Color.white);
        image.sprite = LoadSprite(spritePath);
        image.preserveAspect = true;
        SetTopLeft(image.rectTransform, x, y, width, height);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Button SpriteTextButton(string name, Transform parent, string label, string spritePath, float x, float y, float width, float height, float size, bool danger)
    {
        Button button = SpriteButton(name, parent, spritePath, x, y, width, height);
        Text("Label", button.transform, label, size, Hex("FFF0C7"), TextAlignmentOptions.Center, width - 32, 36, 16, (height - 36) * .5f, true);
        return button;
    }

    private static Image SpriteGraphic(string name, Transform parent, string spritePath, float x, float y, float width, float height)
    {
        Image image = Image(name, parent, Color.white);
        image.sprite = LoadSprite(spritePath);
        image.preserveAspect = true;
        image.raycastTarget = false;
        SetTopLeft(image.rectTransform, x, y, width, height);
        return image;
    }

    private static Image Circle(string name, Transform parent, Color color, float width, float height, float x, float y)
    {
        Image image = Image(name, parent, color);
        SetTopLeft(image.rectTransform, x, y, width, height);
        return image;
    }

    private static RawImage Raw(string name, Transform parent, string resourcePath)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage image = go.GetComponent<RawImage>();
        image.texture = Resources.Load<Texture>(resourcePath);
        if (image.texture == null) throw new InvalidOperationException("[Settings V6] Texture 없음: " + resourcePath);
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

    private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment, float width, float height, float x, float y, bool bold)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset;
        text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
        SetTopLeft(text.rectTransform, x, y, width, height);
        return text;
    }

    private static Sprite LoadSprite(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null) throw new InvalidOperationException("[Settings V6] Sprite 없음: " + resourcePath);
        return sprite;
    }

    private static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    private static void SetAnchored(RectTransform rect, float minX, float minY, float maxX, float maxY, Vector2 size)
    {
        rect.anchorMin = new Vector2(minX, minY); rect.anchorMax = new Vector2(maxX, maxY);
        rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Save(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
    }

    private static void Capture(string prefabPath, Vector2Int size, string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            throw new InvalidOperationException("[Settings V6] 캡처할 프리팹이 없습니다: " + prefabPath);

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Camera camera = new GameObject("SettingsV6PreviewCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.10f, 0.20f);
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

    private static Color Hex(string value)
    {
        if (ColorUtility.TryParseHtmlString("#" + value, out Color color)) return color;
        throw new ArgumentException("잘못된 색상: " + value);
    }
}

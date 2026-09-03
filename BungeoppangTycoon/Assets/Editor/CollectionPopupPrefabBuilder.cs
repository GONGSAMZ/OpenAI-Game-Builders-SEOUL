using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>도감 팝업을 코드로 생성해 프리팹 구조와 스타일을 항상 동일하게 유지합니다.</summary>
public static class CollectionPopupPrefabBuilder
{
    private const string V4 = "Sprites/UI/SettingsV4/";
    private const string CardSurface = "Sprites/UI/SettingsV4/Generated/card-surface-v4";

    [MenuItem("Tools/Bungeoppang/Build Collection Popup")]
    public static void Build()
    {
        GameObject root = new("UI_Collection", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UI_Collection));
        Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        RawImage backdrop = Raw("Backdrop", root.transform, "Sprites/Environment/background"); Stretch(backdrop.rectTransform); backdrop.raycastTarget = false;
        Image dim = Image("Dim", root.transform, new Color(.02f, .06f, .12f, .30f)); Stretch(dim.rectTransform);

        RawImage panel = Raw("Panel", root.transform, "Sprites/UI/StoreV2/panel-skin");
        RectTransform panelRect = panel.rectTransform; panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f); panelRect.sizeDelta = new Vector2(1180, 790);
        Text("Title", panel.transform, "도감", 40, TextAlignmentOptions.Left, new Vector2(.06f, .88f), new Vector2(.42f, .96f), new Color(.27f, .19f, .12f), FontStyles.Bold);
        Text("Subtitle", panel.transform, "만난 손님과 마음속 이야기를 모아 보세요.", 18, TextAlignmentOptions.Left, new Vector2(.06f, .84f), new Vector2(.70f, .89f), new Color(.42f, .34f, .26f), FontStyles.Normal);
        Button close = SpriteButton("CloseButton", panel.transform, V4 + "close-button-watercolor"); SetRect(close.GetComponent<RectTransform>(), new Vector2(.90f, .88f), new Vector2(.95f, .95f));

        Button customerTab = Button("CustomerTabButton", panel.transform, "손님", new Color(.12f, .31f, .32f)); SetRect(customerTab.GetComponent<RectTransform>(), new Vector2(.06f, .76f), new Vector2(.48f, .82f));
        Button storyTab = Button("StoryTabButton", panel.transform, "스토리", new Color(.12f, .31f, .32f)); SetRect(storyTab.GetComponent<RectTransform>(), new Vector2(.52f, .76f), new Vector2(.94f, .82f));

        GameObject customerPanel = Panel("CustomerPanel", panel.transform); SetRect(customerPanel.GetComponent<RectTransform>(), new Vector2(.03f, .05f), new Vector2(.97f, .76f));
        Text("CustomerHint", customerPanel.transform, "만난 손님은 이름과 최근 대화를 확인할 수 있어요.", 18, TextAlignmentOptions.Left, new Vector2(.01f, .91f), new Vector2(.95f, .99f), new Color(.31f, .28f, .22f), FontStyles.Normal);
        GameObject customerGrid = Panel("CustomerGrid", customerPanel.transform); SetRect(customerGrid.GetComponent<RectTransform>(), new Vector2(.01f, .01f), new Vector2(.99f, .89f)); AddGrid(customerGrid, new Vector2(250, 210));

        GameObject storyPanel = Panel("StoryPanel", panel.transform); SetRect(storyPanel.GetComponent<RectTransform>(), new Vector2(.03f, .05f), new Vector2(.97f, .76f));
        Text("StoryHint", storyPanel.transform, "특별 주문을 완성하면 이야기가 열려요. 열린 이야기는 언제든 다시 볼 수 있어요.", 18, TextAlignmentOptions.Left, new Vector2(.01f, .91f), new Vector2(.95f, .99f), new Color(.31f, .28f, .22f), FontStyles.Normal);
        GameObject storyGrid = Panel("StoryGrid", storyPanel.transform); SetRect(storyGrid.GetComponent<RectTransform>(), new Vector2(.01f, .01f), new Vector2(.99f, .89f)); AddGrid(storyGrid, new Vector2(250, 210));

        GameObject detailPanel = Panel("DetailPanel", panel.transform); SetRect(detailPanel.GetComponent<RectTransform>(), new Vector2(.03f, .05f), new Vector2(.97f, .76f));
        Button back = Button("BackButton", detailPanel.transform, "← 목록으로", new Color(.83f, .70f, .47f)); SetRect(back.GetComponent<RectTransform>(), new Vector2(.01f, .91f), new Vector2(.16f, .99f));
        Image portrait = Image("DetailPortrait", detailPanel.transform, Color.white); portrait.preserveAspect = true; SetRect(portrait.rectTransform, new Vector2(.02f, .12f), new Vector2(.36f, .86f));
        Text("DetailName", detailPanel.transform, "정현", 32, TextAlignmentOptions.Left, new Vector2(.42f, .70f), new Vector2(.60f, .84f), new Color(.16f, .16f, .14f), FontStyles.Bold);
        Text("DetailAge", detailPanel.transform, "32세", 24, TextAlignmentOptions.Left, new Vector2(.62f, .70f), new Vector2(.75f, .84f), new Color(.16f, .16f, .14f), FontStyles.Bold);
        Text("DetailJob", detailPanel.transform, "회사원", 24, TextAlignmentOptions.Left, new Vector2(.77f, .70f), new Vector2(.96f, .84f), new Color(.16f, .16f, .14f), FontStyles.Bold);
        Text("DetailIntroduction", detailPanel.transform, string.Empty, 20, TextAlignmentOptions.TopLeft, new Vector2(.42f, .43f), new Vector2(.96f, .66f), new Color(.16f, .16f, .14f), FontStyles.Normal);
        Text("RecentLabel", detailPanel.transform, "최근 나눈 대화", 22, TextAlignmentOptions.Left, new Vector2(.42f, .33f), new Vector2(.96f, .41f), new Color(.12f, .31f, .32f), FontStyles.Bold);
        Text("RecentTalks", detailPanel.transform, string.Empty, 19, TextAlignmentOptions.TopLeft, new Vector2(.42f, .08f), new Vector2(.96f, .31f), new Color(.16f, .16f, .14f), FontStyles.Normal);

        GameObject replayPanel = Panel("ReplayPanel", panel.transform); SetRect(replayPanel.GetComponent<RectTransform>(), new Vector2(.03f, .05f), new Vector2(.97f, .76f));
        Text("ReplayTitle", replayPanel.transform, string.Empty, 22, TextAlignmentOptions.Center, new Vector2(.05f, .90f), new Vector2(.95f, .99f), new Color(.16f, .16f, .14f), FontStyles.Bold);
        GameObject raw = new("ReplayImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)); raw.transform.SetParent(replayPanel.transform, false); SetRect(raw.GetComponent<RectTransform>(), new Vector2(.05f, .13f), new Vector2(.95f, .87f));
        Button previous = Button("PreviousSceneButton", replayPanel.transform, "이전", new Color(.12f, .31f, .32f)); SetRect(previous.GetComponent<RectTransform>(), new Vector2(.20f, .02f), new Vector2(.40f, .10f));
        Button next = Button("NextSceneButton", replayPanel.transform, "다음", new Color(.62f, .16f, .10f)); SetRect(next.GetComponent<RectTransform>(), new Vector2(.60f, .02f), new Vector2(.80f, .10f));

        storyPanel.SetActive(false); detailPanel.SetActive(false); replayPanel.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Prefabs/UI/UI_Collection.prefab");
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
    }

    private static GameObject Panel(string name, Transform parent) { Image image = Image(name, parent, Color.white); image.sprite = Resources.Load<Sprite>(CardSurface); image.type = UnityEngine.UI.Image.Type.Sliced; return image.gameObject; }
    private static Image Image(string name, Transform parent, Color color) { GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); image.color = color; return image; }
    private static Button Button(string name, Transform parent, string label, Color color) { Image image = Image(name, parent, Color.white); image.sprite = Resources.Load<Sprite>(V4 + "button-controls-watercolor"); image.type = UnityEngine.UI.Image.Type.Sliced; Button button = image.gameObject.AddComponent<Button>(); Text("Label", image.transform, label, 22, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Color(1f, .94f, .78f), FontStyles.Bold); return button; }
    private static Button SpriteButton(string name, Transform parent, string path) { Image image = Image(name, parent, Color.white); image.sprite = Resources.Load<Sprite>(path); image.preserveAspect = true; return image.gameObject.AddComponent<Button>(); }
    private static RawImage Raw(string name, Transform parent, string path) { GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)); go.transform.SetParent(parent, false); RawImage image = go.GetComponent<RawImage>(); image.texture = Resources.Load<Texture>(path); return image; }
    private static void Text(string name, Transform parent, string content, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color color, FontStyles style) { GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>(); text.font = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset; text.text = content; text.fontSize = size; text.fontStyle = style; text.alignment = alignment; text.color = color; text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; SetRect(text.rectTransform, min, max); }
    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void AddGrid(GameObject go, Vector2 cellSize) { GridLayoutGroup grid = go.AddComponent<GridLayoutGroup>(); grid.cellSize = cellSize; grid.spacing = new Vector2(16, 16); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4; grid.padding = new RectOffset(12, 12, 12, 12); }
}

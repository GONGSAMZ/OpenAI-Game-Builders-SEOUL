using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CustomerStoryCutscenePrefabBuilder
{
    const string PrefabPath = "Assets/Resources/Prefabs/UI/UI_CustomerStoryCutscene.prefab";

    [MenuItem("Tools/Story/Build Customer Story Cutscene Prefab")]
    public static void Build()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        GameObject root = new("UI_CustomerStoryCutscene", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CustomerStoryCutsceneView));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        GameObject backdrop = Image("CutsceneRoot", root.transform, Color.black); Stretch(backdrop.GetComponent<RectTransform>());
        Image art = Image("StoryArt", backdrop.transform, Color.white).GetComponent<Image>(); Stretch(art.rectTransform); art.preserveAspect = true;
        GameObject panel = Image("DialoguePanel", backdrop.transform, new Color(.075f, .045f, .03f, .96f)); SetRect(panel.GetComponent<RectTransform>(), .055f, .035f, .945f, .255f);
        Outline outline = panel.AddComponent<Outline>(); outline.effectColor = new Color(.82f, .58f, .27f, .9f); outline.effectDistance = new Vector2(3f, -3f);

        TextMeshProUGUI progress = Text("Progress", panel.transform, 20, TextAlignmentOptions.Left, FontStyles.Bold, new Color(1f, .76f, .38f)); SetRect(progress.rectTransform, .045f, .77f, .95f, .94f);
        TextMeshProUGUI title = Text("Title", panel.transform, 27, TextAlignmentOptions.Left, FontStyles.Bold, new Color(1f, .91f, .72f)); SetRect(title.rectTransform, .045f, .52f, .95f, .76f);
        TextMeshProUGUI speaker = Text("Speaker", panel.transform, 25, TextAlignmentOptions.Left, FontStyles.Bold, new Color(1f, .84f, .53f)); SetRect(speaker.rectTransform, .045f, .32f, .95f, .53f);
        TextMeshProUGUI body = Text("Body", panel.transform, 31, TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(1f, .97f, .89f)); body.textWrappingMode = TextWrappingModes.Normal; SetRect(body.rectTransform, .045f, .12f, .89f, .42f);
        TextMeshProUGUI hint = Text("NextHint", panel.transform, 18, TextAlignmentOptions.BottomRight, FontStyles.Bold, new Color(1f, .88f, .65f)); SetRect(hint.rectTransform, .70f, .04f, .95f, .22f);

        root.GetComponent<CustomerStoryCutsceneView>().Bind(art, progress, title, speaker, body, hint);
        root.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[손님 이야기] 컷씬 프리팹 생성 완료: " + PrefabPath);
    }

    static GameObject Image(string name, Transform parent, Color color) { GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false); go.GetComponent<Image>().color = color; return go; }
    static TextMeshProUGUI Text(string name, Transform parent, float size, TextAlignmentOptions alignment, FontStyles style, Color color) { GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>(); text.font = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset; text.fontSize = size; text.fontStyle = style; text.color = color; text.alignment = alignment; text.raycastTarget = false; return text; }
    static void Stretch(RectTransform rect) => SetRect(rect, 0, 0, 1, 1);
    static void SetRect(RectTransform rect, float minX, float minY, float maxX, float maxY) { rect.anchorMin = new Vector2(minX, minY); rect.anchorMax = new Vector2(maxX, maxY); rect.offsetMin = rect.offsetMax = Vector2.zero; }
}

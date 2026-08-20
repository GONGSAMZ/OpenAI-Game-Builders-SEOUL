using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>설정 하위 팝업을 같은 스타일로 생성합니다.</summary>
public static class SettingsPopupPrefabBuilder
{
    [MenuItem("Tools/Bungeoppang/Build Settings Popups")]
    public static void BuildAll()
    {
        BuildOptions();
        Build<UI_SettingsHelp>("UI_SettingsHelp", false);
        Build<UI_SettingsStats>("UI_SettingsStats", false);
        Build<UI_SettingsStory>("UI_SettingsStory", false);
        AssetDatabase.SaveAssets();
    }

    /// <summary>옵션 팝업만 다시 생성합니다. 다른 설정 팝업은 건드리지 않습니다.</summary>
    public static void BuildOptions()
    {
        SettingsOptionsFigmaPrefabBuilder.BuildOptions();
        AssetDatabase.SaveAssets();
    }

    static void Build<T>(string name, bool includeSlider) where T : UI_SettingsPopupBase
    {
        GameObject root = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(T));
        Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        Image dim = Image("Dim", root.transform, new Color(.08f, .05f, .04f, .72f)); Stretch(dim.rectTransform);
        Image panel = Image("Panel", root.transform, new Color(1f, .97f, .87f));
        RectTransform pr = panel.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(.5f, .5f); pr.sizeDelta = new Vector2(760, includeSlider ? 520 : 520);
        Outline outline = panel.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.23f, .47f, .63f); outline.effectDistance = new Vector2(5, -5);
        Text("TitleText", panel.transform, 42, TextAlignmentOptions.Center, new Vector2(0, 1), new Vector2(1, 1), new Vector2(38, -105), new Vector2(-38, -30));
        Text("BodyText", panel.transform, 28, TextAlignmentOptions.TopLeft, new Vector2(0, .28f), new Vector2(1, .78f), new Vector2(60, 0), new Vector2(-60, 0));
        if (includeSlider)
        {
            GameObject slider = new("VolumeSlider", typeof(RectTransform), typeof(Slider)); slider.transform.SetParent(panel.transform, false);
            RectTransform sr = slider.GetComponent<RectTransform>(); sr.anchorMin = sr.anchorMax = new Vector2(.5f, .43f); sr.sizeDelta = new Vector2(560, 32);
            Image bg = Image("Background", slider.transform, new Color(.55f, .65f, .67f)); Stretch(bg.rectTransform);
            Image fill = Image("Fill", slider.transform, new Color(.28f, .58f, .73f)); Stretch(fill.rectTransform);
            Slider s = slider.GetComponent<Slider>(); s.fillRect = fill.rectTransform; s.targetGraphic = fill; s.minValue = 0; s.maxValue = 1;

            CreateKeyboardHintButton(panel.transform);
        }
        Button close = Button("CloseButton", panel.transform, "돌아가기"); RectTransform cr = close.GetComponent<RectTransform>(); cr.anchorMin = cr.anchorMax = new Vector2(.5f, 0); cr.anchoredPosition = new Vector2(0, 34); cr.sizeDelta = new Vector2(220, 62);
        PrefabUtility.SaveAsPrefabAsset(root, $"Assets/Resources/Prefabs/UI/{name}.prefab"); Object.DestroyImmediate(root);
    }
    static Image Image(string n, Transform p, Color c) { GameObject o = new(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); o.transform.SetParent(p, false); Image i=o.GetComponent<Image>(); i.color=c; return i; }
    static void Text(string n, Transform p, float size, TextAlignmentOptions align, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax) { GameObject o=new(n, typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));o.transform.SetParent(p,false);var t=o.GetComponent<TextMeshProUGUI>();t.font=Resources.Load<TMP_FontAsset>("omyuPretty SDF")??TMP_Settings.defaultFontAsset;t.fontSize=size;t.color=new Color(.2f,.15f,.12f);t.alignment=align;t.textWrappingMode=TextWrappingModes.Normal;t.raycastTarget=false;var r=t.rectTransform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=offMin;r.offsetMax=offMax; }
    static Button Button(string n, Transform p, string label) { Image i=Image(n,p,new Color(.28f,.58f,.73f));Button b=i.gameObject.AddComponent<Button>();Text("Label",i.transform,27,TextAlignmentOptions.Center,Vector2.zero,Vector2.one,new Vector2(8,4),new Vector2(-8,-4));i.GetComponentInChildren<TextMeshProUGUI>().text=label;i.GetComponentInChildren<TextMeshProUGUI>().color=new Color(1,.98f,.9f);return b; }
    static void CreateKeyboardHintButton(Transform parent)
    {
        Button button = Button("KeyboardHintButton", parent, "키보드 조작 안내  켜짐");
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.5f, .25f);
        buttonRect.sizeDelta = new Vector2(420, 58);
        button.GetComponentInChildren<TextMeshProUGUI>().gameObject.name = "KeyboardHintButtonLabel";
    }
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
}

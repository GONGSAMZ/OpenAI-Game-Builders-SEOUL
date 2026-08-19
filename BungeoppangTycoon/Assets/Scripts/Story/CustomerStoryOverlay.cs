using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>특별 주문 안내와 결과 메시지만 화면 위 팝업으로 표시합니다.</summary>
public sealed class CustomerStoryOverlay : MonoBehaviour
{
    private static CustomerStoryOverlay instance;
    private Canvas canvas;
    private GameObject panel;
    private TextMeshProUGUI title;
    private TextMeshProUGUI body;
    private Button closeButton;
    private CustomerController currentCustomer;
    private bool wasRunning;

    public static void ShowSpecialIntro(CustomerController customer)
    {
        CustomerStoryOverlay overlay = Ensure();
        overlay.OpenMessage(customer, "특별 손님", CustomerStoryProgress.ActiveStory.SpecialIntro, "특별 주문을 받을게요");
    }

    public static void ShowResult(string name, string message, bool success)
    {
        CustomerStoryOverlay overlay = Ensure();
        overlay.OpenMessage(null, success ? name + "의 이야기 해금" : name + "의 다음 힌트", message, "정산으로 이동");
    }

    private static CustomerStoryOverlay Ensure()
    {
        if (instance != null) return instance;
        GameObject root = new("@CustomerStoryOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CustomerStoryOverlay));
        DontDestroyOnLoad(root);
        instance = root.GetComponent<CustomerStoryOverlay>();
        instance.Build();
        return instance;
    }

    private void Build()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        GameObject dim = CreateImage("Dim", transform, new Color(0.08f, .05f, .04f, .72f));
        Stretch(dim.GetComponent<RectTransform>());
        panel = CreateImage("TalkPanel", transform, new Color(1f, .97f, .87f, 1f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
        panelRect.sizeDelta = new Vector2(860, 500);
        panel.AddComponent<Outline>().effectColor = new Color(.23f, .47f, .63f, 1f);
        panel.GetComponent<Outline>().effectDistance = new Vector2(5, -5);

        title = CreateText("Title", panel.transform, 40, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = new Vector2(1, 1);
        title.rectTransform.offsetMin = new Vector2(48, -88); title.rectTransform.offsetMax = new Vector2(-48, -24);
        body = CreateText("Body", panel.transform, 29, TextAlignmentOptions.TopLeft);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.rectTransform.anchorMin = new Vector2(0, .48f); body.rectTransform.anchorMax = new Vector2(1, 1);
        body.rectTransform.offsetMin = new Vector2(66, 4); body.rectTransform.offsetMax = new Vector2(-66, -102);

        closeButton = CreateButton("Close", panel.transform);
        closeButton.GetComponentInChildren<TextMeshProUGUI>().text = "닫기";
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(.5f, 0);
        closeRect.anchoredPosition = new Vector2(0, 28); closeRect.sizeDelta = new Vector2(220, 58);
        closeButton.onClick.AddListener(Close);
        panel.SetActive(false);
    }

    private void OpenMessage(CustomerController customer, string heading, string message, string closeLabel)
    {
        currentCustomer = customer;
        Pause();
        panel.SetActive(true);
        title.text = heading;
        body.text = message;
        closeButton.gameObject.SetActive(true);
        closeButton.GetComponentInChildren<TextMeshProUGUI>().text = closeLabel;
    }

    private void Pause() { wasRunning = Managers.Game.isRunning; Managers.Game.isRunning = false; }
    private void Close()
    {
        panel.SetActive(false);
        Managers.Game.isRunning = wasRunning;
        currentCustomer?.OnStoryOverlayClosed();
        currentCustomer = null;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }
    private static TextMeshProUGUI CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset;
        text.fontSize = size; text.color = new Color(.18f, .13f, .11f); text.alignment = alignment; text.raycastTarget = false;
        return text;
    }
    private static Button CreateButton(string name, Transform parent)
    {
        GameObject go = CreateImage(name, parent, new Color(.47f, .7f, .8f));
        Button button = go.AddComponent<Button>();
        TextMeshProUGUI label = CreateText("Label", go.transform, 27, TextAlignmentOptions.Center);
        label.color = new Color(1f, .98f, .9f, 1f);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = new Vector2(12, 4); label.rectTransform.offsetMax = new Vector2(-12, -4);
        return button;
    }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
}

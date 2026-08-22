using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>특별 주문 성공 및 도감 재감상에서 공용으로 쓰는 클릭형 컷씬 재생기입니다.</summary>
public sealed class CustomerStoryCutscenePlayer : MonoBehaviour
{
    private static CustomerStoryCutscenePlayer instance;
    private Canvas canvas;
    private GameObject root;
    private Image artImage;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI speakerText;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI hintText;
    private CustomerStoryCutsceneData[] scenes;
    private int sceneIndex;
    private int lineIndex;
    private int openedFrame;
    private bool wasGameRunning;
    private Action onFinished;

    public static void PlayJeongHyunUnlock(Action finished)
    {
        Ensure().Open(CustomerStoryCutsceneCatalog.JeongHyunScenes, finished);
    }

    public static void ReplayJeongHyun(Action finished)
    {
        Ensure().Open(CustomerStoryCutsceneCatalog.JeongHyunScenes, finished);
    }

    private static CustomerStoryCutscenePlayer Ensure()
    {
        if (instance != null) return instance;
        GameObject go = new("@CustomerStoryCutscenePlayer", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CustomerStoryCutscenePlayer));
        DontDestroyOnLoad(go);
        instance = go.GetComponent<CustomerStoryCutscenePlayer>();
        instance.Build();
        return instance;
    }

    private void Build()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        root = CreateImage("CutsceneRoot", transform, new Color(.03f, .025f, .02f, 1f));
        Stretch(root.GetComponent<RectTransform>());
        artImage = CreateImage("StoryArt", root.transform, Color.white).GetComponent<Image>();
        RectTransform artRect = artImage.rectTransform;
        artRect.anchorMin = Vector2.zero; artRect.anchorMax = Vector2.one;
        artRect.offsetMin = artRect.offsetMax = Vector2.zero;
        artImage.preserveAspect = true;

        GameObject caption = CreateImage("Caption", root.transform, new Color(.12f, .07f, .045f, .94f));
        RectTransform captionRect = caption.GetComponent<RectTransform>();
        captionRect.anchorMin = new Vector2(.09f, .035f); captionRect.anchorMax = new Vector2(.91f, .265f);
        captionRect.offsetMin = captionRect.offsetMax = Vector2.zero;
        Outline outline = caption.AddComponent<Outline>(); outline.effectColor = new Color(.72f, .54f, .31f, .85f); outline.effectDistance = new Vector2(3, -3);

        titleText = CreateText("Title", caption.transform, 22, TextAlignmentOptions.Left);
        titleText.color = new Color(1f, .79f, .37f); titleText.rectTransform.anchorMin = new Vector2(.05f, .73f); titleText.rectTransform.anchorMax = new Vector2(.95f, .95f); titleText.rectTransform.offsetMin = titleText.rectTransform.offsetMax = Vector2.zero;
        speakerText = CreateText("Speaker", caption.transform, 27, TextAlignmentOptions.Left);
        speakerText.color = new Color(1f, .9f, .72f); speakerText.rectTransform.anchorMin = new Vector2(.05f, .50f); speakerText.rectTransform.anchorMax = new Vector2(.95f, .72f); speakerText.rectTransform.offsetMin = speakerText.rectTransform.offsetMax = Vector2.zero;
        bodyText = CreateText("Body", caption.transform, 32, TextAlignmentOptions.TopLeft);
        bodyText.textWrappingMode = TextWrappingModes.Normal; bodyText.rectTransform.anchorMin = new Vector2(.05f, .14f); bodyText.rectTransform.anchorMax = new Vector2(.92f, .55f); bodyText.rectTransform.offsetMin = bodyText.rectTransform.offsetMax = Vector2.zero;
        hintText = CreateText("Hint", caption.transform, 17, TextAlignmentOptions.BottomRight);
        hintText.color = new Color(1f, .88f, .67f); hintText.text = "클릭 · Space · Enter"; hintText.rectTransform.anchorMin = new Vector2(.72f, .02f); hintText.rectTransform.anchorMax = new Vector2(.95f, .20f); hintText.rectTransform.offsetMin = hintText.rectTransform.offsetMax = Vector2.zero;
        root.SetActive(false);
    }

    private void Open(CustomerStoryCutsceneData[] newScenes, Action finished)
    {
        scenes = newScenes;
        onFinished = finished;
        sceneIndex = 0;
        lineIndex = 0;
        wasGameRunning = Managers.Game != null && Managers.Game.isRunning;
        if (Managers.Game != null) Managers.Game.isRunning = false;
        root.SetActive(true);
        openedFrame = Time.frameCount;
        Render();
    }

    private void Update()
    {
        if (root == null || !root.activeSelf || Time.frameCount == openedFrame) return;
        if (GameInput.LeftClickPressed || GameInput.KeyPressed(Key.Space) || GameInput.KeyPressed(Key.Enter) || GameInput.KeyPressed(Key.NumpadEnter))
            Advance();
    }

    private void Advance()
    {
        if (lineIndex + 1 < scenes[sceneIndex].Lines.Length)
        {
            lineIndex++;
            Render();
            return;
        }
        if (sceneIndex + 1 < scenes.Length)
        {
            sceneIndex++;
            lineIndex = 0;
            Render();
            return;
        }
        root.SetActive(false);
        if (Managers.Game != null) Managers.Game.isRunning = wasGameRunning;
        Action callback = onFinished;
        onFinished = null;
        callback?.Invoke();
    }

    private void Render()
    {
        CustomerStoryCutsceneData scene = scenes[sceneIndex];
        CustomerStoryCutsceneLine line = scene.Lines[lineIndex];
        // 컷씬 PNG는 여러 스프라이트 형식으로 임포트되어 있으므로 첫 조각을 명시적으로 가져옵니다.
        Sprite[] sprites = Resources.LoadAll<Sprite>(scene.ResourcePath);
        Sprite sprite = sprites.Length > 0 ? sprites[0] : null;
        if (sprite == null)
        {
            Debug.LogError($"[손님 이야기] 컷씬 이미지를 찾지 못했습니다: {scene.ResourcePath}");
            Advance();
            return;
        }
        artImage.sprite = sprite;
        titleText.text = $"{sceneIndex + 1:00}/{scenes.Length:00} · {scene.Title}";
        speakerText.text = line.Speaker;
        speakerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(line.Speaker));
        bodyText.text = line.Text;
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
        text.fontSize = size; text.color = new Color(1f, .97f, .89f); text.alignment = alignment; text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

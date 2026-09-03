using System;
using TMPro;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 에디터 메뉴와 배치 실행에서 UI_Tutorial.prefab을 같은 구조로 생성합니다.
/// 손으로 하이어라키를 연결하지 않아도 되도록 모든 참조를 여기서 연결합니다.
/// </summary>
public static class TutorialPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/Prefabs/UI/UI_Tutorial.prefab";
    private const int PrefabBuildVersion = 4;
    private const string SessionBuildVersionKey = "Bungeoppang.TutorialPrefabBuildVersion";

    private static readonly Color Ink = new(43f / 255f, 35f / 255f, 32f / 255f, 1f);
    private static readonly Color Paper = new(255f / 255f, 249f / 255f, 235f / 255f, 1f);
    private static readonly Color Orange = new(242f / 255f, 142f / 255f, 58f / 255f, 1f);
    private static readonly Color Purple = new(126f / 255f, 83f / 255f, 151f / 255f, 1f);
    private static readonly Color Dim = new(24f / 255f, 15f / 255f, 12f / 255f, 210f / 255f);

    [DidReloadScripts]
    private static void RebuildAfterScriptReload()
    {
        // CI의 WebGL 빌드는 저장된 프리팹을 사용한다. 여기서 프리팹을 다시 생성하면
        // 에디터 초기화 중 리소스가 변경되어 빌드가 불안정해질 수 있다.
        if (Application.isBatchMode)
            return;

        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetInt(SessionBuildVersionKey, 0) >= PrefabBuildVersion)
                return;

            BuildTutorialPrefab();
            SessionState.SetInt(SessionBuildVersionKey, PrefabBuildVersion);
        };
    }

    [MenuItem("Tools/Bungeoppang/Build Tutorial UI Prefab")]
    public static void BuildTutorialPrefab()
    {
        GameObject root = new("UI_Tutorial", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UI_Tutorial));
        try
        {
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            Image dimTop = CreatePanel("DimTop", root.transform, Dim, true);
            Image dimBottom = CreatePanel("DimBottom", root.transform, Dim, true);
            Image dimLeft = CreatePanel("DimLeft", root.transform, Dim, true);
            Image dimRight = CreatePanel("DimRight", root.transform, Dim, true);
            dimTop.gameObject.SetActive(false);
            dimBottom.gameObject.SetActive(false);
            dimLeft.gameObject.SetActive(false);
            dimRight.gameObject.SetActive(false);

            Image highlight = CreatePanel("HighlightFrame", root.transform, new Color(1f, 1f, 1f, 0f), false);
            Outline highlightOutline = highlight.gameObject.AddComponent<Outline>();
            highlightOutline.effectColor = Orange;
            highlightOutline.effectDistance = new Vector2(6f, -6f);

            TextMeshProUGUI arrowText = CreateText("GuideArrow", root.transform, "↙", 88f, Orange, TextAlignmentOptions.Center);
            arrowText.fontStyle = FontStyles.Bold;
            arrowText.rectTransform.sizeDelta = new Vector2(110f, 110f);

            Image guidePanel = CreatePanel("GuidePanel", root.transform, Paper, false);
            RoundOutline(guidePanel, new Color(100f / 255f, 68f / 255f, 48f / 255f, 1f), 3f);
            SetAnchored(guidePanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 250f));

            Image badge = CreatePanel("StepBadge", guidePanel.transform, new Color(231f / 255f, 218f / 255f, 245f / 255f, 1f), false);
            SetAnchored(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -30f), new Vector2(170f, 42f));
            TextMeshProUGUI step = CreateText("StepText", badge.transform, "1 / 10 · 첫 붕어빵", 19f, Purple, TextAlignmentOptions.Center);
            Stretch(step.rectTransform, 8f, 8f, 4f, 4f);

            TextMeshProUGUI title = CreateText("GuideTitle", guidePanel.transform, "첫 손님의 주문을 받아요", 35f, Ink, TextAlignmentOptions.MidlineLeft);
            SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(34f, -115f), new Vector2(-34f, -58f));

            TextMeshProUGUI description = CreateText("GuideDescription", guidePanel.transform, "손님이 나타나면 손님을 클릭하세요.", 23f, Ink, TextAlignmentOptions.TopLeft);
            description.textWrappingMode = TextWrappingModes.Normal;
            SetAnchoredRect(description.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(34f, 50f), new Vector2(-34f, -124f));

            TextMeshProUGUI progress = CreateText("Progress", guidePanel.transform, "● ○ ○ ○ ○ ○ ○ ○ ○ ○", 24f, Orange, TextAlignmentOptions.MidlineLeft);
            SetAnchoredRect(progress.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(34f, 18f), new Vector2(-34f, 50f));

            Button skip = CreateButton("SkipButton", root.transform, "건너뛰기", Paper, Ink, out _);
            RoundOutline(skip.GetComponent<Image>(), new Color(139f / 255f, 101f / 255f, 62f / 255f, 1f), 2f);
            SetAnchored(skip.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46f, -46f), new Vector2(190f, 66f));
            guidePanel.gameObject.SetActive(false);
            skip.gameObject.SetActive(false);

            Image complete = CreatePanel("CompletePanel", root.transform, Paper, true);
            RoundOutline(complete, Orange, 4f);
            SetAnchored(complete.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 270f));
            TextMeshProUGUI completeTitle = CreateText("CompleteTitle", complete.transform, "튜토리얼 완료!", 44f, Ink, TextAlignmentOptions.Center);
            SetAnchoredRect(completeTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(30f, -112f), new Vector2(-30f, -34f));
            TextMeshProUGUI completeDescription = CreateText("CompleteDescription", complete.transform, "연습은 여기까지예요. 새 가게에서 본 게임을 시작합니다.", 24f, Ink, TextAlignmentOptions.Center);
            completeDescription.textWrappingMode = TextWrappingModes.Normal;
            SetAnchoredRect(completeDescription.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(42f, 42f), new Vector2(-42f, -72f));
            complete.gameObject.SetActive(false);

            Image welcomeDim = CreatePanel("WelcomeDim", root.transform, Dim, true);
            Stretch(welcomeDim.rectTransform);

            Image welcomePanel = CreatePanel("WelcomePanel", root.transform, Paper, true);
            RoundOutline(welcomePanel, Orange, 4f);
            SetAnchored(welcomePanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 440f));

            TextMeshProUGUI welcomeTitle = CreateText("WelcomeTitle", welcomePanel.transform, "가게 문을 열기 전에", 42f, Ink, TextAlignmentOptions.Center);
            SetAnchoredRect(welcomeTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(42f, -120f), new Vector2(-42f, -42f));

            TextMeshProUGUI welcomeDescription = CreateText(
                "WelcomeDescription",
                welcomePanel.transform,
                "처음 오셨다면 첫 팥붕어빵을 함께 만들어 볼까요?\n조리 중에도 언제든 건너뛸 수 있어요.",
                27f,
                Ink,
                TextAlignmentOptions.Center);
            welcomeDescription.textWrappingMode = TextWrappingModes.Normal;
            SetAnchoredRect(welcomeDescription.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(56f, 132f), new Vector2(-56f, -138f));

            Button yesButton = CreateButton("TutorialYesButton", welcomePanel.transform, "네, 배워볼래요", Orange, Color.white, out _);
            SetAnchored(yesButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-155f, 42f), new Vector2(270f, 70f));

            Button noButton = CreateButton("TutorialNoButton", welcomePanel.transform, "아니요, 바로 시작", new Color(112f / 255f, 92f / 255f, 79f / 255f, 1f), Color.white, out _);
            SetAnchored(noButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(155f, 42f), new Vector2(270f, 70f));

            Button nextButton = CreateButton("WelcomeNextButton", welcomePanel.transform, "다음", Orange, Color.white, out TextMeshProUGUI nextLabel);
            nextLabel.name = "WelcomeNextLabel";
            SetAnchored(nextButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(270f, 70f));
            nextButton.gameObject.SetActive(false);

            UI_Tutorial tutorial = root.GetComponent<UI_Tutorial>();
            tutorial.SetReferences(
                rootRect,
                dimTop,
                dimBottom,
                dimLeft,
                dimRight,
                highlight,
                arrowText.rectTransform,
                guidePanel.rectTransform,
                step,
                title,
                description,
                progress,
                skip,
                complete.gameObject,
                welcomeDim.gameObject,
                welcomePanel.gameObject,
                welcomeTitle,
                welcomeDescription,
                yesButton,
                noButton,
                nextButton,
                nextLabel);

            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"튜토리얼 프리팹을 생성했습니다: {PrefabPath}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Tools/Bungeoppang/Reset Tutorial Progress")]
    public static void ResetTutorialProgress()
    {
        PlayerPrefs.DeleteKey("tutorial_completed_v1");
        PlayerPrefs.Save();
        Debug.Log("튜토리얼 진행 기록을 초기화했습니다. 다음 Play에서 선택 패널이 다시 표시됩니다.");
    }

    private static Image CreatePanel(string name, Transform parent, Color color, bool raycastTarget)
    {
        GameObject value = CreateUiObject(name, parent);
        Image image = value.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(16f, fontSize - 8f);
        text.fontSizeMax = fontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color background, Color foreground, out TextMeshProUGUI labelText)
    {
        Image image = CreatePanel(name, parent, background, true);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.82f, 1f);
        colors.selectedColor = new Color(1f, 0.94f, 0.82f, 1f);
        colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        labelText = CreateText("Label", image.transform, label, 24f, foreground, TextAlignmentOptions.Center);
        Stretch(labelText.rectTransform, 12f, 12f, 6f, 6f);
        return button;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject value = new(name, typeof(RectTransform));
        value.transform.SetParent(parent, false);
        return value;
    }

    private static void RoundOutline(Image image, Color color, float width)
    {
        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(width, -width);
    }

    private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
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
}

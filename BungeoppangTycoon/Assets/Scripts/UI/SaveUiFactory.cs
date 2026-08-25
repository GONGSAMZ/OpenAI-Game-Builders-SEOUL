using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SaveUiFactory
{
    private static readonly Color Paper = new(1f, .97f, .87f);
    private static readonly Color Ink = new(.20f, .15f, .12f);
    private static readonly Color Blue = new(.28f, .58f, .73f);
    private static readonly Color Danger = new(.58f, .19f, .16f);

    public static void ShowResetConfirmation()
    {
        if (GameObject.Find("UI_GameResetConfirmation") != null) return;
        GameObject root = CreateRoot("UI_GameResetConfirmation");
        Image panel = CreatePanel(root.transform, new Vector2(820, 520));
        Text("TitleText", panel.transform, "게임 플레이 초기화", 42, TextAlignmentOptions.Center,
            new Vector2(.07f, .77f), new Vector2(.93f, .94f));
        TextMeshProUGUI body = Text("BodyText", panel.transform,
            "1일차, 보유금, 일반 재료 해금과 진행 중인 영업이 삭제됩니다.\n\n스토리, 도감, 영혼, 업적, 누적 기록과 구매 아이템은 유지됩니다.",
            28, TextAlignmentOptions.Center, new Vector2(.09f, .31f), new Vector2(.91f, .76f));
        Button cancel = Button("CancelButton", panel.transform, "취소", Blue,
            new Vector2(.18f, .08f), new Vector2(.47f, .22f));
        Button confirm = Button("ConfirmButton", panel.transform, "초기화하기", Danger,
            new Vector2(.53f, .08f), new Vector2(.82f, .22f));
        UI_GameResetConfirmation controller = root.AddComponent<UI_GameResetConfirmation>();
        controller.Configure(body, cancel, confirm);
        Select(confirm);
    }

    public static void ShowAchievements()
    {
        if (GameObject.Find("UI_AchievementsRuntime") != null) return;
        GameObject root = CreateRoot("UI_AchievementsRuntime");
        Image panel = CreatePanel(root.transform, new Vector2(980, 860));
        Text("TitleText", panel.transform, "업적", 44, TextAlignmentOptions.Center,
            new Vector2(.08f, .87f), new Vector2(.92f, .97f));

        GameObject list = new("AchievementList", typeof(RectTransform), typeof(VerticalLayoutGroup));
        list.transform.SetParent(panel.transform, false);
        RectTransform listRect = list.GetComponent<RectTransform>();
        SetAnchors(listRect, new Vector2(.08f, .14f), new Vector2(.92f, .86f));
        VerticalLayoutGroup layout = list.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = true;

        foreach (AchievementDefinition definition in AchievementCatalog.Entries)
        {
            AchievementProgressData state = SaveService.Data.account.achievements.Find(value => value.achievementId == definition.Id);
            long progress = state?.progress ?? 0;
            bool unlocked = state?.unlocked ?? false;
            Image row = Image(definition.Id, list.transform, unlocked ? new Color(.94f, .78f, .36f) : new Color(.78f, .74f, .66f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
            string mark = unlocked ? "완료" : $"{Mathf.Min(progress, definition.Target)}/{definition.Target}";
            Text("Label", row.transform, $"{definition.DisplayName}  ·  {definition.Description}    {mark}", 23,
                TextAlignmentOptions.MidlineLeft, new Vector2(.04f, 0), new Vector2(.96f, 1));
        }

        Button close = Button("CloseButton", panel.transform, "돌아가기", Blue,
            new Vector2(.37f, .035f), new Vector2(.63f, .115f));
        close.onClick.AddListener(() => Object.Destroy(root));
        Select(close);
    }

    private static GameObject CreateRoot(string name)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // UI_Settings(정렬 순서 200) 위에 표시해야 하므로 별도 생성 팝업도
        // UIManager 팝업과 같은 상위 순서를 명시적으로 사용합니다.
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1200;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        Image dim = Image("Dim", root.transform, new Color(.08f, .05f, .04f, .78f));
        SetAnchors(dim.rectTransform, Vector2.zero, Vector2.one);
        return root;
    }

    private static Image CreatePanel(Transform parent, Vector2 size)
    {
        Image panel = Image("Panel", parent, Paper);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.23f, .47f, .63f);
        outline.effectDistance = new Vector2(5, -5);
        return panel;
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, float size,
        TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.color = Ink;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.text = value;
        SetAnchors(text.rectTransform, anchorMin, anchorMax);
        return text;
    }

    private static Button Button(string name, Transform parent, string label, Color color,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        Image image = Image(name, parent, color);
        SetAnchors(image.rectTransform, anchorMin, anchorMax);
        Button button = image.gameObject.AddComponent<Button>();
        TextMeshProUGUI text = Text("Label", image.transform, label, 27, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
        text.color = new Color(1f, .98f, .9f);
        return button;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Select(Button button)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}

public sealed class UI_GameResetConfirmation : MonoBehaviour
{
    private TextMeshProUGUI bodyText;
    private Button cancelButton;
    private Button confirmButton;
    private bool processing;

    public void Configure(TextMeshProUGUI body, Button cancel, Button confirm)
    {
        bodyText = body;
        cancelButton = cancel;
        confirmButton = confirm;
        cancelButton.onClick.AddListener(Cancel);
        confirmButton.onClick.AddListener(Confirm);
    }

    private void Cancel()
    {
        if (!processing) Destroy(gameObject);
    }

    private void Confirm()
    {
        if (processing) return;
        processing = true;
        cancelButton.interactable = false;
        confirmButton.interactable = false;
        bodyText.text = "저장 데이터를 안전하게 초기화하는 중입니다…";
        SaveService.Service.ResetRunProgress(OnResetCompleted);
    }

    private void OnResetCompleted(bool success, string message)
    {
        if (success)
        {
            Managers.Game.PrepareForSceneReload();
            SceneManager.LoadScene("IntroScene");
            return;
        }
        processing = false;
        cancelButton.interactable = true;
        confirmButton.interactable = true;
        bodyText.text = string.IsNullOrWhiteSpace(message)
            ? "초기화하지 못했습니다. 네트워크 상태를 확인하고 다시 시도해 주세요."
            : message;
    }
}

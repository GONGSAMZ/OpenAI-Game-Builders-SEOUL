using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 낮 대화 동안 다른 화면 입력을 막고, 대화 손님과 세 개의 선택지만 또렷하게 보여 줍니다.
/// </summary>
public sealed class UI_CustomerStoryChoices : MonoBehaviour
{
    private const string PrefabPath = "Prefabs/UI/UI_CustomerStoryChoices";
    private const float ReferenceLeftMargin = 64f;

    private static UI_CustomerStoryChoices instance;

    private readonly Button[] choiceButtons = new Button[3];
    private GameObject focusRoot;
    private GameObject choicePanel;
    private GameObject replyBubble;
    private RectTransform choicePanelRect;
    private RectTransform focusedCustomerRect;
    private RectTransform replyBubbleRect;
    private Image focusedCustomerImage;
    private TextMeshProUGUI replyText;
    private CustomerController currentCustomer;
    private Coroutine replyRoutine;
    private bool wasGameRunning;
    private bool isVisible;
    private bool isReplyVisible;

    public static void Show(CustomerController customer, CustomerStoryData story)
    {
        UI_CustomerStoryChoices view = Ensure();
        if (view == null)
        {
            customer?.CancelStoryDialogueSelection();
            return;
        }

        view.ShowChoicesInternal(customer, story);
    }

    public static void ShowReply(CustomerController customer, string reply, float duration)
    {
        if (instance == null || !instance.isVisible || instance.currentCustomer != customer)
        {
            Debug.LogWarning("[손님 이야기] 선택지 화면이 없는 상태에서 답변을 표시하려 했습니다.", customer);
            customer?.OnStoryReplyFinished();
            return;
        }

        instance.ShowReplyInternal(reply, duration);
    }

    public static void Hide()
    {
        if (instance != null)
            instance.HideInternal(true);
    }

    private static UI_CustomerStoryChoices Ensure()
    {
        if (instance != null)
            return instance;

        UI_Game gameUI = FindFirstObjectByType<UI_Game>();
        if (gameUI == null)
        {
            Debug.LogError("[손님 이야기] 낮 대화 선택지를 붙일 UI_Game을 찾지 못했습니다.");
            return null;
        }

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[손님 이야기] 낮 대화 선택지 프리팹을 찾지 못했습니다: {PrefabPath}");
            return null;
        }

        GameObject root = Instantiate(prefab, gameUI.transform, false);
        instance = root.GetComponent<UI_CustomerStoryChoices>();
        if (instance == null)
        {
            Debug.LogError("[손님 이야기] 선택지 프리팹에 UI_CustomerStoryChoices가 없습니다.", root);
            Destroy(root);
            return null;
        }

        instance.BuildIfNeeded();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildIfNeeded();
    }

    private void Update()
    {
        if (!isVisible)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isReplyVisible)
                currentCustomer?.OnStoryReplyFinished();
            else
                currentCustomer?.CancelStoryDialogueSelection();
            return;
        }

        if (isReplyVisible)
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectChoice(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectChoice(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectChoice(2);
    }

    private void ShowChoicesInternal(CustomerController customer, CustomerStoryData story)
    {
        if (customer == null || story?.Topics == null || story.Topics.Length < choiceButtons.Length)
        {
            Debug.LogError("[손님 이야기] 선택지를 표시할 손님 또는 대화 주제 세 개가 올바르지 않습니다.", customer);
            customer?.CancelStoryDialogueSelection();
            return;
        }

        if (isVisible && currentCustomer != customer)
            HideInternal(true);

        currentCustomer = customer;
        wasGameRunning = Managers.Game.isRunning;
        Managers.Game.isRunning = false;
        isReplyVisible = false;

        UpdateFocusedCustomer(customer);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool completed = CustomerStoryProgress.CompletedTopics.Contains(i);
            TextMeshProUGUI label = choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>(true);
            label.text = $"{i + 1}. {(completed ? "(완료) " : string.Empty)}{story.Topics[i].Choice}";
            choiceButtons[i].gameObject.SetActive(true);
        }

        ApplySafeArea();
        focusRoot.SetActive(true);
        choicePanel.SetActive(true);
        replyBubble.SetActive(false);
        isVisible = true;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(choiceButtons[0].gameObject);

        Debug.Log($"[손님 이야기] 낮 대화 집중 화면 시작 | 손님={story.DisplayName} | 게임 시간 정지=예", customer);
    }

    private void ShowReplyInternal(string reply, float duration)
    {
        if (replyRoutine != null)
            StopCoroutine(replyRoutine);

        choicePanel.SetActive(false);
        replyText.text = reply;
        PositionReplyBubble();
        replyBubble.SetActive(true);
        isReplyVisible = true;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        replyRoutine = StartCoroutine(CoFinishReply(Mathf.Max(.1f, duration)));
    }

    private IEnumerator CoFinishReply(float duration)
    {
        // 게임 시간이 멈춘 상태에서도 손님의 답변은 실제 5초 뒤 끝나야 한다.
        yield return new WaitForSecondsRealtime(duration);
        replyRoutine = null;
        currentCustomer?.OnStoryReplyFinished();
    }

    private void SelectChoice(int index)
    {
        if (!isVisible || isReplyVisible || index < 0 || index >= choiceButtons.Length)
            return;

        currentCustomer?.SelectStoryTopic(index);
    }

    private void HideInternal(bool restoreGame)
    {
        if (replyRoutine != null)
        {
            StopCoroutine(replyRoutine);
            replyRoutine = null;
        }

        if (focusRoot != null)
            focusRoot.SetActive(false);

        if (restoreGame && isVisible)
            Managers.Game.isRunning = wasGameRunning;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        currentCustomer = null;
        isVisible = false;
        isReplyVisible = false;
    }

    private void BuildIfNeeded()
    {
        if (focusRoot != null)
            return;

        RectTransform rootRect = transform as RectTransform;
        Stretch(rootRect);

        focusRoot = new GameObject("ConversationFocus", typeof(RectTransform));
        focusRoot.layer = LayerMask.NameToLayer("UI");
        focusRoot.transform.SetParent(transform, false);
        Stretch(focusRoot.GetComponent<RectTransform>());

        // 이 반투명 막은 뒤 화면을 차분하게 만들고, 동시에 다른 모든 클릭을 가로막는다.
        Image blocker = CreateImage("DesaturatedInteractionBlocker", focusRoot.transform);
        Stretch(blocker.rectTransform);
        blocker.color = new Color(.07f, .09f, .14f, .76f);
        blocker.raycastTarget = true;

        focusedCustomerImage = CreateImage("FocusedCustomer", focusRoot.transform);
        focusedCustomerRect = focusedCustomerImage.rectTransform;
        focusedCustomerImage.preserveAspect = true;
        focusedCustomerImage.raycastTarget = false;

        choicePanel = new GameObject("ChoicePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        choicePanel.layer = LayerMask.NameToLayer("UI");
        choicePanel.transform.SetParent(focusRoot.transform, false);
        choicePanelRect = choicePanel.GetComponent<RectTransform>();
        choicePanelRect.anchorMin = choicePanelRect.anchorMax = new Vector2(0f, .5f);
        choicePanelRect.pivot = new Vector2(0f, .5f);
        choicePanelRect.anchoredPosition = new Vector2(ReferenceLeftMargin, 0f);
        choicePanelRect.sizeDelta = new Vector2(460f, 690f);

        Image panelImage = choicePanel.GetComponent<Image>();
        panelImage.sprite = Managers.Resource.LoadSprite("UI/StoryChoicePanel3-v1", 0);
        panelImage.preserveAspect = true;
        panelImage.raycastTarget = false;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("omyuPretty SDF") ?? TMP_Settings.defaultFontAsset;
        float[] slotY = { 210f, -10f, -230f };

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            GameObject buttonObject = new($"Choice{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.layer = LayerMask.NameToLayer("UI");
            buttonObject.transform.SetParent(choicePanel.transform, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.5f, .5f);
            buttonRect.pivot = new Vector2(.5f, .5f);
            buttonRect.anchoredPosition = new Vector2(0f, slotY[i]);
            buttonRect.sizeDelta = new Vector2(404f, 174f);

            Image hitArea = buttonObject.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, .001f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, .001f);
            colors.highlightedColor = new Color(.36f, .68f, .88f, .20f);
            colors.selectedColor = new Color(.95f, .70f, .25f, .18f);
            colors.pressedColor = new Color(.29f, .60f, .82f, .28f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            button.onClick.AddListener(() => SelectChoice(index));
            choiceButtons[i] = button;

            TextMeshProUGUI label = CreateText("Label", buttonObject.transform, font);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(24f, 12f);
            label.rectTransform.offsetMax = new Vector2(-24f, -12f);
            label.fontSize = 38f;
            label.enableAutoSizing = false;
            label.fontStyle = FontStyles.Normal;
            label.color = new Color(.22f, .15f, .11f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.characterSpacing = 1f;
            label.raycastTarget = false;
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Navigation navigation = new() { mode = Navigation.Mode.Explicit };
            navigation.selectOnUp = choiceButtons[(i + choiceButtons.Length - 1) % choiceButtons.Length];
            navigation.selectOnDown = choiceButtons[(i + 1) % choiceButtons.Length];
            choiceButtons[i].navigation = navigation;
        }

        replyBubble = new GameObject("CustomerReplyBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        replyBubble.layer = LayerMask.NameToLayer("UI");
        replyBubble.transform.SetParent(focusRoot.transform, false);
        replyBubbleRect = replyBubble.GetComponent<RectTransform>();
        replyBubbleRect.anchorMin = replyBubbleRect.anchorMax = new Vector2(.5f, .5f);
        replyBubbleRect.pivot = new Vector2(.5f, .5f);
        replyBubbleRect.sizeDelta = new Vector2(650f, 270f);

        Image replyImage = replyBubble.GetComponent<Image>();
        replyImage.sprite = Managers.Resource.LoadSprite("UI/DialogueBallon", 0);
        replyImage.type = Image.Type.Sliced;
        replyImage.raycastTarget = false;

        replyText = CreateText("ReplyText", replyBubble.transform, font);
        Stretch(replyText.rectTransform);
        replyText.rectTransform.offsetMin = new Vector2(82f, 58f);
        replyText.rectTransform.offsetMax = new Vector2(-82f, -58f);
        replyText.fontSize = 30f;
        replyText.enableAutoSizing = true;
        replyText.fontSizeMin = 24f;
        replyText.fontSizeMax = 32f;
        replyText.color = new Color(.22f, .15f, .11f, 1f);
        replyText.alignment = TextAlignmentOptions.Center;
        replyText.textWrappingMode = TextWrappingModes.Normal;
        replyText.raycastTarget = false;

        focusRoot.SetActive(false);
    }

    private void UpdateFocusedCustomer(CustomerController customerController)
    {
        SpriteRenderer renderer = customerController.StoryFocusRenderer;
        Camera worldCamera = Camera.main;
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform rootRect = transform as RectTransform;

        if (renderer == null || renderer.sprite == null || worldCamera == null || canvas == null)
        {
            focusedCustomerImage.gameObject.SetActive(false);
            Debug.LogWarning("[손님 이야기] 대화 손님의 강조 이미지를 만들 화면 정보를 찾지 못했습니다.", customerController);
            return;
        }

        Vector3 minScreen = worldCamera.WorldToScreenPoint(renderer.bounds.min);
        Vector3 maxScreen = worldCamera.WorldToScreenPoint(renderer.bounds.max);
        Vector2 screenCenter = (minScreen + maxScreen) * .5f;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenCenter, uiCamera, out Vector2 localCenter);

        float scaleFactor = Mathf.Max(.01f, canvas.scaleFactor);
        Vector2 size = new(Mathf.Abs(maxScreen.x - minScreen.x) / scaleFactor, Mathf.Abs(maxScreen.y - minScreen.y) / scaleFactor);
        focusedCustomerRect.anchorMin = focusedCustomerRect.anchorMax = new Vector2(.5f, .5f);
        focusedCustomerRect.pivot = new Vector2(.5f, .5f);
        focusedCustomerRect.anchoredPosition = localCenter;
        focusedCustomerRect.sizeDelta = size;
        focusedCustomerRect.localScale = new Vector3(renderer.flipX ? -1f : 1f, renderer.flipY ? -1f : 1f, 1f);
        focusedCustomerImage.sprite = renderer.sprite;
        focusedCustomerImage.color = Color.white;
        focusedCustomerImage.gameObject.SetActive(true);
    }

    private void PositionReplyBubble()
    {
        Rect rootBounds = (transform as RectTransform).rect;
        Vector2 customerPosition = focusedCustomerRect.anchoredPosition;
        Vector2 customerSize = focusedCustomerRect.sizeDelta;
        Vector2 bubbleSize = replyBubbleRect.sizeDelta;

        float x = customerPosition.x - customerSize.x * .55f - bubbleSize.x * .5f - 28f;
        if (x - bubbleSize.x * .5f < rootBounds.xMin + 24f)
            x = customerPosition.x + customerSize.x * .55f + bubbleSize.x * .5f + 28f;

        float y = customerPosition.y + customerSize.y * .18f;
        x = Mathf.Clamp(x, rootBounds.xMin + bubbleSize.x * .5f + 24f, rootBounds.xMax - bubbleSize.x * .5f - 24f);
        y = Mathf.Clamp(y, rootBounds.yMin + bubbleSize.y * .5f + 24f, rootBounds.yMax - bubbleSize.y * .5f - 24f);
        replyBubbleRect.anchoredPosition = new Vector2(x, y);
    }

    private void ApplySafeArea()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        float scaleFactor = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        float safeInset = Screen.safeArea.xMin / scaleFactor;
        choicePanelRect.anchoredPosition = new Vector2(Mathf.Max(ReferenceLeftMargin, safeInset + 32f), 0f);
    }

    private static Image CreateImage(string objectName, Transform parent)
    {
        GameObject target = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        target.layer = LayerMask.NameToLayer("UI");
        target.transform.SetParent(parent, false);
        return target.GetComponent<Image>();
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, TMP_FontAsset font)
    {
        GameObject target = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        target.layer = LayerMask.NameToLayer("UI");
        target.transform.SetParent(parent, false);
        TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
        text.font = font;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (isVisible)
            Managers.Game.isRunning = wasGameRunning;

        if (instance == this)
            instance = null;
    }
}

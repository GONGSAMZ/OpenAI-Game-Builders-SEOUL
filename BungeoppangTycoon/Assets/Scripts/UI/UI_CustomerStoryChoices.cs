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

    // 이 참조들은 UI_CustomerStoryChoices.prefab에 직접 저장된다.
    // 실행 중 UI를 새로 만들지 않으므로, Inspector에서 카드 크기와 여백을 바로 조정할 수 있다.
    [SerializeField] private GameObject focusRoot;
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private GameObject replyBubble;
    [SerializeField] private RectTransform choicePanelRect;
    [SerializeField] private RectTransform focusedCustomerRect;
    [SerializeField] private RectTransform replyBubbleRect;
    [SerializeField] private Image focusedCustomerImage;
    [SerializeField] private TextMeshProUGUI replyText;
    [SerializeField] private Button[] choiceButtons = new Button[3];
    [SerializeField] private TextMeshProUGUI[] choiceLabels = new TextMeshProUGUI[3];
    private CustomerController currentCustomer;
    // 선택지 버튼을 누른 바로 그 클릭으로 답변 말풍선까지 닫히지 않게 구분한다.
    private int replyOpenedFrame = -1;
    private bool wasGameRunning;
    private bool isVisible;
    private bool isReplyVisible;
    private readonly int[] visibleTopicIndices = { -1, -1, -1 };

#if UNITY_EDITOR
    /// <summary>프리팹 생성기가 실제 UI 자식들을 이 컴포넌트에 저장할 때 사용한다.</summary>
    public void SetPrefabReferences(
        GameObject newFocusRoot,
        GameObject newChoicePanel,
        GameObject newReplyBubble,
        Image newFocusedCustomerImage,
        TextMeshProUGUI newReplyText,
        Button[] newChoiceButtons,
        TextMeshProUGUI[] newChoiceLabels)
    {
        focusRoot = newFocusRoot;
        choicePanel = newChoicePanel;
        replyBubble = newReplyBubble;
        focusedCustomerImage = newFocusedCustomerImage;
        choicePanelRect = newChoicePanel.GetComponent<RectTransform>();
        replyBubbleRect = newReplyBubble.GetComponent<RectTransform>();
        focusedCustomerRect = newFocusedCustomerImage.rectTransform;
        replyText = newReplyText;
        choiceButtons = newChoiceButtons;
        choiceLabels = newChoiceLabels;
    }
#endif

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

    public static void ShowReply(CustomerController customer, string reply)
    {
        if (instance == null || !instance.isVisible || instance.currentCustomer != customer)
        {
            Debug.LogWarning("[손님 이야기] 선택지 화면이 없는 상태에서 답변을 표시하려 했습니다.", customer);
            customer?.OnStoryReplyFinished();
            return;
        }

        instance.ShowReplyInternal(reply);
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

        return instance.BindPrefabIfNeeded() ? instance : null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BindPrefabIfNeeded();
    }

    private void Update()
    {
        if (!isVisible)
            return;

        if (isReplyVisible)
        {
            bool clickedAfterOpening = Input.GetMouseButtonDown(0) && Time.frameCount > replyOpenedFrame;
            bool confirmed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape);
            if (clickedAfterOpening || confirmed)
                currentCustomer?.OnStoryReplyFinished();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            currentCustomer?.CancelStoryDialogueSelection();
            return;
        }

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
        isVisible = true;
        isReplyVisible = false;

        UpdateFocusedCustomer(customer);

        int visibleChoiceCount = 0;
        for (int topicIndex = 0; topicIndex < story.Topics.Length && visibleChoiceCount < choiceButtons.Length; topicIndex++)
        {
            if (CustomerStoryProgress.CompletedTopics.Contains(topicIndex))
                continue;

            visibleTopicIndices[visibleChoiceCount] = topicIndex;
            choiceLabels[visibleChoiceCount].text = $"{visibleChoiceCount + 1}. {story.Topics[topicIndex].Choice}";
            choiceButtons[visibleChoiceCount].gameObject.SetActive(true);
            visibleChoiceCount++;
        }

        for (int slot = visibleChoiceCount; slot < choiceButtons.Length; slot++)
        {
            visibleTopicIndices[slot] = -1;
            choiceButtons[slot].gameObject.SetActive(false);
        }

        if (visibleChoiceCount == 0)
        {
            Debug.LogWarning("[손님 이야기] 표시할 미완료 대화 주제가 없습니다.", customer);
            customer.CancelStoryDialogueSelection();
            return;
        }

        UpdateVisibleNavigation(visibleChoiceCount);

        ApplySafeArea();
        focusRoot.SetActive(true);
        choicePanel.SetActive(true);
        replyBubble.SetActive(false);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(choiceButtons[0].gameObject);

        Debug.Log($"[손님 이야기] 낮 대화 집중 화면 시작 | 손님={story.DisplayName} | 게임 시간 정지=예", customer);
    }

    private void ShowReplyInternal(string reply)
    {
        choicePanel.SetActive(false);
        replyText.text = reply;
        PositionReplyBubble();
        replyBubble.SetActive(true);
        isReplyVisible = true;
        replyOpenedFrame = Time.frameCount;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

    }

    private void SelectChoice(int index)
    {
        if (!isVisible || isReplyVisible || index < 0 || index >= visibleTopicIndices.Length)
            return;

        int topicIndex = visibleTopicIndices[index];
        if (topicIndex >= 0)
            currentCustomer?.SelectStoryTopic(topicIndex);
    }

    private void UpdateVisibleNavigation(int visibleChoiceCount)
    {
        for (int slot = 0; slot < choiceButtons.Length; slot++)
        {
            Navigation navigation = new() { mode = Navigation.Mode.None };
            if (slot < visibleChoiceCount)
            {
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = choiceButtons[(slot + visibleChoiceCount - 1) % visibleChoiceCount];
                navigation.selectOnDown = choiceButtons[(slot + 1) % visibleChoiceCount];
            }

            choiceButtons[slot].navigation = navigation;
        }
    }

    private void HideInternal(bool restoreGame)
    {
        if (focusRoot != null)
            focusRoot.SetActive(false);

        if (restoreGame && isVisible)
            Managers.Game.isRunning = wasGameRunning;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        currentCustomer = null;
        isVisible = false;
        isReplyVisible = false;
        replyOpenedFrame = -1;
        for (int i = 0; i < visibleTopicIndices.Length; i++)
            visibleTopicIndices[i] = -1;
    }

    /// <summary>
    /// 프리팹에 저장된 자식과 참조를 확인하고, 버튼 동작만 연결한다.
    /// 빠진 참조는 이름으로 한 번 보완해 이전 프리팹 인스턴스도 안전하게 처리한다.
    /// </summary>
    private bool BindPrefabIfNeeded()
    {
        if (focusRoot == null)
            focusRoot = transform.Find("ConversationFocus")?.gameObject;

        if (focusRoot != null)
        {
            choicePanel ??= focusRoot.transform.Find("ChoicePanel")?.gameObject;
            replyBubble ??= focusRoot.transform.Find("CustomerReplyBubble")?.gameObject;
            focusedCustomerImage ??= focusRoot.transform.Find("FocusedCustomer")?.GetComponent<Image>();
        }

        choicePanelRect ??= choicePanel != null ? choicePanel.GetComponent<RectTransform>() : null;
        replyBubbleRect ??= replyBubble != null ? replyBubble.GetComponent<RectTransform>() : null;
        focusedCustomerRect ??= focusedCustomerImage != null ? focusedCustomerImage.rectTransform : null;
        replyText ??= replyBubble != null ? replyBubble.GetComponentInChildren<TextMeshProUGUI>(true) : null;

        if (choicePanel != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                Transform card = choicePanel.transform.Find($"Choice{i + 1}");
                choiceButtons[i] ??= card?.GetComponent<Button>();
                choiceLabels[i] ??= card?.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (focusRoot == null || choicePanelRect == null || replyBubbleRect == null || focusedCustomerImage == null || replyText == null ||
            choiceButtons.Any(button => button == null) || choiceLabels.Any(label => label == null))
        {
            Debug.LogError("[손님 이야기] UI_CustomerStoryChoices 프리팹의 필수 오브젝트가 빠져 있습니다. Tools/Bungeoppang/Build Customer Story Choices Prefab을 실행해 복구하세요.", this);
            return false;
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            choiceButtons[i].onClick.RemoveAllListeners();
            choiceButtons[i].onClick.AddListener(() => SelectChoice(index));

            Navigation navigation = new() { mode = Navigation.Mode.Explicit };
            navigation.selectOnUp = choiceButtons[(i + choiceButtons.Length - 1) % choiceButtons.Length];
            navigation.selectOnDown = choiceButtons[(i + 1) % choiceButtons.Length];
            choiceButtons[i].navigation = navigation;
        }

        focusRoot.SetActive(false);
        return true;
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

    private void OnDestroy()
    {
        if (isVisible)
            Managers.Game.isRunning = wasGameRunning;

        if (instance == this)
            instance = null;
    }
}

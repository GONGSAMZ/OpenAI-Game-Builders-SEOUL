using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 첫 팥붕어빵 조리 행동을 따라가는 게임 내 튜토리얼 오버레이입니다.
/// 실제 조리 성공 신호를 받았을 때만 다음 단계로 이동합니다.
/// </summary>
public class UI_Tutorial : UI_Base
{
    private const int TotalSteps = 12;

    private enum Stage
    {
        CustomerOrder,
        SwitchToCooking,
        SelectKettle,
        FillMold,
        SelectRedBean,
        AddFilling,
        SelectKettleAgain,
        AddTopBatter,
        Bake,
        MoveToDisplay,
        SwitchToCustomer,
        ServeCustomer,
    }

    public static bool IsRunning { get; private set; }
    public static bool IsBlockingFirstCustomer => IsRunning && tutorialStarted == false;
    public static bool AllowsManualViewSwitch =>
        IsRunning == false ||
        (activeInstance != null && tutorialStarted &&
         (activeInstance.stage == Stage.SwitchToCooking || activeInstance.stage == Stage.SwitchToCustomer));

    private static bool forcedOrderIssued;
    private static bool promptRequestedForGameStart;
    private static UI_Tutorial activeInstance;

    [Header("Overlay")]
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private Image dimTop;
    [SerializeField] private Image dimBottom;
    [SerializeField] private Image dimLeft;
    [SerializeField] private Image dimRight;
    [SerializeField] private Image highlightFrame;
    [SerializeField] private RectTransform guideArrow;

    [Header("Guide")]
    [SerializeField] private RectTransform guidePanel;
    [SerializeField] private TextMeshProUGUI stepText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Button skipButton;
    [SerializeField] private GameObject completePanel;

    [Header("Welcome")]
    [SerializeField] private GameObject welcomeDim;
    [SerializeField] private GameObject welcomePanel;
    [SerializeField] private TextMeshProUGUI welcomeTitleText;
    [SerializeField] private TextMeshProUGUI welcomeDescriptionText;
    [SerializeField] private Button tutorialYesButton;
    [SerializeField] private Button tutorialNoButton;
    [SerializeField] private Button welcomeNextButton;
    [SerializeField] private TextMeshProUGUI welcomeNextLabel;

    private Stage stage;
    private GameObject currentTarget;
    private GameObject currentFishBun;
    private bool hasInitialized;
    private bool firstBakeFinished;
    private static bool tutorialStarted;
    private int welcomePage;

    public static bool ShouldShow()
    {
        if (IsRunning)
            return false;

        if (promptRequestedForGameStart)
        {
            promptRequestedForGameStart = false;
            return true;
        }

        return SaveService.Data.settings.tutorialCompleted == false;
    }

    /// <summary>
    /// 인트로에서 게임을 시작할 때 튜토리얼 진행 여부를 한 번 묻도록 예약합니다.
    /// 튜토리얼 완료 후 GameScene을 다시 불러올 때는 이 요청이 이미 소비되어 반복 표시되지 않습니다.
    /// </summary>
    public static void RequestPromptForGameStart()
    {
        promptRequestedForGameStart = true;
    }

    /// <summary>
    /// 첫 손님 주문만 팥붕어빵 1개로 고정합니다.
    /// CustomerController가 한 번만 호출합니다.
    /// </summary>
    public static bool TryConsumeForcedRedBeanOrder()
    {
        if (IsRunning == false || tutorialStarted == false || forcedOrderIssued)
            return false;

        forcedOrderIssued = true;
        return true;
    }

    public void SetReferences(
        RectTransform root,
        Image top,
        Image bottom,
        Image left,
        Image right,
        Image highlight,
        RectTransform arrow,
        RectTransform panel,
        TextMeshProUGUI step,
        TextMeshProUGUI title,
        TextMeshProUGUI description,
        TextMeshProUGUI progress,
        Button skip,
        GameObject complete,
        GameObject consentDim,
        GameObject consentPanel,
        TextMeshProUGUI consentTitle,
        TextMeshProUGUI consentDescription,
        Button yesButton,
        Button noButton,
        Button nextButton,
        TextMeshProUGUI nextLabel)
    {
        overlayRoot = root;
        dimTop = top;
        dimBottom = bottom;
        dimLeft = left;
        dimRight = right;
        highlightFrame = highlight;
        guideArrow = arrow;
        guidePanel = panel;
        stepText = step;
        titleText = title;
        descriptionText = description;
        progressText = progress;
        skipButton = skip;
        completePanel = complete;
        welcomeDim = consentDim;
        welcomePanel = consentPanel;
        welcomeTitleText = consentTitle;
        welcomeDescriptionText = consentDescription;
        tutorialYesButton = yesButton;
        tutorialNoButton = noButton;
        welcomeNextButton = nextButton;
        welcomeNextLabel = nextLabel;
    }

    protected override void Init()
    {
        if (hasInitialized)
            return;

        hasInitialized = true;
        activeInstance = this;
        IsRunning = true;
        forcedOrderIssued = false;
        tutorialStarted = false;
        Managers.Game.IsTutorialClockPaused = true;

        if (skipButton != null)
            skipButton.onClick.AddListener(Skip);
        if (tutorialYesButton != null)
            tutorialYesButton.onClick.AddListener(AcceptTutorial);
        if (tutorialNoButton != null)
            tutorialNoButton.onClick.AddListener(DeclineTutorial);
        if (welcomeNextButton != null)
            welcomeNextButton.onClick.AddListener(AdvanceWelcome);

        TutorialSignals.Raised += OnTutorialEvent;
        StartCoroutine(BeginAfterWorldIsReady());
    }

    private IEnumerator BeginAfterWorldIsReady()
    {
        // UI_Game이 생성한 직후라 월드 오브젝트와 카메라가 준비될 시간을 한 프레임 줍니다.
        yield return null;
        ShowTutorialConsent();
    }

    private void LateUpdate()
    {
        if (tutorialStarted == false)
        {
            SetSpotlightVisible(false);
            return;
        }

        if (currentTarget == null)
            currentTarget = ResolveTarget();

        UpdateSpotlight();
    }

    private void OnDestroy()
    {
        TutorialSignals.Raised -= OnTutorialEvent;

        if (IsRunning)
            Managers.Game.IsTutorialClockPaused = false;

        IsRunning = false;
        tutorialStarted = false;
        if (activeInstance == this)
            activeInstance = null;
    }

    private void ShowTutorialConsent()
    {
        welcomePage = 0;
        SetSpotlightVisible(false);
        SetGuideVisible(false);

        if (welcomeDim != null)
            welcomeDim.SetActive(true);
        if (welcomePanel != null)
            welcomePanel.SetActive(true);
        if (welcomeTitleText != null)
            welcomeTitleText.text = "가게 문을 열기 전에";
        if (welcomeDescriptionText != null)
            welcomeDescriptionText.text = "처음 오셨다면 첫 팥붕어빵을 함께 만들어 볼까요?\n조리 중에도 언제든 건너뛸 수 있어요.";
        if (tutorialYesButton != null)
            tutorialYesButton.gameObject.SetActive(true);
        if (tutorialNoButton != null)
            tutorialNoButton.gameObject.SetActive(true);
        if (welcomeNextButton != null)
            welcomeNextButton.gameObject.SetActive(false);

        if (EventSystem.current != null && tutorialYesButton != null)
            EventSystem.current.SetSelectedGameObject(tutorialYesButton.gameObject);
    }

    private void AcceptTutorial()
    {
        welcomePage = 1;
        if (tutorialYesButton != null)
            tutorialYesButton.gameObject.SetActive(false);
        if (tutorialNoButton != null)
            tutorialNoButton.gameObject.SetActive(false);
        if (welcomeNextButton != null)
            welcomeNextButton.gameObject.SetActive(true);

        ShowWelcomePage();
    }

    private void AdvanceWelcome()
    {
        if (welcomePage == 1)
        {
            welcomePage = 2;
            ShowWelcomePage();
            return;
        }

        BeginInteractiveTutorial();
    }

    private void ShowWelcomePage()
    {
        if (welcomePage == 1)
        {
            welcomeTitleText.text = "겨울 골목의 붕어빵 가게에 오신 걸 환영해요!";
            welcomeDescriptionText.text = "이곳에서는 손님의 주문을 받고, 반죽과 속재료를 골라 따뜻한 붕어빵을 구워요.";
            welcomeNextLabel.text = "다음";
        }
        else
        {
            welcomeTitleText.text = "첫 손님이 곧 도착해요";
            welcomeDescriptionText.text = "주문을 받고 팥붕어빵 하나를 같이 만들어 봐요.\n흰색 외곽선이 나타난 곳을 차례로 클릭하면 됩니다.";
            welcomeNextLabel.text = "가게 열기";
        }

        if (EventSystem.current != null && welcomeNextButton != null)
            EventSystem.current.SetSelectedGameObject(welcomeNextButton.gameObject);
    }

    private void BeginInteractiveTutorial()
    {
        tutorialStarted = true;
        forcedOrderIssued = false;
        welcomeDim.SetActive(false);
        welcomePanel.SetActive(false);
        SetGuideVisible(true);
        SetStage(Stage.CustomerOrder);
    }

    private void DeclineTutorial()
    {
        SaveTutorialFinished();
        Managers.Game.IsTutorialClockPaused = false;
        Managers.UI.CloseUI(false);
    }

    private void OnTutorialEvent(TutorialEvent tutorialEvent, GameObject source)
    {
        if (MatchesCurrentStep(tutorialEvent, source) == false)
            return;

        switch (tutorialEvent)
        {
            case TutorialEvent.CustomerOrderAccepted:
                SetStage(Stage.SwitchToCooking);
                break;

            case TutorialEvent.ViewChanged:
                if (stage == Stage.SwitchToCooking && CameraController.Instance?.CurrentView == GameplayView.Cooking)
                    SetStage(Stage.SelectKettle);
                else if (stage == Stage.SwitchToCustomer && CameraController.Instance?.CurrentView == GameplayView.Customer)
                    SetStage(Stage.ServeCustomer);
                break;

            case TutorialEvent.ToolSelected:
                if (stage == Stage.SelectKettle)
                    SetStage(Stage.FillMold);
                else if (stage == Stage.SelectRedBean)
                    SetStage(Stage.AddFilling);
                else if (stage == Stage.SelectKettleAgain)
                    SetStage(Stage.AddTopBatter);
                break;

            case TutorialEvent.MoldFilled:
                currentFishBun = source;
                SetStage(Stage.SelectRedBean);
                break;

            case TutorialEvent.FillingAdded:
                currentFishBun = source;
                SetStage(Stage.SelectKettleAgain);
                break;

            case TutorialEvent.TopBatterAdded:
                currentFishBun = source;
                SetStage(Stage.Bake);
                break;

            case TutorialEvent.BakeStageAdvanced:
                firstBakeFinished = true;
                SetBakeCopy();
                break;

            case TutorialEvent.Cooked:
                currentFishBun = source;
                SetStage(Stage.MoveToDisplay);
                break;

            case TutorialEvent.FishBunDisplayed:
                currentFishBun = source;
                SetStage(Stage.SwitchToCustomer);
                break;

            case TutorialEvent.FishBunServed:
                Complete();
                break;
        }
    }

    private bool MatchesCurrentStep(TutorialEvent tutorialEvent, GameObject source)
    {
        return stage switch
        {
            Stage.CustomerOrder => tutorialEvent == TutorialEvent.CustomerOrderAccepted,
            Stage.SwitchToCooking => tutorialEvent == TutorialEvent.ViewChanged && CameraController.Instance?.CurrentView == GameplayView.Cooking,
            Stage.SelectKettle => tutorialEvent == TutorialEvent.ToolSelected && source != null && source.CompareTag("kettle"),
            Stage.FillMold => tutorialEvent == TutorialEvent.MoldFilled,
            Stage.SelectRedBean => tutorialEvent == TutorialEvent.ToolSelected && source != null && source.name == FillingType.redBean.ToString(),
            Stage.AddFilling => tutorialEvent == TutorialEvent.FillingAdded && source == currentFishBun,
            Stage.SelectKettleAgain => tutorialEvent == TutorialEvent.ToolSelected && source != null && source.CompareTag("kettle"),
            Stage.AddTopBatter => tutorialEvent == TutorialEvent.TopBatterAdded && source == currentFishBun,
            Stage.Bake => tutorialEvent == TutorialEvent.BakeStageAdvanced || tutorialEvent == TutorialEvent.Cooked,
            Stage.MoveToDisplay => tutorialEvent == TutorialEvent.FishBunDisplayed && source == currentFishBun,
            Stage.SwitchToCustomer => tutorialEvent == TutorialEvent.ViewChanged && CameraController.Instance?.CurrentView == GameplayView.Customer,
            Stage.ServeCustomer => tutorialEvent == TutorialEvent.FishBunServed && source == currentFishBun,
            _ => false,
        };
    }

    private void SetStage(Stage nextStage)
    {
        stage = nextStage;
        currentTarget = ResolveTarget();
        firstBakeFinished = false;

        // 조리 시간이 필요한 굽기 단계만 게임 시계를 흐르게 합니다.
        Managers.Game.IsTutorialClockPaused = stage != Stage.Bake;

        switch (stage)
        {
            case Stage.CustomerOrder:
                SetGuide(1, "첫 손님의 주문을 받아요", "손님이 나타나면 손님을 클릭하세요. 첫 주문은 팥붕어빵 1개예요.");
                break;
            case Stage.SwitchToCooking:
                SetGuide(2, "조리대로 이동하세요", "PC는 SPACE 키를 누르고, 모바일은 왼쪽 전환 버튼을 눌러 조리대로 이동하세요.");
                break;
            case Stage.SelectKettle:
                SetGuide(3, "주전자를 선택하세요", "PC는 Q 키를 누르거나, 주전자를 클릭해 반죽을 부을 준비를 하세요.");
                break;
            case Stage.FillMold:
                SetGuide(4, "빈 붕어빵 틀을 클릭하세요", "선택한 주전자로 빈 틀에 밑반죽을 부어 주세요.");
                break;
            case Stage.SelectRedBean:
                SetGuide(5, "팥 재료를 선택하세요", "PC는 1 키를 누르거나, 팥 통을 클릭해 재료를 선택하세요.");
                break;
            case Stage.AddFilling:
                SetGuide(6, "붕어빵에 팥을 넣으세요", "밑반죽 위의 붕어빵을 클릭해 팥을 넣어 주세요.");
                break;
            case Stage.SelectKettleAgain:
                SetGuide(7, "주전자를 다시 선택하세요", "PC는 Q 키를 누르거나, 주전자를 다시 클릭해 주세요.");
                break;
            case Stage.AddTopBatter:
                SetGuide(8, "윗반죽을 부으세요", "팥이 들어간 붕어빵을 클릭해 반죽으로 덮습니다.");
                break;
            case Stage.Bake:
                SetBakeCopy();
                break;
            case Stage.MoveToDisplay:
                SetGuide(10, "완성된 붕어빵을 진열하세요", "완성된 붕어빵을 클릭해 선택한 뒤 진열대를 클릭하세요. PC에서는 W 키로 바로 진열할 수도 있어요.");
                break;
            case Stage.SwitchToCustomer:
                SetGuide(11, "손님 화면으로 이동하세요", "PC는 SPACE 키를 누르고, 모바일은 왼쪽 전환 버튼을 눌러 손님에게 돌아가세요.");
                break;
            case Stage.ServeCustomer:
                SetGuide(12, "손님에게 건네세요", "진열대의 붕어빵을 클릭해 선택한 뒤 손님을 클릭하면 첫 주문 완료입니다.");
                break;
        }
    }

    private void SetBakeCopy()
    {
        if (firstBakeFinished)
            SetGuide(9, "한 번 더 구워 완성하세요", "조금 더 기다렸다가 붕어빵을 다시 클릭하면 완성됩니다.");
        else
            SetGuide(9, "노릇해질 때까지 기다리세요", "조금 기다린 뒤 조리 중인 붕어빵을 클릭해 굽기 상태를 진행하세요.");
    }

    private void SetGuide(int number, string title, string description)
    {
        if (stepText != null)
            stepText.text = $"{number} / {TotalSteps} · 첫 붕어빵";
        if (titleText != null)
            titleText.text = title;
        if (descriptionText != null)
            descriptionText.text = description;
        if (progressText != null)
        {
            string progress = string.Empty;
            for (int i = 1; i <= TotalSteps; ++i)
                progress += i <= number ? "● " : "○ ";
            progressText.text = progress.TrimEnd();
        }
    }

    private GameObject ResolveTarget()
    {
        return stage switch
        {
            Stage.CustomerOrder => FindActiveCustomer(),
            Stage.SwitchToCooking or Stage.SwitchToCustomer => GameObject.Find("toggleViewButton"),
            Stage.SelectKettle or Stage.SelectKettleAgain => GameObject.Find("kettle"),
            Stage.FillMold => FindEmptyMold(),
            Stage.SelectRedBean => GameObject.Find(FillingType.redBean.ToString()),
            Stage.AddFilling or Stage.AddTopBatter or Stage.Bake or Stage.MoveToDisplay => currentFishBun,
            Stage.ServeCustomer => FindActiveCustomer(),
            _ => null,
        };
    }

    private GameObject FindActiveCustomer()
    {
        foreach (CustomerController controller in FindObjectsByType<CustomerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (controller.Customer != null && controller.Customer.activeInHierarchy)
                return controller.Customer;
        }

        return null;
    }

    private GameObject FindEmptyMold()
    {
        foreach (MoldController mold in FindObjectsByType<MoldController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (mold.IsFilled == false)
                return mold.gameObject;
        }

        return null;
    }

    private CameraController FindCameraController()
    {
        return FindFirstObjectByType<CameraController>();
    }

    private void UpdateSpotlight()
    {
        if (overlayRoot == null || dimTop == null || currentTarget == null)
        {
            SetSpotlightVisible(false);
            return;
        }

        if (TryGetTargetRect(currentTarget, out Rect targetRect) == false)
        {
            SetSpotlightVisible(false);
            return;
        }

        // 마지막 단계는 진열대의 붕어빵에서 드래그를 시작해야 한다.
        // 손님만 열어 두면 시작 지점이 Dim 패널에 막히므로 두 영역을 모두 연다.
        if (stage == Stage.ServeCustomer &&
            currentFishBun != null &&
            TryGetTargetRect(currentFishBun, out Rect fishBunRect))
        {
            targetRect = Union(targetRect, fishBunRect);
        }

        if (stage == Stage.MoveToDisplay &&
            currentFishBun != null &&
            TryGetTargetRect(currentFishBun, out Rect selectedBunRect))
        {
            GameObject displayPlate = GameObject.Find("DisplayPlate");
            if (displayPlate != null && TryGetTargetRect(displayPlate, out Rect displayRect))
                targetRect = Union(selectedBunRect, displayRect);
        }

        SetSpotlightVisible(true);
        float padding = 26f;
        targetRect.xMin -= padding;
        targetRect.xMax += padding;
        targetRect.yMin -= padding;
        targetRect.yMax += padding;
        targetRect = ClampToRoot(targetRect);

        SetRect(dimTop.rectTransform, new Rect(overlayRoot.rect.xMin, targetRect.yMax, overlayRoot.rect.width, overlayRoot.rect.yMax - targetRect.yMax));
        SetRect(dimBottom.rectTransform, new Rect(overlayRoot.rect.xMin, overlayRoot.rect.yMin, overlayRoot.rect.width, targetRect.yMin - overlayRoot.rect.yMin));
        SetRect(dimLeft.rectTransform, new Rect(overlayRoot.rect.xMin, targetRect.yMin, targetRect.xMin - overlayRoot.rect.xMin, targetRect.height));
        SetRect(dimRight.rectTransform, new Rect(targetRect.xMax, targetRect.yMin, overlayRoot.rect.xMax - targetRect.xMax, targetRect.height));

        RectTransform frame = highlightFrame.rectTransform;
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
        frame.anchoredPosition = targetRect.center;
        frame.sizeDelta = targetRect.size;

        PositionGuidePanel(targetRect);

        if (guideArrow != null)
        {
            guideArrow.anchorMin = guideArrow.anchorMax = new Vector2(0.5f, 0.5f);
            guideArrow.anchoredPosition = targetRect.center + new Vector2(targetRect.width * 0.5f + 52f, 36f);
        }
    }

    private void PositionGuidePanel(Rect targetRect)
    {
        if (guidePanel == null)
            return;

        guidePanel.anchorMin = guidePanel.anchorMax = new Vector2(0.5f, 0.5f);
        guidePanel.pivot = new Vector2(0.5f, 0.5f);

        float edgeMargin = 48f;
        float halfWidth = guidePanel.rect.width * 0.5f;
        float halfHeight = guidePanel.rect.height * 0.5f;
        Vector2[] candidates =
        {
            new(0f, overlayRoot.rect.yMax - halfHeight - edgeMargin),
            new(0f, overlayRoot.rect.yMin + halfHeight + edgeMargin),
            new(overlayRoot.rect.xMin + halfWidth + edgeMargin, 0f),
            new(overlayRoot.rect.xMax - halfWidth - edgeMargin, 0f),
        };

        Vector2 bestPosition = candidates[0];
        float bestScore = float.MaxValue;
        foreach (Vector2 candidate in candidates)
        {
            Rect panelRect = new(candidate - new Vector2(halfWidth, halfHeight), guidePanel.rect.size);
            float overlapArea = IntersectionArea(panelRect, targetRect);
            float distance = Vector2.Distance(candidate, targetRect.center);
            float score = overlapArea * 1000f - distance;
            if (score < bestScore)
            {
                bestScore = score;
                bestPosition = candidate;
            }
        }

        guidePanel.anchoredPosition = bestPosition;
    }

    private static Rect Union(Rect first, Rect second)
    {
        return Rect.MinMaxRect(
            Mathf.Min(first.xMin, second.xMin),
            Mathf.Min(first.yMin, second.yMin),
            Mathf.Max(first.xMax, second.xMax),
            Mathf.Max(first.yMax, second.yMax));
    }

    private static float IntersectionArea(Rect first, Rect second)
    {
        float width = Mathf.Max(0f, Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin));
        float height = Mathf.Max(0f, Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin));
        return width * height;
    }

    private bool TryGetTargetRect(GameObject target, out Rect localRect)
    {
        localRect = default;
        Camera camera = Camera.main;
        if (target == null || camera == null)
            return false;

        // 손님·도구 프리팹에도 RectTransform이 붙어 있을 수 있다.
        // 이 경우 RectTransform은 크기가 0인 배치용 정보일 뿐이므로,
        // 실제로 화면에 보이는 SpriteRenderer/Collider의 범위를 먼저 사용한다.
        Renderer targetRenderer = target.GetComponentInChildren<Renderer>();
        Collider2D targetCollider = target.GetComponentInChildren<Collider2D>();
        RectTransform uiRect = target.GetComponent<RectTransform>();
        if (targetRenderer == null && targetCollider == null && uiRect != null)
        {
            Vector3[] corners = new Vector3[4];
            uiRect.GetWorldCorners(corners);
            Vector2 uiScreenMin = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 uiScreenMax = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, uiScreenMin, null, out Vector2 uiLocalMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, uiScreenMax, null, out Vector2 uiLocalMax);
            localRect = Rect.MinMaxRect(
                Mathf.Min(uiLocalMin.x, uiLocalMax.x),
                Mathf.Min(uiLocalMin.y, uiLocalMax.y),
                Mathf.Max(uiLocalMin.x, uiLocalMax.x),
                Mathf.Max(uiLocalMin.y, uiLocalMax.y));
            return localRect.Overlaps(overlayRoot.rect);
        }

        Bounds bounds;
        if (targetRenderer != null)
            bounds = targetRenderer.bounds;
        else if (targetCollider != null)
            bounds = targetCollider.bounds;
        else
            return false;

        Vector3 screenMin = camera.WorldToScreenPoint(bounds.min);
        Vector3 screenMax = camera.WorldToScreenPoint(bounds.max);
        if (screenMin.z < 0f || screenMax.z < 0f)
            return false;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, screenMin, null, out Vector2 localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot, screenMax, null, out Vector2 localMax);

        float xMin = Mathf.Min(localMin.x, localMax.x);
        float xMax = Mathf.Max(localMin.x, localMax.x);
        float yMin = Mathf.Min(localMin.y, localMax.y);
        float yMax = Mathf.Max(localMin.y, localMax.y);
        localRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return localRect.Overlaps(overlayRoot.rect);
    }

    private Rect ClampToRoot(Rect value)
    {
        float xMin = Mathf.Clamp(value.xMin, overlayRoot.rect.xMin, overlayRoot.rect.xMax);
        float xMax = Mathf.Clamp(value.xMax, overlayRoot.rect.xMin, overlayRoot.rect.xMax);
        float yMin = Mathf.Clamp(value.yMin, overlayRoot.rect.yMin, overlayRoot.rect.yMax);
        float yMax = Mathf.Clamp(value.yMax, overlayRoot.rect.yMin, overlayRoot.rect.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private void SetRect(RectTransform rectTransform, Rect localRect)
    {
        rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = localRect.center;
        rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, localRect.width), Mathf.Max(0f, localRect.height));
    }

    private void SetSpotlightVisible(bool visible)
    {
        if (dimTop != null)
        {
            dimTop.gameObject.SetActive(visible);
            dimBottom.gameObject.SetActive(visible);
            dimLeft.gameObject.SetActive(visible);
            dimRight.gameObject.SetActive(visible);
        }

        if (highlightFrame != null)
            highlightFrame.gameObject.SetActive(visible);
        if (guideArrow != null)
            guideArrow.gameObject.SetActive(visible);
    }

    private void SetGuideVisible(bool visible)
    {
        if (guidePanel != null)
            guidePanel.gameObject.SetActive(visible);
        if (skipButton != null)
            skipButton.gameObject.SetActive(visible);
    }

    private void SaveTutorialFinished()
    {
        SaveService.Service.MarkTutorialCompleted();
    }

    private void Skip()
    {
        SaveTutorialFinished();
        Managers.Game.IsTutorialClockPaused = false;
        Managers.UI.CloseUI(false);
    }

    private void Complete()
    {
        SaveTutorialFinished();
        Managers.Game.IsTutorialClockPaused = false;
        SetSpotlightVisible(false);
        SetGuideVisible(false);

        if (completePanel != null)
            completePanel.SetActive(true);

        StartCoroutine(StartNewGameAfterComplete());
    }

    private IEnumerator StartNewGameAfterComplete()
    {
        yield return new WaitForSecondsRealtime(1.5f);

        // 연습 중 사용한 재료비·주문·손님 상태를 버리고 설정된 초기값으로 다시 시작한다.
        Managers.Game.PrepareForSceneReload();
        Managers.UI.CloseUI(false);
        SceneManager.LoadScene("GameScene");
    }
}

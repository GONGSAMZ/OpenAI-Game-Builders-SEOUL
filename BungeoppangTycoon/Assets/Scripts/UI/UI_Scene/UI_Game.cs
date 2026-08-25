using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine;

public class UI_Game : UI_Base
{
    #region 게임 요소
    enum TMP {
        dayText,
        timeText,
        moneyText,
        redBeanCoinText,
    }

    enum Btns {
        settingsButton,
        toggleViewButton,
        inAppMarketButton,
    }

    static GameObject ordersPanel;

    GameObject viewHintPanel;
    GameObject shortcutHintPanel;
    TextMeshProUGUI viewHintText;
    TextMeshProUGUI shortcutHintText;
    TextMeshProUGUI selectedFishBunText;
    TextMeshProUGUI timeText;
    TextMeshProUGUI moneyText;

    #endregion

    int minute
    {
        //시간은 10분단위로 표시
        get { return Managers.Game.minute / 10; }
    }

    public static Action orderUpdateAction = null;

    protected override void Init()
    {
        //바인딩
        Bind<TextMeshProUGUI>(typeof(TMP));
        Bind<Button>(typeof(Btns));

        //데이터
        GetTMP((int)TMP.dayText).text = $"Day {Managers.Game.Day}";
        timeText = GetTMP((int)TMP.timeText);
        moneyText = GetTMP((int)TMP.moneyText);
        if (moneyText != null)
            moneyText.text = $"{Managers.Game.Money.ToString("N0")} 원";
        GetButton((int)Btns.toggleViewButton).gameObject.AddEvent(toggleViewBtnFunc);
        GetButton((int)Btns.settingsButton).gameObject.AddEvent(settingsBtnFunc);
        GetButton((int)Btns.inAppMarketButton).gameObject.AddEvent(inAppMarketBtnFunc);

        CreateInputHints();
        CameraController.ViewChanged += RefreshViewHints;
        InputManager.SelectedFishBunChanged += RefreshSelectedFishBun;
        InputManager.TouchModeChanged += RefreshTouchMode;
        if (SaveService.Instance != null)
            SaveService.Instance.DataChanged += RefreshHintSettings;
        RefreshViewHints(CameraController.Instance?.CurrentView ?? GameplayView.Customer);
        RefreshSelectedFishBun(InputManager.Instance?.SelectedFishBun);

        if (GamePlatformClient.Instance != null)
            GamePlatformClient.Instance.StoreStateChanged += RefreshPlatformCurrency;
        RefreshPlatformCurrency();

        //이벤트 구독

        orderUpdateAction -= orderUpdate;
        orderUpdateAction += orderUpdate;

        ordersPanel = Util.FindObject(gameObject, "ordersPanel");
        // 설정 화면에서 돌아와 UI_Game이 새로 만들어진 경우에도,
        // 프리팹 기본값 대신 현재 주문 상태를 바로 표시한다.
        orderUpdate();

        // 첫 플레이에서만 게임 위에 조리 튜토리얼을 띄운다.
        if (UI_Tutorial.ShouldShow())
            Managers.UI.ShowUI<UI_Tutorial>(false);

    }

    private void OnDestroy()
    {
        CameraController.ViewChanged -= RefreshViewHints;
        InputManager.SelectedFishBunChanged -= RefreshSelectedFishBun;
        InputManager.TouchModeChanged -= RefreshTouchMode;
        if (SaveService.Instance != null)
            SaveService.Instance.DataChanged -= RefreshHintSettings;

        if (GamePlatformClient.Instance != null)
            GamePlatformClient.Instance.StoreStateChanged -= RefreshPlatformCurrency;
    }


    void Update()
    {
        //분은 10의 단위로만 바꿈
        if (timeText != null)
            timeText.text = ($"{Managers.Game.hour} : {minute}0");
        if (moneyText != null)
            moneyText.text = ($"{Managers.Game.Money.ToString("N0")} 원 ");
        // WebGL 부모 헤더의 계정 변경을 이벤트 누락과 관계없이 즉시 반영한다.
        RefreshPlatformCurrency();

        // 특별 주문을 시작하기 전에 선택한 재료가 있었다면 즉시 숨긴다.
        if (CustomerStoryProgress.IsSpecialOrderActive && selectedFishBunText != null)
            selectedFishBunText.gameObject.SetActive(false);


    }

    void settingsBtnFunc()
    {
        //Managers.Game.
        Managers.UI.CloseUI();
        Managers.UI.ShowUI<UI_Settings>();
    }

    void toggleViewBtnFunc()
    {
        if (Managers.Game.isRunning == false ||
            UI_Tutorial.IsBlockingFirstCustomer ||
            UI_Tutorial.AllowsManualViewSwitch == false)
            return;

        CameraController.Instance?.ToggleCamera();
    }

    void inAppMarketBtnFunc()
    {
        Managers.UI.ShowUI<UI_InAppMarket>(false);
    }

    void RefreshPlatformCurrency()
    {
        GamePlatformClient client = GamePlatformClient.Instance;
        GetTMP((int)TMP.redBeanCoinText).text = client != null && client.IsLoggedIn
            ? $"팥 코인 {client.RedBeanCoinBalance:N0}개"
            : "팥 코인 —";
    }

    void CreateInputHints()
    {
        viewHintText = CreateHintPanel(
            "ViewInputHint",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(24f, -150f),
            new Vector2(470f, 58f),
            28f);
        viewHintPanel = viewHintText.transform.parent.gameObject;

        shortcutHintText = CreateHintPanel(
            "CookingShortcutHint",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 22f),
            new Vector2(1740f, 58f),
            25f);
        shortcutHintPanel = shortcutHintText.transform.parent.gameObject;
        shortcutHintText.text = "Q  주전자   |   1  팥   2  슈크림   3  누텔라   4  크림치즈   5  피자   6  민트   7  녹차   8  고구마   |   W  진열   E  버리기";

        selectedFishBunText = CreateHintPanel(
            "SelectedFishBunHint",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 92f),
            new Vector2(560f, 54f),
            27f);
        selectedFishBunText.gameObject.SetActive(false);
    }

    TextMeshProUGUI CreateHintPanel(
        string objectName,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize)
    {
        GameObject panel = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = anchor;
        panelRect.anchorMax = anchor;
        panelRect.pivot = pivot;
        panelRect.anchoredPosition = anchoredPosition;
        panelRect.sizeDelta = size;

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.16f, 0.10f, 0.06f, 0.82f);
        background.raycastTarget = false;

        GameObject textObject = new("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 6f);
        textRect.offsetMax = new Vector2(-16f, -6f);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI gameFontSource = GetTMP((int)TMP.moneyText);
        if (gameFontSource != null)
            label.font = gameFontSource.font;

        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.93f, 0.75f, 1f);
        label.fontStyle = FontStyles.Bold;
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = 17f;
        label.fontSizeMax = fontSize;
        label.raycastTarget = false;
        return label;
    }

    void RefreshViewHints(GameplayView view)
    {
        if (viewHintPanel == null || shortcutHintPanel == null || viewHintText == null || shortcutHintText == null)
            return;

        bool touchMode = InputManager.Instance != null && InputManager.Instance.IsTouchMode;
        bool showKeyboardHints = KeyboardHintSettings.IsEnabled && touchMode == false;
        string viewName = view == GameplayView.Customer ? "손님 화면" : "조리대 화면";
        string controlHint = touchMode ? "위아래로 밀거나 전환 버튼" : "SPACE  화면 전환";
        viewHintText.text = $"{viewName}  ·  {controlHint}";
        // 텍스트만 끄면 부모 패널의 Image가 남아 빈 회색 박스로 보인다.
        // 안내 UI는 배경 패널 전체를 함께 숨긴다.
        viewHintPanel.SetActive(touchMode || showKeyboardHints);
        shortcutHintPanel.SetActive(showKeyboardHints && view == GameplayView.Cooking);
    }

    void RefreshTouchMode(bool touchMode)
    {
        RefreshViewHints(CameraController.Instance?.CurrentView ?? GameplayView.Customer);
    }

    void RefreshHintSettings()
    {
        RefreshViewHints(CameraController.Instance?.CurrentView ?? GameplayView.Customer);
    }

    void RefreshSelectedFishBun(FishBunController fishBun)
    {
        if (selectedFishBunText == null)
            return;

        // 특별 주문 중에는 화면 상단의 선택 재료명을 숨겨 정답 메뉴를 추측할 단서를 남기지 않는다.
        bool hasSelection = fishBun != null && !CustomerStoryProgress.IsSpecialOrderActive;
        selectedFishBunText.gameObject.SetActive(hasSelection);
        if (hasSelection)
        {
            string actions = fishBun.IsOnDisplay
                ? "E 버리기 · 손님 클릭"
                : "W 진열 · E 버리기";
            selectedFishBunText.text = $"선택됨  ·  {GetFillingName(fishBun.fillingType)} 붕어빵  |  {actions}";
        }
    }

    static string GetFillingName(FillingType filling)
    {
        return filling switch
        {
            FillingType.redBean => "팥",
            FillingType.custard => "슈크림",
            FillingType.nutella => "누텔라",
            FillingType.creamCheese => "크림치즈",
            FillingType.pizza => "피자",
            FillingType.mint => "민트",
            FillingType.greenTea => "녹차",
            FillingType.sweetPotato => "고구마",
            _ => filling.ToString(),
        };
    }

    static void orderUpdate()
    {
        int numOfPanel = 0;
        GameObject panel; //ordersPanel산하의 panel

        //주문 종류&개수UI 표시
        foreach (var order in Managers.Game.Order)
        {
            panel = ordersPanel.transform.GetChild(numOfPanel).gameObject;
            
            panel.SetActive(true);
            panel.transform.GetChild(0).GetComponent<Image>().sprite =
                Managers.Resource.LoadSprite("fillingChunks", (int) order.Key);
            panel.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text =
                order.Value.ToString();

            ++numOfPanel;

        }

        //나머지 비활성화
        Util.checkNull(ordersPanel);
        for(int j = numOfPanel;  j < ordersPanel.transform.childCount; ++j)
        {
            panel = ordersPanel.transform.GetChild(j).gameObject;
            panel.SetActive(false);
        }
    }
}

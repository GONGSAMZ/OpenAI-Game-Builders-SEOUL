using System;
using System.Collections.Generic;
using UnityEngine;
using static Util;

/// <summary>
/// 게임 씬이 열려 있는 동안만 사용하는 영업 상태입니다.
/// 저장이 필요한 도감·손님 이야기·해금 재료·설정은 SaveService.Data에 보관합니다.
/// 이 객체의 값은 하루가 끝날 때 GameManagerEx가 SaveService로 정산하여 저장합니다.
/// </summary>
public class GameData
{
    /// <summary>현재 영업 중인 날짜입니다. 저장 데이터의 nextDay와는 의미가 다릅니다.</summary>
    public int day;

    /// <summary>재료비를 빼기 전후로 영업 중 변하는 현재 보유 금액입니다.</summary>
    public int money;
}

public class GameManagerEx
{
    // SaveGameData를 영업 중에 바로 수정하지 않도록, 씬 안에서는 이 임시 상태만 갱신한다.
    readonly GameData gameData = new GameData();
    GameData CurData => gameData;

    #region 영업 중 임시 상태
    public int Day
    {
        get { return CurData.day; }
        set { CurData.day = value; }
    }

    public int Money
    {
        get { return CurData.money; }
        set { CurData.money = value; }
    }

    // 실제 재료 해금 여부는 저장된 재료 ID를 기준으로 판정한다.
    public bool IsFillingUnlocked(FillingType filling) =>
        SaveService.Instance.IsFillingUnlocked(filling);
    #endregion

    #region 시간 관련 변수
    readonly int startHour = 19;
    readonly int endHour = 22;
    public int hour
    { get { return (int)delta / 60 + startHour; } }

    public int minute
    { get { return (int)delta % 60; } }


    public float delta; //시간
    float gameSpeed = 1f; //게임 속도
    public float GameSpeed
    {
        get { return gameSpeed * Managers.Instance._gameSpeed; }
    }

    //운영 관련 변수
    DayState dayState = DayState.Opening;

    bool didAlertClosingTime = false; // 가게 운영종료 알려줬는지
    public bool isRunning = true; //가게 운영 중인지(정지 여부 포함)

    // 튜토리얼 안내를 읽는 동안에는 영업 시계와 손님 대기 게이지만 멈춘다.
    // 조리 입력은 isRunning을 유지하므로 계속 받을 수 있다.
    public bool IsTutorialClockPaused { get; set; }

    public int numsOfCurCustomers = 0;
    public bool isAllExited
    {
        get {

            //Debug.Log($"{numsOfCurCustomers}명 존재");
            return numsOfCurCustomers == 0; }
    }
    public bool isClosingTime
    {
        get { return hour >= endHour; }
    }
    #endregion

    #region 게임 요소 관련 변수
    GameObject parentGo;
    public GameObject ParentGo
    {
        get
        {
            if (parentGo == null)
                parentGo = GameObject.Find("@GameObject");

            return parentGo;
        }
    }

    GameObject[] fillingArr = new GameObject[GetEnumSize(typeof(FillingType))];

    #endregion

    #region 통계 관련 변수
    public int totalFishBunsSold;      // 판매한 붕어빵 수
    public int totalCustomers;         // 방문한 손님 수

    int openingMoney;                // 오늘 영업을 시작할 때의 보유금
    private int ingredientCost;         // 재료 비용
    public int IngredientCost
    {
        get { return ingredientCost; }
        set { ingredientCost = value; }
    }

    public int todayRevenue;

    public int netProfit //오늘 순수익
    {
        get {
            Debug.Log($"netProfit: {todayRevenue} - {ingredientCost} = {todayRevenue - ingredientCost}");
            return  (todayRevenue - ingredientCost); }
    }
    #endregion

    #region 엔딩 관련 변수
    bool isOver { get { return Money <= 0;  } }
    #endregion

    bool hasFinalizedDaily;
    bool isSettlingDaily;
    float nextSettlementRetryAt;

    //현재 주문
    Dictionary<FillingType, int> order = new Dictionary<FillingType, int>();
    public Dictionary<FillingType, int> Order
    {
        get { return order; }
        set { order = value; }
    }

    public event Action InitAction;

    /// <summary>
    /// 같은 GameScene을 새 게임으로 다시 불러오기 전에 이전 씬 오브젝트의 구독을 정리한다.
    /// 새 씬의 Awake에서 현재 오브젝트들이 다시 등록된다.
    /// </summary>
    public void PrepareForSceneReload()
    {
        InitAction = null;
        parentGo = null;
        order.Clear();
        numsOfCurCustomers = 0;
        IsTutorialClockPaused = false;
        isRunning = false;
    }

    //게임 생성 시 초기화 메서드
    public void InitGame()
    {
        Debug.Log("게임 초기화");

        //1. 필링(fillings) 오브젝트
        for (int i = 0; i < GetEnumSize(typeof(FillingType)); ++i)
            fillingArr[i] = FindObject(ParentGo, $"{(FillingType)i}", true);

        //2. 저장된 영업 기록을 이번 씬의 임시 상태로 복사한다.
        // nextDay는 "다음에 시작할 날"이고, Opening에서 Day를 1 올리므로 여기서는 1을 뺀다.
        SaveGameData saved = SaveService.Data;
        CurData.day = Mathf.Max(1, saved.run.nextDay) - 1;
        CurData.money = saved.run.money;
        CustomerStoryProgress.InitializeGame();

        isRunning = true;
        IsTutorialClockPaused = false;
        dayState = DayState.Opening;
        hasFinalizedDaily = false;

        numsOfCurCustomers = 0;


    }

    //하루 운영 메서드
    public void OnUpdate()
    {
        if (isRunning == false && dayState != DayState.Closing)
            return;

        switch (dayState)
        {
            case DayState.Opening:
                InitDaily();
                dayState = DayState.Running;
                break;

            case DayState.Running:

                //시간 측정: 튜토리얼 안내 중에는 클릭 입력은 유지하고 시계만 멈춘다.
                if (IsTutorialClockPaused == false)
                    delta += Time.deltaTime * GameSpeed;

                if(isClosingTime == true)
                {

                    ExecuteOnce(
                        () => { Managers.UI.ShowUI<UI_AlertClosingTime>(false); }, 
                        ref didAlertClosingTime, false);

                    if (CustomerStoryProgress.IsSpecialOrderActive)
                        break;
                    if (isAllExited == true && CustomerStoryProgress.IsSpecialOrderDue())
                    {
                        // 마감 안내가 닫힌 다음 특별 대화를 시작해 두 입력 차단 UI가 겹치지 않게 한다.
                        if (UI_AlertClosingTime.IsVisible)
                            break;

                        CustomerStoryProgress.BeginSpecialOrder();
                        CustomerController controller = UnityEngine.Object.FindFirstObjectByType<CustomerController>();
                        if (controller != null)
                            controller.BeginSpecialOrder(CustomerStoryProgress.ActiveStory);
                    }
                    else if (isAllExited == true)
                        dayState = DayState.Closing;
                }

                break;

            case DayState.Closing:
                FinalizeDaily();
                break;
        }

        /*//하루 시작 처리 (1회성)
        if (hasInitialized == false)
        {
            InitDaily();
            hasInitialized = true;
        }
        //하루 끝 처리 (1회성) 조건: 운영 종료 & 남은 손님 없음
        else if ( isClosingTime == true && isAllExited == true)
        {
            if (hasFinalized == false)
            {
                FinalizeDaily();
                hasFinalized = true;
            }

        }

        else
        {
            //가게 운영: 시간 계산
            delta += Time.deltaTime * GameSpeed;

            //가게 종료 알리기
            if (isClosingTime == true && didAlertClosingTime == false)
            {
                Managers.UI.ShowUI<UI_AlertClosingTime>(false);
                didAlertClosingTime = true;
            }
        }*/
    }

    #region 하루 루틴 처리
    void InitDaily()
    {
        Debug.Log("1. 하루 시작");

        //1. 데이터 초기화
        delta = 0; 
        ++CurData.day;
        CustomerStoryProgress.BeginDay(CurData.day);

        totalFishBunsSold = 0;      
        totalCustomers = 0;         
        ingredientCost = 0;
        todayRevenue = 0;
        openingMoney = Money;
        hasFinalizedDaily = false;
        isSettlingDaily = false;
        nextSettlementRetryAt = 0f;
        didAlertClosingTime = false;

        //2. UI화면
        Managers.UI.CloseUI();
        Managers.UI.ShowUI<UI_Game>();

        //3. 오브젝트 활성화/비활성화
        InitAction?.Invoke();

        //4. 필링 활성화/비활성화
        for (int i = 0; i < GetEnumSize(typeof(FillingType)); ++i)
        {
            if (IsFillingUnlocked((FillingType)i))
                fillingArr[i].SetActive(true);
            else
                fillingArr[i].SetActive(false);
        }
    }

    void FinalizeDaily()
    {
        if (hasFinalizedDaily || isSettlingDaily || Time.realtimeSinceStartup < nextSettlementRetryAt)
            return;

        isSettlingDaily = true;
        Debug.Log("2. 하루 끝 & 엔딩 체크");
        isRunning = false;
        IsTutorialClockPaused = false;
        order.Clear();

        //정산
        todayRevenue = Money - openingMoney;
        //Debug.Log($"현재 돈: {Money} - 오늘 시작 보유금 {openingMoney}");
        //Debug.Log($"오늘 매출: {todayRevenue} - 재료비: {ingredientCost} = 오늘 순수익 {netProfit}");
        SaveService.Instance.SettleDay(
            Day,
            todayRevenue,
            ingredientCost,
            totalFishBunsSold,
            totalCustomers,
            (success, message) =>
            {
                isSettlingDaily = false;
                if (!success)
                {
                    nextSettlementRetryAt = Time.realtimeSinceStartup + 5f;
                    Debug.LogError($"영업일 정산을 다시 시도합니다: {message}");
                    return;
                }

                hasFinalizedDaily = true;
                dayState = DayState.Opening;
                Managers.UI.CloseUI();
                Managers.UI.ShowUI<UI_DayEnd>();
            });
    }

    public void StartNextDay()
    {
        Debug.Log("3. 다음 날로 넘어가기");

/*        //엔딩
        if (Managers.Game.IsEnding() == true)
            return;*/

        isRunning = true;


    }

    public void CompleteSpecialOrder()
    {
        dayState = DayState.Closing;
    }

    public bool IsEnding()
    {
        Debug.Log("IsEnding 진입");

        if (isOver == false)
            return false;

        Managers.UI.CloseUI();
        Managers.UI.ShowUI<UI_Ending>().SetInfo(EndingType.Over);
        return true;
    }

    #endregion

    #region 주문
    public void acceptOrder(Dictionary<FillingType, int> orders)
    {
        foreach (var _order in orders)
        {
            if(order.ContainsKey(_order.Key) == true)
            {
                order[_order.Key] += _order.Value;
                //Debug.Log($"{_order.Key}: {_order.Value} += {order[_order.Key]}개");

            }
            else
            {
                //Debug.Log($"{_order.Key} 새로운 맛 주문 받음");
                order.Add(_order.Key, _order.Value);
            }

        }

        UI_Game.orderUpdateAction?.Invoke();

    }

    public void serveOrder(Dictionary<FillingType, int> orders, FillingType filling)
    {
        if (orders.ContainsKey(filling) == false)
            return;

        if(--Order[filling] == 0)
            Order.Remove(filling);

        UI_Game.orderUpdateAction?.Invoke();
    }

    public void cancelOrder(Dictionary<FillingType, int> orders)
    {
        foreach (var order in orders)
        {
            //Debug.Log($"주문 취소 {order.Key}: {Order[order.Key]} - {order.Value}");
            if (Order.ContainsKey(order.Key) == false)
                return;

            Order[order.Key] -= order.Value;
            //Debug.Log($"주문 취소 결과 {Order[order.Key]}");

            if (Order[order.Key] == 0)
                Order.Remove(order.Key);

        }

        UI_Game.orderUpdateAction?.Invoke();

    }
    #endregion

    #region 기타
    public void QuitGame()
    {
    #if UNITY_EDITOR
        //에디터에서 실행할 때
        UnityEditor.EditorApplication.isPlaying = false; //에디터 실행 중단
    #else
        Application.Quit();
    #endif
    }

    #endregion

}

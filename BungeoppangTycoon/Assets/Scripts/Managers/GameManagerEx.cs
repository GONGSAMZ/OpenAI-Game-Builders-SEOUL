using System;
using System.Collections.Generic;
using UnityEngine;
using static Util;

public class GameData
{
    public int day;

    public int money;
    //public int spritPiece;

    //해금된 재료 개수
    public int numOfFilling;
}

public class GameManagerEx
{
    GameData gameData = new GameData();
    GameData CurData
    {
        get { return gameData; }
        set { gameData = value; }
    }

    #region GameData
    public int Day
    {
        get { 
            return CurData.day + Managers.Instance.day; }
        set { CurData.day = value; }
    }

    public int Money
    {
        get { return CurData.money + Managers.Instance.money; }
        set {
            Debug.Log($"돈 바뀜 {value}");
            CurData.money = value; }
    }

    public int NumOfFilling
    {
        get { return CurData.numOfFilling; }
        set { CurData.numOfFilling = value; }
    }
    #endregion

    #region 시간 관련 변수
    readonly int startHour = 18;
    readonly int endHour = 23;
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

    public int yesterdayProfit;      // 어제 수익
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
    int clearCondition = 40000;
    int endingDay = 5;
    
    bool isEndingDay { get { return Day >= endingDay;  } }
    bool isOver { get { return Money <= 0;  } }
    bool isClear { get { return Money > clearCondition; } }
    #endregion

    //현재 주문 
    Dictionary<FillingType, int> order = new Dictionary<FillingType, int>();
    public Dictionary<FillingType, int> Order
    {
        get { return order; }
        set { order = value; }
    }

    public event Action InitAction;

    //게임 생성 시 초기화 메서드
    public void InitGame()
    {
        Debug.Log("게임 초기화");

        //1. 필링(fillings) 오브젝트
        for (int i = 0; i < GetEnumSize(typeof(FillingType)); ++i)
            fillingArr[i] = FindObject(ParentGo, $"{(FillingType)i}", true);

        //2. 데이터 초기화
        CurData.day = 0;
        CurData.numOfFilling = 4;
        CurData.money = 0;

        isRunning = true;

        numsOfCurCustomers = 0;


    }

    //하루 운영 메서드
    public void OnUpdate()
    {
        if (isRunning == false)
            return;

        switch (dayState)
        {
            case DayState.Opening:
                InitDaily();
                dayState = DayState.Running;
                break;

            case DayState.Running:

                //시간 측정
                delta += Time.deltaTime * GameSpeed;

                if(isClosingTime == true)
                {

                    ExecuteOnce(
                        () => { Managers.UI.ShowUI<UI_AlertClosingTime>(false); }, 
                        ref didAlertClosingTime, false);

                    if(isAllExited == true)
                        dayState = DayState.Closing;
                }

                break;

            case DayState.Closing:
                FinalizeDaily();
                dayState = DayState.Opening;

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

        totalFishBunsSold = 0;      
        totalCustomers = 0;         
        ingredientCost = 0;
        yesterdayProfit = CurData.money;
        didAlertClosingTime = false;

        //2. UI화면
        Managers.UI.CloseUI();
        Managers.UI.ShowUI<UI_Game>();

        //3. 오브젝트 활성화/비활성화 
        InitAction?.Invoke();

        //4. 필링 활성화/비활성화
        for (int i = 0; i < GetEnumSize(typeof(FillingType)); ++i)
        {
            if (i < Managers.Game.CurData.numOfFilling)
                fillingArr[i].SetActive(true);
            else
                fillingArr[i].SetActive(false);
        }
    }

    void FinalizeDaily()
    {
        Debug.Log("2. 하루 끝 & 엔딩 체크");
        isRunning = false;
        order.Clear();

        //정산
        todayRevenue = Money - yesterdayProfit;
        //Debug.Log($"현재 돈: {Money} - 어제 매출{yesterdayProfit}");
        //Debug.Log($"오늘 매출: {todayRevenue} - 재료비: {ingredientCost} = 오늘 순수익 {netProfit}");
        Money -= ingredientCost;



        Managers.UI.CloseUI();
        Managers.UI.ShowUI<UI_DayEnd>();



    }

    public void StartNextDay()
    {
        Debug.Log("3. 다음 날로 넘어가기");

/*        //엔딩
        if (Managers.Game.IsEnding() == true)
            return;*/

        isRunning = true;


    }

    public bool IsEnding()
    {
        Debug.Log("IsEnding 진입");

        if (isOver == true)
        {
            Managers.UI.CloseUI();
            Managers.UI.ShowUI<UI_Ending>().SetInfo(EndingType.Over);
            return true;
        }
        else
        {
            Debug.Log($"{Day} VS {endingDay}");

            if (isEndingDay == false)
                return false;

            Debug.Log("5일차다");

            Managers.UI.CloseUI();

            if (isClear == true)
                Managers.UI.ShowUI<UI_Ending>().SetInfo(EndingType.Clear);
            else
                Managers.UI.ShowUI<UI_Ending>().SetInfo(EndingType.Normal);

            return true;
        }

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

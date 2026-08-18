using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CustomerController : MonoBehaviour, IPointerClickHandler
{
    CustomerData CustomerData; //SO 데이터
/*    static int level = 1; //손님 레벨
    static int Ex; //누적 손님 만족도*/

    #region 게임 오브젝트 관련 변수
    UI_Order ui_order;
    UI_Order UI_order
    {
        get
        {
            if (ui_order == null)
                ui_order = Util.FindObject(gameObject, "UI_Order", true).GetComponent<UI_Order>();

            return ui_order;
        }
    }

    GameObject customer;
    public GameObject Customer
    {
        get
        {
            if (customer == null)
                customer = Util.FindObject(gameObject, "Sprite", true);

            return customer;

        }
    }
    #endregion

    #region 주문 관련 변수
    bool didAcceptOrder = false;

    static readonly string[] OrderNotAcceptedMessages =
    {
        "저 아직 주문 안 했는데요?",
        "음… 아직 고르는 중이에요.",
        "아직 주문을 못 했어요."
    };

    const string WrongFillingMessage = "이건 제가 주문한 맛이 아닌데요.";
    const int WrongFillingAngryPoint = 20;

    Coroutine orderNotAcceptedMessageRoutine;
    Dictionary<FillingType, int> order = new Dictionary<FillingType, int>(); //붕어빵 종류, 개수
    int numsOfFishBun; //주문하는 붕빵 개수
    public int NumOfFishBun
    {
        get{ return numsOfFishBun; }
        set { numsOfFishBun = value; }
    }

    //붕어빵 주문 개수 범위
    int minFishBun = 1;
    int maxFishBun = 3;
/*
    //붕어빵 종류 개수 범위
    const int minOrderType = 1;
    const int maxOrderType = 3;*/

    int orderAngryPoint; //주문 관련 불만도
    int angryPoint; //종합 불만도
    public int AngryPoint //종합 불만도 (주문 + 대기 시간)
    {
        get
        {
            angryPoint = orderAngryPoint + (int)WaitingTime;
            angryPoint = Mathf.Clamp(angryPoint, 0, 100);
            return angryPoint;

        }
        set
        {
            angryPoint = Mathf.Clamp(value, 0, 100);
            //Debug.Log($"angryPoint: {value} => {angryPoint} VS {AngryPoint}");
        }
    }

    int pay = 0;
    #endregion

    #region 시간 관련 변수
    float startTime;
    float endTime;
    float WaitingTime
    {
        get
        {
            endTime = Managers.Game.delta;
            return endTime - startTime;
        }
        set { startTime = value; }
    }

    int reactionTime = 1;
    #endregion

    // 퇴장 반응이 시작된 손님은 더 이상 주문을 받거나 음식을 받을 수 없다.
    bool isLeaving = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (didAcceptOrder == true || isLeaving == true)
            return;

        //주문
        Order();
        //주문 받음
        Managers.Game.acceptOrder(order);
        didAcceptOrder = true;
        TutorialSignals.Raise(TutorialEvent.CustomerOrderAccepted, Customer);
    }

    void ShowOrderNotAcceptedMessage()
    {
        int randomIndex = Random.Range(0, OrderNotAcceptedMessages.Length);
        ShowTemporaryMessage(OrderNotAcceptedMessages[randomIndex]);
    }

    void ShowWrongFillingMessage()
    {
        ShowTemporaryMessage(WrongFillingMessage);
    }

    void ShowTemporaryMessage(string message)
    {
        if (orderNotAcceptedMessageRoutine != null)
            StopCoroutine(orderNotAcceptedMessageRoutine);

        orderNotAcceptedMessageRoutine = StartCoroutine(ShowTemporaryMessageRoutine(message));
    }

    IEnumerator ShowTemporaryMessageRoutine(string message)
    {
        UI_order.SetMessage(message);
        UI_order.gameObject.SetActive(true);

        yield return new WaitForSeconds(1f);

        if (didAcceptOrder == true)
            UI_order.SetOrderText(order);
        else
            UI_order.gameObject.SetActive(false);

        orderNotAcceptedMessageRoutine = null;
    }

    void Awake()
    {
        Managers.Game.InitAction -= CoInstantiateCustomer;
        Managers.Game.InitAction +=  CoInstantiateCustomer;

    }

    void Update()
    {
        if (Managers.Game.isRunning == false)
            return;


        if (AngryPoint < 100)
        {
            UI_order.slider.value = AngryPoint;

        }
        else
            BeginExit(true);


    }

    public void InitCustomer()
    {
        if (orderNotAcceptedMessageRoutine != null)
            StopCoroutine(orderNotAcceptedMessageRoutine);

        orderNotAcceptedMessageRoutine = null;

        UI_order.gameObject.SetActive(false);
        Customer.gameObject.SetActive(false);

        //1. 만족도 관련 변수 측정 시작
        startTime = Managers.Game.delta;
        orderAngryPoint = 0;

        //1. 손님 종류 랜덤 지정
        int customerType = UnityEngine.Random.Range(0, Util.GetEnumSize(typeof(CustomerType)));
        CustomerData = Managers.Resource.LoadCustomerSO((CustomerType)customerType);

        //2. 손님 스프라이트
        customer.GetComponent<SpriteRenderer>().sprite = CustomerData.GetImage();
        //콜라이더 reset
        Destroy(customer.gameObject.GetComponent<PolygonCollider2D>());
        customer.gameObject.AddComponent<PolygonCollider2D>();

        pay = 0;
        didAcceptOrder = false;
        isLeaving = false;
        ++Managers.Game.numsOfCurCustomers;
    }

    public void Order()
    {

        //주문 내역 비우기
        order.Clear();

        // 첫 튜토리얼에서만 결과가 흔들리지 않도록 팥붕어빵 1개로 고정한다.
        if (UI_Tutorial.TryConsumeForcedRedBeanOrder())
        {
            NumOfFishBun = 1;
            order.Add(FillingType.redBean, 1);
            UI_order.SetOrderText(order);
            UI_order.gameObject.SetActive(true);
            return;
        }

        //맛 중복 방지를 위한 범위 리스트
        List<int> orderableFillingType = new List<int>();

        //주문 가능한 맛 범위(orderableFillingType)에 대해 초기화
        for (int i = 0; i < Managers.Game.NumOfFilling; ++i)
            orderableFillingType.Add(i);

        //1. 주문할 붕어빵 개수
        NumOfFishBun = UnityEngine.Random.Range(minFishBun, maxFishBun + 1);
        //Debug.Log($"[Order]{gameObject.name}의 주문 : 총 {NumOfFishBun}개");

        //붕어빵 랜덤 종류*개수
        for (int fishbun = NumOfFishBun; fishbun > 0;)
        {
            //종류 랜덤
            int randomIndex = UnityEngine.Random.Range(0, orderableFillingType.Count);
            FillingType fillingType = (FillingType)orderableFillingType[randomIndex];
            orderableFillingType.RemoveAt(randomIndex); //고른 맛 빼기

            //개수 랜덤
            int _numsOfFishBun; // fillingType맛으로 시킬 붕빵 개수
            /*            //남은 붕어빵 개수 1개 이상일 때에만 랜덤
                        if (fishbun > 1)
                            _numsOfFishBun = UnityEngine.Random.Range(1, fishbun - 1);
                        else
                            _numsOfFishBun = 1;*/

            _numsOfFishBun = Random.Range(1, fishbun);
            if(numsOfFishBun == 0)
                _numsOfFishBun = 1;

            fishbun -= _numsOfFishBun;
            order.Add(fillingType, _numsOfFishBun);

        }

        UI_order.SetOrderText(order);
        UI_order.gameObject.SetActive(true);


    }

    public bool TryEat(FillingType filling, QualityStatus baking)
    {
        if (isLeaving == true)
        {
            ShowTemporaryMessage("손님이 퇴장 중입니다.");
            return false;
        }

        if (didAcceptOrder == false)
        {
            ShowOrderNotAcceptedMessage();
            return false;
        }

        if (order.ContainsKey(filling) == false)
        {
            orderAngryPoint += WrongFillingAngryPoint;
            ShowWrongFillingMessage();
            return false;
        }

        Eat(filling, baking);
        return true;
    }
    void Eat(FillingType filling, QualityStatus baking)
    {

        Managers.Game.serveOrder(order, filling);

        //분노Point 판정
        int perfectPoint = (100 / NumOfFishBun);
        int normalPoint = (int)(perfectPoint * 0.8);

        //1. 종류가 맞는 지 체크
        if (order.ContainsKey(filling) == true)
        {

            //맛 체크
            if (baking == QualityStatus.perfect)
            {
                //Debug.Log($"{AngryPoint} - {perfectPoint}");
                orderAngryPoint -= perfectPoint;
                //Debug.Log($"{AngryPoint}로 내려감");

            }
            else
                orderAngryPoint -= normalPoint;



            //지불할 돈 적립
            pay += Define.FillingPrice[(int)filling];
            //Debug.Log($"지금까지 {pay}원 어치 먹음 ");

            //order 딕셔너리 정리
            if (--order[filling] == 0)
            {
                order.Remove(filling); //딕셔너리 제거

                if (order.Count == 0)
                {
                    orderAngryPoint -= normalPoint;
                    BeginExit();
                }
            }




        }
        else
            return;

        //다시 업뎃
        UI_order.SetOrderText(order);

        //통계 업뎃
        ++Managers.Game.totalFishBunsSold;
        

    }

    void BeginExit(bool isAngry = false)
    {
        if (isLeaving == true)
            return;

        isLeaving = true;
        StartCoroutine(Exit(isAngry));
    }

    IEnumerator Exit(bool isAngry = false)
    {
        //Debug.Log($" {gameObject.name} Exit 시작");

        //반응 효과
        Sprite reaction;
        if (isAngry == true)
        {
            reaction = CustomerData.GetImage(2); //불만족
            if(didAcceptOrder == true)
                Managers.Game.cancelOrder(order); //주문 취소
        }
        else
            reaction = CustomerData.GetImage(1); //만족

        // 대화 팝업 없애기
        UI_order.gameObject.SetActive(false);

        customer.GetComponent<SpriteRenderer>().sprite = reaction;

        yield return new WaitForSeconds(reactionTime);

        //돈 내기
        Managers.Game.Money += pay;

        //손님 비활성화
        customer.gameObject.SetActive(false);
        --Managers.Game.numsOfCurCustomers;
        Debug.Log($" {gameObject.name} Exit 끝");


        //다음 손님
        StartCoroutine(InstatiateCustomer());
        yield break;

    }

    public void CoInstantiateCustomer()
    {
        StartCoroutine(InstatiateCustomer());
    }

    IEnumerator InstatiateCustomer()
    {
        if (Managers.Game.isClosingTime == true)
            yield break;

        InitCustomer();
        ++Managers.Game.totalCustomers;

        //스폰 대기 시간 관련 변수
        float spawnDalayMin = 3f;
        float spawnDalayMax = 8f;

        float spawnDelayTime = UnityEngine.Random.Range(spawnDalayMin, spawnDalayMax);
        //Debug.Log($"1. {spawnDelayTime} 초 후 생성");
        spawnDelayTime /= Managers.Game.GameSpeed; //시간 속도
        //Debug.Log($"2. {spawnDelayTime} 초 후 생성");

        yield return new WaitForSeconds(spawnDelayTime);

        // 튜토리얼 선택/환영 패널이 열려 있으면 첫 손님은 아직 화면에 등장하지 않는다.
        while (UI_Tutorial.IsBlockingFirstCustomer)
            yield return null;

        customer.gameObject.SetActive(true);

        yield break;
    }


}

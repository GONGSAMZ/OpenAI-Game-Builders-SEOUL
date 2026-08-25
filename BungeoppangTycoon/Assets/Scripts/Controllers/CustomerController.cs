using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class CustomerController : MonoBehaviour, IPointerClickHandler
{
    CustomerData CustomerData; //SO 데이터
    CustomerType customerType;
    bool isSpecialOrder;
    GameObject storyBubble;
    bool isStoryChoiceOpen;
    bool isStoryReplyVisible;
    bool isSpecialResultVisible;
    
    string[] specialIntroLines = System.Array.Empty<string>();
    int specialIntroLineIndex = -1;
    int specialIntroAdvanceFrame = -1;
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

    /// <summary>대화 집중 화면에서 원래 색으로 강조할 손님 그림입니다.</summary>
    public SpriteRenderer StoryFocusRenderer => Customer != null ? Customer.GetComponent<SpriteRenderer>() : null;
    /// <summary>선택한 붕어빵을 실제로 받을 수 있는, 주문 접수 완료 상태의 손님인지 나타냅니다.</summary>
    public bool CanReceiveFishBun => Customer != null && Customer.activeInHierarchy && didAcceptOrder && !isLeaving && order.Count > 0;
    /// <summary>현재 선택한 맛을 주문한 손님인지 나타냅니다. 전달 강조 표시에 사용합니다.</summary>
    public bool CanReceiveSelectedFishBun(FillingType filling) =>
        CanReceiveFishBun && order.TryGetValue(filling, out int remainingCount) && remainingCount > 0;
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
        if (isLeaving == true)
            return;

        // 특별 주문 대화 중에는 이전에 선택해 둔 붕어빵 전달보다 대화 진행을 우선한다.
        if (isSpecialOrder && !didAcceptOrder && specialIntroLineIndex >= 0)
        {
            AdvanceSpecialIntro();
            return;
        }

        // 완성된 붕어빵을 선택한 상태라면 주문 받기나 대화보다 전달을 우선합니다.
        if (InputManager.Instance != null && InputManager.Instance.TryHandleSelectedFishBun(Customer))
            return;

        if (isStoryChoiceOpen)
        {
            CancelStoryDialogueSelection();
            return;
        }

        if (isStoryReplyVisible)
            return;

        if (didAcceptOrder == true)
        {
            TryOpenStoryDialogue();
            return;
        }

        AcceptOrder();
    }

    /// <summary>일반 주문과 특별 주문이 같은 접수 경로를 한 번만 사용하게 합니다.</summary>
    private void AcceptOrder()
    {
        if (didAcceptOrder || isLeaving)
            return;

        if (isSpecialOrder)
            startTime = Managers.Game.delta;

        Order();
        if (order.Count == 0)
            return;

        Managers.Game.acceptOrder(order);
        didAcceptOrder = true;
        TutorialSignals.Raise(TutorialEvent.CustomerOrderAccepted, Customer);
        string customerName = CustomerCollectionCatalog.Get(customerType)?.DisplayName ?? customerType.ToString();
        Debug.Log(
            $"[손님 이야기] 주문 수락 | 손님={customerName}" +
            $" | 특별 주문={(isSpecialOrder ? "예" : "아니요")}" +
            $" | 튜토리얼 진행 중={(UI_Tutorial.IsRunning ? "예" : "아니요")}" +
            $" | {CustomerStoryProgress.GetTalkDebugState(customerType)}");
        RefreshStoryBubble();
    }

    public void TryOpenStoryDialogue()
    {
        if (isSpecialOrder || !didAcceptOrder || isLeaving || !CustomerStoryProgress.CanTalkToday(customerType)) return;
        if (isStoryChoiceOpen)
            return;

        isStoryChoiceOpen = true;
        RefreshStoryBubble();
        UI_CustomerStoryChoices.Show(this, CustomerStoryProgress.ActiveStory);
    }

    public void SelectStoryTopic(int topicIndex)
    {
        CustomerStoryData story = CustomerStoryProgress.ActiveStory;
        if (!isStoryChoiceOpen || story?.Topics == null || topicIndex < 0 || topicIndex >= story.Topics.Length ||
            story.CustomerType != customerType || !CustomerStoryProgress.CanTalkToday(customerType))
        {
            Debug.LogWarning($"[손님 이야기] 낮 대화 선택 처리 차단 | 선택지 번호={topicIndex + 1} | {CustomerStoryProgress.GetTalkDebugState(customerType)}", this);
            CancelStoryDialogueSelection();
            return;
        }

        bool completedBefore = CustomerStoryProgress.IsTopicCompleted(customerType, topicIndex);
        bool isNew = CustomerStoryProgress.CompleteTalkTopic(customerType, topicIndex);
        string reply = completedBefore ? story.Topics[topicIndex].RepeatReply : story.Topics[topicIndex].FirstReply;

        isStoryChoiceOpen = false;
        isStoryReplyVisible = true;

        if (isNew)
            orderAngryPoint = Mathf.Max(-100, orderAngryPoint - 10);

        // 선택지에서 답변으로 넘어가도 집중 화면과 게임 시간 정지는 그대로 유지한다.
        UI_CustomerStoryChoices.ShowReply(this, reply);
        RefreshStoryBubble();
        Debug.Log(
            $"[손님 이야기] 낮 대화 답변 표시 | 손님={story.DisplayName} | 선택지 번호={topicIndex + 1}" +
            $" | 처음 들은 주제={(isNew ? "예" : "아니요")} | 답변 닫기=화면 클릭 또는 Enter/Esc", this);
    }

    public void CancelStoryDialogueSelection()
    {
        if (!isStoryChoiceOpen)
            return;

        isStoryChoiceOpen = false;
        UI_CustomerStoryChoices.Hide();
        RefreshStoryBubble();
    }

    public void ReduceAngerForStoryTalk()
    {
        orderAngryPoint = Mathf.Max(-100, orderAngryPoint - 10);
        RefreshStoryBubble();
    }

    public void OnStoryReplyFinished()
    {
        if (!isStoryReplyVisible)
            return;

        isStoryReplyVisible = false;
        UI_CustomerStoryChoices.Hide();
        RefreshStoryBubble();
    }

    void CancelStoryReply(bool refresh = true)
    {
        if (!isStoryReplyVisible)
            return;

        isStoryReplyVisible = false;
        UI_CustomerStoryChoices.Hide();
        if (refresh)
            RefreshStoryBubble();
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

        if (isSpecialOrder && !didAcceptOrder && specialIntroLineIndex >= 0)
            ShowCurrentSpecialIntroLine();
        else if (didAcceptOrder == true)
            UI_order.SetOrderText(order);
        else
            UI_order.gameObject.SetActive(false);

        orderNotAcceptedMessageRoutine = null;
    }

    void Awake()
    {
        Managers.Game.InitAction -= CoInstantiateCustomer;
        Managers.Game.InitAction +=  CoInstantiateCustomer;
        CustomerStoryCutscenePlayer.Preload();

    }

    void OnDestroy()
    {
        Managers.Game.InitAction -= CoInstantiateCustomer;
        if (isStoryChoiceOpen || isStoryReplyVisible)
            UI_CustomerStoryChoices.Hide();
    }

    void Update()
    {
        if (Managers.Game.isRunning == false)
            return;

        // 특별 대사를 읽는 시간은 손님의 대기 시간으로 계산하지 않는다.
        if (isSpecialOrder && (!didAcceptOrder || isSpecialResultVisible))
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
        if (isStoryChoiceOpen)
            UI_CustomerStoryChoices.Hide();
        isStoryChoiceOpen = false;
        CancelStoryReply(false);

        if (orderNotAcceptedMessageRoutine != null)
            StopCoroutine(orderNotAcceptedMessageRoutine);

        orderNotAcceptedMessageRoutine = null;

        UI_order.gameObject.SetActive(false);
        Customer.gameObject.SetActive(false);

        //1. 만족도 관련 변수 측정 시작
        startTime = Managers.Game.delta;
        orderAngryPoint = 0;

        //1. 손님 종류 랜덤 지정
        bool guaranteedStoryCustomer = CustomerStoryProgress.TryGetGuaranteedCustomer(Managers.Game.totalCustomers, out customerType);
        if (!guaranteedStoryCustomer)
            customerType = (CustomerType)UnityEngine.Random.Range(0, Util.GetEnumSize(typeof(CustomerType)));
        CustomerData = Managers.Resource.LoadCustomerSO(customerType);
        CustomerCollectionProgress.MarkMet(customerType);
        string customerName = CustomerCollectionCatalog.Get(customerType)?.DisplayName ?? customerType.ToString();
        Debug.Log(
            $"[손님 이야기] 손님 등장 준비 | 손님={customerName}" +
            $" | 이야기 손님 우선 등장={(guaranteedStoryCustomer ? "예" : "아니요")}" +
            $" | 등장 전 누적 손님 수={Managers.Game.totalCustomers}명" +
            $" | 튜토리얼 진행 중={(UI_Tutorial.IsRunning ? "예" : "아니요")}");

        //2. 손님 스프라이트
        customer.GetComponent<SpriteRenderer>().sprite = CustomerData.GetImage();
        //콜라이더 reset
        Destroy(customer.gameObject.GetComponent<PolygonCollider2D>());
        customer.gameObject.AddComponent<PolygonCollider2D>();

        pay = 0;
        didAcceptOrder = false;
        isLeaving = false;
        isSpecialOrder = false;
        isSpecialResultVisible = false;
        specialIntroLines = System.Array.Empty<string>();
        specialIntroLineIndex = -1;
        specialIntroAdvanceFrame = -1;
        EnsureStoryBubble();
        RefreshStoryBubble();
        ++Managers.Game.numsOfCurCustomers;
    }

    public void Order()
    {

        //주문 내역 비우기
        order.Clear();

        if (isSpecialOrder)
        {
            order.Add(CustomerStoryProgress.ActiveStory.RequiredFilling, 1);
            NumOfFishBun = 1;
            // 특별 주문의 정답 메뉴는 대사나 주문 UI에 공개하지 않는다.
            UI_order.SetSpecialOrderStatus();
            UI_order.gameObject.SetActive(true);
            return;
        }

        // 첫 튜토리얼에서만 결과가 흔들리지 않도록 팥붕어빵 1개로 고정한다.
        if (UI_Tutorial.TryConsumeForcedRedBeanOrder())
        {
            NumOfFishBun = 1;
            order.Add(FillingType.redBean, 1);
            UI_order.SetOrderText(order);
            UI_order.gameObject.SetActive(true);
            return;
        }

        // 주문 가능한 맛 목록입니다. 한 가지 맛을 여러 개 주문할 수 있으므로,
        // 주문을 만들면서 선택한 맛을 목록에서 제거하지 않습니다.
        List<int> orderableFillingType = new List<int>();

        // 해금 개수가 아니라 저장된 재료 ID를 기준으로 주문 가능한 맛을 만든다.
        for (int i = 0; i < Util.GetEnumSize(typeof(FillingType)); ++i)
            if (Managers.Game.IsFillingUnlocked((FillingType)i))
                orderableFillingType.Add(i);

        // 비정상 종료·이전 저장 데이터 때문에 오늘의 선택 재료가 비어 있으면,
        // 인덱스 0을 읽다가 손님 클릭 전체가 멈추지 않게 기본 재료를 즉시 복구한다.
        if (orderableFillingType.Count == 0)
        {
            SaveService.Service.RestoreSelectedFillingsIfEmpty();

            for (int i = 0; i < Util.GetEnumSize(typeof(FillingType)); ++i)
                if (Managers.Game.IsFillingUnlocked((FillingType)i))
                    orderableFillingType.Add(i);
        }

        // 복구 서비스까지 사용할 수 없는 초기화 순서에서도 클릭을 예외로 끝내지 않는다.
        if (orderableFillingType.Count == 0)
        {
            Debug.LogError("[주문] 선택된 속재료가 없어 주문을 만들 수 없습니다.", this);
            UI_order.SetMessage("오늘 판매할 속재료를 먼저 골라 주세요.");
            UI_order.gameObject.SetActive(true);
            return;
        }

        //1. 주문할 붕어빵 개수
        NumOfFishBun = UnityEngine.Random.Range(minFishBun, maxFishBun + 1);
        //Debug.Log($"[Order]{gameObject.name}의 주문 : 총 {NumOfFishBun}개");

        //붕어빵 랜덤 종류*개수
        for (int fishbun = NumOfFishBun; fishbun > 0;)
        {
            //종류 랜덤
            int randomIndex = UnityEngine.Random.Range(0, orderableFillingType.Count);
            FillingType fillingType = (FillingType)orderableFillingType[randomIndex];

            //개수 랜덤
            int _numsOfFishBun; // fillingType맛으로 시킬 붕빵 개수
            /*            //남은 붕어빵 개수 1개 이상일 때에만 랜덤
                        if (fishbun > 1)
                            _numsOfFishBun = UnityEngine.Random.Range(1, fishbun - 1);
                        else
                            _numsOfFishBun = 1;*/

            _numsOfFishBun = Random.Range(1, fishbun);
            fishbun -= _numsOfFishBun;

            // 같은 맛이 다시 선택되면 주문 수량만 누적한다.
            // Dictionary는 같은 키를 Add하면 예외가 나므로 기존 값을 갱신해야 한다.
            if (order.ContainsKey(fillingType))
                order[fillingType] += _numsOfFishBun;
            else
                order.Add(fillingType, _numsOfFishBun);

        }

        UI_order.SetOrderText(order);
        UI_order.gameObject.SetActive(true);


    }

    public bool TryEat(FillingType filling, QualityStatus baking)
    {
        // 답변 표시보다 붕어빵 전달 결과를 우선한다.
        CancelStoryReply();

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

        if (isSpecialOrder)
        {
            bool storySucceeded = CustomerStoryProgress.ResolveSpecialOrder(filling, baking);
            if (storySucceeded)
            {
                // 컷씬이 끝나기 전까지는 특별 주문 상태를 유지해 마감 정산이 앞서지 않게 합니다.
                Debug.Log($"[손님 이야기] 특별 주문 성공 - 컷씬 호출 | 손님={customerType}", this);
                CustomerStoryCutscenePlayer.PlaySpecialOrderSuccess(customerType, () => BeginExit());
            }
            else
            {
                ShowSpecialOrderResult(CustomerStoryProgress.LastSpecialOrderMessage);
            }
            return true;
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

        //1. 종류가 맞는 지 체크
        if (order.ContainsKey(filling) == true)
        {
            // 각 붕어빵은 품질에 따른 감소를 정확히 한 번만 적용한다.
            orderAngryPoint -= CalculateAngerReduction(NumOfFishBun, baking);

            //지불할 돈 적립
            pay += Define.FillingPrice[(int)filling];
            //Debug.Log($"지금까지 {pay}원 어치 먹음 ");

            //order 딕셔너리 정리
            if (--order[filling] == 0)
            {
                order.Remove(filling); //딕셔너리 제거

                if (order.Count == 0)
                {
                    BeginExit();
                }
            }




        }
        else
            return;

        //다시 업뎃
        UI_order.SetOrderText(order);

        //통계 업뎃
        Managers.Game.RecordSale(filling);
        

    }

    private static int CalculateAngerReduction(int totalFishBuns, QualityStatus baking)
    {
        int perfectPoint = 100 / Mathf.Max(1, totalFishBuns);
        return baking == QualityStatus.perfect
            ? perfectPoint
            : (int)(perfectPoint * 0.8f);
    }

    void BeginExit(bool isAngry = false)
    {
        if (isLeaving == true)
            return;

        isLeaving = true;
        specialIntroLineIndex = -1;
        if (isStoryChoiceOpen)
        {
            isStoryChoiceOpen = false;
            UI_CustomerStoryChoices.Hide();
        }
        CancelStoryReply(false);
        RefreshStoryBubble();
        StartCoroutine(Exit(isAngry));
    }

    IEnumerator Exit(bool isAngry = false)
    {
        //Debug.Log($" {gameObject.name} Exit 시작");

        //반응 효과
        Sprite reaction;
        if (isAngry == true)
        {
            reaction = CustomerData.GetImage(CustomerExpression.Disappointed);
            if(didAcceptOrder == true)
                Managers.Game.cancelOrder(order); //주문 취소
        }
        else
            reaction = CustomerData.GetImage(CustomerExpression.Joy);

        // 주문 또는 낮 대화 답변 말풍선 없애기
        UI_order.gameObject.SetActive(false);

        customer.GetComponent<SpriteRenderer>().sprite = reaction;

        yield return new WaitForSeconds(reactionTime);

        //돈 내기
        if (!isSpecialOrder)
            Managers.Game.Money += pay;

        //손님 비활성화
        customer.gameObject.SetActive(false);
        if (!isSpecialOrder)
            --Managers.Game.numsOfCurCustomers;
        Debug.Log($" {gameObject.name} Exit 끝");


        //다음 손님
        if (!isSpecialOrder)
            StartCoroutine(InstatiateCustomer());
        else
        {
            CustomerStoryProgress.CompleteSpecialOrderSession();
            Managers.Game.CompleteSpecialOrder();
        }
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

        // 프리팹은 기본적으로 활성화되어 있으므로 대기 시간에는 이전 손님과 주문 UI를 숨긴다.
        // 실제 손님 정보와 방문 기록은 마감 검사를 통과한 뒤에만 만든다.
        Customer.gameObject.SetActive(false);
        UI_order.gameObject.SetActive(false);

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

        // 대기 중에 19시가 되었으면 손님 정보를 만들거나 도감 방문 기록을 남기지 않는다.
        if (Managers.Game.isClosingTime == true)
            yield break;

        InitCustomer();
        ++Managers.Game.totalCustomers;
        customer.gameObject.SetActive(true);

        yield break;
    }

    public void BeginSpecialOrder(CustomerStoryData story)
    {
        StopAllCoroutines();
        if (isStoryChoiceOpen)
            UI_CustomerStoryChoices.Hide();
        isStoryChoiceOpen = false;
        CancelStoryReply(false);
        customerType = story.CustomerType;
        CustomerData = Managers.Resource.LoadCustomerSO(customerType);
        Customer.GetComponent<SpriteRenderer>().sprite = CustomerData.GetImage();
        isSpecialResultVisible = false;
        pay = 0; didAcceptOrder = false; isLeaving = false; isSpecialOrder = true;
        orderAngryPoint = 0;
        startTime = Managers.Game.delta;
        UI_order.slider.value = 0f;
        customer.gameObject.SetActive(true);
        EnsureStoryBubble();

        // 화면 전체 팝업 대신 손님에게 붙어 있는 기존 주문 말풍선에서 한 문장씩 진행한다.
        specialIntroLines = story.SpecialIntro
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        specialIntroLineIndex = specialIntroLines.Length > 0 ? 0 : -1;
        if (specialIntroLineIndex >= 0)
            ShowCurrentSpecialIntroLine();
        else
            AcceptOrder();
    }

    private void ShowCurrentSpecialIntroLine()
    {
        // 진행 안내 문구는 프리팹에서 지워도 코드가 다시 붙이고 있었다.
        // 특별 주문 대사만 표시하고, 말풍선 클릭 동작은 그대로 유지한다.
        UI_order.SetMessage(specialIntroLines[specialIntroLineIndex], AdvanceSpecialIntro);
        UI_order.gameObject.SetActive(true);
    }

    private void AdvanceSpecialIntro()
    {
        if (specialIntroAdvanceFrame == Time.frameCount)
            return;

        specialIntroAdvanceFrame = Time.frameCount;
        if (!isSpecialOrder || didAcceptOrder || isLeaving || specialIntroLineIndex < 0)
            return;

        if (specialIntroLineIndex < specialIntroLines.Length - 1)
        {
            specialIntroLineIndex++;
            ShowCurrentSpecialIntroLine();
            return;
        }

        specialIntroLineIndex = -1;
        AcceptOrder();
    }

    private void EnsureStoryBubble()
    {
        if (storyBubble != null) return;
        // StoryTalkBubble.png는 Multiple Sprite로 임포트되어 있으므로 첫 번째 조각을 명시한다.
        Sprite sprite = Managers.Resource.LoadSprite("UI/StoryTalkBubble", 0);
        if (sprite == null)
        {
            Debug.LogError("대화 말풍선 스프라이트를 불러오지 못했습니다: Sprites/UI/StoryTalkBubble", this);
            return;
        }
        storyBubble = new GameObject("StoryTalkBubble", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(CustomerStoryBubble));
        storyBubble.transform.SetParent(Customer.transform, false);
        storyBubble.transform.localPosition = new Vector3(0f, 3.9f, 0f);
        // 원본 PNG 해상도와 무관하게 손님 머리 위에서 일정한 크기로 보이게 한다.
        float scale = sprite.bounds.size.y > 0f ? 1.45f / sprite.bounds.size.y : 1.5f;
        storyBubble.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = storyBubble.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingLayerName = "UI"; renderer.sortingOrder = 20;
        storyBubble.GetComponent<CustomerStoryBubble>().SetOwner(this);
        string customerName = CustomerCollectionCatalog.Get(customerType)?.DisplayName ?? customerType.ToString();
        Debug.Log(
            $"[손님 이야기] 대화 말풍선 생성 | 손님={customerName} | 사용 이미지={sprite.name}" +
            $" | 손님 기준 위치={storyBubble.transform.localPosition} | 크기 배율={scale:F3}" +
            $" | 표시 순서 레이어={renderer.sortingLayerName} | 레이어 안 순서={renderer.sortingOrder}", this);
    }

    private void RefreshStoryBubble()
    {
        bool canTalkToday = CustomerStoryProgress.CanTalkToday(customerType);
        bool blockedByTutorial = UI_Tutorial.IsRunning;
        bool storyInteractionVisible = isStoryChoiceOpen || isStoryReplyVisible;
        bool shouldOfferStoryTalk = !isSpecialOrder && didAcceptOrder && !isLeaving && canTalkToday && !blockedByTutorial && !storyInteractionVisible;
        bool shouldShow = shouldOfferStoryTalk && storyBubble != null;
        bool shouldShowOrderBubble = didAcceptOrder && !isLeaving && !shouldShow && !storyInteractionVisible;

        if (storyBubble != null)
            storyBubble.SetActive(shouldShow);

        if (shouldShowOrderBubble)
        {
            UI_order.SetOrderText(order);
            UI_order.gameObject.SetActive(true);
        }
        else
        {
            UI_order.gameObject.SetActive(false);
        }

        string customerName = CustomerCollectionCatalog.Get(customerType)?.DisplayName ?? customerType.ToString();
        Debug.Log(
            $"[손님 이야기] 말풍선 표시 상태 갱신 | 손님={customerName}" +
            $" | 대화 말풍선 생성됨={(storyBubble != null ? "예" : "아니요")}" +
            $" | 특별 주문 중={(isSpecialOrder ? "예" : "아니요")}" +
            $" | 주문 수락됨={(didAcceptOrder ? "예" : "아니요")}" +
            $" | 퇴장 중={(isLeaving ? "예" : "아니요")}" +
            $" | 오늘 대화 가능={(canTalkToday ? "예" : "아니요")}" +
            $" | 튜토리얼로 차단됨={(blockedByTutorial ? "예" : "아니요")}" +
            $" | 선택지 표시 중={(isStoryChoiceOpen ? "예" : "아니요")}" +
            $" | 답변 표시 중={(isStoryReplyVisible ? "예" : "아니요")}" +
            $" | 대화 제안 조건 충족={(shouldOfferStoryTalk ? "예" : "아니요")}" +
            $" | 대화 말풍선 표시={(storyBubble != null && storyBubble.activeSelf ? "예" : "아니요")}" +
            $" | 주문 말풍선 표시={(UI_order.gameObject.activeSelf ? "예" : "아니요")}" +
            $" | {CustomerStoryProgress.GetTalkDebugState(customerType)}",
            this);
    }




private void ShowSpecialOrderResult(string message)
    {
        isSpecialResultVisible = true;
        string resultMessage = string.IsNullOrWhiteSpace(message) ? "오늘은 여기까지 할게요." : message;
        UI_order.SetMessage($"{resultMessage}", ContinueAfterSpecialOrderResult);
        UI_order.gameObject.SetActive(true);
    }

    private void ContinueAfterSpecialOrderResult()
    {
        if (!isSpecialResultVisible)
            return;

        isSpecialResultVisible = false;
        BeginExit();
    }
}

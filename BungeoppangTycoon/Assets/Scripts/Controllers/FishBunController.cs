using UnityEngine;
using UnityEngine.EventSystems;

public class FishBunController : MonoBehaviour,
    IDragHandler, IEndDragHandler, IPointerClickHandler
{
    #region 변수
    static int numsOfFisBun = 0; //전체 plate에서 구워지는 붕어빵 개수

    GameObject parentMold; //하이어라키 상 부모 오브젝트
    GameObject filling; //붕어삥 오브젝트 산하의 붕어빵 소 게임오브젝트

    public FillingType fillingType; //붕어빵 맛
    public Vector3 spawnPos; //초기 위치

    public CookingState state = CookingState.bottomBatter; //초기 상태
    QualityStatus bakingStatus;
    // 성공한 판매 뒤 Destroy가 적용되기 전 같은 드래그 종료 이벤트가 다시 들어오는 것을 막는다.
    bool isConsumed = false;
    bool isOnDisplay = false;
    // EventSystem 포인터 이벤트와 Unity 마우스 이벤트가 같은 클릭에 함께 들어올 수 있다.
    // 한 번의 클릭이 조리 단계를 두 번 넘기지 않도록 마지막 처리 프레임을 기록한다.
    int lastCookingClickFrame = -1;
    SpriteRenderer fishBunRenderer;
    Vector3 baseScale;
    Color baseColor;
    /*    public QualityStatus batterStatus;
    public QualityStatus fillingStatus;
    public QualityStatus warmStatus;*/

    bool isDraggable 
    {
        get { return (state == CookingState.cooked); }
    }

    // 진열대에 올린 완성품만 손님에게 전달할 수 있다.
    public bool IsOnDisplay => isOnDisplay;

    //굽기 정도 측정 관련 변수
    float startDelta;
    float endDelta;
    float bakingTime 
        { get { return endDelta - startDelta; } }

    const float BaseRequiredTime = 6f; //perfect하게 구워지는 데 걸리는 초
    const float BaseBurntingTime = 15f; //타버리는 데 걸리는 초
    float requiredTime = BaseRequiredTime;
    float burntingTime = BaseBurntingTime;

    #endregion

    #region 클릭 관련 인터페이스 구현
    public void OnDrag(PointerEventData eventData)
    {
        if (Managers.Game.isRunning == false || isDraggable == false)
            return;

        InputManager.Instance?.ClearSelectedFishBun(this);

        //붕어빵 게임 오브젝트 위치 드래그 하는 곳으로 이동
        Vector3 mouse = Camera.main.ScreenToWorldPoint(eventData.position);
        mouse.z = 0;
        gameObject.transform.position = mouse;


    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Managers.Game.isRunning == false || isDraggable == false || isConsumed == true)
            return;

        Vector3 mouse = Camera.main.ScreenToWorldPoint(eventData.position);
        int dropLayerMask = LayerMask.GetMask("DropZone"); //허용 레이어: DropZone게임 오브젝트(진열대/쓰레기통)
        RaycastHit2D hit = Physics2D.Raycast(mouse, Vector2.zero, 0, dropLayerMask);

        if (hit.collider == null || TryPlaceOn(hit.collider.gameObject) == false)
            gameObject.transform.position = spawnPos;

    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HandleCookingClick();
    }

    // 기존 마우스 조작 경로. Physics2DRaycaster/EventSystem 설정에 의존하지 않는다.
    void OnMouseUpAsButton()
    {
        HandleCookingClick();
    }

    void HandleCookingClick()
    {
        if (lastCookingClickFrame == Time.frameCount || Managers.Game.isRunning == false)
            return;

        lastCookingClickFrame = Time.frameCount;
        if (isDraggable)
        {
            InputManager.Instance?.SelectFishBun(this);
            return;
        }

        if (state == CookingState.filled &&
            ToolController.selectedTool != null &&
            ToolController.selectedTool.CompareTag("kettle"))
        {
            FishBunController adjacent = GameplayItemEffects.HasItem(
                    SaveService.Data,
                    GameplayItemEffects.DualPourItemId)
                ? FindAdjacentFishBun(CookingState.filled)
                : null;
            cooking();
            adjacent?.cooking();
            return;
        }

        if ((state == CookingState.topBatter || state == CookingState.cooking) &&
            GameplayItemEffects.HasItem(SaveService.Data, GameplayItemEffects.DoubleGoldenMoldItemId))
        {
            CookingState expectedState = state;
            FishBunController adjacent = FindAdjacentFishBun(expectedState);
            bool advanceTogether = CanAdvanceCookingNow() && adjacent != null &&
                                   adjacent.CanAdvanceCookingNow();
            cooking();
            if (advanceTogether)
                adjacent.cooking();
            return;
        }

        cooking();
    }

    bool CanAdvanceCookingNow() =>
        (state == CookingState.topBatter || state == CookingState.cooking) &&
        Managers.Game.delta - startDelta >= requiredTime;

    FishBunController FindAdjacentFishBun(CookingState expectedState)
    {
        if (parentMold == null || !parentMold.TryGetComponent(out MoldController mold))
            return null;
        MoldController adjacentMold = GameplayItemEffects.FindAdjacentMold(mold);
        FishBunController adjacent = adjacentMold != null ? adjacentMold.ActiveFishBun : null;
        return adjacent != null && adjacent.state == expectedState ? adjacent : null;
    }

    #endregion

    //초기화
    void Start()
    {
        //게임 오브젝트 : 구조&이름
        gameObject.transform.SetParent(parentMold.transform);
        gameObject.name = $"{++numsOfFisBun}";
        //Debug.Log($"{gameObject.name} 붕어빵 생성");

        //위치 조정
        transform.position = spawnPos;
        transform.localScale = parentMold.transform.localScale * 1.4f;
        baseScale = transform.localScale;
        fishBunRenderer = GetComponent<SpriteRenderer>();
        baseColor = fishBunRenderer.color;

        //산하 오브젝트 정리
        filling = Util.FindObject(gameObject, Define.FillingString, true);
        filling.SetActive(false);

        //이미지
        fishBunRenderer.sprite =
            Managers.Resource.LoadSprite("FishBunState_proto", (int)CookingState.bottomBatter);

        //상태
        state = CookingState.bottomBatter;
    }

    public void Set(Vector3 spawnPos, GameObject parentMold)
    {
        this.spawnPos = spawnPos;
        this.parentMold = parentMold;

    }

    void OnDestroy()
    {
        InputManager.Instance?.ClearSelectedFishBun(this);
    }

    public void SetSelected(bool selected)
    {
        if (fishBunRenderer == null)
            fishBunRenderer = GetComponent<SpriteRenderer>();

        transform.localScale = selected ? baseScale * 1.08f : baseScale;
        fishBunRenderer.color = selected
            ? new Color(1f, 0.88f, 0.45f, baseColor.a)
            : baseColor;
    }

    public bool CanUseTool(ToolController tool)
    {
        if (tool == null)
            return false;

        return state switch
        {
            CookingState.bottomBatter => tool.CompareTag("filling"),
            CookingState.filled => tool.CompareTag("kettle"),
            _ => false,
        };
    }

    /// <summary>
    /// 클릭으로 선택한 완성품을 진열대, 손님, 쓰레기통에 놓습니다.
    /// 대상이 맞으면 성공·거절 여부와 관계없이 true를 반환해 클릭을 소비합니다.
    /// </summary>
    public bool TryPlaceOn(GameObject target)
    {
        if (isDraggable == false || isConsumed || target == null)
            return false;

        if (target.CompareTag("displayPlate"))
        {
            if (isOnDisplay == false)
            {
                DisplateController.Set(gameObject);
                isOnDisplay = true;
                TutorialSignals.Raise(TutorialEvent.FishBunDisplayed, gameObject);
                ReleaseMold();
            }

            transform.position = spawnPos;
            return true;
        }

        if (target.CompareTag("customer"))
        {
            // 조리대에서 막 구운 붕어빵은 바로 전달하지 않는다.
            // 먼저 진열대에 놓아야 판매 대상으로 전환된다.
            if (isOnDisplay == false)
            {
                Debug.Log("[조리] 완성된 붕어빵은 진열대에 놓은 뒤 손님에게 건넬 수 있습니다.");
                transform.position = spawnPos;
                return true;
            }

            Debug.Log($"{target.name}에게 붕어빵 제공");
            if (TryServeFishBun(target) == false)
            {
                transform.position = spawnPos;
                return true;
            }

            isConsumed = true;
            RemoveFromDisplayCount();
            TutorialSignals.Raise(TutorialEvent.FishBunServed, gameObject);
            ReleaseMold();
            Destroy(gameObject);
            return true;
        }

        if (target.CompareTag("bin"))
        {
            isConsumed = true;
            RemoveFromDisplayCount();
            ReleaseMold();
            Destroy(gameObject);
            return true;
        }

        return false;
    }

    void RemoveFromDisplayCount()
    {
        if (isOnDisplay == false)
            return;

        DisplateController.Reset(fillingType);
        isOnDisplay = false;
    }

    void ReleaseMold()
    {
        if (parentMold != null && parentMold.TryGetComponent(out MoldController mold))
            mold.IsFilled = false;
    }

    bool TryServeFishBun(GameObject sprite)
    {
        //부모 오브젝트에서 스크립트 추출
        CustomerController controller = sprite.GetComponentInParent<CustomerController>();
        return controller != null && controller.TryEat(fillingType, bakingStatus);
    }
    #region 요리 함수
    void cooking()
    {
        switch (state)
        {

            case CookingState.bottomBatter:
                addFilling();
                break;

            case CookingState.filled:
                addBatter();
                break;

            case CookingState.topBatter:
                baking();
                break;

            case CookingState.cooking:
                cooked();
                break;
        }

        //PolygonCollider2D reset
        Destroy(gameObject.GetComponent<PolygonCollider2D>());
        gameObject.AddComponent<PolygonCollider2D>();
        InputManager.Instance?.RefreshToolTargetHighlights();

    }

    void addFilling()
    {
        if (ToolController.selectedTool == null || ToolController.selectedTool.CompareTag("filling") == false)
            return;

        filling.SetActive(true);
        fillingType = ToolController.selectedTool.filling;
        filling.GetComponent<SpriteRenderer>().sprite
            = Managers.Resource.LoadSprite("fillingChunks", (int)fillingType);


        ++state;
        TutorialSignals.Raise(TutorialEvent.FillingAdded, gameObject);

        //재료 비용 통계
        Managers.Game.RecordFillingUse(fillingType);
        //Debug.Log($"{(int) (Define.FillingPrice[(int)fillingType] * Define.FillingCostRate)}원의 소");
    }

    void addBatter()
    {
        if (ToolController.selectedTool == null || ToolController.selectedTool.CompareTag("kettle") == false)
            return;

        
        // 프리미엄 황금 틀과 당일 조리 피버를 붓기 시작 시점에 한 번만 고정한다.
        float speedMultiplier = GameplayItemEffects.CurrentBakingTimeMultiplier();
        requiredTime = BaseRequiredTime * speedMultiplier;
        burntingTime = BaseBurntingTime * speedMultiplier;
        startDelta = Managers.Game.delta; //1단계 굽기 측정 시작

        GetComponent<SpriteRenderer>().sprite =
            Managers.Resource.LoadSprite("FishBunState_proto", (int)CookingState.topBatter-1);

        //layer 우선순위 처리
        //미구현
        //붕어빵 내용물 투명도 처리
/*            SpriteRenderer sr = filling.GetComponent<SpriteRenderer>();
        Color color = sr.color; 
        color.a = 0.5f;
        sr.color = color;*/

        ++state;
        TutorialSignals.Raise(TutorialEvent.TopBatterAdded, gameObject);
        
    }

    void baking()
    {
        endDelta = Managers.Game.delta; //1단계 굽기 측정 종료

        if (nextState() == true)
        {
            startDelta = endDelta; //2단계 굽기 측정 시작
            TutorialSignals.Raise(TutorialEvent.BakeStageAdvanced, gameObject);
        }


    }

    void cooked()
    {
        endDelta = Managers.Game.delta; //2단계 굽기 측정 종료
        if (nextState())
            TutorialSignals.Raise(TutorialEvent.Cooked, gameObject);

    }

    //다음 단계로 구워지는 지 확인하고 되면 상태 전환
    bool nextState()
    {
        //requiredTime초 이내에는 안구워짐
        if (bakingTime < requiredTime)
            return false;

        int imgIndex;

        //burntingTime초 넘으면 탐
        if (bakingTime > burntingTime)
        {
            state = CookingState.cooked;
            bakingStatus = QualityStatus.excessive;
            imgIndex = 7;

        }
        else
        {
            if (state == CookingState.topBatter)
            {
                imgIndex = (int)CookingState.cooking;
                state = CookingState.cooking;

            }
            else 
                //if (state == CookingState.cooking)
            {
                bakingStatus = bakingTime >= requiredTime * 2f ? QualityStatus.crisp :
                    bakingTime >= requiredTime * 1.35f ? QualityStatus.perfect : QualityStatus.soft;
                imgIndex = (int)CookingState.cooked;
                state = CookingState.cooked;

            }
        }

        GetComponent<SpriteRenderer>().sprite =
            Managers.Resource.LoadSprite("FishBunState_proto", imgIndex);
        
        return true;

    }


    #endregion
}

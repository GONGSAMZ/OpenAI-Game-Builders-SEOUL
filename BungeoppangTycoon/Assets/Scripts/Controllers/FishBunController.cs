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
    /*    public QualityStatus batterStatus;
    public QualityStatus fillingStatus;
    public QualityStatus warmStatus;*/

    bool isDraggable 
    {
        get { return (state == CookingState.cooked); }
    }

    //굽기 정도 측정 관련 변수
    float startDelta;
    float endDelta;
    float bakingTime 
        { get { return endDelta - startDelta; } }

    static float requiredTime = 6; //perfect하게 구워지는 데 걸리는 초
    static float burntingTime = 15; //타버리는 데 걸리는 초

    #endregion

    #region 클릭 관련 인터페이스 구현
    public void OnDrag(PointerEventData eventData)
    {
        if (isDraggable == false)
            return;

        //붕어빵 게임 오브젝트 위치 드래그 하는 곳으로 이동
        Vector3 mouse = Camera.main.ScreenToWorldPoint(eventData.position);
        mouse.z = 0;
        gameObject.transform.position = mouse;


    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isDraggable == false)
            return;

        Vector3 mouse = Camera.main.ScreenToWorldPoint(eventData.position);
        int dropLayerMask = LayerMask.GetMask("DropZone"); //허용 레이어: DropZone게임 오브젝트(진열대/쓰레기통)
        RaycastHit2D hit = Physics2D.Raycast(mouse, Vector2.zero, 0, dropLayerMask);

        if(hit.collider != null)
        {
            //진열대에 놓는 경우
            if (hit.collider.CompareTag("displayPlate"))
            {

                //spawnPos = DisplateController.SetPos(fillingType);
                DisplateController.Set(gameObject);
                transform.position = spawnPos;


            }
            else
            {
                if (hit.collider.CompareTag("customer"))
                {
                    Debug.Log($"{hit.collider.name}에게 붕어빵 제공");
                    DisplateController.Reset(fillingType);
                    ServeFishBun(hit.transform.gameObject);

                }

                Destroy(gameObject);

            }

            //몰드 비우기
            Debug.Log($"{hit.collider.name}에 부딪힘");
            parentMold.GetComponent<MoldController>().IsFilled = false;


        }
        else
        {
            //원위치
            gameObject.transform.position = spawnPos;

        }

    }

    public void OnPointerClick(PointerEventData eventData)
    {
        cooking();
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

        //산하 오브젝트 정리
        filling = Util.FindObject(gameObject, Define.FillingString, true);
        filling.SetActive(false);

        //이미지
        GetComponent<SpriteRenderer>().sprite =
            Managers.Resource.LoadSprite("FishBunState_proto", (int)CookingState.bottomBatter);

        //상태
        state = CookingState.bottomBatter;
    }

    public void Set(Vector3 spawnPos, GameObject parentMold)
    {
        this.spawnPos = spawnPos;
        this.parentMold = parentMold;

    }

    void ServeFishBun(GameObject sprite)
    {
        //부모 오브젝트에서 스크립트 추출
        CustomerController controller = sprite.GetComponentInParent<CustomerController>();
        controller.Eat(fillingType, bakingStatus);

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

    }

    void addFilling()
    {
        if (ToolController.selectedTool.CompareTag("filling") == false)
            return;

        filling.SetActive(true);
        fillingType = ToolController.selectedTool.filling;
        filling.GetComponent<SpriteRenderer>().sprite
            = Managers.Resource.LoadSprite("fillingChunks", (int)fillingType);


        ++state;

        //재료 비용 통계
        Managers.Game.IngredientCost += (int) (Define.FillingPrice[(int)fillingType] * Define.FillingCostRate);
        //Debug.Log($"{(int) (Define.FillingPrice[(int)fillingType] * Define.FillingCostRate)}원의 소");
    }

    void addBatter()
    {
        if (ToolController.selectedTool.CompareTag("kettle") == false)
            return;

        
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
        
    }

    void baking()
    {
        endDelta = Managers.Game.delta; //1단계 굽기 측정 종료

        if (nextState() == true)
            startDelta = endDelta; //2단계 굽기 측정 시작


    }

    void cooked()
    {
        endDelta = Managers.Game.delta; //2단계 굽기 측정 종료
        nextState();

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
                bakingStatus = QualityStatus.perfect;
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

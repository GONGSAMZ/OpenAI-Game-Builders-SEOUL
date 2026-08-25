using UnityEngine;
using UnityEngine.EventSystems;

public class MoldController : MonoBehaviour
    , IPointerClickHandler
{
    private static readonly Color32 GoldenBase = new(205, 145, 28, 255);
    private static readonly Color32 GoldenHighlight = new(255, 218, 92, 255);

    SpriteRenderer moldRenderer;
    Color originalColor;
    bool isGolden;

    bool isFilled = false;
    public bool IsFilled
    {
        get
        {
            return isFilled;
        }
        set {
/*            if (value == false)
                Debug.Log($"{gameObject.name} 몰드 비워짐");*/
            isFilled = value;
        }

    }

    string fishBun = "fishBun";

    void Awake()
    {
        moldRenderer = GetComponent<SpriteRenderer>();
        if (moldRenderer != null)
            originalColor = moldRenderer.color;

        Managers.Game.InitAction -= InitMold;
        Managers.Game.InitAction += InitMold;

        if (GamePlatformClient.Instance != null)
            GamePlatformClient.Instance.StoreStateChanged += RefreshEquipmentAppearance;
        RefreshEquipmentAppearance();
    }

    void OnDestroy()
    {
        Managers.Game.InitAction -= InitMold;
        if (GamePlatformClient.Instance != null)
            GamePlatformClient.Instance.StoreStateChanged -= RefreshEquipmentAppearance;
    }

    void Update()
    {
        if (!isGolden || moldRenderer == null)
            return;

        float sheen = (Mathf.Sin(Time.unscaledTime * 2.4f) + 1f) * 0.5f;
        moldRenderer.color = Color.Lerp(GoldenBase, GoldenHighlight, sheen * 0.35f);
    }

    void RefreshEquipmentAppearance()
    {
        isGolden = GamePlatformClient.Instance != null && GamePlatformClient.Instance.IsGoldenPanEquipped;
        if (moldRenderer != null)
            moldRenderer.color = isGolden ? GoldenBase : originalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryCreateFishBun();
    }

    // 기존 마우스 조작 경로. UI 포인터 이벤트가 없는 환경에서도 틀을 클릭해 반죽을 붓는다.
    void OnMouseUpAsButton()
    {
        TryCreateFishBun();
    }

    void TryCreateFishBun()
    {
        if (Managers.Game.isRunning == false || ToolController.selectedTool == null)
            return;

        if (ToolController.selectedTool.CompareTag("kettle") == false)
            return;

        if (!TryPourBottomBatter())
            return;

        if (!GameplayItemEffects.HasItem(SaveService.Data, GameplayItemEffects.DualPourItemId))
            return;

        GameplayItemEffects.FindAdjacentMold(this)?.TryPourBottomBatter();
    }

    public FishBunController ActiveFishBun => GetComponentInChildren<FishBunController>(true);

    public bool TryPourBottomBatter()
    {
        if (Managers.Game.isRunning == false || isFilled || ToolController.selectedTool == null ||
            ToolController.selectedTool.CompareTag("kettle") == false)
            return false;

        isFilled = true;
        Debug.Log("생성");
        InstanciateFishBun();
        return true;
    }

    void InstanciateFishBun()
    {
        if(ToolController.selectedTool.CompareTag("kettle"))
        {
            GameObject _fishBun = Managers.Resource.Instantiate($"Prefabs/{fishBun}");
            _fishBun.GetComponent<FishBunController>().Set(transform.position, gameObject);

            TutorialSignals.Raise(TutorialEvent.MoldFilled, _fishBun);

            //재료비 통계
            Managers.Game.IngredientCost += Define.BatterCost; //반죽 원가
        }


    }

    void InitMold()
    {
        isFilled = false;

        //붕어빵 전체 삭제
        int childCount = transform.childCount;
        if (childCount == 0)
            return;

        for (int i = 0; i < childCount; ++i)
            Destroy(transform.GetChild(i).gameObject);

    }
}


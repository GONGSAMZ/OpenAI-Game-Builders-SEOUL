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
        if (Managers.Game.isRunning == false || ToolController.selectedTool == null)
            return;

        if (ToolController.selectedTool.CompareTag("kettle") == false)
            return;

/*        if (IsFilled == false)
        {
            
            IsFilled = true;
        }*/
        Util.ExecuteOnce(
            () => {
                Debug.Log("생성");
                InstanciateFishBun(); },
            ref isFilled, false
            );
    }

    void InstanciateFishBun()
    {
        if(ToolController.selectedTool.CompareTag("kettle"))
        {
            GameObject _fishBun = Managers.Resource.Instantiate($"Prefabs/{fishBun}");
            _fishBun.GetComponent<FishBunController>().Set(transform.position, gameObject);

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


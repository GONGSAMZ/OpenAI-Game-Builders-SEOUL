using UnityEngine;
using UnityEngine.EventSystems;

public class MoldController : MonoBehaviour
    , IPointerClickHandler
{

    bool isFilled = false;
    public bool IsFilled
    {
        get
        {
            return isFilled;
        }
        set {
/*            if (value == false)
                Debug.Log($"{gameObject.name} ¸ôµå ºñ¿öÁü");*/
            isFilled = value;
        }

    }

    string fishBun = "fishBun";

    void Awake()
    {
        Managers.Game.InitAction -= InitMold;
        Managers.Game.InitAction += InitMold;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Managers.Game.isRunning == false || ToolController.selectedTool == null)
            return;

/*        if (IsFilled == false)
        {
            
            IsFilled = true;
        }*/
        Util.ExecuteOnce(
            () => {
                Debug.Log("»ý¼º");
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

            //Àç·áºñ Åë°è
            Managers.Game.IngredientCost += Define.BatterCost; //¹ÝÁ× ¿ø°¡ 
        }


    }

    void InitMold()
    {
        isFilled = false;

        //ºØ¾î»§ ÀüÃ¼ »èÁ¦
        int childCount = transform.childCount;
        if (childCount == 0)
            return;

        for (int i = 0; i < childCount; ++i)
            Destroy(transform.GetChild(i).gameObject);

    }
}


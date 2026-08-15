using UnityEngine;

public class ToolController : MonoBehaviour
{
    public static ToolController selectedTool;
    [SerializeField] public FillingType filling;

    Vector3 moveDir = new Vector3(0, 1, 0);

    float originZRotation;
    float zRotation = -20;

    Vector3 originScale;

    int originSortingOrder;
    int maxSortingOrder = 10;

    void Awake()
    {
        Managers.Game.InitAction -= InitIngredient;
        Managers.Game.InitAction += InitIngredient;
    }

    void Start()
    {
        originSortingOrder = GetComponent<SpriteRenderer>().sortingOrder;
        originZRotation = transform.rotation.eulerAngles.z;
        originScale = transform.localScale;
    }

    void OnMouseDown()
    {
        //선택된 이전 물건이 있었으면
        if (selectedTool != null)
            pickDown(); //이전 pickup 물건 내려 놓기

        pickUp();

    }

    //객체 집어 올리는 메서드
    void pickUp()
    {
        transform.position += moveDir; //위로 올리기
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.Euler(0, 0, originZRotation+zRotation); // 비스듬히 회전
        transform.localScale = originScale;

        GetComponent<SpriteRenderer>().sortingOrder = maxSortingOrder;
        selectedTool = this;

    }

    //객체 내려놓는 메서드
    void pickDown()
    {
        selectedTool.transform.position -= moveDir;
        transform.localScale = Vector3.one;
        selectedTool.transform.rotation = Quaternion.Euler(0, 0, selectedTool.originZRotation);
        transform.localScale = originScale;
        selectedTool.GetComponent<SpriteRenderer>().sortingOrder = selectedTool.originSortingOrder;
        selectedTool = null;
    }

    void InitIngredient()
    {
        if(selectedTool != null)
            selectedTool.pickDown();

    }
}

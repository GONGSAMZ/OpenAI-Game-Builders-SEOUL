using UnityEngine;
using UnityEngine.EventSystems;

public class ToolController : MonoBehaviour, IPointerClickHandler
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
        Select();

    }

    // 새 Input System + Physics2DRaycaster 경로에서도 도구를 클릭해 선택한다.
    // OnMouseDown은 이전 입력 경로와의 호환용으로 그대로 둔다.
    public void OnPointerClick(PointerEventData eventData)
    {
        Select();
    }

    public void Select()
    {
        if (Managers.Game.isRunning == false)
            return;

        if (selectedTool == this)
            return;

        InputManager.Instance?.ClearSelectedFishBun();
        DeselectCurrent();

        transform.position += moveDir; //위로 올리기
        transform.rotation = Quaternion.Euler(0, 0, originZRotation+zRotation); // 비스듬히 회전
        transform.localScale = originScale;

        GetComponent<SpriteRenderer>().sortingOrder = maxSortingOrder;
        selectedTool = this;
        InputManager.Instance?.ShowToolTargets(this);

        TutorialSignals.Raise(TutorialEvent.ToolSelected, gameObject);

    }

    public void Deselect()
    {
        if (selectedTool != this)
            return;

        transform.position -= moveDir;
        transform.rotation = Quaternion.Euler(0, 0, originZRotation);
        transform.localScale = originScale;
        GetComponent<SpriteRenderer>().sortingOrder = originSortingOrder;
        selectedTool = null;
        InputManager.Instance?.ClearTargetHighlights();
    }

    public static void DeselectCurrent()
    {
        selectedTool?.Deselect();
    }

    void InitIngredient()
    {
        DeselectCurrent();

    }
}

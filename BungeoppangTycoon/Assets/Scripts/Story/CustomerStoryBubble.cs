using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>손님 머리 위 말풍선 클릭을 부모 손님의 대화 열기로 전달합니다.</summary>
public sealed class CustomerStoryBubble : MonoBehaviour, IPointerClickHandler
{
    private CustomerController owner;
    private int lastClickFrame = -1;
    public void SetOwner(CustomerController value) => owner = value;
    public void OnPointerClick(PointerEventData eventData) => Open();
    private void OnMouseUpAsButton() => Open();
    private void Open()
    {
        if (lastClickFrame == Time.frameCount) return;
        lastClickFrame = Time.frameCount;
        owner?.TryOpenStoryDialogue();
    }
}

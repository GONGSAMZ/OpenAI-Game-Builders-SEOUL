using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_EventHandler : MonoBehaviour, IPointerClickHandler
{
    //클릭 핸들러
    public Action OnClickHandler = null;

    /*매개 변수 eventData
     * IPointerClickHandler의 시그니처로 있어서 
     * 사용하지 않더라도 반드시 있음
     */
    public void OnPointerClick(PointerEventData eventData)
    {
         OnClickHandler?.Invoke(); //클릭 액션 실행
    }
}

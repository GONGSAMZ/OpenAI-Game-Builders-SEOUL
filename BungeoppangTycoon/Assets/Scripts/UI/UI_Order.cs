using System;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Order : UI_Base, IPointerClickHandler
{
    TextMeshProUGUI orderText;
    Action messageClickAction;
    public Slider slider;

    protected override void Init()
    {
        // 1. 캔버스 스페이스 렌더링 모드로 초기화
        SetWorldUI();

        // 2. 변수 맵핑
        orderText = Util.Find<TextMeshProUGUI>(gameObject, "orderText");
        slider = Util.Find<Slider>(gameObject, "slider");

        //slider.gameObject.AddEvent(SetOrder);

        // 3. 손님 분노 게이지 초기화
        slider.value = 0f; 

    }



    public void SetOrderText(Dictionary<FillingType, int> orders)
    {
        messageClickAction = null;
        slider.gameObject.SetActive(true);

        //기존 텍스트 없애기
        orderText.text = null;

        foreach (var order in orders)
            orderText.text += $"{Define.FillingText[(int)order.Key]} * {order.Value}개 \n";

    }

    public void SetMessage(string message, Action onClick = null)
    {
        messageClickAction = onClick;
        orderText.text = message;
        slider.gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        messageClickAction?.Invoke();
    }





}

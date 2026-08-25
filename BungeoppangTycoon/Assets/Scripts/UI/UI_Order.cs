using UnityEngine;
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

        orderText.text = null;
        foreach (var order in orders)
            orderText.text += $"{Define.FillingText[(int)order.Key]} * {order.Value}개 \n";
    }

    public void SetSpecialOrderStatus()
    {
        messageClickAction = null;
        slider.gameObject.SetActive(true);
        // 특별 주문의 맛·수량·유추 가능한 안내는 표시하지 않는다.
        orderText.text = "특별 주문 진행 중";
    }

public void SetMessage(string message, Action onClick = null)
    {
        messageClickAction = onClick;
        // 대사도 손님 프리팹에 이미 연결된 원형 slider 말풍선 안에 표시한다.
        // slider의 채움값은 0으로 유지해 분노 게이지가 대사 위에 보이지 않게 한다.
        slider.gameObject.SetActive(true);
        slider.value = 0f;
        orderText.text = message;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        messageClickAction?.Invoke();
    }



}

using UnityEngine;
using System;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_Order : UI_Base, IPointerClickHandler
{
    TextMeshProUGUI orderText;
    Image messageBubble;
    Action messageClickAction;
    static Sprite fallbackBubbleSprite;
    public Slider slider;

    protected override void Init()
    {
        // 1. 캔버스 스페이스 렌더링 모드로 초기화
        SetWorldUI();

        // 2. 변수 맵핑
        orderText = Util.Find<TextMeshProUGUI>(gameObject, "orderText");
        slider = Util.Find<Slider>(gameObject, "slider");
        messageBubble = GetComponent<Image>();
        if (messageBubble != null)
        {
            // Unity 버전에 없는 기본 UI 파일을 참조하지 않고, 내장 흰색 텍스처로 안전한 배경을 만든다.
            messageBubble.sprite = GetFallbackBubbleSprite();
            messageBubble.type = Image.Type.Simple;
            messageBubble.preserveAspect = false;
            messageBubble.color = new Color(1f, 0.94f, 0.70f, 0.96f);
            messageBubble.raycastTarget = false;
            messageBubble.enabled = false;
        }

        //slider.gameObject.AddEvent(SetOrder);

        // 3. 손님 분노 게이지 초기화
        slider.value = 0f; 

    }



public void SetOrderText(Dictionary<FillingType, int> orders)
    {
        messageClickAction = null;
        SetMessageBubbleVisible(false);
        slider.gameObject.SetActive(true);

        orderText.text = null;
        foreach (var order in orders)
            orderText.text += $"{Define.FillingText[(int)order.Key]} * {order.Value}개 \n";
    }

public void SetSpecialOrderStatus()
    {
        messageClickAction = null;
        SetMessageBubbleVisible(true);
        slider.gameObject.SetActive(true);
        // 특별 주문의 맛·수량·유추 가능한 안내는 표시하지 않는다.
        orderText.text = "특별 주문 진행 중";
    }

    void SetMessageBubbleVisible(bool visible)
    {
        if (messageBubble != null)
            messageBubble.enabled = visible;
    }


public void SetMessage(string message, Action onClick = null)
    {
        messageClickAction = onClick;
        SetMessageBubbleVisible(true);
        orderText.text = message;
        slider.gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        messageClickAction?.Invoke();
    }







private static Sprite GetFallbackBubbleSprite()
    {
        if (fallbackBubbleSprite != null)
            return fallbackBubbleSprite;

        Texture2D texture = Texture2D.whiteTexture;
        fallbackBubbleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        return fallbackBubbleSprite;
    }
}

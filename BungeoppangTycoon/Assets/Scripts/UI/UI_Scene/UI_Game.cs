using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine;

public class UI_Game : UI_Base
{
    #region 게임 요소
    enum TMP {
        dayText,
        timeText,
        moneyText,
        redBeanCoinText,
    }

    enum Btns {
        settingsButton,
        toggleViewButton,
        inAppMarketButton,
    }

    static GameObject ordersPanel;

    #endregion

    int minute
    {
        //시간은 10분단위로 표시
        get { return Managers.Game.minute / 10; }
    }

    public static Action orderUpdateAction = null;

    protected override void Init()
    {
        //바인딩
        Bind<TextMeshProUGUI>(typeof(TMP));
        Bind<Button>(typeof(Btns));

        //데이터
        GetTMP((int)TMP.dayText).text = $"Day {Managers.Game.Day}";
        GetTMP((int)TMP.moneyText).text = $"{Managers.Game.Money.ToString("N0")} 원";
        GetButton((int)Btns.toggleViewButton).gameObject.AddEvent(CameraController.toggleCameraAction);
        GetButton((int)Btns.settingsButton).gameObject.AddEvent(settingsBtnFunc);
        GetButton((int)Btns.inAppMarketButton).gameObject.AddEvent(inAppMarketBtnFunc);

        if (GamePlatformClient.Instance != null)
            GamePlatformClient.Instance.StoreStateChanged += RefreshPlatformCurrency;
        RefreshPlatformCurrency();

        //이벤트 구독

        orderUpdateAction -= orderUpdate;
        orderUpdateAction += orderUpdate;

        ordersPanel = Util.FindObject(gameObject, "ordersPanel");
        // 설정 화면에서 돌아와 UI_Game이 새로 만들어진 경우에도,
        // 프리팹 기본값 대신 현재 주문 상태를 바로 표시한다.
        orderUpdate();

        // 첫 플레이에서만 게임 위에 조리 튜토리얼을 띄운다.
        if (UI_Tutorial.ShouldShow())
            Managers.UI.ShowUI<UI_Tutorial>(false);

    }

    private void OnDestroy()
    {
        if (GamePlatformClient.Instance != null)
            GamePlatformClient.Instance.StoreStateChanged -= RefreshPlatformCurrency;
    }


    void Update()
    {
        //분은 10의 단위로만 바꿈
        GetTMP((int)TMP.timeText).text = ($"{Managers.Game.hour} : {minute}0");
        GetTMP((int)TMP.moneyText).text = ($"{Managers.Game.Money.ToString("N0")} 원 ");


    }

    void settingsBtnFunc()
    {
        //Managers.Game.
        Managers.UI.CloseUI();
        Managers.UI.ShowUI<UI_Settings>();
    }

    void inAppMarketBtnFunc()
    {
        Managers.UI.ShowUI<UI_InAppMarket>(false);
    }

    void RefreshPlatformCurrency()
    {
        GamePlatformClient client = GamePlatformClient.Instance;
        GetTMP((int)TMP.redBeanCoinText).text = client != null && client.IsLoggedIn
            ? $"팥 코인 {client.RedBeanCoinBalance:N0}개"
            : "팥 코인 —";
    }

    static void orderUpdate()
    {
        int numOfPanel = 0;
        GameObject panel; //ordersPanel산하의 panel

        //주문 종류&개수UI 표시
        foreach (var order in Managers.Game.Order)
        {
            panel = ordersPanel.transform.GetChild(numOfPanel).gameObject;
            
            panel.SetActive(true);
            panel.transform.GetChild(0).GetComponent<Image>().sprite =
                Managers.Resource.LoadSprite("fillingChunks", (int) order.Key);
            panel.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text =
                order.Value.ToString();

            ++numOfPanel;

        }

        //나머지 비활성화
        Util.checkNull(ordersPanel);
        for(int j = numOfPanel;  j < ordersPanel.transform.childCount; ++j)
        {
            panel = ordersPanel.transform.GetChild(j).gameObject;
            panel.SetActive(false);
        }
    }
}

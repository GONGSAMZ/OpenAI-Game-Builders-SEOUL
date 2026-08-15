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
    }

    enum Btns {
        settingsButton,
        toggleViewButton,
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

        //이벤트 구독

        orderUpdateAction -= orderUpdate;
        orderUpdateAction += orderUpdate;

        ordersPanel = Util.FindObject(gameObject, "ordersPanel");

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

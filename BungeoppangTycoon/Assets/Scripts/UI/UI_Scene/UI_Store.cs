using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_Store : UI_Base
{
    #region 화면 요소
    enum Btns
    {
        FillingButton,
        SkillButton,
        ToolButton,
        MarketingButton,
    }

    Button NextDayButton;
    TextMeshProUGUI MoneyNum;
    TextMeshProUGUI MoneyText;
    TextMeshProUGUI TitleText;

    #endregion

    protected override void Init()
    {

        Bind<Button>(typeof(Btns));
        BindChilds<Button, TextMeshProUGUI> (typeof(Btns), "Text (TMP)");

        NextDayButton = Util.Find<Button>(gameObject, "NextDayButton");
        MoneyNum = Util.Find<TextMeshProUGUI>(gameObject, "MoneyNum");
        TitleText = Util.Find<TextMeshProUGUI>(gameObject, "TitleText");
        MoneyText = Util.Find<TextMeshProUGUI>(gameObject, "MoneyText");

        
        for(int i = 0; i < Define.UI_StoreText.Length; ++i)
            GetTMP(i).text = Define.UI_StoreText[i];


        MoneyText.text = "돈";
        TitleText.text = "상점";
        MoneyNum.text = Managers.Game.Money.ToString("N0")+" 원";

        AddEvent(NextDayButton.gameObject, Managers.Game.StartNextDay);
        AddEvent(GetButton((int)Btns.MarketingButton).gameObject, OpenHiveShop);

    }

    void Fuc()
    {
        
    }

    void OpenHiveShop()
    {
        if (GamePlatformClient.Instance == null)
        {
            Debug.LogError("GamePlatformClient가 초기화되지 않았습니다.");
            return;
        }

        GamePlatformClient.Instance.OpenHiveWebShop();
    }
}

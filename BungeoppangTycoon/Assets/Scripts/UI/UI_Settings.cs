using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_Settings : UI_Base
{
    #region 화면요소
    enum Buttons
    {
        QuitBtn,

    }
    Button ExitBtn;

    #endregion

    protected override void Init()
    {
        Managers.Game.isRunning = false;

        ExitBtn = Util.Find<Button>(gameObject, "ExitBtn");
        Bind<Button>(typeof(Buttons));
        BindChilds<Button, TextMeshProUGUI>(typeof(Buttons), "Text (TMP)");

        for (int i = 0; i < dic[typeof(TextMeshProUGUI)].Length; ++i)
            GetTMP(i).text = Define.UI_Settings[i];

        ExitBtn.gameObject.AddEvent(Exit);
        GetButton((int)Buttons.QuitBtn).gameObject.AddEvent(Quit);

    }

    void Exit()
    {
        Managers.UI.CloseUI();
        Managers.UI.ShowUI<UI_Game>();
        Managers.Game.isRunning = true;
    }

    void Quit()
    {
        Managers.Game.QuitGame();
    }
}

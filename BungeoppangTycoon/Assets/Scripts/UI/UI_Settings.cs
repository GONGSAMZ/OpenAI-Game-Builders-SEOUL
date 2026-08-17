using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Settings : UI_Base
{
    const string ExitButtonName = "ExitBtn";
    const string QuitButtonName = "QuitButton";
    const string LegacyQuitButtonName = "QuitBtn";

    Button exitButton;

    protected override void Init()
    {
        Managers.Game.isRunning = false;

        exitButton = Util.Find<Button>(gameObject, ExitButtonName, true);
        Button quitButton = Util.Find<Button>(gameObject, QuitButtonName, true);
        Button legacyQuitButton = Util.Find<Button>(gameObject, LegacyQuitButtonName, true);

        if (exitButton == null)
        {
            Debug.LogError($"[UI_Settings] '{ExitButtonName}' 버튼을 찾지 못했습니다.", gameObject);
        }
        else
        {
            exitButton.gameObject.AddEvent(Exit);
        }

        if (quitButton == null && legacyQuitButton == null)
        {
            Debug.LogError(
                $"[UI_Settings] 종료 버튼을 찾지 못했습니다. '{QuitButtonName}' 이름을 확인하세요.",
                gameObject);
            return;
        }

        BindQuitButton(quitButton);

        // 이전 설정 화면이 프리팹 안에 남아 있어도 해당 탭이 다시 쓰일 수 있도록 함께 연결한다.
        if (legacyQuitButton != quitButton)
            BindQuitButton(legacyQuitButton);
    }

    void BindQuitButton(Button button)
    {
        if (button == null)
            return;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null && Define.UI_Settings.Length > 0)
            label.text = Define.UI_Settings[0];

        button.gameObject.AddEvent(Quit);
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

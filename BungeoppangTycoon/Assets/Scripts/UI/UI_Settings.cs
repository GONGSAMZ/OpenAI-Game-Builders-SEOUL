using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>설정 메인 메뉴입니다. 상세 기능은 각 전용 팝업 프리팹이 담당합니다.</summary>
public class UI_Settings : UI_Base
{
    Button exitButton, quitButton, helpButton, achievementButton, documentsButton;
    Button settingsButton;

    protected override void Init()
    {
        Managers.Game.isRunning = false;
        exitButton = FindButton("ExitBtn"); quitButton = FindButton("QuitBtn"); helpButton = FindButton("HelpButton"); settingsButton = FindButton("SettingBtn");
        achievementButton = FindButton("AchivementButton"); documentsButton = FindButton("DocumentsButton");
        BindButton(exitButton, Close); BindButton(quitButton, Close); BindButton(settingsButton, OpenOptions);
        BindButton(documentsButton, OpenCollection);
        if (helpButton != null) helpButton.gameObject.SetActive(false);
        if (achievementButton != null) achievementButton.gameObject.SetActive(false);
        SetContinueLabel(quitButton);
        SetLabel(documentsButton, "도감");
    }

    Button FindButton(string name) => Util.Find<Button>(gameObject, name, true);
    void BindButton(Button button, UnityEngine.Events.UnityAction action) { if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); } }
    void SetContinueLabel(Button button)
    {
        TextMeshProUGUI label = button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (label != null) label.text = "계속하기";
    }
    void OpenOptions() => Managers.UI.ShowUI<UI_SettingsOptions>(false);
    void OpenCollection()
    {
        if (Resources.Load<GameObject>("Prefabs/UI/UI_Collection") != null)
            Managers.UI.ShowUI<UI_Collection>(false);
        else
            CollectionPopupFactory.CreateRoot();
    }
    void SetLabel(Button button, string labelText)
    {
        TextMeshProUGUI label = button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (label != null) label.text = labelText;
    }
    void Close() { Managers.UI.CloseUI(); Managers.UI.ShowUI<UI_Game>(); Managers.Game.isRunning = true; }
}

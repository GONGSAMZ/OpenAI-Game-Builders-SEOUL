using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>설정 메인 메뉴입니다. 상세 기능은 각 전용 팝업 프리팹이 담당합니다.</summary>
public class UI_Settings : UI_Base
{
    Button headerCloseButton, quitButton, helpButton, achievementButton, documentsButton, resetButton;
    Button settingsButton;
    bool wasGameRunning;
    bool isClosing;

    protected override void Init()
    {
        wasGameRunning = Managers.Game.isRunning;
        Managers.Game.isRunning = false;
        headerCloseButton = FindButton("CloseButton"); quitButton = FindButton("QuitBtn"); helpButton = FindButton("HelpButton"); settingsButton = FindButton("SettingBtn");
        achievementButton = FindButton("AchivementButton"); documentsButton = FindButton("DocumentsButton"); resetButton = FindButton("ResetButton");
        BindButton(headerCloseButton, Close); BindButton(quitButton, Close); BindButton(settingsButton, OpenOptions);
        BindButton(documentsButton, OpenCollection);
        BindButton(achievementButton, SaveUiFactory.ShowAchievements);
        BindButton(resetButton, SaveUiFactory.ShowResetConfirmation);
        if (helpButton != null) helpButton.gameObject.SetActive(false);
        if (achievementButton != null) achievementButton.gameObject.SetActive(true);
        SetContinueLabel(quitButton);
        SetLabel(documentsButton, "도감");
        SetLabel(achievementButton, "업적");
    }

    Button FindButton(string name) => Util.Find<Button>(gameObject, name, true);
    void BindButton(Button button, UnityEngine.Events.UnityAction action) { if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); } }
    void SetContinueLabel(Button button)
    {
        TextMeshProUGUI label = button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (label != null) label.text = "계속하기";
    }
    void OpenOptions() => Managers.UI.ShowUI<UI_SettingsOptions>(false);
    void Update()
    {
        if (GameInput.KeyPressed(Key.Escape)) Close();
    }
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
    void Close()
    {
        if (isClosing) return;
        isClosing = true;
        Managers.UI.CloseUI();
        Managers.UI.ShowUI<UI_Game>();
        Managers.Game.isRunning = wasGameRunning;
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>설정 메인 메뉴입니다. 상세 기능은 각 전용 팝업 프리팹이 담당합니다.</summary>
public class UI_Settings : UI_Base
{
    Button exitButton, quitButton, helpButton, achievementButton, documentsButton, resetGameButton;
    Button settingsButton;

    protected override void Init()
    {
        Managers.Game.isRunning = false;
        exitButton = FindButton("ExitBtn");
        quitButton = FindButton("QuitButton") ?? FindButton("QuitBtn");
        helpButton = FindButton("HelpButton");
        settingsButton = FindButton("SettingButton") ?? FindButton("SettingBtn");
        achievementButton = FindButton("AchivementButton"); documentsButton = FindButton("DocumentsButton");
        BindButton(exitButton, Close); BindButton(quitButton, Close); BindButton(settingsButton, OpenOptions);
        BindButton(documentsButton, OpenCollection);
        if (helpButton != null) helpButton.gameObject.SetActive(false);
        if (achievementButton != null)
        {
            achievementButton.gameObject.SetActive(true);
            BindButton(achievementButton, SaveUiFactory.ShowAchievements);
        }
        resetGameButton = CreateResetButton();
        BindButton(resetGameButton, SaveUiFactory.ShowResetConfirmation);
        SetContinueLabel(quitButton);
        SetLabel(documentsButton, "도감");
        SetLabel(achievementButton, "업적");
    }

    Button FindButton(string name)
    {
        // 같은 이름의 이전 버튼이 비활성 상태로 남아 있어도,
        // 플레이어가 실제로 보고 누르는 버튼을 먼저 연결합니다.
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == name && button.gameObject.activeInHierarchy)
                return button;
        }

        return Util.Find<Button>(gameObject, name, true);
    }
    void BindButton(Button button, UnityEngine.Events.UnityAction action) { if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); } }
    void SetContinueLabel(Button button)
    {
        TextMeshProUGUI label = button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (label != null) label.text = "계속하기";
    }
    void OpenOptions() => Managers.UI.ShowUI<UI_SettingsOptions>(false);
    Button CreateResetButton()
    {
        Button existing = FindButton("ResetGameButton");
        if (existing != null) return existing;
        Button source = documentsButton ?? achievementButton ?? settingsButton;
        if (source == null) return null;
        GameObject clone = Instantiate(source.gameObject, source.transform.parent);
        clone.name = "ResetGameButton";
        clone.transform.SetSiblingIndex(Mathf.Max(0, source.transform.parent.childCount - 1));
        Button button = clone.GetComponent<Button>();
        Image image = clone.GetComponent<Image>();
        if (image != null) image.color = new Color(.58f, .19f, .16f);
        SetLabel(button, "게임 플레이 초기화");
        return button;
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
    void Close() { Managers.UI.CloseUI(); Managers.UI.ShowUI<UI_Game>(); Managers.Game.isRunning = true; }
}

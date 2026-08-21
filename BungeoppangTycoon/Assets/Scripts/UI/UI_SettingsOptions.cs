using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 가게 설정 프리팹의 음량·키보드 안내·플레이 초기화 조작을 담당합니다.
/// Unity가 프리팹 스크립트를 안정적으로 연결하도록 전용 파일에 둡니다.
/// </summary>
public sealed class UI_SettingsOptions : UI_SettingsPopupBase
{
    private Slider volumeSlider;
    private Button keyboardHintButton;
    private Button keyboardHelpButton;
    private TextMeshProUGUI keyboardHintButtonLabel;
    private Button volumeMinusButton;
    private Button volumePlusButton;
    private Button resetGameButton;
    private Button footerCloseButton;
    private TextMeshProUGUI volumeValueText;
    private TextMeshProUGUI volumeStateText;
    private TextMeshProUGUI keyboardHintStateText;
    private TextMeshProUGUI keyboardHintDescriptionText;
    private RectTransform keyboardHintToggleThumb;
    private Image keyboardHintToggleBackground;

    protected override void Init()
    {
        base.Init();

        volumeSlider = Util.Find<Slider>(gameObject, "VolumeSlider", true);
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(SaveService.Data.settings.masterVolume);
            volumeSlider.onValueChanged.AddListener(ApplyVolume);
            ApplyVolume(volumeSlider.value);
        }

        volumeMinusButton = Util.Find<Button>(gameObject, "VolumeMinusButton", true);
        volumePlusButton = Util.Find<Button>(gameObject, "VolumePlusButton", true);
        if (volumeMinusButton != null)
            volumeMinusButton.onClick.AddListener(() => AdjustVolume(-0.1f));
        if (volumePlusButton != null)
            volumePlusButton.onClick.AddListener(() => AdjustVolume(0.1f));

        keyboardHintButton = Util.Find<Button>(gameObject, "KeyboardHintToggle", true)
            ?? Util.Find<Button>(gameObject, "KeyboardHintButton", true);
        keyboardHelpButton = Util.Find<Button>(gameObject, "KeyboardHelpButton", true);
        keyboardHintButtonLabel = Util.Find<TextMeshProUGUI>(gameObject, "KeyboardHintButtonLabel", true);
        volumeValueText = Util.Find<TextMeshProUGUI>(gameObject, "VolumeValueText", true);
        volumeStateText = Util.Find<TextMeshProUGUI>(gameObject, "VolumeStateText", true);
        keyboardHintStateText = Util.Find<TextMeshProUGUI>(gameObject, "KeyboardHintStateText", true);
        keyboardHintDescriptionText = Util.Find<TextMeshProUGUI>(gameObject, "KeyboardHintDescriptionText", true);
        keyboardHintToggleThumb = Util.Find<RectTransform>(gameObject, "KeyboardHintToggleThumb", true);
        keyboardHintToggleBackground = keyboardHintButton != null
            ? keyboardHintButton.GetComponent<Image>()
            : null;
        if (keyboardHintButton != null)
            keyboardHintButton.onClick.AddListener(ToggleKeyboardHints);
        if (keyboardHelpButton != null)
            keyboardHelpButton.onClick.AddListener(() => Managers.UI.ShowUI<UI_SettingsHelp>(false));

        resetGameButton = Util.Find<Button>(gameObject, "ResetGameButton", true);
        if (resetGameButton != null)
            resetGameButton.onClick.AddListener(SaveUiFactory.ShowResetConfirmation);

        footerCloseButton = Util.Find<Button>(gameObject, "FooterCloseButton", true);
        if (footerCloseButton != null)
            footerCloseButton.onClick.AddListener(() => Managers.UI.CloseUI(false));

        RefreshDescription();
        if (EventSystem.current != null && closeButton != null)
            EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Managers.UI.CloseUI(false);
    }

    protected override void Render()
    {
        titleText.text = "가게 설정";
    }

    private void ApplyVolume(float value)
    {
        SaveService.Instance.SetMasterVolume(value);
        RefreshDescription();
    }

    private void ToggleKeyboardHints()
    {
        KeyboardHintSettings.SetEnabled(!KeyboardHintSettings.IsEnabled);
        RefreshDescription();
    }

    private void AdjustVolume(float amount)
    {
        if (volumeSlider != null)
            volumeSlider.value = Mathf.Clamp01(volumeSlider.value + amount);
    }

    private void RefreshDescription()
    {
        int volumePercent = Mathf.RoundToInt(AudioListener.volume * 100f);
        bool keyboardHintsEnabled = KeyboardHintSettings.IsEnabled;

        if (bodyText != null)
            bodyText.text = $"전체 음량  {volumePercent}%\n\n키보드 단축키 안내를 표시할지 선택하세요.";
        if (keyboardHintButtonLabel != null)
            keyboardHintButtonLabel.text = keyboardHintsEnabled ? "키보드 조작 안내  켜짐" : "키보드 조작 안내  꺼짐";
        if (volumeValueText != null)
            volumeValueText.text = $"{volumePercent}%";
        if (volumeStateText != null)
            volumeStateText.text = volumePercent >= 95 ? "현재 소리: 가장 크게"
                : volumePercent >= 50 ? "현재 소리: 적당하게"
                : volumePercent > 0 ? "현재 소리: 작게"
                : "현재 소리: 꺼짐";
        if (keyboardHintStateText != null)
            keyboardHintStateText.text = keyboardHintsEnabled ? "켜짐" : "꺼짐";
        if (keyboardHintToggleBackground != null)
            keyboardHintToggleBackground.color = keyboardHintsEnabled
                ? new Color(0.18f, 0.42f, 0.44f, 1f)
                : new Color(0.40f, 0.38f, 0.33f, 1f);
        if (keyboardHintDescriptionText != null)
            keyboardHintDescriptionText.text = "단축키를 화면에 표시해요.";
        if (keyboardHintToggleThumb != null)
        {
            keyboardHintToggleThumb.anchorMin = keyboardHintToggleThumb.anchorMax =
                new Vector2(keyboardHintsEnabled ? 1f : 0f, 0.5f);
            keyboardHintToggleThumb.anchoredPosition = new Vector2(keyboardHintsEnabled ? -29f : 29f, 0f);
        }
        if (volumeMinusButton != null)
            volumeMinusButton.interactable = volumePercent > 0;
        if (volumePlusButton != null)
            volumePlusButton.interactable = volumePercent < 100;
    }
}

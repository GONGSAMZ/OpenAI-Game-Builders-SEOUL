using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 게임 화면에 표시되는 PC 단축키 안내의 표시 여부를 저장합니다.
/// 실제 키보드 조작은 이 설정과 관계없이 계속 사용할 수 있습니다.
/// </summary>
public static class KeyboardHintSettings
{
    public static bool IsEnabled => SaveService.Data.settings.keyboardHintsEnabled;

    public static void SetEnabled(bool enabled)
    {
        SaveService.Instance.SetKeyboardHintsEnabled(enabled);
    }
}

public abstract class UI_SettingsPopupBase : UI_Base
{
    protected TextMeshProUGUI titleText;
    protected TextMeshProUGUI bodyText;
    protected Button closeButton;

    protected override void Init()
    {
        titleText = Util.Find<TextMeshProUGUI>(gameObject, "TitleText", true);
        bodyText = Util.Find<TextMeshProUGUI>(gameObject, "BodyText", true);
        closeButton = Util.Find<Button>(gameObject, "CloseButton", true);
        AddEvent(closeButton.gameObject, () => Managers.UI.CloseUI(false));
        Render();
    }
    protected abstract void Render();
}

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
        {
            keyboardHintButton.onClick.AddListener(ToggleKeyboardHints);
        }
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
        KeyboardHintSettings.SetEnabled(KeyboardHintSettings.IsEnabled == false);
        RefreshDescription();
    }

    private void AdjustVolume(float amount)
    {
        if (volumeSlider == null)
            return;

        volumeSlider.value = Mathf.Clamp01(volumeSlider.value + amount);
    }

    private void RefreshDescription()
    {
        int volumePercent = Mathf.RoundToInt(AudioListener.volume * 100f);
        bool keyboardHintsEnabled = KeyboardHintSettings.IsEnabled;

        if (bodyText != null)
            bodyText.text = $"전체 음량  {volumePercent}%\n\n키보드 단축키 안내를 표시할지 선택하세요.";

        if (keyboardHintButtonLabel != null)
            keyboardHintButtonLabel.text = keyboardHintsEnabled
                ? "키보드 조작 안내  켜짐"
                : "키보드 조작 안내  꺼짐";

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

public sealed class UI_SettingsHelp : UI_SettingsPopupBase
{
    protected override void Render() { titleText.text = "조작 방법"; bodyText.text = "1. 손님을 눌러 주문을 받습니다.\n2. 반죽과 속재료를 넣고 덮개를 닫습니다.\n3. 구운 붕어빵을 진열대에서 손님에게 드래그합니다.\n\n정현 머리 위 말풍선을 누르면 대화를 시작할 수 있습니다."; }
}

public sealed class UI_SettingsStats : UI_SettingsPopupBase
{
    protected override void Render() { titleText.text = "오늘의 기록"; bodyText.text = $"판매한 붕어빵  {Managers.Game.totalFishBunsSold}개\n방문한 손님  {Managers.Game.totalCustomers}명\n현재 보유금  {Managers.Game.Money:N0}원"; }
}

public sealed class UI_SettingsStory : UI_SettingsPopupBase
{
    protected override void Render()
    {
        string specialOrder = CustomerStoryProgress.IsStoryCompleted
            ? "완료"
            : CustomerStoryProgress.SpecialOrderDueDay > 0
                ? $"{CustomerStoryProgress.SpecialOrderDueDay}일 차 마감 뒤"
                : CustomerStoryProgress.RetryAvailableDay > 0
                    ? $"{CustomerStoryProgress.RetryAvailableDay}일 차부터 재도전 가능"
                    : "아직 예약되지 않음";
        string completion = CustomerStoryProgress.IsStoryCompleted ? "완료" : "진행 중";
        titleText.text = "손님 이야기";
        bodyText.text = $"정현과 나눈 이야기  {CustomerStoryProgress.CompletedTopics.Count}/3\n특별 주문  {specialOrder}\n스토리  {completion}";
    }
}

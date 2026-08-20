using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 화면에 표시되는 PC 단축키 안내의 표시 여부를 저장합니다.
/// 실제 키보드 조작은 이 설정과 관계없이 계속 사용할 수 있습니다.
/// </summary>
public static class KeyboardHintSettings
{
    private const string KeyboardHintsEnabledKey = "settings_keyboard_hints_enabled_v1";

    public static bool IsEnabled => PlayerPrefs.GetInt(KeyboardHintsEnabledKey, 1) == 1;

    public static void SetEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(KeyboardHintsEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
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
    private const string VolumeKey = "settings_master_volume_v1";

    private Slider volumeSlider;
    private Button keyboardHintButton;
    private TextMeshProUGUI keyboardHintButtonLabel;

    protected override void Init()
    {
        base.Init();

        volumeSlider = Util.Find<Slider>(gameObject, "VolumeSlider", true);
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(VolumeKey, AudioListener.volume));
            volumeSlider.onValueChanged.AddListener(ApplyVolume);
            ApplyVolume(volumeSlider.value);
        }

        keyboardHintButton = Util.Find<Button>(gameObject, "KeyboardHintButton", true);
        keyboardHintButtonLabel = Util.Find<TextMeshProUGUI>(gameObject, "KeyboardHintButtonLabel", true);
        if (keyboardHintButton != null)
        {
            keyboardHintButton.onClick.AddListener(ToggleKeyboardHints);
        }

        RefreshDescription();
    }

    protected override void Render()
    {
        titleText.text = "옵션";
        RefreshDescription();
    }

    private void ApplyVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
        PlayerPrefs.Save();
        RefreshDescription();
    }

    private void ToggleKeyboardHints()
    {
        KeyboardHintSettings.SetEnabled(KeyboardHintSettings.IsEnabled == false);
        RefreshDescription();
    }

    private void RefreshDescription()
    {
        if (bodyText == null)
            return;

        bodyText.text = $"전체 음량  {Mathf.RoundToInt(AudioListener.volume * 100f)}%\n\n키보드 단축키 안내를 표시할지 선택하세요.";

        if (keyboardHintButtonLabel != null)
            keyboardHintButtonLabel.text = KeyboardHintSettings.IsEnabled
                ? "키보드 조작 안내  켜짐"
                : "키보드 조작 안내  꺼짐";
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

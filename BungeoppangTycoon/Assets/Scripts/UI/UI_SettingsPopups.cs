using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    const string VolumeKey = "settings_master_volume_v1";
    Slider volumeSlider;
    protected override void Init()
    {
        base.Init();
        volumeSlider = Util.Find<Slider>(gameObject, "VolumeSlider", true);
        volumeSlider.value = PlayerPrefs.GetFloat(VolumeKey, AudioListener.volume);
        volumeSlider.onValueChanged.AddListener(ApplyVolume);
        ApplyVolume(volumeSlider.value);
    }
    protected override void Render() { titleText.text = "옵션"; bodyText.text = "전체 음량을 조절합니다."; }
    void ApplyVolume(float value) { AudioListener.volume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume); PlayerPrefs.Save(); bodyText.text = $"전체 음량  {Mathf.RoundToInt(AudioListener.volume * 100f)}%"; }
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

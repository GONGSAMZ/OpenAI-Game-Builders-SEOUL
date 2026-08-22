public sealed class UI_SettingsStats : UI_SettingsPopupBase
{
    protected override void Render()
    {
        titleText.text = "오늘의 기록";
        bodyText.text = $"판매한 붕어빵  {Managers.Game.totalFishBunsSold}개\n방문한 손님  {Managers.Game.totalCustomers}명\n현재 보유금  {Managers.Game.Money:N0}원";
    }
}

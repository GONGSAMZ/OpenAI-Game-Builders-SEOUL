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

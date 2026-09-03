public sealed class UI_SettingsStory : UI_SettingsPopupBase
{
    protected override void Render()
    {
        string specialOrder = CustomerStoryProgress.IsStoryCompleted
            ? "완료"
            : CustomerStoryProgress.SpecialOrderDueDay > 0
                ? CustomerStoryProgress.SpecialOrderState == CustomerStorySchedule.Retry
                    ? $"{CustomerStoryProgress.SpecialOrderDueDay}일 차 마감 뒤 재도전"
                    : $"{CustomerStoryProgress.SpecialOrderDueDay}일 차 마감 뒤 특별 주문"
                : "아직 예약되지 않음";
        string completion = CustomerStoryProgress.IsStoryCompleted ? "완료" : "진행 중";
        titleText.text = "손님 이야기";
        bodyText.text = $"정현과 나눈 이야기  {CustomerStoryProgress.CompletedTopics.Count}/3\n특별 주문  {specialOrder}\n스토리  {completion}";
    }
}

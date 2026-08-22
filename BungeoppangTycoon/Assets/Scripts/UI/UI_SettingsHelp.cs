public sealed class UI_SettingsHelp : UI_SettingsPopupBase
{
    protected override void Render()
    {
        titleText.text = "조작 방법";
        bodyText.text = "1. 손님을 눌러 주문을 받습니다.\n2. 반죽과 속재료를 넣고 덮개를 닫습니다.\n3. 구운 붕어빵을 진열대에서 손님에게 드래그합니다.\n\n정현 머리 위 말풍선을 누르면 대화를 시작할 수 있습니다.";
    }
}

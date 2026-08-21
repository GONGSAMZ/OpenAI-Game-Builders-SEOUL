/// <summary>
/// 게임 화면에 표시되는 PC 단축키 안내의 표시 여부를 저장합니다.
/// 실제 키보드 조작은 이 설정과 관계없이 계속 사용할 수 있습니다.
/// Unity 프리팹이 UI_SettingsPopups.cs를 컴포넌트 스크립트로 안정적으로
/// 해석할 수 있도록 비컴포넌트 설정 도우미는 독립 파일에 둡니다.
/// </summary>
public static class KeyboardHintSettings
{
    public static bool IsEnabled => SaveService.Data.settings.keyboardHintsEnabled;

    public static void SetEnabled(bool enabled)
    {
        SaveService.Instance.SetKeyboardHintsEnabled(enabled);
    }
}

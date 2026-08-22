using UnityEngine.InputSystem;

/// <summary>게임 코드가 Unity의 새 Input System만 사용하도록 입력을 한곳에서 읽습니다.</summary>
public static class GameInput
{
    public static bool LeftClickPressed => Mouse.current?.leftButton.wasPressedThisFrame == true;
    public static bool RightClickPressed => Mouse.current?.rightButton.wasPressedThisFrame == true;
    public static bool AnyKeyboardKeyPressed => Keyboard.current?.anyKey.wasPressedThisFrame == true;

    public static bool KeyPressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
}

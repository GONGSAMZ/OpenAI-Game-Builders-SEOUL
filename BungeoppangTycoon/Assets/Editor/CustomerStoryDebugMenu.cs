#if UNITY_EDITOR
using UnityEditor;

public static class CustomerStoryDebugMenu
{
    [MenuItem("Bungeoppang/손님 이야기/정현 진행도 초기화")]
    private static void ResetJeongHyunStory()
    {
        CustomerStoryProgress.ResetForDebug();
    }
}
#endif

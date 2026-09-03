using UnityEditor;

/// <summary>
/// 자동 새로고침이 꺼진 상태에서 추가한 피그마 상점 자산을
/// Unity가 다음에 다시 읽을 때 한 번만 프리팹으로 조립합니다.
/// 이후에는 Tools/GONGSAMZ/Rebuild Store UI from Figma 메뉴로 수동 재생성합니다.
/// </summary>
[InitializeOnLoad]
internal static class StorePrefabAutoBuilder
{
    private const string CompletionKey = "GONGSAMZ.StorePrefab.FigmaV6.Generated";

    static StorePrefabAutoBuilder()
    {
        EditorApplication.delayCall += BuildOnceAfterCompile;
    }

    private static void BuildOnceAfterCompile()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += BuildOnceAfterCompile;
            return;
        }

        if (EditorPrefs.GetBool(CompletionKey, false))
            return;

        StorePrefabBuilder.BuildAll();
        EditorPrefs.SetBool(CompletionKey, true);
    }
}

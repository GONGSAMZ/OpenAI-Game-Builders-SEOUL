using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SettingsPanelValidator
{
    const string PrefabPath = "Assets/Resources/Prefabs/UI/UI_Settings.prefab";

    [MenuItem("Tools/GONGSAMZ/Validate Settings Panel")]
    public static void ValidateFromMenu()
    {
        Validate();
    }

    public static void ValidateFromCommandLine()
    {
        Validate();
    }

    static void Validate()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"설정 프리팹을 찾지 못했습니다: {PrefabPath}");

        if (prefab.GetComponent<UI_Settings>() == null)
            throw new InvalidOperationException("UI_Settings 컴포넌트가 프리팹 루트에 없습니다.");

        Button exitButton = FindButton(prefab, "ExitBtn");
        Button quitButton = FindButton(prefab, "QuitButton");

        if (exitButton == null)
            throw new InvalidOperationException("ExitBtn 버튼이 설정 프리팹에 없습니다.");

        if (quitButton == null)
            throw new InvalidOperationException("QuitButton 버튼이 설정 프리팹에 없습니다.");

        if (quitButton.GetComponentInChildren<TextMeshProUGUI>(true) == null)
            throw new InvalidOperationException("QuitButton 아래에 TextMeshProUGUI가 없습니다.");

        Debug.Log("[SettingsPanelValidator] PASS: ExitBtn과 QuitButton 연결 구조가 올바릅니다.");
    }

    static Button FindButton(GameObject root, string buttonName)
    {
        return root.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => button.name == buttonName);
    }
}

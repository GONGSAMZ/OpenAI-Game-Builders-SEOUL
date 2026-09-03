#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>빌드에 포함된 씬의 구형 UI 입력 모듈을 새 Input System 모듈로 교체합니다.</summary>
public static class InputSystemMigration
{
    private const string DefaultActionsPath = "Packages/com.unity.inputsystem/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions";

    public static void MigrateBuildScenes()
    {
        string originalScene = SceneManager.GetActiveScene().path;
        int replacementCount = 0;

        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
                continue;

            Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            foreach (StandaloneInputModule legacyModule in UnityEngine.Object.FindObjectsByType<StandaloneInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                GameObject eventSystemObject = legacyModule.gameObject;
                UnityEngine.Object.DestroyImmediate(legacyModule);
                InputSystemUIInputModule inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>()
                    ?? eventSystemObject.AddComponent<InputSystemUIInputModule>();
                replacementCount++;
                EditorUtility.SetDirty(eventSystemObject);
            }

            foreach (InputSystemUIInputModule inputModule in UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                AssignPersistentDefaultActions(inputModule, buildScene.path);
                EditorUtility.SetDirty(inputModule);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrWhiteSpace(originalScene))
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

        if (replacementCount == 0)
            Debug.Log("[Input System] 변환할 StandaloneInputModule이 없습니다. 모든 빌드 씬이 이미 새 입력 모듈을 사용합니다.");
        else
            Debug.Log($"[Input System] UI 입력 모듈 {replacementCount}개를 변환했습니다.");

        AssetDatabase.SaveAssets();
    }

    private static void AssignPersistentDefaultActions(InputSystemUIInputModule module, string scenePath)
    {
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultActionsPath);
        InputActionReference[] references = AssetDatabase.LoadAllAssetsAtPath(DefaultActionsPath)
            .OfType<InputActionReference>()
            .ToArray();
        if (actions == null || references.Length == 0)
            throw new InvalidOperationException($"Input System 기본 UI Actions를 찾지 못했습니다: {scenePath}");

        InputActionReference Find(string actionName) => references.FirstOrDefault(reference => reference.action?.name == actionName)
            ?? throw new InvalidOperationException($"기본 UI Action '{actionName}'을 찾지 못했습니다: {scenePath}");

        module.actionsAsset = actions;
        module.point = Find("Point");
        module.leftClick = Find("Click");
        module.rightClick = Find("RightClick");
        module.middleClick = Find("MiddleClick");
        module.scrollWheel = Find("ScrollWheel");
        module.move = Find("Navigate");
        module.submit = Find("Submit");
        module.cancel = Find("Cancel");
        module.trackedDevicePosition = Find("TrackedDevicePosition");
        module.trackedDeviceOrientation = Find("TrackedDeviceOrientation");
    }
}
#endif

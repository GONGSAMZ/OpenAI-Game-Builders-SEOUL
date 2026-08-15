#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class WebGLBuildCommand
{
    public static void Configure()
    {
        EnsureUrpCompatibilityMode();

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new InvalidOperationException("WebGL 플랫폼 전환에 실패했습니다.");

        Console.WriteLine("WEBGL_CONFIGURATION_SUCCEEDED");
    }

    public static void Build()
    {
        EnsureUrpCompatibilityMode();

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException("WebGL 빌드에 포함된 장면이 없습니다.");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine("Builds", "WebGL"),
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"WebGL 빌드에 실패했습니다: {report.summary.result}");

        Console.WriteLine($"WEBGL_BUILD_SUCCEEDED size={report.summary.totalSize} path={options.locationPathName}");
    }

    private static void EnsureUrpCompatibilityMode()
    {
        NamedBuildTarget webTarget = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.WebGL);
        string defines = PlayerSettings.GetScriptingDefineSymbols(webTarget);
        string[] updatedDefines = defines
            .Split(';')
            .Where(define => !string.IsNullOrWhiteSpace(define))
            .Append("URP_COMPATIBILITY_MODE")
            .Distinct()
            .ToArray();

        PlayerSettings.SetScriptingDefineSymbols(
            webTarget,
            string.Join(";", updatedDefines));
        AssetDatabase.SaveAssets();
    }
}
#endif

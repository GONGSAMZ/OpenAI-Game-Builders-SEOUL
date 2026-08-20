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
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new InvalidOperationException("WebGL 플랫폼 전환에 실패했습니다.");

        EnsureWebGlBuildEnvironment();

        Console.WriteLine("WEBGL_CONFIGURATION_SUCCEEDED");
    }

    public static void Build()
    {
        EnsureWebGlBuildEnvironment();

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

        InjectPlatformBridge(options.locationPathName);

        Console.WriteLine($"WEBGL_BUILD_SUCCEEDED size={report.summary.totalSize} path={options.locationPathName}");
    }

    private static void InjectPlatformBridge(string buildDirectory)
    {
        string indexPath = Path.Combine(buildDirectory, "index.html");
        const string bridgeTag = "<script src=\"/game-bridge.js\"></script>";

        if (!File.Exists(indexPath))
            throw new FileNotFoundException("WebGL index.html을 찾을 수 없습니다.", indexPath);

        string html = File.ReadAllText(indexPath);
        if (!html.Contains(bridgeTag) && !html.Contains("</head>"))
            throw new InvalidOperationException("WebGL index.html의 </head> 태그를 찾을 수 없습니다.");

        if (!html.Contains(bridgeTag))
            html = html.Replace("</head>", $"  {bridgeTag}{Environment.NewLine}  </head>");

        const string unityReadyMarker = "window.dispatchEvent(new Event(\"UNITY_INSTANCE_READY\"));";
        if (!html.Contains(unityReadyMarker))
        {
            const string unityCallback = ".then((unityInstance) => {";
            if (!html.Contains(unityCallback))
                throw new InvalidOperationException("Unity 인스턴스 생성 콜백을 찾을 수 없습니다.");

            html = html.Replace(
                unityCallback,
                $"{unityCallback}{Environment.NewLine}                window.unityInstance = unityInstance;{Environment.NewLine}                {unityReadyMarker}");
        }

        File.WriteAllText(indexPath, html);
    }

    private static void EnsureWebGlBuildEnvironment()
    {
        // 플랫폼을 이 메서드 안에서 전환하면 Unity가 WebGL 전용 스크립트 심볼을
        // 재컴파일하기 전에 BuildPipeline이 실행될 수 있습니다. CI는 targetPlatform=WebGL,
        // 로컬 배치는 -buildTarget WebGL로 에디터를 시작해 컴파일 경계를 보장합니다.
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new InvalidOperationException(
                "현재 활성 빌드 대상이 WebGL이 아닙니다. " +
                "Unity에서 WebGL로 전환하거나 배치 실행에 -buildTarget WebGL을 지정한 뒤 다시 빌드하세요.");

        NamedBuildTarget webTarget = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.WebGL);
        string defines = PlayerSettings.GetScriptingDefineSymbols(webTarget);
        bool hasCompatibilityMode = defines
            .Split(';')
            .Where(define => !string.IsNullOrWhiteSpace(define))
            .Any(define => define == "URP_COMPATIBILITY_MODE");
        if (!hasCompatibilityMode)
            throw new InvalidOperationException(
                "WebGL Scripting Define Symbols에 URP_COMPATIBILITY_MODE가 필요합니다. " +
                "ProjectSettings.asset에 영구 설정한 뒤 다시 빌드하세요.");
    }
}
#endif

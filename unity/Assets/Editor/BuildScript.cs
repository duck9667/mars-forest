using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 배치 빌드: Unity -batchmode -projectPath unity -executeMethod BuildScript.BuildMac (또는 BuildIOS) -quit
public static class BuildScript
{
    static readonly string[] SCENES = { "Assets/Scenes/Main.unity" };

    [MenuItem("Mars/Build macOS")]
    public static void BuildMac()
    {
        ProjectSetup.Ensure();
        TrySetMacArm64();
        var r = BuildPipeline.BuildPlayer(SCENES, "Builds/Mac/MarsBottle.app", BuildTarget.StandaloneOSX, BuildOptions.None);
        Report(r);
    }

    [MenuItem("Mars/Build iOS (Xcode project)")]
    public static void BuildIOS()
    {
        ProjectSetup.Ensure();
        var r = BuildPipeline.BuildPlayer(SCENES, "Builds/iOS", BuildTarget.iOS, BuildOptions.None);
        Report(r);
    }

    static void Report(BuildReport r)
    {
        Debug.Log("Mars build: " + r.summary.result + " " + r.summary.totalSize / 1024 / 1024 + "MB, errors=" + r.summary.totalErrors);
        if (Application.isBatchMode && r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // macOS 모듈의 UserBuildSettings.architecture = arm64 (모듈 어셈블리가 없으면 조용히 건너뜀)
    static void TrySetMacArm64()
    {
        try
        {
            var t = Type.GetType("UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandalone.Extensions");
            var prop = t?.GetProperty("architecture");
            var archT = Type.GetType("UnityEditor.Build.OSArchitecture, UnityEditor.CoreModule") ?? Type.GetType("UnityEditor.Build.OSArchitecture, UnityEditor");
            if (prop != null && archT != null) prop.SetValue(null, Enum.Parse(archT, "arm64"));
        }
        catch (Exception e) { Debug.LogWarning("arm64 설정 건너뜀: " + e.Message); }
    }
}

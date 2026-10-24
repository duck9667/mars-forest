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
        // 수동 서명(Xcode 계정 없이 이미 받아 둔 프로파일로): IOS_PROFILE_UUID·IOS_BUNDLE_ID 환경 변수가 있으면 앱 타깃에만 적용.
        // 빌드 뒤 원래 설정으로 되돌린다(Unity가 기본값을 다른 표현으로 다시 써서 ProjectSettings.asset에 diff가 남으면 git checkout으로 되돌릴 것).
        string uuid = Environment.GetEnvironmentVariable("IOS_PROFILE_UUID"), bundle = Environment.GetEnvironmentVariable("IOS_BUNDLE_ID");
        bool manual = !string.IsNullOrEmpty(uuid);
        bool prevAuto = PlayerSettings.iOS.appleEnableAutomaticSigning; string prevId = PlayerSettings.iOS.iOSManualProvisioningProfileID;
        var prevType = PlayerSettings.iOS.iOSManualProvisioningProfileType;
        string prevTeam = PlayerSettings.iOS.appleDeveloperTeamID; string prevBundle = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS);
        try
        {
            if (manual)
            {
                PlayerSettings.iOS.appleEnableAutomaticSigning = false;
                PlayerSettings.iOS.iOSManualProvisioningProfileID = uuid;
                PlayerSettings.iOS.iOSManualProvisioningProfileType = ProvisioningProfileType.Development;
                PlayerSettings.iOS.appleDeveloperTeamID = Environment.GetEnvironmentVariable("IOS_TEAM") ?? "SXH247VWQA";
                if (!string.IsNullOrEmpty(bundle)) PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, bundle);
            }
            var r = BuildPipeline.BuildPlayer(SCENES, "Builds/iOS", BuildTarget.iOS, BuildOptions.None);
            Report(r);
        }
        finally
        {
            if (manual)
            {
                PlayerSettings.iOS.appleEnableAutomaticSigning = prevAuto; PlayerSettings.iOS.iOSManualProvisioningProfileID = prevId;
                PlayerSettings.iOS.iOSManualProvisioningProfileType = prevType;
                PlayerSettings.iOS.appleDeveloperTeamID = prevTeam; PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, prevBundle);
                AssetDatabase.SaveAssets();
            }
        }
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

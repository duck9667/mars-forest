using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터가 열리면 한 번: 빌드에 셰이더를 포함시킬 재질 에셋, 빈 Main 씬, 빌드 세팅, 세로 화면·해상도 등 플레이어 설정.
[InitializeOnLoad]
public static class ProjectSetup
{
    const string SCENE = "Assets/Scenes/Main.unity";
    static ProjectSetup() { EditorApplication.delayCall += () => { if (!Application.isBatchMode) Ensure(); }; }

    [MenuItem("Mars/Setup Project")]
    public static void Ensure()
    {
        Directory.CreateDirectory("Assets/Resources/Mats");
        if (!File.Exists("Assets/Resources/Mats/Base.mat"))
        {
            var m = new Material(Shader.Find("Standard")); m.SetFloat("_Glossiness", 0.05f); m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, "Assets/Resources/Mats/Base.mat");
        }
        if (!File.Exists("Assets/Resources/Mats/Emissive.mat"))
        {
            // 가로등·창문 등 발광용: _EMISSION 키워드를 켠 재질 에셋이 있어야 셰이더 변형이 빌드에 포함된다
            var m = new Material(Shader.Find("Standard")); m.SetFloat("_Glossiness", 0.05f); m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", Color.black);
            AssetDatabase.CreateAsset(m, "Assets/Resources/Mats/Emissive.mat");
        }
        if (!File.Exists("Assets/Resources/Mats/Trans.mat"))
        {
            var sh = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Transparent/Diffuse");
            var m = new Material(sh);
            AssetDatabase.CreateAsset(m, "Assets/Resources/Mats/Trans.mat");
        }
        if (!File.Exists(SCENE))
        {
            Directory.CreateDirectory("Assets/Scenes");
            // 배치 모드의 기본 씬은 저장 안 된 Untitled라 Additive가 불가 → Single로 교체 생성
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(s, SCENE);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SCENE, true) };

        PlayerSettings.companyName = "duck9667";
        PlayerSettings.productName = "화성 물병 가게";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.duck9667.marsbottle");
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.duck9667.marsbottle");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false; PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 1170;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        AssetDatabase.SaveAssets();
        Debug.Log("Mars: 프로젝트 셋업 완료");
    }
}

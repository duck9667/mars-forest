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
        if (!File.Exists("Assets/Resources/Mats/Trans.mat"))
        {
            var sh = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Transparent/Diffuse");
            var m = new Material(sh);
            AssetDatabase.CreateAsset(m, "Assets/Resources/Mats/Trans.mat");
        }
        if (!File.Exists("Assets/Resources/Mats/Emissive.mat"))
        {
            // Standard 의 _EMISSION 변형을 빌드에 포함시키기 위한 재질. 런타임에 EnableKeyword("_EMISSION") 만 하면(가로등 전구·제빙 코어)
            // 에셋 재질 중 그 키워드를 쓰는 게 없어 변형이 잘려 나가고 발광이 안 보인다.
            var m = new Material(Shader.Find("Standard")); m.SetFloat("_Glossiness", 0.05f); m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(m, "Assets/Resources/Mats/Emissive.mat");
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
        EnsureHdr();
        AssetDatabase.SaveAssets();
        Debug.Log("Mars: 프로젝트 셋업 완료");
    }

    // PostFX(ACES 톤매핑·블룸)는 1 이상의 값이 살아 있어야 하므로 모든 티어에서 카메라 HDR 을 켠다 (기본 티어 설정은 모바일 Tier1 이 꺼져 있음)
    static void EnsureHdr()
    {
        try
        {
            foreach (var grp in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.iOS })
                foreach (UnityEngine.Rendering.GraphicsTier tier in System.Enum.GetValues(typeof(UnityEngine.Rendering.GraphicsTier)))
                {
                    var ts = UnityEditor.Rendering.EditorGraphicsSettings.GetTierSettings(grp, tier);
                    if (ts.hdr && ts.hdrMode == UnityEngine.Rendering.CameraHDRMode.FP16) continue;
                    ts.hdr = true; ts.hdrMode = UnityEngine.Rendering.CameraHDRMode.FP16;
                    UnityEditor.Rendering.EditorGraphicsSettings.SetTierSettings(grp, tier, ts);
                }
        }
        catch (System.Exception e) { Debug.LogWarning("HDR 티어 설정 건너뜀: " + e.Message); }
    }
}

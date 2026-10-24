#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// Unity as a Library: 게임 데이터(Data 폴더)를 UnityFramework 타깃 리소스에도 넣는다.
// 꿈마을 같은 호스트 앱은 UnityFramework.framework만 임베드하므로, Data가 프레임워크 안에 있어야
// 호스트가 setDataBundleId("com.unity3d.framework")로 찾을 수 있다. (Unity 기본값은 Unity-iPhone 앱 타깃에만 넣음)
// UNITY_IOS는 에디터를 -buildTarget iOS로 띄울 때 정의된다 → tools/build-unity-framework.sh 참고
public static class IOSUaaLPostProcess
{
    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string projPath = PBXProject.GetPBXProjectPath(path);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);
        string fw = proj.GetUnityFrameworkTargetGuid();
        string data = proj.FindFileGuidByProjectPath("Data");
        if (data == null) { UnityEngine.Debug.LogError("UaaL: Data 폴더 참조를 찾지 못함"); return; }
        proj.AddFileToBuild(fw, data);
        proj.WriteToFile(projPath);
        UnityEngine.Debug.Log("UaaL: Data → UnityFramework 리소스 추가");
    }
}
#endif

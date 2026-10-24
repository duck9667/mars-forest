#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// 짧은 햅틱. iOS는 네이티브 플러그인(Plugins/iOS/MarsHaptics.mm, UIImpactFeedbackGenerator)으로 톡 치는 느낌만 주고,
// 그 외 플랫폼(에디터·맥·안드로이드)은 아무것도 안 한다. 설정 켜짐 여부는 호출부(JuiceFX.Vibrate)가 본다
public static class Haptics
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void _MarsHaptic(int style);
#endif

    /// ms(예전 진동 길이)로 세기 선택: 15 미만 light, 35 미만 medium, 그 외 heavy
    public static void Play(int ms)
    {
        if (ms <= 0) return;
        int style = ms < 15 ? 0 : ms < 35 ? 1 : 2;
#if UNITY_IOS && !UNITY_EDITOR
        _MarsHaptic(style);
#else
        _ = style;
#endif
    }
}

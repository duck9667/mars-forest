using UnityEngine;

// §7.2 렌더 품질 레이어(카메라 포스트 프로세스, 내장 RP OnRenderImage). CameraRig 가 카메라에 붙인다.
//  - 외곽선: 카메라 깊이 텍스처의 2차 미분 엣지 검출 → 남색 0x1b1a2e. 역면 hull 대신 쓰는 이유는 셰이더 머리 주석 참고.
//  - 톤매핑: ACES Filmic(three.js 와 같은 식) + 채도·대비. 노출은 원본 1.1 이 아니라 0.6 이 기본인데, 원본(three r160)은 물리 조명이라
//    DirectionalLight 1.9 가 실제론 1.9/π≈0.605 의 복사휘도이고 그 위에 ACES 가 exposure/0.6=×1.83 을 곱한다. 지금 World/DayNight 의
//    태양 1.15·반구광 0.55 는 Unity(π 나눗셈 없음) 기준으로 이미 "0.605×1.83≈1.11·0.334×1.83≈0.61" 에 해당하므로 추가 배율 없이(0.6/0.6=1)
//    ACES 커브만 태우는 것이 원본과 같은 밝기다. World/DayNight 를 1.9·1.05 로 되돌리면 노출을 0.6×1.15/1.9≈0.36 으로 낮춰야 한다.
//  - 비네트: 원본 DOM #vig 그라디언트를 셰이더에서 같은 수치로.
//  - 블룸: HDR 문턱 이상(밤의 가로등 발광 등)만 가볍게.
//  - 그림자 품질: 직교 카메라에 맞춰 캐스케이드 없음·CloseFit·거리 130·해상도 VeryHigh (World.Lights 가 Start 에서 넣는 값을 그 뒤 덮어쓴다).
// 검증용 인자 -fx k=v,k=v : off | exp | sat | con | out(px) | dt(깊이 문턱, 논리 단위) | ns(노멀 선 세기) | nt | bloom | bt | vig | shd | sres(0~3) | bias | nbias
[RequireComponent(typeof(Camera))]
public class PostFX : MonoBehaviour
{
    public static PostFX I;

    [Header("외곽선")]
    public Color outlineColor = GameConfig.OUTLINE;
    public float outlineStrength = 1f;
    public float outlinePx = 2f;          // 화면 높이 1170px 기준 선 두께(px). 해상도에 비례
    public float depthThreshL = 0.6f;     // 이 깊이 단차(논리 단위) 이상이면 선. 바닥 기울기는 2차 미분으로 상쇄되므로 작아도 바닥엔 선이 없다
    public float normalStrength = 0f;     // 노멀 기반 주름 선(상자 윗모서리 등). 원본엔 없어 기본 0
    public float normalThresh = 0.7f;     // 1-dot 문턱(0.7 ≈ 72°)

    [Header("톤매핑")]
    public float exposure = 0.85f;        // three.js toneMappingExposure 와 같은 의미(위 주석). 계산상 0.6 이 원본 등가지만 스크린샷 대조로 0.85 가 원본 밝기에 가까웠다
    public float saturation = 1.1f;
    public float contrast = 1.02f;

    [Header("비네트 (원본 #vig)")]
    public float vignette = 0.38f;
    public Color vignetteColor = new Color(40 / 255f, 8 / 255f, 0f);
    public Vector2 vignetteCenter = new Vector2(0.5f, 0.55f);   // CSS 'at 50% 45%' (위에서 45%) → uv 아래 기준 0.55
    public Vector2 vignetteRadius = new Vector2(0.8f, 0.7f);
    public float vignetteStart = 0.55f;

    [Header("블룸")]
    public float bloomIntensity = 0.5f;
    public float bloomThreshold = 1.3f;   // 낮의 햇빛 받는 크림색 바닥(≈1.26)은 걸리지 않고 발광(≥1.6)만
    public float bloomKnee = 0.4f;

    [Header("그림자")]
    public float shadowDistance = 130f;
    public ShadowResolution shadowRes = Application.isMobilePlatform ? ShadowResolution.High : ShadowResolution.VeryHigh;   // 모바일은 메모리 절약
    public float sunBias = -1f, sunNormalBias = -1f;   // 음수면 World 값 유지

    public bool off;
    Material mat; Camera cam; bool shadowTuned;

    void Awake()
    {
        I = this;
        cam = GetComponent<Camera>();
        var sh = Resources.Load<Shader>("Shaders/MarsPostFX");
        if (sh == null) sh = Shader.Find("Hidden/Mars/PostFX");
        if (sh == null || !sh.isSupported) { Debug.LogWarning("PostFX: 셰이더 없음, 포스트 프로세스 끔"); enabled = false; return; }
        mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        ParseArgs();
        cam.allowHDR = true;
        cam.depthTextureMode |= DepthTextureMode.Depth;
        if (normalStrength > 0) cam.depthTextureMode |= DepthTextureMode.DepthNormals;
    }

    void ParseArgs()
    {
        var s = DevArgs.Get("-fx");
        if (string.IsNullOrEmpty(s)) return;
        foreach (var kv in s.Split(','))
        {
            var p = kv.Split('='); float v = 0;
            if (p.Length == 2) float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);
            switch (p[0])
            {
                case "off": off = true; break;
                case "exp": exposure = v; break;
                case "sat": saturation = v; break;
                case "con": contrast = v; break;
                case "out": outlinePx = v; break;
                case "os": outlineStrength = v; break;
                case "dt": depthThreshL = v; break;
                case "ns": normalStrength = v; break;
                case "nt": normalThresh = v; break;
                case "bloom": bloomIntensity = v; break;
                case "bt": bloomThreshold = v; break;
                case "vig": vignette = v; break;
                case "shd": shadowDistance = v; break;
                case "sres": shadowRes = (ShadowResolution)Mathf.Clamp((int)v, 0, 3); break;
                case "bias": sunBias = v; break;
                case "nbias": sunNormalBias = v; break;
            }
        }
        Debug.Log("PostFX: -fx " + s);
    }

    void LateUpdate()
    {
        // World.Lights()(World.Start) 뒤에 한 번: 직교 카메라용 그림자 설정
        if (!shadowTuned && Time.frameCount > 2) { shadowTuned = true; TuneShadows(); }
    }

    void TuneShadows()
    {
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowCascades = 1;                       // 직교 카메라는 캐스케이드가 의미 없고 단일 맵이 텍셀 밀도가 높다
        QualitySettings.shadowProjection = ShadowProjection.CloseFit;
        QualitySettings.shadowDistance = shadowDistance;          // 카메라 거리 90 ± 보이는 깊이 ~16 + 화면 밖 긴 그림자 여유
        QualitySettings.shadowResolution = shadowRes;
        QualitySettings.shadowNearPlaneOffset = 2f;
        if (World.sun != null)
        {
            if (sunBias >= 0) World.sun.shadowBias = sunBias;
            if (sunNormalBias >= 0) World.sun.shadowNormalBias = sunNormalBias;
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null || off) { Graphics.Blit(src, dst); return; }
        float scale = Screen.height / 1170f;
        float px = Mathf.Max(1f, Mathf.Round(outlinePx * scale));
        float depthT = depthThreshL * GameConfig.K / Mathf.Max(1f, cam.farClipPlane - cam.nearClipPlane);
        var oc = outlineColor.linear; oc.a = outlineStrength;
        mat.SetColor("_OutlineColor", oc);
        mat.SetVector("_EdgeParams", new Vector4(px, depthT, normalThresh, normalStrength));
        mat.SetVector("_ToneParams", new Vector4(exposure, saturation, contrast, bloomIntensity));
        mat.SetVector("_VigParams", new Vector4(vignetteCenter.x, vignetteCenter.y, vignetteStart, vignette));
        mat.SetColor("_VigColor", vignetteColor);
        mat.SetVector("_VigRadius", new Vector4(vignetteRadius.x, vignetteRadius.y, 0, 0));
        mat.SetVector("_BloomParams", new Vector4(bloomThreshold, bloomKnee, 0, 0));

        if (bloomIntensity > 0)
        {
            int w = Mathf.Max(1, src.width / 4), h = Mathf.Max(1, src.height / 4);
            var a = RenderTexture.GetTemporary(w, h, 0, src.format); var b = RenderTexture.GetTemporary(w, h, 0, src.format);
            a.filterMode = FilterMode.Bilinear; b.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, a, mat, 1);
            mat.SetVector("_BlurDir", new Vector4(1, 0, 0, 0)); Graphics.Blit(a, b, mat, 2);
            mat.SetVector("_BlurDir", new Vector4(0, 1, 0, 0)); Graphics.Blit(b, a, mat, 2);
            mat.SetVector("_BlurDir", new Vector4(2, 0, 0, 0)); Graphics.Blit(a, b, mat, 2);
            mat.SetVector("_BlurDir", new Vector4(0, 2, 0, 0)); Graphics.Blit(b, a, mat, 2);
            mat.SetTexture("_BloomTex", a);
            Graphics.Blit(src, dst, mat, 0);
            RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b);
        }
        else Graphics.Blit(src, dst, mat, 0);
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}

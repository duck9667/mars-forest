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
// 검증용 인자 -fx k=v,k=v : off | exp | sat | con | out(px) | dt(깊이 문턱, 논리 단위) | ns(노멀 선 세기) | nt | bloom | bt | vig | shd | sres(0~3) | bias | nbias | sb(정적 배칭 0/1) | msaa(1·2·4·8)
// 모바일 경량(r9, MobileLook = 폰 또는 맥 검증 인자 -mobiletest): 외곽선 마스크 RHalf(카메라 초점 깊이를 빼서 저장해 정밀도 유지), 블룸 1/8(블러 반경은 같게),
//  MSAA 4·그림자 High·130 은 유지(낮추면 차양 지글거림·화면 위쪽 그림자 깜빡임), 얼음 비침 GrabPass 대신 미리 정한 바닥색(Items.IceMat → MarsIce LOD 200). 화면 모양은 같게 두는 것이 원칙.
//  배경(메사·바위·북쪽 배관)은 조립 뒤 한 번 StaticBatchingUtility.Combine(모바일·맥 공통), 렌더러·재질 수를 "perf" 로그로 전후 비교.
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
    public float bloomThreshold = 2.4f;   // 햇빛 받은 얼음·흰 기계(≈1.3~2)가 흰 덩어리로 번쩍이던 문제(jake 10/26 "흰색 깜빡임") → 밤 발광만
    public float bloomKnee = 0.4f;

    [Header("그림자")]
    public float shadowDistance = 130f;
    public ShadowResolution shadowRes = Application.isMobilePlatform ? ShadowResolution.High : ShadowResolution.VeryHigh;   // 모바일은 메모리 절약
    public float sunBias = -1f, sunNormalBias = -1f;   // 음수면 World 값 유지

    [Header("외곽선 마스크")]
    public bool maskOutline = true;       // 배경(바닥·울타리·메사·소품)엔 선을 안 긋는다. -fx mask=0 으로 예전처럼 전부
    public const int ENV_LAYER = 8;       // 배경 레이어(이름 없는 사용자 레이어 8)
    static readonly string[] ENV_ROOTS = { "World", "Zones", "Deco", "Terraform", "Pads" };   // Bootstrap 루트 이름
    Camera maskCam; RenderTexture maskRT; Shader maskShader; float envT;

    public bool off;
    Material mat; Camera cam; bool shadowTuned;

    // 모바일 경량 경로(폰이거나 맥 검증 인자 -mobiletest). 화면은 같게, 비용만 줄인다
    static int mobileLook = -1;
    public static bool MobileLook { get { if (mobileLook < 0) mobileLook = Application.isMobilePlatform || DevArgs.Has("-mobiletest") ? 1 : 0; return mobileLook == 1; } }
    public static bool IceCheap => MobileLook && !DevArgs.Has("-icefull");   // 얼음 저가 경로(검증 인자 -icefull 이면 모바일에서도 GrabPass)
    bool halfMask;            // 마스크 RT 를 RHalf 로(초점 깊이 기준 상대값 저장)
    int bloomDiv = 4;         // 블룸 해상도 분모(모바일 8)
    int msaa = 4;
    public bool staticBatch = true;
    bool batched;
    static readonly string[] STATIC_ROOTS = { "World/rocks", "World/northPipe" };   // 조립 뒤 움직이지 않는 배경만(본부·안테나 회전·로버 등은 제외)

    void Awake()
    {
        I = this;
        cam = GetComponent<Camera>();
        var sh = Resources.Load<Shader>("Shaders/MarsPostFX");
        if (sh == null) sh = Shader.Find("Hidden/Mars/PostFX");
        if (sh == null || !sh.isSupported) { Debug.LogWarning("PostFX: 셰이더 없음, 포스트 프로세스 끔"); enabled = false; return; }
        mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        if (MobileLook)
        {
            // 그림자는 예전 모바일 그대로(High·거리 130). Medium·거리 112 를 시험했지만 거리 끝 페이드(마지막 ~10%)가 화면 위쪽(카메라 거리 ~100)에 걸려
            //  얼음 더미처럼 낮은 물체 그림자가 카메라가 움직일 때 생겼다 사라졌다(-mobiletest 비교) → 줄이지 않는다
            shadowRes = ShadowResolution.High;
            bloomDiv = 8;   // MSAA 는 4 유지: 2배는 0.05초 연속 촬영에서 차양 가장자리가 지글거림이 두 배(프레임 차이 0.09→0.17%). 애플 GPU 는 타일 안에서 해소해 4배도 싸다
            halfMask = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf);
        }
        ParseArgs();
        cam.allowHDR = true;
        cam.depthTextureMode |= DepthTextureMode.Depth;
        if (normalStrength > 0) cam.depthTextureMode |= DepthTextureMode.DepthNormals;
        maskShader = Resources.Load<Shader>("Shaders/MarsMaskDepth");
        if (maskShader == null || !maskShader.isSupported) maskOutline = false;
        if (maskOutline) BuildMaskCam();
        if (MobileLook) Debug.Log("PostFX: 모바일 경량 mask=" + (halfMask ? "RHalf" : "RFloat") + " bloom=1/" + bloomDiv + " msaa=" + msaa + " shadow=" + shadowRes + "/" + shadowDistance);
    }

    // 메인 카메라 자식(같은 위치·투영)으로 붙어 먼저 그린다(depth -1). 배경 레이어·UI 를 빼고 선형 깊이만 기록
    void BuildMaskCam()
    {
        var go = new GameObject("outlineMask"); go.transform.SetParent(transform, false);
        maskCam = go.AddComponent<Camera>();
        maskCam.enabled = true;
        maskCam.depth = cam.depth - 1;
        maskCam.clearFlags = CameraClearFlags.SolidColor; maskCam.backgroundColor = Color.white;
        maskCam.cullingMask = ~((1 << ENV_LAYER) | (1 << 5));
        maskCam.renderingPath = RenderingPath.VertexLit;   // 그림자 맵을 다시 만들지 않게
        maskCam.allowHDR = false; maskCam.allowMSAA = false;
        maskCam.SetReplacementShader(maskShader, "RenderType");
        SyncMask();
    }

    void SyncMask()
    {
        maskCam.orthographic = cam.orthographic; maskCam.orthographicSize = cam.orthographicSize; maskCam.fieldOfView = cam.fieldOfView;
        maskCam.nearClipPlane = cam.nearClipPlane; maskCam.farClipPlane = cam.farClipPlane;
        // RHalf 는 0.4 근처 간격이 2.4e-4 로 외곽선 문턱(≈1.3e-4)보다 거칠어 비탈면에 잔선이 생긴다 → 화면 가운데 바닥 깊이를 빼서 0 근처(간격 ≤3e-5)로 저장.
        //  합성 패스가 같은 값을 더해 되돌린다(MarsMaskDepth·MarsPostFX 의 _MarsMaskRef). RFloat 경로는 0 이라 예전과 같다
        float mref = 0f;
        if (halfMask)
        {
            var f = transform.forward;
            float z = f.y < -0.01f ? -transform.position.y / f.y : cam.farClipPlane * 0.4f;   // 카메라 → 바닥(y=0) 시선 거리 = 화면 가운데 바닥 깊이
            mref = Mathf.Clamp01((z - cam.nearClipPlane) / Mathf.Max(1f, cam.farClipPlane - cam.nearClipPlane));
        }
        Shader.SetGlobalFloat("_MarsMaskRef", mref);
        maskCam.backgroundColor = new Color(1f - mref, 1f - mref, 1f - mref, 1f);   // 빈 곳 = 1(먼 끝)
        int w = Mathf.Max(1, cam.pixelWidth), h = Mathf.Max(1, cam.pixelHeight);
        if (maskRT == null || maskRT.width != w || maskRT.height != h)
        {
            if (maskRT != null) { maskCam.targetTexture = null; maskRT.Release(); }
            maskRT = new RenderTexture(w, h, 24, halfMask ? RenderTextureFormat.RHalf : RenderTextureFormat.RFloat) { name = "OutlineMask", filterMode = FilterMode.Point };
            maskCam.targetTexture = maskRT;
        }
    }

    // 배경 루트(바닥·구역 울타리·장식·테라포밍·발판) 아래를 배경 레이어로. 구역 해제 등으로 새로 생기니 1초마다 다시 훑는다
    void TagEnv()
    {
        foreach (var n in ENV_ROOTS)
        {
            var r = GameObject.Find(n); if (r == null) continue;
            foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (t.gameObject.layer != ENV_LAYER) t.gameObject.layer = ENV_LAYER;
        }
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
                case "mask": maskOutline = v > 0.5f; break;
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
                case "sb": staticBatch = v > 0.5f; break;
                case "msaa": msaa = Mathf.Clamp((int)v, 1, 8); break;
            }
        }
        Debug.Log("PostFX: -fx " + s);
    }

    void LateUpdate()
    {
        // World.Lights()(World.Start) 뒤에 한 번: 직교 카메라용 그림자 설정
        if (!shadowTuned && Time.frameCount > 2) { shadowTuned = true; TuneShadows(); }
        // 배경 정적 배칭: 메사·바위가 다 선 뒤(KayKit 조각까지) 한 번. 이주(Restart)로 다시 조립되면 새 PostFX 가 다시 한다
        if (!batched && Time.frameCount > 10) { batched = true; StaticCombine(); }
        if (maskCam != null)
        {
            maskCam.enabled = maskOutline && !off;
            SyncMask();
            envT -= Time.unscaledDeltaTime; if (envT <= 0) { envT = 1f; TagEnv(); }
        }
    }

    void TuneShadows()
    {
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowCascades = 1;                       // 직교 카메라는 캐스케이드가 의미 없고 단일 맵이 텍셀 밀도가 높다
        QualitySettings.shadowProjection = ShadowProjection.StableFit;   // CloseFit은 카메라가 움직일 때 그림자 가장자리가 지글거렸다(jake 10/26 "확대하면 지지직")
        QualitySettings.antiAliasing = msaa;                       // 4배(검증 인자 -fx msaa=N, 모바일 2배는 지글거림으로 보류)
        QualitySettings.shadowDistance = shadowDistance;          // 카메라 거리 90 ± 보이는 깊이 ~16 + 화면 밖 긴 그림자 여유
        QualitySettings.shadowResolution = shadowRes;
        QualitySettings.shadowNearPlaneOffset = 2f;
        if (World.sun != null)
        {
            if (sunBias >= 0) World.sun.shadowBias = sunBias;
            if (sunNormalBias >= 0) World.sun.shadowNormalBias = sunNormalBias;
        }
    }

    // 렌더러 수·재질 수·서브메시 수(드로우콜 근사, 그림자·깊이·마스크 패스는 이 수에 비례)
    static string RenderStats()
    {
        var rs = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        int n = 0, vis = 0, sub = 0, visSub = 0, stat = 0; var mats = new System.Collections.Generic.HashSet<Material>();
        var batches = new System.Collections.Generic.HashSet<(Material, int)>();
        foreach (var r in rs)
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            n++; var ms = r.sharedMaterials; sub += ms.Length; foreach (var m in ms) if (m != null) mats.Add(m);
            if (r.isPartOfStaticBatch) stat++;
            if (r.isVisible)
            {
                vis++;
                // 정적 배칭된 렌더러는 같은 재질끼리 한 번에 그려진다(근사: 배치 루트·재질 쌍 하나 = 1콜)
                if (r.isPartOfStaticBatch) { for (int i = 0; i < ms.Length; i++) if (batches.Add((ms[i], r.GetComponent<MeshFilter>().sharedMesh.GetInstanceID()))) visSub++; }
                else visSub += ms.Length;
            }
        }
        return "renderers=" + n + " subMeshes=" + sub + " materials=" + mats.Count + " staticBatched=" + stat + " visible=" + vis + " visibleDrawsApprox=" + visSub;
    }

    void StaticCombine()
    {
        Debug.Log("perf before: " + RenderStats() + " mobileLook=" + MobileLook);
        if (!staticBatch) return;
        int c = 0;
        foreach (var path in STATIC_ROOTS)
        {
            var r = GameObject.Find(path); if (r == null) continue;
            // 런타임 메시는 읽기 가능해야 합칠 수 있다(ModelLib·ToyMesh 메시는 런타임 생성이라 가능). 못 읽는 메시는 Unity 가 건너뛴다
            StaticBatchingUtility.Combine(r); c++;
        }
        Debug.Log("perf after: " + RenderStats() + " roots=" + c);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null || off) { Graphics.Blit(src, dst); return; }
        float scale = Screen.height / 1170f;
        float px = Mathf.Max(1f, Mathf.Round(outlinePx * scale));
        float depthT = depthThreshL * GameConfig.K / Mathf.Max(1f, cam.farClipPlane - cam.nearClipPlane);
        var oc = outlineColor.linear; oc.a = outlineStrength;
        mat.SetColor("_OutlineColor", oc);
        bool useMask = maskOutline && maskRT != null;
        if (useMask) mat.SetTexture("_MaskDepth", maskRT);
        mat.SetVector("_MaskParams", new Vector4(useMask ? 1 : 0, depthT * 6f, 0, 0));
        mat.SetVector("_EdgeParams", new Vector4(px, depthT, normalThresh, normalStrength));
        mat.SetVector("_ToneParams", new Vector4(exposure, saturation, contrast, bloomIntensity));
        mat.SetVector("_VigParams", new Vector4(vignetteCenter.x, vignetteCenter.y, vignetteStart, vignette));
        mat.SetColor("_VigColor", vignetteColor);
        mat.SetVector("_VigRadius", new Vector4(vignetteRadius.x, vignetteRadius.y, 0, 0));
        mat.SetVector("_BloomParams", new Vector4(bloomThreshold, bloomKnee, 0, 0));

        if (bloomIntensity > 0 && bloomDiv > 4)
        {
            // 모바일 1/8: 프리필터는 1/4 로(작은 밝은 점이 빠져 깜빡이지 않게 원본 텍셀을 다 덮는다), 첫 가로 블러가 1/8 로 줄이며 흐린다.
            //  블러 반경(1/4 텍셀 기준 σ ≈ √(1.4²+2.8²))은 맥 경로와 같게: 가로 1(1/4 텍셀)+1(1/8 텍셀), 세로 0.5+1(1/8 텍셀)
            int w4 = Mathf.Max(1, src.width / 4), h4 = Mathf.Max(1, src.height / 4);
            int w8 = Mathf.Max(1, src.width / bloomDiv), h8 = Mathf.Max(1, src.height / bloomDiv);
            var a = RenderTexture.GetTemporary(w4, h4, 0, src.format);
            var b = RenderTexture.GetTemporary(w8, h8, 0, src.format); var c = RenderTexture.GetTemporary(w8, h8, 0, src.format);
            a.filterMode = FilterMode.Bilinear; b.filterMode = FilterMode.Bilinear; c.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, a, mat, 1);
            mat.SetVector("_BlurDir", new Vector4(1, 0, 0, 0)); Graphics.Blit(a, b, mat, 2);
            mat.SetVector("_BlurDir", new Vector4(0, 0.5f, 0, 0)); Graphics.Blit(b, c, mat, 2);
            mat.SetVector("_BlurDir", new Vector4(1, 0, 0, 0)); Graphics.Blit(c, b, mat, 2);
            mat.SetVector("_BlurDir", new Vector4(0, 1, 0, 0)); Graphics.Blit(b, c, mat, 2);
            mat.SetTexture("_BloomTex", c);
            Graphics.Blit(src, dst, mat, 0);
            RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b); RenderTexture.ReleaseTemporary(c);
        }
        else if (bloomIntensity > 0)
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

    void OnDestroy() { if (mat != null) Destroy(mat); if (maskRT != null) maskRT.Release(); }
}

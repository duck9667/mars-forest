// 화성 물병 가게 포스트 프로세스(내장 RP, PostFX.cs 의 OnRenderImage 에서 사용). 명세 §7.2.
//  패스 0 합성: 외곽선(깊이 2차 미분 엣지 검출) → 블룸 더하기 → ACES Filmic(three.js ACESFilmicToneMapping 과 같은 식, exposure/0.6)
//             → sRGB 공간에서 채도·대비 → 비네트(원본 DOM #vig: ellipse 80% 70% at 50% 45%, 55%→100%, rgba(40,8,0,.38))
//  패스 1 블룸 프리필터(1/4 해상도, 부드러운 문턱)  패스 2 가우시안 블러(방향은 _BlurDir)
// 외곽선은 역면 hull 대신 카메라 깊이 텍스처를 쓴다. 모든 물체가 프리미티브·런타임 메시라 메시마다 hull 을 다는 것보다 일관되고,
// 직교 카메라라 깊이가 선형이어서 "이웃 평균보다 먼 픽셀(2차 미분 > 문턱)" 만 칠하면 평면의 기울기는 상쇄돼 바닥 내부엔 선이 안 생기고
// 물체의 바깥쪽(먼 쪽 실루엣)과 바닥에 닿는 오목 모서리에만 선이 생긴다. 투명 물체(접지 그림자·링·구역 덮개)는 깊이 텍스처에 없어 원본처럼 제외된다.
Shader "Hidden/Mars/PostFX"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex; float4 _MainTex_TexelSize;
    sampler2D_float _CameraDepthTexture; float4 _CameraDepthTexture_TexelSize;
    sampler2D _CameraDepthNormalsTexture;
    sampler2D _BloomTex;
    sampler2D_float _MaskDepth;   // 배경 레이어를 뺀 물체만의 선형 깊이(MarsMaskDepth). 비어 있는 곳 = 1
    float _MarsMaskRef;          // 모바일 RHalf 마스크가 빼 둔 기준 깊이(PostFX.SyncMask). RFloat 는 0
    float4 _MaskParams;          // x 1이면 마스크 깊이로 외곽선(배경엔 선 없음)  y 가림 허용 오차
    float4 _OutlineColor;   // 선형 공간 색, a = 세기
    float4 _EdgeParams;     // x 샘플 오프셋(px)  y 깊이 문턱(near~far 를 0~1 로 본 단위)  z 노멀 문턱(1-dot)  w 노멀 선 세기(0이면 노멀 텍스처 안 읽음)
    float4 _ToneParams;     // x 노출(three.js toneMappingExposure 와 같은 의미)  y 채도  z 대비  w 블룸 세기
    float4 _VigParams;      // x,y 중심(화면 uv, y 는 아래가 0)  z 시작 반경(0~1)  w 알파
    float4 _VigColor;       // sRGB
    float4 _VigRadius;      // x,y 타원 반경(uv)
    float4 _BloomParams;    // x 문턱(선형 HDR)  y 무릎
    float4 _BlurDir;        // xy 블러 방향(텍셀 배수)

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 uvd : TEXCOORD1; float4 spos : TEXCOORD2; };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        // 깊이 텍스처는 뒤집히지 않으므로 소스 RT 가 뒤집힌 경우(_MainTex_TexelSize.y<0) 깊이용 uv 만 되돌린다 (내장 이미지 이펙트 관례)
        o.uvd = v.texcoord;
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0) o.uvd.y = 1 - o.uvd.y;
        #endif
        o.spos = ComputeScreenPos(o.pos);
        return o;
    }

    // 직교 카메라의 깊이 버퍼는 선형. 플랫폼과 무관하게 "멀수록 큰 값(0 near ~ 1 far)" 으로 통일
    float Depth(float2 uv)
    {
        float d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
        #if defined(UNITY_REVERSED_Z)
        d = 1.0 - d;
        #endif
        return d;
    }

    // 깊이 2차 미분 엣지. 축 2쌍 + 대각 2쌍 중 최대. 양수 = 중심이 이웃 평균보다 멀다 = 단차의 먼 쪽(물체 바깥) 또는 오목 모서리.
    // 평면 위에서는 좌우 기울기가 상쇄돼 0, 볼록 모서리(상자 윗모서리)는 음수라 선이 안 생긴다.
    float EdgeDepth(float2 uv, float2 o, float t)
    {
        float d0 = Depth(uv);
        float dl = Depth(uv - float2(o.x, 0)), dr = Depth(uv + float2(o.x, 0));
        float dd = Depth(uv - float2(0, o.y)), du = Depth(uv + float2(0, o.y));
        float2 q = o * 0.7071;
        float d1 = Depth(uv + float2(-q.x, -q.y)), d2 = Depth(uv + float2(q.x, q.y));
        float d3 = Depth(uv + float2(-q.x, q.y)), d4 = Depth(uv + float2(q.x, -q.y));
        float e = max(max(2 * d0 - dl - dr, 2 * d0 - dd - du), max(2 * d0 - d1 - d2, 2 * d0 - d3 - d4));
        return smoothstep(t, t * 2.0, e);
    }

    // 마스크 깊이 엣지: 배경을 뺀 물체만의 깊이로 같은 2차 미분. 실루엣 바깥 픽셀(배경 위)에 선이 생기므로
    // 그 물체가 다른 물체(메사·건물) 뒤에 가려져 있으면(마스크 최솟값이 장면 깊이보다 멀면) 선을 지운다.
    float MaskD(float2 uv) { return tex2D(_MaskDepth, uv).r + _MarsMaskRef; }
    float EdgeMask(float2 uv, float2 o, float t)
    {
        float d0 = MaskD(uv);
        float dl = MaskD(uv - float2(o.x, 0)), dr = MaskD(uv + float2(o.x, 0));
        float dd = MaskD(uv - float2(0, o.y)), du = MaskD(uv + float2(0, o.y));
        float2 q = o * 0.7071;
        float d1 = MaskD(uv + float2(-q.x, -q.y)), d2 = MaskD(uv + float2(q.x, q.y));
        float d3 = MaskD(uv + float2(-q.x, q.y)), d4 = MaskD(uv + float2(q.x, -q.y));
        float e = max(max(2 * d0 - dl - dr, 2 * d0 - dd - du), max(2 * d0 - d1 - d2, 2 * d0 - d3 - d4));
        float near = min(min(min(dl, dr), min(dd, du)), min(min(d1, d2), min(d3, d4)));
        // 선이 그려질 자리(실루엣 바깥)의 장면 깊이가 물체보다 충분히 멀어야 한다: 바닥에 붙은 납작한 원판(서리·발판) 둘레엔 선을 안 긋는다
        float sd = Depth(uv);
        float vis = step(near, sd + _MaskParams.y) * smoothstep(t * 2.0, t * 4.0, sd - near);
        return smoothstep(t, t * 2.0, e) * vis;
    }

    // 노멀 엣지(선택): 이웃과의 1-dot 최대. 깊이 단차가 있는 곳은 깊이 엣지가 이미 칠하므로 거기선 끈다(물체 안쪽까지 두 줄 생기는 것 방지)
    float EdgeNormal(float2 uv, float2 o, float t, float dt)
    {
        float3 n0 = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, uv));
        float3 nl = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, uv - float2(o.x, 0)));
        float3 nr = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, uv + float2(o.x, 0)));
        float3 nd = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, uv - float2(0, o.y)));
        float3 nu = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, uv + float2(0, o.y)));
        float e = max(max(1 - dot(n0, nl), 1 - dot(n0, nr)), max(1 - dot(n0, nd), 1 - dot(n0, nu)));
        float d0 = Depth(uv);
        float jump = max(max(abs(Depth(uv - float2(o.x, 0)) - d0), abs(Depth(uv + float2(o.x, 0)) - d0)),
                         max(abs(Depth(uv - float2(0, o.y)) - d0), abs(Depth(uv + float2(0, o.y)) - d0)));
        return smoothstep(t, t * 1.3, e) * (1 - smoothstep(dt * 4, dt * 8, jump));
    }

    // three.js ACESFilmicToneMapping (Stephen Hill fit). 입력은 선형, 출력도 선형 0~1
    float3 AcesFilmic(float3 c, float exposure)
    {
        c *= exposure / 0.6;
        float3 a = float3(dot(c, float3(0.59719, 0.35458, 0.04823)), dot(c, float3(0.07600, 0.90834, 0.01566)), dot(c, float3(0.02840, 0.13383, 0.83777)));
        float3 r = (a * (a + 0.0245786) - 0.000090537) / (a * (0.983729 * a + 0.4329510) + 0.238081);
        float3 o = float3(dot(r, float3(1.60475, -0.53108, -0.07367)), dot(r, float3(-0.10208, 1.10813, -0.00605)), dot(r, float3(-0.00327, -0.07276, 1.07602)));
        return saturate(o);
    }

    float4 fragComposite(v2f i) : SV_Target
    {
        float3 c = tex2D(_MainTex, i.uv).rgb;
        float2 o = _CameraDepthTexture_TexelSize.xy * _EdgeParams.x;
        float edge = _MaskParams.x > 0.5 ? EdgeMask(i.uvd, o, _EdgeParams.y) : EdgeDepth(i.uvd, o, _EdgeParams.y);
        if (_EdgeParams.w > 0) edge = max(edge, EdgeNormal(i.uvd, o, _EdgeParams.z, _EdgeParams.y) * _EdgeParams.w);
        if (_ToneParams.w > 0) c += tex2D(_BloomTex, i.uv).rgb * _ToneParams.w;
        c = lerp(c, _OutlineColor.rgb, edge * _OutlineColor.a);
        c = AcesFilmic(c, _ToneParams.x);

        // 채도·대비·비네트는 sRGB 공간에서 (원본은 톤매핑된 캔버스 위에 CSS 그라디언트를 합성)
        float3 s = LinearToGammaSpace(c);
        float l = dot(s, float3(0.299, 0.587, 0.114));
        s = lerp(l.xxx, s, _ToneParams.y);
        s = (s - 0.5) * _ToneParams.z + 0.5;
        float2 suv = i.spos.xy / i.spos.w;
        float2 p = (suv - _VigParams.xy) / _VigRadius.xy;
        float vt = saturate((length(p) - _VigParams.z) / max(1e-3, 1 - _VigParams.z));
        s = lerp(s, _VigColor.rgb, vt * _VigParams.w);
        return float4(GammaToLinearSpace(saturate(s)), 1);
    }

    // 블룸 프리필터: 4탭 박스 다운샘플 + 부드러운 문턱(무릎)
    float4 fragPrefilter(v2f i) : SV_Target
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(-1, -1, 1, 1);
        float3 c = tex2D(_MainTex, i.uv + d.xy).rgb + tex2D(_MainTex, i.uv + d.zy).rgb + tex2D(_MainTex, i.uv + d.xw).rgb + tex2D(_MainTex, i.uv + d.zw).rgb;
        c *= 0.25;
        float br = max(c.r, max(c.g, c.b));
        float knee = _BloomParams.y;
        float soft = clamp(br - _BloomParams.x + knee, 0, 2 * knee);
        soft = soft * soft / (4 * knee + 1e-4);
        float w = max(soft, br - _BloomParams.x) / max(br, 1e-4);
        return float4(c * w, 1);
    }

    // 9탭 가우시안(선형 보간 5샘플)
    float4 fragBlur(v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy * _BlurDir.xy;
        float3 c = tex2D(_MainTex, i.uv).rgb * 0.227027;
        c += (tex2D(_MainTex, i.uv + d * 1.384615).rgb + tex2D(_MainTex, i.uv - d * 1.384615).rgb) * 0.316216;
        c += (tex2D(_MainTex, i.uv + d * 3.230769).rgb + tex2D(_MainTex, i.uv - d * 3.230769).rgb) * 0.070270;
        return float4(c, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG
        }
    }
    Fallback Off
}

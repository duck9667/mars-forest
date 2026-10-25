// 정점 색 저폴리 모형(ToyMesh): 물건·얼음·나무·돌을 한 메시 한 재질로 그린다(드로우콜 1, 같은 모형끼리는 인스턴싱).
// 조명은 Standard 와 같고 알베도만 정점 색. 불투명이라 외곽선 마스크(RenderType=Opaque)에 그대로 잡히고 투명 정렬 깜빡임이 없다.
// _Rim: 가장자리가 살짝 밝아지는 정도(얼음의 속 비침 흉내), _Self: 제 색 발광. 블룸 문턱(PostFX 2.4)에 닿지 않게 작게 쓴다.
Shader "Mars/VColor"
{
    Properties
    {
        _Glossiness ("매끈함", Range(0,1)) = 0.1
        _Rim ("가장자리 밝기", Range(0,1)) = 0
        _RimColor ("가장자리 색", Color) = (0.85, 0.95, 1, 1)
        _Self ("제 색 발광", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma multi_compile_instancing
        #pragma target 3.0
        half _Glossiness, _Rim, _Self; fixed4 _RimColor;
        float _MarsNight;   // DayNight 가 넣는 전역 밤 정도(0 낮 ~ 1 밤)
        struct Input { float4 color : COLOR; float3 viewDir; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // 정점 색은 감마(sRGB) 값으로 넣으므로 리니어 색 공간에선 변환해야 Mats.Solid(재질 색)와 같은 색이 된다
            #ifdef UNITY_COLORSPACE_GAMMA
            o.Albedo = IN.color.rgb;
            #else
            o.Albedo = GammaToLinearSpace(IN.color.rgb);
            #endif
            // 밤: 하늘색 물건이 진한 파랑으로 튀지 않게 채도를 덜어 낸다(DayNight SOFT_* 와 같은 몫)
            half lum = dot(o.Albedo, half3(0.299, 0.587, 0.114));
            o.Albedo = lerp(o.Albedo, lum.xxx * 1.05, _MarsNight * 0.45);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            half f = 1 - saturate(dot(normalize(IN.viewDir), o.Normal));
            // _Self: 제 색으로 조금 빛나 따뜻한 햇빛·저녁빛에도 색조가 탁해지지 않게(얼음 하늘색). 블룸 문턱(2.4)보다 한참 낮다
            o.Emission = _RimColor.rgb * (_Rim * f * f * f) * o.Albedo + o.Albedo * (_Self * (1 - _MarsNight));
        }
        ENDCG
    }
    Fallback "Diffuse"
}

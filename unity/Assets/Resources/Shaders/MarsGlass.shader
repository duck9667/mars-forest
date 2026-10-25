// 맑은 유리·얼음 겉면(다듬기 3차, docs/art/art-bible.md). Legacy Transparent/Diffuse 는 따뜻한 햇빛을 그대로 받아 유리가 분홍 판처럼 보였다.
// 가운데는 거의 비치고(가운데 알파), 가장자리로 갈수록 진해지는 프레넬 테 + 해 쪽 반짝임 하나. 조명 색은 밝기만 따르고 색조는 따르지 않는다(낮 분홍·밤 파랑 방지).
// 깊이를 쓰지 않으므로 외곽선 마스크(MarsMaskDepth: Opaque 만)에 잡히지 않는다.
// _VHi = 1 인 재질은 정점 색 알파를 "하이라이트 양"으로 읽는다(10/26 유리 표현): 위에서 내려다보면 프레넬 테가 거의 안 생기므로
// 메시에 구운 가장자리 테·반사 줄이 형태를 읽게 한다. 같은 메시 안이라 정렬이 고정돼 깜빡이지 않고, 밝기는 조명만 따라 1 남짓(블룸 문턱 2.4 아래).
// _RimAdd(r6, 얼음 Mars/Ice 와 같은 가장자리 밝힘): 시선과 비스듬한 면을 흰 하늘빛으로 더 밝히고 조금 더 불투명하게 → 돔 둘레·진열장 끝 판이 형태로 읽힌다.
//  지수 _RimPow 가 낮아 위에서 내려다봐도(고도 55°) 둘레에 넓게 생긴다. 밤엔 줄이고(_MarsNight), 더해지는 밝기는 0.4 남짓(블룸 문턱 아래).
Shader "Mars/Glass"
{
    Properties
    {
        _Color ("가운데 색(알파 = 가운데 투명도)", Color) = (0.92, 0.97, 1, 0.06)
        _EdgeColor ("가장자리 색(알파 = 테 투명도)", Color) = (0.85, 0.94, 1, 0.45)
        _Pow ("프레넬 지수", Float) = 2.5
        _Spec ("반짝임 세기", Float) = 0.8
        _Gloss ("반짝임 날카로움", Float) = 60
        _VHi ("정점 알파 하이라이트(0 끔·1 켬)", Float) = 0
        _HiColor ("하이라이트 색(알파 = 불투명도)", Color) = (1, 1, 1, 0.85)
        _RimAdd ("가장자리 밝힘(0 끔)", Float) = 0
        _RimPow ("가장자리 밝힘 지수", Float) = 1.6
        _RimColor ("가장자리 밝힘 색", Color) = (0.88, 0.96, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            fixed4 _Color, _EdgeColor, _HiColor, _RimColor; float _Pow, _Spec, _Gloss, _VHi, _RimAdd, _RimPow;
            float _MarsNight;   // DayNight 가 넣는 전역 밤 정도(0 낮 ~ 1 밤)
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; float hi : TEXCOORD2; };
            v2f vert(appdata_full i)
            {
                v2f o;
                o.hi = saturate(i.color.a) * _VHi;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, i.vertex).xyz);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.n), v = normalize(i.v), l = normalize(_WorldSpaceLightPos0.xyz);
                float f = pow(1 - saturate(abs(dot(n, v))), _Pow);
                fixed4 c = lerp(_Color, _EdgeColor, f);
                // 밝기만 조명을 따른다(색조는 무시): 햇빛·반구광 휘도
                float3 lit = ShadeSH9(float4(n, 1)) + _LightColor0.rgb * 0.6;
                float lum = saturate(dot(lit, float3(0.299, 0.587, 0.114)) * 0.9 + 0.25);
                c.rgb *= lum;
                // 구운 하이라이트(테·반사 줄): 밝기만 조명을 따르는 흰빛
                c.rgb = lerp(c.rgb, _HiColor.rgb * lum, i.hi);
                c.a = lerp(c.a, _HiColor.a, i.hi);
                // 가장자리 밝힘: 비스듬한 면일수록 흰 하늘빛을 더하고 조금 더 불투명하게(밤엔 줄임)
                float rim = _RimAdd * pow(1 - saturate(abs(dot(n, v))), _RimPow) * (1 - 0.65 * _MarsNight);
                c.rgb += _RimColor.rgb * rim * lum; c.a = saturate(c.a + rim * 0.5);
                float s = pow(saturate(dot(n, normalize(l + v))), _Gloss) * _Spec * saturate(dot(_LightColor0.rgb, float3(0.33, 0.33, 0.33)));
                c.rgb += s; c.a = saturate(c.a + s);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}

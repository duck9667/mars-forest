// 맑은 유리·얼음 겉면(다듬기 3차, docs/art/art-bible.md). Legacy Transparent/Diffuse 는 따뜻한 햇빛을 그대로 받아 유리가 분홍 판처럼 보였다.
// 가운데는 거의 비치고(가운데 알파), 가장자리로 갈수록 진해지는 프레넬 테 + 해 쪽 반짝임 하나. 조명 색은 밝기만 따르고 색조는 따르지 않는다(낮 분홍·밤 파랑 방지).
// 깊이를 쓰지 않으므로 외곽선 마스크(MarsMaskDepth: Opaque 만)에 잡히지 않는다.
Shader "Mars/Glass"
{
    Properties
    {
        _Color ("가운데 색(알파 = 가운데 투명도)", Color) = (0.92, 0.97, 1, 0.06)
        _EdgeColor ("가장자리 색(알파 = 테 투명도)", Color) = (0.85, 0.94, 1, 0.45)
        _Pow ("프레넬 지수", Float) = 2.5
        _Spec ("반짝임 세기", Float) = 0.8
        _Gloss ("반짝임 날카로움", Float) = 60
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
            fixed4 _Color, _EdgeColor; float _Pow, _Spec, _Gloss;
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; };
            v2f vert(appdata_base i)
            {
                v2f o;
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
                float s = pow(saturate(dot(n, normalize(l + v))), _Gloss) * _Spec * saturate(dot(_LightColor0.rgb, float3(0.33, 0.33, 0.33)));
                c.rgb += s; c.a = saturate(c.a + s);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}

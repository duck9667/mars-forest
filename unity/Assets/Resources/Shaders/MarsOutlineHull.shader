// 역면(hull) 외곽선: 원본 outMat(MeshBasicMaterial BackSide) 대응. Outline.cs 가 평균 노멀 메시를 만들어 붙인다.
// 월드 공간에서 노멀 방향으로 _Thick(Unity 단위)만큼 밀어내므로 물체 스케일과 무관하게 두께가 일정하다.
// 기본 외곽선은 PostFX 의 깊이 엣지가 맡고, 이 셰이더는 색을 따로 줘야 하는 물체(VIP 금색 0xd49000)에만 쓴다.
Shader "Mars/OutlineHull"
{
    Properties
    {
        _Color ("Color", Color) = (0.106, 0.102, 0.18, 1)
        _Thick ("Thickness (world units)", Float) = 0.08
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-1" }
        Cull Front ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color; float _Thick;
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert(appdata_base v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wn = UnityObjectToWorldNormal(v.normal);
                wp += normalize(wn) * _Thick;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return _Color; }
            ENDCG
        }
    }
    Fallback Off
}

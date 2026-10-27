// 외곽선 마스크 깊이(PostFX 의 마스크 카메라가 RenderWithShader 대신 SetReplacementShader 로 쓴다).
// 배경 레이어(PostFX.ENV_LAYER: 바닥·울타리·메사·소품)를 뺀 불투명 물체만 그려 선형 깊이(0 near ~ 1 far)를 RFloat 에 기록한다.
// 합성 패스가 이 깊이로 엣지를 찾으므로 외곽선은 캐릭터·물건·기계에만 생긴다(jake 10/25 "배경까지 테두리라 복잡").
// 모바일 RHalf 마스크(r9)는 화면 가운데 바닥 깊이 _MarsMaskRef 를 빼서 0 근처로 저장한다(반정밀도 간격이 외곽선 문턱보다 거칠지 않게). RFloat 는 0
Shader "Hidden/Mars/MaskDepth"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _MarsMaskRef;
            struct v2f { float4 pos : SV_POSITION; float z : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.z = -UnityObjectToViewPos(v.vertex).z;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float d = (i.z - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y) - _MarsMaskRef;
                return float4(d, d, d, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}

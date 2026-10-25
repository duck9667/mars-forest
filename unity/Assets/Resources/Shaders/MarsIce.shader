// 얼음 전용(r6, docs/art/art-bible.md "유리·얼음"): 불투명 메시인데 뒤가 비쳐 보이게.
//  - 비침: 얼음은 모든 불투명 물체 뒤(AlphaTest+20)에 그리고, 그 직전 화면을 이름 붙인 GrabPass 로 프레임당 한 번만 떠서
//    면 법선만큼 살짝 밀어(가짜 굴절) 얼음빛을 곱해 섞는다. 깊이를 쓰는 불투명이라 정렬 깜빡임이 없고, 외곽선 마스크(RenderType=Opaque)엔
//    예전처럼 바깥 둘레만 잡힌다. 얼음 위 얼음은 비침에 안 보이지만(같은 대기열) 비침 양이 절반 남짓이라 티가 나지 않는다.
//  - 속 깊이: 정면으로 보는 면일수록 속을 길게 지나므로 색을 진하게(가짜 깊이), 비스듬한 면은 비침을 덜고 얼음빛을 더한다.
//  - 가장자리 밝힘(프레넬 테): 시선과 비스듬한 면에 흰 하늘빛 테. 밤엔 줄인다(_MarsNight).
//  - 정점 색 알파 = 비침 양(1 비침, 0 불투명): 깎은 위 모서리·윗면 반사 줄(Items.ICE_EDGE·ICE_GLINT)은 덜 비치게 해서 흰빛이 남는다.
//  밝기: 비침은 뒤 화면(이미 조명 받은 HDR 값)에 1 아래 얼음빛을 곱한 것이라 뒤보다 밝아지지 않고, 테는 0.3 남짓이라 블룸 문턱(2.4) 아래.
Shader "Mars/Ice"
{
    Properties
    {
        _Glossiness ("매끈함", Range(0,1)) = 0.55
        _Self ("제 색 발광", Range(0,1)) = 0.16
        _Trans ("비침 양", Range(0,1)) = 0.55
        _Tint ("비침 색(얼음 속 빛깔)", Color) = (0.5, 0.8, 1, 1)
        _Refract ("굴절 밀림(화면 비율)", Range(0,0.05)) = 0.012
        _Deep ("속 깊이(정면일수록 진하게)", Range(0,1)) = 0.45
        _Rim ("가장자리 밝힘", Range(0,1)) = 0.35
        _RimPow ("가장자리 지수", Float) = 2.5
        _RimColor ("가장자리 색", Color) = (0.9, 0.97, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest+20" }
        GrabPass { "_MarsIceBG" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma multi_compile_instancing
        #pragma target 3.0
        sampler2D _MarsIceBG;
        half _Glossiness, _Self, _Trans, _Refract, _Deep, _Rim, _RimPow; fixed4 _Tint, _RimColor;
        float _MarsNight;   // DayNight 가 넣는 전역 밤 정도(0 낮 ~ 1 밤)
        struct Input { float4 color : COLOR; float3 viewDir; float4 grabPos; };
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.grabPos = ComputeGrabScreenPos(UnityObjectToClipPos(v.vertex));
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            #ifdef UNITY_COLORSPACE_GAMMA
            half3 alb = IN.color.rgb;
            #else
            half3 alb = GammaToLinearSpace(IN.color.rgb);
            #endif
            // 밤: 하늘색이 진한 파랑으로 튀지 않게 채도를 덜어 낸다(Mars/VColor 와 같은 몫)
            half lum = dot(alb, half3(0.299, 0.587, 0.114));
            alb = lerp(alb, lum.xxx * 1.05, _MarsNight * 0.45);
            half ndv = saturate(dot(normalize(IN.viewDir), o.Normal));
            half f = 1 - ndv;
            // 속 깊이: 정면 면은 제곱(채도·명도 ↓)쪽으로
            alb = lerp(alb, alb * alb * 1.25, _Deep * ndv);
            // 비침 양: 정면일수록 많이, 비스듬하면 반사가 늘어 적게. 정점 알파로 모서리·반사 줄은 덜 비치게
            half t = _Trans * IN.color.a * lerp(0.35, 1, ndv);
            // 가짜 굴절: 뷰 공간 법선 xy 만큼 화면 좌표를 민다
            float3 vn = mul((float3x3)UNITY_MATRIX_V, o.Normal);
            float2 uv = IN.grabPos.xy / IN.grabPos.w + vn.xy * _Refract;
            half3 bg = tex2D(_MarsIceBG, uv).rgb;
            half3 tint = lerp(_Tint.rgb, dot(_Tint.rgb, half3(0.299, 0.587, 0.114)).xxx, _MarsNight * 0.45);
            o.Albedo = alb * (1 - t);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            half rim = _Rim * pow(f, _RimPow) * (1 - 0.65 * _MarsNight);
            o.Emission = bg * tint * t + _RimColor.rgb * rim + alb * (_Self * (1 - _MarsNight)) * (1 - t);
        }
        ENDCG
    }
    Fallback "Diffuse"
}

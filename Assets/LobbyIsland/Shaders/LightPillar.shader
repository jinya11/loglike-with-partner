// 구름 바다 틈에서 솟는 빛기둥(가산 합성). _Mode 0 = 세로 기둥(쿼드 v=0이 아래), 1 = 바닥의 둥근 번짐.
// 밝기·색은 스크립트가 MaterialPropertyBlock(_Intensity, _Color)으로 조절한다.
Shader "LobbyIsland/LightPillar"
{
    Properties
    {
        [HDR] _Color ("Glow Color", Color) = (1.2, 0.75, 0.95, 1)
        [HDR] _CoreColor ("Core Color", Color) = (2.8, 1.7, 2.1, 1)
        _Intensity ("Intensity", Range(0, 2)) = 1
        _CoreWidth ("Core Width", Range(0.005, 0.3)) = 0.07
        _GlowWidth ("Glow Width", Range(0.05, 1)) = 0.32
        _TopFade ("Top Fade Start", Range(0, 1)) = 0.25
        _Flicker ("Flicker", Range(0, 1)) = 0.25
        [Enum(Pillar,0,Radial,1)] _Mode ("Mode", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _CoreColor;
                float _Intensity, _CoreWidth, _GlowWidth, _TopFade, _Flicker, _Mode;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; float seed : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.seed = frac(dot(TransformObjectToWorld(float3(0, 0, 0)).xz, float2(0.0137, 0.0071)));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 col;
                if (_Mode < 0.5)
                {
                    float x = i.uv.x - 0.5;
                    float core = exp(-pow(x / _CoreWidth, 2));
                    float glow = exp(-pow(x / _GlowWidth, 2));
                    // 아래(구름 속)는 부드럽게 시작, 위로 갈수록 하늘에 녹아 사라짐
                    float v = i.uv.y;
                    float vert = smoothstep(0.0, 0.06, v) * (1 - smoothstep(_TopFade, 1.0, v));
                    float t = _Time.y + i.seed * 40;
                    float flick = 1 - _Flicker * (0.5 + 0.5 * sin(t * 2.3 + v * 9) * sin(t * 1.1));
                    col = (_CoreColor.rgb * core + _Color.rgb * glow * 0.75) * vert * flick;
                }
                else
                {
                    float d = length(i.uv - 0.5) * 2;
                    float g = exp(-d * d * 4) * (1 - smoothstep(0.8, 1.0, d));
                    col = _Color.rgb * g * 0.6;
                }
                col *= _Intensity;
                // 가산 합성: 멀수록 안개에 묻혀 연해지되 완전히 사라지지는 않게
                col *= lerp(0.45, 1.0, ComputeFogIntensity(i.fog));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}

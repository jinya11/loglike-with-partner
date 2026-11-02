// 폭포: UV v 방향으로 흐르는 줄무늬 + 가장자리/끝 페이드. 메시는 위(v=1)에서 아래(v=0)로 뻗은 쿼드.
Shader "LobbyIsland/Waterfall"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.85, 0.95, 1.2, 0.85)
        [HDR] _StreakColor ("Streak Color", Color) = (1.6, 1.6, 1.8, 1)
        _Speed ("Speed", Float) = 1.4
        _StreakScale ("Streak Scale", Vector) = (7, 2.5, 0, 0)
        _BottomFade ("Bottom Fade", Range(0.01, 1)) = 0.45
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _StreakColor, _StreakScale;
                float _Speed, _BottomFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = float2(i.uv.x * _StreakScale.x, i.uv.y * _StreakScale.y + _Time.y * _Speed);
                float n = noise(p) * 0.6 + noise(p * 2.3 + 7.1) * 0.4;
                float streak = smoothstep(0.45, 0.8, n);
                float edge = smoothstep(0, 0.2, i.uv.x) * smoothstep(1, 0.8, i.uv.x);
                float bottom = smoothstep(0, _BottomFade, i.uv.y);
                float3 col = lerp(_Color.rgb, _StreakColor.rgb, streak);
                float a = _Color.a * edge * bottom * (0.55 + 0.45 * n);
                col = MixFog(col, i.fog);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}

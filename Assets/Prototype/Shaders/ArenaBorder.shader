// 지역 스테이지의 정사각형 보더: 얇은 에너지 장막 (가산 블렌드, 양면).
// uv.x = 벽을 따라간 거리(m), uv.y = 0(바닥)~1(꼭대기). ArenaBorder가 메시를 만듦.
Shader "Roguelike/ArenaBorder"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.45, 0.75, 1.6, 1)
        [HDR] _EdgeColor ("Ground Edge Color", Color) = (1.2, 1.6, 2.4, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _StreakDensity ("Streak Density (per m)", Float) = 0.6
        _ScrollSpeed ("Scroll Speed", Float) = 0.6
        _GridSize ("Grid Size (m)", Float) = 1.6
        _Pulse ("Pulse", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Border"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _EdgeColor;
                half _Intensity, _StreakDensity, _ScrollSpeed, _GridSize, _Pulse;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float worldY : TEXCOORD1; half fog : TEXCOORD2; };

            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            float Noise1(float x)
            {
                float i = floor(x), f = frac(x);
                return lerp(Hash(i), Hash(i + 1.0), f * f * (3.0 - 2.0 * f));
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = v.uv;
                o.worldY = p.positionWS.y;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float x = i.uv.x, y = saturate(i.uv.y);

                // 위로 갈수록 사라지는 장막
                float fade = pow(1.0 - y, 1.6);
                // 위로 흐르는 세로 줄기 (위치마다 다른 속도/밝기)
                float sx = x * _StreakDensity;
                float lane = floor(sx);
                float flow = frac(y * 1.5 - t * _ScrollSpeed * (0.6 + Hash(lane) * 0.8) + Hash(lane + 7.0));
                float streak = smoothstep(0.35, 0.0, abs(frac(sx) - 0.5)) * pow(flow, 6.0) * Noise1(sx * 3.1 + t);
                // 옅은 격자
                float2 g = abs(frac(float2(x, i.worldY) / _GridSize) - 0.5);
                float grid = smoothstep(0.47, 0.5, max(g.x, g.y)) * 0.35;
                // 바닥에서 위로 스캔되는 띠
                float scan = smoothstep(0.08, 0.0, abs(y - frac(t * 0.25))) * 0.5;
                // 바닥 경계선 (지면과 닿는 곳)
                float edge = smoothstep(0.06, 0.0, y);

                float pulse = 1.0 + sin(t * 2.2 + x * 0.15) * _Pulse;
                half3 c = _Color.rgb * (0.18 + grid + streak * 1.6 + scan) * fade * pulse + _EdgeColor.rgb * edge;
                c *= _Intensity;
                c = MixFogColor(c, half3(0, 0, 0), i.fog); // 가산 블렌드라 안개 속에서는 검정으로 사라짐
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}

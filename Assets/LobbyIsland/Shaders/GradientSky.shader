// 로비 노을 하늘: 수직 4색 그라데이션 + 태양 글로우(HDR로 블룸 유도)
Shader "LobbyIsland/GradientSky"
{
    Properties
    {
        [HDR] _TopColor ("Top", Color) = (0.55, 0.75, 1.0, 1)
        [HDR] _MidColor ("Mid", Color) = (0.85, 0.75, 0.95, 1)
        [HDR] _HorizonColor ("Horizon", Color) = (1.0, 0.72, 0.78, 1)
        [HDR] _BottomColor ("Bottom", Color) = (0.95, 0.7, 0.85, 1)
        _MidHeight ("Mid Height", Range(0, 1)) = 0.25
        _HorizonSharpness ("Horizon Sharpness", Range(0.5, 8)) = 2.5
        _SunDir ("Sun Direction", Vector) = (-0.8, 0.05, 0.6, 0)
        [HDR] _SunColor ("Sun Color", Color) = (6, 3.6, 2.4, 1)
        _SunSize ("Sun Size", Range(0.0005, 0.05)) = 0.006
        [HDR] _SunGlowColor ("Sun Glow", Color) = (1.4, 0.75, 0.6, 1)
        _SunGlowPower ("Sun Glow Power", Range(1, 64)) = 10
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor, _MidColor, _HorizonColor, _BottomColor;
                float _MidHeight, _HorizonSharpness;
                float4 _SunDir, _SunColor, _SunGlowColor;
                float _SunSize, _SunGlowPower;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 col;
                if (y >= 0)
                {
                    float h = pow(saturate(y), 1.0 / _HorizonSharpness);
                    float3 low = lerp(_HorizonColor.rgb, _MidColor.rgb, saturate(h / _MidHeight));
                    col = lerp(low, _TopColor.rgb, saturate((h - _MidHeight) / (1 - _MidHeight)));
                }
                else
                {
                    col = lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-y * 3));
                }
                float3 sd = normalize(_SunDir.xyz);
                float cosA = saturate(dot(d, sd));
                float glow = pow(cosA, _SunGlowPower);
                // 수평선을 따라 퍼지는 넓은 띠 형태의 노을빛
                float band = pow(cosA, 8) * exp(-abs(y) * 14);
                col += _SunGlowColor.rgb * (glow * 1.5 + band * 0.45);
                float disc = smoothstep(1 - _SunSize, 1 - _SunSize * 0.5, cosA);
                col = lerp(col, _SunColor.rgb, disc);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}

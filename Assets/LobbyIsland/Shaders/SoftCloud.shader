// 뭉게구름: 반 램버트 + 높이 그라데이션(아래 라벤더, 위 분홍빛 흰색) + 태양 쪽 림라이트
Shader "LobbyIsland/SoftCloud"
{
    Properties
    {
        [HDR] _LitColor ("Lit", Color) = (1.15, 0.95, 1.0, 1)
        [HDR] _ShadeColor ("Shade", Color) = (0.72, 0.62, 0.9, 1)
        [HDR] _RimColor ("Rim", Color) = (1.6, 0.95, 0.8, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _HeightMin ("Height Min", Float) = -40
        _HeightMax ("Height Max", Float) = -20
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _LitColor, _ShadeColor, _RimColor;
                float _RimPower, _HeightMin, _HeightMax;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float fog : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight();
                float wrap = saturate(dot(n, light.direction) * 0.5 + 0.5);
                float h = saturate((i.positionWS.y - _HeightMin) / (_HeightMax - _HeightMin));
                float3 col = lerp(_ShadeColor.rgb, _LitColor.rgb, saturate(wrap * 0.7 + h * 0.5));
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float rim = pow(1 - saturate(dot(n, v)), _RimPower);
                float towardSun = saturate(dot(-v, light.direction) * 0.5 + 0.5);
                col += _RimColor.rgb * rim * towardSun;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}

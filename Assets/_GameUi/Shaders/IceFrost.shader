Shader "PennyBall/IceFrost"
{
    Properties
    {
        _IceTex ("Ice Overlay", 2D) = "white" {}
        _FrostColor ("Frost Color", Color) = (0.72, 0.9, 1, 0.62)
        _RimColor ("Rim Color", Color) = (0.92, 0.98, 1, 0.95)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.4
        _ShimmerSpeed ("Shimmer Speed", Float) = 0.65
        _ShimmerScale ("Shimmer Scale", Float) = 3.5
        _CrackIntensity ("Crack Intensity", Range(0, 1)) = 0.35
        _Alpha ("Alpha", Range(0, 1)) = 0.78
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_IceTex);
            SAMPLER(sampler_IceTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _IceTex_ST;
                half4 _FrostColor;
                half4 _RimColor;
                half _RimPower;
                half _ShimmerSpeed;
                half _ShimmerScale;
                half _CrackIntensity;
                half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _IceTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 shimmerUv = input.uv * _ShimmerScale + float2(_Time.y * _ShimmerSpeed, -_Time.y * _ShimmerSpeed * 0.7);
                half4 iceSample = SAMPLE_TEXTURE2D(_IceTex, sampler_IceTex, shimmerUv);

                float2 crackUv = input.positionWS.xz * 5.5 + float2(_Time.y * 0.08, 0);
                float crack = Hash21(floor(crackUv)) * _CrackIntensity;

                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _RimPower);

                half procedural = iceSample.r * 0.55 + iceSample.a * 0.45;
                procedural = saturate(procedural + crack + fresnel * 0.18);

                half3 color = lerp(_FrostColor.rgb, _RimColor.rgb, fresnel);
                half alpha = saturate(_FrostColor.a * procedural + fresnel * _RimColor.a) * _Alpha;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

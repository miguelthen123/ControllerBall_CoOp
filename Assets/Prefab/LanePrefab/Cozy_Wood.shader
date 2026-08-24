Shader "Custom/VR_CozyStylizedWood"
{
    Properties
    {
        _BaseWoodColor ("Base Wood Color", Color) = (0.85, 0.62, 0.42, 1)    // Warm Light Oak
        _GrainColor ("Grain / Line Color", Color) = (0.65, 0.42, 0.26, 1)     // Soft Warm Brown
        _BevelColor ("Edge Bevel Shadow", Color) = (0.45, 0.28, 0.18, 1)      // Deep Shadow
        _PlankScale ("Plank Width Scale", Float) = 2.0
        _GrainFrequency ("Grain Detail Scale", Float) = 15.0
        _BevelWidth ("Edge Bevel Width", Range(0.01, 0.2)) = 0.05
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.4
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry" 
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 worldPos     : TEXCOORD0;
                float3 worldNormal  : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseWoodColor;
                half4 _GrainColor;
                half4 _BevelColor;
                float _PlankScale;
                float _GrainFrequency;
                float _BevelWidth;
                half _Smoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 N = normalize(input.worldNormal);

                // 1. Procedural Plank Lines & Soft Grain Waves
                float plankCoord = input.worldPos.y * _PlankScale;
                float plankPattern = abs(frac(plankCoord) - 0.5) * 2.0;
                
                // Subtle sine wave grain detail along the length
                float grainWave = sin(input.worldPos.x * _GrainFrequency + sin(input.worldPos.z * 5.0)) * 0.5 + 0.5;
                half4 woodPattern = lerp(_GrainColor, _BaseWoodColor, saturate(plankPattern + grainWave * 0.3));

                // 2. Beveled Edge Shadowing using UV borders
                float2 edgeDist = min(input.uv, 1.0 - input.uv);
                float borderFactor = smoothstep(0.0, _BevelWidth, min(edgeDist.x, edgeDist.y));
                half4 finalAlbedo = lerp(_BevelColor, woodPattern, borderFactor);

                // 3. URP Lighting & Shadows Calculation
                float4 shadowCoord = TransformWorldToShadowCoord(input.worldPos);
                Light mainLight = GetMainLight(shadowCoord);

                half NdotL = saturate(dot(N, mainLight.direction));
                half shadowFactor = mainLight.shadowAttenuation;
                half3 ambient = SampleSH(N);
                half3 lighting = (mainLight.color * NdotL * shadowFactor) + ambient;

                return half4(finalAlbedo.rgb * lighting, finalAlbedo.a);
            }
            ENDHLSL
        }

        // Shadow Caster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float3 worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.positionHCS = TransformWorldToHClip(ApplyShadowBias(worldPos, worldNormal, _MainLightPosition.xyz));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }
    }
}
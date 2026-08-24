Shader "Custom/VR_CozyClayCapsule"
{
    Properties
    {
        _MainColor ("Clay Color", Color) = (0.35, 0.65, 0.72, 1)        // Muted Soft Sage / Turquoise
        _RimColor ("Soft Rim Glow", Color) = (0.98, 0.90, 0.82, 1)        // Warm Peach Rim Highlight
        _Smoothness ("Glossiness", Range(0.0, 1.0)) = 0.6               // Soft Clay Polish
        _RimPower ("Rim Width", Range(0.5, 8.0)) = 3.0
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 worldPos     : TEXCOORD0;
                float3 worldNormal  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _MainColor;
                half4 _RimColor;
                half _Smoothness;
                float _RimPower;
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
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 N = normalize(input.worldNormal);
                float3 V = GetWorldSpaceNormalizeViewDir(input.worldPos);

                // 1. Fresnel Rim Calculation (soft edge light)
                float NdotV = saturate(dot(N, V));
                float rim = pow(1.0 - NdotV, _RimPower);
                half3 rimGlow = _RimColor.rgb * rim;

                // 2. Light & Shadow Sampling
                float4 shadowCoord = TransformWorldToShadowCoord(input.worldPos);
                Light mainLight = GetMainLight(shadowCoord);

                half NdotL = saturate(dot(N, mainLight.direction));
                half shadowFactor = mainLight.shadowAttenuation;
                half3 ambient = SampleSH(N);

                // 3. Smooth Diffuse + Soft Rim Integration
                half3 diffuseLighting = (mainLight.color * NdotL * shadowFactor) + ambient;
                half3 finalColor = (_MainColor.rgb * diffuseLighting) + (rimGlow * shadowFactor);

                return half4(finalColor, _MainColor.a);
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
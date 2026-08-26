Shader "Custom/VR_CozyTopOnlyTiles"
{
    Properties
    {
        _TileColor ("Tile Surface Color", Color) = (0.99, 0.96, 0.91, 1)      // Warm Pastel Cream
        _GroutColor ("Grout / Edge Line Color", Color) = (0.92, 0.81, 0.72, 1)   // Soft Warm Sand
        _SideColor ("Side Wall Color", Color) = (0.94, 0.88, 0.81, 1)          // Soft Matte Beige
        _TileScale ("Tile Scale (Tiling Rate)", Float) = 1.0
        _BevelWidth ("Bevel / Edge Width", Range(0.01, 0.2)) = 0.05
        _PillowBulge ("Center Glow / Cushion", Range(0.0, 0.5)) = 0.15
        _TopThreshold ("Top Surface Threshold", Range(0.1, 0.99)) = 0.7
        _TopBlendSoftness ("Top/Side Transition", Range(0.01, 0.5)) = 0.1
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry" 
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Shadow Pragma Compilation Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            // VR Multi-Pass / Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0; // <--- ADDED: Mesh UVs
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 worldPos     : TEXCOORD0;
                float3 worldNormal  : TEXCOORD1;
                float2 uv           : TEXCOORD2; // <--- ADDED: Passed UVs
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TileColor;
                half4 _GroutColor;
                half4 _SideColor;
                float _TileScale;
                float _BevelWidth;
                float _PillowBulge;
                float _TopThreshold;
                float _TopBlendSoftness;
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
                output.uv = input.uv; // <--- ADDED: Pass UV to fragment
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 N = normalize(input.worldNormal);

                // 1. Evaluate top tile grid using local mesh UVs instead of world XZ
                float2 tileUV = frac(input.uv * _TileScale);
                float2 distFromEdge = min(tileUV, 1.0 - tileUV);
                float edgeDistance = min(distFromEdge.x, distFromEdge.y);

                float bevelFactor = smoothstep(0.0, _BevelWidth, edgeDistance);
                float centerDistance = length(tileUV - 0.5);
                float pillowFactor = 1.0 - saturate(centerDistance * 1.414);
                
                half4 finalTileColor = lerp(_TileColor * (1.0 - _PillowBulge), _TileColor, pillowFactor);
                half4 topPattern = lerp(_GroutColor, finalTileColor, bevelFactor);

                // 2. Filter by upward world normal (Y component)
                float isTopSurface = smoothstep(_TopThreshold - _TopBlendSoftness, _TopThreshold + _TopBlendSoftness, N.y);

                // 3. Base color selection
                half4 baseColor = lerp(_SideColor, topPattern, isTopSurface);

                // 4. Sample URP Main Light & Shadows
                float4 shadowCoord = TransformWorldToShadowCoord(input.worldPos);
                Light mainLight = GetMainLight(shadowCoord);

                // Calculate simple NdotL light attenuation + shadow attenuation
                half NdotL = saturate(dot(N, mainLight.direction));
                half shadowFactor = mainLight.shadowAttenuation;
                
                // Soft ambient factor
                half3 ambient = SampleSH(N);
                half3 lighting = (mainLight.color * NdotL * shadowFactor) + ambient;

                return half4(baseColor.rgb * lighting, baseColor.a);
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
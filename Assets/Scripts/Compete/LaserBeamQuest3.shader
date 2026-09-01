Shader "Custom/URP/LaserBeamQuest3_VR"
{
    Properties
    {
        [HDR] _CoreColor ("Core Color (Inner)", Color) = (5.0, 5.0, 5.0, 1.0)
        [HDR] _GlowColor ("Glow Color (Outer)", Color) = (3.0, 0.1, 0.1, 1.0)
        
        [Header(Glow Control)]
        _CoreThickness ("Core Thickness", Range(0.01, 1.0)) = 0.25
        _GlowFalloff ("Glow Softness Falloff", Range(1.0, 8.0)) = 3.0
        
        [Header(Texture Energy Flow)]
        _MainTex ("Energy Texture (Grayscale)", 2D) = "white" {}
        _ScrollSpeed ("Scroll Speed X", Float) = 4.0
        _NoiseIntensity ("Texture Energy Intensity", Range(0.0, 1.0)) = 0.3
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UnlitMobileLaserVR"
            Blend One One       // Additive blending (Fastest on mobile tile-based GPUs)
            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                half4 color         : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Required for Single Pass Instanced VR
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                half4 color         : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Required for Single Pass Instanced VR
                UNITY_VERTEX_OUTPUT_STEREO     // Required for Single Pass Instanced VR
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _GlowColor;
                float4 _MainTex_ST;
                half _CoreThickness;
                half _GlowFalloff;
                half _ScrollSpeed;
                half _NoiseIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                // --- VR Stereo Rendering Setup ---
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                
                // Apply UV Tiling/Offset settings
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // --- VR Stereo Rendering Fragment Setup ---
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;

                // --- 1. Fast Distance Falloff (Center to Edge) ---
                half distFromCenter = abs(uv.y - 0.5h) * 2.0h;

                // --- 2. Low-Cost Texture-Based Scrolling Energy ---
                float2 scrolledUV = float2(uv.x - (_Time.y * _ScrollSpeed), uv.y);
                half energyTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrolledUV).r;

                distFromCenter = saturate(distFromCenter + (energyTex - 0.5h) * _NoiseIntensity);

                // --- 3. Compute Core and Glow Masks ---
                half coreMask = saturate((1.0h - distFromCenter) / _CoreThickness);
                coreMask = smoothstep(0.0h, 1.0h, coreMask);

                half glowMask = pow(saturate(1.0h - distFromCenter), _GlowFalloff);

                // --- 4. Color Assembly ---
                half3 finalColor = (_GlowColor.rgb * glowMask) + (_CoreColor.rgb * coreMask);

                finalColor *= lerp(1.0h, energyTex, _NoiseIntensity);
                finalColor *= input.color.rgb;

                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
    }
}
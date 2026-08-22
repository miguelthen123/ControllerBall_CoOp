Shader "Custom/StencilWriter"
{
    Properties
    {
        [IntRange] _StencilID ("Stencil Ref ID", Range(0, 255)) = 1
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "Queue"="Geometry-1" 
            "RenderPipeline"="UniversalPipeline" 
        }

        Pass
        {
            Name "StencilWriterPass"
            
            ColorMask 0        // Makes the sphere completely invisible
            ZWrite Off         // Doesn't block depth
            Cull Off           // Render both inner and outer faces

            Stencil
            {
                Ref [_StencilID]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // 1. Enable Single Pass Instanced VR Support
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Declare instance ID for VR
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Pass instance ID to fragment
                UNITY_VERTEX_OUTPUT_STEREO     // Pass stereo eye index
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 2. Setup VR Instance & Stereo Eye Data
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 3. Setup Stereo Eye Data in Fragment Shader
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
Shader "Custom/SpherePathway"
{
    Properties
    {
        _GrassColor ("Grass / Sphere Color", Color) = (0.18, 0.35, 0.11, 1)
        _PathColor ("Pathway Color", Color) = (0.82, 0.70, 0.55, 1)
        _BorderColor ("Border Color", Color) = (0.36, 0.25, 0.20, 1)
        
        _PathWidth ("Pathway Width", Range(0.01, 1.0)) = 0.2
        _BorderThickness ("Border Thickness", Range(0.0, 0.2)) = 0.03
        _EdgeSmoothness ("Edge Anti-Aliasing", Range(0.001, 0.05)) = 0.005

        [Header(Pathway Orientation)]
        _RotationX ("Pitch (Rotate Around X)", Range(0, 360)) = 0
        _RotationY ("Yaw (Rotate Around Y)", Range(0, 360)) = 0
        _RotationZ ("Roll (Rotate Around Z)", Range(0, 360)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 localPos : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO // Correct VR Macro for struct definition
            };

            fixed4 _GrassColor;
            fixed4 _PathColor;
            fixed4 _BorderColor;
            
            float _PathWidth;
            float _BorderThickness;
            float _EdgeSmoothness;

            float _RotationX;
            float _RotationY;
            float _RotationZ;

            float3 RotatePos(float3 pos, float3 angles)
            {
                float3 rad = radians(angles);
                
                float c = cos(rad.x); float s = sin(rad.x);
                float3x3 rx = float3x3(1, 0, 0,  0, c, -s,  0, s, c);

                c = cos(rad.y); s = sin(rad.y);
                float3x3 ry = float3x3(c, 0, s,  0, 1, 0, -s, 0, c);

                c = cos(rad.z); s = sin(rad.z);
                float3x3 rz = float3x3(c, -s, 0,  s, c, 0,  0, 0, 1);

                return mul(rz, mul(ry, mul(rx, pos)));
            }

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 rotatedPos = RotatePos(i.localPos, float3(_RotationX, _RotationY, _RotationZ));
                float3 normPos = normalize(rotatedPos);

                float distFromEquator = abs(normPos.y);

                float halfWidth = _PathWidth * 0.5;
                float inPath = 1.0 - smoothstep(halfWidth - _EdgeSmoothness, halfWidth + _EdgeSmoothness, distFromEquator);

                float halfWidthWithBorder = halfWidth + _BorderThickness;
                float inBorder = 1.0 - smoothstep(halfWidthWithBorder - _EdgeSmoothness, halfWidthWithBorder + _EdgeSmoothness, distFromEquator);

                fixed4 finalColor = lerp(_GrassColor, _BorderColor, inBorder);
                finalColor = lerp(finalColor, _PathColor, inPath);

                return finalColor;
            }
            ENDCG
        }
    }
}
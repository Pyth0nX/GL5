Shader "Custom/InteractableOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.84, 0, 1)
        _OutlineWidth("Outline Width", Range(0, 10)) = 2.5
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4 // 4 = LessEqual, 8 = Always
        [Toggle] _ExtrusionMode("World Space Extrusion", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZTest [_ZTest]
            ZWrite Off
            Offset 1, 1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Explicit 16-byte alignment strictly required by Metal / MSL (macOS & Apple Silicon)
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _ExtrusionMode;
                float2 _MetalBufferPadding; // Aligns struct to 32 bytes (multiple of 16)
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                float4 posCS;

                if (_ExtrusionMode > 0.5)
                {
                    // World-space extrusion
                    float3 worldPos = vertexInput.positionWS + normalInput.normalWS * (_OutlineWidth * 0.005);
                    posCS = TransformWorldToHClip(worldPos);
                }
                else
                {
                    // Screen-space extrusion (uniform pixel width at any distance, Mac & Retina compatible)
                    posCS = vertexInput.positionCS;
                    float3 normalVS = TransformWorldToViewNormal(normalInput.normalWS, true);
                    float2 normalVS2D = normalVS.xy;
                    
                    // Metal SIMD safe normalization (prevents division by zero / NaN in vector units on Apple Silicon)
                    float sqrLen = dot(normalVS2D, normalVS2D);
                    float2 offset = (sqrLen > 1e-6) ? (normalVS2D * rsqrt(max(sqrLen, 1e-8))) : float2(0.0, 0.0);

                    // Aspect ratio calculation compatible with Windows, Mac (Metal / Retina), and Linux
                    float aspect = (_ScreenParams.y > 0.0) ? (_ScreenParams.x / _ScreenParams.y) : 1.0;
                    offset.x /= aspect;

                    posCS.xy += offset * (_OutlineWidth * 0.003) * posCS.w;
                }

                output.positionCS = posCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(_OutlineColor);
            }
            ENDHLSL
        }
    }

    // Fallback for Built-in Pipeline
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Transparent+100"
        }

        Pass
        {
            Name "Outline"
            Cull Front
            ZTest [_ZTest]
            ZWrite Off
            Offset 1, 1
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _ExtrusionMode;

            v2f vert(appdata v)
            {
                v2f o;
                if (_ExtrusionMode > 0.5)
                {
                    float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz + UnityObjectToWorldNormal(v.normal) * (_OutlineWidth * 0.005);
                    o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                }
                else
                {
                    float4 pos = UnityObjectToClipPos(v.vertex);
                    float3 norm = UnityObjectToViewPos(v.vertex + v.normal) - UnityObjectToViewPos(v.vertex);
                    float2 normalVS2D = norm.xy;
                    float sqrLen = dot(normalVS2D, normalVS2D);
                    float2 offset = (sqrLen > 1e-6) ? (normalVS2D * rsqrt(max(sqrLen, 1e-8))) : float2(0.0, 0.0);
                    float aspect = (_ScreenParams.y > 0.0) ? (_ScreenParams.x / _ScreenParams.y) : 1.0;
                    offset.x /= aspect;
                    pos.xy += offset * (_OutlineWidth * 0.003) * pos.w;
                    o.pos = pos;
                }
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
    FallBack Off
}

Shader "ML-Agents/GridPattern"
{
    Properties
    {
        _LineColor ("Line Color", Color) = (1,1,1,1)
        _CellColor ("Cell Color", Color) = (0,0,0,0)
        [PerRendererData] _MainTex ("Albedo (RGB)", 2D) = "white" {}
        [IntRange] _GridSize ("Grid Size", Range(1,100)) = 10
        _LineSize ("Line Size", Range(0,1)) = 0.15
        [IntRange] _DrawU ("Draw U Toggle ( 0 = False , 1 = True )", Range(0,1)) = 1
        [IntRange] _DrawV ("Draw V Toggle ( 0 = False , 1 = True )", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
            "IgnoreProjector" = "True"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _LineColor;
            float4 _CellColor;
            float  _GridSize;
            float  _LineSize;
            float  _DrawU;
            float  _DrawV;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        // 원본 surf() 의 격자 판정 로직을 그대로 옮긴 함수.
        // rgb = 색상 * brightness, a = brightness (0 이면 클립 대상)
        half4 SampleGrid(float2 uv)
        {
            float gsize = floor(_GridSize);
            gsize += _LineSize;

            float4 color = _CellColor;
            float brightness = _CellColor.w;

            if (round(_DrawU) == 1.0)
            {
                if (frac(uv.x * gsize) <= _LineSize)
                {
                    brightness = _LineColor.w;
                    color = _LineColor;
                }
            }
            if (round(_DrawV) == 1.0)
            {
                if (frac(uv.y * gsize) <= _LineSize)
                {
                    brightness = _LineColor.w;
                    color = _LineColor;
                }
            }

            return half4(color.rgb * brightness, brightness);
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
            float  fogCoord   : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        ENDHLSL

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogCoord   = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 grid = SampleGrid(input.uv);

                // 원본의 alpha cutout: brightness 가 0 이면 픽셀 제거
                clip(grid.a - 0.001);

                half3 col = MixFog(grid.rgb, input.fogCoord);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            Varyings DepthVert (Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 DepthFrag (Varyings input) : SV_Target
            {
                clip(SampleGrid(input.uv).a - 0.001);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            Varyings ShadowVert (Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 ShadowFrag (Varyings input) : SV_Target
            {
                clip(SampleGrid(input.uv).a - 0.001);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}

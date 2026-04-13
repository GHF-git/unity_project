Shader "Custom/VideoLumaKey"
{
    Properties
    {
        _MainTex   ("Video Texture", 2D)          = "white" {}
        _Threshold ("Black Threshold", Range(0,1)) = 0.08
        _Softness  ("Edge Softness",   Range(0,1)) = 0.12
    }
    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Threshold;
                float  _Softness;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 col  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Rec.709 luminance — black pixels have luma ≈ 0, coloured pixels > threshold
                float luma = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));

                // Smooth ramp: fully transparent below threshold, fully opaque above threshold+softness
                col.a = smoothstep(_Threshold, _Threshold + _Softness, luma);

                return col * IN.color;
            }
            ENDHLSL
        }
    }
}

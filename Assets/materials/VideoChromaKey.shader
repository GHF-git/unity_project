Shader "Custom/VideoChromaKey"
{
    Properties
    {
        _MainTex    ("Video Texture",    2D)           = "white" {}
        _KeyColor   ("Key Color",        Color)        = (0, 1, 0, 1)
        _Threshold  ("Threshold",        Range(0, 1))  = 0.35
        _Softness   ("Softness",         Range(0, 1))  = 0.15
        _Despill    ("Despill Strength", Range(0, 1))  = 0.5
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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _KeyColor;
                float  _Threshold;
                float  _Softness;
                float  _Despill;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Distance from pixel colour to the key colour in RGB space
                float dist = distance(col.rgb, _KeyColor.rgb);

                // Smooth transparency ramp
                col.a = smoothstep(_Threshold, _Threshold + _Softness, dist);

                // Despill: reduce green channel contamination on semi-transparent edges
                float despillAmount = (1.0 - col.a) * _Despill;
                col.g -= despillAmount * col.g;

                return col;
            }
            ENDHLSL
        }
    }
}

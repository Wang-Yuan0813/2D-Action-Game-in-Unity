Shader "Action2DGame/LaserBeam"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Emission Intensity", Range(0, 8)) = 1
        _FlowStrength ("Energy Flow", Range(0, 1)) = 0.2
        _FlowSpeed ("Flow Speed", Range(0, 30)) = 10
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "LaserUnlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Intensity;
                float _FlowStrength;
                float _FlowSpeed;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float timeOffset = _Time.y * _FlowSpeed;
                float primaryWave = sin(input.uv.x * 72.0 - timeOffset);
                float secondaryWave = sin(input.uv.x * 29.0 + timeOffset * 1.37);
                float energy = saturate(0.68 + primaryWave * 0.2 + secondaryWave * 0.12);
                color.rgb *= _Intensity * lerp(1.0, energy, _FlowStrength);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}

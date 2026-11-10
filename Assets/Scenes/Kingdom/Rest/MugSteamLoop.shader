Shader "DuelHero/Environment/Mug Steam Loop"
{
    Properties
    {
        _MainTex ("Steam Flipbook (8 × 8)", 2D) = "white" {}
        _Tint ("Steam Tint", Color) = (1, 0.94, 0.84, 0.78)
        _LoopSeconds ("Loop Seconds", Range(0.1, 10)) = 2.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                half4 _Tint;
                float _LoopSeconds;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(float3 positionOS : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = uv;
                return output;
            }

            half SampleFrame(float frame, float2 uv)
            {
                float row = floor(frame / 8.0);
                float2 cell = float2(frame - row * 8.0, 7.0 - row);
                float2 inset = _MainTex_TexelSize.xy * 4.0;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, (cell + lerp(inset, 1.0 - inset, uv)) / 8.0).a;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float phase = frac(_Time.y / max(_LoopSeconds, 0.1)) * 64.0;
                float frame = floor(phase);
                float nextFrame = frame + 1.0 - 64.0 * step(63.0, frame);
                half steam = lerp(SampleFrame(frame, input.uv), SampleFrame(nextFrame, input.uv), frac(phase));
                half fade = 1.0 - smoothstep(0.7, 1.0, input.uv.y);
                return half4(_Tint.rgb, steam * _Tint.a * fade);
            }
            ENDHLSL
        }
    }
}

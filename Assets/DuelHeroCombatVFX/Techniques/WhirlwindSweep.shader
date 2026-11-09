Shader "DuelHero/VFX/Whirlwind Sweep"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha One
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float3 ring : TEXCOORD0; // Angle, radius across the band, particle AgePercent.
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 ring : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.ring = input.ring;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float angle = input.ring.x;
                float radius = input.ring.y;
                float age = input.ring.z;
                float head = age / 0.72;
                float distanceBehind = head - angle;
                float feather = max(fwidth(angle), 0.008);
                float reveal = smoothstep(-feather, feather, distanceBehind);
                float fade = 1.0 - smoothstep(0.79, 1.0, age);
                float leadingEdge = 1.0 - smoothstep(0.0, 0.16, distanceBehind);

                float center = 0.55 + 0.15 * leadingEdge;
                float width = 0.19 + 0.19 * leadingEdge;
                float body = 1.0 - smoothstep(0.0, width, abs(radius - center));
                float core = 1.0 - smoothstep(0.0, 0.075, abs(radius - center));
                float innerEdge = 1.0 - smoothstep(0.0, 0.08, abs(radius - 0.26));
                half3 color = input.color.rgb *
                    (half3(0.34, 0.69, 1.0) + half3(0.75, 0.66, 0.45) * core);
                float alpha = (body * (0.55 + 0.45 * leadingEdge) + innerEdge * 0.24) *
                    reveal * fade * input.color.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

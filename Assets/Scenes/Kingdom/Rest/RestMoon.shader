Shader "DuelHero/Environment/Rest Moon"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 sphere : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.sphere = positionOS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 surface = normalize(input.sphere);
                float3 normal = TransformObjectToWorldNormal(surface);
                float limb = saturate(dot(normal, GetWorldSpaceNormalizeViewDir(input.positionWS)));
                float darkPatch = smoothstep(0.15, 0.7,
                    sin(surface.x * 11.0 + sin(surface.y * 7.0)) * sin(surface.y * 9.0 + surface.z * 4.0));
                float finePatch = sin(surface.x * 43.0 + surface.z * 16.0) * sin(surface.y * 35.0);
                half3 color = lerp(half3(0.26, 0.48, 0.75), half3(0.62, 0.82, 1.0), 1.0 - darkPatch * 0.58);
                return half4(color * (0.74 + limb * 0.26 + finePatch * 0.025), 1);
            }
            ENDHLSL
        }
    }
}

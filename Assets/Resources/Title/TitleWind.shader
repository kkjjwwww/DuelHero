Shader "DuelHero/UI/TitleWind"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Mode ("Wind target", Float) = 0
        _Phase ("Phase", Float) = 0
        _Clock ("Time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Vertex { float4 position : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Fragment { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            float _Mode, _Phase, _Clock;

            float Foliage(float2 uv)
            {
                fixed4 pixel = tex2D(_MainTex, uv);
                return smoothstep(0.025, 0.09, min(pixel.r, pixel.g) - pixel.b) * pixel.a;
            }

            Fragment vert(Vertex input)
            {
                Fragment output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(Fragment input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Clock * 2.1 + _Phase;
                if (_Mode < 0.5)
                {
                    float loose = 1.0 - smoothstep(0.12, 0.85, uv.y);
                    uv.x += sin(time + uv.y * 7.0) * loose * loose * 0.032;
                }
                else if (_Mode < 1.5)
                {
                    // The cape extends left of the shoulder, well clear of the sword.
                    float edge = lerp(0.32, 0.43, smoothstep(0.28, 0.72, uv.y));
                    float loose = (1.0 - smoothstep(0.10, edge, uv.x)) * smoothstep(0.24, 0.32, uv.y);
                    uv.y += sin(time + uv.x * 11.0) * loose * 0.012;
                }
                else
                {
                    // Include the dark outline and nearby transparent pixels in the wind mask.
                    float mask = Foliage(uv);
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * 0.78539816;
                        float2 direction = float2(cos(angle), sin(angle));
                        mask = max(mask, Foliage(uv + direction * 0.005));
                        mask = max(mask, Foliage(uv + direction * 0.010) * 0.5);
                    }
                    uv.x += sin(time + uv.x * 32.0) * 0.00125 * mask;
                }
                return tex2D(_MainTex, uv) * input.color;
            }
            ENDCG
        }
    }
}

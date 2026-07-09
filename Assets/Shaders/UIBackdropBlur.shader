Shader "UI/BulletHell/BackdropBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Radius ("Blur Radius", Range(0, 12)) = 4
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _Radius;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 stepUV = _MainTex_TexelSize.xy * _Radius;
                fixed4 color = 0;

                // Full 5x5 separable Gaussian kernel (1,4,6,4,1)^2 / 256.
                // The previous sparse nine-tap pattern created visible doubled
                // edges and made the screenshot look low-resolution.
                [unroll]
                for (int y = -2; y <= 2; y++)
                {
                    float wy = y == 0 ? 6.0 : (abs(y) == 1 ? 4.0 : 1.0);
                    [unroll]
                    for (int x = -2; x <= 2; x++)
                    {
                        float wx = x == 0 ? 6.0 : (abs(x) == 1 ? 4.0 : 1.0);
                        float2 uv = saturate(input.uv + float2(x, y) * stepUV);
                        color += tex2D(_MainTex, uv) * (wx * wy);
                    }
                }

                return (color / 256.0) * input.color;
            }
            ENDCG
        }
    }
}

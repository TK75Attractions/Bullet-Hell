// UI の RawImage 用。貼っているテクスチャの色数を落とし、必要なら 4x4 の順序ディザを
// 掛ける「ドット絵風」表示。TitleRoomController / CityMapController が低解像度の
// RenderTexture を Point フィルタで拡大したうえで、さらに階調を落としたいときだけ使う。
//
// _PixelatePalette が 0(または 1)のときは何もしない = 素通し。
// ディザのセルは画面画素ではなくテクスチャの texel 単位で数えるので、拡大後も
// 1 ドット単位の市松になる(画面解像度が変わっても模様の細かさが変わらない)。
Shader "BulletHell/UI/PixelQuantize"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PixelatePalette ("Levels per channel (0 = off)", Float) = 0
        _PixelateDither ("Ordered dither amount", Range(0,1)) = 1
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UIPixelQuantize"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _PixelatePalette;
            float _PixelateDither;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            // Bayer 4x4(0..15)。値が小さいほど暗い側へ寄せる。
            static const float Bayer[16] =
            {
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0
            };

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;

                float levels = _PixelatePalette;
                if (levels > 1.5)
                {
                    // texel 座標(= 低解像度 RT の 1 ドット)でディザのセルを決める。
                    float2 texel = floor(i.texcoord * _MainTex_TexelSize.zw);
                    int cell = (int)fmod(texel.x, 4.0) + 4 * (int)fmod(texel.y, 4.0);
                    float d = (Bayer[cell] / 16.0 - 0.5) * _PixelateDither;
                    float steps = levels - 1.0;
                    // 階調はガンマ空間で刻む。RT はリニア(HDR)なので、そのまま等間隔に
                    // 丸めると暗部が全部 0 へ潰れる(夜の室内が真っ黒になる)。
                    float3 g = pow(saturate(col.rgb), 1.0 / 2.2);
                    g = floor(g * steps + 0.5 + d / steps) / steps;
                    col.rgb = pow(saturate(g), 2.2);
                }
                return col;
            }
        ENDCG
        }
    }
    FallBack Off
}

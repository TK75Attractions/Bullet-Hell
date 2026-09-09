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
        // 色の調整(0 = 素通し)。ステージ選択の街だけが使う。
        _GradeEnabled ("Color grade on/off", Float) = 0
        _Saturation ("Saturation", Range(0,2)) = 1
        _Contrast ("Contrast", Range(0,2)) = 1
        _ContrastPivot ("Contrast pivot", Range(0,1)) = 0.5
        _BlackLift ("Black lift", Range(0,0.5)) = 0
        _WarmKeep ("Keep warm hues saturated", Range(0,1)) = 1
        _TintColor ("District tint (multiplier)", Color) = (1,1,1,1)
        _TintAmount ("District tint amount", Range(0,1)) = 0
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
            float _GradeEnabled;
            float _Saturation;
            float _Contrast;
            float _ContrastPivot;
            float _BlackLift;
            float _WarmKeep;
            float4 _TintColor;
            float _TintAmount;

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
                bool quantize = levels > 1.5;
                bool grade = _GradeEnabled > 0.5;
                if (!quantize && !grade) return col;

                // 階調も色の調整もガンマ空間で行う。RT はリニア(HDR)なので、そのまま
                // 等間隔に丸めると暗部が全部 0 へ潰れる(夜の街が真っ黒になる)。
                float3 g = pow(saturate(col.rgb), 1.0 / 2.2);

                if (grade)
                {
                    // 彩度を落とす。ただし窓灯り・ランタンの橙(r が b より強い画素)は
                    // そのまま残す(指示「窓灯りとランタンの橙だけは残す」)。
                    float lum = dot(g, float3(0.299, 0.587, 0.114));
                    float warm = saturate((g.r - g.b) * 2.5) * _WarmKeep;
                    float sat = lerp(_Saturation, 1.0, warm);
                    g = lerp(lum.xxx, g, sat);

                    // コントラストを落とす。軸を中間より下(既定 0.38)に置くので、
                    // ハイライトの下がり幅の方が大きく、夜の暗さが残る。
                    g = (g - _ContrastPivot) * _Contrast + _ContrastPivot;
                    g = g * (1.0 - _BlackLift) + _BlackLift;
                    g = saturate(g);

                    // 区画の基調色を薄く被せる(_TintColor は 1 を中立とする倍率)。
                    g = saturate(lerp(g, g * _TintColor.rgb, _TintAmount));
                }

                if (quantize)
                {
                    // texel 座標(= 低解像度 RT の 1 ドット)でディザのセルを決める。
                    float2 texel = floor(i.texcoord * _MainTex_TexelSize.zw);
                    int cell = (int)fmod(texel.x, 4.0) + 4 * (int)fmod(texel.y, 4.0);
                    float d = (Bayer[cell] / 16.0 - 0.5) * _PixelateDither;
                    float steps = levels - 1.0;
                    g = floor(g * steps + 0.5 + d / steps) / steps;
                }

                col.rgb = pow(saturate(g), 2.2);
                return col;
            }
        ENDCG
        }
    }
    FallBack Off
}

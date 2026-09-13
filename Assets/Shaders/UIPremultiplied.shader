// UI の RawImage 用。すでに「アルファを掛けた色」で描かれた RenderTexture を、
// そのまま下の絵の上へ重ねる(プリマルチプライド合成)。
//
// タイトルの立ち絵は専用カメラで 1920x1080 の透明な RT へ描いている。カメラ側の
// アルファブレンドを One / OneMinusSrcAlpha にしてあるので RT の中身は
// (色 x アルファ, アルファ) になっており、既定の UI シェーダ(SrcAlpha 前提)で
// 重ねるとアルファが二重に掛かって薄くなる。この 1 パスはそれを正しく合成する。
Shader "BulletHell/UI/Premultiplied"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
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
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UIPremultiplied"
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
            fixed4 _Color;

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

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord);
                // 中身はプリマルチプライド。RawImage の色で薄めるときは色とアルファの
                // 両方へ同じ係数を掛ける(プリマルチプライドのまま保つ)。
                col.rgb *= i.color.rgb * i.color.a;
                col.a *= i.color.a;
                return col;
            }
        ENDCG
        }
    }
    FallBack Off
}

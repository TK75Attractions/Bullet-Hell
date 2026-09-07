// 石工の背景 CG の中に置くボスの代理スプライト。
//
// ボスは 2D の SpriteRenderer のままだと「CG の板に貼りついた絵」になるため、CG の 3D 空間側
// （平面 z = StoneCgController.bossDepth）へ逆投影して置き、CGCamera が RenderTexture へ描く。
// 画面上の位置と大きさは 2D の論理座標のときと同じになる（逆投影＋(36+z)/36 倍のスケール）。
//
// アルファは「表示板でボスとして扱う量」として使う。CG 本体（StoneCG/Flat）は 0 を書くので、
// ボスが乗った画素だけ alpha>0 になり、StoneCgDisplay 側で露出・中央減光ではなく
// ボス用の明度（_BossBrightness）を掛けられる。アルファ側だけプリマルチプライで合成して
// （Blend の第 2 組 One OneMinusSrcAlpha）、被覆率がそのまま alpha に入るようにする。
Shader "StoneCG/BossSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "StoneCgBossSprite"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _Color;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 col = tex * i.color;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

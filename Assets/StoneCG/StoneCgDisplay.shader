// 背景 CG の表示板。CGCamera が描いた RenderTexture を、フィールド 32x18 を覆う
// 不透明 Quad に貼る。不透明キュー(Geometry)なので、Transparent の弾・ボスより必ず奥になる。
// _Exposure で全体の明るさ、_CenterDarken でフィールド中央(弾が飛ぶ帯)を落として弾の視認性を上げる。
Shader "StoneCG/Display"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("CG RenderTexture", 2D) = "black" {}
        _Exposure ("Exposure", Range(0,2)) = 0.45
        _CenterDarken ("Center Darken", Range(0,1)) = 0.55
        _Tint ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "StoneCgDisplay"
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float _Exposure;
            float _CenterDarken;
            float4 _Tint;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                col *= _Exposure * _Tint.rgb;
                // フィールド中央(論理 x 4..28 / y 2..16)の楕円マスクで中央だけさらに暗くする。
                float2 f = float2(i.uv.x * 32.0, i.uv.y * 18.0);
                float2 d = (f - float2(16.0, 9.0)) / float2(12.0, 7.0);
                float m = 1.0 - saturate(dot(d, d));
                m = m * m * (3.0 - 2.0 * m); // smoothstep 状の減衰
                col *= 1.0 - _CenterDarken * m;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

// 背景 CG の表示板。CGCamera が描いた RenderTexture を、フィールド 32x18 を覆う
// 不透明 Quad に貼る。不透明キュー(Geometry)なので、Transparent の弾・ボスより必ず奥になる。
// _Exposure で全体の明るさ、_CenterDarken でフィールド中央(弾が飛ぶ帯)を落として弾の視認性を上げる。
Shader "StoneCG/Display"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("CG RenderTexture", 2D) = "black" {}
        // ドット風のとき、ボスだけを画面解像度で描いた RenderTexture（2 台目のカメラ）。
        [NoScaleOffset] _BossTex ("Boss RenderTexture (full res)", 2D) = "black" {}
        _BossSplit ("Boss Layer Split (0=一体 1=分離)", Range(0,1)) = 0
        _Exposure ("Exposure", Range(0,2)) = 0.35
        _CenterDarken ("Center Darken", Range(0,1)) = 0.55
        _Tint ("Tint", Color) = (1,1,1,1)
        _BossBrightness ("Boss Brightness", Range(0,2)) = 0.5
        _Fade ("Intro Fade", Range(0,1)) = 1
        _CgFade ("CG Blackout", Range(0,1)) = 1
        // 第 6 便 (C): ステージごとの色調整。背景 CG の画素にだけ掛ける（ボスは素のまま）。
        _HueShift ("Hue Shift (deg)", Range(-180,180)) = 0
        _Saturation ("Saturation", Range(0,2)) = 1
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        _TintAmount ("Tint Amount", Range(0,1)) = 0
        // 第 6 便 (A): 形態変化の瞬間の控えめなフラッシュ（端と上部だけ明るくする）。
        _Flash ("Phase Flash", Range(0,1)) = 0
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
            TEXTURE2D(_BossTex); SAMPLER(sampler_BossTex);
            float _BossSplit;
            float _Exposure;
            float _CenterDarken;
            float4 _Tint;
            float _BossBrightness;
            float _Fade;
            float _CgFade;
            float _HueShift;
            float _Saturation;
            float4 _TintColor;
            float _TintAmount;
            float _Flash;

            // 色相回転（YIQ 空間の IQ 平面を回す。輝度は保存される）。
            float3 HueRotate(float3 c, float deg)
            {
                float a = radians(deg);
                float s = sin(a), co = cos(a);
                float3 yiq = float3(
                    dot(c, float3(0.299, 0.587, 0.114)),
                    dot(c, float3(0.596, -0.274, -0.322)),
                    dot(c, float3(0.211, -0.523, 0.312)));
                float2 iq = float2(yiq.y * co - yiq.z * s, yiq.y * s + yiq.z * co);
                return float3(
                    yiq.x + dot(iq, float2(0.956, 0.621)),
                    yiq.x + dot(iq, float2(-0.272, -0.647)),
                    yiq.x + dot(iq, float2(-1.106, 1.703)));
            }

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
                float4 src = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                // ボスは別レイヤー・別カメラで画面解像度の _BossTex に描いてある（_BossSplit=1）。
                // 分離していないとき（_BossSplit=0）は今までどおり _MainTex のアルファを使う。
                float4 bsrc = SAMPLE_TEXTURE2D(_BossTex, sampler_BossTex, i.uv);
                float bossA = lerp(src.a, bsrc.a, _BossSplit);
                float3 bossRgb = lerp(src.rgb, bsrc.rgb, _BossSplit);
                // フィールド中央(論理 x 4..28 / y 2..16)の楕円マスクで中央だけさらに暗くする。
                float2 f = float2(i.uv.x * 32.0, i.uv.y * 18.0);
                float2 d = (f - float2(16.0, 9.0)) / float2(12.0, 7.0);
                float m = 1.0 - saturate(dot(d, d));
                m = m * m * (3.0 - 2.0 * m); // smoothstep 状の減衰
                // _CgFade は終端の「背景だけ黒へ」(石工 v34 #22)。ボスには掛からない。
                float cgGain = _Exposure * (1.0 - _CenterDarken * m) * _CgFade;
                // bossA は「ボスとして描かれた被覆率」(CG 本体は 0)。ボスの画素には露出でも
                // 中央減光でもなく _BossBrightness を掛ける = ボスの明度を CG と独立に決める。
                float gain = lerp(cgGain, _BossBrightness, saturate(bossA));

                // 第 6 便 (C): 色相回転 → 彩度 → 色被せ。背景 CG の画素にだけ効かせたいので、
                //   ボスの被覆率 bossA で元の色へ戻す（ボスの見え方は今までどおり）。
                float3 graded = src.rgb;
                if (abs(_HueShift) > 0.001) graded = HueRotate(graded, _HueShift);
                float lum = dot(graded, float3(0.299, 0.587, 0.114));
                graded = lerp(float3(lum, lum, lum), graded, _Saturation);
                graded = lerp(graded, lum * _TintColor.rgb, _TintAmount);
                graded = max(graded, 0.0);
                float3 rgb = lerp(graded, bossRgb, saturate(bossA));

                // _Fade は導入の黒 → 空のフェード(0 で真っ黒)。
                float3 col = rgb * gain * _Tint.rgb * _Fade;
                // 第 6 便 (A): 形態変化のフラッシュ。中央の楕円マスク m の外側（＝画面の端）と
                //   上部だけを明るくするので、弾が飛ぶ中央の明度は上がらない。
                float edge = saturate(1.0 - m);
                float top = saturate((f.y - 12.0) / 6.0);
                col *= 1.0 + _Flash * max(edge, top);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

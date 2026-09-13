// 石工の背景 CG 専用のフラットシェーダ。
// Blender(Eevee)側の見た目を Unity のグローバルなライティング状態に依存せず再現するため、
// URP/Lit ではなく自前の閉じた式で塗る。
//   色 = base_linear * (ambient + sun * saturate(N・L) + core) + emission_linear * groupScale
// base/emission はリニア値をそのまま Vector で渡す(Color 変換の二重適用を避ける)。
// sun/ambient は StoneCgController がグローバル uniform で与える。
//
// core = ゴーレム降臨(72.94s)後に灯るコアの赤い点光源 1 灯。
//   _StoneCgCoreParams.xyz = 位置 / .w = 減衰半径。_StoneCgCoreColor.rgb = 色 * 強さ。
//   グローバルの既定は 0 なので、降臨前と他ステージでは 1 項ぶんの乗算が増えるだけで絵は不変。
// groupScale = 発光の系統別スケール。材質ごとの _EmisGroup(1=ランタン warm / 2=街の灯り
//   city_light / 3=割れ目 crack_glow / 4=空 sky_texture)で 4 本のグローバルから 1 つを選ぶ。
//   _EmisGroup=0(既定)の材質は常に 1 倍＝従来どおり。
Shader "StoneCG/Flat"
{
    Properties
    {
        _BaseLin ("Base (linear)", Vector) = (0,0,0,1)
        _EmisLin ("Emission (linear)", Vector) = (0,0,0,1)
        _EmisGroup ("Emission Group (0=none 1=lantern 2=city 3=crack 4=sky)", Float) = 0
        _Alpha ("Alpha (material)", Range(0,1)) = 1
        _FadeAlpha ("Fade Alpha (per renderer)", Range(0,1)) = 1
        [NoScaleOffset] _EmisTex ("Emission Tex", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "StoneCgFlat"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _BaseLin;
            float4 _EmisLin;
            float _EmisGroup;
            float _Alpha;
            float _FadeAlpha;
            TEXTURE2D(_EmisTex); SAMPLER(sampler_EmisTex);

            float4 _StoneCgSunDir;    // xyz = ライトへ向かう単位ベクトル
            float4 _StoneCgSunColor;  // リニア(強度込み)
            float4 _StoneCgAmbient;   // リニア
            float4 _StoneCgCoreParams; // xyz = コアの位置(ワールド) / w = 減衰半径
            float4 _StoneCgCoreColor;  // リニア(強度込み)。既定 0 = 消灯
            float4 _StoneCgEmisGrp1;   // ランタン
            float4 _StoneCgEmisGrp2;   // 街の灯り
            float4 _StoneCgEmisGrp3;   // 割れ目の発光
            float4 _StoneCgEmisGrp4;   // 空

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            // 4x4 Bayer によるディザ抜き。不透明キュー・ZWrite のまま半透明とクロスフェードを
            // 表現できるので、材質を共有するオブジェクト(鎖・帆など)でも per-renderer で
            // フェードできる(MaterialPropertyBlock の _FadeAlpha)。
            static const float BAYER4[16] = {
                 0.5/16.0,  8.5/16.0,  2.5/16.0, 10.5/16.0,
                12.5/16.0,  4.5/16.0, 14.5/16.0,  6.5/16.0,
                 3.5/16.0, 11.5/16.0,  1.5/16.0,  9.5/16.0,
                15.5/16.0,  7.5/16.0, 13.5/16.0,  5.5/16.0 };
            float DitherThreshold(float2 pixel)
            {
                int2 p = int2(fmod(pixel, 4.0));
                return BAYER4[p.y * 4 + p.x];
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float a = _Alpha * _FadeAlpha;
                clip(a - DitherThreshold(i.positionCS.xy) - 1e-5);

                float3 n = normalize(i.normalWS);
                float ndl = saturate(dot(n, _StoneCgSunDir.xyz));
                float3 tex = SAMPLE_TEXTURE2D(_EmisTex, sampler_EmisTex, i.uv).rgb;

                // コアの赤い点光源(降臨後)。距離減衰は saturate(1 - d/r)^2。
                float3 toCore = _StoneCgCoreParams.xyz - i.positionWS;
                float coreDist = length(toCore);
                float atten = saturate(1.0 - coreDist / max(1e-4, _StoneCgCoreParams.w));
                float coreNdl = saturate(dot(n, toCore / max(1e-4, coreDist)));
                float3 core = _StoneCgCoreColor.rgb * (atten * atten) * coreNdl;

                // 発光の系統別スケール(_EmisGroup で 1 本選ぶ。0 なら等倍)。
                float3 gm = float3(1.0, 1.0, 1.0);
                int grp = (int)(_EmisGroup + 0.5);
                if (grp == 1) gm = _StoneCgEmisGrp1.rgb;
                else if (grp == 2) gm = _StoneCgEmisGrp2.rgb;
                else if (grp == 3) gm = _StoneCgEmisGrp3.rgb;
                else if (grp == 4) gm = _StoneCgEmisGrp4.rgb;

                float3 col = _BaseLin.rgb * (_StoneCgAmbient.rgb + _StoneCgSunColor.rgb * ndl + core)
                           + _EmisLin.rgb * tex * gm;
                // アルファは「表示板でボスとして扱う量」。CG 本体は 0、ボスの代理スプライトだけ
                // 1 を書く（StoneCgDisplay が alpha で露出/中央減光の適用を切り替える）。
                return half4(col, 0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

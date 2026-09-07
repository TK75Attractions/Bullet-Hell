// 石工の背景 CG 専用のフラットシェーダ。
// Blender(Eevee)側の見た目を Unity のグローバルなライティング状態に依存せず再現するため、
// URP/Lit ではなく自前の閉じた式で塗る。
//   色 = base_linear * (ambient + sun * saturate(N・L)) + emission_linear
// base/emission はリニア値をそのまま Vector で渡す(Color 変換の二重適用を避ける)。
// sun/ambient は StoneCgController がグローバル uniform で与える。
Shader "StoneCG/Flat"
{
    Properties
    {
        _BaseLin ("Base (linear)", Vector) = (0,0,0,1)
        _EmisLin ("Emission (linear)", Vector) = (0,0,0,1)
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
            TEXTURE2D(_EmisTex); SAMPLER(sampler_EmisTex);

            float4 _StoneCgSunDir;    // xyz = ライトへ向かう単位ベクトル
            float4 _StoneCgSunColor;  // リニア(強度込み)
            float4 _StoneCgAmbient;   // リニア

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float ndl = saturate(dot(n, _StoneCgSunDir.xyz));
                float3 tex = SAMPLE_TEXTURE2D(_EmisTex, sampler_EmisTex, i.uv).rgb;
                float3 col = _BaseLin.rgb * (_StoneCgAmbient.rgb + _StoneCgSunColor.rgb * ndl)
                           + _EmisLin.rgb * tex;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

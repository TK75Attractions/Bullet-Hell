// StoneCG/Flat の半透明版。霧・月光板・水しぶきのように「本当に薄い」材質だけに使う。
// Flat のディザ抜きは alpha が 0.2 未満だと粒が目立つので、こちらは素直にアルファ合成する。
//
// アルファチャンネルは表示板が「ボスの被覆率」として読むので、RGB だけ合成して
// アルファは書き換えない（Blend ... , Zero One）。ZWrite は切り、キューは Transparent。
Shader "StoneCG/FlatBlend"
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
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "StoneCgFlatBlend"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
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

            float4 _StoneCgSunDir;
            float4 _StoneCgSunColor;
            float4 _StoneCgAmbient;
            float4 _StoneCgCoreParams;
            float4 _StoneCgCoreColor;
            float4 _StoneCgEmisGrp1;
            float4 _StoneCgEmisGrp2;
            float4 _StoneCgEmisGrp3;
            float4 _StoneCgEmisGrp4;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; float3 positionWS : TEXCOORD2; };

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
                float3 n = normalize(i.normalWS);
                float ndl = saturate(dot(n, _StoneCgSunDir.xyz));
                float3 tex = SAMPLE_TEXTURE2D(_EmisTex, sampler_EmisTex, i.uv).rgb;

                float3 toCore = _StoneCgCoreParams.xyz - i.positionWS;
                float coreDist = length(toCore);
                float atten = saturate(1.0 - coreDist / max(1e-4, _StoneCgCoreParams.w));
                float coreNdl = saturate(dot(n, toCore / max(1e-4, coreDist)));
                float3 core = _StoneCgCoreColor.rgb * (atten * atten) * coreNdl;

                float3 gm = float3(1.0, 1.0, 1.0);
                int grp = (int)(_EmisGroup + 0.5);
                if (grp == 1) gm = _StoneCgEmisGrp1.rgb;
                else if (grp == 2) gm = _StoneCgEmisGrp2.rgb;
                else if (grp == 3) gm = _StoneCgEmisGrp3.rgb;
                else if (grp == 4) gm = _StoneCgEmisGrp4.rgb;

                float3 col = _BaseLin.rgb * (_StoneCgAmbient.rgb + _StoneCgSunColor.rgb * ndl + core)
                           + _EmisLin.rgb * tex * gm;
                return half4(col, saturate(_Alpha * _FadeAlpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}

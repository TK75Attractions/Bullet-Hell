Shader "Hidden/BulletHell/ScreenNoiseBlur"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "Screen Noise Blur"

            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _ScreenNoiseBlurParams;
            float4 _ScreenNoiseJitterParams;

            half4 SampleScreen(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy;
                float strength = saturate(_ScreenNoiseBlurParams.z);

                float2 blurPixels = _ScreenNoiseBlurParams.xy;
                float2 jitterPixels = _ScreenNoiseJitterParams.xy;
                float2 jitterOffset = jitterPixels * texelSize;
                float2 centerUv = uv + jitterOffset * strength;

                float jitterLength = length(jitterPixels);
                float2 fallbackDirection = normalize(blurPixels + float2(0.0001, 0.0));
                float2 blurDirection = jitterLength > 0.001 ? jitterPixels / jitterLength : fallbackDirection;
                float blurLength = min(jitterLength * 0.85 + length(blurPixels) * 0.35, 48.0);
                float2 sampleOffset = blurDirection * blurLength * texelSize * strength;

                half4 original = SampleScreen(uv);
                half4 color = SampleScreen(centerUv) * 0.30;
                color += SampleScreen(centerUv + sampleOffset * 0.30) * 0.16;
                color += SampleScreen(centerUv - sampleOffset * 0.30) * 0.16;
                color += SampleScreen(centerUv + sampleOffset * 0.60) * 0.10;
                color += SampleScreen(centerUv - sampleOffset * 0.60) * 0.10;
                color += SampleScreen(centerUv + sampleOffset * 0.90) * 0.06;
                color += SampleScreen(centerUv - sampleOffset * 0.90) * 0.06;
                color += SampleScreen(centerUv + sampleOffset * 1.20) * 0.03;
                color += SampleScreen(centerUv - sampleOffset * 1.20) * 0.03;

                return lerp(original, color, strength);
            }
            ENDHLSL
        }
    }
}

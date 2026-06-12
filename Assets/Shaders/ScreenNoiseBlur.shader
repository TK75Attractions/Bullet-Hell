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

                float2 blurVectorPixels = jitterPixels * 0.65 + blurPixels * 0.25;
                float blurLength = max(length(blurVectorPixels), 0.0001);
                float2 blurDirection = blurVectorPixels / blurLength;
                float2 sampleOffset = blurDirection * min(blurLength, 48.0) * texelSize * strength;

                half4 color = SampleScreen(centerUv) * 0.30;
                color += SampleScreen(centerUv + sampleOffset * 0.25) * 0.18;
                color += SampleScreen(centerUv - sampleOffset * 0.25) * 0.18;
                color += SampleScreen(centerUv + sampleOffset * 0.55) * 0.12;
                color += SampleScreen(centerUv - sampleOffset * 0.55) * 0.12;
                color += SampleScreen(centerUv + sampleOffset) * 0.05;
                color += SampleScreen(centerUv - sampleOffset) * 0.05;
                color += SampleScreen(centerUv + sampleOffset * 1.45) * 0.025;
                color += SampleScreen(centerUv - sampleOffset * 1.45) * 0.025;

                half4 original = SampleScreen(uv);
                float chromaPixels = min(max(abs(jitterPixels.x), abs(jitterPixels.y)) * 0.75, 16.0);
                float2 chromaOffset = blurDirection * chromaPixels * texelSize * strength;
                half red = SampleScreen(centerUv + chromaOffset).r;
                half blue = SampleScreen(centerUv - chromaOffset).b;
                color.r = lerp(color.r, red, 0.45 * strength);
                color.b = lerp(color.b, blue, 0.45 * strength);

                float pixelNoise = frac(sin(dot(floor(uv * _ScreenParams.xy), float2(12.9898, 78.233)) + _Time.y * 17.0) * 43758.5453);
                color.rgb += (pixelNoise - 0.5) * 0.025 * strength;

                return lerp(original, color, strength);
            }
            ENDHLSL
        }
    }
}

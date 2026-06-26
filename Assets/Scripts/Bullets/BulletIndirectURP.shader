Shader "Custom/BulletIndirectURP"
{
    Properties
    {
        _MainArray("Main Texture Array", 2DArray) = "" {}
        _MaskArray("Mask Texture Array", 2DArray) = "" {}
        _AttentionBorderWidth("Attention Border Width", Float) = 0.08
        _AttentionMarkScale("Attention Mark Scale", Range(0, 1)) = 1
        _AttentionMarkSourceMin("Attention Mark Source Min", Vector) = (0.35, 0.2, 0, 0)
        _AttentionMarkSourceMax("Attention Mark Source Max", Vector) = (0.65, 0.8, 0, 0)
        _CounterMaskTexelSize("Counter Mask Texel Size", Float) = 0.0078125
        _CounterGlowRadius("Counter Glow Radius", Float) = 1.5
        _CounterGlowStrength("Counter Glow Strength", Float) = 0.42
        _CounterRimBoost("Counter Rim Boost", Float) = 1.25
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "BulletPass"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_MainArray);
            SAMPLER(sampler_MainArray);
            
            TEXTURE2D_ARRAY(_MaskArray);
            SAMPLER(sampler_MaskArray);

            float _AttentionBorderWidth;
            float _AttentionMarkScale;
            float4 _AttentionMarkSourceMin;
            float4 _AttentionMarkSourceMax;
            float _CounterMaskTexelSize;
            float _CounterGlowRadius;
            float _CounterGlowStrength;
            float _CounterRimBoost;

            struct BulletData
            {
                float2 pos;
                float2 scale;
                float angle;
                float texIndex;
                float maskIndex;
                float appear;
                float4 color;
                int renderPriority;
                float renderMode;
            };

            StructuredBuffer<BulletData> _BulletBuffer;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float texIndex : TEXCOORD1;
                float maskIndex : TEXCOORD2;
                float appear : TEXCOORD3;
                float4 color : TEXCOORD4;
                float renderMode : TEXCOORD5;
                float2 scale : TEXCOORD6;
            };

            Varyings vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                BulletData b = _BulletBuffer[instanceID];
                Varyings output;

                // 非等方スケール適用
                float2 p = input.positionOS.xy * b.scale;

                // 回転
                float s = sin(b.angle);
                float c = cos(b.angle);

                float2 rot;
                rot.x = p.x * c - p.y * s;
                rot.y = p.x * s + p.y * c;

                // 並行移動
                float2 worldPos = rot + b.pos;

                // ワールド座標からクリップ空間に変換
                float4 worldPosition = float4(worldPos.x, worldPos.y, 0, 1);
                output.positionCS = mul(UNITY_MATRIX_VP, worldPosition);
                
                output.uv = input.uv;
                output.texIndex = b.texIndex;
                output.maskIndex = b.maskIndex;
                output.appear = b.appear;
                output.color = b.color;
                output.renderMode = b.renderMode;
                output.scale = abs(b.scale);

                return output;
            }

            half4 fragAttention(Varyings input)
            {
                float appear = saturate(input.appear);
                float2 size = max(input.scale, float2(1e-4, 1e-4));
                float minSize = min(size.x, size.y);

                float borderWidth = min(max(_AttentionBorderWidth, 0.0), minSize * 0.45);
                float2 edgeDistances = min(input.uv, 1.0 - input.uv) * size;
                float edgeDistance = min(edgeDistances.x, edgeDistances.y);
                float aa = max(fwidth(edgeDistance), 1e-4);
                float borderAlpha = (1.0 - smoothstep(borderWidth, borderWidth + aa, edgeDistance))
                    * saturate(input.color.a)
                    * appear;

                float markSize = max(minSize * saturate(_AttentionMarkScale), 1e-4);
                float2 markUv = ((input.uv - 0.5) * size) / markSize + 0.5;

                float2 sourceMin = min(_AttentionMarkSourceMin.xy, _AttentionMarkSourceMax.xy);
                float2 sourceMax = max(_AttentionMarkSourceMin.xy, _AttentionMarkSourceMax.xy);
                float2 inSquare = step(float2(0.0, 0.0), markUv) * step(markUv, float2(1.0, 1.0));
                float2 inSource = step(sourceMin, markUv) * step(markUv, sourceMax);
                float sourceMask = inSquare.x * inSquare.y * inSource.x * inSource.y;

                half4 markBase = SAMPLE_TEXTURE2D_ARRAY(_MainArray, sampler_MainArray,
                    markUv, input.texIndex);
                half markMask = SAMPLE_TEXTURE2D_ARRAY(_MaskArray, sampler_MaskArray,
                    markUv, input.maskIndex).r * sourceMask;
                half markTintStrength = saturate(markMask * input.color.a);
                half3 markRgb = lerp(markBase.rgb, input.color.rgb, markTintStrength);
                half markAlpha = max(markBase.a * sourceMask, markMask) * saturate(input.color.a) * appear;

                half3 borderRgb = input.color.rgb;
                half outAlpha = saturate(markAlpha + borderAlpha * (1.0 - markAlpha));
                half3 outRgb = outAlpha > 1e-4
                    ? (markRgb * markAlpha + borderRgb * borderAlpha * (1.0 - markAlpha)) / outAlpha
                    : half3(0.0, 0.0, 0.0);

                return half4(outRgb, outAlpha);
            }

            half SampleCounterMask(float2 uv, float maskIndex)
            {
                return SAMPLE_TEXTURE2D_ARRAY(_MaskArray, sampler_MaskArray, uv, maskIndex).r;
            }

            half4 fragCounter(Varyings input)
            {
                half appear = saturate(input.appear);
                half colorAlpha = saturate(input.color.a);

                half4 baseCol = SAMPLE_TEXTURE2D_ARRAY(_MainArray, sampler_MainArray,
                    input.uv, input.texIndex);
                half mask = SampleCounterMask(input.uv, input.maskIndex);

                float2 glowStep = float2(_CounterMaskTexelSize, _CounterMaskTexelSize)
                    * max(_CounterGlowRadius, 0.0);
                half glowMask = mask;
                glowMask = max(glowMask, SampleCounterMask(input.uv + float2(glowStep.x, 0.0), input.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(input.uv + float2(-glowStep.x, 0.0), input.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(input.uv + float2(0.0, glowStep.y), input.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(input.uv + float2(0.0, -glowStep.y), input.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(input.uv + glowStep, input.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(input.uv - glowStep, input.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(input.uv + float2(glowStep.x, -glowStep.y), input.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(input.uv + float2(-glowStep.x, glowStep.y), input.maskIndex));

                half rimAlpha = saturate(mask * colorAlpha * _CounterRimBoost);
                half glowAlpha = saturate(max(glowMask - mask, 0.0) * colorAlpha * _CounterGlowStrength);
                half tintAlpha = saturate(rimAlpha + glowAlpha * (1.0 - rimAlpha));
                half baseAlpha = saturate(baseCol.a);
                half outAlpha = saturate(baseAlpha + tintAlpha * (1.0 - baseAlpha));
                half3 outRgb = outAlpha > 1e-4
                    ? (baseCol.rgb * baseAlpha + input.color.rgb * tintAlpha * (1.0 - baseAlpha)) / outAlpha
                    : half3(0.0, 0.0, 0.0);

                return half4(outRgb, outAlpha * appear);
            }

            half4 fragCounterTrail(Varyings input)
            {
                half appear = saturate(input.appear);
                half colorAlpha = saturate(input.color.a);
                half2 centeredUv = abs(input.uv - 0.5) * 2.0;
                half cross = centeredUv.y;
                half cap = smoothstep(0.0, 0.22, input.uv.x)
                    * smoothstep(0.0, 0.22, 1.0 - input.uv.x);
                half core = 1.0 - smoothstep(0.05, 0.48, cross);
                half glow = 1.0 - smoothstep(0.16, 1.0, cross);
                half headBoost = lerp(0.72, 1.12, saturate(input.uv.x));
                half alpha = saturate((core * 0.68 + glow * 0.34) * cap * headBoost * colorAlpha);

                return half4(input.color.rgb, alpha * appear);
            }

            half4 fragCounterSpawnFlash(Varyings input)
            {
                half appear = saturate(input.appear);
                half colorAlpha = saturate(input.color.a);
                float2 centeredUv = input.uv - 0.5;
                float distanceFromCenter = length(centeredUv) * 2.0;
                float ringDistance = abs(distanceFromCenter - 0.62);
                half ring = 1.0 - smoothstep(0.035, 0.11, ringDistance);
                half innerGlow = (1.0 - smoothstep(0.0, 0.9, distanceFromCenter)) * 0.22;
                half alpha = saturate((ring + innerGlow) * colorAlpha);

                return half4(input.color.rgb, alpha * appear);
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (input.renderMode > 4.5)
                {
                    return fragCounterSpawnFlash(input);
                }

                if (input.renderMode > 3.5)
                {
                    return fragCounterTrail(input);
                }

                if (input.renderMode > 2.5)
                {
                    return fragCounter(input);
                }

                if (input.renderMode > 1.5)
                {
                    return fragAttention(input);
                }

                // テクスチャ配列からサンプリング
                half4 baseCol = SAMPLE_TEXTURE2D_ARRAY(_MainArray, sampler_MainArray, 
                    input.uv, input.texIndex);

                half mask = SAMPLE_TEXTURE2D_ARRAY(_MaskArray, sampler_MaskArray, 
                    input.uv, input.maskIndex).r;
                half appear = saturate(input.appear);

                if (input.renderMode > 0.5)
                {
                    baseCol.rgb = lerp(baseCol.rgb, input.color.rgb, saturate(mask));
                    baseCol.a *= saturate(input.color.a) * appear;
                    return baseCol;
                }

                half tintAlpha = saturate(mask * input.color.a);
                half baseAlpha = saturate(baseCol.a);
                half outAlpha = saturate(baseAlpha + tintAlpha * (1.0 - baseAlpha));
                half3 outRgb = outAlpha > 1e-4
                    ? (baseCol.rgb * baseAlpha * (1.0 - tintAlpha) + input.color.rgb * tintAlpha) / outAlpha
                    : half3(0.0, 0.0, 0.0);

                // マスク値に color.a を掛けて色の掛かり方を 0-1 で制御する
                baseCol.rgb = outRgb;
                baseCol.a = outAlpha * appear;

                return baseCol;
            }
            ENDHLSL
        }
    }
    
    Fallback Off
}

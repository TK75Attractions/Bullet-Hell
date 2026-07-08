Shader "Custom/BulletIndirectMasked"
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
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "BulletPass"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            //========================
            // Texture Arrays
            //========================
            UNITY_DECLARE_TEX2DARRAY(_MainArray);
            UNITY_DECLARE_TEX2DARRAY(_MaskArray);

            float _AttentionBorderWidth;
            float _AttentionMarkScale;
            float4 _AttentionMarkSourceMin;
            float4 _AttentionMarkSourceMax;
            float _CounterMaskTexelSize;
            float _CounterGlowRadius;
            float _CounterGlowStrength;
            float _CounterRimBoost;
            float _StoneBeltScroll;

            //========================
            // GPU �\���́iC# �ƈ�v�K�{�j
            //========================
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

            //========================
            // ���_���́i�|���j
            //========================
            struct appdata
            {
                float3 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            //========================
            // ���_ �� �s�N�Z��
            //========================
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;

                float texIndex : TEXCOORD1;
                float maskIndex : TEXCOORD2;
                float appear : TEXCOORD3;
                float4 color    : TEXCOORD4;
                float renderMode : TEXCOORD5;
                float2 scale : TEXCOORD6;
            };

            //========================
            // Vertex Shader
            //========================
            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                BulletData b = _BulletBuffer[instanceID];

                v2f o;

                // スケール適用
                float2 p = v.vertex.xy * b.scale;

                // ��]
                float s = sin(b.angle);
                float c = cos(b.angle);

                float2 rot;
                rot.x = p.x * c - p.y * s;
                rot.y = p.x * s + p.y * c;

                // ���s�ړ�
                float2 world = rot + b.pos;

                // ワールド座標からクリップ空間に変換
                o.pos = mul(UNITY_MATRIX_VP, float4(world.x, world.y, 0, 1));
                o.uv = v.uv;

                o.texIndex = b.texIndex;
                o.maskIndex = b.maskIndex;
                o.appear = b.appear;
                o.color = b.color;
                o.renderMode = b.renderMode;
                o.scale = abs(b.scale);

                return o;
            }

            fixed4 fragAttention(v2f i)
            {
                float appear = saturate(i.appear);
                float2 size = max(i.scale, float2(1e-4, 1e-4));
                float minSize = min(size.x, size.y);

                float borderWidth = min(max(_AttentionBorderWidth, 0.0), minSize * 0.45);
                float2 edgeDistances = min(i.uv, 1.0 - i.uv) * size;
                float edgeDistance = min(edgeDistances.x, edgeDistances.y);
                float aa = max(fwidth(edgeDistance), 1e-4);
                float borderAlpha = (1.0 - smoothstep(borderWidth, borderWidth + aa, edgeDistance))
                    * saturate(i.color.a)
                    * appear;

                float markSize = max(minSize * saturate(_AttentionMarkScale), 1e-4);
                float2 markUv = ((i.uv - 0.5) * size) / markSize + 0.5;

                float2 sourceMin = min(_AttentionMarkSourceMin.xy, _AttentionMarkSourceMax.xy);
                float2 sourceMax = max(_AttentionMarkSourceMin.xy, _AttentionMarkSourceMax.xy);
                float2 inSquare = step(float2(0.0, 0.0), markUv) * step(markUv, float2(1.0, 1.0));
                float2 inSource = step(sourceMin, markUv) * step(markUv, sourceMax);
                float sourceMask = inSquare.x * inSquare.y * inSource.x * inSource.y;

                fixed4 markBase =
                    UNITY_SAMPLE_TEX2DARRAY(_MainArray, float3(markUv, i.texIndex));
                float markMask =
                    UNITY_SAMPLE_TEX2DARRAY(_MaskArray, float3(markUv, i.maskIndex)).r * sourceMask;
                // color.a is opacity, not tint strength. Keeping it out of the RGB
                // blend prevents low-alpha bullets from fading back to white.
                float markTintStrength = saturate(markMask);
                fixed3 markRgb = lerp(markBase.rgb, i.color.rgb, markTintStrength);
                float markAlpha = max(markBase.a * sourceMask, markMask) * saturate(i.color.a) * appear;

                fixed3 borderRgb = i.color.rgb;
                float outAlpha = saturate(markAlpha + borderAlpha * (1.0 - markAlpha));
                fixed3 outRgb = outAlpha > 1e-4
                    ? (markRgb * markAlpha + borderRgb * borderAlpha * (1.0 - markAlpha)) / outAlpha
                    : fixed3(0.0, 0.0, 0.0);

                return fixed4(outRgb, outAlpha);
            }

            float SampleCounterMask(float2 uv, float maskIndex)
            {
                return UNITY_SAMPLE_TEX2DARRAY(_MaskArray, float3(uv, maskIndex)).r;
            }

            fixed4 fragCounter(v2f i)
            {
                float appear = saturate(i.appear);
                float colorAlpha = saturate(i.color.a);

                fixed4 baseCol =
                    UNITY_SAMPLE_TEX2DARRAY(_MainArray, float3(i.uv, i.texIndex));
                float mask = SampleCounterMask(i.uv, i.maskIndex);

                float2 glowStep = float2(_CounterMaskTexelSize, _CounterMaskTexelSize)
                    * max(_CounterGlowRadius, 0.0);
                float glowMask = mask;
                glowMask = max(glowMask, SampleCounterMask(i.uv + float2(glowStep.x, 0.0), i.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(i.uv + float2(-glowStep.x, 0.0), i.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(i.uv + float2(0.0, glowStep.y), i.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(i.uv + float2(0.0, -glowStep.y), i.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(i.uv + glowStep, i.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(i.uv - glowStep, i.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(i.uv + float2(glowStep.x, -glowStep.y), i.maskIndex));
                glowMask = max(glowMask, SampleCounterMask(i.uv + float2(-glowStep.x, glowStep.y), i.maskIndex));

                float rimAlpha = saturate(mask * colorAlpha * _CounterRimBoost);
                float glowAlpha = saturate(max(glowMask - mask, 0.0) * colorAlpha * _CounterGlowStrength);
                float tintAlpha = saturate(rimAlpha + glowAlpha * (1.0 - rimAlpha));
                float baseAlpha = saturate(baseCol.a);
                float outAlpha = saturate(baseAlpha + tintAlpha * (1.0 - baseAlpha));
                fixed3 outRgb = outAlpha > 1e-4
                    ? (baseCol.rgb * baseAlpha + i.color.rgb * tintAlpha * (1.0 - baseAlpha)) / outAlpha
                    : fixed3(0.0, 0.0, 0.0);

                return fixed4(outRgb, outAlpha * appear);
            }

            fixed4 fragCounterTrail(v2f i)
            {
                float appear = saturate(i.appear);
                float colorAlpha = saturate(i.color.a);
                float2 centeredUv = abs(i.uv - 0.5) * 2.0;
                float cross = centeredUv.y;
                float cap = smoothstep(0.0, 0.22, i.uv.x)
                    * smoothstep(0.0, 0.22, 1.0 - i.uv.x);
                float core = 1.0 - smoothstep(0.05, 0.48, cross);
                float glow = 1.0 - smoothstep(0.16, 1.0, cross);
                float headBoost = lerp(0.72, 1.12, saturate(i.uv.x));
                float alpha = saturate((core * 0.68 + glow * 0.34) * cap * headBoost * colorAlpha);

                return fixed4(i.color.rgb, alpha * appear);
            }

            fixed4 fragCounterSpawnFlash(v2f i)
            {
                float appear = saturate(i.appear);
                float colorAlpha = saturate(i.color.a);
                float2 centeredUv = i.uv - 0.5;
                float distanceFromCenter = length(centeredUv) * 2.0;
                float ringDistance = abs(distanceFromCenter - 0.62);
                float ring = (1.0 - smoothstep(0.018, 0.06, ringDistance)) * 0.6;
                float innerGlow = (1.0 - smoothstep(0.0, 0.9, distanceFromCenter)) * 0.08;
                float alpha = saturate((ring + innerGlow) * colorAlpha);

                return fixed4(i.color.rgb, alpha * appear);
            }

            //========================
            // Fragment Shader
            //========================
            fixed4 frag(v2f i) : SV_Target
            {
                if (i.scale.x > 20.0 && i.scale.y < 3.5)
                {
                    i.uv.x = frac(i.uv.x + _StoneBeltScroll);
                }

                if (i.renderMode > 4.5)
                {
                    return fragCounterSpawnFlash(i);
                }

                if (i.renderMode > 3.5)
                {
                    return fragCounterTrail(i);
                }

                if (i.renderMode > 2.5)
                {
                    return fragCounter(i);
                }

                if (i.renderMode > 1.5)
                {
                    return fragAttention(i);
                }

                fixed4 baseCol =
                    UNITY_SAMPLE_TEX2DARRAY(_MainArray, float3(i.uv, i.texIndex));

                float mask =
                    UNITY_SAMPLE_TEX2DARRAY(_MaskArray, float3(i.uv, i.maskIndex)).r;
                float appear = saturate(i.appear);

                if (i.renderMode > 0.5)
                {
                    baseCol.rgb = lerp(baseCol.rgb, i.color.rgb, saturate(mask));
                    baseCol.a *= saturate(i.color.a) * appear;
                    return baseCol;
                }

                // Compose the texture and tint independently from opacity so that
                // color.a only controls the transparency of the finished bullet.
                float tintAlpha = saturate(mask);
                float baseAlpha = saturate(baseCol.a);
                float outAlpha = saturate(baseAlpha + tintAlpha * (1.0 - baseAlpha));
                fixed3 outRgb = outAlpha > 1e-4
                    ? (baseCol.rgb * baseAlpha * (1.0 - tintAlpha) + i.color.rgb * tintAlpha) / outAlpha
                    : fixed3(0.0, 0.0, 0.0);

                // デバッグ: マスク値を可視化（一時的）
                // return fixed4(mask, mask, mask, 1); // マスクをグレースケールで表示
                
                baseCol.rgb = outRgb;
                baseCol.a = outAlpha * saturate(i.color.a) * appear;

                return baseCol;
            }

        ENDCG
    }
    }
}

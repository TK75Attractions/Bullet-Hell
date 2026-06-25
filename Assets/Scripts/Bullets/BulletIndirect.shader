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
                float markTintStrength = saturate(markMask * i.color.a);
                fixed3 markRgb = lerp(markBase.rgb, i.color.rgb, markTintStrength);
                float markAlpha = max(markBase.a * sourceMask, markMask) * saturate(i.color.a) * appear;

                fixed3 borderRgb = i.color.rgb;
                float outAlpha = saturate(markAlpha + borderAlpha * (1.0 - markAlpha));
                fixed3 outRgb = outAlpha > 1e-4
                    ? (markRgb * markAlpha + borderRgb * borderAlpha * (1.0 - markAlpha)) / outAlpha
                    : fixed3(0.0, 0.0, 0.0);

                return fixed4(outRgb, outAlpha);
            }

            //========================
            // Fragment Shader
            //========================
            fixed4 frag(v2f i) : SV_Target
            {
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

                float tintStrength = saturate(mask * i.color.a);

                // デバッグ: マスク値を可視化（一時的）
                // return fixed4(mask, mask, mask, 1); // マスクをグレースケールで表示
                
                // マスク値に color.a を掛けて色の掛かり方を 0-1 で制御する
                baseCol.rgb = lerp(baseCol.rgb, i.color.rgb, tintStrength);
                baseCol.a = max(baseCol.a, tintStrength) * appear;

                return baseCol;
            }

        ENDCG
    }
    }
}

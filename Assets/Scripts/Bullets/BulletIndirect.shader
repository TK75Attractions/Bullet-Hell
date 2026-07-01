Shader "Custom/BulletIndirectMasked"
{
    Properties
    {
        _MainArray("Main Texture Array", 2DArray) = "" {}
        _MaskArray("Mask Texture Array", 2DArray) = "" {}
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
                float2 scale : TEXCOORD5;
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
                o.scale = b.scale;

                return o;
            }

            //========================
            // Fragment Shader
            //========================
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                if (i.scale.x > 20.0 && i.scale.y < 3.5)
                {
                uv.x = frac(uv.x + _Time.y * 0.14);
                }

                fixed4 baseCol =
                    UNITY_SAMPLE_TEX2DARRAY(_MainArray, float3(uv, i.texIndex));

                float mask =
                    UNITY_SAMPLE_TEX2DARRAY(_MaskArray, float3(uv, i.maskIndex)).r;
                float appear = saturate(i.appear);
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

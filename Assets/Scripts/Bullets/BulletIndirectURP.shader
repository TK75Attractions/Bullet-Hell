Shader "Custom/BulletIndirectURP"
{
    Properties
    {
        _MainArray("Main Texture Array", 2DArray) = "" {}
        _MaskArray("Mask Texture Array", 2DArray) = "" {}
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
                float2 scale : TEXCOORD5;
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
                output.scale = b.scale;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                if (input.scale.x > 20.0 && input.scale.y < 3.5)
                {
                // ベルト帯(scale.x36.5)のスリット模様を UV スクロール。
                // 速度は帯上を流れるブロック(belt_flow ov.x=-9.5)と厳密一致させる:
                // 0.26 UV/s * 36.5 world/UV = 9.49 world/s ≒ 9.5(REVIEW @6.3 速度一致)
                uv.x = frac(uv.x + _Time.y * 0.26);
                }

                // テクスチャ配列からサンプリング
                half4 baseCol = SAMPLE_TEXTURE2D_ARRAY(_MainArray, sampler_MainArray, 
                    uv, input.texIndex);

                half mask = SAMPLE_TEXTURE2D_ARRAY(_MaskArray, sampler_MaskArray, 
                    uv, input.maskIndex).r;
                half appear = saturate(input.appear);
                half tintStrength = saturate(mask * input.color.a);

                // マスク値に color.a を掛けて色の掛かり方を 0-1 で制御する
                baseCol.rgb = lerp(baseCol.rgb, input.color.rgb, tintStrength);
                baseCol.a = max(baseCol.a, tintStrength) * appear;

                return baseCol;
            }
            ENDHLSL
        }
    }
    
    Fallback Off
}

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
                float angle;
                float size;
                float texIndex;
                float maskIndex;
                float4 color;
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
                float4 color : TEXCOORD3;
            };

            Varyings vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                BulletData b = _BulletBuffer[instanceID];
                Varyings output;

                // サイズ適用
                float2 p = input.positionOS.xy * b.size;

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
                output.color = b.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // テクスチャ配列からサンプリング
                half4 baseCol = SAMPLE_TEXTURE2D_ARRAY(_MainArray, sampler_MainArray, 
                    input.uv, input.texIndex);

                half mask = SAMPLE_TEXTURE2D_ARRAY(_MaskArray, sampler_MaskArray, 
                    input.uv, input.maskIndex).r;
                half tintStrength = saturate(mask * input.color.a);

                // マスク値に color.a を掛けて色の掛かり方を 0-1 で制御する
                baseCol.rgb = lerp(baseCol.rgb, input.color.rgb, tintStrength);
                baseCol.a = max(baseCol.a, tintStrength);

                return baseCol;
            }
            ENDHLSL
        }
    }
    
    Fallback Off
}

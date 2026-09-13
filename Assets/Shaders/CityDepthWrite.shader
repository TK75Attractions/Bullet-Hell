// ステージ選択の街(CityMapController)の輪郭線用。街の複製(CityDepth レイヤー)を
// 深度専用カメラで描き、視点からの距離(正投影なので view 空間の -z、ワールド単位)を
// R チャンネルに書く。UIPixelQuantize がこの RT を読み、隣の texel との距離差で線を引く。
Shader "BulletHell/City/DepthWrite"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        ZWrite On
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "CityDepthWrite"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float depth : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 vs = TransformWorldToView(ws);
                o.positionCS = TransformWorldToHClip(ws);
                o.depth = -vs.z;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                return float4(i.depth, 0, 0, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

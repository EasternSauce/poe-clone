Shader "PoeClone/Outline"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        // Skinned hulls can't be inflated by scale (bones place their vertices): push along normals.
        _Extrude ("Extrude", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        LOD 100

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="UniversalForward" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Extrude;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz + IN.normalOS * _Extrude);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(_Color.rgb, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

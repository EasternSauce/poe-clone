Shader "PoeClone/SpiritGlow"
{
    // Unlit, self-lit surfaces (waystone crystals, carved runes): a deep body colour that
    // brightens to a pale core where the surface faces the viewer, so facets still read.
    Properties
    {
        _BaseColor ("Body", Color) = (0.25, 0.45, 1, 1)
        _CoreColor ("Core", Color) = (0.85, 0.95, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _CoreColor;
            CBUFFER_END
            struct A { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct V { float4 position : SV_POSITION; float3 normal : TEXCOORD0; float3 view : TEXCOORD1; float fog : TEXCOORD2; };
            V vert(A v)
            {
                V o;
                float3 world = TransformObjectToWorld(v.vertex.xyz);
                o.position = TransformWorldToHClip(world);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.view = GetWorldSpaceViewDir(world);
                o.fog = ComputeFogFactor(o.position.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half facing = saturate(dot(normalize(i.normal), normalize(i.view)));
                half3 color = lerp(_BaseColor.rgb, _CoreColor.rgb, facing * facing);
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }
    }
}

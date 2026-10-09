Shader "PoeClone/AmbientParticle"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 position : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };
            V vert(A v) { V o; o.position = TransformObjectToHClip(v.vertex.xyz); o.color = v.color; o.uv = v.uv; o.fog = ComputeFogFactor(o.position.z); return o; }
            half4 frag(V i) : SV_Target
            {
                float r = length(i.uv * 2 - 1);
                i.color.a *= pow(saturate(1 - r * r), 2);
                i.color.rgb = MixFog(i.color.rgb, i.fog);
                return i.color;
            }
            ENDHLSL
        }
    }
}

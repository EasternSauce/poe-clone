Shader "PoeClone/WindGrass"
{
    Properties { _Wind ("Wind", Vector) = (0.7,0,0.5,0) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Wind;
            CBUFFER_END
            struct A { float4 vertex : POSITION; float2 motion : TEXCOORD0; half4 color : COLOR; };
            struct V { float4 position : SV_POSITION; float3 world : TEXCOORD0; half3 color : COLOR; float fog : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                float3 world = TransformObjectToWorld(v.vertex.xyz);
                float t = _Time.y;
                // Coherent travelling gusts with individual blade flutter; roots have zero weight.
                float wave = sin(dot(world.xz, float2(0.8,0.55)) - t * 2.1);
                float flutter = sin(t * 4.3 + v.motion.y + world.x * 1.7) * 0.09;
                world.xz += (_Wind.xz * (0.32 + wave * 0.14) + float2(-_Wind.z, _Wind.x) * flutter) * v.motion.x;
                world.y -= v.motion.x * (0.035 + 0.025 * wave);
                o.world = world;
                o.position = TransformWorldToHClip(world);
                o.color = v.color.rgb;
                o.fog = ComputeFogFactor(o.position.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 light = SampleSH(half3(0,1,0)) + sun.color * (0.35 + 0.65 * sun.shadowAttenuation) * saturate(sun.direction.y * 0.5 + 0.5);
                return half4(MixFog(i.color * light, i.fog), 1);
            }
            ENDHLSL
        }
    }
}

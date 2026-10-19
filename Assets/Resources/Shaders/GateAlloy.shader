Shader "PoeClone/GateAlloy"
{
    Properties
    {
        _BaseColor ("Alloy", Color) = (0.1,0.15,0.19,1)
        _Metallic ("Metal highlights", Range(0,1)) = 0.7
        [HideInInspector] _BaseMap ("Base", 2D) = "white" {}
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; float _Metallic;
            CBUFFER_END
            struct A { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct V { float4 position : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; float3 local : TEXCOORD2; float fog : TEXCOORD3; };
            V vert(A v)
            {
                V o; o.world=TransformObjectToWorld(v.vertex.xyz); o.position=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normal); o.local=v.vertex.xyz; o.fog=ComputeFogFactor(o.position.z); return o;
            }
            float hash(float3 p) { return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453); }
            half4 frag(V i) : SV_Target
            {
                float3 n=normalize(i.normal), view=normalize(GetWorldSpaceViewDir(i.world));
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                float hammer=hash(floor(i.local*46));
                float scratches=pow(saturate(sin(i.local.y*220+sin(i.local.x*83)*3)),18)*0.07;
                half3 base=_BaseColor.rgb*(0.83+hammer*0.24-scratches);
                float diffuse=saturate(dot(n,sun.direction));
                float spec=pow(saturate(dot(n,normalize(view+sun.direction))),38)*_Metallic;
                half3 color=base*(SampleSH(n)+0.25+sun.color*(0.2+diffuse*0.8)*sun.shadowAttenuation);
                color+=lerp(half3(0.25,0.32,0.4),_BaseColor.rgb,0.65)*spec*sun.color*sun.shadowAttenuation;
                color+=base*pow(1-saturate(dot(n,view)),3)*0.3;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}

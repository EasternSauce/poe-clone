Shader "PoeClone/AnatomySkin"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Skin Color", Color) = (0.66,0.43,0.31,1)
        [Normal] _BumpMap ("Skin Pores", 2D) = "bump" {}
        _BumpScale ("Pore Strength", Range(0,1)) = 0.16
        _DetailTiling ("Pore Tiling", Float) = 12
        _Smoothness ("Smoothness", Range(0,1)) = 0.32
        _OcclusionMap ("Roughness Variation", 2D) = "white" {}
        _Subsurface ("Warm Wrapped Light", Range(0,1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile _ _MOBILE_LITE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; half4 color:COLOR; float fog:TEXCOORD4; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _BumpScale, _DetailTiling, _Smoothness, _Subsurface;
            CBUFFER_END
            #define MAX_MANUAL_LIGHTS 24
            float4 _ManualLightPosRange[MAX_MANUAL_LIGHTS];
            float4 _ManualLightColorIntensity[MAX_MANUAL_LIGHTS];
            int _ManualLightCount;
            float4 _CaveBounds, _HollowBounds, _SanctuaryBounds, _WorksBounds, _WardBounds, _RootBounds;

            float InCave(float3 p,float4 bounds)
            {
                float2 d=abs(p.xz-bounds.xy);
                return bounds.z>0 && d.x<bounds.z && d.y<bounds.w ? 1:0;
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs n=GetVertexNormalInputs(input.normalOS,input.tangentOS);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=n.normalWS;
                o.tangentWS=float4(n.tangentWS,input.tangentOS.w*GetOddNegativeScale());
                o.uv=input.uv;o.color=input.color;o.fog=ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 Illuminate(half3 albedo,float3 n,float3 view,float3 direction,half3 light,float attenuation,float smoothness)
            {
                float ndl=dot(n,direction);
                float diffuse=saturate(ndl);
                float wrap=saturate((ndl+.35)/1.35)-diffuse;
                half3 warm=half3(1,.37,.22)*wrap*_Subsurface;
                float3 h=SafeNormalize(view+direction);
                float power=exp2(3+smoothness*7);
                float spec=pow(saturate(dot(n,h)),power)*(.025+smoothness*.12)*diffuse;
                return (albedo*(diffuse+warm)+spec)*light*attenuation;
            }

            half4 frag(Varyings input):SV_Target
            {
                float3 n=normalize(input.normalWS);
                float3 view=SafeNormalize(GetCameraPositionWS()-input.positionWS);
                float smoothness=_Smoothness;
                #if !defined(_MOBILE_LITE)
                    float2 uv=input.uv*_DetailTiling;
                    float3 tn=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv),_BumpScale);
                    float3 t=SafeNormalize(input.tangentWS.xyz);
                    float3 b=cross(n,t)*input.tangentWS.w;
                    n=normalize(t*tn.x+b*tn.y+n*tn.z);
                    smoothness*=lerp(.7,1.1,SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,uv).r);
                #endif
                half3 albedo=_BaseColor.rgb*input.color.rgb*SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv*_BaseMap_ST.xy+_BaseMap_ST.zw).rgb;
                float cave=max(max(InCave(input.positionWS,_CaveBounds),InCave(input.positionWS,_HollowBounds)),max(InCave(input.positionWS,_SanctuaryBounds),InCave(input.positionWS,_WorksBounds)));
                cave=max(cave,max(InCave(input.positionWS,_WardBounds),InCave(input.positionWS,_RootBounds)));
                float environment=lerp(1,.23,cave);
                Light main=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 color=albedo*(SampleSH(n)*.65+.12)*environment;
                color+=Illuminate(albedo,n,view,main.direction,main.color,main.shadowAttenuation*environment,smoothness);
                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                    uint lightCount=GetAdditionalLightsCount();
                    for(uint j=0;j<lightCount;j++)
                    {
                        Light l=GetAdditionalLight(j,input.positionWS);
                        color+=Illuminate(albedo,n,view,l.direction,l.color,l.distanceAttenuation*l.shadowAttenuation,smoothness);
                    }
                #endif
                for(int i=0;i<_ManualLightCount;i++)
                {
                    float3 delta=_ManualLightPosRange[i].xyz-input.positionWS;
                    float d2=max(dot(delta,delta),.0001),range2=_ManualLightPosRange[i].w*_ManualLightPosRange[i].w;
                    if(d2>=range2) continue;
                    float cutoff=saturate(1-(d2/range2)*(d2/range2));
                    color+=Illuminate(albedo,n,view,delta*rsqrt(d2),_ManualLightColorIntensity[i].rgb*_ManualLightColorIntensity[i].a,cutoff*cutoff/d2,smoothness);
                }
                return half4(MixFog(color,input.fog),_BaseColor.a);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Universal Render Pipeline/Lit"
}

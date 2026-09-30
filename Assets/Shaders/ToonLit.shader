Shader "PoeClone/ToonLit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Tint", Color) = (0.55,0.55,0.68,1)
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _RimIntensity ("Rim Intensity", Range(0,1)) = 0.2
        _ShadowBandThreshold ("Shadow Band Threshold", Range(0,1)) = 0.5
        _ShadowBandSmooth ("Shadow Band Smoothness", Range(0.001,0.5)) = 0.08
        _AmbientBoost ("Ambient Fill", Range(0,1)) = 0.35
        _TriplanarTileSize ("Triplanar Tile Size (world units)", Float) = 2.0
        _TexInfluence ("Texture Influence", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _ShadowBandThreshold;
                float _ShadowBandSmooth;
                float _AmbientBoost;
                float _TriplanarTileSize;
                float _TexInfluence;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs vni = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.normalWS = vni.normalWS;
                OUT.fogCoord = ComputeFogFactor(vpi.positionCS.z);
                return OUT;
            }

            // World-space triplanar sampling: the procedurally generated low-poly
            // meshes in this project have no UVs at all, so texturing has to come
            // from world position/normal instead of UV0.
            half3 SampleTriplanar(float3 positionWS, float3 normalWS, float tileSize)
            {
                float scale = 1.0 / max(tileSize, 0.001);
                float3 blend = abs(normalWS);
                blend = blend / max(blend.x + blend.y + blend.z, 1e-5);

                half3 texX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.zy * scale).rgb;
                half3 texY = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xz * scale).rgb;
                half3 texZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xy * scale).rgb;

                return texX * blend.x + texY * blend.y + texZ * blend.z;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = dot(normalWS, mainLight.direction);
                float lit = saturate(NdotL);
                float band = smoothstep(_ShadowBandThreshold - _ShadowBandSmooth, _ShadowBandThreshold + _ShadowBandSmooth, lit);
                band *= mainLight.shadowAttenuation;

                half3 texCol = SampleTriplanar(IN.positionWS, normalWS, _TriplanarTileSize);
                texCol = lerp(half3(1, 1, 1), texCol, _TexInfluence);
                half3 albedo = texCol * _BaseColor.rgb;

                half3 litColor = albedo * mainLight.color.rgb;
                half3 shadowedColor = albedo * _ShadowColor.rgb;
                half3 color = lerp(shadowedColor, litColor, band);

                half3 ambient = SampleSH(normalWS) * albedo;
                color += ambient * _AmbientBoost;

                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                int pixelLightCount = GetAdditionalLightsCount();
                for (int i = 0; i < pixelLightCount; i++)
                {
                    Light addLight = GetAdditionalLight(i, IN.positionWS);
                    float addNdotL = saturate(dot(normalWS, addLight.direction));
                    color += albedo * addLight.color.rgb * addNdotL * addLight.distanceAttenuation * addLight.shadowAttenuation;
                }
                #endif

                float3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);
                float rim = 1.0 - saturate(dot(viewDir, normalWS));
                rim = pow(rim, _RimPower) * _RimIntensity * band;
                color += _RimColor.rgb * rim;

                color = MixFog(color, IN.fogCoord);

                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Universal Render Pipeline/Lit"
}

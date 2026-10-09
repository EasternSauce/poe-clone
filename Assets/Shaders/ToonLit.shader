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
        _LivingWater ("Living Water", Range(0,1)) = 0
        _WaterOcean ("Ocean Wave Strength", Range(0,1)) = 0
        _SerpentScales ("Serpent Scale Surface", Range(0,1)) = 0
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
            // One directive, as URP's Lit declares it: URP's build stripping selects these keywords
            // per directive, and as two separate directives only the both-on variant survived, so
            // builds (but not the Editor) had no variant matching the shadows URP actually enables.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            // Global, not per-material: toggled once at startup from TouchMode (see TouchMode.cs).
            // Mobile here means "runs acceptably", not "looks as good as PC" - this strips the
            // costliest per-pixel work (extra texture samples, rim power) for every opaque pixel
            // on screen, independent of scene complexity.
            #pragma multi_compile _ _MOBILE_LITE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 waterMotion : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float4 waterMotion : TEXCOORD4;
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
                float _SerpentScales;
                float _LivingWater;
                float _WaterOcean;
            CBUFFER_END

            // ---------------------------------------------------------------
            // Manual point-light feed (fallback for URP's Forward+ additional
            // lights, which aren't reliably reaching this custom shader in
            // this project's URP version). Pushed once per frame from
            // ManualPointLightManager.cs via Shader.SetGlobalVectorArray /
            // SetGlobalInt. Plain "old-school" forward-additive lighting,
            // independent of any Forward+/clustered light-loop keywords.
            // ---------------------------------------------------------------
            #define MAX_MANUAL_LIGHTS 24
            float4 _ManualLightPosRange[MAX_MANUAL_LIGHTS];   // xyz = world pos, w = range
            float4 _ManualLightColorIntensity[MAX_MANUAL_LIGHTS]; // rgb = color, a = intensity
            int _ManualLightCount;
            // World-space cave regions keep sunlight/ambient dim while preserving local torches.
            float4 _CaveBounds;
            float4 _HollowBounds;
            float4 _SanctuaryBounds;
            float InCave(float3 p, float4 bounds)
            {
                float2 distance = abs(p.xz - bounds.xy);
                return bounds.z > 0 && distance.x < bounds.z && distance.y < bounds.w ? 1.0 : 0.0;
            }

            float WaterWave(float2 p, float4 motion)
            {
                float t = _Time.y;
                float still = sin(dot(p, float2(0.87, 0.54)) - t * 1.1) *
                    0.035 + sin(dot(p, float2(-0.6, 1.3)) - t * 0.83) * 0.018;
                float current = sin(motion.z * 2.6 - t * 3.8) * 0.035 +
                    sin(dot(p, float2(-motion.y, motion.x)) * 4 + motion.z * 1.3 - t * 2) * 0.012;
                return lerp(still, current, motion.w) * (1 + _WaterOcean * 0.5);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                if (_LivingWater > 0)
                    IN.positionOS.y += WaterWave(TransformObjectToWorld(IN.positionOS.xyz).xz, IN.waterMotion);
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs vni = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.normalWS = vni.normalWS;
                OUT.fogCoord = ComputeFogFactor(vpi.positionCS.z);
                OUT.uv = IN.uv;
                OUT.waterMotion = IN.waterMotion;
                return OUT;
            }

            // World-space triplanar sampling: the procedurally generated low-poly
            // meshes in this project have no UVs at all, so texturing has to come
            // from world position/normal instead of UV0.
            half3 SampleTriplanar(float3 positionWS, float3 normalWS, float tileSize)
            {
                float scale = 1.0 / max(tileSize, 0.001);

                #if defined(_MOBILE_LITE)
                // One sample instead of three blended ones - visibly flatter on steep surfaces,
                // but a third of the texture bandwidth on every opaque pixel on screen.
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xz * scale + _BaseMap_ST.zw).rgb;
                #else
                float3 blend = abs(normalWS);
                blend = blend / max(blend.x + blend.y + blend.z, 1e-5);

                half3 texX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.zy * scale + _BaseMap_ST.zw).rgb;
                half3 texY = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xz * scale + _BaseMap_ST.zw).rgb;
                half3 texZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xy * scale + _BaseMap_ST.zw).rgb;

                return texX * blend.x + texY * blend.y + texZ * blend.z;
                #endif
            }

            // Staggered, overlapping scales anchored to the growing tube's UVs. No world-space
            // projection: the scales stay wrapped around bends and keep their physical size.
            half4 SerpentSurface(float2 uv)
            {
                float row = floor(uv.y);
                float2 shifted = float2(uv.x + fmod(row, 2.0) * 0.5, uv.y);
                float2 cell = frac(shifted) - 0.5;
                float2 id = float2(fmod(floor(shifted.x), 12.0), row);
                float variation = frac(sin(dot(id, float2(127.1, 311.7))) * 43758.5453);
                float edge = abs(cell.x) * 1.35 + abs(cell.y + 0.08) * 0.85;
                float seam = smoothstep(0.48, 0.61, edge);
                float crown = 1.0 - smoothstep(0.12, 0.58, edge);
                float weathering = sin(uv.y * 0.59 + sin(uv.x * 1.0472)) * cos(uv.x * 0.5236);
                half3 scales = lerp(half3(0.68, 0.72, 0.52), half3(1.35, 1.22, 0.82), variation);
                scales *= 0.96 + weathering * 0.18 + crown * 0.12;
                scales = lerp(scales, half3(0.34, 0.38, 0.25), seam * 0.85);
                // Broad, paler belly plates contrast with the mottled dorsal scales.
                float belly = smoothstep(0.35, 0.70, -sin(uv.x * 0.5235988));
                float bellySeam = smoothstep(0.38, 0.49, abs(cell.y));
                scales = lerp(scales, half3(1.25, 1.18, 0.83) * (1.0 - bellySeam * 0.4), belly);
                float relief = lerp(crown * 0.045 - seam * 0.02, -bellySeam * 0.018, belly);
                return half4(scales, relief);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                half waterHighlight = 0;
                if (_LivingWater > 0)
                {
                    float wave = WaterWave(IN.positionWS.xz, IN.waterMotion);
                    float3 surfaceNormal = normalize(cross(ddy(IN.positionWS), ddx(IN.positionWS)));
                    normalWS = surfaceNormal.y < 0 ? -surfaceNormal : surfaceNormal;
                    // Fine travelling highlights remain visible even on the coarse low-poly mesh.
                    float ripple = lerp(sin(dot(IN.positionWS.xz, float2(1.6, 0.7)) - _Time.y * 1.4),
                        sin(IN.waterMotion.z * 3.4 - _Time.y * 4.9), IN.waterMotion.w);
                    waterHighlight = smoothstep(0.72, 0.98, ripple) * 0.14 + wave * 0.7;
                }
                half4 scales = half4(1, 1, 1, 0);
                if (_SerpentScales > 0.0)
                {
                    scales = SerpentSurface(IN.uv);
                    // Surface-gradient relief adds small, matte scale facets to all lighting,
                    // including the sanctuary torches, without a glossy specular highlight.
                    float3 dx = ddx(IN.positionWS), dy = ddy(IN.positionWS);
                    float3 acrossX = cross(dy, normalWS), acrossY = cross(normalWS, dx);
                    float determinant = dot(dx, acrossX);
                    float3 gradient = (acrossX * ddx(scales.a) + acrossY * ddy(scales.a))
                        * sign(determinant) / max(abs(determinant), 0.000001);
                    normalWS = normalize(normalWS - gradient * _SerpentScales);
                }
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = dot(normalWS, mainLight.direction);
                float lit = saturate(NdotL);
                float band = smoothstep(_ShadowBandThreshold - _ShadowBandSmooth, _ShadowBandThreshold + _ShadowBandSmooth, lit);
                band *= mainLight.shadowAttenuation;

                half3 texCol = SampleTriplanar(IN.positionWS, normalWS, _TriplanarTileSize);
                texCol = lerp(half3(1, 1, 1), texCol, _TexInfluence);
                half3 albedo = texCol * _BaseColor.rgb;
                albedo *= lerp(half3(1, 1, 1), scales.rgb, _SerpentScales);

                half3 litColor = albedo * mainLight.color.rgb;
                half3 shadowedColor = albedo * _ShadowColor.rgb;
                half3 color = lerp(shadowedColor, litColor, band);

                half3 ambient = SampleSH(normalWS) * albedo;
                color += ambient * _AmbientBoost;
                float cave = max(InCave(IN.positionWS, _CaveBounds), max(InCave(IN.positionWS, _HollowBounds), InCave(IN.positionWS, _SanctuaryBounds)));
                color *= lerp(1.0, 0.23, cave);

                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                int pixelLightCount = GetAdditionalLightsCount();
                for (int i = 0; i < pixelLightCount; i++)
                {
                    Light addLight = GetAdditionalLight(i, IN.positionWS);
                    float addNdotL = saturate(dot(normalWS, addLight.direction));
                    color += albedo * addLight.color.rgb * addNdotL * addLight.distanceAttenuation * addLight.shadowAttenuation;
                }
                #endif

                // Manual point lights - simple windowed inverse-square falloff,
                // matching Unity's own smooth range cutoff formula, entirely
                // independent of the URP additional-lights keyword machinery.
                for (int m = 0; m < _ManualLightCount; m++)
                {
                    float3 lightPosWS = _ManualLightPosRange[m].xyz;
                    float lightRange = max(_ManualLightPosRange[m].w, 0.001);
                    float3 toLight = lightPosWS - IN.positionWS;
                    float distSq = dot(toLight, toLight);
                    float rangeSq = lightRange * lightRange;

                    // Most fragments on screen sit outside most torches' reach (the manual light
                    // list is shared globally, not per-object), so skip the sqrt/divide below for
                    // an out-of-range light entirely rather than paying for it and multiplying by a
                    // zero falloff - this is the hot loop for every opaque pixel on screen.
                    if (distSq >= rangeSq)
                        continue;

                    distSq = max(distSq, 1e-4);
                    float dist = sqrt(distSq);
                    float3 lightDir = toLight / dist;

                    float distFrac = distSq / rangeSq;
                    float distanceAtten = 1.0 - distFrac * distFrac;
                    distanceAtten = distanceAtten * distanceAtten;
                    float invSq = 1.0 / distSq;

                    float mNdotL = saturate(dot(normalWS, lightDir));
                    half3 mLightColor = _ManualLightColorIntensity[m].rgb * _ManualLightColorIntensity[m].a;
                    color += albedo * mLightColor * mNdotL * invSq * distanceAtten;
                }

                #if !defined(_MOBILE_LITE)
                float3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);
                float rim = 1.0 - saturate(dot(viewDir, normalWS));
                rim = pow(rim, _RimPower) * _RimIntensity * band;
                color += _RimColor.rgb * rim * lerp(1.0, 0.23, cave);
                #endif

                color += waterHighlight * half3(0.45, 0.75, 0.8);
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

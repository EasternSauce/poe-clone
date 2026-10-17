Shader "PoeClone/ReflectiveWater"
{
    Properties
    {
        _BaseColor ("Deep Water", Color) = (0.13, 0.17, 0.15, 1)
        _WaterOcean ("Ocean Swell", Range(0,1)) = 0
        _Waterfall ("Falling Sheet", Range(0,1)) = 0
        _RainIntensity ("Rain Ripples", Range(0,1)) = 0
        _Puddle ("Shallow Puddle", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _WaterOcean;
                float _Waterfall;
                float _RainIntensity;
                float _Puddle;
            CBUFFER_END
            TEXTURE2D(_WaterReflection); SAMPLER(sampler_WaterReflection);
            float4x4 _WaterReflectionVP;
            float _WaterReflectionHeight, _WaterReflectionValid;
            TEXTURE2D(_PuddleReflection); SAMPLER(sampler_PuddleReflection);
            float4x4 _PuddleReflectionVP;
            float _PuddleReflectionHeight, _PuddleReflectionValid;
            float4 _CaveBounds, _HollowBounds, _SanctuaryBounds, _WorksBounds, _WardBounds, _RootBounds;
            float4 _ManualLightPosRange[24], _ManualLightColorIntensity[24];
            int _ManualLightCount;

            float InCave(float3 p, float4 bounds)
            {
                float2 d = abs(p.xz - bounds.xy);
                return bounds.z > 0 && d.x < bounds.z && d.y < bounds.w ? 1 : 0;
            }
            float3 SceneWorld(float2 uv)
            {
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 motion : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 motion : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float fog : TEXCOORD4;
                float baseHeight : TEXCOORD5;
            };

            // Two wind wave trains cross slowly on lakes. River phase follows authored
            // distance downstream, so motion follows bends rather than a global texture scroll.
            float Wave(float2 p, float4 flow)
            {
                float t = _Time.y;
                float wind = sin(dot(p, float2(0.48, 0.31)) - t * 0.85) * 0.035 +
                             sin(dot(p, float2(-0.27, 0.63)) - t * 0.61) * 0.018;
                float across = dot(p, float2(-flow.y, flow.x));
                float river = sin(flow.z * 2.2 + sin(across * 0.65) * 0.7 - t * 3.5) * 0.035;
                return lerp(wind * (1 + _WaterOcean * 1.8), river, flow.w);
            }

            float2 RainHash(float2 cell)
            {
                float3 p = frac(cell.xyx * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yzx + 33.33);
                return frac((p.xx + p.yz) * p.zy);
            }

            // Overlapping expanding wave fronts perturb both reflected and refracted light.
            // World coordinates keep impacts stationary across water meshes and chunk seams.
            float2 RainSlope(float2 p, out float rings)
            {
                float2 grid = p / 1.6;
                // Rings reach less than half a cell: only the four nearest cells can contribute.
                float2 baseCell = floor(grid - 0.5);
                float2 slope = 0;
                rings = 0;
                [unroll] for (int y = 0; y <= 1; y++)
                    [unroll] for (int x = 0; x <= 1; x++)
                    {
                        float2 cell = baseCell + float2(x, y);
                        float2 seed = RainHash(cell);
                        float clock = _Time.y * 1.05 + seed.x * 9;
                        float cycle = floor(clock);
                        float age = frac(clock);
                        float2 hit = cell + 0.18 + RainHash(cell + cycle * 17.3) * 0.64;
                        float2 delta = (grid - hit) * 1.6;
                        float distance = length(delta);
                        float front = distance - age * 0.7;
                        float envelope = exp(-abs(front) * 32) * smoothstep(0, 0.08, age) * (1 - age);
                        // Some cells miss each cycle, avoiding a uniformly boiling surface.
                        envelope *= step(1 - _RainIntensity, seed.y);
                        slope += delta / max(distance, 0.015) * sin(front * 65) * envelope * 0.12;
                        rings += envelope * 0.09;
                    }
                return slope;
            }
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 p = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.baseHeight = p.y;
                float3 n = TransformObjectToWorldNormal(IN.normalOS);
                if (_Waterfall < 0.5) p.y += Wave(p.xz, IN.motion) * (1 - _Puddle);
                else p += n * sin(IN.uv.y * 24 - _Time.y * 8 + IN.uv.x * 13) * 0.04;
                OUT.positionWS = p;
                OUT.positionCS = TransformWorldToHClip(p);
                OUT.normalWS = n;
                OUT.motion = IN.motion;
                OUT.uv = IN.uv;
                OUT.fog = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float2 p = IN.positionWS.xz;
                float t = _Time.y;
                float2 flow = IN.motion.xy;
                float2 across = float2(-flow.y, flow.x);
                float current = IN.motion.z * 2.2 + sin(dot(p, across) * 0.65) * 0.7 - t * 3.5;
                float2 windSlope = float2(0.48, 0.31) * cos(dot(p, float2(0.48, 0.31)) - t * 0.85) * 0.12 +
                    float2(-0.27, 0.63) * cos(dot(p, float2(-0.27, 0.63)) - t * 0.61) * 0.08;
                float2 riverSlope = flow * cos(current) * 0.15 + across * sin(dot(p, across) * 3.7 + IN.motion.z - t * 2.5) * 0.045;
                float2 slope = lerp(windSlope * (1 + _WaterOcean), riverSlope, IN.motion.w);
                slope *= lerp(1, 0.08, _Puddle);
                float rainRings = 0;
                if (_RainIntensity > 0 && _Waterfall < 0.5) slope += RainSlope(p, rainRings);
                float streak = 0;
                float3 normal = normalize(float3(-slope.x, 1, -slope.y));
                if (_Waterfall > 0.5)
                {
                    // UV.y increases down the sheet; accelerating phase stretches falling ribbons.
                    float phase = sqrt(max(IN.uv.y, 0.001)) * 30 - t * 7;
                    streak = smoothstep(0.25, 0.85, sin(IN.uv.x * 57 + sin(phase) * 1.3)) *
                             (0.4 + 0.6 * sin(phase * 0.7) * sin(phase * 0.7));
                    normal = normalize(IN.normalWS + float3(flow.x, 0.2, flow.y) * sin(phase + IN.uv.x * 37) * 0.15);
                    normal *= IS_FRONT_VFACE(facing, 1, -1);
                }
                float3 view = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float fresnel = 0.035 + 0.965 * pow(1 - saturate(dot(normal, view)), 5);
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                float3 behind = SceneWorld(screenUV);
                float depth = max(0, IN.positionWS.y - behind.y);
                float2 distortion = slope * 0.009;
                float2 refractedUV = saturate(screenUV + distortion);
                float3 refractedWorld = SceneWorld(refractedUV);
                if (refractedWorld.y > IN.positionWS.y + 0.02) refractedUV = screenUV;
                half3 bed = SampleSceneColor(refractedUV);
                Light sun = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half3 illumination = SampleSH(normal) + sun.color * (0.25 + 0.75 * saturate(dot(normal, sun.direction))) * sun.shadowAttenuation;
                float cave = max(max(InCave(IN.positionWS, _CaveBounds), InCave(IN.positionWS, _WorksBounds)),
                    max(InCave(IN.positionWS, _HollowBounds), InCave(IN.positionWS, _SanctuaryBounds)));
                cave = max(cave, max(InCave(IN.positionWS, _WardBounds), InCave(IN.positionWS, _RootBounds)));
                illumination *= lerp(1, 0.23, cave);
                for (int i = 0; i < _ManualLightCount; i++)
                {
                    float3 delta = _ManualLightPosRange[i].xyz - IN.positionWS;
                    float distSq = max(dot(delta, delta), 0.0001);
                    float rangeSq = _ManualLightPosRange[i].w * _ManualLightPosRange[i].w;
                    if (distSq >= rangeSq) continue;
                    float attenuation = saturate(1 - pow(distSq / max(rangeSq, 0.0001), 2));
                    illumination += _ManualLightColorIntensity[i].rgb * _ManualLightColorIntensity[i].a *
                        saturate(dot(normal, normalize(delta))) * attenuation * attenuation / distSq;
                }
                half3 tint = _BaseColor.rgb * max(illumination, half3(0.12, 0.12, 0.12));
                // Clear shallows expose the sloping bed; long water paths absorb more light.
                float absorption = lerp(1 - exp(-depth * 0.12), 0.08, _Puddle);
                half3 color = lerp(bed, tint, _Waterfall > 0.5 ? 0.25 : absorption);
                float3 reflectedDirection = reflect(-view, normal);
                half3 reflected = GlossyEnvironmentReflection(reflectedDirection, 0.12, 1);
                float reflectionValid = _Puddle > 0.5 ? _PuddleReflectionValid : _WaterReflectionValid;
                float reflectionHeight = _Puddle > 0.5 ? _PuddleReflectionHeight : _WaterReflectionHeight;
                if (_Waterfall < 0.5 && reflectionValid > 0.5 && abs(IN.baseHeight - reflectionHeight) < 0.08)
                {
                    float4 position = float4(p.x, IN.baseHeight, p.y, 1);
                    float4 projected = _Puddle > 0.5 ? mul(_PuddleReflectionVP, position) : mul(_WaterReflectionVP, position);
                    float2 uv = projected.xy / projected.w * 0.5 + 0.5 + distortion;
                    float edge = saturate(min(min(uv.x, uv.y), min(1 - uv.x, 1 - uv.y)) * 25);
                    half3 sceneReflection;
                    if (_Puddle > 0.5)
                        sceneReflection = SAMPLE_TEXTURE2D(_PuddleReflection, sampler_PuddleReflection, saturate(uv)).rgb;
                    else
                        sceneReflection = SAMPLE_TEXTURE2D(_WaterReflection, sampler_WaterReflection, saturate(uv)).rgb;
                    reflected = lerp(reflected, sceneReflection, edge);
                }
                color = lerp(color, reflected, saturate(fresnel * 0.85 + lerp(0.18, 0.3, _Puddle)));
                float specular = pow(saturate(dot(normal, normalize(view + sun.direction))), 160) * sun.shadowAttenuation;
                color += sun.color * specular * 0.7 * lerp(1, 0.23, cave);
                float foam = _Waterfall > 0.5 ? streak * 0.65 + smoothstep(0.78, 1, IN.uv.y) * 0.3 :
                    (1 - smoothstep(0.03, 0.32, depth)) * (0.12 + 0.08 * sin(current + p.x * 3));
                foam *= 1 - _Puddle;
                color = lerp(color, half3(0.73, 0.75, 0.68) * max(illumination, half3(0.3, 0.3, 0.3)), saturate(foam));
                color += rainRings * max(illumination, half3(0.22, 0.22, 0.22));
                float edge = length(IN.uv * 2 - 1);
                float alpha = lerp(1, 1 - smoothstep(0.72, 1, edge), _Puddle);
                return half4(MixFog(color, IN.fog), alpha);
            }
            ENDHLSL
        }
    }
}

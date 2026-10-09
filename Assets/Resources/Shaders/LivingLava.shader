Shader "PoeClone/LivingLava"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 vertex : POSITION; };
            struct V { float4 position : SV_POSITION; float3 world : TEXCOORD0; float fog : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                o.world = TransformObjectToWorld(v.vertex.xyz);
                o.position = TransformWorldToHClip(o.world);
                o.fog = ComputeFogFactor(o.position.z);
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 a = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(a), hash(a + float2(1,0)), f.x),
                    lerp(hash(a + float2(0,1)), hash(a + 1), f.x), f.y);
            }
            half4 frag(V i) : SV_Target
            {
                float t = _Time.y;
                float2 direction = normalize(TransformObjectToWorldDir(float3(0.35,0,1)).xz);
                float2 p = i.world.xz;
                float2 flow = p * 1.3 - direction * t * 0.22;
                float2 warp = float2(noise(flow * 0.6 + 7), noise(flow * 0.6 - 13));
                float molten = noise(flow + warp * 2.1);
                float detail = noise(flow * 3.1 + warp - direction * t * 0.13);
                float seams = 1 - smoothstep(0.025, 0.15, abs(molten - 0.5));
                float crust = smoothstep(0.55, 0.77, molten + detail * 0.14);
                half3 color = lerp(half3(0.72,0.055,0.008), half3(2.4,0.87,0.10), seams);
                color = lerp(color, half3(0.12,0.018,0.009), crust * 0.92);
                // Independent cells swell slowly, then pop into expanding rings.
                float2 cell = floor(p * 1.8);
                float seed = hash(cell);
                float phase = frac(t * (0.18 + seed * 0.1) + seed * 9);
                float2 center = 0.25 + float2(hash(cell + 17), hash(cell - 31)) * 0.5;
                float2 delta = frac(p * 1.8) - center;
                float radius = lerp(0.035, 0.24, smoothstep(0, 0.7, phase));
                float distance = length(delta);
                float dome = (1 - smoothstep(radius * 0.6, radius, distance)) * (1 - smoothstep(0.69,0.75,phase));
                float rim = (1 - smoothstep(0.012,0.035,abs(distance - radius))) * (1 - smoothstep(0.7,0.79,phase));
                float ringRadius = 0.24 + max(0,phase - 0.73) * 0.85;
                float ring = (1 - smoothstep(0.008,0.03,abs(distance - ringRadius)))
                    * smoothstep(0.72,0.77,phase) * (1 - smoothstep(0.8,1,phase));
                color = lerp(color, half3(0.42,0.032,0.006), dome * 0.85);
                float glint = pow(saturate(1 - length((delta + float2(0.05,-0.06)) / max(radius,0.03))), 5) * dome;
                color += half3(1.8,0.70,0.13) * (rim * 0.65 + ring * 0.5 + glint);
                // Slowly changing highlights make the molten channels read as viscous liquid.
                color += half3(0.36,0.16,0.045) * pow(saturate(detail), 5) * (1 - crust);
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }
    }
}

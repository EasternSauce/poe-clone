Shader "PoeClone/GroundTelegraph"
{
    // An animated warning decal for GroundTelegraph. The mesh is a flat quad: a disc (-1..1, scaled by
    // the radius) clipped to a circle, ring or wedge, or a strip (x -0.5..0.5, z 0..1, scaled by width
    // and length). Textures are procedural and in world space, so they never stretch with the shape.
    Properties
    {
        _WarnColor ("Warning", Color) = (0.3,0.05,0.03,1)
        _FillColor ("Fill", Color) = (1,0.45,0.1,1)
        _Progress ("Progress", Range(0,1)) = 0
        _Type ("Pattern (0 fire, 1 cold, 2 lightning, 3 physical)", Float) = 0
        _Shape ("Shape (0 disc, 1 strip)", Float) = 0
        _Inner ("Inner radius fraction", Float) = 0
        _HalfAngle ("Half angle (radians)", Float) = 3.1416
        _Size ("Radius or length", Float) = 1
        _Width ("Strip width", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-50" "IgnoreProjector"="True" }
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

            CBUFFER_START(UnityPerMaterial)
            float4 _WarnColor, _FillColor;
            float _Progress, _Type, _Shape, _Inner, _HalfAngle, _Size, _Width;
            CBUFFER_END

            struct A { float4 vertex : POSITION; };
            struct V { float4 position : SV_POSITION; float3 local : TEXCOORD0; float3 world : TEXCOORD1; float fog : TEXCOORD2; };

            V vert(A v)
            {
                V o;
                o.local = v.vertex.xyz;
                o.world = TransformObjectToWorld(v.vertex.xyz);
                o.position = TransformWorldToHClip(o.world);
                o.fog = ComputeFogFactor(o.position.z);
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 hash2(float2 p) { return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 a = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(a), hash(a + float2(1, 0)), f.x),
                    lerp(hash(a + float2(0, 1)), hash(a + 1), f.x), f.y);
            }
            float fbm(float2 p) { return noise(p) * 0.55 + noise(p * 2.03 + 5.1) * 0.3 + noise(p * 4.1 - 3.7) * 0.15; }
            // x: distance to the nearest cell centre, y: distance to the nearest cell border, z: that cell's id.
            float3 voronoi(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                float d1 = 8, d2 = 8, id = 0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float d = length(g + hash2(cell + g) - f);
                    if (d < d1) { d2 = d1; d1 = d; id = hash(cell + g); }
                    else if (d < d2) d2 = d;
                }
                return float3(d1, d2 - d1, id);
            }

            // Each pattern returns x: body texture (0..1), y: bright accent lines/sparks (0..1).
            // Fire: rolling flames with drifting embers.
            float2 fire(float2 p, float t)
            {
                float2 q = p * 0.9 + float2(0, -t * 0.7);
                float2 warp = float2(fbm(q + 3.1), fbm(q - 7.7));
                float flame = fbm(q * 1.4 + warp * 1.6);
                float2 ep = p * 3 + float2(0, -t * 1.6);
                float spark = hash(floor(ep));
                float ember = step(0.93, spark) * (1 - smoothstep(0.05, 0.22, length(frac(ep) - 0.5)))
                    * (0.5 + 0.5 * sin(t * 9 + spark * 40));
                return float2(smoothstep(0.3, 0.8, flame), ember + smoothstep(0.62, 0.8, flame) * 0.5);
            }
            // Cold: frost crystals with a shimmer sweeping across.
            float2 cold(float2 p, float t)
            {
                float3 v = voronoi(p * 1.4);
                float facet = 0.35 + 0.65 * v.z;
                float crack = 1 - smoothstep(0.0, 0.07, v.y);
                float shimmer = pow(saturate(sin(dot(p, float2(0.7, 0.5)) * 1.3 - t * 2.2)), 12);
                float frost = smoothstep(0.55, 0.9, noise(p * 6 + v.z * 10));
                return float2(facet * 0.7 + frost * 0.3, crack * 0.8 + shimmer * facet);
            }
            // Lightning: arcs that jump to new paths several times a second.
            float2 lightning(float2 p, float t)
            {
                float step1 = floor(t * 11), step2 = floor(t * 7 + 0.5);
                float a = noise(p * 1.1 + step1 * 3.17) + noise(p * 2.6 - step1 * 1.3) * 0.35;
                float b = noise(p * 1.5 - step2 * 2.71 + 9) + noise(p * 3.3 + step2) * 0.3;
                float arcA = 1 - smoothstep(0.0, 0.035, abs(a - 0.67));
                float arcB = 1 - smoothstep(0.0, 0.03, abs(b - 0.65));
                float flicker = 0.75 + 0.25 * hash(float2(step1, 1.7));
                float haze = noise(p * 0.8 + t * 0.3);
                return float2(haze * flicker, saturate(arcA + arcB * 0.7) * flicker);
            }
            // Physical: cracked earth whose cracks glow once filled, and drifting grit.
            float2 physical(float2 p, float t)
            {
                float3 c = voronoi(p * 0.9);
                float crack = 1 - smoothstep(0.0, 0.05 + 0.04 * noise(p * 4), c.y);
                float grit = noise(p * 7 + float2(t * 0.4, 0)) * noise(p * 3.1 - t * 0.2);
                float slab = 0.55 + 0.45 * c.z;
                return float2(slab * 0.7 + grit * 0.6, crack);
            }

            float2 pattern(float2 p, float t)
            {
                float2 result;
                if (_Type < 0.5) result = fire(p, t);
                else if (_Type < 1.5) result = cold(p, t);
                else if (_Type < 2.5) result = lightning(p, t);
                else result = physical(p, t);
                return result;
            }

            half4 frag(V i) : SV_Target
            {
                float t = _Time.y;
                float2 o = i.local.xz;
                float edge, along, span;
                if (_Shape < 0.5)
                {
                    float r = length(o);
                    float outer = (1 - r) * _Size;
                    float inner = _Inner > 0.001 ? (r - _Inner) * _Size : 1e3;
                    float angle = abs(atan2(o.x, o.y));
                    float side = _HalfAngle < 3.1 ? r * _Size * sin(clamp(_HalfAngle - angle, -1.5, 1.5)) : 1e3;
                    edge = min(outer, min(inner, side));
                    along = saturate((r - _Inner) / max(1 - _Inner, 0.001));
                    span = _Size * (1 - _Inner);
                }
                else
                {
                    edge = min((0.5 - abs(o.x)) * _Width, min(o.y, 1 - o.y) * _Size);
                    along = o.y;
                    span = _Size;
                }
                float aa = saturate(edge / max(fwidth(edge), 1e-4));
                clip(aa - 0.001);

                // How far behind the advancing fill this point is, in metres.
                float front = (_Progress - along) * span;
                float filled = smoothstep(-0.04, 0.04, front);
                float2 pat = pattern(i.world.xz, t);
                float urgency = smoothstep(0.7, 1.0, _Progress);

                half3 warn = _WarnColor.rgb * (0.75 + 0.7 * pat.x) + _FillColor.rgb * pat.y * 0.35;
                // Faint ripples running in towards the advancing edge.
                float ripple = pow(1 - abs(frac(along * span * 0.55 + t * (1.1 + urgency * 1.5)) - 0.5) * 2, 6);
                warn += _FillColor.rgb * ripple * 0.18 * (1 - filled);
                half3 fill = _FillColor.rgb * (0.55 + 0.6 * pat.x) + lerp(_FillColor.rgb, 1, 0.5) * pat.y * 0.6;
                half3 color = lerp(warn, fill, filled);
                float alpha = lerp(0.5 + 0.2 * pat.x + 0.2 * pat.y, 0.72 + 0.15 * pat.x, filled);

                // A bright band riding the fill's leading edge.
                float band = exp(-abs(front) * 7) * step(_Progress, 0.999) * step(0.001, _Progress);
                color += lerp(_FillColor.rgb, 1, 0.4) * band * 0.9;
                alpha = max(alpha, band * 0.9);

                // Outline that pulses faster as the blast gets close.
                float pulse = 0.65 + 0.35 * sin(t * lerp(5, 20, _Progress));
                float rim = exp(-edge * 7);
                color = lerp(color, lerp(_FillColor.rgb, 1, 0.3) * (0.8 + 0.5 * pulse), rim * 0.85);
                alpha = max(alpha, rim * (0.7 + 0.3 * pulse));

                color *= 1 + urgency * 0.35 * (0.5 + 0.5 * sin(t * 30));
                return half4(MixFog(color, i.fog), saturate(alpha) * aa * 0.95);
            }
            ENDHLSL
        }
    }
}

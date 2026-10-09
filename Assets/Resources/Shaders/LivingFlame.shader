Shader "PoeClone/LivingFlame"
{
    Properties
    {
        _FlameColor ("Edge", Color) = (1,0.28,0.025,1)
        _CoreColor ("Core", Color) = (1,0.91,0.46,1)
        _Seed ("Phase", Float) = 0
        _Wind ("Wind", Vector) = (0,0,0,0)
    }
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
            CBUFFER_START(UnityPerMaterial)
                half4 _FlameColor, _CoreColor;
                float _Seed;
                float4 _Wind;
            CBUFFER_END
            struct A { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                o.position = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.position.z);
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 a = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(a), hash(a + float2(1,0)), f.x),
                    lerp(hash(a + float2(0,1)), hash(a + 1), f.x), f.y);
            }
            half4 frag(V i) : SV_Target
            {
                float y = i.uv.y;
                float t = _Time.y * 3.8 + _Seed;
                // Detail travels upwards; only the tip curls, leaving the base anchored.
                float flow = noise(float2(i.uv.x * 5 + _Seed, y * 5 - t));
                float fine = noise(float2(i.uv.x * 11 - _Seed, y * 9 - t * 1.7));
                float bend = (flow - 0.5) * 0.30 * y * y
                    + clamp(_Wind.x + _Wind.y, -1.0, 1.0) * y * y * 0.10;
                float x = abs(i.uv.x - 0.5 - bend);
                float width = lerp(0.12, 0.34, smoothstep(0, 0.18, y)) * pow(saturate(1 - y), 0.8);
                width *= 0.8 + flow * 0.28 + fine * 0.14;
                float edge = 1 - smoothstep(width * 0.58, width + 0.018, x);
                float tip = 1 - smoothstep(0.70 + flow * 0.21, 0.99, y);
                float alpha = edge * tip * smoothstep(0, 0.07, y) * 0.85;
                float core = (1 - smoothstep(0, max(0.01, width * 0.8), x)) * (1 - smoothstep(0.20, 0.74, y));
                half3 color = lerp(_FlameColor.rgb, _CoreColor.rgb, core) * 1.5;
                return half4(MixFog(color, i.fog), alpha);
            }
            ENDHLSL
        }
    }
}

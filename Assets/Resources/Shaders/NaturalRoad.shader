Shader "PoeClone/NaturalRoad"
{
    Properties
    {
        _BaseMap ("Packed Earth", 2D) = "white" {}
        _BaseColor ("Earth Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct A { float4 position : POSITION; float2 uv : TEXCOORD0; float2 verge : TEXCOORD1; };
            struct V { float4 position : SV_POSITION; float3 world : TEXCOORD0; float2 uv : TEXCOORD1; float edge : TEXCOORD2; float fog : TEXCOORD3; };
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + 1), f.x), f.y);
            }
            V vert(A v)
            {
                V o;
                o.world = TransformObjectToWorld(v.position.xyz);
                o.position = TransformWorldToHClip(o.world);
                o.uv = v.uv;
                o.edge = v.verge.x;
                o.fog = ComputeFogFactor(o.position.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float2 p = i.world.xz;
                // World mapping keeps the grain the same size through bends and wide junctions.
                half3 soil = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p / 4).rgb;
                half3 detail = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p * 0.83 + float2(0.31,0.67)).rgb;
                soil = lerp(soil, detail, 0.3) * _BaseColor.rgb;
                float grain = Noise(p * 19);
                float mottling = Noise(p * 0.7);
                // Interrupted shallow wear, rather than perfectly ruled wheel marks.
                float across = i.uv.x + (Noise(p * 0.55) - 0.5) * 0.055;
                float wear = exp(-pow((across - 0.28) * 17, 2)) + exp(-pow((across - 0.72) * 17, 2));
                soil *= (0.91 + mottling * 0.15 + grain * 0.07) * (1 - wear * (0.025 + Noise(p * 1.2) * 0.055));
                float3 normal = normalize(float3((Noise(p * 8 + float2(0.05,0)) - Noise(p * 8 - float2(0.05,0))) * 0.3,
                    1, (Noise(p * 8 + float2(0,0.05)) - Noise(p * 8 - float2(0,0.05))) * 0.3));
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 light = SampleSH(normal) + sun.color * saturate(dot(normal, sun.direction)) * sun.shadowAttenuation;
                float erosion = Noise(p * 3.7) * 0.15 + Noise(p * 13) * 0.06;
                float alpha = smoothstep(0.02, 0.38, i.edge - erosion);
                return half4(MixFog(soil * light, i.fog), alpha);
            }
            ENDHLSL
        }
    }
}

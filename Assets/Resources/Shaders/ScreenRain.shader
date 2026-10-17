Shader "PoeClone/ScreenRain"
{
    Properties { _Intensity ("Rain intensity", Range(0,1)) = 0.7 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay-100" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Drops(float2 uv, float density, float speed, float seed)
            {
                // Coordinates measured in screen heights keep the rain equally visible in portrait.
                float2 p = float2(uv.x * _ScreenParams.x / _ScreenParams.y, uv.y);
                p.x += p.y * 0.16;
                p.x *= density;
                // Stagger each column's fall so streaks never form horizontal rows.
                p.y = (p.y + _Time.y * speed + Hash(float2(floor(p.x), seed)) * 7) * 15;
                float2 cell = floor(p);
                float random = Hash(cell + seed);
                float2 local = frac(p);
                float width = max(fwidth(p.x), 0.015);
                float streak = 1 - smoothstep(width * 0.35, width * 1.15, abs(local.x - lerp(0.15, 0.85, random)));
                float tail = smoothstep(0.05, 0.15, local.y) * (1 - smoothstep(0.24, 0.65, local.y));
                // Leave most cells empty: readable individual drops, with clear space between them.
                return streak * tail * step(0.7, random) * lerp(0.3, 0.65, random);
            }
            half4 frag(V i) : SV_Target
            {
                float rain = Drops(i.uv, 72, 0.9, 3) + Drops(i.uv, 40, 1.45, 17) * 0.65;
                return half4(0.68, 0.77, 0.84, saturate(rain * _Intensity * 0.85));
            }
            ENDHLSL
        }
    }
}

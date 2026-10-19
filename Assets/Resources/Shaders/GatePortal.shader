Shader "PoeClone/GatePortal"
{
    Properties { _BaseColor ("Destination tint", Color) = (0.18,0.65,0.85,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct A { float4 vertex : POSITION; };
            struct V { float4 position : SV_POSITION; float2 local : TEXCOORD0; float fog : TEXCOORD1; };
            V vert(A v)
            {
                V o; o.position = TransformObjectToHClip(v.vertex.xyz);
                o.local = v.vertex.xy; o.fog = ComputeFogFactor(o.position.z); return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 c = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(hash(c),hash(c+float2(1,0)),f.x),lerp(hash(c+float2(0,1)),hash(c+1),f.x),f.y);
            }
            half4 frag(V i) : SV_Target
            {
                float t = _Time.y;
                float2 p = (i.local - float2(0,2.4)) / float2(2.14,2.3);
                float r = length(p), angle = atan2(p.y,p.x);
                // Rotating domain keeps the field seamless across atan2's branch cut.
                float twist = r * 3.6 - t * 0.32;
                float sn = sin(twist), cs = cos(twist);
                float2 q = float2(cs*p.x-sn*p.y,sn*p.x+cs*p.y);
                float mist = noise(q*3.8 + float2(0,t*0.12));
                float detail = noise(q*8.5 + mist*2 - t*0.18);
                float filaments = pow(saturate(1-abs(sin(r*19-angle*4+t*1.5+mist*4))),10);
                float eddy = pow(saturate(detail*0.65+mist*0.55),3);
                float edgeDistance = min(2.14-abs(i.local.x), i.local.y-0.18);
                if (i.local.y > 2.45) edgeDistance = (1-length(float2(i.local.x/2.14,(i.local.y-2.45)/2.0972)))*2.14;
                float rim = exp(-max(0,edgeDistance)*18);
                float halo = exp(-max(0,edgeDistance)*4);
                float pulse = 0.85+0.15*sin(t*1.8+angle*3);
                half3 tint = _BaseColor.rgb;
                half3 color = tint*(0.045 + eddy*0.7 + halo*0.3);
                color += lerp(tint,half3(0.6,0.8,1),0.35)*filaments*smoothstep(0.05,0.35,r)*0.5;
                color += lerp(tint,half3(0.8,1,1),0.65)*rim*1.8*pulse;
                // Small drifting stars have separate phases; no particles or per-frame scripts.
                float2 stars = p*11 + float2(t*0.12,-t*0.23);
                float2 cell = floor(stars);
                float star = pow(saturate(1-length(frac(stars)-0.5)*7),5);
                color += half3(0.65,0.95,1)*star*step(0.78,hash(cell))*pow(0.5+0.5*sin(t*2+hash(cell)*30),2);
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}

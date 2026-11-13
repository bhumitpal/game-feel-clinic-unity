Shader "Game Feel Clinic/Boathouse Sky"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Interpolators { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Interpolators Vert(Attributes i)
            {
                Interpolators o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }
            half4 Frag(Interpolators i) : SV_Target
            {
                float2 p = i.uv;
                float up = smoothstep(.29, .86, p.y);
                float3 ink = lerp(float3(.95, .66, .45), float3(.25, .55, .76), up);
                float2 sunVector = (p - float2(.56, .46)) * float2(2.2, 1);
                float radius = length(sunVector);
                ink += exp(-radius * 11) * float3(.44, .28, .08);
                ink += (1 - smoothstep(.027, .033, radius)) * float3(.95, .79, .44);
                float cloud = sin(p.x * 36 + sin(p.y * 67) * .32)
                            + .62 * sin(p.x * 64 + p.y * 17) + .39 * sin(p.x * 114 + p.y * 91);
                float stripe = saturate(1 - abs(p.y - (.57 + .035 * sin(p.x * 17))) * 19);
                float wisp = smoothstep(.5, 1.45, cloud) * stripe;
                ink = lerp(ink, float3(1, .83, .68), wisp * .45);
                return half4(ink, 1);
            }
            ENDHLSL
        }
    }
}

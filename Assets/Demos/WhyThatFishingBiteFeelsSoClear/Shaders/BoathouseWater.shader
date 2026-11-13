Shader "Game Feel Clinic/Boathouse Water"
{
    Properties
    {
        _FlowTime("Episode time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _FlowTime;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Interpolators { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            float Height(float2 xz)
            {
                float a = dot(xz, float2(1.6, 1.25)) + _FlowTime * 2.3;
                float b = dot(xz, float2(3.1, -2.8)) + _FlowTime * 4.3;
                return sin(a) * .045 + sin(b) * .022;
            }

            Interpolators Vert(Attributes input)
            {
                Interpolators o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionWS.y += Height(o.positionWS.xz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 Frag(Interpolators i) : SV_Target
            {
                float2 p = i.positionWS.xz;
                float a = dot(p, float2(1.6, 1.25)) + _FlowTime * 2.3;
                float b = dot(p, float2(3.1, -2.8)) + _FlowTime * 4.3;
                float c = dot(p, float2(8.2, 7.2)) + _FlowTime * 5.2;
                float d = dot(p, float2(-11.5, 4.7)) + _FlowTime * 3.5;
                float2 slopes = cos(a) * float2(1.6, 1.25) * .045 + cos(b) * float2(3.1, -2.8) * .022;
                slopes += cos(c) * float2(.049, .035) + cos(d) * float2(-.037, .024);
                float3 n = normalize(float3(-slopes.x, 1, -slopes.y));
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
                float fresnel = pow(1 - saturate(dot(n, v)), 2.2);
                float3 r = reflect(-v, n);
                float3 sun = normalize(float3(-.24, .28, .93));
                float reflection = saturate(dot(r, sun));
                float band = .5 + .5 * sin(a * 1.7 + sin(b) * .4);
                float glint = pow(reflection, 46) * lerp(.2, 1, pow(band, 2));
                float fine = pow(saturate(.5 + .5 * sin(c + .7 * sin(d))), 12);
                float bank = exp(-pow((p.x + 1.2) * .39, 2)) * .32;
                float3 under = lerp(float3(.014, .106, .149), float3(.032, .190, .174), bank + band * .22);
                float sunPath = exp(-abs(p.x - (.9 + p.y * .08)) * .46);
                float3 sky = lerp(float3(.145, .338, .411), float3(.71, .50, .31), pow(reflection, 13) * sunPath * .70);
                float3 color = lerp(under, sky, saturate(.13 + fresnel * .78));
                color += float3(1.1, .64, .25) * glint * .31 * sunPath;
                color += fine * float3(.025, .045, .044) * (.3 + fresnel);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}

Shader "Game Feel Clinic/Responsive Smoke Volume"
{
    Properties
    {
        _FogColor ("Blue fog", Color) = (0.24, 0.43, 0.64, 1)
        _LightColor ("Cool highlights", Color) = (0.79, 0.91, 1, 1)
        _Density ("Extinction per world unit", Range(0, 6)) = 1.8
        _WholeOpacity ("Whole cloud opacity multiplier", Range(0, 1)) = 1
        _CutA ("Cut start, world position", Vector) = (0, 1.6, -2.2, 0)
        _CutB ("Cut end, world position", Vector) = (0, 1.6, 2.2, 0)
        _CutRadius ("Cut radius, world units", Float) = 0.72
        _CutSoftness ("Cut edge softness, world units", Float) = 0.24
        _CutStrength ("Cut strength", Range(0, 1)) = 0
        _FogTime ("Fog clock, seconds", Float) = 0
        _NoiseScale ("Noise frequency per world unit", Float) = 1.7
        _Wind ("Noise drift, world units per second", Vector) = (0.16, 0.08, 0.11, 0)
        _Steps ("Ray samples", Range(8, 64)) = 32
        [Toggle] _UseSceneDepth ("Clip against opaque scene depth", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "ResponsiveSmoke"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest Always
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float4 _LightColor;
                float4 _CutA;
                float4 _CutB;
                float4 _Wind;
                float _Density;
                float _WholeOpacity;
                float _CutRadius;
                float _CutSoftness;
                float _CutStrength;
                float _FogTime;
                float _NoiseScale;
                float _Steps;
                float _UseSceneDepth;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
                Varyings output;
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                return output;
            }

            float LatticeHash(float3 cell)
            {
                float3 value = frac(cell * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float ValueNoise(float3 position)
            {
                float3 cell = floor(position);
                float3 weight = frac(position);
                weight = weight * weight * (3.0 - 2.0 * weight);

                float lowerLeft = lerp(LatticeHash(cell), LatticeHash(cell + float3(1, 0, 0)), weight.x);
                float lowerRight = lerp(LatticeHash(cell + float3(0, 1, 0)), LatticeHash(cell + float3(1, 1, 0)), weight.x);
                float upperLeft = lerp(LatticeHash(cell + float3(0, 0, 1)), LatticeHash(cell + float3(1, 0, 1)), weight.x);
                float upperRight = lerp(LatticeHash(cell + float3(0, 1, 1)), LatticeHash(cell + float3(1, 1, 1)), weight.x);
                return lerp(lerp(lowerLeft, lowerRight, weight.y), lerp(upperLeft, upperRight, weight.y), weight.z);
            }

            float DistanceToSegment(float3 position, float3 start, float3 end)
            {
                float3 axis = end - start;
                float along = saturate(dot(position - start, axis) / max(dot(axis, axis), 1e-8));
                return length(position - (start + axis * along));
            }

            float CutMultiplier(float3 positionWS, float radius, float softness, float strength)
            {
                float distanceToCut = DistanceToSegment(positionWS, _CutA.xyz, _CutB.xyz);
                float keep = smoothstep(max(0.0, radius - softness), radius + softness, distanceToCut);
                return lerp(1.0, keep, strength);
            }

            float2 BoxInterval(float3 originOS, float3 directionOS)
            {

                float3 magnitude = max(abs(directionOS), 1e-7);
                float3 safeDirection = lerp(-magnitude, magnitude, step(0.0, directionOS));
                float3 first = (-0.5 - originOS) / safeDirection;
                float3 second = (0.5 - originOS) / safeDirection;
                float3 nearAxis = min(first, second);
                float3 farAxis = max(first, second);
                return float2(max(nearAxis.x, max(nearAxis.y, nearAxis.z)), min(farAxis.x, min(farAxis.y, farAxis.z)));
            }

            float ClampToOpaqueDepth(float exitDistance, float2 screenUV, float3 originWS, float3 directionWS)
            {
                float rawDepth = SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 1e-6)
                        return exitDistance;
                    float clipDepth = rawDepth;
                #else
                    if (rawDepth >= 1.0 - 1e-6)
                        return exitDistance;
                    float clipDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 opaquePositionWS = ComputeWorldSpacePosition(screenUV, clipDepth, UNITY_MATRIX_I_VP);
                return min(exitDistance, dot(opaquePositionWS - originWS, directionWS));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float wholeOpacity = saturate(_WholeOpacity);
                float baseDensity = max(0.0, _Density) * wholeOpacity;
                if (baseDensity <= 1e-4)
                    return 0;

                float3 directionWS = -GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 originWS = GetCameraPositionWS();
                if (unity_OrthoParams.w > 0.5)
                {

                    originWS = input.positionWS - directionWS * dot(input.positionWS - originWS, directionWS);
                }

                float3 originOS = TransformWorldToObject(originWS);
                float3 directionOS = mul((float3x3)GetWorldToObjectMatrix(), directionWS);
                float2 interval = BoxInterval(originOS, directionOS);
                float entry = max(0.0, interval.x);
                float exitDistance = interval.y;
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                if (_UseSceneDepth > 0.5)
                    exitDistance = ClampToOpaqueDepth(exitDistance, screenUV, originWS, directionWS);
                if (exitDistance <= entry)
                    return 0;

                int steps = (int)clamp(round(_Steps), 8.0, 64.0);
                float stepDistance = (exitDistance - entry) / steps;
                float jitter = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));
                float distanceAlongRay = entry + (0.15 + 0.7 * jitter) * stepDistance;
                float3 sampleWS = originWS + directionWS * distanceAlongRay;
                float3 sampleOS = originOS + directionOS * distanceAlongRay;
                float3 stepWS = directionWS * stepDistance;
                float3 stepOS = directionOS * stepDistance;
                float3 noiseOrigin = _Wind.xyz * _FogTime;
                float noiseScale = max(_NoiseScale, 0.01);
                float cutRadius = max(_CutRadius, 0.0);
                float cutSoftness = max(_CutSoftness, 1e-4);
                float cutStrength = saturate(_CutStrength);
                float transmission = 1.0;
                float3 fog = 0.0;

                [loop]
                for (int sampleIndex = 0; sampleIndex < steps; sampleIndex++)
                {
                    float3 faceDistance = 0.5 - abs(sampleOS);
                    float envelope = smoothstep(0.0, 0.13, min(faceDistance.x, min(faceDistance.y, faceDistance.z)));
                    float localDensity = baseDensity * envelope;
                    if (cutStrength > 1e-4)
                        localDensity *= CutMultiplier(sampleWS, cutRadius, cutSoftness, cutStrength);

                    if (localDensity > 1e-4)
                    {
                        float3 noisePoint = (sampleWS - noiseOrigin) * noiseScale * float3(1.0, 0.83, 1.0);
                        float coarse = ValueNoise(noisePoint);
                        float detail = ValueNoise(noisePoint * 2.07 + float3(13.7, 7.4, 19.3));
                        float pattern = 0.72 * coarse + 0.28 * detail;
                        localDensity *= 0.34 + 0.94 * smoothstep(0.18, 0.82, pattern);

                        float lighting = saturate(0.13 + 0.57 * pattern + 0.24 * (sampleOS.y + 0.5));
                        float3 tint = lerp(_FogColor.rgb, _LightColor.rgb, lighting);
                        float sampleTransmission = exp(-localDensity * stepDistance);
                        fog += tint * (transmission * (1.0 - sampleTransmission));
                        transmission *= sampleTransmission;
                        if (transmission < 0.008)
                            break;
                    }

                    sampleWS += stepWS;
                    sampleOS += stepOS;
                }

                return half4(fog, 1.0 - transmission);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

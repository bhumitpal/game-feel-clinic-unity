Shader "GameFeelClinic/CloudRelaySky"
{
    Properties
    {
        _Top ("Zenith", Color) = (.08,.12,.29,1)
        _Horizon ("Horizon", Color) = (.66,.45,.57,1)
        _Ground ("Lower sky", Color) = (.25,.34,.53,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Top, _Horizon, _Ground;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 clip : SV_POSITION; float3 ray : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.ray = v.vertex.xyz;
                return o;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                float3 direction = normalize(input.ray);
                float height = direction.y;
                float above = pow(saturate(height * 1.6), .42);
                float below = saturate(-height * 2.8);
                float3 sky = lerp(_Horizon.rgb, _Top.rgb, above);
                sky = lerp(sky, _Ground.rgb, below);
                float glow = pow(saturate(dot(direction, normalize(float3(-.48,.09,.88)))), 11);
                sky += float3(.19,.095,.035) * glow;
                float2 angles = float2(atan2(direction.x, direction.z), asin(height));
                float2 pixels = angles * 110;
                float2 starCell = floor(pixels);
                float hash = frac(sin(dot(starCell, float2(127.1, 311.7))) * 43758.5453);
                float star = step(.978, hash) * smoothstep(.18,.02,length(frac(pixels)-.5)) * smoothstep(.04,.28,height);
                sky += float3(.46,.60,.82) * star;
                return float4(sky,1);
            }
            ENDHLSL
        }
    }
}
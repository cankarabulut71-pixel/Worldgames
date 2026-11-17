// The sea-level sphere. Land rises above it; where the ground dips below, ponds appear.
// COLOR.r holds how deep the ground is under the water (0 at the shore, 1 deep); TEXCOORD1.x the biome index.
Shader "TinyPlanet/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.42, 0.88, 0.80, 1)
        _Deep ("Deep", Color) = (0.07, 0.46, 0.56, 1)
        _Horizon ("Grazing", Color) = (0.70, 0.97, 0.95, 1)
        _Foam ("Shore foam", Color) = (0.92, 1, 0.97, 1)
        _Ambient ("Ambient", Color) = (0.45, 0.50, 0.55, 1)
        _DormantTint ("Dormant tint", Color) = (0.55, 0.58, 0.62, 1)
        _WaveHeight ("Wave height", Float) = 0.01
        _Gloss ("Gloss", Float) = 90
        _Sparkle ("Sparkle", Range(0, 2)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+1" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow;
                half4 _Deep;
                half4 _Horizon;
                half4 _Foam;
                half4 _Ambient;
                half4 _DormantTint;
                float _WaveHeight;
                half _Gloss;
                half _Sparkle;
            CBUFFER_END

            float _BiomeWake[8];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half depth : TEXCOORD3;
                half wake : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float t = _Time.y;
                float3 p = input.positionOS.xyz;
                float wave = sin(dot(p, float3(6.1, 4.3, 5.7)) + t * 1.3) + sin(dot(p, float3(-3.7, 7.9, 2.3)) + t * 1.7);
                p += input.normalOS * wave * _WaveHeight;

                VertexPositionInputs positions = GetVertexPositionInputs(p);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.positionOS = p;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.depth = input.color.r;
                int biome = (int)round(input.uv2.x);
                output.wake = biome >= 0 && biome < 8 ? _BiomeWake[biome] : 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light light = GetMainLight();

                half3 color = lerp(_Shallow.rgb, _Deep.rgb, smoothstep(0.0, 1.0, input.depth));
                half fresnel = pow(1.0 - saturate(dot(normal, view)), 3.0);
                color = lerp(color, _Horizon.rgb, fresnel * 0.45);

                half diffuse = saturate(dot(normal, light.direction) * 0.5 + 0.5);
                color *= light.color * diffuse * 0.75 + _Ambient.rgb;

                // Small moving ripples break up the highlight into sparkles.
                float3 p = input.positionOS * 3.0;
                float3 ripple = float3(sin(p.x * 9.0 + t * 1.1), sin(p.y * 11.0 + t * 1.4), sin(p.z * 10.0 - t * 1.2));
                float3 rippled = normalize(normal + ripple * 0.12);
                float3 halfway = normalize(light.direction + view);
                half specular = pow(saturate(dot(rippled, halfway)), _Gloss) * _Sparkle;
                color += light.color * specular;

                half foam = smoothstep(0.14, 0.0, input.depth) * (0.65 + 0.35 * sin(t * 2.0 + p.x * 6.0 + p.z * 5.0));
                color = lerp(color, _Foam.rgb, foam * 0.75);

                half grey = dot(color, half3(0.3, 0.59, 0.11));
                color = lerp(grey * _DormantTint.rgb * 1.3, color, input.wake);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}

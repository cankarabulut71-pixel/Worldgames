// Vertex-coloured, softly lit surface used for the planet ground, ground cover and clouds.
// TEXCOORD1.x holds a biome index; biomes that are still asleep (see _BiomeWake) are drawn grey.
Shader "TinyPlanet/Terrain"
{
    Properties
    {
        _Ambient ("Ambient", Color) = (0.42, 0.47, 0.55, 1)
        _Wrap ("Light wrap", Range(0, 1)) = 0.45
        _RimColor ("Rim colour", Color) = (0.65, 0.95, 1, 1)
        _RimPower ("Rim power", Float) = 3.5
        _RimStrength ("Rim strength", Range(0, 1)) = 0.22
        _DormantTint ("Dormant tint", Color) = (0.62, 0.62, 0.64, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Ambient;
                half _Wrap;
                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half4 _DormantTint;
            CBUFFER_END

            float _BiomeWake[8];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
                half wake : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                int biome = (int)round(input.uv2.x);
                output.wake = biome >= 0 && biome < 8 ? _BiomeWake[biome] : 1.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                Light light = GetMainLight();
                half diffuse = saturate((dot(normal, light.direction) + _Wrap) / (1.0 + _Wrap));

                half3 albedo = input.color.rgb;
                half grey = dot(albedo, half3(0.3, 0.59, 0.11));
                albedo = lerp(grey * _DormantTint.rgb * 1.5, albedo, input.wake);

                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = pow(1.0 - saturate(dot(normal, view)), _RimPower) * _RimStrength;

                half3 color = albedo * (light.color * diffuse + _Ambient.rgb) + _RimColor.rgb * rim;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}

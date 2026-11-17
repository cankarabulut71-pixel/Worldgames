// Skybox: a teal glow centred behind the planet (_SkyCentre, set every frame from the camera) fading
// to deep space at the edges, with small twinkling stars.
Shader "TinyPlanet/SpaceSky"
{
    Properties
    {
        _Centre ("Centre colour", Color) = (0.33, 0.62, 0.62, 1)
        _Edge ("Edge colour", Color) = (0.05, 0.17, 0.20, 1)
        _Falloff ("Falloff", Float) = 2.2
        _StarDensity ("Star density", Range(0.9, 1)) = 0.985
        _StarScale ("Star scale", Float) = 90
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Centre;
                half4 _Edge;
                half _Falloff;
                half _StarDensity;
                float _StarScale;
            CBUFFER_END

            float3 _SkyCentre;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float3 centre = length(_SkyCentre) > 0.5 ? normalize(_SkyCentre) : float3(0, 0, 1);
                half glow = pow(saturate(dot(direction, centre) * 0.5 + 0.5), _Falloff * 2.0);
                half3 color = lerp(_Edge.rgb, _Centre.rgb, glow);

                float3 p = direction * _StarScale;
                float3 cell = floor(p);
                float h = Hash(cell);
                float3 offset = frac(p) - 0.5 - (float3(Hash(cell + 1.7), Hash(cell + 3.1), Hash(cell + 5.3)) - 0.5) * 0.5;
                half star = step(_StarDensity, h) * smoothstep(0.16, 0.0, length(offset));
                star *= 0.6 + 0.4 * sin(_Time.y * (1.0 + h * 3.0) + h * 40.0);
                color += star * (1.0 - glow * 0.6);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}

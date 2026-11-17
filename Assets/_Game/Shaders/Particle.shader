// Soft dots and rings for effects and the placement marker. Colour comes from the vertex colour (particle colour)
// times _Tint; the shape is drawn from the UVs, so no texture is needed. Blend factors are properties, so one
// shader serves additive sparkles (One, One) and alpha-blended puffs (One, OneMinusSrcAlpha; premultiplied).
Shader "TinyPlanet/Particle"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        [Enum(Dot, 0, Ring, 1)] _Shape ("Shape", Float) = 0
        _RingWidth ("Ring Width", Range(0.02, 0.5)) = 0.12
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Shape;
                half _RingWidth;
                half _SrcBlend;
                half _DstBlend;
                half _ZTest;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Tint;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv - 0.5) * 2.0; // 0 at the centre, 1 at the edge
                half soft = saturate(1.0 - r);
                soft = soft * soft * (3.0 - 2.0 * soft);
                half core = saturate(1.0 - r * 2.2);
                soft = soft * 0.75 + core * 0.5;

                half ring = saturate(1.0 - abs(r - (0.98 - _RingWidth)) / _RingWidth);
                ring = ring * ring * (3.0 - 2.0 * ring);

                half shape = lerp(soft, ring, step(0.5, _Shape));
                half alpha = saturate(shape * input.color.a);
                // Premultiplied, so the same output works for additive and alpha blending.
                return half4(input.color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}

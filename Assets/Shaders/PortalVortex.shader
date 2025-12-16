Shader "SoloBandStudio/PortalVortex"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _CoreColor ("Core Color", Color) = (0, 2, 2, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.5, 0, 2, 1)
        [HDR] _SpiralColor ("Spiral Color", Color) = (1, 1, 2, 1)
        _BackgroundColor ("Background Color", Color) = (0.02, 0.02, 0.05, 1)

        [Header(Vortex Settings)]
        _VortexSpeed ("Vortex Speed", Range(0.1, 5)) = 1.0
        _SpiralCount ("Spiral Arms", Range(2, 12)) = 4
        _SpiralTightness ("Spiral Tightness", Range(0.5, 5)) = 2.0
        _SpiralWidth ("Spiral Width", Range(0.1, 1)) = 0.4

        [Header(Center Effect)]
        _CenterGlow ("Center Glow", Range(0, 3)) = 1.5
        _CenterSize ("Center Size", Range(0.05, 0.5)) = 0.15
        _PullEffect ("Pull Effect Strength", Range(0, 1)) = 0.3

        [Header(Edge Ring)]
        _EdgeWidth ("Edge Ring Width", Range(0.02, 0.2)) = 0.08
        _EdgeGlow ("Edge Glow Intensity", Range(0, 5)) = 2.0
        _EdgePulseSpeed ("Edge Pulse Speed", Range(0, 5)) = 2.0

        [Header(Distortion)]
        _DistortionStrength ("Distortion", Range(0, 0.5)) = 0.1
        _NoiseScale ("Noise Scale", Range(1, 20)) = 8

        [Header(Transparency)]
        _OverallAlpha ("Overall Alpha", Range(0, 1)) = 0.95
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PortalVortex"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _EdgeColor;
                float4 _SpiralColor;
                float4 _BackgroundColor;
                float _VortexSpeed;
                float _SpiralCount;
                float _SpiralTightness;
                float _SpiralWidth;
                float _CenterGlow;
                float _CenterSize;
                float _PullEffect;
                float _EdgeWidth;
                float _EdgeGlow;
                float _EdgePulseSpeed;
                float _DistortionStrength;
                float _NoiseScale;
                float _OverallAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // Simple hash for noise
            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Value noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Fractal noise
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // Convert from cylinder UV to circular
                // Cylinder has UV where x goes around, y goes up/down
                // We want to use xz position for the portal face
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Convert UV to centered coordinates (-1 to 1)
                // For a cylinder's top face, we need to map UV properly
                float2 uv = input.uv * 2.0 - 1.0;

                // Calculate polar coordinates
                float dist = length(uv);
                float angle = atan2(uv.y, uv.x);

                // Time
                float time = _Time.y;

                // Add noise-based distortion
                float2 noiseUV = uv * _NoiseScale + time * 0.5;
                float distortion = fbm(noiseUV) * _DistortionStrength;
                dist += distortion * (1.0 - dist); // More distortion toward center

                // Spiral pattern
                float spiral = angle + dist * _SpiralTightness * 3.14159 - time * _VortexSpeed;
                float spiralPattern = sin(spiral * _SpiralCount) * 0.5 + 0.5;
                spiralPattern = smoothstep(0.5 - _SpiralWidth, 0.5 + _SpiralWidth, spiralPattern);

                // Radial gradient (brighter toward center)
                float radialGradient = 1.0 - smoothstep(0.0, 0.9, dist);

                // Center glow
                float centerGlow = exp(-dist * dist / (_CenterSize * _CenterSize)) * _CenterGlow;

                // Pull effect - lines going toward center
                float pullLines = sin((dist * 20.0 - time * 3.0) * 3.14159) * 0.5 + 0.5;
                pullLines *= (1.0 - dist) * _PullEffect;

                // Edge ring
                float edgeDist = abs(dist - (1.0 - _EdgeWidth * 0.5));
                float edgeRing = 1.0 - smoothstep(0.0, _EdgeWidth, edgeDist);
                float edgePulse = sin(time * _EdgePulseSpeed) * 0.3 + 0.7;
                edgeRing *= edgePulse * _EdgeGlow;

                // Secondary rotating ring
                float ring2Dist = abs(dist - 0.7);
                float ring2 = 1.0 - smoothstep(0.0, 0.05, ring2Dist);
                ring2 *= 0.5 * (sin(angle * 8.0 + time * 2.0) * 0.5 + 0.5);

                // Combine colors
                float3 color = _BackgroundColor.rgb;

                // Add spiral
                color = lerp(color, _SpiralColor.rgb, spiralPattern * radialGradient * 0.6);

                // Add pull effect
                color += _CoreColor.rgb * pullLines * 0.3;

                // Add radial glow
                color = lerp(color, _CoreColor.rgb, radialGradient * 0.4);

                // Add center glow
                color += _CoreColor.rgb * centerGlow;

                // Add edge ring
                color += _EdgeColor.rgb * edgeRing;

                // Add secondary ring
                color += _SpiralColor.rgb * ring2;

                // Alpha: visible inside the circle, fade at edges
                float alpha = smoothstep(1.1, 0.9, dist) * _OverallAlpha;

                // Boost alpha at center and edges
                alpha = saturate(alpha + centerGlow * 0.3 + edgeRing * 0.2);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}

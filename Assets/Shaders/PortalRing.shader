Shader "SoloBandStudio/PortalRing"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _MainColor ("Main Color", Color) = (0, 2, 2, 1)
        [HDR] _SecondaryColor ("Secondary Color", Color) = (0.5, 0, 2, 1)

        [Header(Ring Settings)]
        _RingWidth ("Ring Width", Range(0.05, 0.5)) = 0.15
        _InnerRadius ("Inner Radius", Range(0.3, 0.95)) = 0.8

        [Header(Animation)]
        _RotationSpeed ("Rotation Speed", Range(0, 5)) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 2.0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3

        [Header(Segments)]
        _SegmentCount ("Segment Count", Range(0, 24)) = 8
        _SegmentGap ("Segment Gap", Range(0, 0.5)) = 0.1

        [Header(Glow)]
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2.0
        _GlowSpread ("Glow Spread", Range(0.01, 0.3)) = 0.1

        [Header(Energy Particles)]
        _ParticleCount ("Particle Count", Range(0, 20)) = 8
        _ParticleSpeed ("Particle Speed", Range(0.5, 5)) = 2.0
        _ParticleSize ("Particle Size", Range(0.01, 0.1)) = 0.03
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
            Name "PortalRing"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One  // Additive blending for glow
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _SecondaryColor;
                float _RingWidth;
                float _InnerRadius;
                float _RotationSpeed;
                float _PulseSpeed;
                float _PulseAmount;
                float _SegmentCount;
                float _SegmentGap;
                float _GlowIntensity;
                float _GlowSpread;
                float _ParticleCount;
                float _ParticleSpeed;
                float _ParticleSize;
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

            // Hash function
            float hash(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Center the UV
                float2 uv = input.uv * 2.0 - 1.0;

                float dist = length(uv);
                float angle = atan2(uv.y, uv.x);

                float time = _Time.y;

                // Pulse effect
                float pulse = sin(time * _PulseSpeed) * _PulseAmount + 1.0;

                // Rotating angle
                float rotatedAngle = angle + time * _RotationSpeed;

                // Ring shape
                float outerRadius = 1.0;
                float innerRadius = _InnerRadius;

                // Soft ring with glow
                float ringDist = abs(dist - (innerRadius + _RingWidth * 0.5));
                float ring = 1.0 - smoothstep(0.0, _RingWidth * 0.5 + _GlowSpread, ringDist);

                // Inner glow (toward center)
                float innerGlow = 1.0 - smoothstep(innerRadius - _GlowSpread * 2.0, innerRadius, dist);
                innerGlow *= 0.3;

                // Segments
                float segmentAngle = 6.28318 / max(_SegmentCount, 1.0);
                float segment = frac(rotatedAngle / segmentAngle);
                float segmentMask = smoothstep(0.0, _SegmentGap, segment) * smoothstep(1.0, 1.0 - _SegmentGap, segment);

                // Apply segments only if count > 0
                if (_SegmentCount > 0)
                {
                    ring *= segmentMask;
                }

                // Energy particles orbiting
                float particles = 0.0;
                for (int i = 0; i < 12; i++)
                {
                    if (i >= (int)_ParticleCount) break;

                    float particleAngle = (float)i / _ParticleCount * 6.28318 + time * _ParticleSpeed;
                    float particleRadius = innerRadius + _RingWidth * 0.5;

                    // Add some variation
                    particleRadius += sin(time * 3.0 + (float)i) * 0.02;

                    float2 particlePos = float2(cos(particleAngle), sin(particleAngle)) * particleRadius;
                    float particleDist = length(uv - particlePos);

                    float particle = 1.0 - smoothstep(0.0, _ParticleSize, particleDist);
                    particles += particle * pulse;
                }

                // Outer edge highlight
                float outerEdge = 1.0 - smoothstep(outerRadius - 0.05, outerRadius, dist);
                outerEdge *= smoothstep(outerRadius - 0.15, outerRadius - 0.05, dist);

                // Combine
                float intensity = ring * _GlowIntensity * pulse;
                intensity += innerGlow * _GlowIntensity * 0.5;
                intensity += particles * 2.0;
                intensity += outerEdge * 0.5;

                // Color blend based on angle
                float colorBlend = sin(rotatedAngle * 2.0) * 0.5 + 0.5;
                float3 color = lerp(_MainColor.rgb, _SecondaryColor.rgb, colorBlend);

                // Alpha
                float alpha = saturate(intensity);

                // Clip outside the ring area
                alpha *= smoothstep(outerRadius + 0.1, outerRadius - 0.05, dist);
                alpha *= smoothstep(innerRadius - _GlowSpread * 3.0, innerRadius, dist);

                return half4(color * intensity, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}

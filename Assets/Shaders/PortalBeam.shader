Shader "SoloBandStudio/PortalBeam"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _BaseColor ("Base Color", Color) = (0, 2, 2, 1)
        [HDR] _TopColor ("Top Color", Color) = (0.5, 1, 2, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (1, 0, 2, 1)

        [Header(Beam Shape)]
        _BeamWidth ("Beam Width", Range(0.1, 1)) = 0.8
        _TopFade ("Top Fade", Range(0.1, 1)) = 0.7
        _BottomIntensity ("Bottom Intensity", Range(0.5, 3)) = 1.5
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.15

        [Header(Flow Animation)]
        _FlowSpeed ("Flow Speed", Range(0.1, 5)) = 1.5
        _FlowScale ("Flow Scale", Range(1, 20)) = 8
        _FlowIntensity ("Flow Intensity", Range(0, 1)) = 0.4

        [Header(Spiral Effect)]
        _SpiralSpeed ("Spiral Speed", Range(0, 3)) = 0.8
        _SpiralCount ("Spiral Count", Range(1, 8)) = 3
        _SpiralIntensity ("Spiral Intensity", Range(0, 1)) = 0.3

        [Header(Pulse)]
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 2
        _PulseIntensity ("Pulse Intensity", Range(0, 0.5)) = 0.15

        [Header(Particles)]
        _ParticleCount ("Rising Particles", Range(0, 30)) = 12
        _ParticleSpeed ("Particle Speed", Range(0.5, 5)) = 2
        _ParticleSize ("Particle Size", Range(0.01, 0.1)) = 0.03

        [Header(Transparency)]
        _OverallAlpha ("Overall Alpha", Range(0, 1)) = 0.6
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
            Name "PortalBeam"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TopColor;
                float4 _EdgeColor;
                float _BeamWidth;
                float _TopFade;
                float _BottomIntensity;
                float _EdgeSoftness;
                float _FlowSpeed;
                float _FlowScale;
                float _FlowIntensity;
                float _SpiralSpeed;
                float _SpiralCount;
                float _SpiralIntensity;
                float _PulseSpeed;
                float _PulseIntensity;
                float _ParticleCount;
                float _ParticleSpeed;
                float _ParticleSize;
                float _OverallAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            // Hash functions
            float hash(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            float hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Value noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash2(i);
                float b = hash2(i + float2(1, 0));
                float c = hash2(i + float2(0, 1));
                float d = hash2(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Fractal noise
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 3; i++)
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
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;

                // For cylinder: uv.x = angle around (0-1), uv.y = height (0-1)
                float height = uv.y;
                float angle = uv.x * 6.28318; // Convert to radians

                // Horizontal distance from center (for cylinder, this is constant at surface)
                // We'll use UV to simulate radial effect
                float2 centered = float2(uv.x * 2.0 - 1.0, 0);

                // Height-based fade (stronger at bottom, fading toward top)
                float heightFade = 1.0 - smoothstep(_TopFade, 1.0, height);
                float bottomBoost = (1.0 - height) * _BottomIntensity;

                // Flowing energy pattern (moving upward)
                float2 flowUV = float2(angle, height * _FlowScale - time * _FlowSpeed);
                float flowPattern = fbm(flowUV * 2.0);
                flowPattern = flowPattern * _FlowIntensity;

                // Spiral pattern
                float spiral = sin(angle * _SpiralCount + height * 10.0 - time * _SpiralSpeed * 3.0);
                spiral = spiral * 0.5 + 0.5;
                spiral *= _SpiralIntensity * heightFade;

                // Pulse effect
                float pulse = sin(time * _PulseSpeed + height * 5.0) * _PulseIntensity + 1.0;

                // Rising particles
                float particles = 0.0;
                for (int i = 0; i < 20; i++)
                {
                    if (i >= (int)_ParticleCount) break;

                    float seed = (float)i * 1.618;
                    float particleAngle = hash(seed) * 6.28318;
                    float particlePhase = hash(seed + 1.0);
                    float particleY = frac(particlePhase + time * _ParticleSpeed * (0.5 + hash(seed + 2.0) * 0.5));

                    // Particle position in UV space
                    float particleU = particleAngle / 6.28318;
                    float dist = abs(uv.x - particleU);
                    dist = min(dist, 1.0 - dist); // Wrap around

                    float yDist = abs(height - particleY);

                    float particle = 1.0 - smoothstep(0.0, _ParticleSize, dist);
                    particle *= 1.0 - smoothstep(0.0, _ParticleSize * 2.0, yDist);
                    particle *= heightFade; // Fade with height

                    particles += particle;
                }

                // Edge glow (brighter at edges of the beam)
                float edgeDist = abs(sin(angle * 2.0)); // Creates vertical stripes effect
                float edgeGlow = smoothstep(1.0 - _EdgeSoftness, 1.0, edgeDist) * 0.3;

                // Combine intensity
                float intensity = heightFade * pulse;
                intensity += flowPattern;
                intensity += spiral;
                intensity += particles * 1.5;
                intensity *= _BeamWidth;
                intensity += bottomBoost * 0.3;

                // Color gradient based on height
                float3 color = lerp(_BaseColor.rgb, _TopColor.rgb, height);
                color = lerp(color, _EdgeColor.rgb, edgeGlow + spiral * 0.5);

                // Final color
                float3 finalColor = color * intensity;

                // Alpha
                float alpha = saturate(intensity * _OverallAlpha);
                alpha *= heightFade;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}

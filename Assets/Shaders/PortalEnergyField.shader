Shader "SoloBandStudio/PortalEnergyField"
{
    Properties
    {
        [Header(Colors)]
        [HDR] _CoreColor ("Core Color", Color) = (0.1, 0.5, 1.0, 0.4)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.3, 0.8, 1.5, 1.0)
        [HDR] _RippleColor ("Ripple Color", Color) = (0.5, 1.0, 2.0, 1.0)

        [Header(Edge Glow)]
        _EdgeWidth ("Edge Width", Range(0.01, 0.3)) = 0.08
        _EdgeGlowPower ("Edge Glow Power", Range(0.5, 5)) = 2.0
        _EdgeGlowIntensity ("Edge Glow Intensity", Range(0, 5)) = 2.0

        [Header(Ripple Effect)]
        _RippleSpeed ("Ripple Speed", Range(0.1, 3)) = 0.8
        _RippleCount ("Ripple Count", Range(1, 10)) = 4
        _RippleIntensity ("Ripple Intensity", Range(0, 1)) = 0.5

        [Header(Energy Flow)]
        _FlowSpeed ("Flow Speed", Range(0.1, 3)) = 1.0
        _FlowScale ("Flow Scale", Range(1, 15)) = 6
        _FlowIntensity ("Flow Intensity", Range(0, 1)) = 0.4

        [Header(Warp Distortion)]
        _WarpStrength ("Warp Strength", Range(0, 0.2)) = 0.05
        _WarpSpeed ("Warp Speed", Range(0.1, 2)) = 0.5
        _WarpScale ("Warp Scale", Range(1, 10)) = 4

        [Header(Shimmer)]
        _ShimmerSpeed ("Shimmer Speed", Range(0.5, 5)) = 2.0
        _ShimmerIntensity ("Shimmer Intensity", Range(0, 1)) = 0.3

        [Header(Overall)]
        _OverallAlpha ("Overall Alpha", Range(0, 1)) = 0.85
        _OverallBrightness ("Overall Brightness", Range(0.5, 3)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PortalEnergyField"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _EdgeColor;
                float4 _RippleColor;
                float _EdgeWidth;
                float _EdgeGlowPower;
                float _EdgeGlowIntensity;
                float _RippleSpeed;
                float _RippleCount;
                float _RippleIntensity;
                float _FlowSpeed;
                float _FlowScale;
                float _FlowIntensity;
                float _WarpStrength;
                float _WarpSpeed;
                float _WarpScale;
                float _ShimmerSpeed;
                float _ShimmerIntensity;
                float _OverallAlpha;
                float _OverallBrightness;
            CBUFFER_END

            // Hash functions
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float hash2(float n)
            {
                return frac(sin(n) * 43758.5453);
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

            // Fractal Brownian Motion
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

            // Smooth rectangle edge distance
            float rectEdge(float2 uv, float edgeWidth)
            {
                float2 fromCenter = abs(uv - 0.5) * 2.0;
                float distToEdge = max(fromCenter.x, fromCenter.y);
                return smoothstep(1.0 - edgeWidth * 2.0, 1.0, distToEdge);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.uv = input.uv;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float2 uv = input.uv;

                // Normalize vectors for fresnel
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float fresnel = 1.0 - saturate(abs(dot(normalWS, viewDirWS)));
                fresnel = pow(fresnel, 2.0);

                // UV warp distortion for "dimensional rift" feel
                float2 warpOffset;
                warpOffset.x = fbm(uv * _WarpScale + float2(time * _WarpSpeed, 0)) - 0.5;
                warpOffset.y = fbm(uv * _WarpScale + float2(0, time * _WarpSpeed * 0.7)) - 0.5;
                float2 warpedUV = uv + warpOffset * _WarpStrength;

                // Rectangle edge glow (for door shape)
                float edgeDist = rectEdge(uv, _EdgeWidth);
                float edgeGlow = pow(edgeDist, _EdgeGlowPower) * _EdgeGlowIntensity;

                // Horizontal ripples expanding from center
                float2 centered = uv - 0.5;
                float distFromCenter = length(centered);
                float ripple = sin((distFromCenter * _RippleCount * 6.28318) - time * _RippleSpeed * 3.0);
                ripple = ripple * 0.5 + 0.5;
                ripple = pow(ripple, 2.0) * _RippleIntensity;

                // Energy flow pattern (rising energy)
                float2 flowUV = warpedUV * _FlowScale;
                flowUV.y -= time * _FlowSpeed;
                float flowPattern = fbm(flowUV);
                flowPattern = flowPattern * _FlowIntensity;

                // Secondary flow layer for more complexity
                float2 flowUV2 = warpedUV * _FlowScale * 0.7;
                flowUV2.y += time * _FlowSpeed * 0.6;
                flowUV2.x -= time * _FlowSpeed * 0.3;
                float flowPattern2 = fbm(flowUV2) * _FlowIntensity * 0.6;

                // Shimmer/sparkle effect
                float shimmer = noise(uv * 30.0 + time * _ShimmerSpeed);
                shimmer = pow(shimmer, 8.0) * _ShimmerIntensity * 3.0;

                // Vertical gradient (brighter at bottom like energy rising)
                float vertGradient = 1.0 - uv.y * 0.3;

                // Combine intensities
                float coreIntensity = (1.0 - edgeDist * 0.5) * vertGradient;
                coreIntensity += flowPattern + flowPattern2;
                coreIntensity += ripple * (1.0 - edgeDist);

                // Build color
                float3 color = _CoreColor.rgb * coreIntensity;
                color += _EdgeColor.rgb * edgeGlow;
                color += _RippleColor.rgb * ripple * 0.5;
                color += shimmer * _RippleColor.rgb;

                // Fresnel edge enhancement
                color += _EdgeColor.rgb * fresnel * 0.5;

                // Apply brightness
                color *= _OverallBrightness;

                // Alpha calculation
                float alpha = _CoreColor.a * coreIntensity;
                alpha += edgeGlow * _EdgeColor.a;
                alpha += ripple * 0.3;
                alpha += shimmer;
                alpha = saturate(alpha) * _OverallAlpha;

                // Ensure minimum visibility in center
                alpha = max(alpha, _CoreColor.a * 0.3 * _OverallAlpha);

                return float4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}

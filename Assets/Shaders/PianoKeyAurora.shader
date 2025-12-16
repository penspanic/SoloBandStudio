Shader "SoloBandStudio/PianoKeyAurora"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.1, 0.1, 0.15, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.8

        [Header(Aurora Effect)]
        [HDR] _AuroraColor1 ("Aurora Color 1", Color) = (0.2, 1.0, 0.5, 1)
        [HDR] _AuroraColor2 ("Aurora Color 2", Color) = (0.3, 0.5, 1.0, 1)
        [HDR] _AuroraColor3 ("Aurora Color 3", Color) = (1.0, 0.3, 0.8, 1)

        _AuroraSpeed ("Aurora Speed", Range(0.1, 5)) = 1.0
        _AuroraScale ("Aurora Scale", Range(0.5, 10)) = 3.0
        _AuroraIntensity ("Aurora Intensity", Range(0, 3)) = 1.0
        _AuroraWaveStrength ("Wave Strength", Range(0, 2)) = 1.0

        [Header(Flow Direction)]
        _FlowDirectionX ("Flow Direction X", Range(-1, 1)) = 0.3
        _FlowDirectionY ("Flow Direction Y", Range(-1, 1)) = 1.0
        _FlowDirectionZ ("Flow Direction Z", Range(-1, 1)) = 0.5

        [Header(Pressed State)]
        _PressedBoost ("Pressed Intensity Boost", Range(1, 5)) = 2.0
        _PressedSpeedBoost ("Pressed Speed Boost", Range(1, 3)) = 1.5

        [Header(Rim Light)]
        [HDR] _RimColor ("Rim Color", Color) = (0.5, 0.8, 1.0, 1)
        _RimPower ("Rim Power", Range(1, 10)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 0.5

        [Header(Base Emission)]
        [HDR] _EmissionColor ("Base Emission", Color) = (0.05, 0.05, 0.1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Metallic;
                float _Smoothness;
                float4 _AuroraColor1;
                float4 _AuroraColor2;
                float4 _AuroraColor3;
                float _AuroraSpeed;
                float _AuroraScale;
                float _AuroraIntensity;
                float _AuroraWaveStrength;
                float _FlowDirectionX;
                float _FlowDirectionY;
                float _FlowDirectionZ;
                float _PressedBoost;
                float _PressedSpeedBoost;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float4 _EmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                float2 uv : TEXCOORD4;
            };

            // Simplex-like noise
            float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float4 mod289(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float4 permute(float4 x) { return mod289(((x * 34.0) + 1.0) * x); }
            float4 taylorInvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

            float snoise(float3 v)
            {
                const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
                const float4 D = float4(0.0, 0.5, 1.0, 2.0);

                float3 i = floor(v + dot(v, C.yyy));
                float3 x0 = v - i + dot(i, C.xxx);

                float3 g = step(x0.yzx, x0.xyz);
                float3 l = 1.0 - g;
                float3 i1 = min(g.xyz, l.zxy);
                float3 i2 = max(g.xyz, l.zxy);

                float3 x1 = x0 - i1 + C.xxx;
                float3 x2 = x0 - i2 + C.yyy;
                float3 x3 = x0 - D.yyy;

                i = mod289(i);
                float4 p = permute(permute(permute(
                    i.z + float4(0.0, i1.z, i2.z, 1.0))
                    + i.y + float4(0.0, i1.y, i2.y, 1.0))
                    + i.x + float4(0.0, i1.x, i2.x, 1.0));

                float n_ = 0.142857142857;
                float3 ns = n_ * D.wyz - D.xzx;

                float4 j = p - 49.0 * floor(p * ns.z * ns.z);

                float4 x_ = floor(j * ns.z);
                float4 y_ = floor(j - 7.0 * x_);

                float4 x = x_ * ns.x + ns.yyyy;
                float4 y = y_ * ns.x + ns.yyyy;
                float4 h = 1.0 - abs(x) - abs(y);

                float4 b0 = float4(x.xy, y.xy);
                float4 b1 = float4(x.zw, y.zw);

                float4 s0 = floor(b0) * 2.0 + 1.0;
                float4 s1 = floor(b1) * 2.0 + 1.0;
                float4 sh = -step(h, float4(0, 0, 0, 0));

                float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
                float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

                float3 p0 = float3(a0.xy, h.x);
                float3 p1 = float3(a0.zw, h.y);
                float3 p2 = float3(a1.xy, h.z);
                float3 p3 = float3(a1.zw, h.w);

                float4 norm = taylorInvSqrt(float4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
                p0 *= norm.x;
                p1 *= norm.y;
                p2 *= norm.z;
                p3 *= norm.w;

                float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
                m = m * m;
                return 42.0 * dot(m * m, float4(dot(p0, x0), dot(p1, x1), dot(p2, x2), dot(p3, x3)));
            }

            // Fractal Brownian Motion
            float fbm(float3 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * snoise(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }

                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Flow direction
                float3 flowDir = normalize(float3(_FlowDirectionX, _FlowDirectionY, _FlowDirectionZ));
                float time = _Time.y * _AuroraSpeed;

                // Aurora noise coordinates
                float3 noiseCoord = input.positionOS * _AuroraScale;
                noiseCoord += flowDir * time;

                // Multiple layers of aurora
                float aurora1 = fbm(noiseCoord, 4) * 0.5 + 0.5;
                float aurora2 = fbm(noiseCoord * 1.5 + float3(100, 0, 0) + time * 0.3, 4) * 0.5 + 0.5;
                float aurora3 = fbm(noiseCoord * 0.8 + float3(0, 100, 0) - time * 0.2, 4) * 0.5 + 0.5;

                // Wave pattern
                float wave = sin(input.positionOS.y * 10.0 + time * 2.0) * 0.5 + 0.5;
                wave *= _AuroraWaveStrength;

                // Combine aurora colors
                float3 auroraColor = float3(0, 0, 0);
                auroraColor += _AuroraColor1.rgb * aurora1 * (0.5 + wave * 0.5);
                auroraColor += _AuroraColor2.rgb * aurora2 * (1.0 - wave * 0.3);
                auroraColor += _AuroraColor3.rgb * aurora3 * wave;

                // Apply intensity
                auroraColor *= _AuroraIntensity;

                // Surface-based aurora visibility (more visible on top surface)
                float surfaceFactor = saturate(dot(normalWS, float3(0, 1, 0)) * 0.5 + 0.7);
                auroraColor *= surfaceFactor;

                // Rim light
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float rim = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                float3 rimColor = _RimColor.rgb * rim;

                // Main lighting
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float NdotL = saturate(dot(normalWS, lightDir));

                // Specular
                float3 halfDir = normalize(lightDir + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float specular = pow(NdotH, _Smoothness * 128.0) * _Smoothness;

                // Combine - aurora as subtle color tint, not additive emission
                float3 baseColor = _BaseColor.rgb;

                // Aurora modulates the base color subtly instead of adding brightness
                float auroraLuminance = dot(auroraColor, float3(0.299, 0.587, 0.114));
                float3 tintedBase = lerp(baseColor, baseColor + auroraColor * 0.3, auroraLuminance);

                float3 diffuse = tintedBase * (0.2 + NdotL * 0.8) * mainLight.color;
                float3 emission = _EmissionColor.rgb + rimColor;

                float3 finalColor = diffuse + emission + specular * mainLight.color * 0.5;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // Depth pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}

// Stylized wet road for URP (Unity 6): an asphalt toon shader that gets wet.
// Wetness comes from the style controller (_Anime_Wetness, e.g. rainy night) plus the material's own Wetness.
// When wet: darker surface, puddles (noise blobs) that mirror the sky, a fresnel reflection of the skybox / probe,
// a toon sun glint, and long light streaks under point / spot lights (street lamps, neon, headlights).
// Dry (wetness 0) it looks like a normal toon asphalt. Use it for roads, sidewalks and markings.
Shader "Anime/RoadWet"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.3,0.29,0.37,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2

        [Header(Toon Lighting)]
        _LitTint ("Lit Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.5,0.45,0.7,1)
        _Threshold ("Shadow Threshold", Range(0,1)) = 0.5
        _Softness ("Shadow Edge Softness", Range(0.001,0.5)) = 0.05
        _ReceiveShadow ("Receive Shadow", Range(0,1)) = 1
        _LightIntensity ("Light Intensity", Range(0,2)) = 0.9
        _AmbientInfluence ("Ambient Influence", Range(0,1)) = 0.4
        _AdditionalLightIntensity ("Additional Light Intensity", Range(0,3)) = 1
        _AdditionalLightSoftness ("Additional Light Softness", Range(0.01,0.5)) = 0.15

        [Header(Painted Variation)]
        _NoiseScale ("Noise Scale", Float) = 0.6
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.28

        [Header(Wetness)]
        _Wetness ("Wetness (added to the style controller's)", Range(0,1)) = 0
        _WetDarken ("Wet Darkening", Range(0,1)) = 0.4
        _WetReflection ("Wet Surface Reflection", Range(0,1)) = 0.45
        _PuddleAmount ("Puddle Amount", Range(0,1)) = 0.5
        _PuddleScale ("Puddle Scale", Float) = 0.12
        _PuddleSoftness ("Puddle Edge Softness", Range(0.005,0.3)) = 0.06
        _PuddleReflection ("Puddle Reflection", Range(0,1)) = 0.9
        _FresnelPower ("Fresnel Power", Range(0.5,8)) = 3
        _ProbeBlend ("Real Reflection Blend (0 = fake gradient)", Range(0,1)) = 0.8
        _ProbeRoughness ("Reflection Blur", Range(0,1)) = 0.2
        _HorizonColor ("Fake Horizon Color", Color) = (1,0.85,0.9,1)
        _ZenithColor ("Fake Zenith Color", Color) = (0.5,0.7,1,1)

        [Header(Highlights)]
        _SunThreshold ("Sun Glint Threshold", Range(0,1)) = 0.55
        _SunExponent ("Sun Glint Exponent", Range(1,300)) = 120
        _SunIntensity ("Sun Glint Intensity", Range(0,3)) = 1
        _StreakIntensity ("Light Streak Intensity", Range(0,3)) = 1.2
        _StreakWidth ("Streak Width (higher = thinner)", Range(5,300)) = 60
        _StreakStretch ("Streak Length (lower = longer)", Range(0.05,20)) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _LitTint, _ShadowTint;
            half   _Threshold, _Softness, _ReceiveShadow, _LightIntensity, _AmbientInfluence;
            half   _AdditionalLightIntensity, _AdditionalLightSoftness;
            float  _NoiseScale;
            half   _NoiseStrength;
            half   _Wetness, _WetDarken, _WetReflection, _PuddleAmount;
            float  _PuddleScale;
            half   _PuddleSoftness, _PuddleReflection, _FresnelPower, _ProbeBlend, _ProbeRoughness;
            half4  _HorizonColor, _ZenithColor;
            half   _SunThreshold, _SunExponent, _SunIntensity;
            half   _StreakIntensity, _StreakWidth, _StreakStretch;
        CBUFFER_END

        // set by AnimeStyleController / AnimeQuality (0 = neutral)
        float4 _Anime_ShadowTint;
        float  _Anime_ShadowTintAmount;
        float  _Anime_LightIntensityOffset;
        float  _Anime_LowQuality;
        float  _Anime_Wetness;

        float RoadHash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float RoadNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = RoadHash21(i);
            float b = RoadHash21(i + float2(1, 0));
            float c = RoadHash21(i + float2(0, 1));
            float d = RoadHash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        half3 SampleReflectionProbe(float3 R, half roughness)
        {
            half mip = roughness * 6.0h;
            half4 enc = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, R, mip);
            return DecodeHDREnvironment(enc, unity_SpecCube0_HDR);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardRoad"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor  = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half ToonStep(half v, half center, half soft)
            {
                return smoothstep(center - soft, center + soft, v);
            }

            half4 frag(Varyings IN, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;

                float3 N = normalize(IN.normalWS);
                N = IS_FRONT_VFACE(facing, N, -N);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                float2 xz = IN.positionWS.xz;

                // painted variation (skipped on the Low quality tier)
                if (_Anime_LowQuality < 0.5)
                {
                    float2 nuv = xz * _NoiseScale;
                    half pn = RoadNoise(nuv) * 0.65h + RoadNoise(nuv * 3.1) * 0.35h;
                    base *= 1.0h + (pn - 0.5h) * _NoiseStrength * 2.0h;
                }

                // ---- wetness and puddles
                half wet = saturate(_Wetness + (half)_Anime_Wetness);
                half puddle = 0.0h;
                if (wet > 0.001h)
                {
                    float pn2 = RoadNoise(xz * _PuddleScale) * 0.65 + RoadNoise(xz * _PuddleScale * 2.7) * 0.35;
                    float th = lerp(0.75, 0.3, _PuddleAmount);
                    puddle = smoothstep(th - _PuddleSoftness, th + _PuddleSoftness, pn2) * wet;
                    base *= 1.0h - _WetDarken * wet * (0.6h + 0.4h * puddle);
                }

                // ---- toon lighting
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif
                Light L = GetMainLight(shadowCoord);
                half shadowAtten = L.shadowAttenuation;

                half NdotL = dot(N, L.direction);
                half t = NdotL * 0.5h + 0.5h;
                t *= lerp(1.0h, shadowAtten, _ReceiveShadow);

                half s = ToonStep(t, _Threshold, _Softness);
                half3 shadowTint = lerp(_ShadowTint.rgb, _Anime_ShadowTint.rgb, _Anime_ShadowTintAmount);
                half3 tint = lerp(shadowTint, _LitTint.rgb, s);

                half3 lighting = L.color * (_LightIntensity * (1.0h + _Anime_LightIntensityOffset)) + SampleSH(N) * _AmbientInfluence;
                half3 col = base * tint * lighting;

                // ---- the water film: slightly rippled normal, fresnel reflection, sun glint
                float3 Nw = N;
                half3 streaks = 0.0h;
                if (wet > 0.001h)
                {
                    if (_Anime_LowQuality < 0.5)
                    {
                        float2 rp = xz * 2.5;
                        float rx = RoadNoise(rp + _Time.y * 0.15) - 0.5;
                        float rz = RoadNoise(rp.yx - _Time.y * 0.1) - 0.5;
                        Nw = normalize(N + float3(rx, 0.0, rz) * (0.35 * (1.0 - puddle)));
                    }

                    float3 R = reflect(-V, Nw);
                    half sky = saturate(R.y * 0.5 + 0.5);
                    half3 refl = lerp(_HorizonColor.rgb, _ZenithColor.rgb, sky);
                    if (_Anime_LowQuality < 0.5) refl = lerp(refl, SampleReflectionProbe(R, _ProbeRoughness), _ProbeBlend);

                    half fres = pow(1.0h - saturate(dot(Nw, V)), _FresnelPower);
                    half reflAmount = saturate(wet * _WetReflection * (0.35h + 0.65h * fres)
                                             + puddle * _PuddleReflection * (0.55h + 0.45h * fres));
                    col = lerp(col, refl, reflAmount);

                    float3 H = normalize(L.direction + V);
                    half sunSpec = pow(saturate(dot(Nw, H)), _SunExponent);
                    half sun = smoothstep(_SunThreshold - 0.02h, _SunThreshold + 0.02h, sunSpec);
                    col += sun * L.color * _SunIntensity * saturate(wet * 0.6h + puddle);

                    // strength of the long streaks under artificial lights
                    streaks = saturate(wet * 0.6h + puddle) * _StreakIntensity;
                }

                // ---- point / spot lights: toon pools of light, and long streaks when wet
                #if defined(_ADDITIONAL_LIGHTS)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = IN.positionWS;
                    inputData.normalWS = N;
                    inputData.viewDirectionWS = V;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    half4 shadowMask = half4(1, 1, 1, 1);

                    float3 viewFlat = float3(V.x, 0.0, V.z);
                    float3 Vh = viewFlat / max(length(viewFlat), 0.0001);

                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light addLight = GetAdditionalLight(lightIndex, IN.positionWS, shadowMask);
                        half atten = addLight.distanceAttenuation * addLight.shadowAttenuation;

                        half a = saturate(dot(N, addLight.direction)) * atten;
                        half band1 = smoothstep(0.02h, 0.02h + _AdditionalLightSoftness, a);
                        half band2 = smoothstep(0.35h, 0.35h + _AdditionalLightSoftness, a);
                        col += base * addLight.color * (band1 * 0.55h + band2 * 0.45h) * _AdditionalLightIntensity;

                        // streak: a highlight stretched along the viewing direction, like a lamp reflected in a wet road
                        float3 Hl = normalize(addLight.direction + V);
                        float3 tilt = Hl - Nw;
                        float par = dot(tilt, Vh);
                        float3 perp = tilt - Vh * par;
                        half streak = exp(-dot(perp, perp) * _StreakWidth - par * par * _StreakStretch);
                        col += addLight.color * atten * streak * streaks;
                    LIGHT_LOOP_END
                }
                #endif

                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            struct DAttributes { float4 positionOS : POSITION; };
            struct DVaryings   { float4 positionCS : SV_POSITION; };

            DVaryings vertDepth(DAttributes IN)
            {
                DVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half fragDepth(DVaryings IN) : SV_Target { return IN.positionCS.z; }
            ENDHLSL
        }
    }
}

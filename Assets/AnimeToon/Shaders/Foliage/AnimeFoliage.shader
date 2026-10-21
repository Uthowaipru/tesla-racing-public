// Toon foliage for URP (Unity 6): leaves, bushes, flowers, grass, tree crowns.
//  - alpha clipped, two sided (back faces get their normal flipped)
//  - SPHERICAL SHADING: the normals of a crown bend toward "away from the crown's middle", so a whole tree or bush
//    shades like one soft puffy ball instead of a noisy mess of leaf cards
//  - root-to-tip color gradient, backlight glow (light shining through the leaves), cool tinted shadows
//  - wind sway from the height above the pivot, or from vertex color R (switch "Wind Mask")
// The pivot of the model must be at its base. Works with the style controller (shadow tint, light) and the quality tiers.
Shader "Anime/Foliage"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map (alpha = leaf shape)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Off = two sided)", Float) = 0

        [Header(Toon Lighting)]
        _LitTint ("Lit Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.42,0.48,0.72,1)
        _Threshold ("Shadow Threshold", Range(0,1)) = 0.5
        _Softness ("Shadow Edge Softness", Range(0.001,0.5)) = 0.08
        _ReceiveShadow ("Receive Shadow", Range(0,1)) = 1
        _LightIntensity ("Light Intensity", Range(0,2)) = 0.95
        _AmbientInfluence ("Ambient Influence", Range(0,1)) = 0.4
        _AdditionalLightIntensity ("Additional Light Intensity", Range(0,3)) = 1
        _AdditionalLightSoftness ("Additional Light Softness", Range(0.01,0.5)) = 0.15

        [Header(Foliage Shape)]
        _SphericalAmount ("Spherical Shading (0 = real normals)", Range(0,1)) = 0.6
        _CrownHeight ("Crown Center Height (object units)", Float) = 2
        _TipColor ("Tip Color (top)", Color) = (1,1,0.7,1)
        _RootColor ("Root Color (bottom)", Color) = (0.5,0.55,0.8,1)
        _HeightRange ("Gradient Height Range", Float) = 4
        _GradientStrength ("Gradient Strength", Range(0,1)) = 0.5
        _Translucency ("Backlight Glow", Range(0,2)) = 0.6
        _TranslucencyColor ("Backlight Color", Color) = (1,0.95,0.5,1)

        [Header(Wind)]
        [Enum(Height, 0, VertexColorR, 1)] _WindMask ("Wind Mask", Float) = 0
        _WindStrength ("Wind Strength", Range(0,1)) = 0.12
        _WindSpeed ("Wind Speed", Float) = 1.4
        _WindScale ("Wind Gust Scale", Float) = 0.4
        _WindHeight ("Height For Full Wind", Float) = 4
        _WindDirection ("Wind Direction (X, Z)", Vector) = (1,0.4,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Cutoff;
            half4  _LitTint, _ShadowTint;
            half   _Threshold, _Softness, _ReceiveShadow, _LightIntensity, _AmbientInfluence;
            half   _AdditionalLightIntensity, _AdditionalLightSoftness;
            half   _SphericalAmount;
            float  _CrownHeight;
            half4  _TipColor, _RootColor;
            float  _HeightRange;
            half   _GradientStrength, _Translucency;
            half4  _TranslucencyColor;
            half   _WindMask, _WindStrength;
            float  _WindSpeed, _WindScale, _WindHeight;
            float4 _WindDirection;
        CBUFFER_END

        // set by AnimeStyleController / AnimeQuality (0 = neutral)
        float4 _Anime_ShadowTint;
        float  _Anime_ShadowTintAmount;
        float  _Anime_LightIntensityOffset;
        float  _Anime_LowQuality;

        // sways the vertex in world space; the mask is 0 at the pivot (stiff) and 1 at the tip
        float3 ApplyWind(float3 positionWS, float heightOS, half vertexColorR)
        {
            float mask = _WindMask < 0.5 ? saturate(heightOS / max(_WindHeight, 0.01)) : vertexColorR;
            mask *= mask;

            float2 dir = normalize(_WindDirection.xy + float2(1e-5, 0));
            float phase = dot(positionWS.xz, dir) * _WindScale + _Time.y * _WindSpeed;
            float sway = sin(phase) + 0.5 * sin(phase * 2.3 + positionWS.x * 0.7);

            positionWS.xz += dir * (sway * _WindStrength * mask);
            positionWS.y -= abs(sway) * _WindStrength * mask * 0.3;
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardFoliage"
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
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float3 crownWS    : TEXCOORD3;   // middle of the crown, for the spherical shading
                float  heightOS   : TEXCOORD4;
                half   fogFactor  : TEXCOORD5;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                positionWS = ApplyWind(positionWS, IN.positionOS.y, IN.color.r);

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.crownWS    = TransformObjectToWorld(float3(0.0, _CrownHeight, 0.0));
                OUT.heightOS   = IN.positionOS.y;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor  = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half ToonStep(half v, half center, half soft)
            {
                return smoothstep(center - soft, center + soft, v);
            }

            half4 frag(Varyings IN, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                #if defined(_ALPHATEST_ON)
                    clip(tex.a * _BaseColor.a - _Cutoff);
                #endif
                half3 base = tex.rgb * _BaseColor.rgb;

                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                // normal: real (two sided) or bent toward "away from the crown's middle"
                float3 N = normalize(IN.normalWS);
                N = IS_FRONT_VFACE(facing, N, -N);
                if (_Anime_LowQuality < 0.5)
                {
                    float3 spherical = normalize(IN.positionWS - IN.crownWS + float3(0, 1e-4, 0));
                    N = normalize(lerp(N, spherical, _SphericalAmount));
                }

                // root to tip color
                half h = saturate(IN.heightOS / max(_HeightRange, 0.01));
                base *= lerp(half3(1, 1, 1), lerp(_RootColor.rgb, _TipColor.rgb, h), _GradientStrength);

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

                // backlight: light shining through the leaves when you look toward the light
                if (_Anime_LowQuality < 0.5)
                {
                    half through = saturate(dot(V, -L.direction));
                    through = through * through;
                    col += base * _TranslucencyColor.rgb * L.color * (through * _Translucency * shadowAtten);
                }

                #if defined(_ADDITIONAL_LIGHTS)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = IN.positionWS;
                    inputData.normalWS = N;
                    inputData.viewDirectionWS = V;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    half4 shadowMask = half4(1, 1, 1, 1);
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light addLight = GetAdditionalLight(lightIndex, IN.positionWS, shadowMask);
                        half a = saturate(dot(N, addLight.direction)) * addLight.distanceAttenuation * addLight.shadowAttenuation;
                        half band1 = smoothstep(0.02h, 0.02h + _AdditionalLightSoftness, a);
                        half band2 = smoothstep(0.35h, 0.35h + _AdditionalLightSoftness, a);
                        col += base * addLight.color * (band1 * 0.55h + band2 * 0.45h) * _AdditionalLightIntensity;
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
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            float3 _LightDirection;
            float3 _LightPosition;

            struct SAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct SVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            SVaryings vertShadow(SAttributes IN)
            {
                SVaryings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                positionWS = ApplyWind(positionWS, IN.positionOS.y, IN.color.r);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 fragShadow(SVaryings IN) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
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
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            struct DAttributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct DVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            DVaryings vertDepth(DAttributes IN)
            {
                DVaryings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                positionWS = ApplyWind(positionWS, IN.positionOS.y, IN.color.r);
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half fragDepth(DVaryings IN) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return IN.positionCS.z;
            }
            ENDHLSL
        }
    }
}

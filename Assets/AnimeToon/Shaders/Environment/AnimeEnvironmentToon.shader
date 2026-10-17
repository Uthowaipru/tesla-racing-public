// Anime environment toon shader for buildings, roads, rocks and props (URP, Unity 6).
// One forward pass, main light only, mobile friendly.
//
// Vertex color R = baked AO (white = none). Leave "Vertex AO Strength" at 0 for assets
// that use vertex color for something else (wind masks etc).
Shader "Anime/EnvironmentToon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)

        [Header(Surface)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Off = two sided)", Float) = 2

        [Header(Toon Lighting)]
        _LitTint ("Lit Tint", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (0.62,0.55,0.85,1)
        _Threshold ("Shadow Threshold", Range(0,1)) = 0.5
        _Softness ("Shadow Edge Softness", Range(0.001,0.5)) = 0.05
        _ReceiveShadow ("Receive Shadow", Range(0,1)) = 1
        _LightIntensity ("Light Intensity", Range(0,2)) = 0.9
        _AmbientInfluence ("Ambient Influence", Range(0,1)) = 0.35

        [Header(Additional Lights)]
        _AdditionalLightIntensity ("Additional Light Intensity", Range(0,3)) = 1
        _AdditionalLightSoftness ("Additional Light Softness", Range(0.01,0.5)) = 0.15

        [Header(World Height Gradient)]
        _GradientBottomColor ("Bottom Color", Color) = (0.75,0.65,0.9,1)
        _GradientTopColor ("Top Color", Color) = (1,1,1,1)
        _GradientMinY ("Gradient Min Y", Float) = 0
        _GradientMaxY ("Gradient Max Y", Float) = 20
        _GradientStrength ("Gradient Strength", Range(0,1)) = 0.5

        [Header(Procedural Painted Variation World Space)]
        _NoiseScale ("Noise Scale", Float) = 0.4
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.15

        [Header(Brush Overlay World Space)]
        _BrushTex ("Brush Texture", 2D) = "gray" {}
        _BrushScale ("Brush Scale", Float) = 0.3
        _BrushStrength ("Brush Strength", Range(0,0.5)) = 0.1

        [Header(Procedural Facade World Space)]
        [Toggle(_FACADE_ON)] _FacadeOn ("Enable Facade (windows)", Float) = 0
        _FloorHeight ("Floor Height", Float) = 3
        _WindowSpacing ("Window Spacing", Float) = 2.5
        _FacadeOffset ("Pattern Offset (Along Wall, Height)", Vector) = (0,0,0,0)
        _WindowOffset ("Window Offset In Cell (X, Y)", Vector) = (0,0,0,0)
        _WindowSize ("Window Size (W, H)", Vector) = (0.55,0.6,0,0)
        _WindowBottomColor ("Window Bottom", Color) = (0.25,0.35,0.6,1)
        _WindowTopColor ("Window Top", Color) = (0.65,0.85,1,1)
        _WindowLitColor ("Lit Window Color", Color) = (1,0.85,0.5,1)
        _NightAmount ("Night Amount (lit windows)", Range(0,1)) = 0
        _LitRatio ("Lit Window Ratio", Range(0,1)) = 0.4
        _WindowEmission ("Window Emission", Range(0,5)) = 2
        _LedgeThickness ("Floor Ledge Thickness", Range(0,0.3)) = 0.08
        _LedgeDarken ("Floor Ledge Darkening", Range(0,1)) = 0.25

        [Header(Vertex AO)]
        _AOColor ("AO Tint", Color) = (0.5,0.42,0.72,1)
        _VertexAOStrength ("Vertex AO Strength", Range(0,1)) = 0

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1,0.9,0.85,1)
        _RimWidth ("Rim Width", Range(0,1)) = 0.75
        _RimSoftness ("Rim Softness", Range(0.001,0.5)) = 0.12
        _RimIntensity ("Rim Intensity", Range(0,2)) = 0.2
        _RimGroundFade ("Fade Rim On Flat Ground", Range(0,1)) = 1

        [Header(Emission)]
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        TEXTURE2D(_BrushTex);    SAMPLER(sampler_BrushTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Cutoff;
            half4  _LitTint, _ShadowTint;
            half   _Threshold, _Softness, _ReceiveShadow, _LightIntensity, _AmbientInfluence;
            half   _AdditionalLightIntensity, _AdditionalLightSoftness;
            half4  _GradientBottomColor, _GradientTopColor;
            float  _GradientMinY, _GradientMaxY;
            half   _GradientStrength;
            float  _NoiseScale;
            half   _NoiseStrength;
            half   _BrushScale, _BrushStrength;
            float  _FloorHeight, _WindowSpacing;
            float4 _WindowSize;
            float4 _FacadeOffset, _WindowOffset;
            half4  _WindowBottomColor, _WindowTopColor, _WindowLitColor;
            half   _NightAmount, _LitRatio, _WindowEmission, _LedgeThickness, _LedgeDarken;
            half4  _AOColor;
            half   _VertexAOStrength;
            half4  _RimColor;
            half   _RimWidth, _RimSoftness, _RimIntensity, _RimGroundFade;
            half4  _EmissionColor;
        CBUFFER_END

        // Global style values, set by AnimeStyleController. Zero = neutral (no effect),
        // so materials look normal when no controller exists in the scene.
        float4 _Anime_ShadowTint;
        float  _Anime_ShadowTintAmount;
        float  _Anime_NightAmount;
        float  _Anime_LightIntensityOffset;
        float  _Anime_RimBoost;

        float EnvHash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float EnvNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = EnvHash21(i);
            float b = EnvHash21(i + float2(1, 0));
            float c = EnvHash21(i + float2(0, 1));
            float d = EnvHash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _FACADE_ON
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP

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
                half4  color      : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color      = IN.color;
                OUT.fogFactor  = ComputeFogFactor(p.positionCS.z);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    OUT.shadowCoord = ComputeScreenPos(p.positionCS);
                #else
                    OUT.shadowCoord = float4(0, 0, 0, 0);
                #endif
                return OUT;
            }

            half ToonStep(half v, half center, half soft)
            {
                return smoothstep(center - soft, center + soft, v);
            }

            half4 frag(Varyings IN, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                #if defined(_ALPHATEST_ON)
                    clip(baseTex.a * _BaseColor.a - _Cutoff);
                #endif
                half3 base = baseTex.rgb * _BaseColor.rgb;

                float3 N = normalize(IN.normalWS);
                N = IS_FRONT_VFACE(facing, N, -N);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                // world height gradient
                half gy = saturate((IN.positionWS.y - _GradientMinY) / max(_GradientMaxY - _GradientMinY, 0.001));
                half3 grad = lerp(_GradientBottomColor.rgb, _GradientTopColor.rgb, gy);
                base = lerp(base, base * grad, _GradientStrength);

                // painted-looking variation, no texture needed
                float2 nuv = abs(N.y) > 0.7 ? IN.positionWS.xz
                           : (abs(N.x) > abs(N.z) ? IN.positionWS.zy : IN.positionWS.xy);
                half pn = EnvNoise(nuv * _NoiseScale) * 0.65h + EnvNoise(nuv * _NoiseScale * 3.1) * 0.35h;
                base *= 1.0h + (pn - 0.5h) * _NoiseStrength * 2.0h;

                // procedural facade: windows and floor ledges from world position
                half3 windowEmission = 0;
                #if defined(_FACADE_ON)
                {
                    half wall = 1.0h - smoothstep(0.35h, 0.6h, abs(N.y));
                    float2 wallDir = normalize(float2(-N.z, N.x) + float2(1e-5, 0));
                    float u = (dot(IN.positionWS.xz, wallDir) + _FacadeOffset.x) / max(_WindowSpacing, 0.01);
                    float v = (IN.positionWS.y + _FacadeOffset.y) / max(_FloorHeight, 0.01);
                    float2 cell = float2(u, v);
                    float2 id = floor(cell);
                    float2 lc = frac(cell);
                    float2 m = (1.0 - _WindowSize.xy) * 0.5;
                    float2 wlc = lc - _WindowOffset.xy;
                    half inside = step(m.x, wlc.x) * step(wlc.x, 1.0 - m.x) * step(m.y, wlc.y) * step(wlc.y, 1.0 - m.y);
                    inside *= wall;
                    half rnd = EnvHash21(id + 17.0);
                    half lit = step(1.0h - _LitRatio, rnd) * max(_NightAmount, (half)_Anime_NightAmount);
                    half3 windowDay = lerp(_WindowBottomColor.rgb, _WindowTopColor.rgb, lc.y);
                    windowDay *= lerp(0.85h, 1.1h, EnvHash21(id + 3.0));
                    half3 windowCol = lerp(windowDay, _WindowLitColor.rgb, lit);
                    base = lerp(base, windowCol, inside);
                    windowEmission = _WindowLitColor.rgb * (lit * inside * _WindowEmission);
                    half ledge = (1.0h - step(_LedgeThickness, lc.y)) * wall;
                    base *= lerp(1.0h, 1.0h - _LedgeDarken, ledge);
                }
                #endif

                // baked vertex AO, tinted
                half ao = lerp(1.0h, IN.color.r, _VertexAOStrength);
                base = lerp(base * _AOColor.rgb, base, ao);

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = IN.shadowCoord;
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #endif
                Light L = GetMainLight(shadowCoord);
                half shadowAtten = L.shadowAttenuation;

                half NdotL = dot(N, L.direction);
                half t = NdotL * 0.5h + 0.5h;
                t *= lerp(1.0h, shadowAtten, _ReceiveShadow);

                // hand-painted brush overlay on the ramp coordinate
                half brush = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, nuv * _BrushScale).r;
                t += (brush - 0.5h) * _BrushStrength;

                half s = ToonStep(t, _Threshold, _Softness);
                half3 shadowTint = lerp(_ShadowTint.rgb, _Anime_ShadowTint.rgb, _Anime_ShadowTintAmount);
                half3 tint = lerp(shadowTint, _LitTint.rgb, s);

                half3 lighting = L.color * (_LightIntensity * (1.0h + _Anime_LightIntensityOffset)) + SampleSH(N) * _AmbientInfluence;
                half3 col = base * tint * lighting;

                // additional lights (point / spot), toon banded
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

                // rim light: view dependent, so it is faded out on flat ground
                half NdotV = saturate(dot(N, V));
                half rim = smoothstep(_RimWidth, _RimWidth + _RimSoftness, 1.0h - NdotV);
                half groundFade = lerp(1.0h, 1.0h - saturate(N.y), _RimGroundFade);
                half rimMask = smoothstep(0.0h, 0.4h, NdotL) * shadowAtten * groundFade;
                col += rim * rimMask * _RimColor.rgb * (_RimIntensity * (1.0h + _Anime_RimBoost)) * L.color;

                col += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb * _EmissionColor.rgb;
                col += windowEmission;

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

            struct SAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct SVaryings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            SVaryings vertShadow(SAttributes IN)
            {
                SVaryings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
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

            struct DAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct DVaryings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            DVaryings vertDepth(DAttributes IN)
            {
                DVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
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

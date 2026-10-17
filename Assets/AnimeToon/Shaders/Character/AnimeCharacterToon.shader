// Anime character toon shader for Unity URP (Unity 6).
// Based on techniques from "From mobile to high-end PC: Achieving high quality
// anime style rendering on Unity" (miHoYo).
//
// Vertex color:  R = outline width multiplier (paint hair tips to 0)
//                G = face shading mask (1 = normal shading, 0 = suppressed, use on face skin)
// UV2 (TEXCOORD2): tangent-space smoothed normal, written by AnimeSmoothNormalBaker.cs
// Mask texture:  R = specular mask, G = ramp hardness (0 hard, 1 soft), B = AO
Shader "Anime/CharacterToon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadingMask ("Mask: R=Spec  G=Ramp Hardness  B=AO", 2D) = "white" {}
        _AOStrength ("AO Strength", Range(0,1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Off = two sided, for single face meshes)", Float) = 2

        [Header(Three Layer Diffuse)]
        _LitTint ("Lit Tint", Color) = (1,1,1,1)
        _ShadowTint1 ("Shadow Tint 1", Color) = (0.85,0.78,0.92,1)
        _ShadowTint2 ("Shadow Tint 2 (Deep)", Color) = (0.6,0.5,0.8,1)
        _Threshold1 ("Lit / Shadow1 Threshold", Range(0,1)) = 0.5
        _Threshold2 ("Shadow1 / Shadow2 Threshold", Range(0,1)) = 0.25
        _SoftnessMin ("Edge Softness (Mask G = 0)", Range(0.001,0.5)) = 0.01
        _SoftnessMax ("Edge Softness (Mask G = 1)", Range(0.001,0.5)) = 0.08
        _ReceiveShadow ("Receive Shadow", Range(0,1)) = 1
        _LightIntensity ("Light Intensity", Range(0,2)) = 0.85
        _AmbientInfluence ("Ambient Influence", Range(0,1)) = 0.3

        [Header(Additional Lights)]
        _AdditionalLightIntensity ("Additional Light Intensity", Range(0,3)) = 1
        _AdditionalLightSoftness ("Additional Light Softness", Range(0.01,0.5)) = 0.15

        [Header(Brush Texture)]
        _BrushTex ("Brush (RGBA = 4 patterns)", 2D) = "gray" {}
        _BrushWeights ("Brush Channel Weights", Vector) = (1,0,0,0)
        _BrushScale ("Brush UV Scale", Float) = 4
        _BrushStrength ("Brush Strength", Range(0,0.5)) = 0

        [Header(Face)]
        [Toggle(_USE_FACE_MASK)] _UseFaceMask ("Use Vertex Color G As Face Shading Mask", Float) = 0
        _FaceSuppress ("Face Shading Suppression", Range(0,1)) = 0.9

        [Header(Rim Light)]
        _RimColor ("Rim Color", Color) = (1,0.95,0.9,1)
        _RimWidth ("Rim Width", Range(0,1)) = 0.6
        _RimSoftness ("Rim Softness", Range(0.001,0.5)) = 0.1
        _RimIntensity ("Rim Intensity", Range(0,2)) = 0.4

        [Header(Specular)]
        [Toggle(_ANISO_SPEC)] _AnisoSpec ("Anisotropic (Hair) Specular", Float) = 0
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecIntensity ("Specular Intensity", Range(0,2)) = 0
        _SpecThreshold ("Specular Threshold", Range(0,1)) = 0.5
        _SpecSoftness ("Specular Softness", Range(0.001,0.5)) = 0.05
        _SpecExpHigh ("High Frequency Exponent", Range(1,500)) = 200
        _SpecExpLow ("Low Frequency Exponent", Range(1,100)) = 30
        _SpecLowWeight ("Low Frequency Weight", Range(0,1)) = 0.4
        _SpecShiftTex ("Hair Shift Noise", 2D) = "gray" {}
        _SpecShiftTexStrength ("Shift Noise Strength", Range(0,1)) = 0.3
        _SpecShift1 ("Highlight Shift 1", Range(-1,1)) = 0.0
        _SpecShift2 ("Highlight Shift 2", Range(-1,1)) = 0.3

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.15,0.1,0.12,1)
        _OutlineWidth ("Outline Width (pixels)", Range(0,6)) = 1.5
        _OutlineBaseTint ("Tint Outline By Base Color", Range(0,1)) = 0.6
        _OutlineDistMin ("Outline Min Distance", Float) = 1
        _OutlineDistMax ("Outline Max Distance", Float) = 15
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_ShadingMask);  SAMPLER(sampler_ShadingMask);
        TEXTURE2D(_BrushTex);     SAMPLER(sampler_BrushTex);
        TEXTURE2D(_SpecShiftTex); SAMPLER(sampler_SpecShiftTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _AOStrength;
            half4  _LitTint, _ShadowTint1, _ShadowTint2;
            half   _Threshold1, _Threshold2, _SoftnessMin, _SoftnessMax;
            half   _ReceiveShadow, _LightIntensity, _AmbientInfluence;
            half   _AdditionalLightIntensity, _AdditionalLightSoftness;
            half4  _BrushWeights;
            half   _BrushScale, _BrushStrength;
            half   _FaceSuppress;
            half4  _RimColor;
            half   _RimWidth, _RimSoftness, _RimIntensity;
            half4  _SpecularColor;
            half   _SpecIntensity, _SpecThreshold, _SpecSoftness;
            half   _SpecExpHigh, _SpecExpLow, _SpecLowWeight;
            half   _SpecShiftTexStrength, _SpecShift1, _SpecShift2;
            half4  _OutlineColor;
            half   _OutlineWidth, _OutlineBaseTint, _OutlineDistMin, _OutlineDistMax;
        CBUFFER_END

        // Global style values, set by AnimeStyleController. Zero = neutral (no effect),
        // so materials look normal when no controller exists in the scene.
        float4 _Anime_ShadowTint;
        float  _Anime_ShadowTintAmount;
        float  _Anime_NightAmount;
        float  _Anime_LightIntensityOffset;
        float  _Anime_RimBoost;
        ENDHLSL

        // ------------------------------------------------------------
        // Main toon pass
        // ------------------------------------------------------------
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
            #pragma shader_feature_local _USE_FACE_MASK
            #pragma shader_feature_local _ANISO_SPEC
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float3 tangentWS  : TEXCOORD3;
                half4  color      : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS   = n.normalWS;
                OUT.tangentWS  = n.tangentWS;
                OUT.color      = IN.color;
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
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;
                half4 mask = SAMPLE_TEXTURE2D(_ShadingMask, sampler_ShadingMask, IN.uv);

                float3 N = normalize(IN.normalWS);
                N = IS_FRONT_VFACE(facing, N, -N);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = IN.shadowCoord;
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #endif
                Light L = GetMainLight(shadowCoord);
                half shadowAtten = L.shadowAttenuation;

                half NdotL = dot(N, L.direction);
                half t = NdotL * 0.5h + 0.5h; // half lambert

                // shadow + AO feed the ramp coordinate
                t *= lerp(1.0h, shadowAtten, _ReceiveShadow);
                half ao = lerp(1.0h, mask.b, _AOStrength);
                t *= ao;

                // brush texture perturbs the ramp coordinate for a hand-painted feel
                half4 brushSample = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, IN.uv * _BrushScale);
                half brush = dot(brushSample, _BrushWeights);
                t += (brush - 0.5h) * _BrushStrength;

                // face: suppress shading using vertex color G
                #if defined(_USE_FACE_MASK)
                    t = lerp(t, 1.0h, (1.0h - IN.color.g) * _FaceSuppress);
                #endif

                // three layer diffuse, ramp softness chosen per pixel by mask G
                half soft = lerp(_SoftnessMin, _SoftnessMax, mask.g);
                half s1 = ToonStep(t, _Threshold1, soft);
                half s2 = ToonStep(t, _Threshold2, soft);
                half3 shadowTint1 = lerp(_ShadowTint1.rgb, _Anime_ShadowTint.rgb, _Anime_ShadowTintAmount);
                half3 shadowTint2 = lerp(_ShadowTint2.rgb, _Anime_ShadowTint.rgb * 0.75h, _Anime_ShadowTintAmount);
                half3 tint = lerp(shadowTint2, shadowTint1, s2);
                tint = lerp(tint, _LitTint.rgb, s1);

                half3 ambient = SampleSH(N) * _AmbientInfluence;
                half3 lighting = L.color * (_LightIntensity * (1.0h + _Anime_LightIntensityOffset)) + ambient;
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

                // rim light (directional), masked by lit side, shadow and AO
                half NdotV = saturate(dot(N, V));
                half rim = smoothstep(_RimWidth, _RimWidth + _RimSoftness, 1.0h - NdotV);
                half rimMask = smoothstep(0.0h, 0.4h, NdotL) * shadowAtten * ao;
                col += rim * rimMask * _RimColor.rgb * (_RimIntensity * (1.0h + _Anime_RimBoost)) * L.color;

                // stepped specular
                if (_SpecIntensity > 0.0h)
                {
                    float3 H = normalize(L.direction + V);
                    half spec;
                    #if defined(_ANISO_SPEC)
                        half shiftNoise = SAMPLE_TEXTURE2D(_SpecShiftTex, sampler_SpecShiftTex, IN.uv).r - 0.5h;
                        float3 T  = normalize(IN.tangentWS);
                        float3 T1 = normalize(T + N * (_SpecShift1 + shiftNoise * _SpecShiftTexStrength));
                        float3 T2 = normalize(T + N * (_SpecShift2 + shiftNoise * _SpecShiftTexStrength));
                        float tH1 = dot(T1, H);
                        float tH2 = dot(T2, H);
                        half hi = pow(sqrt(saturate(1.0 - tH1 * tH1)), _SpecExpHigh);
                        half lo = pow(sqrt(saturate(1.0 - tH2 * tH2)), _SpecExpLow);
                        spec = ToonStep(hi, _SpecThreshold, _SpecSoftness)
                             + ToonStep(lo, _SpecThreshold, _SpecSoftness) * _SpecLowWeight;
                    #else
                        half nH = saturate(dot(N, H));
                        spec = ToonStep(pow(nH, _SpecExpHigh), _SpecThreshold, _SpecSoftness);
                    #endif
                    spec *= smoothstep(0.0h, 0.3h, NdotL) * shadowAtten * mask.r;
                    col += spec * _SpecularColor.rgb * _SpecIntensity * L.color;
                }

                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // Back-face outline. Uses smoothed normals from UV2 (tangent space),
        // vertex color R for per-vertex width, pixel-constant width with distance clamp.
        // ------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vertOutline
            #pragma fragment fragOutline

            struct OAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float3 smoothTS   : TEXCOORD2;
                float4 color      : COLOR;
            };

            struct OVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            OVaryings vertOutline(OAttributes IN)
            {
                OVaryings OUT;
                // width 0 fully disables the outline (otherwise back faces of open meshes show as black)
                if (_OutlineWidth <= 0.0001h)
                {
                    OUT.positionCS = float4(0, 0, 0, 0);
                    OUT.uv = float2(0, 0);
                    return OUT;
                }

                float3 nOS = IN.normalOS;
                if (dot(IN.smoothTS, IN.smoothTS) > 0.0001)
                {
                    float3 bitan = cross(IN.normalOS, IN.tangentOS.xyz) * IN.tangentOS.w;
                    nOS = IN.tangentOS.xyz * IN.smoothTS.x + bitan * IN.smoothTS.y + IN.normalOS * IN.smoothTS.z;
                }

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS = normalize(TransformObjectToWorldNormal(nOS));

                float dist = clamp(distance(_WorldSpaceCameraPos, posWS), _OutlineDistMin, _OutlineDistMax);
                float worldPerPixel = dist / unity_CameraProjection._m11 * 2.0 / _ScreenParams.y;
                posWS += nWS * (_OutlineWidth * worldPerPixel * IN.color.r);

                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 fragOutline(OVaryings IN) : SV_Target
            {
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb * _BaseColor.rgb;
                half3 col = _OutlineColor.rgb * lerp(half3(1,1,1), base, _OutlineBaseTint);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // Shadow caster
        // ------------------------------------------------------------
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

            float3 _LightDirection;
            float3 _LightPosition;

            struct SAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct SVaryings   { float4 positionCS : SV_POSITION; };

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
                return OUT;
            }

            half4 fragShadow(SVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------
        // Depth only
        // ------------------------------------------------------------
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

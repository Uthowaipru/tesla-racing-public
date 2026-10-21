// Stylized anime glass for URP (Unity 6). Mobile friendly: no grab pass, no refraction.
// Reflects the real skybox / reflection probe (blended with a fake sky gradient), height tint gradient,
// fresnel alpha, diagonal highlight streaks, toon sun glint.
// NIGHT WINDOWS: when the style controller's night amount rises (or the material's own Night Amount), the glass turns
// into a dark mirror and a grid of windows lights up, like the facade of Anime/EnvironmentToon.
Shader "Anime/Glass"
{
    Properties
    {
        [Header(Glass)]
        _GlassTint ("Glass Tint (Bottom)", Color) = (0.35,0.6,0.85,1)
        _GlassTopTint ("Glass Tint (Top)", Color) = (0.75,0.9,1,1)
        _GradientMinY ("Gradient Min Y", Float) = 0
        _GradientMaxY ("Gradient Max Y", Float) = 30
        _BaseAlpha ("Base Alpha", Range(0,1)) = 0.4
        _FresnelPower ("Fresnel Power", Range(0.5,8)) = 3
        _FresnelAlpha ("Fresnel Alpha Boost", Range(0,1)) = 0.5

        [Header(Reflection)]
        _ReflectionStrength ("Reflection Strength", Range(0,1)) = 0.55
        _ProbeBlend ("Real Reflection Blend (0 = fake gradient)", Range(0,1)) = 0.8
        _ProbeRoughness ("Reflection Blur", Range(0,1)) = 0.15
        _ReflectionBoost ("Reflection Brightness", Range(0,3)) = 1.2
        _HorizonColor ("Fake Horizon Color", Color) = (1,0.85,0.9,1)
        _ZenithColor ("Fake Zenith Color", Color) = (0.5,0.7,1,1)

        [Header(Highlight Streaks)]
        _StreakColor ("Streak Color", Color) = (1,1,1,1)
        _StreakIntensity ("Streak Intensity", Range(0,1)) = 0.5
        _StreakScale ("Streak Scale", Float) = 2
        _StreakWidth ("Streak Width", Range(0.01,0.5)) = 0.12
        _StreakSoftness ("Streak Softness", Range(0.001,0.2)) = 0.02
        _StreakParallax ("Streak View Parallax", Float) = 0.5

        [Header(Sun Glint)]
        _SunThreshold ("Sun Threshold", Range(0,1)) = 0.6
        _SunExponent ("Sun Exponent", Range(1,200)) = 60
        _SunIntensity ("Sun Intensity", Range(0,2)) = 0.8

        [Header(Night Windows)]
        [Toggle] _NightWindows ("Enable Night Windows", Float) = 1
        _NightAmount ("Night Amount (material, added to the style controller's)", Range(0,1)) = 0
        _NightTintBrightness ("Glass Brightness At Night", Range(0,1)) = 0.35
        _NightReflection ("Reflection At Night", Range(0,1)) = 0.4
        _NightAlpha ("Alpha At Night", Range(0,1)) = 0.85
        _FloorHeight ("Floor Height", Float) = 3
        _WindowSpacing ("Window Spacing", Float) = 2.5
        _WindowSize ("Window Size (W, H)", Vector) = (0.7,0.65,0,0)
        _FacadeOffset ("Pattern Offset (Along Wall, Height)", Vector) = (0,0,0,0)
        _WindowUnlitColor ("Unlit Window Color", Color) = (0.05,0.08,0.18,1)
        _WindowLitColor ("Lit Window Color", Color) = (1,0.85,0.5,1)
        _LitRatio ("Lit Window Ratio", Range(0,1)) = 0.45
        _WindowEmission ("Window Emission", Range(0,5)) = 2.2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "ForwardGlass"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlassTint, _GlassTopTint;
                float _GradientMinY, _GradientMaxY;
                half  _BaseAlpha, _FresnelPower, _FresnelAlpha;
                half  _ReflectionStrength, _ProbeBlend, _ProbeRoughness, _ReflectionBoost;
                half4 _HorizonColor, _ZenithColor;
                half4 _StreakColor;
                half  _StreakIntensity;
                float _StreakScale;
                half  _StreakWidth, _StreakSoftness;
                float _StreakParallax;
                half  _SunThreshold, _SunExponent, _SunIntensity;
                half  _NightWindows, _NightAmount, _NightTintBrightness, _NightReflection, _NightAlpha;
                float _FloorHeight, _WindowSpacing;
                float4 _WindowSize, _FacadeOffset;
                half4 _WindowUnlitColor, _WindowLitColor;
                half  _LitRatio, _WindowEmission;
            CBUFFER_END

            // set by AnimeStyleController; 0 = day
            float _Anime_NightAmount;

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

            float GlassHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // samples the scene's default reflection (skybox) or the nearest reflection probe
            half3 SampleReflectionProbe(float3 R, half roughness)
            {
                half mip = roughness * 6.0h;
                half4 enc = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, R, mip);
                return DecodeHDREnvironment(enc, unity_SpecCube0_HDR);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = IN.uv;
                OUT.fogFactor  = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                // 0 by day, 1 at night (style controller and/or the material's own value)
                half night = (_NightWindows > 0.5h) ? saturate(max(_NightAmount, (half)_Anime_NightAmount)) : 0.0h;

                half NdotV = saturate(dot(N, V));
                half fres = pow(1.0h - NdotV, _FresnelPower);

                // tint gradient by world height, darker at night
                half gy = saturate((IN.positionWS.y - _GradientMinY) / max(_GradientMaxY - _GradientMinY, 0.001));
                half3 tint = lerp(_GlassTint.rgb, _GlassTopTint.rgb, gy);
                tint *= lerp(1.0h, _NightTintBrightness, night);

                // reflection: real probe/skybox blended with a fake gradient, weaker at night
                float3 R = reflect(-V, N);
                half sky = saturate(R.y * 0.5 + 0.5);
                half3 fake = lerp(_HorizonColor.rgb, _ZenithColor.rgb, sky);
                half3 probe = SampleReflectionProbe(R, _ProbeRoughness) * _ReflectionBoost;
                half3 refl = lerp(fake, probe, _ProbeBlend);
                half reflAmount = saturate((_ReflectionStrength + fres) * lerp(1.0h, _NightReflection, night));
                half3 col = lerp(tint, refl, reflAmount);

                // diagonal highlight streaks that slide with the view angle
                float coord = (IN.uv.x + IN.uv.y * 0.5) * _StreakScale + V.x * _StreakParallax;
                half f = frac(coord);
                half streak = smoothstep(0.0h, _StreakSoftness, f)
                            * (1.0h - smoothstep(_StreakWidth, _StreakWidth + _StreakSoftness, f));
                col += streak * _StreakColor.rgb * _StreakIntensity * (1.0h - night * 0.7h);

                // toon sun glint
                Light L = GetMainLight();
                float3 H = normalize(L.direction + V);
                half sunSpec = pow(saturate(dot(N, H)), _SunExponent);
                half sun = smoothstep(_SunThreshold - 0.02h, _SunThreshold + 0.02h, sunSpec);
                col += sun * L.color * _SunIntensity;

                half alpha = saturate(_BaseAlpha + fres * _FresnelAlpha + streak * _StreakIntensity * 0.5h + sun);

                // night windows: a grid of cells on the wall, some of them lit
                if (night > 0.001h)
                {
                    half wall = 1.0h - smoothstep(0.35h, 0.6h, abs(N.y));
                    float2 wallDir = normalize(float2(-N.z, N.x) + float2(1e-5, 0));
                    float u = (dot(IN.positionWS.xz, wallDir) + _FacadeOffset.x) / max(_WindowSpacing, 0.01);
                    float v = (IN.positionWS.y + _FacadeOffset.y) / max(_FloorHeight, 0.01);
                    float2 cell = float2(u, v);
                    float2 id = floor(cell);
                    float2 lc = frac(cell);
                    float2 m = (1.0 - _WindowSize.xy) * 0.5;
                    half inside = step(m.x, lc.x) * step(lc.x, 1.0 - m.x) * step(m.y, lc.y) * step(lc.y, 1.0 - m.y);
                    inside *= wall * night;

                    half lit = step(1.0h - _LitRatio, (half)GlassHash(id + 17.0));
                    half3 windowCol = lerp(_WindowUnlitColor.rgb, _WindowLitColor.rgb, lit);

                    col = lerp(col, windowCol, inside);
                    col += _WindowLitColor.rgb * (lit * inside * _WindowEmission);

                    // the glass is more solid at night, and the windows are opaque
                    alpha = lerp(alpha, max(alpha, _NightAlpha), night);
                    alpha = max(alpha, inside * 0.95h);
                }

                col = MixFog(col, IN.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}

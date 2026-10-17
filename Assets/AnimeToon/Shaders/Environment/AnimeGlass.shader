// Stylized anime glass for URP (Unity 6). Mobile friendly: no grab pass, no refraction.
// Reflects the real skybox / reflection probe (blended with a fake sky gradient),
// has a world-height tint gradient, fresnel alpha, diagonal streaks and a toon sun glint.
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
            CBUFFER_END

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

                half NdotV = saturate(dot(N, V));
                half fres = pow(1.0h - NdotV, _FresnelPower);

                // tint gradient by world height, so tall glass is never one flat color
                half gy = saturate((IN.positionWS.y - _GradientMinY) / max(_GradientMaxY - _GradientMinY, 0.001));
                half3 tint = lerp(_GlassTint.rgb, _GlassTopTint.rgb, gy);

                // reflection: real probe/skybox blended with a fake gradient
                float3 R = reflect(-V, N);
                half sky = saturate(R.y * 0.5 + 0.5);
                half3 fake = lerp(_HorizonColor.rgb, _ZenithColor.rgb, sky);
                half3 probe = SampleReflectionProbe(R, _ProbeRoughness) * _ReflectionBoost;
                half3 refl = lerp(fake, probe, _ProbeBlend);
                half3 col = lerp(tint, refl, saturate(_ReflectionStrength + fres));

                // diagonal highlight streaks that slide with the view angle
                float coord = (IN.uv.x + IN.uv.y * 0.5) * _StreakScale + V.x * _StreakParallax;
                half f = frac(coord);
                half streak = smoothstep(0.0h, _StreakSoftness, f)
                            * (1.0h - smoothstep(_StreakWidth, _StreakWidth + _StreakSoftness, f));
                col += streak * _StreakColor.rgb * _StreakIntensity;

                // toon sun glint
                Light L = GetMainLight();
                float3 H = normalize(L.direction + V);
                half sunSpec = pow(saturate(dot(N, H)), _SunExponent);
                half sun = smoothstep(_SunThreshold - 0.02h, _SunThreshold + 0.02h, sunSpec);
                col += sun * L.color * _SunIntensity;

                half alpha = saturate(_BaseAlpha + fres * _FresnelAlpha + streak * _StreakIntensity * 0.5h + sun);

                col = MixFog(col, IN.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}

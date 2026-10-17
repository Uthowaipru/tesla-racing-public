// Stylized anime water for URP (Unity 6).
// Needs "Depth Texture" enabled in the URP asset (for shoreline foam and depth color).
// Procedural: no textures required. Shallow/deep color, stepped shore foam,
// animated ripple lines, fake + real sky reflection, toon sun glint, gentle vertex waves.
Shader "Anime/Water"
{
    Properties
    {
        [Header(Depth Color)]
        _ShallowColor ("Shallow Color", Color) = (0.45,0.88,0.9,1)
        _DeepColor ("Deep Color", Color) = (0.1,0.3,0.65,1)
        _ShallowAlpha ("Shallow Alpha", Range(0,1)) = 0.4
        _DeepAlpha ("Deep Alpha", Range(0,1)) = 0.9
        _DepthMax ("Depth For Full Deep Color", Float) = 3

        [Header(Shore Foam)]
        _FoamColor ("Foam Color", Color) = (1,1,1,1)
        _FoamDistance ("Foam Distance", Float) = 0.7
        _FoamNoiseScale ("Foam Noise Scale", Float) = 2.5
        _FoamEdge ("Hard Foam Line Width", Float) = 0.08
        _FoamSpeed ("Foam Speed", Float) = 0.3

        [Header(Ripple Lines)]
        _PatternColor ("Ripple Color", Color) = (1,1,1,1)
        _PatternScale ("Ripple Scale", Float) = 0.6
        _PatternSpeed ("Ripple Speed", Float) = 0.15
        _PatternWidth ("Ripple Line Width", Range(0.005,0.15)) = 0.04
        _PatternIntensity ("Ripple Intensity", Range(0,1)) = 0.5

        [Header(Waves)]
        _WaveHeight ("Wave Height", Float) = 0.05
        _WaveFrequency ("Wave Frequency", Float) = 0.6
        _WaveSpeed ("Wave Speed", Float) = 1

        [Header(Reflection)]
        _HorizonColor ("Fake Horizon Color", Color) = (1,0.88,0.92,1)
        _ZenithColor ("Fake Zenith Color", Color) = (0.55,0.75,1,1)
        _ReflectionStrength ("Reflection Strength", Range(0,1)) = 0.25
        _ProbeBlend ("Real Reflection Blend", Range(0,1)) = 0.6
        _FresnelPower ("Fresnel Power", Range(0.5,8)) = 4

        [Header(Sun Glint)]
        _SunThreshold ("Sun Threshold", Range(0,1)) = 0.6
        _SunExponent ("Sun Exponent", Range(1,300)) = 80
        _SunIntensity ("Sun Intensity", Range(0,2)) = 0.9
        _SunNoise ("Sun Sparkle Distortion", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "ForwardWater"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor;
                half  _ShallowAlpha, _DeepAlpha;
                float _DepthMax;
                half4 _FoamColor;
                float _FoamDistance, _FoamNoiseScale, _FoamEdge, _FoamSpeed;
                half4 _PatternColor;
                float _PatternScale, _PatternSpeed;
                half  _PatternWidth, _PatternIntensity;
                float _WaveHeight, _WaveFrequency, _WaveSpeed;
                half4 _HorizonColor, _ZenithColor;
                half  _ReflectionStrength, _ProbeBlend, _FresnelPower;
                half  _SunThreshold, _SunExponent, _SunIntensity, _SunNoise;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  eyeDepth   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
            };

            float WaterHash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float WaterNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = WaterHash(i);
                float b = WaterHash(i + float2(1, 0));
                float c = WaterHash(i + float2(0, 1));
                float d = WaterHash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half3 SampleReflectionProbe(float3 R, half roughness)
            {
                half mip = roughness * 6.0h;
                half4 enc = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, R, mip);
                return DecodeHDREnvironment(enc, unity_SpecCube0_HDR);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 pw = TransformObjectToWorld(IN.positionOS.xyz);
                float w = sin(pw.x * _WaveFrequency + _Time.y * _WaveSpeed)
                        + sin(pw.z * _WaveFrequency * 1.3 - _Time.y * _WaveSpeed * 0.8);
                pw.y += w * 0.5 * _WaveHeight;

                OUT.positionWS = pw;
                OUT.positionCS = TransformWorldToHClip(pw);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.eyeDepth   = -TransformWorldToView(pw).z;
                OUT.fogFactor  = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

                // water depth from the scene depth buffer
                float2 suv = GetNormalizedScreenSpaceUV(IN.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float diff = max(sceneEye - IN.eyeDepth, 0.0);
                half depth01 = saturate(diff / max(_DepthMax, 0.01));

                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);
                half alpha = lerp(_ShallowAlpha, _DeepAlpha, depth01);

                // animated ripple lines: thin contours where two noise layers cross
                float2 wp = IN.positionWS.xz;
                float t = _Time.y * _PatternSpeed;
                float n1 = WaterNoise(wp * _PatternScale + float2(t, t * 0.6));
                float n2 = WaterNoise(wp * _PatternScale * 1.7 - float2(t * 0.8, t));
                half ripple = 1.0h - smoothstep(0.0h, _PatternWidth, abs(n1 - n2));
                col = lerp(col, _PatternColor.rgb, ripple * _PatternIntensity * (1.0h - depth01 * 0.6h));

                // reflection: fake sky gradient blended with the real probe / skybox
                half NdotV = saturate(dot(N, V));
                half fres = pow(1.0h - NdotV, _FresnelPower);
                float3 R = reflect(-V, N);
                half sky = saturate(R.y);
                half3 fake = lerp(_HorizonColor.rgb, _ZenithColor.rgb, sky);
                half3 probe = SampleReflectionProbe(R, 0.2h);
                half3 refl = lerp(fake, probe, _ProbeBlend);
                col = lerp(col, refl, saturate(_ReflectionStrength + fres * 0.6h));

                // toon sun glint on a noise-distorted normal, gives sparkling highlights
                float3 Np = normalize(N + float3(n1 - 0.5, 0.0, n2 - 0.5) * _SunNoise);
                Light L = GetMainLight();
                float3 H = normalize(L.direction + V);
                half sunSpec = pow(saturate(dot(Np, H)), _SunExponent);
                half sun = smoothstep(_SunThreshold - 0.02h, _SunThreshold + 0.02h, sunSpec);
                col += sun * L.color * _SunIntensity;

                // stepped shore foam with a noisy edge, plus a hard line right at the shore
                half foamMask = 1.0h - saturate(diff / max(_FoamDistance, 0.01));
                half foamNoise = WaterNoise(wp * _FoamNoiseScale + float2(_Time.y * _FoamSpeed, 0.0));
                half foam = step(foamNoise, foamMask * 1.15h);
                foam = max(foam, step(diff, _FoamEdge));
                col = lerp(col, _FoamColor.rgb, foam);

                alpha = saturate(max(alpha, foam) + fres * 0.3h + sun);

                col = MixFog(col, IN.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}

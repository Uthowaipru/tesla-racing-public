// Anime sky for URP (Unity 6). Assign as the scene Skybox material.
// Gradient with optional bands, horizon glow, toon sun/moon disc, toon clouds
// (procedural, two-tone, lit on the sun-facing edge) and stars.
// Colors come from the material, or from AnimeStyleController when it drives the sky.
Shader "Anime/Sky"
{
    Properties
    {
        [Header(Gradient)]
        _ZenithColor ("Zenith Color", Color) = (0.25,0.5,0.95,1)
        _HorizonColor ("Horizon Color", Color) = (0.75,0.9,1,1)
        _GroundColor ("Ground Color", Color) = (0.55,0.6,0.7,1)
        _GradientPower ("Gradient Power", Range(0.1,3)) = 0.7
        _GroundBlend ("Ground Blend", Range(0.1,10)) = 3
        _SkyBands ("Sky Bands (0 = smooth)", Range(0,12)) = 0

        [Header(Horizon Glow)]
        _GlowColor ("Glow Color", Color) = (1,0.8,0.6,1)
        _GlowAmount ("Glow Amount", Range(0,2)) = 0.1
        _GlowFalloff ("Glow Height Falloff", Range(1,30)) = 6
        _GlowSpread ("Glow Azimuth Spread", Range(0.5,8)) = 2

        [Header(Sun or Moon Disc)]
        _SunColor ("Sun Color", Color) = (1,0.95,0.85,1)
        _SunIntensity ("Sun Intensity (HDR, feeds bloom)", Range(0,10)) = 3
        _SunSize ("Sun Size", Range(0.0001,0.02)) = 0.002
        _SunSoftness ("Sun Edge Softness", Range(0.01,1)) = 0.1
        _HaloIntensity ("Halo Intensity", Range(0,2)) = 0.4
        _HaloExponent ("Halo Exponent", Range(2,200)) = 40

        [Header(Clouds)]
        _CloudLit ("Cloud Lit Color", Color) = (1,1,1,1)
        _CloudShadow ("Cloud Shadow Color", Color) = (0.7,0.78,0.95,1)
        _CloudCoverage ("Cloud Coverage", Range(0,1)) = 0.5
        _CloudScale ("Cloud Scale", Float) = 0.8
        _CloudSpeed ("Cloud Speed", Float) = 0.01
        _CloudWindDir ("Wind Direction (X, Z)", Vector) = (1,0.3,0,0)
        _CloudSoftness ("Cloud Edge Softness", Range(0.005,0.3)) = 0.05
        _CloudHorizonOffset ("Cloud Horizon Offset", Range(0.05,1)) = 0.2
        _CloudFadeHeight ("Cloud Horizon Fade Height", Range(0.02,0.6)) = 0.2
        _CloudLightOffset ("Cloud Light Offset", Range(0.01,0.5)) = 0.08
        _CloudLitThreshold ("Cloud Lit Threshold", Range(0,1)) = 0.35
        _CloudToonSoftness ("Cloud Toon Softness", Range(0.005,0.5)) = 0.08
        _CloudAmbientLight ("Cloud Ambient Light", Range(0,1)) = 0.5

        [Header(Stars)]
        _StarScale ("Star Scale", Float) = 60
        _StarDensity ("Star Density", Range(0,0.2)) = 0.04
        _StarAmount ("Star Amount (when not driven)", Range(0,1)) = 0
        _StarIntensity ("Star Brightness", Range(0,5)) = 1.5
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "AnimeSky"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor;
                half  _GradientPower, _GroundBlend, _SkyBands;
                half4 _GlowColor;
                half  _GlowAmount, _GlowFalloff, _GlowSpread;
                half4 _SunColor;
                half  _SunIntensity, _SunSize, _SunSoftness, _HaloIntensity, _HaloExponent;
                half4 _CloudLit, _CloudShadow;
                half  _CloudCoverage;
                float _CloudScale, _CloudSpeed;
                float4 _CloudWindDir;
                half  _CloudSoftness, _CloudHorizonOffset, _CloudFadeHeight;
                half  _CloudLightOffset, _CloudLitThreshold, _CloudToonSoftness, _CloudAmbientLight;
                float _StarScale;
                half  _StarDensity, _StarAmount, _StarIntensity;
            CBUFFER_END

            // Set by AnimeStyleController. _Anime_SkyOverride = 0 means use the material values.
            float  _Anime_SkyOverride;
            float  _Anime_CloudQuality;   // 0 = full, 1 = simple, 2 = off (set by AnimeQuality)
            float4 _Anime_SkyZenith;
            float4 _Anime_SkyHorizon;
            float4 _Anime_SkyGround;
            float4 _Anime_HorizonGlow;   // rgb = color, a = amount
            float4 _Anime_CloudLit;
            float4 _Anime_CloudShadow;
            float  _Anime_CloudCoverage;
            float  _Anime_StarAmount;
            float4 _Anime_SunColor;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirOS      : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dirOS = IN.positionOS.xyz;
                return OUT;
            }

            float SkyHash12(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float SkyHash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
            }

            float SkyNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = SkyHash12(i);
                float b = SkyHash12(i + float2(1, 0));
                float c = SkyHash12(i + float2(0, 1));
                float d = SkyHash12(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float SkyFbm(float2 p)
            {
                float a = 0.5;
                float s = 0.0;
                for (int i = 0; i < 3; i++)
                {
                    s += a * SkyNoise(p);
                    p = p * 2.03 + 17.3;
                    a *= 0.5;
                }
                return s / 0.875;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dirOS);
                float y = dir.y;

                float ov = _Anime_SkyOverride;
                half3 zenith  = lerp(_ZenithColor.rgb,  _Anime_SkyZenith.rgb,  ov);
                half3 horizon = lerp(_HorizonColor.rgb, _Anime_SkyHorizon.rgb, ov);
                half3 ground  = lerp(_GroundColor.rgb,  _Anime_SkyGround.rgb,  ov);
                half3 glowCol = lerp(_GlowColor.rgb,    _Anime_HorizonGlow.rgb, ov);
                half  glowAmt = lerp(_GlowAmount,       _Anime_HorizonGlow.a,   ov);
                half3 cloudLit    = lerp(_CloudLit.rgb,    _Anime_CloudLit.rgb,    ov);
                half3 cloudShadow = lerp(_CloudShadow.rgb, _Anime_CloudShadow.rgb, ov);
                half  coverage    = lerp(_CloudCoverage,   _Anime_CloudCoverage,   ov);
                half  starAmt     = lerp(_StarAmount,      _Anime_StarAmount,      ov);
                half3 sunCol      = lerp(_SunColor.rgb,    _Anime_SunColor.rgb,    ov);

                float3 sunDir = _MainLightPosition.xyz;
                sunDir = dot(sunDir, sunDir) > 0.0001 ? normalize(sunDir) : float3(0.4, 0.8, 0.4);

                // gradient, optionally banded like a painted sky
                float t = pow(saturate(y), _GradientPower);
                if (_SkyBands > 0.5)
                {
                    float tb = t * _SkyBands;
                    tb = floor(tb) + smoothstep(0.35, 0.65, frac(tb));
                    t = tb / _SkyBands;
                }
                half3 sky = lerp(horizon, zenith, t);
                sky = lerp(sky, ground, saturate(-y * _GroundBlend));

                // horizon glow toward the sun
                float2 hd = normalize(dir.xz + float2(1e-5, 0));
                float2 sd2 = normalize(sunDir.xz + float2(1e-5, 0));
                float az = saturate(dot(hd, sd2) * 0.5 + 0.5);
                float glow = pow(az, _GlowSpread * 4.0) * exp(-abs(y) * _GlowFalloff);
                sky += glowCol * (glow * glowAmt);

                // stars
                if (starAmt > 0.001 && y > 0.0)
                {
                    float3 p = dir * _StarScale;
                    float3 id = floor(p);
                    float3 f = frac(p) - 0.5;
                    float h = SkyHash13(id);
                    float on = step(1.0 - _StarDensity, h);
                    float3 jitter = (float3(SkyHash13(id + 1.3), SkyHash13(id + 2.7), SkyHash13(id + 4.1)) - 0.5) * 0.5;
                    float size = lerp(0.04, 0.14, frac(h * 37.0));
                    float d = length(f - jitter);
                    float star = on * (1.0 - smoothstep(size * 0.4, size, d));
                    float twinkle = 0.75 + 0.25 * sin(_Time.y * 2.5 + h * 60.0);
                    sky += half3(0.9, 0.95, 1.0) * (star * twinkle * _StarIntensity * smoothstep(0.0, 0.25, y) * starAmt);
                }

                // sun or moon disc with a soft halo
                float sd = dot(dir, sunDir);
                float disc = 1.0 - smoothstep(_SunSize * (1.0 - _SunSoftness), _SunSize, 1.0 - sd);
                float halo = pow(saturate(sd), _HaloExponent) * _HaloIntensity;
                sky += sunCol * ((disc * _SunIntensity + halo) * smoothstep(-0.03, 0.02, y));

                // toon clouds: thresholded noise, lit on the sun facing edge
                float cloudFade = smoothstep(0.0, _CloudFadeHeight, y);
                half cloudMask = 0.0h;
                half3 cloudCol = 0.0h;
                if (cloudFade > 0.001 && _Anime_CloudQuality < 1.5)
                {
                    float2 wind = normalize(_CloudWindDir.xy + float2(1e-5, 0)) * (_Time.y * _CloudSpeed);
                    float2 cuv = dir.xz / (y + _CloudHorizonOffset);
                    float2 p = cuv * _CloudScale + wind;
                    float th = lerp(0.72, 0.32, coverage);
                    float dens = SkyFbm(p);
                    cloudMask = smoothstep(th, th + _CloudSoftness, dens);
                    if (cloudMask > 0.001h && _Anime_CloudQuality > 0.5)
                    {
                        // simple: one flat tone, no second noise lookup for the lit edge
                        cloudCol = cloudLit;
                    }
                    else if (cloudMask > 0.001h)
                    {
                        float2 sunOff = normalize(sunDir.xz + float2(1e-5, 0)) * _CloudLightOffset;
                        float densSun = SkyFbm(p + sunOff);
                        float edge = clamp((dens - densSun) * 10.0, -1.0, 1.0);
                        half lit = smoothstep(_CloudLitThreshold, _CloudLitThreshold + _CloudToonSoftness, edge + _CloudAmbientLight);
                        cloudCol = lerp(cloudShadow, cloudLit, lit);
                    }
                }
                sky = lerp(sky, cloudCol, cloudMask * cloudFade);

                return half4(sky, 1.0h);
            }
            ENDHLSL
        }
    }
}

using System;
using UnityEngine;

namespace AnimeToon
{
    /// <summary>Which quality tier is in effect.</summary>
    public enum AnimeQualityLevel { Low = 0, Medium = 1, High = 2 }

    /// <summary>
    /// What the player chose. Auto picks a tier for the device and steps down by itself if the game runs slowly.
    /// Custom is the player's own mix of settings (the graphics menu switches to it when a single setting is changed).
    /// </summary>
    public enum AnimeQualityMode { Auto = 0, Low = 1, Medium = 2, High = 3, Custom = 4 }

    /// <summary>Everything one quality tier changes.</summary>
    [Serializable]
    public class AnimeQualityTier
    {
        [Header("Resolution and frame rate")]
        [Range(0.4f, 1f)] public float renderScale = 1f;
        [Tooltip("Frame rate cap. Phones: 30 is smoother and cooler than a 60 target that is never reached.")]
        public int targetFps = 60;

        [Header("URP asset")]
        [Tooltip("1 = off, 2 or 4 = MSAA.")]
        public int msaa = 1;
        public bool hdr = true;
        [Tooltip("Shadow distance in meters. Used only when Shadows is on.")]
        public float shadowDistance = 40f;
        [Range(1, 4)] public int shadowCascades = 2;
        public bool shadows = true;
        [Tooltip("How many point / spot lights can touch one object. 0 = none.")]
        [Range(0, 8)] public int maxAdditionalLights = 4;

        [Header("Post processing")]
        public bool bloom = true;

        [Header("Anime shaders")]
        [Tooltip("Back-face outline on characters.")]
        public bool outlines = true;
        [Tooltip("Skips painted noise, brush overlay and water ripples.")]
        public bool simpleShading = false;
        [Tooltip("0 = full toon clouds, 1 = flat clouds (half the sky cost), 2 = no clouds.")]
        [Range(0, 2)] public int cloudQuality = 0;

        [Header("Textures and models")]
        [Tooltip("0 = full size textures, 1 = half size, 2 = quarter size.")]
        [Range(0, 2)] public int textureMipLimit = 0;
        [Range(0.3f, 2f)] public float lodBias = 1f;
        public bool anisotropic = true;

        [Header("Effects")]
        [Tooltip("Multiplies particle emission rates in the anime effect scripts.")]
        [Range(0.1f, 1f)] public float particleScale = 1f;
        public bool skidMarks = true;
        public bool driftSparks = true;

        public static AnimeQualityTier Create(AnimeQualityLevel level)
        {
            switch (level)
            {
                case AnimeQualityLevel.Low:
                    return new AnimeQualityTier
                    {
                        renderScale = 0.6f, targetFps = 30, msaa = 1, hdr = false,
                        shadows = false, shadowDistance = 0f, shadowCascades = 1, maxAdditionalLights = 0,
                        bloom = false, outlines = false, simpleShading = true, cloudQuality = 1,
                        textureMipLimit = 1, lodBias = 0.7f, anisotropic = false,
                        particleScale = 0.4f, skidMarks = false, driftSparks = false
                    };

                case AnimeQualityLevel.Medium:
                    return new AnimeQualityTier
                    {
                        renderScale = 0.8f, targetFps = 60, msaa = 1, hdr = true,
                        shadows = true, shadowDistance = 25f, shadowCascades = 1, maxAdditionalLights = 2,
                        bloom = true, outlines = true, simpleShading = false, cloudQuality = 0,
                        textureMipLimit = 0, lodBias = 1f, anisotropic = false,
                        particleScale = 0.7f, skidMarks = true, driftSparks = false
                    };

                default:
                    return new AnimeQualityTier
                    {
                        renderScale = 1f, targetFps = 60, msaa = 2, hdr = true,
                        shadows = true, shadowDistance = 50f, shadowCascades = 2, maxAdditionalLights = 4,
                        bloom = true, outlines = true, simpleShading = false, cloudQuality = 0,
                        textureMipLimit = 0, lodBias = 1.3f, anisotropic = true,
                        particleScale = 1f, skidMarks = true, driftSparks = true
                    };
            }
        }
    }

    /// <summary>
    /// Optional asset holding the three tiers so you can edit them. Without it the built-in defaults are used.
    /// Create it with Tools > Anime Toon > Create Quality Settings Asset (it lives in Resources/AnimeToon).
    /// </summary>
    public class AnimeQualitySettings : ScriptableObject
    {
        public const string ResourcePath = "AnimeToon/AnimeQualitySettings";

        public AnimeQualityTier low = AnimeQualityTier.Create(AnimeQualityLevel.Low);
        public AnimeQualityTier medium = AnimeQualityTier.Create(AnimeQualityLevel.Medium);
        public AnimeQualityTier high = AnimeQualityTier.Create(AnimeQualityLevel.High);

        public AnimeQualityTier Get(AnimeQualityLevel level)
        {
            switch (level)
            {
                case AnimeQualityLevel.Low: return low;
                case AnimeQualityLevel.Medium: return medium;
                default: return high;
            }
        }

        public static AnimeQualitySettings Load()
        {
            var asset = Resources.Load<AnimeQualitySettings>(ResourcePath);
            if (asset != null) return asset;

            asset = CreateInstance<AnimeQualitySettings>();
            asset.hideFlags = HideFlags.DontSave;
            return asset;
        }
    }
}

using UnityEngine;

namespace AnimeToon
{
    /// <summary>
    /// One "mood" (Day, Dusk, Night...). Contains no game specific data, so it can be
    /// reused in any project. Create via Assets > Create > Anime Toon > Style Preset,
    /// or Tools > Anime Toon > Create Default Style Presets.
    /// </summary>
    [CreateAssetMenu(menuName = "Anime Toon/Style Preset", fileName = "NewAnimeStylePreset")]
    public class AnimeStylePreset : ScriptableObject
    {
        [Header("Sun / Moon")]
        public Color sunColor = new Color(1f, 0.96f, 0.88f);
        public float sunIntensity = 1.1f;
        public Vector3 sunEuler = new Vector3(50f, -35f, 0f);

        [Header("Ambient (Trilight)")]
        public Color ambientSky = new Color(0.62f, 0.78f, 1f);
        public Color ambientEquator = new Color(0.8f, 0.82f, 0.9f);
        public Color ambientGround = new Color(0.55f, 0.5f, 0.6f);

        [Header("Fog (Exponential Squared)")]
        public Color fogColor = new Color(0.78f, 0.88f, 1f);
        public float fogDensity = 0.006f;

        [Header("Toon Shader Globals (0 = leave materials alone)")]
        public Color shadowTint = new Color(0.6f, 0.58f, 0.88f);
        [Range(0f, 1f)] public float shadowTintAmount = 0.5f;
        [Range(0f, 1f)] public float nightAmount = 0f;
        [Range(-1f, 1f)] public float lightIntensityOffset = 0f;
        [Range(-1f, 3f)] public float rimBoost = 0f;
        [Tooltip("Wet roads (Anime/RoadWet): puddles, reflections and light streaks.")]
        [Range(0f, 1f)] public float wetness = 0f;

        [Header("Sky (needs a skybox material using Anime/Sky)")]
        public Color skyZenith = new Color(0.25f, 0.5f, 0.95f);
        public Color skyHorizon = new Color(0.75f, 0.9f, 1f);
        public Color skyGround = new Color(0.55f, 0.6f, 0.7f);
        public Color horizonGlowColor = new Color(1f, 0.85f, 0.7f);
        [Range(0f, 2f)] public float horizonGlowAmount = 0.15f;
        public Color cloudLit = Color.white;
        public Color cloudShadow = new Color(0.7f, 0.78f, 0.95f);
        [Range(0f, 1f)] public float cloudCoverage = 0.5f;
        [Range(0f, 1f)] public float starAmount = 0f;

        [Header("Post Processing (needs an Anime Volume, see Tools > Anime Toon)")]
        [Range(0f, 3f)] public float bloomIntensity = 0.6f;
        [Range(0f, 2f)] public float bloomThreshold = 1f;
        [Range(-100f, 100f)] public float saturation = 15f;
        [Range(-100f, 100f)] public float contrast = 8f;
        public float postExposure = 0f;
        public Color colorFilter = Color.white;
        [Range(0f, 1f)] public float vignetteIntensity = 0.18f;

        public struct Snapshot
        {
            public Color sunColor;
            public float sunIntensity;
            public Quaternion sunRotation;
            public Color ambientSky, ambientEquator, ambientGround;
            public Color fogColor;
            public float fogDensity;
            public Color shadowTint;
            public float shadowTintAmount, nightAmount, lightIntensityOffset, rimBoost, wetness;
            public Color skyZenith, skyHorizon, skyGround;
            public Color horizonGlowColor;
            public float horizonGlowAmount;
            public Color cloudLit, cloudShadow;
            public float cloudCoverage, starAmount;
            public float bloomIntensity, bloomThreshold, saturation, contrast, postExposure;
            public Color colorFilter;
            public float vignetteIntensity;

            public static Snapshot Lerp(Snapshot a, Snapshot b, float t)
            {
                Snapshot s;
                s.sunColor = Color.Lerp(a.sunColor, b.sunColor, t);
                s.sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);
                s.sunRotation = Quaternion.Slerp(a.sunRotation, b.sunRotation, t);
                s.ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t);
                s.ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
                s.ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t);
                s.fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
                s.fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t);
                s.shadowTint = Color.Lerp(a.shadowTint, b.shadowTint, t);
                s.shadowTintAmount = Mathf.Lerp(a.shadowTintAmount, b.shadowTintAmount, t);
                s.nightAmount = Mathf.Lerp(a.nightAmount, b.nightAmount, t);
                s.lightIntensityOffset = Mathf.Lerp(a.lightIntensityOffset, b.lightIntensityOffset, t);
                s.rimBoost = Mathf.Lerp(a.rimBoost, b.rimBoost, t);
                s.wetness = Mathf.Lerp(a.wetness, b.wetness, t);
                s.skyZenith = Color.Lerp(a.skyZenith, b.skyZenith, t);
                s.skyHorizon = Color.Lerp(a.skyHorizon, b.skyHorizon, t);
                s.skyGround = Color.Lerp(a.skyGround, b.skyGround, t);
                s.horizonGlowColor = Color.Lerp(a.horizonGlowColor, b.horizonGlowColor, t);
                s.horizonGlowAmount = Mathf.Lerp(a.horizonGlowAmount, b.horizonGlowAmount, t);
                s.cloudLit = Color.Lerp(a.cloudLit, b.cloudLit, t);
                s.cloudShadow = Color.Lerp(a.cloudShadow, b.cloudShadow, t);
                s.cloudCoverage = Mathf.Lerp(a.cloudCoverage, b.cloudCoverage, t);
                s.starAmount = Mathf.Lerp(a.starAmount, b.starAmount, t);
                s.bloomIntensity = Mathf.Lerp(a.bloomIntensity, b.bloomIntensity, t);
                s.bloomThreshold = Mathf.Lerp(a.bloomThreshold, b.bloomThreshold, t);
                s.saturation = Mathf.Lerp(a.saturation, b.saturation, t);
                s.contrast = Mathf.Lerp(a.contrast, b.contrast, t);
                s.postExposure = Mathf.Lerp(a.postExposure, b.postExposure, t);
                s.colorFilter = Color.Lerp(a.colorFilter, b.colorFilter, t);
                s.vignetteIntensity = Mathf.Lerp(a.vignetteIntensity, b.vignetteIntensity, t);
                return s;
            }
        }

        public Snapshot ToSnapshot()
        {
            Snapshot s;
            s.sunColor = sunColor;
            s.sunIntensity = sunIntensity;
            s.sunRotation = Quaternion.Euler(sunEuler);
            s.ambientSky = ambientSky;
            s.ambientEquator = ambientEquator;
            s.ambientGround = ambientGround;
            s.fogColor = fogColor;
            s.fogDensity = fogDensity;
            s.shadowTint = shadowTint;
            s.shadowTintAmount = shadowTintAmount;
            s.nightAmount = nightAmount;
            s.lightIntensityOffset = lightIntensityOffset;
            s.rimBoost = rimBoost;
            s.wetness = wetness;
            s.skyZenith = skyZenith;
            s.skyHorizon = skyHorizon;
            s.skyGround = skyGround;
            s.horizonGlowColor = horizonGlowColor;
            s.horizonGlowAmount = horizonGlowAmount;
            s.cloudLit = cloudLit;
            s.cloudShadow = cloudShadow;
            s.cloudCoverage = cloudCoverage;
            s.starAmount = starAmount;
            s.bloomIntensity = bloomIntensity;
            s.bloomThreshold = bloomThreshold;
            s.saturation = saturation;
            s.contrast = contrast;
            s.postExposure = postExposure;
            s.colorFilter = colorFilter;
            s.vignetteIntensity = vignetteIntensity;
            return s;
        }
    }
}

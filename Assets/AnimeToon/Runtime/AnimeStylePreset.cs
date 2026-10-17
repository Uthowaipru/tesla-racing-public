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

        public struct Snapshot
        {
            public Color sunColor;
            public float sunIntensity;
            public Quaternion sunRotation;
            public Color ambientSky, ambientEquator, ambientGround;
            public Color fogColor;
            public float fogDensity;
            public Color shadowTint;
            public float shadowTintAmount, nightAmount, lightIntensityOffset, rimBoost;

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
            return s;
        }
    }
}

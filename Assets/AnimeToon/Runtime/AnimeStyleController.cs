using UnityEngine;
using UnityEngine.Rendering;

namespace AnimeToon
{
    /// <summary>
    /// Blends between style presets (e.g. Day, Dusk, Night) and drives the sun, ambient,
    /// fog and the global values the Anime toon shaders read. Works in edit mode too.
    /// Presets are spread evenly over timeOfDay 0..1.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AnimeStyleController : MonoBehaviour
    {
        [Header("Presets (evenly spaced over 0..1, e.g. Day, Dusk, Night)")]
        public AnimeStylePreset[] presets;

        [Range(0f, 1f)] public float timeOfDay = 0f;

        [Header("Auto Play")]
        public bool autoPlay = false;
        public float cycleSeconds = 120f;
        [Tooltip("On: Day > Night > Day. Off: wraps from the last preset straight to the first.")]
        public bool pingPong = true;

        [Header("What this controller drives")]
        [Tooltip("Leave empty to use RenderSettings.sun")]
        public Light sun;
        public bool controlSun = true;
        public bool controlAmbient = true;
        public bool controlFog = true;

        static readonly int ShadowTintId = Shader.PropertyToID("_Anime_ShadowTint");
        static readonly int ShadowTintAmountId = Shader.PropertyToID("_Anime_ShadowTintAmount");
        static readonly int NightAmountId = Shader.PropertyToID("_Anime_NightAmount");
        static readonly int LightOffsetId = Shader.PropertyToID("_Anime_LightIntensityOffset");
        static readonly int RimBoostId = Shader.PropertyToID("_Anime_RimBoost");

        float _direction = 1f;

        void OnEnable() { Apply(); }
        void OnDisable() { ResetGlobals(); }
        void OnValidate() { Apply(); }

        void Update()
        {
            if (autoPlay && Application.isPlaying)
            {
                float step = Time.deltaTime / Mathf.Max(cycleSeconds, 0.01f);
                if (pingPong)
                {
                    timeOfDay += step * _direction;
                    if (timeOfDay >= 1f) { timeOfDay = 1f; _direction = -1f; }
                    else if (timeOfDay <= 0f) { timeOfDay = 0f; _direction = 1f; }
                }
                else
                {
                    timeOfDay = Mathf.Repeat(timeOfDay + step, 1f);
                }
            }
            Apply();
        }

        public void SetTimeOfDay(float t)
        {
            timeOfDay = Mathf.Clamp01(t);
            Apply();
        }

        [ContextMenu("Apply Now")]
        public void Apply()
        {
            if (!TryEvaluate(timeOfDay, out AnimeStylePreset.Snapshot s)) return;

            if (controlSun)
            {
                Light l = sun != null ? sun : RenderSettings.sun;
                if (l != null)
                {
                    l.color = s.sunColor;
                    l.intensity = s.sunIntensity;
                    l.transform.rotation = s.sunRotation;
                }
            }

            if (controlAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = s.ambientSky;
                RenderSettings.ambientEquatorColor = s.ambientEquator;
                RenderSettings.ambientGroundColor = s.ambientGround;
            }

            if (controlFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = s.fogColor;
                RenderSettings.fogDensity = s.fogDensity;
            }

            Shader.SetGlobalColor(ShadowTintId, s.shadowTint);
            Shader.SetGlobalFloat(ShadowTintAmountId, s.shadowTintAmount);
            Shader.SetGlobalFloat(NightAmountId, s.nightAmount);
            Shader.SetGlobalFloat(LightOffsetId, s.lightIntensityOffset);
            Shader.SetGlobalFloat(RimBoostId, s.rimBoost);
        }

        bool TryEvaluate(float t, out AnimeStylePreset.Snapshot result)
        {
            result = default;
            if (presets == null) return false;

            // ignore empty slots
            int count = 0;
            for (int i = 0; i < presets.Length; i++) if (presets[i] != null) count++;
            if (count == 0) return false;

            var list = new AnimeStylePreset[count];
            int k = 0;
            for (int i = 0; i < presets.Length; i++) if (presets[i] != null) list[k++] = presets[i];

            if (count == 1) { result = list[0].ToSnapshot(); return true; }

            float f = Mathf.Clamp01(t) * (count - 1);
            int index = Mathf.Min(Mathf.FloorToInt(f), count - 2);
            float blend = f - index;
            result = AnimeStylePreset.Snapshot.Lerp(list[index].ToSnapshot(), list[index + 1].ToSnapshot(), blend);
            return true;
        }

        static void ResetGlobals()
        {
            Shader.SetGlobalFloat(ShadowTintAmountId, 0f);
            Shader.SetGlobalFloat(NightAmountId, 0f);
            Shader.SetGlobalFloat(LightOffsetId, 0f);
            Shader.SetGlobalFloat(RimBoostId, 0f);
        }
    }
}

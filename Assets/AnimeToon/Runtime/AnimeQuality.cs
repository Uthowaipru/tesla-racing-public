using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AnimeToon
{
    /// <summary>
    /// Quality tiers (Low / Medium / High) and Auto, for phones from cheap to flagship.
    ///
    ///   AnimeQuality.SetMode(AnimeQualityMode.Auto / Low / Medium / High)   choose (saved between sessions)
    ///   AnimeQuality.CycleMode()                                            for a single "GRAPHICS" button
    ///   AnimeQuality.Label                                                  text for that button
    ///
    /// Auto picks a tier from the device (memory, cores, GPU) the first time, and if the game then runs below about
    /// 24 fps for a while it drops one tier by itself (see AnimeQualityRunner). It never goes back up on its own.
    ///
    /// It starts by itself before the first scene loads. A tier changes: render scale, frame cap, MSAA, HDR, shadows,
    /// extra lights, bloom, character outlines, simple shading, clouds, texture size, LOD bias and effect density.
    /// The project's URP asset is never edited: a copy is used while the game runs.
    /// </summary>
    public static class AnimeQuality
    {
        const string ModeKey = "AnimeToon.Quality.Mode";
        const string AutoLevelKey = "AnimeToon.Quality.AutoLevel";
        const string CustomKey = "AnimeToon.Quality.Custom";

        static readonly int LowQualityId = Shader.PropertyToID("_Anime_LowQuality");
        static readonly int OutlineOffId = Shader.PropertyToID("_Anime_OutlineOff");
        static readonly int CloudQualityId = Shader.PropertyToID("_Anime_CloudQuality");

        static AnimeQualitySettings settings;
        static AnimeQualityMode mode = AnimeQualityMode.Auto;
        static AnimeQualityLevel level = AnimeQualityLevel.High;
        static AnimeQualityTier tier;
        static AnimeQualityTier custom;   // the player's own settings (mode Custom)
        static bool started;

        static RenderPipelineAsset levelPipelineOriginal;   // what the quality level had before we replaced it (may be null)
        static UniversalRenderPipelineAsset pipelineCopy;
        static readonly Dictionary<Light, LightShadows> savedShadows = new Dictionary<Light, LightShadows>();

        /// <summary>Raised after a tier was applied.</summary>
        public static event Action Changed;

        public static AnimeQualityMode Mode { get { return mode; } }
        public static AnimeQualityLevel Level { get { return level; } }

        /// <summary>The tier in effect. Before the system starts this is the High tier.</summary>
        public static AnimeQualityTier Tier
        {
            get
            {
                if (tier == null) tier = AnimeQualityTier.Create(AnimeQualityLevel.High);
                return tier;
            }
        }

        public static float ParticleScale { get { return Tier.particleScale; } }
        public static bool SkidMarks { get { return Tier.skidMarks; } }
        public static bool DriftSparks { get { return Tier.driftSparks; } }

        /// <summary>True while Auto may still step down: Auto mode, on a phone, above the Low tier.</summary>
        public static bool AdaptiveActive
        {
            get { return mode == AnimeQualityMode.Auto && Application.isMobilePlatform && level > AnimeQualityLevel.Low; }
        }

        /// <summary>For a GRAPHICS button, for example "AUTO (LOW)" or "HIGH".</summary>
        public static string Label
        {
            get
            {
                return mode == AnimeQualityMode.Auto
                    ? "AUTO (" + level.ToString().ToUpperInvariant() + ")"
                    : mode.ToString().ToUpperInvariant();
            }
        }

        // ------------------------------------------------------------------ start

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            settings = null;
            tier = null;
            custom = null;
            started = false;
            levelPipelineOriginal = null;
            pipelineCopy = null;
            savedShadows.Clear();
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Start()
        {
            if (started) return;
            started = true;

            settings = AnimeQualitySettings.Load();
            mode = (AnimeQualityMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, (int)AnimeQualityMode.Auto), 0, 4);

            Apply(ResolveLevel());

            var go = new GameObject("AnimeQuality");
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<AnimeQualityRunner>();

            Application.quitting += Restore;
        }

        // ------------------------------------------------------------------ custom settings

        /// <summary>
        /// The player's own settings. Edit the fields, then call ApplyCustom(). If a preset was active, it is copied first
        /// and the mode switches to Custom, so changing one setting never loses the rest.
        /// </summary>
        public static AnimeQualityTier BeginCustom()
        {
            if (!started) Start();

            if (mode != AnimeQualityMode.Custom)
            {
                custom = Clone(Tier);
                mode = AnimeQualityMode.Custom;
                PlayerPrefs.SetInt(ModeKey, (int)mode);
            }
            return LoadCustom();
        }

        /// <summary>Applies and saves the custom settings after they were edited.</summary>
        public static void ApplyCustom()
        {
            AnimeQualityTier c = LoadCustom();
            c.renderScale = Mathf.Clamp(c.renderScale, 0.4f, 1f);
            c.targetFps = Mathf.Clamp(c.targetFps, 15, 120);
            c.msaa = c.msaa >= 4 ? 4 : c.msaa >= 2 ? 2 : 1;
            c.particleScale = Mathf.Clamp(c.particleScale, 0.1f, 1f);

            mode = AnimeQualityMode.Custom;
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            PlayerPrefs.SetString(CustomKey, JsonUtility.ToJson(c));
            PlayerPrefs.Save();
            Apply(level);
        }

        static AnimeQualityTier LoadCustom()
        {
            if (custom != null) return custom;

            string json = PlayerPrefs.GetString(CustomKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { custom = JsonUtility.FromJson<AnimeQualityTier>(json); }
                catch (ArgumentException) { custom = null; }
            }

            if (custom == null) custom = Clone(tier != null ? tier : settings.Get(level));
            return custom;
        }

        static AnimeQualityTier Clone(AnimeQualityTier source)
        {
            return JsonUtility.FromJson<AnimeQualityTier>(JsonUtility.ToJson(source));
        }

        // ------------------------------------------------------------------ choosing

        public static void SetMode(AnimeQualityMode newMode)
        {
            if (!started) Start();

            // going to Custom from a preset: use the saved custom settings, or start from what is in effect now
            if (newMode == AnimeQualityMode.Custom && custom == null) custom = LoadCustom();

            mode = newMode;
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            PlayerPrefs.Save();
            Apply(ResolveLevel());
        }

        public static void CycleMode()
        {
            // Auto > Low > Medium > High > Auto (Custom is reached from the graphics menu)
            SetMode(mode >= AnimeQualityMode.High ? AnimeQualityMode.Auto : (AnimeQualityMode)((int)mode + 1));
        }

        /// <summary>Drops one tier (used by Auto when the game runs slowly). Remembers it for the next launch.</summary>
        public static void StepDown()
        {
            if (level <= AnimeQualityLevel.Low) return;

            var lower = (AnimeQualityLevel)((int)level - 1);
            PlayerPrefs.SetInt(AutoLevelKey, (int)lower);
            PlayerPrefs.Save();
            Debug.Log("AnimeQuality: running slowly, stepping down to " + lower + ".");
            Apply(lower);
        }

        static AnimeQualityLevel ResolveLevel()
        {
            switch (mode)
            {
                case AnimeQualityMode.Low: return AnimeQualityLevel.Low;
                case AnimeQualityMode.Medium: return AnimeQualityLevel.Medium;
                case AnimeQualityMode.High: return AnimeQualityLevel.High;
                case AnimeQualityMode.Custom: return level;
            }

            // Auto: what we decided on an earlier launch, otherwise guess from the device
            if (PlayerPrefs.HasKey(AutoLevelKey))
                return (AnimeQualityLevel)Mathf.Clamp(PlayerPrefs.GetInt(AutoLevelKey), 0, 2);

            AnimeQualityLevel detected = Detect();
            PlayerPrefs.SetInt(AutoLevelKey, (int)detected);
            PlayerPrefs.Save();
            return detected;
        }

        /// <summary>A guess from memory, cores and GPU name. Computers always get High.</summary>
        public static AnimeQualityLevel Detect()
        {
            if (!Application.isMobilePlatform) return AnimeQualityLevel.High;

            int ram = SystemInfo.systemMemorySize;       // MB
            int cores = SystemInfo.processorCount;
            string gpu = SystemInfo.graphicsDeviceName.ToLowerInvariant();

            int score = 0;
            if (ram >= 8000) score += 3;
            else if (ram >= 5500) score += 2;
            else if (ram >= 3500) score += 1;
            if (cores >= 8) score += 1;

            bool weakGpu = gpu.Contains("mali-t") || gpu.Contains("mali-g31") || gpu.Contains("mali-g51") ||
                           gpu.Contains("mali-g52") || gpu.Contains("powervr") || gpu.Contains("adreno (tm) 5") ||
                           gpu.Contains("adreno (tm) 610") || gpu.Contains("adreno (tm) 612") || gpu.Contains("adreno (tm) 613");
            if (weakGpu) score -= 2;

            AnimeQualityLevel result = score >= 4 ? AnimeQualityLevel.High : score >= 2 ? AnimeQualityLevel.Medium : AnimeQualityLevel.Low;
            Debug.Log("AnimeQuality: detected " + result + " (RAM " + ram + " MB, " + cores + " cores, GPU " + SystemInfo.graphicsDeviceName + ").");
            return result;
        }

        // ------------------------------------------------------------------ applying

        static void Apply(AnimeQualityLevel newLevel)
        {
            level = newLevel;
            tier = mode == AnimeQualityMode.Custom ? LoadCustom() : settings.Get(newLevel);

            ApplyPipeline(tier);
            ApplyEngineSettings(tier);
            ApplyShaderGlobals(tier);
            ApplySceneBits();

            if (Changed != null) Changed();
        }

        static void ApplyPipeline(AnimeQualityTier t)
        {
            var current = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (current == null) return; // not URP: nothing to change here

            if (pipelineCopy == null)
            {
                levelPipelineOriginal = QualitySettings.renderPipeline;
                pipelineCopy = UnityEngine.Object.Instantiate(current);
                pipelineCopy.name = current.name + " (AnimeQuality copy)";
                QualitySettings.renderPipeline = pipelineCopy;
            }

            pipelineCopy.renderScale = t.renderScale;
            pipelineCopy.msaaSampleCount = Mathf.Clamp(t.msaa, 1, 8);
            pipelineCopy.supportsHDR = t.hdr;
            pipelineCopy.shadowDistance = t.shadows ? Mathf.Max(0f, t.shadowDistance) : 0f;
            pipelineCopy.shadowCascadeCount = Mathf.Clamp(t.shadowCascades, 1, 4);
            pipelineCopy.maxAdditionalLightsCount = Mathf.Clamp(t.maxAdditionalLights, 0, 8);
        }

        static void ApplyEngineSettings(AnimeQualityTier t)
        {
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(t.textureMipLimit, 0, 3);
            QualitySettings.lodBias = t.lodBias;
            QualitySettings.anisotropicFiltering = t.anisotropic ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = t.targetFps;
        }

        static void ApplyShaderGlobals(AnimeQualityTier t)
        {
            Shader.SetGlobalFloat(LowQualityId, t.simpleShading ? 1f : 0f);
            Shader.SetGlobalFloat(OutlineOffId, t.outlines ? 0f : 1f);
            Shader.SetGlobalFloat(CloudQualityId, Mathf.Clamp(t.cloudQuality, 0, 2));
        }

        /// <summary>Parts that live in the scene (bloom in the Volumes, shadows of the sun). Called again after every scene load.</summary>
        public static void ApplySceneBits()
        {
            AnimeQualityTier t = Tier;

            foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                VolumeProfile profile = volume.profile;
                if (profile != null && profile.TryGet(out Bloom bloom)) bloom.active = t.bloom;
            }

            if (savedShadows.Count > 0)
            {
                var gone = new List<Light>();
                foreach (Light saved in savedShadows.Keys) if (saved == null) gone.Add(saved);
                foreach (Light g in gone) savedShadows.Remove(g);
            }

            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;

                if (!t.shadows)
                {
                    if (light.shadows != LightShadows.None)
                    {
                        savedShadows[light] = light.shadows;
                        light.shadows = LightShadows.None;
                    }
                }
                else if (savedShadows.TryGetValue(light, out LightShadows original))
                {
                    light.shadows = original;
                    savedShadows.Remove(light);
                }
            }
        }

        /// <summary>Called every second or two: keeps the frame cap (other scripts may set their own) and the scene parts in line.</summary>
        public static void Tick()
        {
            if (Application.targetFrameRate != Tier.targetFps) Application.targetFrameRate = Tier.targetFps;
            ApplySceneBits();
        }

        static void Restore()
        {
            // give the project's own pipeline asset back, so the editor / quality settings are left as they were
            if (pipelineCopy != null) QualitySettings.renderPipeline = levelPipelineOriginal;
            Shader.SetGlobalFloat(LowQualityId, 0f);
            Shader.SetGlobalFloat(OutlineOffId, 0f);
            Shader.SetGlobalFloat(CloudQualityId, 0f);
        }
    }
}

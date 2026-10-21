// Editor only. Place in an "Editor" folder.
// One-click anime looks for existing materials: buildings, walls, roads, sidewalks, glass and water.
// Use it from Tools > Anime Toon > Material Presets (AnimeMaterialPresetWindow.cs).
//
// Applying a preset switches the material to the matching Anime shader and sets all its look values.
// By default the material keeps its own base color and texture, so your colors survive.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AnimeToon.Editor
{
    public enum AnimePreset
    {
        BuildingPastel,
        BuildingConcrete,
        BuildingShop,
        WallPlain,
        RoadAsphalt,
        RoadMarking,
        Sidewalk,
        RoadWet,
        FoliageTree,
        FoliageBush,
        FoliageGrass,
        Glass,
        Water
    }

    public static class AnimeMaterialPresets
    {
        const string SampleFolder = "Assets/AnimeToon/Materials/Presets";

        /// <summary>When on, building and wall presets also get the silhouette outline and the corner lines.</summary>
        public static bool BuildingOutlines;

        static bool IsBuilding(AnimePreset p)
        {
            return p == AnimePreset.BuildingPastel || p == AnimePreset.BuildingConcrete ||
                   p == AnimePreset.BuildingShop || p == AnimePreset.WallPlain;
        }

        // ------------------------------------------------------------------ info

        public static string Describe(AnimePreset p)
        {
            switch (p)
            {
                case AnimePreset.BuildingPastel:
                    return "Soft pastel building: windows, floor ledges, purple-to-white height gradient, light painted variation.";
                case AnimePreset.BuildingConcrete:
                    return "Muted concrete building: smaller windows, darker grimy base, stronger painted variation.";
                case AnimePreset.BuildingShop:
                    return "Colorful shop or market: wide low windows, many lit windows at night, saturated tints.";
                case AnimePreset.WallPlain:
                    return "Plain wall or fence: no windows, gentle gradient and painted variation.";
                case AnimePreset.RoadAsphalt:
                    return "Asphalt: dark lavender gray, patchy variation, no rim light.";
                case AnimePreset.RoadMarking:
                    return "Painted lane markings: warm white, light shadow tint, no rim light.";
                case AnimePreset.Sidewalk:
                    return "Sidewalk and curb: warm gray with patchy variation.";
                case AnimePreset.RoadWet:
                    return "Anime/RoadWet: asphalt that gets wet when the style controller's Wetness is up: puddles, sky reflection, light streaks.";
                case AnimePreset.FoliageTree:
                    return "Anime/Foliage for tree crowns and branches: puffy spherical shading, root-to-tip color, backlight glow, wind. Pivot at the base.";
                case AnimePreset.FoliageBush:
                    return "Anime/Foliage for bushes, leaves and flowers: soft spherical shading, gentle wind.";
                case AnimePreset.FoliageGrass:
                    return "Anime/Foliage for grass: strong root-to-tip gradient and lively wind.";
                case AnimePreset.Glass:
                    return "Anime/Glass: reflects the skybox, tint gradient by height, highlight streaks. Use on transparent glass.";
                default:
                    return "Anime/Water: shallow and deep color, shoreline foam, ripples. Needs Depth Texture on in the URP asset.";
            }
        }

        /// <summary>Guess a preset from a material's name (glass, water, asphalt, curb, building ...).</summary>
        public static bool TryGuess(string materialName, out AnimePreset preset)
        {
            string n = materialName.ToLowerInvariant();
            preset = AnimePreset.BuildingPastel;

            if (n.Contains("glass")) { preset = AnimePreset.Glass; return true; }
            if (n.Contains("water")) { preset = AnimePreset.Water; return true; }
            if (n.Contains("marking") || n.Contains("lane") || n.Contains("stripe")) { preset = AnimePreset.RoadMarking; return true; }
            if (n.Contains("curb") || n.Contains("kerb") || n.Contains("sidewalk") || n.Contains("pavement")) { preset = AnimePreset.Sidewalk; return true; }
            if (n.Contains("wetroad") || n.Contains("wet road")) { preset = AnimePreset.RoadWet; return true; }
            if (n.Contains("asphalt") || n.Contains("road")) { preset = AnimePreset.RoadAsphalt; return true; }
            if (n.Contains("grass")) { preset = AnimePreset.FoliageGrass; return true; }
            if (n.Contains("tree") || n.Contains("branch") || n.Contains("spruce") || n.Contains("fir") || n.Contains("pine")) { preset = AnimePreset.FoliageTree; return true; }
            if (n.Contains("bush") || n.Contains("leaf") || n.Contains("leaves") || n.Contains("flower") || n.Contains("foliage")) { preset = AnimePreset.FoliageBush; return true; }
            if (n.Contains("shop") || n.Contains("market") || n.Contains("store")) { preset = AnimePreset.BuildingShop; return true; }
            if (n.Contains("building") || n.Contains("tower") || n.Contains("house")) { preset = AnimePreset.BuildingPastel; return true; }
            if (n.Contains("wall") || n.Contains("fence")) { preset = AnimePreset.WallPlain; return true; }
            return false;
        }

        // ------------------------------------------------------------------ selection

        /// <summary>Materials in the selection: selected materials, materials inside selected folders, and the materials of selected objects.</summary>
        public static List<Material> CollectSelection()
        {
            var set = new HashSet<Material>();

            foreach (Material m in Selection.GetFiltered<Material>(SelectionMode.DeepAssets))
                if (m != null) set.Add(m);

            foreach (GameObject go in Selection.GetFiltered<GameObject>(SelectionMode.Unfiltered))
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        if (m != null) set.Add(m);

            return new List<Material>(set);
        }

        // ------------------------------------------------------------------ apply

        public static bool Apply(Material m, AnimePreset preset, bool keepColor, bool keepTexture)
        {
            if (m == null) return false;

            bool glass = preset == AnimePreset.Glass;
            bool water = preset == AnimePreset.Water;
            bool roadWet = preset == AnimePreset.RoadWet;
            bool foliage = preset == AnimePreset.FoliageTree || preset == AnimePreset.FoliageBush || preset == AnimePreset.FoliageGrass;
            string shaderName = glass ? "Anime/Glass" : water ? "Anime/Water" : roadWet ? "Anime/RoadWet" : foliage ? "Anime/Foliage" : "Anime/EnvironmentToon";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("Anime material presets: shader '" + shaderName + "' not found.");
                return false;
            }

            Undo.RecordObject(m, "Apply Anime Preset");

            // remember what the material looked like
            bool hasColor = false;
            Color oldColor = Color.white;
            if (m.HasProperty("_BaseColor")) { oldColor = m.GetColor("_BaseColor"); hasColor = true; }
            else if (m.HasProperty("_Color")) { oldColor = m.GetColor("_Color"); hasColor = true; }
            oldColor.a = 1f;

            Texture oldTexture = null;
            Vector2 oldScale = Vector2.one, oldOffset = Vector2.zero;
            if (m.HasProperty("_BaseMap"))
            {
                oldTexture = m.GetTexture("_BaseMap");
                oldScale = m.GetTextureScale("_BaseMap");
                oldOffset = m.GetTextureOffset("_BaseMap");
            }
            else if (m.HasProperty("_MainTex"))
            {
                oldTexture = m.GetTexture("_MainTex");
                oldScale = m.GetTextureScale("_MainTex");
                oldOffset = m.GetTextureOffset("_MainTex");
            }

            m.shader = shader;
            m.renderQueue = -1; // back to the shader's own queue

            if (glass) SetGlass(m);
            else if (water) SetWater(m);
            else if (roadWet)
            {
                SetRoadWet(m);
                m.SetColor("_BaseColor", keepColor && hasColor ? oldColor : new Color(0.3f, 0.29f, 0.37f));
                m.SetTexture("_BaseMap", keepTexture ? oldTexture : null);
                if (keepTexture && oldTexture != null)
                {
                    m.SetTextureScale("_BaseMap", oldScale);
                    m.SetTextureOffset("_BaseMap", oldOffset);
                }
            }
            else if (foliage)
            {
                Color leafColor = SetFoliage(m, preset);
                m.SetColor("_BaseColor", keepColor && hasColor ? oldColor : leafColor);
                m.SetTexture("_BaseMap", keepTexture ? oldTexture : null);
                if (keepTexture && oldTexture != null)
                {
                    m.SetTextureScale("_BaseMap", oldScale);
                    m.SetTextureOffset("_BaseMap", oldOffset);
                }
            }
            else
            {
                EnvBase(m);
                Color presetColor = SetEnvPreset(m, preset);
                if (BuildingOutlines && IsBuilding(preset))
                {
                    m.SetFloat("_OutlineWidth", 1.2f);   // silhouette
                    m.SetFloat("_CreaseLines", 0.7f);    // lines on corners and edges
                }
                m.SetColor("_BaseColor", keepColor && hasColor ? oldColor : presetColor);
                m.SetTexture("_BaseMap", keepTexture ? oldTexture : null);
                if (keepTexture && oldTexture != null)
                {
                    m.SetTextureScale("_BaseMap", oldScale);
                    m.SetTextureOffset("_BaseMap", oldOffset);
                }
            }

            EditorUtility.SetDirty(m);
            return true;
        }

        /// <summary>Creates one sample material per preset in Assets/AnimeToon/Materials/Presets.</summary>
        public static void CreateSamples()
        {
            Shader shader = Shader.Find("Anime/EnvironmentToon");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Anime Material Presets", "Shader 'Anime/EnvironmentToon' not found.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/AnimeToon")) AssetDatabase.CreateFolder("Assets", "AnimeToon");
            if (!AssetDatabase.IsValidFolder("Assets/AnimeToon/Materials")) AssetDatabase.CreateFolder("Assets/AnimeToon", "Materials");
            if (!AssetDatabase.IsValidFolder(SampleFolder)) AssetDatabase.CreateFolder("Assets/AnimeToon/Materials", "Presets");

            foreach (AnimePreset preset in System.Enum.GetValues(typeof(AnimePreset)))
            {
                string path = SampleFolder + "/Anime_" + preset + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader);
                    AssetDatabase.CreateAsset(m, path);
                }
                Apply(m, preset, false, false);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(SampleFolder));
        }

        // ------------------------------------------------------------------ environment values

        static void EnvBase(Material m)
        {
            m.SetFloat("_AlphaClip", 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Cull", 2f);

            m.SetColor("_LitTint", Color.white);
            m.SetColor("_ShadowTint", new Color(0.62f, 0.55f, 0.85f));
            m.SetFloat("_Threshold", 0.5f);
            m.SetFloat("_Softness", 0.05f);
            m.SetFloat("_ReceiveShadow", 1f);
            m.SetFloat("_LightIntensity", 0.9f);
            m.SetFloat("_AmbientInfluence", 0.35f);
            m.SetFloat("_AdditionalLightIntensity", 1f);
            m.SetFloat("_AdditionalLightSoftness", 0.15f);

            m.SetColor("_GradientBottomColor", new Color(0.75f, 0.65f, 0.9f));
            m.SetColor("_GradientTopColor", Color.white);
            m.SetFloat("_GradientMinY", 0f);
            m.SetFloat("_GradientMaxY", 20f);
            m.SetFloat("_GradientStrength", 0f);

            m.SetFloat("_NoiseScale", 0.4f);
            m.SetFloat("_NoiseStrength", 0.1f);
            m.SetFloat("_BrushScale", 0.3f);
            m.SetFloat("_BrushStrength", 0.1f);

            SetFacadeOff(m);

            m.SetColor("_AOColor", new Color(0.5f, 0.42f, 0.72f));
            m.SetFloat("_VertexAOStrength", 0f);

            m.SetColor("_RimColor", new Color(1f, 0.9f, 0.85f));
            m.SetFloat("_RimWidth", 0.75f);
            m.SetFloat("_RimSoftness", 0.12f);
            m.SetFloat("_RimIntensity", 0.2f);
            m.SetFloat("_RimGroundFade", 1f);

            m.SetColor("_EmissionColor", Color.black);

            // outlines are off unless a building preset is applied with "Building outlines" ticked
            m.SetColor("_OutlineColor", new Color(0.2f, 0.15f, 0.3f));
            m.SetFloat("_OutlineWidth", 0f);
            m.SetFloat("_CreaseLines", 0f);
            m.SetFloat("_CreaseThreshold", 0.3f);
            m.SetFloat("_OutlineBaseTint", 0.5f);
            m.SetFloat("_OutlineDistMin", 2f);
            m.SetFloat("_OutlineDistMax", 60f);
        }

        static void SetFacadeOff(Material m)
        {
            m.SetFloat("_FacadeOn", 0f);
            m.DisableKeyword("_FACADE_ON");
        }

        static void SetFacade(Material m, float floorHeight, float spacing, Vector2 windowSize,
                              Color windowBottom, Color windowTop, Color windowLit,
                              float litRatio, float emission, float ledgeThickness, float ledgeDarken)
        {
            m.SetFloat("_FacadeOn", 1f);
            m.EnableKeyword("_FACADE_ON");
            m.SetFloat("_FloorHeight", floorHeight);
            m.SetFloat("_WindowSpacing", spacing);
            m.SetVector("_WindowSize", new Vector4(windowSize.x, windowSize.y, 0f, 0f));
            m.SetVector("_FacadeOffset", Vector4.zero);
            m.SetVector("_WindowOffset", Vector4.zero);
            m.SetColor("_WindowBottomColor", windowBottom);
            m.SetColor("_WindowTopColor", windowTop);
            m.SetColor("_WindowLitColor", windowLit);
            m.SetFloat("_NightAmount", 0f);          // the style controller lights the windows at night
            m.SetFloat("_LitRatio", litRatio);
            m.SetFloat("_WindowEmission", emission);
            m.SetFloat("_LedgeThickness", ledgeThickness);
            m.SetFloat("_LedgeDarken", ledgeDarken);
        }

        static void SetGradient(Material m, Color bottom, Color top, float strength, float maxY)
        {
            m.SetColor("_GradientBottomColor", bottom);
            m.SetColor("_GradientTopColor", top);
            m.SetFloat("_GradientStrength", strength);
            m.SetFloat("_GradientMaxY", maxY);
        }

        /// <summary>Sets the values of one environment preset and returns the preset's own base color.</summary>
        static Color SetEnvPreset(Material m, AnimePreset p)
        {
            switch (p)
            {
                case AnimePreset.BuildingPastel:
                    SetFacade(m, 3f, 2.5f, new Vector2(0.55f, 0.6f),
                        new Color(0.25f, 0.35f, 0.6f), new Color(0.65f, 0.85f, 1f), new Color(1f, 0.85f, 0.5f),
                        0.4f, 2f, 0.08f, 0.25f);
                    SetGradient(m, new Color(0.75f, 0.65f, 0.9f), Color.white, 0.55f, 25f);
                    m.SetFloat("_NoiseStrength", 0.12f);
                    return new Color(1f, 0.86f, 0.9f);

                case AnimePreset.BuildingConcrete:
                    SetFacade(m, 3.2f, 2.2f, new Vector2(0.45f, 0.5f),
                        new Color(0.22f, 0.28f, 0.45f), new Color(0.5f, 0.65f, 0.85f), new Color(1f, 0.82f, 0.5f),
                        0.3f, 1.8f, 0.1f, 0.35f);
                    SetGradient(m, new Color(0.5f, 0.46f, 0.6f), Color.white, 0.65f, 25f);
                    m.SetColor("_ShadowTint", new Color(0.58f, 0.52f, 0.78f));
                    m.SetFloat("_NoiseScale", 0.5f);
                    m.SetFloat("_NoiseStrength", 0.22f);
                    m.SetFloat("_RimIntensity", 0.15f);
                    return new Color(0.8f, 0.79f, 0.86f);

                case AnimePreset.BuildingShop:
                    SetFacade(m, 2.8f, 3f, new Vector2(0.7f, 0.45f),
                        new Color(0.3f, 0.4f, 0.65f), new Color(0.8f, 0.92f, 1f), new Color(1f, 0.8f, 0.4f),
                        0.65f, 2.6f, 0.1f, 0.2f);
                    SetGradient(m, new Color(0.95f, 0.6f, 0.7f), Color.white, 0.35f, 15f);
                    m.SetColor("_ShadowTint", new Color(0.7f, 0.5f, 0.8f));
                    m.SetFloat("_NoiseStrength", 0.1f);
                    m.SetFloat("_RimIntensity", 0.25f);
                    return new Color(1f, 0.62f, 0.45f);

                case AnimePreset.WallPlain:
                    SetGradient(m, new Color(0.7f, 0.62f, 0.85f), Color.white, 0.5f, 10f);
                    m.SetFloat("_NoiseStrength", 0.18f);
                    return new Color(0.9f, 0.86f, 0.9f);

                case AnimePreset.RoadAsphalt:
                    m.SetColor("_ShadowTint", new Color(0.5f, 0.45f, 0.7f));
                    m.SetFloat("_AmbientInfluence", 0.4f);
                    m.SetFloat("_NoiseScale", 0.6f);
                    m.SetFloat("_NoiseStrength", 0.28f);
                    m.SetFloat("_RimIntensity", 0f);
                    return new Color(0.3f, 0.29f, 0.37f);

                case AnimePreset.RoadMarking:
                    m.SetColor("_ShadowTint", new Color(0.8f, 0.78f, 0.92f));
                    m.SetFloat("_LightIntensity", 1f);
                    m.SetFloat("_NoiseStrength", 0.12f);
                    m.SetFloat("_RimIntensity", 0f);
                    return new Color(1f, 0.97f, 0.88f);

                default: // Sidewalk
                    m.SetColor("_ShadowTint", new Color(0.6f, 0.52f, 0.78f));
                    m.SetFloat("_NoiseScale", 0.8f);
                    m.SetFloat("_NoiseStrength", 0.22f);
                    m.SetFloat("_RimIntensity", 0.08f);
                    return new Color(0.74f, 0.69f, 0.74f);
            }
        }

        // ------------------------------------------------------------------ glass and water values

        static void SetGlass(Material m)
        {
            m.SetColor("_GlassTint", new Color(0.35f, 0.6f, 0.85f));
            m.SetColor("_GlassTopTint", new Color(0.75f, 0.9f, 1f));
            m.SetFloat("_GradientMinY", 0f);
            m.SetFloat("_GradientMaxY", 30f);
            m.SetFloat("_BaseAlpha", 0.4f);
            m.SetFloat("_FresnelPower", 3f);
            m.SetFloat("_FresnelAlpha", 0.5f);
            m.SetFloat("_ReflectionStrength", 0.55f);
            m.SetFloat("_ProbeBlend", 0.8f);
            m.SetFloat("_ProbeRoughness", 0.15f);
            m.SetFloat("_ReflectionBoost", 1.2f);
            m.SetFloat("_StreakIntensity", 0.5f);
            m.SetFloat("_SunIntensity", 0.8f);

            // lit windows at night (driven by the style controller's night amount)
            m.SetFloat("_NightWindows", 1f);
            m.SetFloat("_NightTintBrightness", 0.35f);
            m.SetFloat("_NightReflection", 0.4f);
            m.SetFloat("_NightAlpha", 0.85f);
            m.SetFloat("_FloorHeight", 3f);
            m.SetFloat("_WindowSpacing", 2.5f);
            m.SetVector("_WindowSize", new Vector4(0.7f, 0.65f, 0f, 0f));
            m.SetVector("_FacadeOffset", Vector4.zero);
            m.SetColor("_WindowUnlitColor", new Color(0.05f, 0.08f, 0.18f));
            m.SetColor("_WindowLitColor", new Color(1f, 0.85f, 0.5f));
            m.SetFloat("_LitRatio", 0.45f);
            m.SetFloat("_WindowEmission", 2.2f);
        }

        /// <summary>Sets the foliage values for a tree, bush or grass and returns a leaf green for the base color.</summary>
        static Color SetFoliage(Material m, AnimePreset p)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Cull", 0f);               // two sided

            m.SetColor("_LitTint", Color.white);
            m.SetColor("_ShadowTint", new Color(0.42f, 0.48f, 0.72f));
            m.SetFloat("_Threshold", 0.5f);
            m.SetFloat("_Softness", 0.08f);
            m.SetFloat("_ReceiveShadow", 1f);
            m.SetFloat("_LightIntensity", 0.95f);
            m.SetFloat("_AmbientInfluence", 0.4f);
            m.SetFloat("_AdditionalLightIntensity", 1f);
            m.SetFloat("_AdditionalLightSoftness", 0.15f);

            m.SetColor("_TipColor", new Color(1f, 1f, 0.7f));
            m.SetColor("_RootColor", new Color(0.5f, 0.55f, 0.8f));
            m.SetColor("_TranslucencyColor", new Color(1f, 0.95f, 0.5f));
            m.SetFloat("_WindMask", 0f);
            m.SetFloat("_WindSpeed", 1.4f);
            m.SetFloat("_WindScale", 0.4f);
            m.SetVector("_WindDirection", new Vector4(1f, 0.4f, 0f, 0f));

            switch (p)
            {
                case AnimePreset.FoliageTree:
                    m.SetFloat("_SphericalAmount", 0.7f);
                    m.SetFloat("_CrownHeight", 3f);
                    m.SetFloat("_HeightRange", 6f);
                    m.SetFloat("_GradientStrength", 0.5f);
                    m.SetFloat("_Translucency", 0.6f);
                    m.SetFloat("_WindStrength", 0.08f);
                    m.SetFloat("_WindHeight", 6f);
                    return new Color(0.55f, 0.85f, 0.45f);

                case AnimePreset.FoliageBush:
                    m.SetFloat("_SphericalAmount", 0.6f);
                    m.SetFloat("_CrownHeight", 0.6f);
                    m.SetFloat("_HeightRange", 1.5f);
                    m.SetFloat("_GradientStrength", 0.5f);
                    m.SetFloat("_Translucency", 0.5f);
                    m.SetFloat("_WindStrength", 0.1f);
                    m.SetFloat("_WindHeight", 1.5f);
                    return new Color(0.6f, 0.9f, 0.5f);

                default: // grass
                    m.SetFloat("_SphericalAmount", 0f);
                    m.SetFloat("_CrownHeight", 0f);
                    m.SetFloat("_HeightRange", 0.6f);
                    m.SetFloat("_GradientStrength", 0.75f);
                    m.SetFloat("_Translucency", 0.3f);
                    m.SetFloat("_WindStrength", 0.2f);
                    m.SetFloat("_WindHeight", 0.6f);
                    return new Color(0.6f, 0.9f, 0.4f);
            }
        }

        static void SetRoadWet(Material m)
        {
            m.SetFloat("_Cull", 2f);
            m.SetColor("_LitTint", Color.white);
            m.SetColor("_ShadowTint", new Color(0.5f, 0.45f, 0.7f));
            m.SetFloat("_Threshold", 0.5f);
            m.SetFloat("_Softness", 0.05f);
            m.SetFloat("_ReceiveShadow", 1f);
            m.SetFloat("_LightIntensity", 0.9f);
            m.SetFloat("_AmbientInfluence", 0.4f);
            m.SetFloat("_AdditionalLightIntensity", 1f);
            m.SetFloat("_AdditionalLightSoftness", 0.15f);
            m.SetFloat("_NoiseScale", 0.6f);
            m.SetFloat("_NoiseStrength", 0.28f);
            m.SetFloat("_Wetness", 0f);              // the style controller drives the wetness
            m.SetFloat("_WetDarken", 0.4f);
            m.SetFloat("_WetReflection", 0.45f);
            m.SetFloat("_PuddleAmount", 0.5f);
            m.SetFloat("_PuddleScale", 0.12f);
            m.SetFloat("_PuddleReflection", 0.9f);
            m.SetFloat("_ProbeBlend", 0.8f);
            m.SetFloat("_StreakIntensity", 1.2f);
        }

        static void SetWater(Material m)
        {
            m.SetColor("_ShallowColor", new Color(0.45f, 0.88f, 0.9f));
            m.SetColor("_DeepColor", new Color(0.1f, 0.3f, 0.65f));
            m.SetFloat("_ShallowAlpha", 0.4f);
            m.SetFloat("_DeepAlpha", 0.9f);
            m.SetFloat("_DepthMax", 3f);
            m.SetFloat("_FoamDistance", 0.7f);
            m.SetFloat("_PatternIntensity", 0.5f);
            m.SetFloat("_WaveHeight", 0.05f);
            m.SetFloat("_ReflectionStrength", 0.25f);
            m.SetFloat("_SunIntensity", 0.9f);
        }
    }
}
#endif

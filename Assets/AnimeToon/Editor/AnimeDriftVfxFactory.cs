// Editor only. Place in an "Editor" folder, next to AnimeVfxFactory.cs.
// Tools > Anime Toon > Create Drift VFX adds to the VFX pack (it never touches the existing prefabs):
//   AnimeDriftSmoke   toon tire-smoke puffs, positioned and launched by code with Emit()
//   AnimeDriftSparks  short stretched sparks, Emit()
//   AnimeVfx_Skid     material for a skid-mark ribbon (the skid texture is generated on first use)
// Run Tools > Anime Toon > Create VFX Pack first (this needs its textures and the toon particle shader).
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimeToon.Editor
{
    public static class AnimeDriftVfxFactory
    {
        [MenuItem("Tools/Anime Toon/Create Drift VFX")]
        static void Menu() { CreateDriftPack(); }

        public static bool DriftPackExists()
        {
            return AnimeVfxFactory.FindAssetPath("AnimeDriftSmoke", "Prefab") != null
                && AnimeVfxFactory.FindAssetPath("AnimeVfx_Skid", "Material") != null;
        }

        public static Material LoadMaterial(string materialName)
        {
            string path = AnimeVfxFactory.FindAssetPath(materialName, "Material");
            return path != null ? AssetDatabase.LoadAssetAtPath<Material>(path) : null;
        }

        public static void CreateDriftPack()
        {
            string puffPath = AnimeVfxFactory.FindAssetPath("AnimeVfx_Puff", "Texture2D");
            if (puffPath == null)
            {
                EditorUtility.DisplayDialog("Anime Drift VFX", "Run Tools > Anime Toon > Create VFX Pack first (its textures are needed).", "OK");
                return;
            }

            Shader shader = Shader.Find("Anime/ParticleToon");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Anime Drift VFX", "Shader 'Anime/ParticleToon' not found.", "OK");
                return;
            }

            string texFolder = Path.GetDirectoryName(puffPath).Replace('\\', '/');
            string root = Path.GetDirectoryName(texFolder).Replace('\\', '/');
            string matFolder = EnsureSub(root, "Materials");
            string prefabFolder = EnsureSub(root, "Prefabs");

            string skidTexPath = texFolder + "/AnimeVfx_Skid.png";
            if (!File.Exists(skidTexPath)) MakeSkidTexture(skidTexPath);

            Texture2D puff = AssetDatabase.LoadAssetAtPath<Texture2D>(puffPath);
            Texture2D dot = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/AnimeVfx_Dot.png");
            Texture2D skid = AssetDatabase.LoadAssetAtPath<Texture2D>(skidTexPath);

            Material smokeMat = Mat(matFolder, shader, "AnimeVfx_DriftSmoke", puff,
                new Color(1.00f, 0.98f, 0.95f), new Color(0.78f, 0.74f, 0.92f), false, 0.10f, 0.07f);
            Material sparkMat = Mat(matFolder, shader, "AnimeVfx_DriftSpark", dot,
                new Color(3.0f, 1.9f, 0.8f), new Color(3.0f, 1.9f, 0.8f), true, 0.25f, 0.20f);
            Mat(matFolder, shader, "AnimeVfx_Skid", skid,
                new Color(0.12f, 0.10f, 0.16f), new Color(0.12f, 0.10f, 0.16f), false, 0.12f, 0.15f);

            // smoke: emitted by code with Emit(position, velocity, size), so no emission, shape or speed of its own
            SavePrefab(BuildSystem("AnimeDriftSmoke", smokeMat, new Vector2(0.6f, 1.0f), new Vector2(-20f, 20f),
                -0.02f, 60, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f), 0.25f, false), prefabFolder);

            // sparks: short stretched streaks
            SavePrefab(BuildSystem("AnimeDriftSparks", sparkMat, new Vector2(0.18f, 0.35f), new Vector2(0f, 0f),
                1.2f, 60, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f), 0.5f, true), prefabFolder);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Anime drift VFX created in " + root + " (AnimeDriftSmoke, AnimeDriftSparks, AnimeVfx_Skid).");
        }

        // ------------------------------------------------------------------ helpers

        static string EnsureSub(string parent, string folderName)
        {
            string path = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, folderName);
            return path;
        }

        /// <summary>
        /// Skid ribbon mask: R = shape field across the ribbon's width (1 in the middle, 0 at the edges, a little
        /// ragged), G = 1 (everything "lit"). Same channel meaning as the other VFX textures.
        /// </summary>
        static void MakeSkidTexture(string path)
        {
            const int w = 64, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float across = 1f - Mathf.Abs(v * 2f - 1f);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float noise = Mathf.PerlinNoise(u * 6f, v * 3f);
                    float r = Mathf.Clamp01(across * (0.82f + 0.18f * noise));
                    tex.SetPixel(x, y, new Color(r, 1f, 0f, 1f));
                }
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); // the AnimeVfx_ importer sets it up as data
        }

        static Material Mat(string folder, Shader shader, string materialName, Texture2D tex, Color lit, Color shadow,
                            bool additive, float edgeCut, float softness)
        {
            string path = folder + "/" + materialName + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_MainTex", tex);
            m.SetColor("_LitColor", lit);
            m.SetColor("_ShadowColor", shadow);
            m.SetFloat("_EdgeCut", edgeCut);
            m.SetFloat("_EdgeSoftness", softness);
            m.SetFloat("_ErodeAmount", 1f);
            m.SetFloat("_OutlineWidth", 0f);
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", additive ? 1f : 10f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static ParticleSystem BuildSystem(string systemName, Material material, Vector2 life, Vector2 rotation,
                                          float gravity, int max, AnimationCurve sizeCurve, float fadeStart, bool stretch)
        {
            var go = new GameObject(systemName, typeof(ParticleSystem));
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = 0f;
            main.startSize = stretch ? new ParticleSystem.MinMaxCurve(0.06f, 0.12f) : new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(rotation.x * Mathf.Deg2Rad, rotation.y * Mathf.Deg2Rad);
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = false;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, Mathf.Clamp01(fadeStart)), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var growth = ps.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = material;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            psr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (stretch)
            {
                psr.renderMode = ParticleSystemRenderMode.Stretch;
                psr.velocityScale = 0.05f;
                psr.lengthScale = 3f;
            }
            else
            {
                psr.renderMode = ParticleSystemRenderMode.Billboard;
            }

            return ps;
        }

        static void SavePrefab(ParticleSystem root, string folder)
        {
            string path = folder + "/" + root.gameObject.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
        }
    }
}
#endif

// Editor only. Place in an "Editor" folder.
// Tools > Anime Toon > Create VFX Pack builds the toon particle materials and ParticleSystem prefabs
// from the textures in VFX/Textures. Nothing here depends on any game code.
//
// Prefabs (all emit along +Z, simulate in world space unless noted):
//   AnimeTrail            dust puffs, rate driven by code
//   AnimeHitSparks        stretched spark streaks, use Emit()
//   AnimeHitDust          dust burst, use Emit()
//   AnimeLowHealthSmoke   dark smoke puffs, rate driven by code
//   AnimeExplosion        fire puffs + smoke + comic burst + ground ring + sparks + sparkles (children), use Play()
//   AnimeImpactBurst      one comic starburst per Emit(), set size with EmitParams
//   AnimeShockRing        one expanding ring per Emit()
//   AnimeSpeedStreaks     streaks flying past a camera (local space), rate driven by AnimeSpeedFx
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnimeToon.Editor
{
    /// <summary>Texture settings for the VFX masks: they hold data, not color.</summary>
    public class AnimeVfxTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!Path.GetFileName(assetPath).StartsWith("AnimeVfx_", StringComparison.Ordinal)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }

    public static class AnimeVfxFactory
    {
        [MenuItem("Tools/Anime Toon/Create VFX Pack")]
        static void Menu()
        {
            if (PackExists() && !EditorUtility.DisplayDialog("Anime VFX Pack",
                    "The VFX pack already exists. Rebuild it?\nChanges you made to the pack prefabs and materials will be lost.",
                    "Rebuild", "Cancel"))
                return;
            CreatePack();
        }

        // ------------------------------------------------------------------ lookup (works wherever the folder lives)

        public static string FindAssetPath(string assetName, string type)
        {
            foreach (string guid in AssetDatabase.FindAssets(assetName + " t:" + type))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == assetName) return path;
            }
            return null;
        }

        public static bool PackExists() { return FindAssetPath("AnimeTrail", "Prefab") != null; }

        public static ParticleSystem LoadPrefab(string prefabName)
        {
            string path = FindAssetPath(prefabName, "Prefab");
            if (path == null) return null;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return go != null ? go.GetComponent<ParticleSystem>() : null;
        }

        // ------------------------------------------------------------------ build

        public static void CreatePack()
        {
            string puffPath = FindAssetPath("AnimeVfx_Puff", "Texture2D");
            if (puffPath == null)
            {
                EditorUtility.DisplayDialog("Anime VFX Pack", "Could not find the textures (AnimeVfx_Puff.png and the others). Keep the VFX/Textures folder in the project.", "OK");
                return;
            }

            Shader shader = Shader.Find("Anime/ParticleToon");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Anime VFX Pack", "Shader 'Anime/ParticleToon' not found. Make sure AnimeParticleToon.shader is in the project and compiled.", "OK");
                return;
            }

            string texFolder = Path.GetDirectoryName(puffPath).Replace('\\', '/');
            string root = Path.GetDirectoryName(texFolder).Replace('\\', '/');
            string matFolder = EnsureSub(root, "Materials");
            string prefabFolder = EnsureSub(root, "Prefabs");

            Texture2D Tex(string n) { return AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/AnimeVfx_" + n + ".png"); }

            // ---- materials (HDR colors above 1 feed bloom)
            Material dust = Mat(matFolder, shader, "AnimeVfx_Dust", Tex("Puff"),
                new Color(1.00f, 0.95f, 0.86f), new Color(0.70f, 0.63f, 0.80f), false, 0.10f, 0.07f);
            Material smoke = Mat(matFolder, shader, "AnimeVfx_Smoke", Tex("Puff"),
                new Color(0.45f, 0.43f, 0.52f), new Color(0.17f, 0.16f, 0.26f), false, 0.10f, 0.07f);
            Material fire = Mat(matFolder, shader, "AnimeVfx_Fire", Tex("Puff"),
                new Color(1.80f, 1.35f, 0.55f), new Color(1.20f, 0.38f, 0.14f), false, 0.10f, 0.07f);
            Material spark = Mat(matFolder, shader, "AnimeVfx_Spark", Tex("Dot"),
                new Color(3.0f, 2.6f, 1.4f), new Color(3.0f, 2.6f, 1.4f), true, 0.25f, 0.20f);
            Material sparkle = Mat(matFolder, shader, "AnimeVfx_Sparkle", Tex("Sparkle"),
                new Color(3.0f, 2.7f, 1.8f), new Color(2.0f, 1.6f, 0.8f), true, 0.08f, 0.08f);
            Material burst = Mat(matFolder, shader, "AnimeVfx_Burst", Tex("Burst"),
                new Color(2.2f, 2.2f, 2.0f), new Color(2.0f, 1.1f, 0.25f), false, 0.05f, 0.04f);
            Material ring = Mat(matFolder, shader, "AnimeVfx_Ring", Tex("Ring"),
                new Color(2.5f, 2.4f, 2.0f), new Color(2.5f, 2.4f, 2.0f), true, 0.15f, 0.20f);
            Material streak = Mat(matFolder, shader, "AnimeVfx_Streak", Tex("Dot"),
                new Color(1.6f, 1.7f, 2.0f), new Color(1.6f, 1.7f, 2.0f), true, 0.30f, 0.30f);

            AnimationCurve Grow(float a, float b) { return AnimationCurve.EaseInOut(0f, a, 1f, b); }
            var pop = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f));
            var shrink = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f);

            // ---- the five slots used by VehicleEffects
            Save(Build(new Fx
            {
                name = "AnimeTrail", mat = dust, life = V(0.5f, 0.85f), speed = V(0.4f, 1.1f), size = V(0.45f, 0.8f),
                gravity = -0.03f, max = 70, shape = Shape.Cone, coneAngle = 14f, shapeRadius = 0.15f, sizeCurve = Grow(0.5f, 1.35f), fadeStart = 0.2f
            }), prefabFolder);

            Save(Build(new Fx
            {
                name = "AnimeHitSparks", mat = spark, life = V(0.22f, 0.42f), speed = V(5f, 12f), size = V(0.08f, 0.16f),
                rotation = V(0f, 0f), gravity = 1.0f, max = 90, shape = Shape.Sphere, shapeRadius = 0.1f,
                stretch = true, velocityScale = 0.05f, lengthScale = 3f, sizeCurve = shrink, fadeStart = 0.5f
            }), prefabFolder);

            Save(Build(new Fx
            {
                name = "AnimeHitDust", mat = dust, life = V(0.4f, 0.75f), speed = V(1.5f, 4.5f), size = V(0.6f, 1.1f),
                gravity = -0.02f, max = 70, shape = Shape.Sphere, shapeRadius = 0.25f, sizeCurve = Grow(0.6f, 1.4f), fadeStart = 0.15f
            }), prefabFolder);

            Save(Build(new Fx
            {
                name = "AnimeLowHealthSmoke", mat = smoke, life = V(1.0f, 1.7f), speed = V(0.7f, 1.3f), size = V(0.45f, 0.85f),
                gravity = -0.06f, max = 60, shape = Shape.Cone, coneAngle = 10f, shapeRadius = 0.2f, sizeCurve = Grow(0.5f, 1.6f), fadeStart = 0.25f
            }), prefabFolder);

            // explosion: root + children, all play together with Play()
            ParticleSystem explosion = Build(new Fx
            {
                name = "AnimeExplosion", mat = fire, loop = false, life = V(0.6f, 1.0f), speed = V(3f, 8f), size = V(0.9f, 1.6f),
                gravity = -0.05f, max = 40, shape = Shape.Sphere, shapeRadius = 0.35f, burst = 14, sizeCurve = Grow(0.4f, 1.5f), fadeStart = 0.35f
            });
            Transform er = explosion.transform;
            Build(new Fx
            {
                name = "ExplosionSmoke", mat = smoke, loop = false, startDelay = 0.08f, life = V(1.1f, 1.8f), speed = V(1f, 3f), size = V(1.1f, 1.9f),
                gravity = -0.08f, max = 30, shape = Shape.Sphere, shapeRadius = 0.4f, burst = 9, sizeCurve = Grow(0.5f, 1.4f), fadeStart = 0.3f
            }, er);
            Build(new Fx
            {
                name = "ExplosionBurst", mat = burst, loop = false, life = V(0.22f, 0.26f), speed = V(0f, 0f), size = V(3.5f, 3.8f),
                max = 2, shape = Shape.None, burst = 1, sizeCurve = Grow(0.55f, 1.1f), fadeStart = 0.3f
            }, er);
            Build(new Fx
            {
                name = "ExplosionRing", mat = ring, loop = false, life = V(0.35f, 0.35f), speed = V(0f, 0f), size = V(4f, 4f), rotation = V(0f, 0f),
                max = 2, shape = Shape.None, burst = 1, sizeCurve = Grow(0.1f, 1f), fadeStart = 0.25f, horizontal = true
            }, er);
            Build(new Fx
            {
                name = "ExplosionSparks", mat = spark, loop = false, life = V(0.3f, 0.6f), speed = V(6f, 14f), size = V(0.1f, 0.2f), rotation = V(0f, 0f),
                gravity = 1.2f, max = 40, shape = Shape.Sphere, shapeRadius = 0.2f, burst = 22, stretch = true, velocityScale = 0.05f, lengthScale = 3f,
                sizeCurve = shrink, fadeStart = 0.5f
            }, er);
            Build(new Fx
            {
                name = "ExplosionSparkles", mat = sparkle, loop = false, life = V(0.5f, 0.9f), speed = V(2f, 5f), size = V(0.3f, 0.6f), rotation = V(-20f, 20f),
                gravity = -0.1f, max = 20, shape = Shape.Sphere, shapeRadius = 0.4f, burst = 8, sizeCurve = pop, fadeStart = 0.5f
            }, er);
            Save(explosion, prefabFolder);

            // ---- extras used by the vehicle bridge and the camera effects
            Save(Build(new Fx
            {
                name = "AnimeImpactBurst", mat = burst, life = V(0.16f, 0.2f), speed = V(0f, 0f), size = V(2.5f, 2.5f), rotation = V(-18f, 18f),
                max = 6, shape = Shape.None, sizeCurve = Grow(0.6f, 1.15f), fadeStart = 0.25f
            }), prefabFolder);

            Save(Build(new Fx
            {
                name = "AnimeShockRing", mat = ring, life = V(0.3f, 0.3f), speed = V(0f, 0f), size = V(3f, 3f), rotation = V(0f, 0f),
                max = 6, shape = Shape.None, sizeCurve = Grow(0.15f, 1f), fadeStart = 0.3f
            }), prefabFolder);

            ParticleSystem streaks = Build(new Fx
            {
                name = "AnimeSpeedStreaks", mat = streak, life = V(0.45f, 0.6f), speed = V(0f, 0f), size = V(0.04f, 0.08f), rotation = V(0f, 0f),
                max = 120, space = ParticleSystemSimulationSpace.Local, shape = Shape.None,
                stretch = true, velocityScale = 0.05f, lengthScale = 8f, fadeStart = 0.6f
            });
            var sShape = streaks.shape;
            sShape.enabled = true;
            sShape.shapeType = ParticleSystemShapeType.Circle;
            sShape.radius = 7f;
            sShape.radiusThickness = 0.55f;
            sShape.arc = 360f;
            sShape.position = new Vector3(0f, 0f, 28f);
            var sVel = streaks.velocityOverLifetime;
            sVel.enabled = true;
            sVel.space = ParticleSystemSimulationSpace.Local;
            sVel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            sVel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            sVel.z = new ParticleSystem.MinMaxCurve(-75f, -55f);
            Save(streaks, prefabFolder);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Anime VFX pack created in " + root + " (Materials, Prefabs).");
        }

        // ------------------------------------------------------------------ helpers

        enum Shape { None, Cone, Sphere }

        class Fx
        {
            public string name;
            public Material mat;
            public bool loop = true;
            public float startDelay;
            public Vector2 life = new Vector2(0.5f, 0.8f);
            public Vector2 speed = new Vector2(1f, 2f);
            public Vector2 size = new Vector2(0.5f, 0.8f);
            public Vector2 rotation = new Vector2(-15f, 15f);
            public float gravity;
            public int max = 80;
            public ParticleSystemSimulationSpace space = ParticleSystemSimulationSpace.World;
            public Shape shape = Shape.None;
            public float coneAngle = 12f;
            public float shapeRadius = 0.2f;
            public int burst;
            public AnimationCurve sizeCurve;
            public float fadeStart = 0.3f;
            public bool stretch;
            public float velocityScale = 0.05f;
            public float lengthScale = 2f;
            public bool horizontal;
        }

        static Vector2 V(float a, float b) { return new Vector2(a, b); }

        static string EnsureSub(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
            return path;
        }

        static Material Mat(string folder, Shader shader, string name, Texture2D tex, Color lit, Color shadow,
                            bool additive, float edgeCut, float softness)
        {
            string path = folder + "/" + name + ".mat";
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
            m.SetFloat("_SrcBlend", 5f);                    // SrcAlpha
            m.SetFloat("_DstBlend", additive ? 1f : 10f);   // One (additive) or OneMinusSrcAlpha
            EditorUtility.SetDirty(m);
            return m;
        }

        static ParticleSystem Build(Fx f, Transform parent = null)
        {
            var go = new GameObject(f.name, typeof(ParticleSystem));
            if (parent != null) go.transform.SetParent(parent, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = f.loop;
            main.playOnAwake = f.loop;
            main.duration = 1f;
            main.startDelay = f.startDelay;
            main.startLifetime = new ParticleSystem.MinMaxCurve(f.life.x, f.life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(f.speed.x, f.speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(f.size.x, f.size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(f.rotation.x * Mathf.Deg2Rad, f.rotation.y * Mathf.Deg2Rad);
            main.startColor = Color.white;
            main.gravityModifier = f.gravity;
            main.maxParticles = f.max;
            main.simulationSpace = f.space;
            main.scalingMode = ParticleSystemScalingMode.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // driven by code (rate) or Emit() / bursts
            if (f.burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)f.burst) });

            var shape = ps.shape;
            switch (f.shape)
            {
                case Shape.Cone:
                    shape.enabled = true;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = f.coneAngle;
                    shape.radius = f.shapeRadius;
                    break;
                case Shape.Sphere:
                    shape.enabled = true;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = f.shapeRadius;
                    break;
                default:
                    shape.enabled = false;
                    break;
            }

            // alpha 1 -> 0 over the second part of the life: the toon shader turns this into erosion
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, Mathf.Clamp01(f.fadeStart)), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            if (f.sizeCurve != null)
            {
                var growth = ps.sizeOverLifetime;
                growth.enabled = true;
                growth.size = new ParticleSystem.MinMaxCurve(1f, f.sizeCurve);
            }

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = f.mat;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            psr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (f.stretch)
            {
                psr.renderMode = ParticleSystemRenderMode.Stretch;
                psr.velocityScale = f.velocityScale;
                psr.lengthScale = f.lengthScale;
            }
            else
            {
                psr.renderMode = f.horizontal ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            }

            return ps;
        }

        static void Save(ParticleSystem root, string folder)
        {
            string path = folder + "/" + root.gameObject.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
        }
    }
}
#endif

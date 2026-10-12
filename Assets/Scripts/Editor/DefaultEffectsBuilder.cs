#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only. Lives in an "Editor" folder, so it is never included in a build.
namespace VehicleSystem.EditorTools
{
    /// <summary>
    /// Creates the DEFAULT particle effects every vehicle falls back on: a soft round particle texture, a material that works in
    /// any render pipeline, five ParticleSystem prefabs and the VehicleEffectsDefaults asset that points to them.
    ///
    /// It runs once by itself when the project loads and the defaults are missing. Everything lands in
    /// Assets/VehicleSystem/Effects and Assets/Resources/VehicleSystem, where you can edit the prefabs freely
    /// (all of them emit along +Z; the vehicle turns them backward / upward when it places them).
    /// Tools > Vehicle > Create Default Effects recreates them.
    /// </summary>
    [InitializeOnLoad]
    public static class DefaultEffectsBuilder
    {
        const string EffectsFolder = "Assets/VehicleSystem/Effects";
        const string ResourcesFolder = "Assets/Resources/VehicleSystem";
        public const string AssetPath = ResourcesFolder + "/VehicleEffectsDefaults.asset";

        static DefaultEffectsBuilder()
        {
            EditorApplication.delayCall += EnsureDefaults;
        }

        public static void EnsureDefaults()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (AssetDatabase.LoadAssetAtPath<VehicleEffectsDefaults>(AssetPath) != null) return;
            Create();
        }

        [MenuItem("Tools/Vehicle/Create Default Effects")]
        static void CreateFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<VehicleEffectsDefaults>(AssetPath) != null &&
                !EditorUtility.DisplayDialog("Default Effects",
                    "The default effects already exist. Recreate them?\nChanges you made to the default prefabs will be lost.",
                    "Recreate", "Cancel"))
                return;

            Create();
        }

        static void Create()
        {
            EnsureFolder("Assets", "VehicleSystem");
            EnsureFolder("Assets/VehicleSystem", "Effects");
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "VehicleSystem");

            Texture2D texture = MakeTexture(EffectsFolder + "/SoftParticle.png");
            Material material = MakeMaterial(EffectsFolder + "/DefaultParticle.mat", texture);

            var trail = MakeEffect("Default Trail", material, loop: true, life: (0.9f, 1.5f), speed: (0.3f, 0.9f), size: (0.5f, 0.9f),
                                   color: new Color(0.85f, 0.8f, 0.7f, 0.5f), gravity: -0.02f, max: 150, grow: true, cone: (14f, 0.15f));
            var sparks = MakeEffect("Default Hit Sparks", material, loop: true, life: (0.3f, 0.55f), speed: (4f, 10f), size: (0.12f, 0.22f),
                                    color: new Color(1f, 0.75f, 0.25f, 1f), gravity: 1.2f, max: 120, grow: false, sphere: 0.1f);
            var dust = MakeEffect("Default Hit Dust", material, loop: true, life: (0.7f, 1.2f), speed: (1f, 3f), size: (0.5f, 1f),
                                  color: new Color(0.72f, 0.67f, 0.6f, 0.7f), gravity: -0.03f, max: 120, grow: true, sphere: 0.25f);
            var smoke = MakeEffect("Default Low Health Smoke", material, loop: true, life: (1.3f, 2.2f), speed: (0.6f, 1.2f), size: (0.6f, 1.2f),
                                   color: new Color(0.2f, 0.2f, 0.2f, 0.7f), gravity: -0.05f, max: 100, grow: true, cone: (10f, 0.2f));
            var explosion = MakeEffect("Default Explosion", material, loop: false, life: (0.7f, 1.3f), speed: (3f, 9f), size: (0.6f, 1.5f),
                                       color: new Color(1f, 0.55f, 0.15f, 1f), gravity: 0.1f, max: 120, grow: true, sphere: 0.3f, burst: 45);

            var defaults = AssetDatabase.LoadAssetAtPath<VehicleEffectsDefaults>(AssetPath);
            bool isNew = defaults == null;
            if (isNew) defaults = ScriptableObject.CreateInstance<VehicleEffectsDefaults>();

            defaults.trail = trail;
            defaults.hitSparks = sparks;
            defaults.hitDust = dust;
            defaults.lowHealthSmoke = smoke;
            defaults.explosion = explosion;

            if (isNew) AssetDatabase.CreateAsset(defaults, AssetPath);
            else EditorUtility.SetDirty(defaults);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Vehicle effects: default particle effects created in " + EffectsFolder + " (they are used by every vehicle that has no custom effect).");
        }

        // ------------------------------------------------------------------ assets

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static Texture2D MakeTexture(string path)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a); // soft edge
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material MakeMaterial(string path, Texture2D texture)
        {
            // "Sprites/Default" exists in every render pipeline and supports particle colours and transparency
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        static ParticleSystem MakeEffect(string name, Material material, bool loop, (float, float) life, (float, float) speed,
                                         (float, float) size, Color color, float gravity, int max, bool grow,
                                         (float, float)? cone = null, float sphere = 0f, int burst = 0)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = loop;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.Item1, life.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // driven by VehicleEffects (trail, smoke) or Emit() (hits)
            if (burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });

            var shape = ps.shape;
            shape.enabled = true;
            if (cone.HasValue)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = cone.Value.Item1;
                shape.radius = cone.Value.Item2;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = sphere;
            }

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var growth = ps.sizeOverLifetime;
            growth.enabled = grow;
            if (grow) growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = material;

            string path = EffectsFolder + "/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ParticleSystem>();
        }
    }
}
#endif

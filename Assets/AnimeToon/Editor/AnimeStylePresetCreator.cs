// Place in an "Editor" folder.
// Tools > Anime Toon > Create Default Style Presets            (never overwrites)
// Tools > Anime Toon > Reset Default Style Presets (Overwrite)  (restores Day/Dusk/Night values)
// Tools > Anime Toon > Create Sky Material                      (Anime/Sky material, set as skybox)
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimeToon.Editor
{
    public static class AnimeStylePresetCreator
    {
        const string Root = "Assets/AnimeToon";
        const string PresetFolder = "Assets/AnimeToon/Presets";
        const string MaterialFolder = "Assets/AnimeToon/Materials";

        [MenuItem("Tools/Anime Toon/Create Default Style Presets")]
        static void CreateMenu() { Run(false); }

        [MenuItem("Tools/Anime Toon/Reset Default Style Presets (Overwrite)")]
        static void ResetMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset Day / Dusk / Night presets",
                "This overwrites any changes you made to the Day, Dusk and Night presets in " + PresetFolder + ".",
                "Overwrite", "Cancel")) return;
            Run(true);
        }

        static void Run(bool overwrite)
        {
            EnsureFolder(Root, "Assets", "AnimeToon");
            EnsureFolder(PresetFolder, Root, "Presets");

            var day = Make("Day", p =>
            {
                p.sunColor = new Color(1f, 0.96f, 0.88f);
                p.sunIntensity = 1.1f;
                p.sunEuler = new Vector3(50f, -35f, 0f);
                p.ambientSky = new Color(0.62f, 0.78f, 1f);
                p.ambientEquator = new Color(0.8f, 0.82f, 0.9f);
                p.ambientGround = new Color(0.55f, 0.5f, 0.6f);
                p.fogColor = new Color(0.78f, 0.88f, 1f);
                p.fogDensity = 0.006f;
                p.shadowTint = new Color(0.6f, 0.58f, 0.88f);
                p.shadowTintAmount = 0.5f;
                p.nightAmount = 0f;
                p.lightIntensityOffset = 0f;
                p.rimBoost = 0f;
                p.skyZenith = new Color(0.25f, 0.5f, 0.95f);
                p.skyHorizon = new Color(0.75f, 0.9f, 1f);
                p.skyGround = new Color(0.55f, 0.6f, 0.7f);
                p.horizonGlowColor = new Color(1f, 0.85f, 0.7f);
                p.horizonGlowAmount = 0.15f;
                p.cloudLit = new Color(1f, 1f, 1f);
                p.cloudShadow = new Color(0.7f, 0.78f, 0.95f);
                p.cloudCoverage = 0.5f;
                p.starAmount = 0f;
            }, overwrite);

            var dusk = Make("Dusk", p =>
            {
                p.sunColor = new Color(1f, 0.6f, 0.38f);
                p.sunIntensity = 1.0f;
                p.sunEuler = new Vector3(12f, -60f, 0f);
                p.ambientSky = new Color(0.55f, 0.45f, 0.75f);
                p.ambientEquator = new Color(0.95f, 0.62f, 0.6f);
                p.ambientGround = new Color(0.4f, 0.3f, 0.5f);
                p.fogColor = new Color(0.98f, 0.68f, 0.65f);
                p.fogDensity = 0.012f;
                p.shadowTint = new Color(0.5f, 0.38f, 0.75f);
                p.shadowTintAmount = 0.8f;
                p.nightAmount = 0.35f;
                p.lightIntensityOffset = 0f;
                p.rimBoost = 0.6f;
                p.skyZenith = new Color(0.28f, 0.25f, 0.6f);
                p.skyHorizon = new Color(1f, 0.6f, 0.5f);
                p.skyGround = new Color(0.4f, 0.3f, 0.5f);
                p.horizonGlowColor = new Color(1f, 0.55f, 0.35f);
                p.horizonGlowAmount = 0.9f;
                p.cloudLit = new Color(1f, 0.75f, 0.7f);
                p.cloudShadow = new Color(0.5f, 0.38f, 0.7f);
                p.cloudCoverage = 0.55f;
                p.starAmount = 0.2f;
            }, overwrite);

            var night = Make("Night", p =>
            {
                p.sunColor = new Color(0.45f, 0.55f, 1f);
                p.sunIntensity = 0.35f;
                p.sunEuler = new Vector3(35f, -120f, 0f);
                p.ambientSky = new Color(0.12f, 0.15f, 0.35f);
                p.ambientEquator = new Color(0.18f, 0.16f, 0.35f);
                p.ambientGround = new Color(0.08f, 0.08f, 0.15f);
                p.fogColor = new Color(0.1f, 0.12f, 0.3f);
                p.fogDensity = 0.014f;
                p.shadowTint = new Color(0.25f, 0.25f, 0.55f);
                p.shadowTintAmount = 1f;
                p.nightAmount = 1f;
                p.lightIntensityOffset = 0f;
                p.rimBoost = 1.2f;
                p.skyZenith = new Color(0.04f, 0.05f, 0.18f);
                p.skyHorizon = new Color(0.18f, 0.2f, 0.45f);
                p.skyGround = new Color(0.06f, 0.06f, 0.15f);
                p.horizonGlowColor = new Color(0.4f, 0.45f, 1f);
                p.horizonGlowAmount = 0.1f;
                p.cloudLit = new Color(0.5f, 0.55f, 0.85f);
                p.cloudShadow = new Color(0.18f, 0.2f, 0.4f);
                p.cloudCoverage = 0.4f;
                p.starAmount = 1f;
            }, overwrite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (Selection.activeGameObject != null)
            {
                var c = Selection.activeGameObject.GetComponent<AnimeStyleController>();
                if (c != null)
                {
                    Undo.RecordObject(c, "Assign Anime Style Presets");
                    c.presets = new[] { day, dusk, night };
                    EditorUtility.SetDirty(c);
                }
            }

            Selection.activeObject = day;
            EditorGUIUtility.PingObject(day);
        }

        [MenuItem("Tools/Anime Toon/Create Sky Material")]
        static void CreateSky()
        {
            Shader shader = Shader.Find("Anime/Sky");
            if (shader == null)
            {
                Debug.LogError("Shader 'Anime/Sky' not found. Make sure AnimeSky.shader is in the project and compiled.");
                return;
            }

            EnsureFolder(Root, "Assets", "AnimeToon");
            EnsureFolder(MaterialFolder, Root, "Materials");

            string path = MaterialFolder + "/AnimeSky.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "AnimeSky" };
                AssetDatabase.CreateAsset(mat, path);
            }

            RenderSettings.skybox = mat;

            var controller = Object.FindFirstObjectByType<AnimeStyleController>();
            if (controller != null)
            {
                Undo.RecordObject(controller, "Assign Sky Material");
                controller.skyMaterial = mat;
                EditorUtility.SetDirty(controller);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeObject = mat;
            EditorGUIUtility.PingObject(mat);
        }

        static void EnsureFolder(string path, string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        static AnimeStylePreset Make(string name, System.Action<AnimeStylePreset> setup, bool overwrite)
        {
            string path = $"{PresetFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AnimeStylePreset>(path);

            var temp = ScriptableObject.CreateInstance<AnimeStylePreset>();
            setup(temp);

            if (existing != null)
            {
                if (overwrite)
                {
                    EditorUtility.CopySerialized(temp, existing);
                    EditorUtility.SetDirty(existing);
                }
                Object.DestroyImmediate(temp);
                return existing;
            }

            AssetDatabase.CreateAsset(temp, path);
            return temp;
        }
    }
}
#endif

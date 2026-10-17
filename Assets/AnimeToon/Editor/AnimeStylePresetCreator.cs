// Place in an "Editor" folder.
// Menu: Tools > Anime Toon > Create Default Style Presets
// Creates Day, Dusk and Night presets in Assets/AnimeToon/Presets.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AnimeToon.Editor
{
    public static class AnimeStylePresetCreator
    {
        const string Root = "Assets/AnimeToon";
        const string Folder = "Assets/AnimeToon/Presets";

        [MenuItem("Tools/Anime Toon/Create Default Style Presets")]
        static void Create()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "AnimeToon");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Root, "Presets");

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
            });

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
            });

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
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // if a controller is selected, fill its preset list
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

        static AnimeStylePreset Make(string name, System.Action<AnimeStylePreset> setup)
        {
            string path = $"{Folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AnimeStylePreset>(path);
            if (existing != null) return existing; // never overwrite your tweaked presets

            var p = ScriptableObject.CreateInstance<AnimeStylePreset>();
            setup(p);
            AssetDatabase.CreateAsset(p, path);
            return p;
        }
    }
}
#endif

// Editor only. Place in an "Editor" folder.
//   Tools > Anime Toon > Create Quality Settings Asset   makes the editable tier asset (Resources/AnimeToon)
//   Tools > Anime Toon > Quality > Auto / Low / Medium / High   preview a tier while the game is running
// A previewed choice is saved like the player's own choice, so put it back on Auto when you are done.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AnimeToon.Editor
{
    public static class AnimeQualityMenu
    {
        const string Folder = "Assets/Resources/AnimeToon";
        const string AssetPath = Folder + "/AnimeQualitySettings.asset";

        [MenuItem("Tools/Anime Toon/Create Quality Settings Asset")]
        static void CreateAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimeQualitySettings>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "AnimeToon");

            var asset = ScriptableObject.CreateInstance<AnimeQualitySettings>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        [MenuItem("Tools/Anime Toon/Quality/Auto (default)")]
        static void PreviewAuto() { Preview(AnimeQualityMode.Auto); }

        [MenuItem("Tools/Anime Toon/Quality/Low")]
        static void PreviewLow() { Preview(AnimeQualityMode.Low); }

        [MenuItem("Tools/Anime Toon/Quality/Medium")]
        static void PreviewMedium() { Preview(AnimeQualityMode.Medium); }

        [MenuItem("Tools/Anime Toon/Quality/High")]
        static void PreviewHigh() { Preview(AnimeQualityMode.High); }

        static void Preview(AnimeQualityMode mode)
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Anime Quality", "Press Play first. The tiers change how the running game is drawn.", "OK");
                return;
            }

            AnimeQuality.SetMode(mode);
            Debug.Log("Anime quality: " + AnimeQuality.Label);
        }
    }
}
#endif

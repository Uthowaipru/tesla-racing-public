// Editor only. Place in an "Editor" folder.
// Tools > Anime Toon > Generate Graphics Menu UI
//
// Builds the graphics settings screen as ordinary UI objects and saves it as a prefab:
//     Assets/Resources/AnimeToon/AnimeGraphicsMenu.prefab
// Open the prefab (double-click) to restyle it: colors, fonts, sizes, positions, texts, sprites, remove or add rows.
// The game loads it by itself at startup. Generating again replaces the prefab, so a warning is shown first.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AnimeToon.Editor
{
    public static class AnimeGraphicsMenuBuilder
    {
        const string Folder = "Assets/Resources/AnimeToon";
        const string PrefabPath = Folder + "/AnimeGraphicsMenu.prefab";

        [MenuItem("Tools/Anime Toon/Generate Graphics Menu UI")]
        static void Generate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                bool replace = EditorUtility.DisplayDialog("Graphics Menu UI",
                    "The prefab already exists:\n" + PrefabPath + "\n\nGenerate it again? Your changes to the prefab will be lost.",
                    "Replace", "Keep and select it");
                if (!replace)
                {
                    Selection.activeObject = existing;
                    EditorGUIUtility.PingObject(existing);
                    return;
                }
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "AnimeToon");

            var go = new GameObject("Anime Graphics Menu");
            var menu = go.AddComponent<AnimeGraphicsMenu>();
            menu.BuildDefaultUI();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Graphics menu UI saved to " + PrefabPath + ". Double-click it to edit; it is loaded automatically when the game starts.");
        }
    }
}
#endif

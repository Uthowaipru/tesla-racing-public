// Editor only. Place in an "Editor" folder.
//
// Tools > Anime Toon > Generate Graphics Menu UI
//     Saves the graphics settings screen as a prefab made of ordinary UI objects:
//     Assets/AnimeToon/UI/AnimeGraphicsPanel.prefab. Open it (double-click) to restyle it.
//     Nothing is loaded automatically: the menu only exists where you place it.
//
// Tools > Anime Toon > Add Graphics Menu To Scene
//     Puts an instance of the prefab into the open scene. If a UI object is selected, it is added under it.
//
// Link it to your own button: Button > OnClick > + > drag the menu object > AnimeGraphicsMenu > Show().
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AnimeToon.Editor
{
    public static class AnimeGraphicsMenuBuilder
    {
        const string Folder = "Assets/AnimeToon/UI";
        const string PrefabPath = Folder + "/AnimeGraphicsPanel.prefab";

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

            if (!AssetDatabase.IsValidFolder("Assets/AnimeToon")) AssetDatabase.CreateFolder("Assets", "AnimeToon");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/AnimeToon", "UI");

            var go = new GameObject("Anime Graphics Menu");
            var menu = go.AddComponent<AnimeGraphicsMenu>();
            menu.BuildDefaultUI();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Graphics menu UI saved to " + PrefabPath + ". Double-click it to edit. Use Tools > Anime Toon > Add Graphics Menu To Scene, " +
                      "then link your button: OnClick > drag the menu object > AnimeGraphicsMenu > Show().");
        }

        [MenuItem("Tools/Anime Toon/Add Graphics Menu To Scene")]
        static void AddToScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Graphics Menu UI", "Run Tools > Anime Toon > Generate Graphics Menu UI first.", "OK");
                return;
            }

            Transform parent = Selection.activeTransform;
            var instance = (GameObject)(parent != null
                ? PrefabUtility.InstantiatePrefab(prefab, parent)
                : PrefabUtility.InstantiatePrefab(prefab));

            Undo.RegisterCreatedObjectUndo(instance, "Add Graphics Menu");
            Selection.activeGameObject = instance;
        }
    }
}
#endif

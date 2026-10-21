// Editor only. Place in an "Editor" folder.
// Tools > Anime Toon > Add Light Shafts To Scene
//   Creates the "Light Shafts" object (with the material) in the open scene. Set Anchor Height to your ground level,
//   and try the style controller's Dusk for the strongest rays.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AnimeToon.Editor
{
    public static class AnimeLightShaftsMenu
    {
        [MenuItem("Tools/Anime Toon/Add Light Shafts To Scene")]
        static void Add()
        {
            Shader shader = Shader.Find("Anime/LightShaft");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Light Shafts", "Shader 'Anime/LightShaft' not found. Make sure AnimeLightShaft.shader is in the project.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/AnimeToon")) AssetDatabase.CreateFolder("Assets", "AnimeToon");
            if (!AssetDatabase.IsValidFolder("Assets/AnimeToon/Materials")) AssetDatabase.CreateFolder("Assets/AnimeToon", "Materials");

            const string path = "Assets/AnimeToon/Materials/AnimeLightShafts.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "AnimeLightShafts" };
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssets();
            }

            var go = new GameObject("Light Shafts", typeof(MeshFilter), typeof(MeshRenderer), typeof(AnimeLightShafts));
            go.GetComponent<AnimeLightShafts>().material = material;

            Undo.RegisterCreatedObjectUndo(go, "Add Light Shafts");
            Selection.activeGameObject = go;
        }
    }
}
#endif

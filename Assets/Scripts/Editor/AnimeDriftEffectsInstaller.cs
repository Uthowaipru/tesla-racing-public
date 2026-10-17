// GAME SPECIFIC (Rickshaw Chaos / VehicleSystem). Editor only, place in an "Editor" folder.
//
// Tools > Vehicle > Install Anime Drift Effects
//     Builds the drift VFX if needed (Tools > Anime Toon > Create Drift VFX) and creates the defaults asset in
//     Assets/Resources/VehicleSystem. After that every vehicle gets drift smoke, sparks and skid marks automatically
//     (AnimeDriftBootstrap adds the component at runtime). No vehicle prefab or existing script is touched.
#if UNITY_EDITOR
using AnimeToon.Editor;
using AnimeToon.Integration;
using UnityEditor;
using UnityEngine;

namespace VehicleSystem.EditorTools
{
    public static class AnimeDriftEffectsInstaller
    {
        const string ResourcesFolder = "Assets/Resources/VehicleSystem";
        const string AssetPath = ResourcesFolder + "/AnimeDriftEffectsDefaults.asset";

        [MenuItem("Tools/Vehicle/Install Anime Drift Effects")]
        static void Install()
        {
            if (!AnimeDriftVfxFactory.DriftPackExists()) AnimeDriftVfxFactory.CreateDriftPack();
            if (!AnimeDriftVfxFactory.DriftPackExists()) return; // the factory already told you why

            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesFolder)) AssetDatabase.CreateFolder("Assets/Resources", "VehicleSystem");

            var defaults = AssetDatabase.LoadAssetAtPath<AnimeDriftEffectsDefaults>(AssetPath);
            bool isNew = defaults == null;
            if (isNew) defaults = ScriptableObject.CreateInstance<AnimeDriftEffectsDefaults>();

            defaults.smoke = AnimeVfxFactory.LoadPrefab("AnimeDriftSmoke");
            defaults.sparks = AnimeVfxFactory.LoadPrefab("AnimeDriftSparks");
            defaults.skid = AnimeDriftVfxFactory.LoadMaterial("AnimeVfx_Skid");

            if (isNew) AssetDatabase.CreateAsset(defaults, AssetPath);
            else EditorUtility.SetDirty(defaults);

            AssetDatabase.SaveAssets();
            Selection.activeObject = defaults;
            EditorGUIUtility.PingObject(defaults);
            Debug.Log("Anime drift effects installed. Vehicles now get drift smoke, sparks and skid marks automatically.");
        }
    }
}
#endif

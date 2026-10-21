// GAME SPECIFIC (Rickshaw Chaos / VehicleSystem). Editor only, place in an "Editor" folder.
//
// Tools > Vehicle > Install Anime Effects
//     Builds the anime VFX pack if needed and puts its prefabs into the VehicleEffectsDefaults slots, so every vehicle
//     that leaves a slot empty uses them. VehicleEffects.cs and the vehicles are not touched.
//     (Tools > Vehicle > Create Default Effects would bring the old placeholders back.)
//
// Tools > Vehicle > Add Anime FX To Camera
//     Adds AnimeSpeedFx and AnimeVehicleFxBridge to the selected camera (or the main camera) with everything wired up.
//     If your camera lives inside a prefab, open that prefab first so the change is saved in it.
#if UNITY_EDITOR
using AnimeToon;
using AnimeToon.Editor;
using AnimeToon.Integration;
using UnityEditor;
using UnityEngine;

namespace VehicleSystem.EditorTools
{
    public static class AnimeVehicleEffectsInstaller
    {
        [MenuItem("Tools/Vehicle/Install Anime Effects")]
        static void InstallEffects()
        {
            if (!AnimeVfxFactory.PackExists()) AnimeVfxFactory.CreatePack();
            if (!AnimeVfxFactory.PackExists()) return; // creation failed, the factory already told you why

            DefaultEffectsBuilder.EnsureDefaults();
            var defaults = AssetDatabase.LoadAssetAtPath<VehicleEffectsDefaults>(DefaultEffectsBuilder.AssetPath);
            if (defaults == null)
            {
                EditorUtility.DisplayDialog("Anime Effects", "Could not find or create the VehicleEffectsDefaults asset.", "OK");
                return;
            }

            defaults.trail = AnimeVfxFactory.LoadPrefab("AnimeTrail");
            defaults.hitSparks = AnimeVfxFactory.LoadPrefab("AnimeHitSparks");
            defaults.hitDust = AnimeVfxFactory.LoadPrefab("AnimeHitDust");
            defaults.lowHealthSmoke = AnimeVfxFactory.LoadPrefab("AnimeLowHealthSmoke");
            defaults.explosion = AnimeVfxFactory.LoadPrefab("AnimeExplosion");

            EditorUtility.SetDirty(defaults);
            AssetDatabase.SaveAssets();
            Selection.activeObject = defaults;
            EditorGUIUtility.PingObject(defaults);
            Debug.Log("Anime effects installed into VehicleEffectsDefaults. Vehicles with empty effect slots now use them.");
        }

        [MenuItem("Tools/Vehicle/Add Anime FX To Camera")]
        static void AddToCamera()
        {
            Camera cam = null;
            if (Selection.activeGameObject != null) cam = Selection.activeGameObject.GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Anime Effects", "Select the gameplay camera first (or tag one as MainCamera).", "OK");
                return;
            }

            if (!AnimeVfxFactory.PackExists()) AnimeVfxFactory.CreatePack();

            var fx = cam.GetComponent<AnimeSpeedFx>();
            if (fx == null) fx = Undo.AddComponent<AnimeSpeedFx>(cam.gameObject);
            else Undo.RecordObject(fx, "Setup Anime FX");

            var bridge = cam.GetComponent<AnimeVehicleFxBridge>();
            if (bridge == null) bridge = Undo.AddComponent<AnimeVehicleFxBridge>(cam.gameObject);
            else Undo.RecordObject(bridge, "Setup Anime FX");

            fx.screenFxShader = Shader.Find("Anime/ScreenFX");
            fx.speedStreaks = AnimeVfxFactory.LoadPrefab("AnimeSpeedStreaks");

            bridge.fx = fx;
            bridge.impactBurstPrefab = AnimeVfxFactory.LoadPrefab("AnimeImpactBurst");
            bridge.shockRingPrefab = AnimeVfxFactory.LoadPrefab("AnimeShockRing");

            EditorUtility.SetDirty(fx);
            EditorUtility.SetDirty(bridge);
            Selection.activeGameObject = cam.gameObject;
        }
    }
}
#endif

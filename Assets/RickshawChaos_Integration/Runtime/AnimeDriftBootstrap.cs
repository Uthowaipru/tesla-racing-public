using UnityEngine;
using VehicleSystem;

namespace AnimeToon.Integration
{
    /// <summary>
    /// GAME SPECIFIC. Adds AnimeDriftEffects (and AnimeVehicleEffectsQuality, when the vehicle has VehicleEffects) to every vehicle that is enabled (player, bots, remote cars), so no vehicle
    /// prefab has to be edited. To opt a vehicle out, add AnimeDriftEffects to its prefab yourself and untick it
    /// (an existing component is never replaced). To stop the automatic adding altogether, set
    /// AnimeDriftBootstrap.autoAdd = false from your own code, or delete this file.
    /// </summary>
    public static class AnimeDriftBootstrap
    {
        public static bool autoAdd = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            // VehicleController clears its static events at startup, so subscribe here, after that
            VehicleController.VehicleEnabled -= Add;
            VehicleController.VehicleEnabled += Add;

            for (int i = 0; i < VehicleController.All.Count; i++) Add(VehicleController.All[i]);
        }

        static void Add(VehicleController vehicle)
        {
            if (!autoAdd || vehicle == null) return;
            if (!vehicle.TryGetComponent(out AnimeDriftEffects _)) vehicle.gameObject.AddComponent<AnimeDriftEffects>();

            // the particles of VehicleEffects follow the quality tier too
            if (vehicle.TryGetComponent(out VehicleEffects _) && !vehicle.TryGetComponent(out AnimeVehicleEffectsQuality _))
                vehicle.gameObject.AddComponent<AnimeVehicleEffectsQuality>();
        }
    }
}

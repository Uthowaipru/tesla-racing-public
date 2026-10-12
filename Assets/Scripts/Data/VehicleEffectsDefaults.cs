using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// The project-wide DEFAULT particle effects. A vehicle uses these for every effect slot it leaves empty, so a vehicle
    /// only needs its own particles when it should look different. The editor creates this asset (and the prefabs it points
    /// to) automatically in Assets/Resources/VehicleSystem; run Tools > Vehicle > Create Default Effects to recreate it.
    ///
    /// Every prefab is a ParticleSystem that emits along its +Z axis (forward). VehicleEffects turns it to point backward
    /// (trail) or upward (smoke) when it places it on a vehicle.
    /// </summary>
    public class VehicleEffectsDefaults : ScriptableObject
    {
        public const string ResourcePath = "VehicleSystem/VehicleEffectsDefaults";

        public ParticleSystem trail;
        public ParticleSystem hitSparks;
        public ParticleSystem hitDust;
        public ParticleSystem lowHealthSmoke;
        public ParticleSystem explosion;

        static VehicleEffectsDefaults cached;

        /// <summary>The defaults asset, or null if it has not been created yet.</summary>
        public static VehicleEffectsDefaults Load()
        {
            if (cached == null) cached = Resources.Load<VehicleEffectsDefaults>(ResourcePath);
            return cached;
        }
    }
}

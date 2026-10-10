using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// One car in the game, as data: which prefab to spawn and its base stats.
    /// Create with Assets > Create > Vehicle System > Vehicle Definition.
    ///
    /// This is read-only at runtime. Upgrades and boosts are StatModifiers on the spawned car's VehicleStats,
    /// never edits to this asset. VehicleStats.ApplyDefinition() copies these values onto a spawned car.
    /// </summary>
    [CreateAssetMenu(menuName = "Vehicle System/Vehicle Definition", fileName = "NewVehicle")]
    public class VehicleDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique id (e.g. \"tesla\"). Sent over the network and saved, so never change it once released.")]
        public string id = "vehicle";
        public string displayName = "Vehicle";
        public Sprite icon;
        [Tooltip("The vehicle prefab (VehicleController + VehicleStats + VehicleRecovery ...).")]
        public GameObject prefab;

        [Header("Base Stats (same meaning as VehicleStats)")]
        public float motorTorque = 600f;
        public float maxSpeedKmh = 55f;
        public float brakeTorque = 1800f;
        public float steerAngle = 32f;
        public float tireGrip = 1f;
        public float suspensionSpring = 18000f;
        public float suspensionDamper = 1800f;
        public float suspensionTravel = 0.25f;
        public float antiRoll = 3000f;

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) id = name;
        }
    }
}

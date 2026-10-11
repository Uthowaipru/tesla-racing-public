using System;
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
        [Tooltip("Optional model-only prefab for the 3D preview in the vehicle selector. " +
                 "Leave empty to show the normal prefab (its scripts and physics are stripped from the preview copy).")]
        public GameObject previewPrefab;

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

        /// <summary>Raised when a value is edited (also while playing), so cars using it can refresh.</summary>
        public static event Action<VehicleDefinition> ValuesChanged;

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) id = name;
            ValuesChanged?.Invoke(this);
        }

        /// <summary>
        /// One-time import: copies the numbers currently typed into the car prefab's VehicleStats into this asset.
        /// Right-click the component header (the three dots) in the Inspector > Copy Stats From Prefab.
        /// </summary>
        [ContextMenu("Copy Stats From Prefab")]
        void CopyStatsFromPrefab()
        {
            if (prefab == null)
            {
                Debug.LogWarning("VehicleDefinition: assign the Prefab first.", this);
                return;
            }

            var s = prefab.GetComponentInChildren<VehicleStats>(true);
            if (s == null)
            {
                Debug.LogWarning($"VehicleDefinition: prefab '{prefab.name}' has no VehicleStats.", this);
                return;
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Copy Stats From Prefab");
#endif
            motorTorque = s.motorTorque;
            maxSpeedKmh = s.maxSpeedKmh;
            brakeTorque = s.brakeTorque;
            steerAngle = s.steerAngle;
            tireGrip = s.tireGrip;
            suspensionSpring = s.suspensionSpring;
            suspensionDamper = s.suspensionDamper;
            suspensionTravel = s.suspensionTravel;
            antiRoll = s.antiRoll;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}

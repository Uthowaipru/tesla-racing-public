using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Creates the cars in a race scene. Put it in the race scene next to a LocalPlayerRig.
    ///
    ///   Single player : on Start it spawns the vehicle the player picked in the selector (PlayerProfile),
    ///                   at the first spawn point, and hands it to the LocalPlayerRig.
    ///   Matches       : SpawnAll(RaceConfig) spawns one car per PlayerSlot (local player, bots, remote players).
    ///                   Only the LocalHuman slot gets the rig; the other cars are left for their own input source.
    ///
    /// Every car gets its VehicleDefinition's base stats. Modifiers are not applied yet: add them here
    /// (slot.modifierIds) once you have a modifier catalog.
    /// </summary>
    [DefaultExecutionOrder(-20)] // after LocalPlayerRig has set itself up
    public class VehicleSpawner : MonoBehaviour
    {
        public VehicleCatalog catalog;
        [Tooltip("Where the cars appear (slot 0 uses the first, slot 1 the second ...). Place them slightly above the ground. Empty = this object.")]
        public Transform[] spawnPoints;
        [Tooltip("Found in the scene if empty. Turn OFF its 'Auto Attach Scene Vehicle' option: this spawner attaches the car.")]
        public LocalPlayerRig localRig;

        [Header("Single player")]
        [Tooltip("On Start: spawn the vehicle chosen in the selector for the local player.")]
        public bool spawnLocalPlayerOnStart = true;

        /// <summary>The local player's car (null before it is spawned).</summary>
        public VehicleController LocalVehicle { get; private set; }

        void Start()
        {
            if (localRig == null) localRig = FindFirstObjectByType<LocalPlayerRig>();
            if (spawnLocalPlayerOnStart) SpawnLocalFromProfile();
        }

        /// <summary>Spawns the vehicle the player picked in the selector (or the catalog's first).</summary>
        public VehicleController SpawnLocalFromProfile()
        {
            var def = PlayerProfile.GetSelectedVehicle(catalog);
            if (def == null)
            {
                Debug.LogError("VehicleSpawner: no vehicle to spawn. Assign a catalog that contains at least one vehicle.", this);
                return null;
            }

            var slot = new PlayerSlot
            {
                slotId = 0,
                displayName = "You",
                vehicleId = def.id,
                controller = SlotController.LocalHuman
            };
            return Spawn(slot);
        }

        /// <summary>Spawns one car per slot of the race.</summary>
        public List<VehicleController> SpawnAll(RaceConfig config)
        {
            var cars = new List<VehicleController>();
            if (config == null) return cars;

            for (int i = 0; i < config.slots.Count; i++)
            {
                var car = Spawn(config.slots[i]);
                if (car != null) cars.Add(car);
            }
            return cars;
        }

        /// <summary>Spawns the slot's vehicle at its spawn point. Returns null if the vehicle id is unknown.</summary>
        public VehicleController Spawn(PlayerSlot slot)
        {
            var def = catalog != null ? catalog.Get(slot.vehicleId) : null;
            if (def == null || def.prefab == null)
            {
                Debug.LogError($"VehicleSpawner: no vehicle with id '{slot.vehicleId}' (or it has no prefab).", this);
                return null;
            }

            Transform point = SpawnPoint(slot.slotId);
            GameObject go = Instantiate(def.prefab, point.position, point.rotation);
            go.name = $"{def.displayName} ({slot.displayName})";

            // the VehicleController may be on the prefab root or on a child (e.g. root "car" > "car lowpoly" > ...)
            VehicleController car = go.GetComponentInChildren<VehicleController>(true);
            if (car == null)
            {
                Debug.LogError($"VehicleSpawner: prefab '{def.prefab.name}' has no VehicleController on it or on any child.", def.prefab);
                Destroy(go);
                return null;
            }

            if (car.TryGetComponent(out VehicleStats stats)) stats.ApplyDefinition(def);

            if (slot.controller == SlotController.LocalHuman && localRig != null)
            {
                localRig.Attach(car);
                LocalVehicle = car;
            }

            return car;
        }

        Transform SpawnPoint(int slotId)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return transform;
            return spawnPoints[Mathf.Abs(slotId) % spawnPoints.Length];
        }
    }
}

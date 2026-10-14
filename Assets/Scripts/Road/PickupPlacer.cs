using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Places batteries and repair kits along the road. When RoadGenerator creates a piece, this decides from the SEED whether
    /// it holds a battery and/or a repair kit, in which lane and where. Every device builds its own copy of the pickups from
    /// the shared seed, so nothing is sent over the network and players never fight over one pickup. The pickups belong to
    /// the piece and disappear when it is recycled.
    /// </summary>
    public class PickupPlacer : MonoBehaviour
    {
        public RoadGenerator road;
        public Pickup batteryPrefab;
        public Pickup repairKitPrefab;

        [Header("How often (chance per piece of road)")]
        [Range(0f, 1f)] public float batteryChance = 0.45f;
        [Range(0f, 1f)] public float repairKitChance = 0.2f;

        [Header("Oncoming road (two-way roads only): the risky side")]
        [Range(0f, 1f)] public float oncomingBatteryChance = 0.35f;
        [Range(0f, 1f)] public float oncomingRepairKitChance = 0.1f;

        [Header("Placement")]
        [Tooltip("No pickups closer than this to the ends of a piece (meters).")]
        public float chunkMargin = 8f;
        [Tooltip("No pickups this close to the very start of the road.")]
        public float startClearDistance = 25f;

        void OnEnable()
        {
            if (road == null) road = FindFirstObjectByType<RoadGenerator>();
            if (road == null) return;

            road.ChunkSpawned += OnChunkSpawned;
            if (road.IsStarted)
                foreach (var chunk in road.ActiveChunks) OnChunkSpawned(chunk);
        }

        void OnDisable()
        {
            if (road != null) road.ChunkSpawned -= OnChunkSpawned;
        }

        void OnChunkSpawned(RoadChunk chunk)
        {
            var rng = SeededRandom.For(road.Seed, chunk.Index, 3);
            TryPlace(ref rng, chunk, batteryPrefab, batteryChance, false);
            TryPlace(ref rng, chunk, repairKitPrefab, repairKitChance, false);

            if (road.OncomingLaneCount > 0)
            {
                var oncomingRng = SeededRandom.For(road.Seed, chunk.Index, 8); // its own numbers: the normal road never changes
                TryPlace(ref oncomingRng, chunk, batteryPrefab, oncomingBatteryChance, true);
                TryPlace(ref oncomingRng, chunk, repairKitPrefab, oncomingRepairKitChance, true);
            }
        }

        void TryPlace(ref SeededRandom rng, RoadChunk chunk, Pickup prefab, float chance, bool oncoming)
        {
            // always take the same random numbers, so a missing prefab never changes the other pickup
            float roll = rng.NextFloat();
            int lane = rng.Range(0, oncoming ? road.OncomingLaneCount : road.LaneCount);
            float t = rng.NextFloat();

            if (prefab == null || roll >= chance) return;

            float usable = Mathf.Max(1f, chunk.Length - 2f * chunkMargin);
            float s = chunk.StartS + chunkMargin + t * usable;
            if (s < startClearDistance) return;

            float offset = oncoming ? road.OncomingLaneOffset(lane) : road.LaneOffset(lane);
            Vector3 position = road.Path.Evaluate(s) + road.Path.Right(s) * offset;
            Quaternion rotation = Quaternion.LookRotation(road.Path.Tangent(s), Vector3.up);
            Instantiate(prefab, position, rotation, chunk.SpawnedRoot);
        }
    }
}

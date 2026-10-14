using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Fills the road with NPC traffic. When RoadGenerator creates a piece, this decides from the SEED which vehicles drive
    /// on it, in which lane and where. Each device builds its own traffic from the shared seed (nothing is sent over the
    /// network), and offline levels with a fixed seed get the same traffic every time.
    ///
    ///   - Every lane has its own speed (slow on the left, fast on the right) and all vehicles in a lane drive at it, so NPCs
    ///     never run into each other. The player is faster than all of them: that is what the player overtakes and bumps.
    ///   - Vehicles are recycled (pooled) and removed when far behind the player.
    ///   - Difficulty: "Density Multiplier" (more or fewer vehicles) and "Speed Multiplier".
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        public RoadGenerator road;
        [Tooltip("The vehicles that can appear. Each has its own weight and allowed lanes.")]
        public TrafficVehicle[] vehiclePrefabs;

        [Header("Density")]
        public int minPerChunk = 3;
        public int maxPerChunk = 6;
        [Range(0f, 3f)] public float densityMultiplier = 1f;

        [Header("Speeds (m/s, from the slowest lane to the fastest)")]
        public float slowestLaneSpeed = 4f;
        public float fastestLaneSpeed = 9f;
        public float speedMultiplier = 1f;

        [Header("Oncoming traffic (only on a two-way road)")]
        public bool spawnOncoming = true;
        public int oncomingMinPerChunk = 2;
        public int oncomingMaxPerChunk = 4;
        [Range(0f, 3f)] public float oncomingDensityMultiplier = 1f;
        [Tooltip("Speeds of the oncoming lanes (m/s), from the slowest (outer edge) to the fastest (next to the median).")]
        public float oncomingSlowestLaneSpeed = 5f;
        public float oncomingFastestLaneSpeed = 10f;
        [Tooltip("No oncoming traffic closer than this to the start of the road (meters), so players start safely.")]
        public float oncomingStartClearDistance = 90f;
        [Tooltip("Vehicles for the oncoming road. Empty = the same list as the normal road.")]
        public TrafficVehicle[] oncomingVehiclePrefabs;

        [Header("Placement")]
        [Tooltip("No traffic closer than this to the start of a piece (meters), so pieces never crowd at their joints.")]
        public float chunkMargin = 10f;
        [Tooltip("No traffic this close to the very start of the road, where players begin.")]
        public float startClearDistance = 35f;
        [Tooltip("Minimum gap between vehicles in the same lane (meters).")]
        public float minGap = 12f;

        [Header("Cleanup")]
        [Tooltip("Vehicles this far behind the player are recycled (meters). Keep it below the length of the pieces kept behind.")]
        public float despawnBehind = 40f;

        struct Placed { public int lane; public float s; public float length; }

        readonly List<TrafficVehicle> active = new List<TrafficVehicle>();
        readonly Dictionary<TrafficVehicle, Stack<TrafficVehicle>> pools = new Dictionary<TrafficVehicle, Stack<TrafficVehicle>>();
        readonly List<Placed> placed = new List<Placed>();
        float[] laneSpeeds = new float[0];
        float[] oncomingLaneSpeeds = new float[0];
        float cleanTimer;

        void OnEnable()
        {
            if (road == null) road = FindFirstObjectByType<RoadGenerator>();
            if (road == null) return;

            road.Restarted += OnRestarted;
            road.ChunkSpawned += OnChunkSpawned;

            if (road.IsStarted)
            {
                BuildLaneSpeeds();
                foreach (var chunk in road.ActiveChunks) OnChunkSpawned(chunk);
            }
        }

        void OnDisable()
        {
            if (road == null) return;
            road.Restarted -= OnRestarted;
            road.ChunkSpawned -= OnChunkSpawned;
        }

        void OnRestarted()
        {
            for (int i = active.Count - 1; i >= 0; i--) Release(active[i]);
            BuildLaneSpeeds();
        }

        /// <summary>The speed of a lane, from the seed: ordered from slow (left) to fast (right), with a little variation.</summary>
        void BuildLaneSpeeds()
        {
            int lanes = Mathf.Max(1, road.LaneCount);
            laneSpeeds = new float[lanes];
            for (int i = 0; i < lanes; i++)
            {
                var rng = SeededRandom.For(road.Seed, i, 4);
                float t = lanes > 1 ? i / (float)(lanes - 1) : 0f;
                laneSpeeds[i] = Mathf.Lerp(slowestLaneSpeed, fastestLaneSpeed, t) + (rng.NextFloat() - 0.5f) * 0.6f;
            }

            int oncoming = road.OncomingLaneCount;
            oncomingLaneSpeeds = new float[oncoming];
            for (int i = 0; i < oncoming; i++)
            {
                var rng = SeededRandom.For(road.Seed, i, 7);
                float t = oncoming > 1 ? i / (float)(oncoming - 1) : 0f;
                oncomingLaneSpeeds[i] = Mathf.Lerp(oncomingSlowestLaneSpeed, oncomingFastestLaneSpeed, t) + (rng.NextFloat() - 0.5f) * 0.6f;
            }
        }

        // ------------------------------------------------------------------ filling a piece of road

        void OnChunkSpawned(RoadChunk chunk)
        {
            if (vehiclePrefabs == null || vehiclePrefabs.Length == 0 || laneSpeeds.Length == 0) return;

            var rng = SeededRandom.For(road.Seed, chunk.Index, 2);
            int count = Mathf.RoundToInt(rng.Range(minPerChunk, maxPerChunk + 1) * densityMultiplier);
            int lanes = road.LaneCount;
            float usable = Mathf.Max(1f, chunk.Length - 2f * chunkMargin);

            placed.Clear();
            for (int i = 0; i < count; i++)
            {
                // always take the same random numbers, whatever is skipped, so the rest of the piece never changes
                int lane = rng.Range(0, lanes);
                float vehicleRoll = rng.NextFloat();
                float s = chunk.StartS + chunkMargin + rng.NextFloat() * usable;

                TrafficVehicle prefab = Choose(vehiclePrefabs, vehicleRoll, lane);
                if (prefab == null || s < startClearDistance) continue;
                if (!HasRoom(lane, s, prefab.length)) continue;

                placed.Add(new Placed { lane = lane, s = s, length = prefab.length });

                TrafficVehicle vehicle = Acquire(prefab);
                vehicle.Begin(road.Path, s, road.LaneOffset(lane), laneSpeeds[lane] * speedMultiplier, 1);
                active.Add(vehicle);
            }

            if (spawnOncoming && road.OncomingLaneCount > 0) SpawnOncoming(chunk);
        }

        /// <summary>Traffic on the oncoming road: it drives toward the player. Lane numbers are in the driver's terms (0 = slowest, the outer edge).</summary>
        void SpawnOncoming(RoadChunk chunk)
        {
            TrafficVehicle[] prefabs = oncomingVehiclePrefabs != null && oncomingVehiclePrefabs.Length > 0 ? oncomingVehiclePrefabs : vehiclePrefabs;
            int lanes = road.OncomingLaneCount;
            if (prefabs == null || prefabs.Length == 0 || oncomingLaneSpeeds.Length < lanes) return;

            var rng = SeededRandom.For(road.Seed, chunk.Index, 6);
            int count = Mathf.RoundToInt(rng.Range(oncomingMinPerChunk, oncomingMaxPerChunk + 1) * oncomingDensityMultiplier);
            float usable = Mathf.Max(1f, chunk.Length - 2f * chunkMargin);

            placed.Clear();
            for (int i = 0; i < count; i++)
            {
                int drivingLane = rng.Range(0, lanes);
                float vehicleRoll = rng.NextFloat();
                float s = chunk.StartS + chunkMargin + rng.NextFloat() * usable;

                TrafficVehicle prefab = Choose(prefabs, vehicleRoll, drivingLane);
                if (prefab == null || s < oncomingStartClearDistance) continue;
                if (!HasRoom(drivingLane, s, prefab.length)) continue;

                placed.Add(new Placed { lane = drivingLane, s = s, length = prefab.length });

                // the slow lane of the oncoming road is the outer edge, the fast lane is next to the median
                int physicalLane = lanes - 1 - drivingLane;

                TrafficVehicle vehicle = Acquire(prefab);
                vehicle.Begin(road.Path, s, road.OncomingLaneOffset(physicalLane), oncomingLaneSpeeds[drivingLane] * speedMultiplier, -1);
                active.Add(vehicle);
            }
        }

        static TrafficVehicle Choose(TrafficVehicle[] prefabs, float roll, int lane)
        {
            float total = 0f;
            for (int i = 0; i < prefabs.Length; i++)
                if (Allowed(prefabs[i], lane)) total += prefabs[i].weight;

            if (total <= 0f) return null;

            float pick = roll * total;
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (!Allowed(prefabs[i], lane)) continue;
                pick -= prefabs[i].weight;
                if (pick <= 0f) return prefabs[i];
            }
            return null;
        }

        static bool Allowed(TrafficVehicle v, int lane) =>
            v != null && v.weight > 0f && lane >= v.minLane && (v.maxLane < 0 || lane <= v.maxLane);

        bool HasRoom(int lane, float s, float length)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i].lane != lane) continue;
                if (Mathf.Abs(placed[i].s - s) < minGap + (placed[i].length + length) * 0.5f) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ pooling and cleanup

        TrafficVehicle Acquire(TrafficVehicle prefab)
        {
            if (pools.TryGetValue(prefab, out var stack) && stack.Count > 0) return stack.Pop();

            TrafficVehicle instance = Instantiate(prefab, transform);
            instance.sourcePrefab = prefab;
            return instance;
        }

        void Release(TrafficVehicle vehicle)
        {
            active.Remove(vehicle);
            vehicle.gameObject.SetActive(false);

            if (!pools.TryGetValue(vehicle.sourcePrefab, out var stack))
            {
                stack = new Stack<TrafficVehicle>();
                pools[vehicle.sourcePrefab] = stack;
            }
            stack.Push(vehicle);
        }

        void Update()
        {
            cleanTimer -= Time.deltaTime;
            if (cleanTimer > 0f || road == null) return;
            cleanTimer = 0.5f;

            float behindLimit = road.PlayerS - despawnBehind;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                TrafficVehicle v = active[i];
                bool ranOutOfRoad = v.Direction < 0 && v.S <= road.Path.StartS + 2f; // oncoming vehicle reached the end of the built road
                if (v.Expired || v.S < behindLimit || ranOutOfRoad) Release(v);
            }
        }
    }
}

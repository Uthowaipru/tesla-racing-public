using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Builds an endless road from a few prefab pieces (RoadChunk), driven by a SEED. The same seed gives the same road on
    /// every device, so in a room every player uses the host's seed and drives the same road. Offline levels use a fixed
    /// seed per level.
    ///
    ///   - Which piece comes next is decided only by (seed, piece number), never by what happened in the game.
    ///   - Only the pieces around the local vehicle exist: a few ahead, a few behind. Pieces are recycled, not destroyed.
    ///   - Every player's phone builds its own road around its own vehicle, so players far apart do not affect each other.
    ///   - TrafficManager and PickupPlacer fill each new piece (also from the seed).
    ///
    /// Set up with GameObject > Vehicle > Road System. The vehicle to follow is given by LocalPlayerRig.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class RoadGenerator : MonoBehaviour
    {
        [Header("Seed")]
        public int seed = 12345;
        [Tooltip("Pick a new random seed every time the game starts (endless mode). Off = the seed above, the same road every time.")]
        public bool randomSeedOnStart = false;
        [Tooltip("Start building when the scene starts. Turn off to start it yourself with Begin(seed) (for example when the host's seed arrives).")]
        public bool autoStart = true;

        [Header("Road pieces (the first one gives the lane layout)")]
        public RoadChunk[] chunkPrefabs;
        [Tooltip("How many pieces exist ahead of the vehicle.")]
        public int chunksAhead = 4;
        [Tooltip("How many pieces are kept behind the vehicle.")]
        public int chunksBehind = 1;
        [Tooltip("The road may not bend further than this from its start direction (degrees), so it does not run into itself.")]
        public float maxHeading = 75f;

        [Header("Start")]
        [Tooltip("Where vehicles start, meters from the beginning of the road.")]
        public float startDistance = 8f;
        [Tooltip("Distance between rows of starting vehicles.")]
        public float rowSpacing = 6f;
        [Tooltip("Height above the road that vehicles are placed at.")]
        public float spawnHeight = 0.4f;

        [Header("Follow")]
        [Tooltip("The vehicle the road is built around. LocalPlayerRig sets it.")]
        public Transform target;

        public RoadPath Path { get; } = new RoadPath();
        public int Seed { get; private set; }
        public bool IsStarted { get; private set; }
        /// <summary>How far along the road the followed vehicle is (meters).</summary>
        public float PlayerS { get; private set; }
        public int LaneCount => chunkPrefabs != null && chunkPrefabs.Length > 0 && chunkPrefabs[0] != null ? chunkPrefabs[0].laneCount : 3;
        public float LaneWidth => chunkPrefabs != null && chunkPrefabs.Length > 0 && chunkPrefabs[0] != null ? chunkPrefabs[0].laneWidth : 3.2f;
        public IReadOnlyList<RoadChunk> ActiveChunks => active;

        /// <summary>A new piece of road exists (traffic and pickups are placed on it).</summary>
        public event Action<RoadChunk> ChunkSpawned;
        /// <summary>A piece is about to be recycled.</summary>
        public event Action<RoadChunk> ChunkRemoved;
        /// <summary>The road was (re)started with a seed: everything on it is gone.</summary>
        public event Action Restarted;

        readonly List<Pose> poses = new List<Pose>();          // where piece k starts
        readonly List<float> headings = new List<float>();     // road direction at the start of piece k (degrees from the start)
        readonly List<int> prefabOfChunk = new List<int>();    // which prefab piece k is
        readonly List<RoadChunk> active = new List<RoadChunk>();
        readonly Dictionary<RoadChunk, Stack<RoadChunk>> pools = new Dictionary<RoadChunk, Stack<RoadChunk>>();
        readonly List<Vector3> scratch = new List<Vector3>();
        int nextIndex;

        void Start()
        {
            if (autoStart) Begin(randomSeedOnStart ? Environment.TickCount : seed);
        }

        public void SetTarget(Transform newTarget) => target = newTarget;

        // ------------------------------------------------------------------ start / restart

        /// <summary>Starts (or restarts) the road with a seed. Everything on the old road is removed.</summary>
        public void Begin(int newSeed)
        {
            if (chunkPrefabs == null || chunkPrefabs.Length == 0)
            {
                Debug.LogError("RoadGenerator: no road pieces assigned. Use GameObject > Vehicle > Road System, or Tools > Vehicle > Create Road Pieces.", this);
                return;
            }

            // remove the old road
            for (int i = 0; i < active.Count; i++) Release(active[i], notifyAndClear: true);
            active.Clear();
            Path.Clear();

            Seed = newSeed;
            seed = newSeed;
            poses.Clear();
            headings.Clear();
            prefabOfChunk.Clear();
            poses.Add(new Pose(transform.position, transform.rotation));
            headings.Add(0f);
            nextIndex = 0;
            PlayerS = 0f;

            IsStarted = true;
            Restarted?.Invoke();

            while (nextIndex <= chunksAhead) Spawn(nextIndex++);
        }

        // ------------------------------------------------------------------ following the vehicle

        void Update()
        {
            if (!IsStarted || target == null) return;

            PlayerS = Path.ProjectS(target.position, PlayerS);
            int current = CurrentIndex(PlayerS);

            while (nextIndex <= current + chunksAhead) Spawn(nextIndex++);

            while (active.Count > 1 && active[0].Index < current - chunksBehind)
            {
                RoadChunk old = active[0];
                active.RemoveAt(0);
                Release(old, notifyAndClear: true);
                Path.RemoveFirst(old.pathPointCount);
            }
        }

        int CurrentIndex(float s)
        {
            if (active.Count == 0) return 0;
            if (s < active[0].StartS) return active[0].Index;

            for (int i = 0; i < active.Count; i++)
                if (s < active[i].EndS) return active[i].Index;

            return active[active.Count - 1].Index;
        }

        // ------------------------------------------------------------------ choosing and placing pieces

        void EnsureLayout(int index)
        {
            while (prefabOfChunk.Count <= index)
            {
                int k = prefabOfChunk.Count;
                int pi = ChoosePrefab(k, headings[k]);
                prefabOfChunk.Add(pi);

                RoadChunk prefab = chunkPrefabs[pi];
                prefab.GetExitLocal(out Vector3 exitPosition, out Quaternion exitRotation);

                Pose start = poses[k];
                poses.Add(new Pose(start.position + start.rotation * exitPosition, start.rotation * exitRotation));
                headings.Add(Mathf.DeltaAngle(0f, headings[k] + prefab.TurnAngle));
            }
        }

        /// <summary>Picks piece k from the seed: weighted, only pieces allowed at this number, and never bending the road too far.</summary>
        int ChoosePrefab(int index, float heading)
        {
            var rng = SeededRandom.For(Seed, index, 1);
            float roll = rng.NextFloat();

            float total = 0f;
            int bestFallback = 0;
            float bestFallbackHeading = float.MaxValue;
            var weights = new float[chunkPrefabs.Length];

            for (int i = 0; i < chunkPrefabs.Length; i++)
            {
                RoadChunk c = chunkPrefabs[i];
                if (c == null || c.weight <= 0f) continue;
                if (index < c.firstIndex || (c.lastIndex >= 0 && index > c.lastIndex)) continue;

                float after = Mathf.Abs(Mathf.DeltaAngle(0f, heading + c.TurnAngle));
                if (after < bestFallbackHeading) { bestFallbackHeading = after; bestFallback = i; }
                if (after > maxHeading) continue;

                weights[i] = c.weight;
                total += c.weight;
            }

            if (total <= 0f) return bestFallback; // nothing fits: take the piece that bends the road back the most

            float pick = roll * total;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f) continue;
                pick -= weights[i];
                if (pick <= 0f) return i;
            }
            return bestFallback;
        }

        void Spawn(int index)
        {
            EnsureLayout(index);

            RoadChunk prefab = chunkPrefabs[prefabOfChunk[index]];
            RoadChunk chunk = Acquire(prefab);

            Pose pose = poses[index];
            chunk.transform.SetPositionAndRotation(pose.position, pose.rotation);
            chunk.gameObject.SetActive(true);
            chunk.Index = index;

            chunk.StartS = Path.Count > 0 ? Path.EndS : 0f;
            chunk.GetWorldPoints(scratch);
            chunk.pathPointCount = Path.Append(scratch);
            chunk.EndS = Path.EndS;

            active.Add(chunk);
            ChunkSpawned?.Invoke(chunk);
        }

        RoadChunk Acquire(RoadChunk prefab)
        {
            if (pools.TryGetValue(prefab, out var stack) && stack.Count > 0) return stack.Pop();

            RoadChunk instance = Instantiate(prefab, transform);
            instance.sourcePrefab = prefab;
            return instance;
        }

        void Release(RoadChunk chunk, bool notifyAndClear)
        {
            if (notifyAndClear)
            {
                ChunkRemoved?.Invoke(chunk);
                chunk.ClearSpawned();
            }

            chunk.gameObject.SetActive(false);

            if (!pools.TryGetValue(chunk.sourcePrefab, out var stack))
            {
                stack = new Stack<RoadChunk>();
                pools[chunk.sourcePrefab] = stack;
            }
            stack.Push(chunk);
        }

        // ------------------------------------------------------------------ helpers for other systems

        /// <summary>Distance across the road from the centre line to the middle of a lane (negative = left).</summary>
        public float LaneOffset(int lane) => (lane - (LaneCount - 1) * 0.5f) * LaneWidth;

        /// <summary>Where a vehicle starts: slot 0 in the first lane, the next slots beside it, then the next row.</summary>
        public Pose GetStartPose(int slot)
        {
            int lanes = Mathf.Max(1, LaneCount);
            int lane = slot % lanes;
            int row = slot / lanes;

            float s = startDistance + row * rowSpacing;
            Vector3 position = Path.Evaluate(s) + Path.Right(s) * LaneOffset(lane) + Vector3.up * spawnHeight;
            return new Pose(position, Quaternion.LookRotation(Path.Tangent(s), Vector3.up));
        }

        /// <summary>
        /// Is this position on the road (a little beyond its edges counts)? VehicleRecovery uses it: a repair kit teleports
        /// a vehicle that is not on the road.
        /// </summary>
        public bool IsOnRoad(Vector3 position)
        {
            if (!IsStarted || Path.Count < 2) return true;

            float s = Path.ProjectS(position, -1f);
            Vector3 centre = Path.Evaluate(s);
            Vector3 offset = position - centre;
            offset.y = 0f;

            float halfWidth = LaneCount * LaneWidth * 0.5f + 2f;
            return offset.magnitude <= halfWidth;
        }
    }
}

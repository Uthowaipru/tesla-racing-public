using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>Which road a position is on: the normal one (the way the player drives) or the oncoming one (wrong way).</summary>
    public enum RoadSide { Forward, Oncoming }

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
    /// TWO-WAY ROAD: tick "Two Way Road" (in each scene as you like) and the road is built from the two-way pieces instead:
    /// forward lanes, a median, and oncoming lanes. Every few pieces comes a crossover piece with an opening in the median.
    /// Players start on the forward road; driving on the oncoming road is "wrong way" (see WrongWayTracker).
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

        [Header("One-way road pieces (the first one gives the lane layout)")]
        public RoadChunk[] chunkPrefabs;

        [Header("Two-way road: turn it on or off in each scene")]
        [Tooltip("ON: the road is built from the two-way pieces below, with oncoming lanes and crossovers. OFF: the one-way pieces above.")]
        public bool twoWayRoad = false;
        [Tooltip("Pieces with a median and oncoming lanes, plus crossover pieces (RoadChunk > Is Crossover). The first one gives the lane layout.")]
        public RoadChunk[] twoWayChunkPrefabs;
        [Tooltip("The first crossover is at this piece number.")]
        public int firstCrossoverIndex = 3;
        [Tooltip("After a crossover the next one comes after this many pieces (a number between min and max, from the seed).")]
        public int minPiecesBetweenCrossovers = 4;
        public int maxPiecesBetweenCrossovers = 8;

        [Header("Building around the vehicle")]
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
        public IReadOnlyList<RoadChunk> ActiveChunks => active;

        /// <summary>True when the two-way pieces are in use (oncoming lanes exist).</summary>
        public bool IsTwoWay
        {
            get
            {
                RoadChunk layout = Layout;
                return twoWayRoad && ReferenceEquals(Prefabs, twoWayChunkPrefabs) && layout != null && layout.oncomingLaneCount > 0;
            }
        }

        // ---- lane layout (read from the first piece in the list that is in use)
        RoadChunk[] Prefabs => activePrefabs ?? ResolvePrefabs();
        RoadChunk Layout => Prefabs != null && Prefabs.Length > 0 ? Prefabs[0] : null;

        /// <summary>Lanes of the normal road (the way the player drives), counted from the left (0).</summary>
        public int LaneCount => Layout != null ? Layout.laneCount : 3;
        public int ForwardLaneCount => LaneCount;
        public int OncomingLaneCount => IsTwoWay ? Layout.oncomingLaneCount : 0;
        public float LaneWidth => Layout != null ? Layout.laneWidth : 3.2f;
        public float MedianWidth => OncomingLaneCount > 0 ? Layout.medianWidth : 0f;
        public float TotalWidth => ForwardLaneCount * LaneWidth + (OncomingLaneCount > 0 ? MedianWidth + OncomingLaneCount * LaneWidth : 0f);

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
        RoadChunk[] activePrefabs;
        int nextIndex, nextCrossoverIndex, crossoverCount;

        RoadChunk[] ResolvePrefabs()
        {
            if (twoWayRoad && twoWayChunkPrefabs != null && twoWayChunkPrefabs.Length > 0) return twoWayChunkPrefabs;
            return chunkPrefabs;
        }

        void Awake()
        {
            // Road Systems made before the wrong-way scoring existed have no tracker: add it, it does nothing on a one-way road
            if (GetComponent<WrongWayTracker>() == null) gameObject.AddComponent<WrongWayTracker>();
        }

        void Start()
        {
            if (autoStart) Begin(randomSeedOnStart ? Environment.TickCount : seed);
        }

        public void SetTarget(Transform newTarget) => target = newTarget;

        // ------------------------------------------------------------------ start / restart

        /// <summary>Starts (or restarts) the road with a seed. Everything on the old road is removed.</summary>
        public void Begin(int newSeed)
        {
            RoadChunk[] prefabs = ResolvePrefabs();
            if (prefabs == null || prefabs.Length == 0)
            {
                Debug.LogError("RoadGenerator: no road pieces assigned. Use GameObject > Vehicle > Road System, or Tools > Vehicle > Create Road Pieces.", this);
                return;
            }

            // remove the old road
            for (int i = 0; i < active.Count; i++) Release(active[i], notifyAndClear: true);
            active.Clear();
            Path.Clear();

            activePrefabs = prefabs;
            Seed = newSeed;
            seed = newSeed;
            poses.Clear();
            headings.Clear();
            prefabOfChunk.Clear();
            poses.Add(new Pose(transform.position, transform.rotation));
            headings.Add(0f);
            nextIndex = 0;
            nextCrossoverIndex = firstCrossoverIndex;
            crossoverCount = 0;
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

                RoadChunk prefab = activePrefabs[pi];
                prefab.GetExitLocal(out Vector3 exitPosition, out Quaternion exitRotation);

                Pose start = poses[k];
                poses.Add(new Pose(start.position + start.rotation * exitPosition, start.rotation * exitRotation));
                headings.Add(Mathf.DeltaAngle(0f, headings[k] + prefab.TurnAngle));
            }
        }

        /// <summary>
        /// Picks piece k from the seed. At the scheduled numbers of a two-way road it is a crossover; otherwise a weighted pick of
        /// the pieces allowed at this number, never bending the road too far.
        /// </summary>
        int ChoosePrefab(int index, float heading)
        {
            RoadChunk[] prefabs = activePrefabs;

            if (IsTwoWay && index == nextCrossoverIndex)
            {
                int crossover = ChooseCrossover(index);
                if (crossover >= 0)
                {
                    var spacing = SeededRandom.For(Seed, crossoverCount++, 5);
                    nextCrossoverIndex += Mathf.Max(1, spacing.Range(minPiecesBetweenCrossovers, maxPiecesBetweenCrossovers + 1));
                    return crossover;
                }
            }

            var rng = SeededRandom.For(Seed, index, 1);
            float roll = rng.NextFloat();

            float total = 0f;
            int bestFallback = 0;
            float bestFallbackHeading = float.MaxValue;
            var weights = new float[prefabs.Length];

            for (int i = 0; i < prefabs.Length; i++)
            {
                RoadChunk c = prefabs[i];
                if (c == null || c.weight <= 0f || c.isCrossover) continue;
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

        int ChooseCrossover(int index)
        {
            RoadChunk[] prefabs = activePrefabs;
            var rng = SeededRandom.For(Seed, index, 1);
            float roll = rng.NextFloat();

            float total = 0f;
            for (int i = 0; i < prefabs.Length; i++)
                if (prefabs[i] != null && prefabs[i].isCrossover && prefabs[i].weight > 0f) total += prefabs[i].weight;
            if (total <= 0f) return -1;

            float pick = roll * total;
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null || !prefabs[i].isCrossover || prefabs[i].weight <= 0f) continue;
                pick -= prefabs[i].weight;
                if (pick <= 0f) return i;
            }
            return -1;
        }

        void Spawn(int index)
        {
            EnsureLayout(index);

            RoadChunk prefab = activePrefabs[prefabOfChunk[index]];
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

        // ------------------------------------------------------------------ lanes and sides (for other systems)

        /// <summary>
        /// Distance across the road from the centre line to the middle of a FORWARD lane (negative = left). Lane 0 is the
        /// leftmost, the slow lane. With oncoming lanes the centre line is the middle of the median.
        /// </summary>
        public float LaneOffset(int lane) => -TotalWidth * 0.5f + (lane + 0.5f) * LaneWidth;

        /// <summary>Same for an ONCOMING lane: lane 0 is the one next to the median, the last one is at the outer edge.</summary>
        public float OncomingLaneOffset(int lane) =>
            -TotalWidth * 0.5f + ForwardLaneCount * LaneWidth + MedianWidth + (lane + 0.5f) * LaneWidth;

        /// <summary>
        /// How far a position is from the centre line across the road (positive = right, the oncoming side). Also gives how
        /// far along the road it is; pass the previous s as the hint.
        /// </summary>
        public float LateralOffset(Vector3 position, float sHint, out float s)
        {
            s = Path.ProjectS(position, sHint);
            return Vector3.Dot(position - Path.Evaluate(s), Path.Right(s));
        }

        /// <summary>Where a vehicle starts: slot 0 in the first lane, the next slots beside it, then the next row.</summary>
        public Pose GetStartPose(int slot)
        {
            int lanes = Mathf.Max(1, ForwardLaneCount);
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

            float lateral = LateralOffset(position, -1f, out _);
            return Mathf.Abs(lateral) <= TotalWidth * 0.5f + 2f;
        }
    }
}

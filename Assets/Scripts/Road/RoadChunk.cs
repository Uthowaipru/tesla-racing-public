using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// One piece of road (a prefab). The generator joins pieces end to end: the entry of the next piece is placed at the
    /// exit of the one before.
    ///
    /// Build your own pieces (Dhaka streets, junction-like bends ...) as prefabs with this component:
    ///   - The ENTRY is this object's origin, the road runs along +Z there, at ground level.
    ///   - "Centerline" lists points along the middle of the road from the entry to the exit, in order (empty child objects).
    ///     A straight piece needs none: it uses "Straight Length". The last segment gives the exit direction.
    ///   - Every piece in a road must have the same Lane Count and Lane Width.
    ///
    /// The editor creates simple default pieces (Tools > Vehicle > Create Road Pieces) that you can replace with your own art.
    /// </summary>
    public class RoadChunk : MonoBehaviour
    {
        [Header("Shape (entry at the origin, road runs along +Z)")]
        [Tooltip("Empty objects along the middle of the road, from the entry (origin) to the exit. Leave empty for a straight road.")]
        public Transform[] centerline;
        [Tooltip("Length of a straight piece (used when the centerline is empty).")]
        public float straightLength = 60f;

        [Header("Lanes (same in every piece)")]
        public int laneCount = 3;
        public float laneWidth = 3.2f;

        [Header("Two-way road (leave Oncoming Lane Count at 0 for a one-way piece)")]
        [Tooltip("Lanes of the OPPOSITE road, to the right of the median. 0 = a one-way piece.")]
        public int oncomingLaneCount = 0;
        [Tooltip("Width of the median between the two roads (meters). Only used when there are oncoming lanes.")]
        public float medianWidth = 2.4f;
        [Tooltip("This piece has an OPENING in the median, so players can cross to the other road. The generator places these at the spacing set on the RoadGenerator; they are never picked as normal pieces.")]
        public bool isCrossover;

        [Header("Selection")]
        [Tooltip("How likely this piece is chosen (0 = never).")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Only used from this chunk number on (0 = from the very start). Use it to start with straight roads.")]
        public int firstIndex = 0;
        [Tooltip("Not used after this chunk number. -1 = no limit.")]
        public int lastIndex = -1;
        [Tooltip("Reserved for the finish line mode.")]
        public bool isFinish;

        // ---- set by the generator
        [HideInInspector] public RoadChunk sourcePrefab;
        [HideInInspector] public int pathPointCount;
        public int Index { get; set; }
        /// <summary>Distance along the road where this piece starts / ends.</summary>
        public float StartS { get; set; }
        public float EndS { get; set; }
        public float Length => EndS - StartS;

        Transform spawnedRoot;
        static readonly List<Vector3> scratch = new List<Vector3>();

        /// <summary>Parent for things the generator places on this piece (pickups). Cleared when the piece is recycled.</summary>
        public Transform SpawnedRoot
        {
            get
            {
                if (spawnedRoot == null)
                {
                    var go = new GameObject("Spawned");
                    go.transform.SetParent(transform, false);
                    spawnedRoot = go.transform;
                }
                return spawnedRoot;
            }
        }

        public void ClearSpawned()
        {
            if (spawnedRoot == null) return;
            for (int i = spawnedRoot.childCount - 1; i >= 0; i--)
                Destroy(spawnedRoot.GetChild(i).gameObject);
        }

        /// <summary>Width of the whole road of this piece: forward lanes + median + oncoming lanes.</summary>
        public float TotalWidth => laneCount * laneWidth + (oncomingLaneCount > 0 ? medianWidth + oncomingLaneCount * laneWidth : 0f);

        /// <summary>The centre line in this piece's own space.</summary>
        public void GetLocalPoints(List<Vector3> into)
        {
            into.Clear();
            if (centerline != null && centerline.Length >= 2)
            {
                for (int i = 0; i < centerline.Length; i++)
                    if (centerline[i] != null) into.Add(transform.InverseTransformPoint(centerline[i].position));
            }

            if (into.Count < 2)
            {
                into.Clear();
                into.Add(Vector3.zero);
                into.Add(new Vector3(0f, 0f, Mathf.Max(1f, straightLength)));
            }
        }

        /// <summary>The centre line in world space (for a placed piece).</summary>
        public void GetWorldPoints(List<Vector3> into)
        {
            GetLocalPoints(into);
            for (int i = 0; i < into.Count; i++) into[i] = transform.TransformPoint(into[i]);
        }

        /// <summary>Where the next piece starts, relative to this piece.</summary>
        public void GetExitLocal(out Vector3 position, out Quaternion rotation)
        {
            GetLocalPoints(scratch);
            position = scratch[scratch.Count - 1];

            Vector3 direction = position - scratch[scratch.Count - 2];
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// <summary>How many degrees this piece turns the road (positive = to the right).</summary>
        public float TurnAngle
        {
            get
            {
                GetExitLocal(out _, out Quaternion rotation);
                return Mathf.DeltaAngle(0f, rotation.eulerAngles.y);
            }
        }
    }
}

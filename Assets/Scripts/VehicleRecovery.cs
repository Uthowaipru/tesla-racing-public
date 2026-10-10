using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Gets the vehicle out of trouble. Works two ways:
    ///   1. Manual : call Recover() (VehicleInput does this for the R key, gamepad Y/North, and the mobile RESET button).
    ///               Puts the car upright, in place if the spot is free, otherwise at the nearest free spot.
    ///   2. Auto   : - flipped / on its side for a while  -> put upright (same as manual).
    ///               - stuck while the player is driving  -> "Reverse Out": the script drives the car backwards
    ///                 (or forwards if it was reversing) for a moment while steering away from the obstacle,
    ///                 like arcade racing games. If that fails a couple of times it falls back to the clear-spot reset.
    ///
    /// Setup: add to the vehicle (next to VehicleController). VehicleInput hands control over during Reverse Out.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleController))]
    public class VehicleRecovery : MonoBehaviour
    {
        public enum StuckRecoverMode { ReverseOut, NearbyClearSpot, LastSafePoint }

        [Header("Reset (button / key)")]
        [Tooltip("Lift used when no ground can be found at all (meters).")]
        public float liftHeight = 1.2f;
        [Tooltip("Minimum time between two resets (seconds).")]
        public float cooldown = 1.5f;

        [Header("Find Clear Spot (used by reset)")]
        [Tooltip("If the car would overlap something where it is, move it to the nearest free spot.")]
        public bool findClearSpot = true;
        [Tooltip("Layers that count as obstacles (ground, walls, other vehicles). Default: everything.")]
        public LayerMask obstacleLayers = ~0;
        [Tooltip("Distance between search rings (meters).")]
        public float searchStep = 1.5f;
        [Tooltip("How far from the car to search (meters).")]
        public float searchMaxRadius = 12f;
        [Tooltip("Extra free space required around the car (meters).")]
        public float clearance = 0.25f;
        [Tooltip("Obstacles lower than this above the wheels' bottom are ignored (curbs, small bumps).")]
        public float ignoreBottomHeight = 0.35f;
        [Tooltip("The car is dropped from this height above the ground (meters).")]
        public float dropMargin = 0.15f;

        [Header("Auto: flipped")]
        public bool recoverWhenFlipped = true;
        [Tooltip("Car up-direction dot world-up below this counts as flipped. 1 = upright, 0 = on its side, -1 = upside down.")]
        [Range(-0.5f, 0.8f)] public float flippedThreshold = 0.25f;
        [Tooltip("Seconds the car must stay flipped before it is recovered.")]
        public float flippedTime = 2f;
        [Tooltip("Only count as flipped when slower than this (m/s), so a rolling car is not reset mid-roll.")]
        public float flippedMaxSpeed = 2f;

        [Header("Auto: stuck")]
        public bool recoverWhenStuck = true;
        [Tooltip("Slower than this (m/s) while the player is accelerating / reversing counts as stuck.")]
        public float stuckSpeed = 0.5f;
        [Tooltip("Seconds the car must be stuck before the escape starts.")]
        public float stuckDelay = 1.2f;
        [Tooltip("ReverseOut = drive out backwards for a moment (default). NearbyClearSpot = teleport to a free spot. LastSafePoint = teleport back along the road.")]
        public StuckRecoverMode stuckMode = StuckRecoverMode.ReverseOut;

        [Header("Reverse Out")]
        [Tooltip("Maximum time the script drives the car out (seconds).")]
        public float reverseTime = 1.8f;
        [Tooltip("Stops as soon as the car has moved this far (meters).")]
        public float reverseDistance = 4f;
        [Tooltip("How much to steer while backing out so the nose swings toward the free side (0 = straight back).")]
        [Range(0f, 1f)] public float reverseSteer = 1f;
        [Tooltip("If Reverse Out did not free the car after this many tries in a row, teleport to a clear spot instead.")]
        public int maxReverseAttempts = 2;
        [Tooltip("Failed tries are forgotten after this many seconds.")]
        public float attemptMemoryTime = 8f;

        [Header("Last Safe Point (stuck mode)")]
        [Tooltip("How often a safe position is remembered (seconds).")]
        public float safePointInterval = 1f;
        [Tooltip("How many positions are remembered. The oldest one is used.")]
        public int safePointCount = 4;

        [Header("Camera")]
        public VehicleCameraRig cameraRig;
        [Tooltip("Snap the camera behind the car after a teleport, so it does not swing across the map.")]
        public bool snapCameraOnRecover = true;

        // ---- set by VehicleInput (or an AI) every frame: the PLAYER's throttle -1..1
        public float DriveInput { get; set; }

        /// <summary>0..1 progress of an automatic recovery (for a HUD "Resetting..." bar).</summary>
        public float AutoRecoverProgress { get; private set; }

        /// <summary>True while the script is driving the car out of a stuck spot.</summary>
        public bool IsReversingOut => reversing;

        public event Action Recovered;

        Rigidbody rb;
        VehicleController vehicle;

        float flippedTimer, stuckTimer, cooldownTimer, safeTimer;
        readonly Queue<Pose> safePoses = new Queue<Pose>();

        // reverse-out state
        bool reversing;
        float reverseTimer, reverseThrottle, reverseSteerValue, lastSide = 1f, attemptTimer;
        int attempts;
        Vector3 reverseStart;

        // car size in the vehicle's own frame (meters, scale-free)
        Bounds localBounds;
        Vector3 testCenterLocal, testHalfExtents;

        readonly RaycastHit[] rayHits = new RaycastHit[16];
        readonly Collider[] overlaps = new Collider[32];
        static readonly float[] SearchAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f }; // 0 = straight behind the car

        Vector3 Velocity
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return rb.linearVelocity;
#else
                return rb.velocity;
#endif
            }
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            vehicle = GetComponent<VehicleController>();
            RecalculateSize();
        }

        void Start()
        {
            if (cameraRig == null) cameraRig = FindFirstObjectByType<VehicleCameraRig>();
        }

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// While this returns true, use these values instead of the player's input (VehicleInput does this).
        /// </summary>
        public bool TryGetOverride(out float throttle, out float steer)
        {
            throttle = reverseThrottle;
            steer = reverseSteerValue;
            return reversing;
        }

        /// <summary>Put the car upright. Stays in place if that spot is free, otherwise moves to the nearest free spot.</summary>
        public void Recover()
        {
            Quaternion rot = FlatRotation();

            if (findClearSpot && TryFindClearSpot(transform.position, transform.position.y, rot, out Vector3 spot))
            {
                Teleport(spot, rot);
                return;
            }

            Teleport(transform.position + Vector3.up * liftHeight, rot);
        }

        /// <summary>Put the car back where it was driving a few seconds ago (falls back to Recover()).</summary>
        public void RecoverToSafePoint()
        {
            if (safePoses.Count == 0) { Recover(); return; }

            Pose p = safePoses.Peek();
            safePoses.Clear();

            if (findClearSpot && TryFindClearSpot(p.position, p.position.y, p.rotation, out Vector3 spot))
                Teleport(spot, p.rotation);
            else
                Teleport(p.position + Vector3.up * 0.5f, p.rotation);
        }

        /// <summary>Re-measure the car (call after changing its model at runtime).</summary>
        public void RecalculateSize()
        {
            // measure in the vehicle's own frame, ignoring scale, so it works at any rotation
            Matrix4x4 toLocal = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one).inverse;

            bool has = false;
            Bounds b = new Bounds();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

                Bounds lb = r.localBounds;
                Matrix4x4 m = toLocal * r.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = lb.center + Vector3.Scale(lb.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
                    else b.Encapsulate(p);
                }
            }

            if (!has) b = new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(2f, 1.6f, 4f));
            localBounds = b;

            float lowY = b.min.y + ignoreBottomHeight;
            float highY = Mathf.Max(lowY + 0.2f, b.max.y);
            testCenterLocal = new Vector3(b.center.x, (lowY + highY) * 0.5f, b.center.z);
            testHalfExtents = new Vector3(b.extents.x + clearance, (highY - lowY) * 0.5f, b.extents.z + clearance);
        }

        // ------------------------------------------------------------------ auto detection

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (reversing)
            {
                UpdateReverseOut(dt);
                return;
            }

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= dt;
                flippedTimer = stuckTimer = 0f;
                AutoRecoverProgress = 0f;
                return;
            }

            float speed = Velocity.magnitude;
            float upDot = Vector3.Dot(transform.up, Vector3.up);

            // forget old failed escape attempts
            if (attempts > 0)
            {
                attemptTimer += dt;
                if (attemptTimer > attemptMemoryTime || speed > 4f) attempts = 0;
            }

            // flipped / on its side and settled
            if (recoverWhenFlipped && upDot < flippedThreshold && speed < flippedMaxSpeed) flippedTimer += dt;
            else flippedTimer = 0f;

            // upright but not moving while the player is trying to drive
            bool trying = Mathf.Abs(DriveInput) > 0.5f;
            if (recoverWhenStuck && upDot >= flippedThreshold && trying && speed < stuckSpeed) stuckTimer += dt;
            else stuckTimer = 0f;

            if (stuckMode == StuckRecoverMode.LastSafePoint) RecordSafePoint(dt, upDot, speed);

            float flippedProgress = flippedTime > 0f ? flippedTimer / flippedTime : 0f;
            float stuckProgress = stuckDelay > 0f ? stuckTimer / stuckDelay : 0f;
            AutoRecoverProgress = Mathf.Clamp01(Mathf.Max(flippedProgress, stuckProgress));

            if (recoverWhenFlipped && flippedTimer >= flippedTime)
            {
                Recover();
            }
            else if (recoverWhenStuck && stuckTimer >= stuckDelay)
            {
                switch (stuckMode)
                {
                    case StuckRecoverMode.ReverseOut: StartReverseOut(); break;
                    case StuckRecoverMode.LastSafePoint: RecoverToSafePoint(); break;
                    default: Recover(); break;
                }
            }
        }

        // ------------------------------------------------------------------ reverse out

        void StartReverseOut()
        {
            attempts++;
            attemptTimer = 0f;

            // tried a few times already and still stuck -> teleport to a free spot
            if (attempts > Mathf.Max(1, maxReverseAttempts))
            {
                attempts = 0;
                Recover();
                return;
            }

            // back out the opposite way to what the player was pushing
            float dir = DriveInput >= 0f ? -1f : 1f;
            reverseThrottle = dir;
            reverseSteerValue = dir < 0f ? -FreeSide() * reverseSteer : 0f; // reversing flips the steering effect

            reverseStart = transform.position;
            reverseTimer = 0f;
            reversing = true;
            stuckTimer = 0f;
            AutoRecoverProgress = 0f;
        }

        void UpdateReverseOut(float dt)
        {
            reverseTimer += dt;

            float moved = Vector3.ProjectOnPlane(transform.position - reverseStart, Vector3.up).magnitude;
            bool flipped = Vector3.Dot(transform.up, Vector3.up) < flippedThreshold;

            if (reverseTimer >= reverseTime || moved >= reverseDistance || flipped)
                EndReverseOut();
        }

        void EndReverseOut()
        {
            reversing = false;
            stuckTimer = 0f;
            cooldownTimer = 0.5f; // give the player the controls back before checking again
        }

        /// <summary>+1 if the right side ahead of the car is more open, -1 for the left. Alternates on a tie.</summary>
        float FreeSide()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) return lastSide;
            fwd.Normalize();

            float right = ClearDistance(origin, Quaternion.AngleAxis(45f, Vector3.up) * fwd);
            float left = ClearDistance(origin, Quaternion.AngleAxis(-45f, Vector3.up) * fwd);

            if (Mathf.Abs(right - left) < 0.5f)
            {
                lastSide = -lastSide;
                return lastSide;
            }
            return right > left ? 1f : -1f;
        }

        float ClearDistance(Vector3 origin, Vector3 dir)
        {
            const float max = 8f;
            int n = Physics.RaycastNonAlloc(origin, dir, rayHits, max, obstacleLayers, QueryTriggerInteraction.Ignore);

            float best = max;
            for (int i = 0; i < n; i++)
            {
                var h = rayHits[i];
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance < best) best = h.distance;
            }
            return best;
        }

        // ------------------------------------------------------------------ safe points

        void RecordSafePoint(float dt, float upDot, float speed)
        {
            safeTimer += dt;
            if (safeTimer < safePointInterval) return;
            safeTimer = 0f;

            // only remember good spots: upright, on the ground, actually driving
            if (upDot < 0.8f || speed < 3f || !AnyWheelGrounded()) return;

            safePoses.Enqueue(new Pose(transform.position, FlatRotation()));
            while (safePoses.Count > Mathf.Max(1, safePointCount)) safePoses.Dequeue();
        }

        bool AnyWheelGrounded()
        {
            foreach (var w in vehicle.wheels)
                if (w != null && w.collider != null && w.collider.isGrounded) return true;
            return false;
        }

        // ------------------------------------------------------------------ clear spot search

        /// <summary>
        /// Searches rings around "around" (straight behind the car first, then the sides, then ahead)
        /// for flat ground where the car's box does not touch anything.
        /// </summary>
        bool TryFindClearSpot(Vector3 around, float baseY, Quaternion rot, out Vector3 origin)
        {
            Vector3 back = rot * Vector3.back;
            int rings = Mathf.Max(0, Mathf.CeilToInt(searchMaxRadius / Mathf.Max(0.1f, searchStep)));

            for (int ring = 0; ring <= rings; ring++)
            {
                float radius = ring * searchStep;
                int dirs = ring == 0 ? 1 : SearchAngles.Length;

                for (int d = 0; d < dirs; d++)
                {
                    Vector3 dir = Quaternion.AngleAxis(SearchAngles[d], Vector3.up) * back;
                    Vector3 xz = around + dir * radius;

                    if (!TryGetGround(xz, baseY, out float groundY)) continue;

                    Vector3 candidate = new Vector3(xz.x, groundY - localBounds.min.y + dropMargin, xz.z);
                    if (IsClear(candidate, rot))
                    {
                        origin = candidate;
                        return true;
                    }
                }
            }

            origin = default;
            return false;
        }

        /// <summary>Flat ground under xz, close to the car's height (so the roof of another vehicle does not count).</summary>
        bool TryGetGround(Vector3 xz, float baseY, out float groundY)
        {
            groundY = 0f;
            Vector3 from = new Vector3(xz.x, baseY + 2f, xz.z);
            int n = Physics.RaycastNonAlloc(from, Vector3.down, rayHits, 8f, obstacleLayers, QueryTriggerInteraction.Ignore);

            bool found = false;
            RaycastHit best = default;
            for (int i = 0; i < n; i++)
            {
                var h = rayHits[i];
                if (h.collider.transform.IsChildOf(transform)) continue; // the car itself
                if (!found || h.distance < best.distance) { best = h; found = true; }
            }

            if (!found) return false;
            if (best.normal.y < 0.7f) return false;           // too steep (wall, roof edge)
            if (best.point.y > baseY + 1.2f) return false;    // standing on top of something tall

            groundY = best.point.y;
            return true;
        }

        bool IsClear(Vector3 origin, Quaternion rot)
        {
            Vector3 center = origin + rot * testCenterLocal;
            int n = Physics.OverlapBoxNonAlloc(center, testHalfExtents, overlaps, rot, obstacleLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < n; i++)
                if (!overlaps[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        // ------------------------------------------------------------------ helpers

        void Teleport(Vector3 position, Quaternion rotation)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;

            transform.SetPositionAndRotation(position, rotation);
            rb.position = position;
            rb.rotation = rotation;
            Physics.SyncTransforms();

            reversing = false;
            flippedTimer = stuckTimer = 0f;
            cooldownTimer = cooldown;
            AutoRecoverProgress = 0f;

            if (snapCameraOnRecover && cameraRig != null) cameraRig.Snap();
            Recovered?.Invoke();
        }

        /// <summary>Same heading as the car, perfectly level.</summary>
        Quaternion FlatRotation()
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.05f) fwd = Vector3.ProjectOnPlane(transform.up, Vector3.up); // standing on its nose / tail
            if (fwd.sqrMagnitude < 0.05f) fwd = Vector3.forward;
            return Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        /// <summary>Select the vehicle in Play mode to see the box that must be free (green).</summary>
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            Quaternion rot = FlatRotation();
            Gizmos.color = Color.green;
            Gizmos.matrix = Matrix4x4.TRS(transform.position + rot * testCenterLocal, rot, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, testHalfExtents * 2f);
        }
    }
}
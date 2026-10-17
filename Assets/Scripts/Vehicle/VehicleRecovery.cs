using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Gets the vehicle out of trouble. Two SEPARATE paths:
    ///   1. Manual : RecoverManual() (LocalPlayerRig calls it for the R key, gamepad Y/North, and the mobile RESET button).
    ///               A repair kit. In a safe place it only HEALS (and is not used when health is full). When the car is
    ///               stuck, tipped over or not in a safe place it also teleports back to a SAFE PLACE it drove through a few
    ///               seconds ago: upright and on the ground (no lift, no hop, no move backward). Safe places are
    ///               remembered all the time while driving.
    ///   2. Auto   : - flipped / on its side for a while  -> RecoverAuto(): upright at the nearest clear spot, on the ground.
    ///               - stuck while the player is driving  -> "Reverse Out": the script drives the car backwards
    ///                 (or forwards if it was reversing) for a moment while steering away from the obstacle,
    ///                 like arcade racing games. If that fails a couple of times it falls back to RecoverAuto().
    ///
    /// Setup: add to the vehicle (next to VehicleController). VehicleDriver hands control over during Reverse Out.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleController))]
    public class VehicleRecovery : MonoBehaviour
    {
        public enum StuckRecoverMode { ReverseOut, NearbyClearSpot, LastSafePoint }

        [Header("Manual Recovery (button / key)")]
        [Tooltip("Minimum time between two recoveries (seconds).")]
        public float cooldown = 1.5f;
        [Tooltip("Manual recovery never uses a safe place newer than this (seconds), so you are not put back into the same trouble.")]
        public float safePointMinAge = 2f;

        [Header("Manual Reset Limit")]
        [Tooltip("Players may only use the manual reset a limited number of times per match. Auto recovery is never limited or counted.")]
        public bool limitManualResets = true;
        [Tooltip("Most manual recoveries (repair charges) the player can carry.")]
        public int maxManualResets = 3;
        [Tooltip("Charges at the start of a match. Collected repair kits add more, up to the maximum above.")]
        public int startingManualResets = 3;

        [Header("Find Clear Spot (used by auto recovery only)")]
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
        [Tooltip("The car is placed this far above the ground (meters). Keep it small so a recovery never looks like a hop.")]
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

        [Header("Repair kit: heal only, or teleport too?")]
        [Tooltip("The kit only teleports when the vehicle is NOT in a safe place: tipped over, stuck, or somewhere it should not be. Otherwise it only heals.")]
        [Range(-0.5f, 0.95f)] public float repairUprightDot = 0.6f;
        [Tooltip("Pushing against something for this long (seconds) while trying to drive counts as stuck.")]
        public float repairStuckTime = 0.6f;
        [Tooltip("In the air for this long (seconds) counts as not safe (fell off the map).")]
        public float unsafeAirTime = 1.5f;

        /// <summary>
        /// Optional: the road system sets this so a position can be declared unsafe (for example off the road). Return false
        /// for unsafe. Leave null to ignore.
        /// </summary>
        public Func<Vector3, bool> isPositionSafe;

        [Header("Safe Places (manual recovery and the Last Safe Point stuck mode)")]
        [Tooltip("How often a safe place is remembered while driving normally (seconds).")]
        public float safePointInterval = 1f;
        [Tooltip("How far back in time safe places are kept (seconds).")]
        public float safeHistorySeconds = 12f;

        [Header("Camera")]
        [Tooltip("Assigned by LocalPlayerRig.Attach() for the local player's car. Leave empty for bots / remote cars.")]
        public VehicleCameraRig cameraRig;
        [Tooltip("Snap the camera behind the car after a teleport, so it does not swing across the map.")]
        public bool snapCameraOnRecover = true;

        // ---- set by VehicleDriver every frame: the driver's own throttle -1..1 (player, bot or network)
        public float DriveInput { get; set; }

        /// <summary>0..1 progress of an automatic recovery (for a HUD "Resetting..." bar).</summary>
        public float AutoRecoverProgress { get; private set; }

        /// <summary>True while the script is driving the car out of a stuck spot.</summary>
        public bool IsReversingOut => reversing;

        public event Action Recovered;

        /// <summary>
        /// Fired after a repair kit was used. The bool says whether the vehicle was also teleported (it was stuck or not
        /// in a safe place) or only healed. VehicleHealth restores health on it.
        /// </summary>
        public event Action<bool> ManualRecovered;

        /// <summary>Manual resets still available this match.</summary>
        public int ManualResetsLeft { get; private set; }

        /// <summary>False when the limit is on and all manual resets are used up.</summary>
        public bool CanManualReset => !limitManualResets || ManualResetsLeft > 0;

        /// <summary>Fired when the player presses reset but has none left (for a sound / UI shake).</summary>
        public event Action ManualResetDenied;

        /// <summary>Give the manual resets back. Call this when a new match starts.</summary>
        public void ResetManualUses() => ManualResetsLeft = Mathf.Clamp(startingManualResets, 0, Mathf.Max(0, maxManualResets));

        /// <summary>A repair kit was collected: adds charges, up to the maximum. Returns false if already full.</summary>
        public bool AddManualUse(int amount = 1)
        {
            int max = Mathf.Max(0, maxManualResets);
            if (ManualResetsLeft >= max) return false;
            ManualResetsLeft = Mathf.Min(max, ManualResetsLeft + Mathf.Max(0, amount));
            return true;
        }

        Rigidbody rb;
        VehicleController vehicle;

        float flippedTimer, stuckTimer, cooldownTimer, safeTimer, lastDriveSign;
        float blockedTimer, airTimer;
        VehicleHealth health;
        VehicleDriver driver;
        struct SafePlace { public Vector3 position; public Quaternion rotation; public float time; }
        readonly List<SafePlace> safePlaces = new List<SafePlace>();

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
            ResetManualUses();
            RecalculateSize();
        }

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// While this returns true, use these values instead of the driver's input (VehicleDriver does this).
        /// </summary>
        public bool TryGetOverride(out float throttle, out float steer)
        {
            throttle = reverseThrottle;
            steer = reverseSteerValue;
            return reversing;
        }

        /// <summary>
        /// MANUAL recovery (R key / RESET button): teleports the car back to a safe place it drove through a few
        /// seconds ago, upright and on the ground. Uses up one repair charge. Returns false (and does nothing)
        /// when none are left.
        /// </summary>
        public bool RecoverManual()
        {
            if (driver == null) TryGetComponent(out driver);
            if (driver != null && !driver.inputEnabled) return false; // countdown, match over, destroyed

            bool safe = IsInSafePlace();
            if (health == null) TryGetComponent(out health);
            bool hurt = health != null && health.Health01 < 0.999f;

            // already safe and nothing to heal: a kit would be wasted
            if (safe && !hurt)
            {
                ManualResetDenied?.Invoke();
                return false;
            }

            if (limitManualResets)
            {
                if (ManualResetsLeft <= 0)
                {
                    ManualResetDenied?.Invoke();
                    return false;
                }
                ManualResetsLeft--;
            }

            bool teleport = !safe;
            if (teleport) TeleportToLastSafePoint();
            ManualRecovered?.Invoke(teleport);
            return true;
        }

        /// <summary>
        /// True when the vehicle is fine where it is: upright, not stuck against something, not stranded in the air,
        /// and (if the road system says so) on a valid position. A repair kit then only heals.
        /// </summary>
        public bool IsInSafePlace()
        {
            if (reversing) return false;                                                   // being driven out of a stuck spot
            if (Vector3.Dot(transform.up, Vector3.up) < repairUprightDot) return false;   // tipped over
            if (blockedTimer >= repairStuckTime) return false;                             // stuck against something
            if (airTimer >= unsafeAirTime) return false;                                   // stranded in the air
            if (isPositionSafe != null && !isPositionSafe(transform.position)) return false;
            return true;
        }

        /// <summary>
        /// Puts the car on the newest remembered safe place that is old enough and free. If there is none (for example
        /// right after spawning) the car is stood upright where it is. No charge is used, so a destroyed vehicle can
        /// use this to respawn too.
        /// </summary>
        public void TeleportToLastSafePoint()
        {
            float now = Time.time;

            // newest first, skipping places that are too recent (that is where the trouble started)
            for (int i = safePlaces.Count - 1; i >= 0; i--)
            {
                SafePlace p = safePlaces[i];
                if (now - p.time < safePointMinAge) continue;

                Vector3 spot = default;
                if (IsClear(p.position, p.rotation)) spot = p.position;
                else if (!findClearSpot || !TryFindClearSpot(p.position, p.position.y, p.rotation, out spot)) continue;

                // forget the places after this one: they led into the trouble
                safePlaces.RemoveRange(i + 1, safePlaces.Count - (i + 1));
                Teleport(spot, p.rotation);
                return;
            }

            // nothing remembered yet: stand upright right here
            PlaceOnGround(transform.position, FlatRotation());
        }

        /// <summary>Kept so older code still compiles. Same as RecoverManual().</summary>
        public void Recover() => RecoverManual();

        /// <summary>
        /// AUTO recovery: upright at the nearest clear spot. If there is none, back to a remembered safe place, or
        /// upright on the ground where it is. It never lifts the car.
        /// </summary>
        public void RecoverAuto()
        {
            Quaternion rot = FlatRotation();

            if (findClearSpot && TryFindClearSpot(transform.position, transform.position.y, rot, out Vector3 spot))
            {
                Teleport(spot, rot);
                return;
            }

            TeleportToLastSafePoint();
        }

        /// <summary>Put the car back where it was driving a few seconds ago (falls back to RecoverAuto()).</summary>
        public void RecoverToSafePoint()
        {
            if (safePlaces.Count == 0) { RecoverAuto(); return; }
            TeleportToLastSafePoint();
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

            // for the repair kit: is the vehicle blocked or in the air? (counted all the time, also during escapes)
            blockedTimer = Mathf.Abs(DriveInput) > 0.5f && Velocity.magnitude < stuckSpeed ? blockedTimer + dt : 0f;
            airTimer = AnyWheelGrounded() ? 0f : airTimer + dt;

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

            // Player switched between forward and reverse (e.g. auto-accelerate -> holding BRAKE to back up):
            // start counting again, so time spent pushing forward never triggers a reverse-out against the new direction.
            float driveSign = trying ? Mathf.Sign(DriveInput) : 0f;
            if (driveSign != lastDriveSign) { stuckTimer = 0f; lastDriveSign = driveSign; }
            if (recoverWhenStuck && upDot >= flippedThreshold && trying && speed < stuckSpeed) stuckTimer += dt;
            else stuckTimer = 0f;

            RecordSafePoint(dt, upDot, speed);

            float flippedProgress = flippedTime > 0f ? flippedTimer / flippedTime : 0f;
            float stuckProgress = stuckDelay > 0f ? stuckTimer / stuckDelay : 0f;
            AutoRecoverProgress = Mathf.Clamp01(Mathf.Max(flippedProgress, stuckProgress));

            if (recoverWhenFlipped && flippedTimer >= flippedTime)
            {
                RecoverAuto();
            }
            else if (recoverWhenStuck && stuckTimer >= stuckDelay)
            {
                switch (stuckMode)
                {
                    case StuckRecoverMode.ReverseOut: StartReverseOut(); break;
                    case StuckRecoverMode.LastSafePoint: RecoverToSafePoint(); break;
                    default: RecoverAuto(); break;
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
                RecoverAuto();
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

            // only remember good places: upright, on the ground, driving forward at a normal pace
            if (upDot < 0.8f || speed < 3f || !AnyWheelGrounded()) return;
            if (Vector3.Dot(Velocity, transform.forward) < 2f) return;

            // face the direction of travel (the road direction), not a sideways drift
            Vector3 heading = Vector3.ProjectOnPlane(Velocity, Vector3.up);
            Quaternion rot = heading.sqrMagnitude > 1f
                ? Quaternion.LookRotation(heading.normalized, Vector3.up)
                : FlatRotation();

            float now = Time.time;
            safePlaces.Add(new SafePlace { position = transform.position, rotation = rot, time = now });

            // forget places that are too old
            float oldest = now - Mathf.Max(safePointMinAge + 1f, safeHistorySeconds);
            while (safePlaces.Count > 0 && safePlaces[0].time < oldest) safePlaces.RemoveAt(0);
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

        /// <summary>Stands the car upright on the ground below "position": only the small Drop Margin, never a lift.</summary>
        void PlaceOnGround(Vector3 position, Quaternion rotation)
        {
            float y = position.y;
            if (TryGetGround(position, position.y, out float groundY))
                y = groundY - localBounds.min.y + dropMargin;

            Teleport(new Vector3(position.x, y, position.z), rotation);
        }

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
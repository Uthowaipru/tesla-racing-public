using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>One wheel's role on the vehicle. Add as many as you need (2, 3, 4, 6...).</summary>
    [Serializable]
    public class WheelSetup
    {
        public string label = "Wheel";
        public WheelCollider collider;
        [Tooltip("The visible mesh. Must NOT be a child of the WheelCollider.")]
        public Transform visual;

        [Tooltip("Wheels sharing an axle index get anti-roll if there are exactly two (left + right).")]
        public int axleIndex;

        public bool steer;
        public bool drive;
        public bool brake;
        public bool handbrake;

        [NonSerialized] public WheelFrictionCurve baseForward;
        [NonSerialized] public WheelFrictionCurve baseSideways;
        [NonSerialized] public Vector3 localPos;
    }

    /// <summary>
    /// Universal wheeled-vehicle controller (2 / 3 / 4+ wheels).
    /// Everything tunable comes from VehicleStats, so upgrades never touch this script.
    /// It only receives a DriveInput (see VehicleDriver) and does not know who is driving.
    ///
    /// Three-wheeler setup:  front wheel  -> steer + brake
    ///                       rear L and R -> drive + brake + handbrake (same axleIndex)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleStats))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Wheels")]
        public List<WheelSetup> wheels = new List<WheelSetup>();

        [Header("Body")]
        [Tooltip("Local-space center of mass. Lower = more stable (important for 3-wheelers).")]
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.35f, 0f);
        [Tooltip("Extra downward force per m/s of speed.")]
        public float downforce = 15f;

        [Header("Steering")]
        [Tooltip("X = speed / max speed, Y = steering multiplier. Less steering at high speed.")]
        public AnimationCurve steerBySpeed = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.5f, 0.55f), new Keyframe(1f, 0.3f));
        [Tooltip("How fast the steering moves toward the input (units/sec, 1 = full lock).")]
        public float steerSpeed = 4f;
        [Tooltip("How fast the steering centers when you let go.")]
        public float steerReturnSpeed = 6f;
        public bool useAckermann = true;

        [Header("Drive")]
        [Tooltip("X = speed / limit, Y = torque multiplier. Fades power out near top speed.")]
        public AnimationCurve torqueCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f));
        public float throttleRiseSpeed = 3f;
        public float throttleFallSpeed = 6f;
        [Range(0.05f, 1f)] public float reverseSpeedFactor = 0.35f;
        [Tooltip("Gentle braking when no throttle (0-1 of brake torque).")]
        [Range(0f, 0.5f)] public float engineBrake = 0.06f;
        [Tooltip("Below this speed with no input, the vehicle is held still (no rolling on slopes).")]
        public float holdSpeed = 0.4f;

        [Header("Handbrake / Drift")]
        public float handbrakeTorque = 4000f;
        [Tooltip("Sideways grip multiplier on handbrake wheels while handbrake is held.")]
        [Range(0.1f, 1f)] public float handbrakeSlideGrip = 0.55f;
        public float handbrakeResponse = 8f;

        [Header("Stability")]
        [Tooltip("0 = off. Needed for 2-wheelers (keeps the bike upright). Try 15-30 for bikes.")]
        public float uprightAssist = 0f;
        public float uprightDamping = 3f;

        // --- Registry of active vehicles (so other scripts never have to search the scene) ---
        static readonly List<VehicleController> active = new List<VehicleController>();

        /// <summary>Every enabled vehicle in the scene (players, bots and remote cars).</summary>
        public static IReadOnlyList<VehicleController> All => active;

        /// <summary>Raised when a vehicle is enabled / disabled (e.g. when a spawner creates or removes one).</summary>
        public static event Action<VehicleController> VehicleEnabled;
        public static event Action<VehicleController> VehicleDisabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
            VehicleEnabled = null;
            VehicleDisabled = null;
        }

        // --- Runtime info (HUD, audio, etc.) ---
        public float ForwardSpeed { get; private set; }          // m/s, + forward
        public float SpeedKmh => Mathf.Abs(ForwardSpeed) * 3.6f;
        public VehicleStats Stats => stats;
        /// <summary>True while at least one wheel touches the ground.</summary>
        public bool IsGrounded { get; private set; }
        /// <summary>0..1, how far the (eased) handbrake is applied. Drifting is detected with it.</summary>
        public float HandbrakeAmount => handbrakeAmount;

        Rigidbody rb;
        VehicleStats stats;

        // input (raw)
        float throttleInput, steerInput;
        bool handbrakeInput;

        // smoothed state
        float motorSmoothed, steerSmoothed, handbrakeAmount, lastHandbrakeAmount;

        // cached
        float wheelBase, gripCache = 1f;
        int driveCount, brakeCount;
        struct AxlePair { public WheelSetup left, right; }
        readonly List<AxlePair> axlePairs = new List<AxlePair>();

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

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            stats = GetComponent<VehicleStats>();

            rb.centerOfMass = centerOfMassOffset;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            RebuildWheels();
        }

        void OnEnable()
        {
            if (stats == null) stats = GetComponent<VehicleStats>();
            stats.OnStatsChanged += ApplyStats;
            ApplyStats();

            if (!active.Contains(this)) active.Add(this);
            VehicleEnabled?.Invoke(this);
        }

        void OnDisable()
        {
            if (stats != null) stats.OnStatsChanged -= ApplyStats;

            active.Remove(this);
            VehicleDisabled?.Invoke(this);
        }

        /// <summary>Call again if you add/remove/swap wheels at runtime.</summary>
        public void RebuildWheels()
        {
            driveCount = brakeCount = 0;
            axlePairs.Clear();

            var steerZ = 0f; int steerN = 0;
            var fixedZ = 0f; int fixedN = 0;
            var byAxle = new Dictionary<int, List<WheelSetup>>();

            foreach (var w in wheels)
            {
                if (w == null || w.collider == null) continue;

                w.baseForward = w.collider.forwardFriction;
                w.baseSideways = w.collider.sidewaysFriction;
                w.localPos = transform.InverseTransformPoint(w.collider.transform.position);
                w.collider.ConfigureVehicleSubsteps(5f, 12, 15); // smoother at low speed

                if (w.drive) driveCount++;
                if (w.brake) brakeCount++;

                if (w.steer) { steerZ += w.localPos.z; steerN++; }
                else { fixedZ += w.localPos.z; fixedN++; }

                if (!byAxle.TryGetValue(w.axleIndex, out var list))
                    byAxle[w.axleIndex] = list = new List<WheelSetup>();
                list.Add(w);
            }

            wheelBase = (steerN > 0 && fixedN > 0)
                ? Mathf.Abs(steerZ / steerN - fixedZ / fixedN)
                : 0f;

            foreach (var kv in byAxle)
            {
                if (kv.Value.Count != 2) continue; // anti-roll only for left+right pairs
                var a = kv.Value[0]; var b = kv.Value[1];
                axlePairs.Add(a.localPos.x <= b.localPos.x
                    ? new AxlePair { left = a, right = b }
                    : new AxlePair { left = b, right = a });
            }
        }

        /// <summary>Pushes current stat values (suspension + grip) into the WheelColliders.</summary>
        void ApplyStats()
        {
            if (stats == null) return;

            float spring = stats.Get(VehicleStat.SuspensionSpring);
            float damper = stats.Get(VehicleStat.SuspensionDamper);
            float travel = Mathf.Max(0.05f, stats.Get(VehicleStat.SuspensionTravel));
            gripCache = stats.Get(VehicleStat.TireGrip);

            foreach (var w in wheels)
            {
                if (w == null || w.collider == null) continue;

                var js = w.collider.suspensionSpring;
                js.spring = spring;
                js.damper = damper;
                w.collider.suspensionSpring = js;
                w.collider.suspensionDistance = travel;

                ApplyGrip(w, 0f);
            }
        }

        void ApplyGrip(WheelSetup w, float slide)
        {
            var f = w.baseForward;
            f.stiffness = w.baseForward.stiffness * gripCache;

            var s = w.baseSideways;
            float slideMult = w.handbrake ? Mathf.Lerp(1f, handbrakeSlideGrip, slide) : 1f;
            s.stiffness = w.baseSideways.stiffness * gripCache * slideMult;

            w.collider.forwardFriction = f;
            w.collider.sidewaysFriction = s;
        }

        // ------------------------------------------------------------------ input

        /// <param name="throttle">-1..1. Opposite to current motion = brake, then reverse.</param>
        /// <param name="steer">-1..1</param>
        public void SetInput(float throttle, float steer, bool handbrake)
        {
            throttleInput = Mathf.Clamp(throttle, -1f, 1f);
            steerInput = Mathf.Clamp(steer, -1f, 1f);
            handbrakeInput = handbrake;
        }

        /// <summary>Same as above, taking a DriveInput (what VehicleDriver sends every frame).</summary>
        public void SetInput(DriveInput input) => SetInput(input.throttle, input.steer, input.handbrake);

        // ------------------------------------------------------------------ simulation

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            ForwardSpeed = Vector3.Dot(Velocity, transform.forward);

            bool grounded = false;
            for (int i = 0; i < wheels.Count; i++)
            {
                var gw = wheels[i];
                if (gw != null && gw.collider != null && gw.collider.isGrounded) { grounded = true; break; }
            }
            IsGrounded = grounded;

            float maxSpeed = Mathf.Max(1f, stats.Get(VehicleStat.MaxSpeed) / 3.6f);
            float speedRatio = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / maxSpeed);

            // ---- throttle / brake decision
            float targetMotor = 0f;
            float footBrake = 0f;
            bool movingFwd = ForwardSpeed > 0.5f;
            bool movingBack = ForwardSpeed < -0.5f;

            if (throttleInput > 0.01f)
            {
                if (movingBack) footBrake = throttleInput;
                else targetMotor = throttleInput;
            }
            else if (throttleInput < -0.01f)
            {
                if (movingFwd) footBrake = -throttleInput;
                else targetMotor = throttleInput;
            }
            else
            {
                footBrake = Mathf.Abs(ForwardSpeed) < holdSpeed ? 1f : engineBrake;
            }

            float motorRate = Mathf.Abs(targetMotor) > Mathf.Abs(motorSmoothed) ? throttleRiseSpeed : throttleFallSpeed;
            motorSmoothed = Mathf.MoveTowards(motorSmoothed, targetMotor, motorRate * dt);

            // ---- steering (smoothed, speed sensitive)
            float steerRate = Mathf.Approximately(steerInput, 0f) ? steerReturnSpeed : steerSpeed;
            steerSmoothed = Mathf.MoveTowards(steerSmoothed, steerInput, steerRate * dt);
            float steerAngle = stats.Get(VehicleStat.SteerAngle) * steerBySpeed.Evaluate(speedRatio) * steerSmoothed;

            // ---- handbrake (smoothed so the slide builds up instead of snapping)
            handbrakeAmount = Mathf.MoveTowards(handbrakeAmount, handbrakeInput ? 1f : 0f, handbrakeResponse * dt);

            // ---- torque
            float limit = motorSmoothed >= 0f ? maxSpeed : maxSpeed * reverseSpeedFactor;
            float along = ForwardSpeed * Mathf.Sign(motorSmoothed == 0f ? 1f : motorSmoothed);
            float torqueTotal = 0f;
            if (along < limit)
            {
                float curve = torqueCurve.Evaluate(Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / limit));
                torqueTotal = stats.Get(VehicleStat.MotorTorque) * motorSmoothed * curve;
            }

            float torquePerWheel = driveCount > 0 ? torqueTotal / driveCount : 0f;
            float brakePerWheel = brakeCount > 0 ? stats.Get(VehicleStat.BrakeTorque) * footBrake / brakeCount : 0f;
            bool handbrakeChanging = handbrakeAmount > 0.001f || lastHandbrakeAmount > 0.001f;

            foreach (var w in wheels)
            {
                if (w == null || w.collider == null) continue;
                var c = w.collider;

                c.steerAngle = w.steer ? AckermannAngle(w, steerAngle) : 0f;
                c.motorTorque = w.drive ? torquePerWheel : 0f;
                c.brakeTorque = (w.brake ? brakePerWheel : 0f)
                              + (w.handbrake ? handbrakeAmount * handbrakeTorque : 0f);

                if (w.handbrake && handbrakeChanging) ApplyGrip(w, handbrakeAmount);
            }
            lastHandbrakeAmount = handbrakeAmount;

            ApplyAntiRoll();

            // ---- downforce
            rb.AddForce(-transform.up * (downforce * Mathf.Abs(ForwardSpeed)));

            // ---- upright assist (2-wheelers)
            if (uprightAssist > 0f)
            {
                Vector3 rollAxis = Vector3.Project(Vector3.Cross(transform.up, Vector3.up), transform.forward);
                Vector3 rollVel = Vector3.Project(rb.angularVelocity, transform.forward);
                rb.AddTorque(rollAxis * uprightAssist - rollVel * uprightDamping, ForceMode.Acceleration);
            }
        }

        float AckermannAngle(WheelSetup w, float angle)
        {
            if (!useAckermann || wheelBase < 0.1f || Mathf.Abs(angle) < 0.1f) return angle;

            float turnRadius = wheelBase / Mathf.Tan(angle * Mathf.Deg2Rad);
            float denom = turnRadius - w.localPos.x;
            if (Mathf.Abs(denom) < 0.01f) return angle;
            return Mathf.Atan(wheelBase / denom) * Mathf.Rad2Deg;
        }

        void ApplyAntiRoll()
        {
            float strength = stats.Get(VehicleStat.AntiRoll);
            if (strength <= 0f) return;

            for (int i = 0; i < axlePairs.Count; i++)
            {
                var p = axlePairs[i];
                if (p.left.collider == null || p.right.collider == null) continue;

                float travelL = SuspensionTravel01(p.left.collider, out bool groundedL);
                float travelR = SuspensionTravel01(p.right.collider, out bool groundedR);
                float force = (travelL - travelR) * strength;

                if (groundedL)
                    rb.AddForceAtPosition(p.left.collider.transform.up * -force, p.left.collider.transform.position);
                if (groundedR)
                    rb.AddForceAtPosition(p.right.collider.transform.up * force, p.right.collider.transform.position);
            }
        }

        static float SuspensionTravel01(WheelCollider c, out bool grounded)
        {
            grounded = c.GetGroundHit(out WheelHit hit);
            if (!grounded) return 1f;
            return (-c.transform.InverseTransformPoint(hit.point).y - c.radius) / c.suspensionDistance;
        }

        // ------------------------------------------------------------------ visuals

        void LateUpdate()
        {
            foreach (var w in wheels)
            {
                if (w == null || w.collider == null || w.visual == null) continue;
                w.collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
                w.visual.SetPositionAndRotation(pos, rot);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.TransformPoint(centerOfMassOffset), 0.08f);
        }
    }
}

using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Arcade "sticky" cornering. WheelColliders alone let a light car slide sideways in corners. This component
    /// removes part of the car's SIDEWAYS speed every physics step, so the car follows the way it is pointing
    /// instead of skating, and it still drifts when the handbrake is used.
    ///
    ///   Lateral Grip       how fast sideways speed is removed (per second). 0 = off, 3 to 6 = grippy, 10+ = on rails.
    ///   Max Lateral Accel  cap, so a hard impact is not turned into a sudden stop.
    ///   Handbrake Factor   fraction of the assist left while the handbrake is held (0 = pure drift).
    ///
    /// Add it next to VehicleController. It only pushes while at least one wheel touches the ground and the car is
    /// moving, so it never affects a car in the air or at rest. Tune it by feel; start with the defaults.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleController))]
    public class VehicleGripAssist : MonoBehaviour
    {
        [Header("Strength")]
        [Tooltip("How fast sideways speed is removed (per second). 0 = off.")]
        [Range(0f, 20f)] public float lateralGrip = 4f;
        [Tooltip("Never push harder than this sideways (m/s^2).")]
        public float maxLateralAcceleration = 18f;
        [Tooltip("Also scale with the car's Tire Grip stat, so grip upgrades are felt.")]
        public bool scaleWithTireGrip = true;

        [Header("Drift")]
        [Tooltip("Share of the assist that stays on while the handbrake is held. 0 = free drift.")]
        [Range(0f, 1f)] public float handbrakeFactor = 0.2f;

        [Header("Drift angle limit (stops the car spinning and sliding backward)")]
        [Tooltip("While drifting, the direction the car travels may not stray further than this from where its nose points.")]
        public bool limitDriftAngle = true;
        [Range(15f, 80f)] public float maxDriftAngle = 45f;
        [Tooltip("How fast (degrees per second) the travel direction is turned back toward the nose when over the limit.")]
        public float driftCorrection = 120f;
        [Tooltip("The limit stays on this long after the handbrake is released (seconds).")]
        public float driftTail = 0.6f;
        [Tooltip("Below this speed (m/s) the limit does nothing.")]
        public float minDriftSpeed = 3f;

        [Header("Speed range")]
        [Tooltip("Below this speed (m/s) the assist is off, so a parked car does not jitter.")]
        public float minSpeed = 1.5f;
        [Tooltip("Reaches full strength at this speed (m/s).")]
        public float fullAtSpeed = 6f;

        Rigidbody rb;
        VehicleController vehicle;
        float driftTimer;

        Vector3 Velocity
        {
#if UNITY_6000_0_OR_NEWER
            get => rb.linearVelocity;
            set => rb.linearVelocity = value;
#else
            get => rb.velocity;
            set => rb.velocity = value;
#endif
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            vehicle = GetComponent<VehicleController>();
        }

        void FixedUpdate()
        {
            if (!vehicle.IsGrounded) return;

            ApplyLateralGrip();
            if (limitDriftAngle) LimitDriftAngle();
        }

        void ApplyLateralGrip()
        {
            if (lateralGrip <= 0f) return;

            float speed = Mathf.Abs(vehicle.ForwardSpeed);
            if (speed <= minSpeed) return;

            float fade = Mathf.Clamp01(Mathf.InverseLerp(minSpeed, Mathf.Max(minSpeed + 0.1f, fullAtSpeed), speed));
            float drift = Mathf.Lerp(1f, handbrakeFactor, vehicle.HandbrakeAmount);
            float grip = scaleWithTireGrip && vehicle.Stats != null ? vehicle.Stats.Get(VehicleStat.TireGrip) : 1f;

            Vector3 velocity = Velocity;
            Vector3 right = transform.right;
            float sideways = Vector3.Dot(velocity, right);

            float accel = -sideways * lateralGrip * fade * drift * grip;
            accel = Mathf.Clamp(accel, -maxLateralAcceleration, maxLateralAcceleration);

            rb.AddForce(right * accel, ForceMode.Acceleration);
        }

        /// <summary>
        /// While the handbrake is (or just was) held, turns the travel direction back toward the nose whenever it strays
        /// further than "Max Drift Angle". The car still slides, but it can no longer rotate past the point where it
        /// would travel backward.
        /// </summary>
        void LimitDriftAngle()
        {
            float dt = Time.fixedDeltaTime;
            if (vehicle.HandbrakeAmount > 0.05f) driftTimer = driftTail;
            else driftTimer -= dt;
            if (driftTimer <= 0f) return;

            // the player is deliberately reversing: leave the direction alone
            if (vehicle.ThrottleInput < -0.01f) return;

            Vector3 velocity = Velocity;
            Vector3 flat = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (flat.magnitude < minDriftSpeed) return;

            Vector3 heading = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (heading.sqrMagnitude < 0.0001f) return;

            // how far the travel direction has to turn to line up with the nose
            float slip = Vector3.SignedAngle(flat, heading, Vector3.up);
            float excess = Mathf.Abs(slip) - maxDriftAngle;
            if (excess <= 0f) return;

            float turn = Mathf.Min(excess, driftCorrection * dt) * Mathf.Sign(slip);
            Velocity = Quaternion.AngleAxis(turn, Vector3.up) * flat + Vector3.up * velocity.y;
        }
    }
}

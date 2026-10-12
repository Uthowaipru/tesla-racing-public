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

        [Header("Speed range")]
        [Tooltip("Below this speed (m/s) the assist is off, so a parked car does not jitter.")]
        public float minSpeed = 1.5f;
        [Tooltip("Reaches full strength at this speed (m/s).")]
        public float fullAtSpeed = 6f;

        Rigidbody rb;
        VehicleController vehicle;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            vehicle = GetComponent<VehicleController>();
        }

        void FixedUpdate()
        {
            if (lateralGrip <= 0f || !vehicle.IsGrounded) return;

            float speed = Mathf.Abs(vehicle.ForwardSpeed);
            if (speed <= minSpeed) return;

            float fade = Mathf.Clamp01(Mathf.InverseLerp(minSpeed, Mathf.Max(minSpeed + 0.1f, fullAtSpeed), speed));
            float drift = Mathf.Lerp(1f, handbrakeFactor, vehicle.HandbrakeAmount);
            float grip = scaleWithTireGrip && vehicle.Stats != null ? vehicle.Stats.Get(VehicleStat.TireGrip) : 1f;

#if UNITY_6000_0_OR_NEWER
            Vector3 velocity = rb.linearVelocity;
#else
            Vector3 velocity = rb.velocity;
#endif
            Vector3 right = transform.right;
            float sideways = Vector3.Dot(velocity, right);

            float accel = -sideways * lateralGrip * fade * drift * grip;
            accel = Mathf.Clamp(accel, -maxLateralAcceleration, maxLateralAcceleration);

            rb.AddForce(right * accel, ForceMode.Acceleration);
        }
    }
}

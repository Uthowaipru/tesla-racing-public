using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Puts the vehicle back on its wheels. Works two ways:
    ///   1. Manual : call Recover() (VehicleInput does this for the R key, gamepad Y/North, and the mobile RESET button).
    ///   2. Auto   : recovers when the car is flipped / on its side for a while, or stuck while the player is driving.
    ///
    /// Recovery keeps the car at the SAME location and heading, lifted slightly, upright, with velocity cleared.
    /// Optionally, "stuck" recovery can move the car back to where it was a few seconds ago (Last Safe Point),
    /// which helps when it is wedged against a wall.
    ///
    /// Setup: add to the vehicle (next to VehicleController). Everything else is found automatically.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleController))]
    public class VehicleRecovery : MonoBehaviour
    {
        public enum StuckRecoverMode { SameSpot, LastSafePoint }

        [Header("Reset (button / key)")]
        [Tooltip("How high the car is lifted when it is put back upright (meters).")]
        public float liftHeight = 1.2f;
        [Tooltip("Minimum time between two recoveries (seconds).")]
        public float cooldown = 1.5f;

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
        [Tooltip("Seconds the car must be stuck before it is recovered.")]
        public float stuckTime = 4f;
        [Tooltip("SameSpot = lift and straighten where it is. LastSafePoint = go back to where it was driving a few seconds ago.")]
        public StuckRecoverMode stuckMode = StuckRecoverMode.SameSpot;
        [Tooltip("Last Safe Point only: how often a safe position is remembered (seconds).")]
        public float safePointInterval = 1f;
        [Tooltip("Last Safe Point only: how many positions are remembered. The oldest one is used.")]
        public int safePointCount = 4;

        [Header("Camera")]
        public VehicleCameraRig cameraRig;
        [Tooltip("Snap the camera behind the car after recovering, so it does not swing across the map.")]
        public bool snapCameraOnRecover = true;

        // ---- set by VehicleInput (or an AI) every frame: final throttle -1..1
        public float DriveInput { get; set; }

        /// <summary>0..1 progress of an automatic recovery (for a HUD "Resetting..." bar).</summary>
        public float AutoRecoverProgress { get; private set; }

        public event Action Recovered;

        Rigidbody rb;
        VehicleController vehicle;

        float flippedTimer, stuckTimer, cooldownTimer, safeTimer;
        readonly Queue<Pose> safePoses = new Queue<Pose>();

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
        }

        void Start()
        {
            if (cameraRig == null) cameraRig = FindFirstObjectByType<VehicleCameraRig>();
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Put the car upright at its current location and heading.</summary>
        public void Recover()
        {
            Teleport(transform.position + Vector3.up * liftHeight, FlatRotation());
        }

        /// <summary>Put the car back where it was driving a few seconds ago (falls back to Recover()).</summary>
        public void RecoverToSafePoint()
        {
            if (safePoses.Count == 0) { Recover(); return; }

            Pose p = safePoses.Peek();
            safePoses.Clear();
            Teleport(p.position + Vector3.up * 0.5f, p.rotation);
        }

        // ------------------------------------------------------------------ auto detection

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= dt;
                flippedTimer = stuckTimer = 0f;
                AutoRecoverProgress = 0f;
                return;
            }

            float speed = Velocity.magnitude;
            float upDot = Vector3.Dot(transform.up, Vector3.up);

            // flipped / on its side and settled
            if (recoverWhenFlipped && upDot < flippedThreshold && speed < flippedMaxSpeed) flippedTimer += dt;
            else flippedTimer = 0f;

            // upright but not moving while the player is trying to drive
            bool trying = Mathf.Abs(DriveInput) > 0.5f;
            if (recoverWhenStuck && upDot >= flippedThreshold && trying && speed < stuckSpeed) stuckTimer += dt;
            else stuckTimer = 0f;

            if (stuckMode == StuckRecoverMode.LastSafePoint) RecordSafePoint(dt, upDot, speed);

            float flippedProgress = flippedTime > 0f ? flippedTimer / flippedTime : 0f;
            float stuckProgress = stuckTime > 0f ? stuckTimer / stuckTime : 0f;
            AutoRecoverProgress = Mathf.Clamp01(Mathf.Max(flippedProgress, stuckProgress));

            if (flippedTimer >= flippedTime && recoverWhenFlipped)
            {
                Recover();
            }
            else if (stuckTimer >= stuckTime && recoverWhenStuck)
            {
                if (stuckMode == StuckRecoverMode.LastSafePoint) RecoverToSafePoint();
                else Recover();
            }
        }

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
    }
}

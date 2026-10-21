using UnityEngine;
using Unity.Cinemachine; // Cinemachine 3.x
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VehicleSystem
{
    /// <summary>
    /// Vehicle camera for arcade / fun racing games (mobile friendly).
    ///
    /// Two modes:
    ///   Chase Mode ON  (default, best for mobile): the camera always sits behind the car and follows its
    ///                  heading with a little lag. No look input needed. Use SetLookBack(true) for a rear view.
    ///   Chase Mode OFF (PC / gamepad): free orbit like the Starter Assets camera, with auto-align after idle.
    ///
    /// Collision is split in two so bumps can no longer collapse the camera:
    ///   - Collision Filter: layers that push the camera in (walls, buildings). Leave your bumps OUT of it.
    ///   - Ground Filter:    layers the ground lift looks at (ground + bumps). Smooth, never snaps.
    ///
    /// Setup: empty GameObject at the scene root -> add this script. LocalPlayerRig assigns the vehicle
    /// (Attach), or assign it yourself in the inspector.
    /// The virtual camera and the CinemachineBrain (on your MainCamera) are created for you.
    /// </summary>
    [DefaultExecutionOrder(-50)] // run before the CinemachineBrain so the camera never lags a frame
    public class VehicleCameraRig : MonoBehaviour
    {
        [Header("Target")]
        public VehicleController vehicle;
        [Tooltip("Where the camera pivots, relative to the vehicle origin.")]
        public Vector3 rootOffset = new Vector3(0f, 1.4f, 0f);

        [Header("Camera Mode")]
        [Tooltip("ON = fixed chase camera behind the car (recommended for mobile). OFF = free orbit with look input.")]
        public bool chaseMode = true;
        [Tooltip("Chase mode: how quickly the camera swings behind the car. Lower = snappier, higher = floatier.")]
        public float chaseAlignSmoothTime = 0.25f;

        [Header("Cinemachine (auto-created if empty)")]
        public CinemachineCamera virtualCamera;
        [Tooltip("Optional handheld noise. Assign a profile from Cinemachine > Presets > Noise.")]
        public NoiseSettings noiseProfile;
        public float noiseAmplitude = 0.5f;
        public float noiseFrequency = 0.3f;
        [Tooltip("Noise amplitude at top speed (set equal to Noise Amplitude to disable the speed effect).")]
        public float noiseAmplitudeAtTopSpeed = 1f;

        [Header("3rd Person Follow")]
        public Vector3 damping = new Vector3(0.1f, 0.25f, 0.3f);
        public Vector3 shoulderOffset = new Vector3(0f, 0f, 0f);
        public float verticalArmLength = 0f;
        [Tooltip("0 = left shoulder, 0.5 = centered, 1 = right shoulder.")]
        [Range(0f, 1f)] public float cameraSide = 0.5f;
        public float cameraDistance = 6f;
        public float cameraRadius = 0.15f;

        [Header("Collision")]
        [Tooltip("Cinemachine pushes the camera toward the car when the arm touches these layers. Turn OFF for open tracks.")]
        public bool avoidObstacles = true;
        [Tooltip("Layers that push the camera in (walls, buildings). Do NOT include your small bumps here.")]
        public LayerMask collisionFilter = 1;
        [Tooltip("Layers the ground-lift check looks at (ground + bumps). Used only to tilt the camera up smoothly.")]
        public LayerMask groundFilter = 1;
        [Tooltip("Colliders with this tag never block the camera. The vehicle's colliders are tagged with it automatically.")]
        public string ignoreTag = "Player";
        public bool autoTagVehicleColliders = true;
        [Tooltip("How softly the camera moves in when it touches something (seconds). 0 = instant snap.")]
        public float dampingIntoCollision = 0.3f;
        [Tooltip("How softly the camera moves back out once clear (seconds).")]
        public float dampingFromCollision = 0.6f;

        [Header("Look (free orbit mode only)")]
        public bool readInput = true;
        public bool lockCursor = true; // ignored on mobile
        [Tooltip("Degrees per pixel.")]
        public float mouseSensitivity = 0.05f;
        [Tooltip("Degrees per second at full stick.")]
        public float stickSensitivity = 300f;
        public bool invertY = false;
        public float topClamp = 70f;
        [Tooltip("Negative = camera goes BELOW the pivot.")]
        public float bottomClamp = -10f;
        [Tooltip("Resting pitch while driving (0 = level with the pivot).")]
        public float defaultPitch = 8f;
        public float cameraAngleOverride = 0f;
        public bool lockCameraPosition = false;

        [Header("Ground Protection")]
        [Tooltip("Tilts the camera up whenever the ground (or a bump) under the camera arm would swallow it.")]
        public bool keepAboveGround = true;
        public float minGroundClearance = 0.5f;
        [Tooltip("The ground check can never tilt the camera up by more than this (degrees).")]
        public float maxGroundLift = 18f;
        [Tooltip("How fast the camera tilts up when the ground rises (seconds). Higher = gentler.")]
        public float groundLiftRiseTime = 0.12f;
        [Tooltip("How fast the camera settles back down once the ground is clear (seconds).")]
        public float groundLiftSmoothTime = 0.25f;

        [Header("Debug")]
        public bool showDebug = false;

        [Header("Auto Align (free orbit mode only)")]
        public bool autoAlign = true;
        public float alignDelay = 0.5f;
        public float alignMinSpeed = 3f; // km/h
        public float alignSmoothTime = 0.5f;
        [Tooltip("0 = align to the car's nose, 1 = align to its direction of travel (nicer in drifts).")]
        [Range(0f, 1f)] public float velocityHeading = 0.6f;

        [Header("Speed Effects")]
        public float baseFov = 55f;
        public float topSpeedFov = 70f;   // set equal to Base Fov to disable
        public float extraDistanceAtTopSpeed = 1.5f;
        public AnimationCurve speedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public float effectSmoothTime = 0.3f;

        // ---- refs
        Rigidbody rb;
        Transform cameraRoot;
        CinemachineThirdPersonFollow follow;
        CinemachineBasicMultiChannelPerlin noise;
        string resolvedIgnoreTag;

        // ---- state
        float yaw, pitch, yawVel, pitchVel, lookIdle;
        float fov, fovVel, distance, distVel, speedRatio;
        Vector2 externalLook; bool externalIsMouse;
        bool lookBack;
        const float LookThreshold = 0.01f;
        float groundLift, groundLiftVel;
        Vector2 lastLook;
        static readonly float[] GroundSampleFractions = { 0.4f, 0.65f, 0.85f, 1f };
        readonly RaycastHit[] groundHits = new RaycastHit[8];

        // ------------------------------------------------------------------ public API

        /// <summary>Swap the followed vehicle. Snaps behind it instantly.</summary>
        public void SetTarget(VehicleController newVehicle)
        {
            vehicle = newVehicle;
            rb = vehicle != null ? vehicle.GetComponent<Rigidbody>() : null;
            if (vehicle != null) PrepareCollision();
            Snap();
        }

        /// <summary>Instantly place the camera behind the vehicle (after teleport / respawn).</summary>
        public void Snap()
        {
            if (vehicle == null) return;

            yaw = HeadingYaw();
            pitch = defaultPitch;
            yawVel = pitchVel = fovVel = distVel = 0f;
            lookIdle = 0f;
            speedRatio = 0f;
            fov = baseFov;
            distance = cameraDistance;
            groundLift = groundLiftVel = 0f;

            UpdateGroundLift(0f);
            PlaceRoot();
            ApplyEffects();
            if (virtualCamera != null) virtualCamera.PreviousStateIsValid = false; // reset Cinemachine damping
        }

        /// <summary>
        /// Hook this to a "look back" UI button (press = true, release = false). Works in both modes.
        /// </summary>
        public void SetLookBack(bool value) => lookBack = value;

        /// <summary>True while the camera is in FREE mode (chase mode off): the player turns it by hand.</summary>
        public bool FreeCamera => !chaseMode;

        /// <summary>Raised when the mode changes (true = free camera).</summary>
        public event System.Action<bool> FreeCameraChanged;

        /// <summary>Chase camera <-> free camera. Hold look-back works in both modes.</summary>
        public void ToggleFreeCamera() => SetFreeCamera(chaseMode);

        public void SetFreeCamera(bool free)
        {
            if (chaseMode == !free) return;
            chaseMode = !free;
            FreeCameraChanged?.Invoke(free);
        }

        // ---- the cursor must be free while a menu or result screen needs the mouse
        static VehicleCameraRig current;
        static bool uiNeedsCursor;

        /// <summary>
        /// Result screens and menus call this: true frees the mouse cursor (PC) so buttons can be clicked, false gives the
        /// camera its cursor lock back. Several callers are fine as long as each one switches it off again.
        /// </summary>
        public static void SetUiNeedsCursor(bool needed)
        {
            uiNeedsCursor = needed;
            if (needed)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (current != null)
            {
                current.ApplyCursor();
            }
        }

        /// <summary>
        /// Feed look input yourself (free orbit mode), e.g. from an on-screen touch pad.
        /// Mouse-like: pass degrees. Stick-like: pass degrees per second.
        /// </summary>
        public void SetLookInput(Vector2 look, bool fromMouse)
        {
            externalLook = look;
            externalIsMouse = fromMouse;
        }

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            if (transform.parent != null) transform.SetParent(null, true); // rig must live in world space

            cameraRoot = new GameObject("Camera Root").transform;
            cameraRoot.SetParent(transform, true);

            SetupCinemachine();
        }

        void OnEnable()
        {
            current = this;
            ApplyCursor();
            VehicleController.VehicleEnabled += OnVehicleEnabled;
        }

        void OnDisable()
        {
            if (current == this) current = null;
            VehicleController.VehicleEnabled -= OnVehicleEnabled;
        }

        void OnApplicationFocus(bool hasFocus) { if (hasFocus) ApplyCursor(); }

        // Cars that spawn later (other racers) must not block this camera either.
        void OnVehicleEnabled(VehicleController other)
        {
            if (vehicle != null) TagVehicleColliders(other);
        }

        void ApplyCursor()
        {
            if (uiNeedsCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }
            if (lockCursor && !Application.isMobilePlatform && !PauseMenu.IsPaused) Cursor.lockState = CursorLockMode.Locked;
        }

        void Start()
        {
            // The target is assigned by LocalPlayerRig.Attach() (or in the inspector). No scene search here,
            // so with several cars the camera can never grab the wrong one.
            if (vehicle != null) SetTarget(vehicle);
        }

        void SetupCinemachine()
        {
            if (virtualCamera == null)
            {
                var go = new GameObject("Vehicle Follow Camera");
                go.transform.SetParent(transform, false);
                virtualCamera = go.AddComponent<CinemachineCamera>();
            }

            // Toggle so Cinemachine rebuilds its component pipeline after we add components.
            virtualCamera.enabled = false;

            var target = virtualCamera.Target;
            target.TrackingTarget = cameraRoot;
            virtualCamera.Target = target;

            if (!virtualCamera.TryGetComponent(out follow))
                follow = virtualCamera.gameObject.AddComponent<CinemachineThirdPersonFollow>();

            follow.Damping = damping;
            follow.ShoulderOffset = shoulderOffset;
            follow.VerticalArmLength = verticalArmLength;
            follow.CameraSide = cameraSide;
            follow.CameraDistance = cameraDistance;

            // Cinemachine 3 groups the collision settings into AvoidObstacles
            var obstacles = follow.AvoidObstacles;
            obstacles.Enabled = avoidObstacles;
            obstacles.CollisionFilter = collisionFilter;
            obstacles.CameraRadius = cameraRadius;
            obstacles.DampingIntoCollision = dampingIntoCollision;
            obstacles.DampingFromCollision = dampingFromCollision;
            follow.AvoidObstacles = obstacles;

            if (noiseProfile != null)
            {
                if (!virtualCamera.TryGetComponent(out noise))
                    noise = virtualCamera.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
                noise.NoiseProfile = noiseProfile;
                noise.AmplitudeGain = noiseAmplitude;
                noise.FrequencyGain = noiseFrequency;
                noise.PivotOffset = new Vector3(0f, 0f, 1f);
            }

            virtualCamera.enabled = true;

            var cam = Camera.main;
            if (cam != null && !cam.TryGetComponent<CinemachineBrain>(out _))
                cam.gameObject.AddComponent<CinemachineBrain>();
        }

        /// <summary>
        /// The 3rd person follow collides with scene geometry, so the vehicle's own colliders must be ignored.
        /// </summary>
        void PrepareCollision()
        {
            resolvedIgnoreTag = vehicle.CompareTag("Untagged") ? ignoreTag : vehicle.tag;

            // our own car plus every other racer, so no car in the pack can push the camera in
            TagVehicleColliders(vehicle);
            var others = VehicleController.All;
            for (int i = 0; i < others.Count; i++) TagVehicleColliders(others[i]);

            if (follow != null)
            {
                var obstacles = follow.AvoidObstacles;
                obstacles.IgnoreTag = resolvedIgnoreTag ?? "";
                follow.AvoidObstacles = obstacles;
            }
        }

        void TagVehicleColliders(VehicleController car)
        {
            if (car == null || !autoTagVehicleColliders || string.IsNullOrEmpty(resolvedIgnoreTag)) return;

            try
            {
                foreach (var c in car.GetComponentsInChildren<Collider>(true))
                    if (c.CompareTag("Untagged")) c.gameObject.tag = resolvedIgnoreTag;
            }
            catch (UnityException)
            {
                Debug.LogWarning($"VehicleCameraRig: tag '{resolvedIgnoreTag}' doesn't exist. " +
                                 "Create it in Project Settings > Tags and Layers, or put the vehicle on its own tag.");
                resolvedIgnoreTag = "";
            }
        }

        // ------------------------------------------------------------------ update

        void LateUpdate()
        {
            if (vehicle == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (chaseMode) UpdateChase(dt);
            else UpdateLook(dt);

            UpdateGroundLift(dt);
            PlaceRoot();
            UpdateSpeedEffects(dt);
        }

        /// <summary>Chase mode: camera always swings behind the car (or in front, while look-back is held).</summary>
        void UpdateChase(float dt)
        {
            externalLook = Vector2.zero;
            lastLook = Vector2.zero;

            float targetYaw = HeadingYaw() + (lookBack ? 180f : 0f);
            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVel, chaseAlignSmoothTime);
            pitch = Mathf.SmoothDamp(pitch, defaultPitch, ref pitchVel, chaseAlignSmoothTime);

            yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            pitch = Mathf.Clamp(pitch, bottomClamp, topClamp);
        }

        /// <summary>Free orbit mode (PC / gamepad).</summary>
        void UpdateLook(float dt)
        {
            Vector2 look = readInput ? ReadLook(dt) : Vector2.zero;

            look += externalIsMouse ? externalLook : externalLook * dt;
            externalLook = Vector2.zero;
            lastLook = look;

            if (look.sqrMagnitude >= LookThreshold && !lockCameraPosition)
            {
                yaw += look.x;
                pitch += look.y * (invertY ? -1f : 1f);
                lookIdle = 0f;
            }
            else
            {
                lookIdle += dt;
            }

            if (lookBack)
            {
                yaw = Mathf.SmoothDampAngle(yaw, HeadingYaw() + 180f, ref yawVel, alignSmoothTime * 0.5f);
                pitch = Mathf.SmoothDamp(pitch, defaultPitch, ref pitchVel, alignSmoothTime * 0.5f);
            }
            else if (autoAlign && !lockCameraPosition && lookIdle > alignDelay && vehicle.SpeedKmh > alignMinSpeed)
            {
                yaw = Mathf.SmoothDampAngle(yaw, HeadingYaw(), ref yawVel, alignSmoothTime);
                pitch = Mathf.SmoothDamp(pitch, defaultPitch, ref pitchVel, alignSmoothTime);
            }

            yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            pitch = Mathf.Clamp(pitch, bottomClamp, topClamp);
        }

        /// <summary>
        /// Samples the ground along the camera arm and tilts the camera up just enough to clear it.
        /// The look pitch is never modified, so it settles back when the ground is clear.
        /// Rises gently (no whip) and is capped by Max Ground Lift.
        /// </summary>
        void UpdateGroundLift(float dt)
        {
            float targetLift = 0f;

            if (keepAboveGround)
            {
                Vector3 rootPos = vehicle.transform.position + rootOffset;
                Vector3 back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
                float dist = Mathf.Max(0.5f, distance);
                float cosP = Mathf.Cos(Mathf.Clamp(pitch + groundLift, -89f, 89f) * Mathf.Deg2Rad);

                float requiredSin = -1f;
                for (int i = 0; i < GroundSampleFractions.Length; i++)
                {
                    float f = GroundSampleFractions[i];
                    Vector3 xz = rootPos + back * (f * dist * cosP);
                    if (!TryGetGroundY(new Vector3(xz.x, rootPos.y + 0.5f, xz.z), out float groundY)) continue;

                    float need = (groundY + minGroundClearance - rootPos.y) / (f * dist);
                    if (need > requiredSin) requiredSin = need;
                }

                if (requiredSin > -1f)
                {
                    float minPitch = Mathf.Asin(Mathf.Clamp(requiredSin, -1f, 1f)) * Mathf.Rad2Deg;
                    targetLift = Mathf.Max(0f, minPitch - pitch);
                    targetLift = Mathf.Min(targetLift, maxGroundLift, Mathf.Max(0f, 85f - pitch));
                }
            }

            if (dt <= 0f)
            {
                groundLift = targetLift;
                groundLiftVel = 0f;
                return;
            }

            float smoothTime = targetLift > groundLift ? groundLiftRiseTime : groundLiftSmoothTime;
            groundLift = Mathf.SmoothDamp(groundLift, targetLift, ref groundLiftVel, Mathf.Max(0.01f, smoothTime));
        }

        bool TryGetGroundY(Vector3 from, out float y)
        {
            y = 0f;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, groundHits, 30f, groundFilter, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = groundHits[i];
                if (h.collider.transform.IsChildOf(vehicle.transform)) continue; // ignore the car itself
                if (h.distance < best) { best = h.distance; y = h.point.y; found = true; }
            }
            return found;
        }

        void PlaceRoot()
        {
            cameraRoot.SetPositionAndRotation(
                vehicle.transform.position + rootOffset,
                Quaternion.Euler(pitch + groundLift + cameraAngleOverride, yaw, 0f));
        }

        void UpdateSpeedEffects(float dt)
        {
            float maxKmh = Mathf.Max(1f, vehicle.Stats.Get(VehicleStat.MaxSpeed));
            float target = speedCurve.Evaluate(Mathf.Clamp01(vehicle.SpeedKmh / maxKmh));
            speedRatio = Mathf.MoveTowards(speedRatio, target, dt / Mathf.Max(0.01f, effectSmoothTime));

            fov = Mathf.SmoothDamp(fov, Mathf.Lerp(baseFov, topSpeedFov, speedRatio), ref fovVel, effectSmoothTime);
            distance = Mathf.SmoothDamp(distance, cameraDistance + extraDistanceAtTopSpeed * speedRatio, ref distVel, effectSmoothTime);
            ApplyEffects();
        }

        void ApplyEffects()
        {
            if (follow != null) follow.CameraDistance = distance;

            if (virtualCamera != null)
            {
                var lens = virtualCamera.Lens;
                lens.FieldOfView = fov;
                virtualCamera.Lens = lens;
            }

            if (noise != null)
                noise.AmplitudeGain = Mathf.Lerp(noiseAmplitude, noiseAmplitudeAtTopSpeed, speedRatio);
        }

        /// <summary>Yaw that puts the camera behind the vehicle (never flips when reversing).</summary>
        float HeadingYaw()
        {
            Vector3 fwd = Vector3.ProjectOnPlane(vehicle.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) return yaw;
            fwd.Normalize();

            if (rb != null && vehicle.ForwardSpeed > 0f && velocityHeading > 0f)
            {
                Vector3 vel = Vector3.ProjectOnPlane(VelocityOf(rb), Vector3.up);
                float speed = vel.magnitude;
                if (speed > 0.01f)
                {
                    float blend = velocityHeading * Mathf.InverseLerp(3f, 9f, speed);
                    fwd = Vector3.Slerp(fwd, vel / speed, blend);
                }
            }
            return Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        }

        static Vector3 VelocityOf(Rigidbody r)
        {
#if UNITY_6000_0_OR_NEWER
            return r.linearVelocity;
#else
            return r.velocity;
#endif
        }

        void OnGUI()
        {
            if (!showDebug) return;
            GUI.Label(new Rect(10, 10, 700, 120),
                $"mode {(chaseMode ? "chase" : "orbit")}  yaw {yaw:0.0}  pitch {pitch:0.0}  groundLift {groundLift:0.0}\n" +
                $"look input {lastLook.x:0.00}, {lastLook.y:0.00}  idle {lookIdle:0.0}s  lookBack {lookBack}\n" +
                $"cursor {Cursor.lockState}  focused {Application.isFocused}  lockCameraPosition {lockCameraPosition}");
        }

        // ------------------------------------------------------------------ input (mouse / gamepad, free orbit mode)

        Vector2 ReadLook(float dt)
        {
            Vector2 v = Vector2.zero;
            float ySign = -1f; // mouse up -> camera looks up

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue();
                v += new Vector2(d.x, d.y * ySign) * mouseSensitivity;
            }

            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 s = gp.rightStick.ReadValue();
                if (s.magnitude >= 0.125f)
                    v += new Vector2(s.x, s.y * ySign) * (stickSensitivity * dt);
            }
#else
            Vector2 d = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            v += new Vector2(d.x, d.y * ySign) * (mouseSensitivity * 10f);
#endif
            return v;
        }
    }
}
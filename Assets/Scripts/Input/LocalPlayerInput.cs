using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VehicleSystem
{
    /// <summary>
    /// The LOCAL player's controls: touch + keyboard + gamepad (new Input System) turned into one DriveInput.
    /// Formerly "VehicleInput". The active device is chosen by InputModeManager (last device used wins, touch
    /// at start). It no longer lives on, or knows about, a specific car:
    ///
    ///   - It implements IDriveInputSource, so any vehicle's VehicleDriver can be pointed at it
    ///     (LocalPlayerRig.Attach does that for the local player's car only).
    ///   - Reset and look-back are exposed as ResetPressed / LookBack. LocalPlayerRig forwards them to the
    ///     attached car's recovery and the camera.
    ///   - It creates no UI and searches for nothing. MobileControls is assigned by LocalPlayerRig.
    ///
    ///   Control            Keyboard + Mouse      Gamepad                         Touch
    ///   Accelerate         W / Up                Right trigger                   GAS (hidden when auto-accelerate is on)
    ///   Brake / Reverse    S / Down              Left trigger                    BRAKE
    ///   Steer              A D / Left Right      Left stick / D-pad              &lt; &gt; buttons (or tilt)
    ///   Drift (handbrake)  Space                 A / Cross or Right bumper       DRIFT
    ///   Look back (hold)   C                     Right stick click               BACK
    ///   Reset vehicle      R                     Y / Triangle                    RESET
    ///   Boost (battery)    Left Shift            X / Square or Left bumper       BOOST
    ///   Camera (free/chase) V                    D-pad up                        CAM
    ///
    /// Auto-accelerate (MobileControls.autoAccelerate) applies to ALL devices: the car drives forward on its own,
    /// the brake control brakes and then reverses, exactly like the BRAKE button on touch. Brake always wins.
    ///
    /// ONE of these exists per local player (one per phone). Because it reads every keyboard / gamepad, do not
    /// give two local players on the same device their own LocalPlayerInput without splitting the bindings.
    ///
    /// Actions are created in code, so you do not need to edit the InputSystem_Actions asset.
    /// Requires Project Settings > Player > Active Input Handling = "Input System Package" or "Both".
    /// </summary>
    [DefaultExecutionOrder(-40)] // poll before the VehicleDrivers read us (default order 0)
    public class LocalPlayerInput : MonoBehaviour, IDriveInputSource
    {
        [Tooltip("Optional. Assigned by LocalPlayerRig. Without it, touch input is unavailable.")]
        public MobileControls mobile;

        [Tooltip("Auto-accelerate used only when there is no MobileControls (otherwise its setting is used for every device).")]
        public bool autoAccelerateWithoutMobileUI = true;

        /// <summary>True when the car accelerates by itself (same for touch, keyboard and gamepad).</summary>
        public bool AutoAccelerate => mobile != null ? mobile.autoAccelerate : autoAccelerateWithoutMobileUI;

        /// <summary>The newest input (updated once per frame, before the drivers run).</summary>
        public DriveInput Current { get; private set; }

        /// <summary>True while the player holds the look-back control.</summary>
        public bool LookBack { get; private set; }

        /// <summary>Fired once when the player presses reset (key, pad button or touch RESET).</summary>
        public event Action ResetPressed;

        /// <summary>Fired once when the player presses BOOST (key, pad button or touch BOOST).</summary>
        public event Action BoostPressed;

        /// <summary>Fired once when the player taps the camera button (chase camera <-> free camera).</summary>
        public event Action CameraTogglePressed;

        public DriveInput Read() => Current;

#if ENABLE_INPUT_SYSTEM
        InputAction accelAction, brakeAction, steerAction, handbrakeAction, lookBackAction, resetAction, boostAction, cameraAction;
#endif

        void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            // ---- accelerate (0..1)
            accelAction = new InputAction("Accelerate", InputActionType.Value);
            accelAction.AddBinding("<Keyboard>/w");
            accelAction.AddBinding("<Keyboard>/upArrow");
            accelAction.AddBinding("<Gamepad>/rightTrigger");

            // ---- brake, then reverse (0..1)
            brakeAction = new InputAction("BrakeReverse", InputActionType.Value);
            brakeAction.AddBinding("<Keyboard>/s");
            brakeAction.AddBinding("<Keyboard>/downArrow");
            brakeAction.AddBinding("<Gamepad>/leftTrigger");

            // ---- steering (-1..1)
            steerAction = new InputAction("Steer", InputActionType.Value);
            steerAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            steerAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            steerAction.AddBinding("<Gamepad>/leftStick/x");
            steerAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/dpad/left").With("Positive", "<Gamepad>/dpad/right");

            // ---- buttons
            handbrakeAction = new InputAction("Handbrake", InputActionType.Button);
            handbrakeAction.AddBinding("<Keyboard>/space");
            handbrakeAction.AddBinding("<Gamepad>/buttonSouth");
            handbrakeAction.AddBinding("<Gamepad>/rightShoulder");

            lookBackAction = new InputAction("LookBack", InputActionType.Button);
            lookBackAction.AddBinding("<Keyboard>/c");
            lookBackAction.AddBinding("<Gamepad>/rightStickPress");

            resetAction = new InputAction("ResetVehicle", InputActionType.Button);
            resetAction.AddBinding("<Keyboard>/r");
            resetAction.AddBinding("<Gamepad>/buttonNorth");

            boostAction = new InputAction("Boost", InputActionType.Button);
            boostAction.AddBinding("<Keyboard>/leftShift");
            boostAction.AddBinding("<Gamepad>/buttonWest");
            boostAction.AddBinding("<Gamepad>/leftShoulder");

            cameraAction = new InputAction("CameraToggle", InputActionType.Button);
            cameraAction.AddBinding("<Keyboard>/v");
            cameraAction.AddBinding("<Gamepad>/dpad/up");
#endif
        }

#if ENABLE_INPUT_SYSTEM
        void OnEnable()
        {
            accelAction.Enable();
            brakeAction.Enable();
            steerAction.Enable();
            handbrakeAction.Enable();
            lookBackAction.Enable();
            resetAction.Enable();
            boostAction.Enable();
            cameraAction.Enable();
        }

        void OnDisable()
        {
            accelAction.Disable();
            brakeAction.Disable();
            steerAction.Disable();
            handbrakeAction.Disable();
            lookBackAction.Disable();
            resetAction.Disable();
            boostAction.Disable();
            cameraAction.Disable();
        }

        void OnDestroy()
        {
            accelAction?.Dispose();
            brakeAction?.Dispose();
            steerAction?.Dispose();
            handbrakeAction?.Dispose();
            lookBackAction?.Dispose();
            resetAction?.Dispose();
            boostAction?.Dispose();
            cameraAction?.Dispose();
        }
#endif

        void Update()
        {
            // the menu is open: ignore the driving controls (no reset / handbrake while paused)
            if (PauseMenu.IsPaused)
            {
                Current = DriveInput.Neutral;
                LookBack = false;
                return;
            }

            float throttle, steer;
            bool hand, lookBack, reset, boost, cameraToggle;

            bool touch = InputModeManager.Instance.Mode == InputMode.Touch && mobile != null;

            if (touch)
            {
                // on-screen buttons / tilt. MobileControls already applies auto-accelerate + BRAKE-then-reverse.
                throttle = mobile.Throttle;
                steer = mobile.Steer;
                hand = mobile.Handbrake;
                lookBack = mobile.LookBack;
                reset = mobile.ConsumeReset();
                boost = mobile.ConsumeBoost();
                cameraToggle = mobile.ConsumeCameraToggle();
            }
            else
            {
                float gas, brake;
#if ENABLE_INPUT_SYSTEM
                gas = accelAction.ReadValue<float>();
                brake = brakeAction.ReadValue<float>();
                steer = steerAction.ReadValue<float>();
                hand = handbrakeAction.IsPressed();
                lookBack = lookBackAction.IsPressed();
                reset = resetAction.WasPressedThisFrame();
                boost = boostAction.WasPressedThisFrame();
                cameraToggle = cameraAction.WasPressedThisFrame();
#else
                float v = Input.GetAxisRaw("Vertical");
                gas = Mathf.Max(0f, v);
                brake = Mathf.Max(0f, -v);
                steer = Input.GetAxisRaw("Horizontal");
                hand = Input.GetKey(KeyCode.Space);
                lookBack = Input.GetKey(KeyCode.C);
                reset = Input.GetKeyDown(KeyCode.R);
                boost = Input.GetKeyDown(KeyCode.LeftShift);
                cameraToggle = Input.GetKeyDown(KeyCode.V);
#endif
                // same rules as the touch buttons: brake wins (brakes, then reverses), otherwise
                // auto-accelerate drives forward, otherwise the accelerate control decides.
                if (brake > 0.05f) throttle = -brake;
                else throttle = AutoAccelerate ? 1f : gas;
            }

            Current = new DriveInput(Mathf.Clamp(throttle, -1f, 1f), Mathf.Clamp(steer, -1f, 1f), hand);
            LookBack = lookBack;

            if (reset) ResetPressed?.Invoke();
            if (boost) BoostPressed?.Invoke();
            if (cameraToggle) CameraTogglePressed?.Invoke();
        }
    }
}

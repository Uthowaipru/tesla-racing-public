using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VehicleSystem
{
    /// <summary>
    /// ONE input script for keyboard + gamepad + mobile touch/tilt (new Input System).
    /// Everything ends up in a single vehicle.SetInput(throttle, steer, handbrake) call, so the
    /// vehicle never knows which device is driving. AI / replays / network can still call SetInput() themselves.
    ///
    ///   Keyboard : W/S or Up/Down = throttle/brake/reverse, A/D or Left/Right = steer, Space = handbrake, C = look back
    ///   Gamepad  : Right/Left trigger = throttle/brake, Left stick or D-pad = steer, South button / Right shoulder = handbrake,
    ///              Right stick press = look back
    ///   Mobile   : see MobileControls (on-screen buttons, auto-accelerate, optional tilt steering)
    ///
    /// Actions are created in code, so you do not need to edit the InputSystem_Actions asset.
    /// Requires Project Settings > Player > Active Input Handling = "Input System Package" or "Both".
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleInput : MonoBehaviour
    {
        [Tooltip("Optional. Found automatically if empty.")]
        public MobileControls mobile;
        [Tooltip("Optional. Found automatically if empty. Used for the look-back button.")]
        public VehicleCameraRig cameraRig;

        VehicleController vehicle;

#if ENABLE_INPUT_SYSTEM
        InputAction throttleAction, steerAction, handbrakeAction, lookBackAction;
#endif

        void Awake()
        {
            vehicle = GetComponent<VehicleController>();

#if ENABLE_INPUT_SYSTEM
            // ---- throttle / brake / reverse (-1..1)
            throttleAction = new InputAction("Throttle", InputActionType.Value);
            throttleAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/s").With("Positive", "<Keyboard>/w");
            throttleAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/downArrow").With("Positive", "<Keyboard>/upArrow");
            throttleAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/leftTrigger").With("Positive", "<Gamepad>/rightTrigger");

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
#endif
        }

        void Start()
        {
            if (mobile == null) mobile = FindFirstObjectByType<MobileControls>();
            if (cameraRig == null) cameraRig = FindFirstObjectByType<VehicleCameraRig>();
        }

#if ENABLE_INPUT_SYSTEM
        void OnEnable()
        {
            throttleAction.Enable();
            steerAction.Enable();
            handbrakeAction.Enable();
            lookBackAction.Enable();
        }

        void OnDisable()
        {
            throttleAction.Disable();
            steerAction.Disable();
            handbrakeAction.Disable();
            lookBackAction.Disable();
        }

        void OnDestroy()
        {
            throttleAction?.Dispose();
            steerAction?.Dispose();
            handbrakeAction?.Dispose();
            lookBackAction?.Dispose();
        }
#endif

        void Update()
        {
            float throttle, steer;
            bool hand, lookBack;

#if ENABLE_INPUT_SYSTEM
            throttle = throttleAction.ReadValue<float>();
            steer = steerAction.ReadValue<float>();
            hand = handbrakeAction.IsPressed();
            lookBack = lookBackAction.IsPressed();
#else
            throttle = Input.GetAxisRaw("Vertical");
            steer = Input.GetAxisRaw("Horizontal");
            hand = Input.GetKey(KeyCode.Space);
            lookBack = Input.GetKey(KeyCode.C);
#endif

            // ---- merge mobile touch / tilt on top
            if (mobile != null && mobile.Active)
            {
                throttle += mobile.Throttle;
                steer += mobile.Steer;
                hand |= mobile.Handbrake;
                lookBack |= mobile.LookBack;
            }

            vehicle.SetInput(Mathf.Clamp(throttle, -1f, 1f), Mathf.Clamp(steer, -1f, 1f), hand);

            if (cameraRig != null) cameraRig.SetLookBack(lookBack);
        }
    }
}
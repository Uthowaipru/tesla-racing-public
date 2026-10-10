using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace VehicleSystem
{
    public enum InputMode { Touch, KeyboardMouse, Gamepad }

    /// <summary>
    /// Figures out which kind of device the player is using RIGHT NOW and exposes it as Mode.
    ///
    ///   Touch          : screen touched                          (default at start)
    ///   KeyboardMouse  : key pressed / mouse moved or clicked
    ///   Gamepad        : any gamepad button / stick / trigger used
    ///
    /// The last device used wins, so the game follows the player from touch to keyboard to pad and back.
    /// MobileControls shows its buttons only in Touch mode, ControlsOverlay shows the hotkey list in the other two,
    /// and LocalPlayerInput reads the matching device.
    ///
    /// No setup needed: it is created automatically. Add it to a GameObject yourself only to change the settings.
    /// </summary>
    [DefaultExecutionOrder(-100)] // detect before LocalPlayerInput / MobileControls read the mode
    [DisallowMultipleComponent]
    public class InputModeManager : MonoBehaviour
    {
        static InputModeManager instance;

        public static InputModeManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<InputModeManager>();
                    if (instance == null)
                        instance = new GameObject("Input Mode Manager").AddComponent<InputModeManager>();
                }
                return instance;
            }
        }

        [Header("Mode")]
        [Tooltip("Mode used until the player touches / presses something.")]
        public InputMode startMode = InputMode.Touch;
        [Tooltip("Off = never switch by itself (use SetMode()).")]
        public bool autoSwitch = true;

        [Header("Detection")]
        [Tooltip("Mouse movement / clicks switch to Keyboard+Mouse mode. Turn OFF if you test touch with the mouse in the editor (or use the Device Simulator).")]
        public bool mouseSwitchesMode = true;
        [Tooltip("Mouse movement (pixels per frame) needed to count as 'mouse in use'.")]
        public float mouseMoveThreshold = 3f;

        public InputMode Mode { get; private set; }

        /// <summary>Fired when the mode changes.</summary>
        public event Action<InputMode> ModeChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            Mode = startMode;
        }

        public void SetMode(InputMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }

        void Update()
        {
            if (!autoSwitch) return;
            if (Detect(out InputMode detected)) SetMode(detected);
        }

        bool Detect(out InputMode detected)
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) { detected = InputMode.Touch; return true; }

            var pad = Gamepad.current;
            if (pad != null && AnyButtonDown(pad)) { detected = InputMode.Gamepad; return true; }

            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) { detected = InputMode.KeyboardMouse; return true; }

            if (mouseSwitchesMode && !Application.isMobilePlatform)
            {
                var m = Mouse.current;
                if (m != null &&
                    (m.delta.ReadValue().sqrMagnitude > mouseMoveThreshold * mouseMoveThreshold ||
                     m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame ||
                     m.middleButton.wasPressedThisFrame || m.scroll.ReadValue().sqrMagnitude > 0.01f))
                {
                    detected = InputMode.KeyboardMouse;
                    return true;
                }
            }
#else
            if (Input.touchCount > 0) { detected = InputMode.Touch; return true; }

            for (int i = (int)KeyCode.JoystickButton0; i <= (int)KeyCode.JoystickButton19; i++)
                if (Input.GetKeyDown((KeyCode)i)) { detected = InputMode.Gamepad; return true; }

            bool mouseMoved = Mathf.Abs(Input.GetAxisRaw("Mouse X")) + Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > mouseMoveThreshold * 0.1f;
            if (Input.anyKeyDown || (mouseSwitchesMode && !Application.isMobilePlatform && mouseMoved))
            { detected = InputMode.KeyboardMouse; return true; }
#endif
            detected = default;
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>True while any button, trigger, d-pad direction or stick direction of the pad is held.</summary>
        static bool AnyButtonDown(Gamepad pad)
        {
            var controls = pad.allControls;
            for (int i = 0; i < controls.Count; i++)
                if (controls[i] is ButtonControl b && b.isPressed) return true;
            return false;
        }
#endif
    }
}

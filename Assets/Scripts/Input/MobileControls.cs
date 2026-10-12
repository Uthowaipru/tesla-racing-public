using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace VehicleSystem
{
    /// <summary>
    /// On-screen touch controls for the vehicle. Exposes Throttle / Steer / Handbrake / LookBack for LocalPlayerInput.
    ///
    /// UI source:
    ///   - If "Ui Root" is assigned (made with the editor tool: Tools > Vehicle > Generate Mobile Controls UI,
    ///     or the button on this component) the buttons you see in the Hierarchy are used. Move, resize,
    ///     recolour or replace them freely.
    ///   - If it is empty, a default UI is built at runtime.
    ///
    /// The UI only shows while InputModeManager is in Touch mode (the default). It hides when the player
    /// uses a keyboard / mouse / gamepad and comes back on the next touch.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        [Header("Driving")]
        [Tooltip("Car always accelerates. The brake control brakes, then reverses. Hides the Gas button. Applies to keyboard and gamepad too (LocalPlayerInput).")]
        public bool autoAccelerate = true;

        [Header("Tilt Steering")]
        [Tooltip("Steer by tilting the phone like a steering wheel. Hides the left / right buttons.")]
        public bool useTilt = false;
        [Tooltip("Degrees of phone rotation for full steering lock.")]
        public float tiltMaxAngle = 30f;
        [Tooltip("Degrees around the neutral position that are ignored.")]
        public float tiltDeadzone = 2f;
        [Tooltip("Tick this if the car steers the wrong way.")]
        public bool invertTilt = false;
        public float tiltSmoothing = 12f;

        [Header("UI (filled in by the editor tool)")]
        [Tooltip("Parent object of all touch buttons. Hidden on non-mobile platforms.")]
        public GameObject uiRoot;
        public TouchHoldButton left;
        public TouchHoldButton right;
        public TouchHoldButton gas;
        public TouchHoldButton brake;
        public TouchHoldButton drift;
        public TouchHoldButton lookBack;
        [Tooltip("Puts the car back on its wheels at the same spot.")]
        public TouchHoldButton reset;
        [Tooltip("Opens the pause menu (PauseMenu reads it).")]
        public TouchHoldButton pause;
        [Tooltip("Spends a battery for a speed boost. Created for you above DRIFT if it is missing.")]
        public TouchHoldButton boost;

        [Header("Layout when auto-accelerate is OFF (the GAS button is shown)")]
        [Tooltip("For a UI built in the editor: BRAKE and DRIFT move here when the GAS button appears, and back afterwards.")]
        public Vector2 brakePositionWhenManual = new Vector2(-500f, 190f);
        public float brakeSizeWhenManual = 220f;
        public Vector2 driftPositionWhenManual = new Vector2(-200f, 520f);

        [Header("Default colours (editor tool + runtime fallback)")]
        public Color idleColor = new Color(1f, 1f, 1f, 0.28f);
        public Color heldColor = new Color(1f, 1f, 1f, 0.6f);

        // ---- values read by LocalPlayerInput
        public bool Active { get; private set; }
        public float Throttle { get; private set; }
        public float Steer { get; private set; }
        public bool Handbrake { get; private set; }
        public bool LookBack { get; private set; }

        /// <summary>True once per tap of the RESET button. Reading it clears it.</summary>
        public bool ConsumeReset()
        {
            bool value = resetRequested;
            resetRequested = false;
            return value;
        }

        /// <summary>True once per tap of the BOOST button. Reading it clears it.</summary>
        public bool ConsumeBoost()
        {
            bool value = boostRequested;
            boostRequested = false;
            return value;
        }

        /// <summary>True once per tap of the PAUSE button. Reading it clears it.</summary>
        public bool ConsumePause()
        {
            bool value = pauseRequested;
            pauseRequested = false;
            return value;
        }

        /// <summary>
        /// Switch auto-accelerate while playing (the pause menu's option). Refused when there is no GAS button
        /// to drive with, so the car can never be left without a way to accelerate.
        /// </summary>
        public void SetAutoAccelerate(bool value)
        {
            if (!value && gas == null) return;
            autoAccelerate = value;
            if (runtimeUi) PositionRuntimeButtons();
            else ApplyManualLayout();
            ApplyOptionVisibility();
        }

        /// <summary>True when this device has an accelerometer, so tilt steering can work.</summary>
        public bool TiltAvailable
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Accelerometer.current != null;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Switch between steering with the &lt; &gt; buttons and steering by tilting the phone (the pause menu's option).
        /// Refused when the device has no accelerometer, or when there are no steering buttons to go back to, so the
        /// vehicle can never be left without a way to steer.
        /// </summary>
        public void SetUseTilt(bool value)
        {
            if (value && !TiltAvailable) return;
            if (!value && (left == null || right == null)) return;

            useTilt = value;
            tiltCalibrated = false; // zero the tilt where the phone is held now

#if ENABLE_INPUT_SYSTEM
            if (value && Accelerometer.current != null) InputSystem.EnableDevice(Accelerometer.current);
#endif
            ApplyOptionVisibility();
        }

        Vector2 brakeAutoPosition, driftAutoPosition;
        float brakeAutoSize;
        bool autoLayoutCaptured;

        /// <summary>Remembers where BRAKE and DRIFT were placed in the editor (the auto-accelerate layout).</summary>
        void CaptureAutoLayout()
        {
            if (!autoAccelerate || brake == null || drift == null) return;

            var brakeRt = (RectTransform)brake.transform;
            var driftRt = (RectTransform)drift.transform;
            brakeAutoPosition = brakeRt.anchoredPosition;
            brakeAutoSize = brakeRt.sizeDelta.x;
            driftAutoPosition = driftRt.anchoredPosition;
            autoLayoutCaptured = true;
        }

        /// <summary>Editor-built UI: with the GAS button on screen, BRAKE and DRIFT use the manual layout; otherwise the original one.</summary>
        void ApplyManualLayout()
        {
            if (!autoLayoutCaptured || brake == null || drift == null) return;

            float driftSize = ((RectTransform)drift.transform).sizeDelta.x;
            if (autoAccelerate)
            {
                Place(brake, brakeAutoPosition, brakeAutoSize);
                Place(drift, driftAutoPosition, driftSize);
            }
            else
            {
                Place(brake, brakePositionWhenManual, brakeSizeWhenManual);
                Place(drift, driftPositionWhenManual, driftSize);
            }
        }

        bool resetRequested, resetWasHeld, pauseRequested, pauseWasHeld, boostRequested, boostWasHeld, runtimeUi;
        InputModeManager modeManager;
        float tiltZero, tiltSmoothed;
        bool tiltCalibrated;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            if (Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.LandscapeLeft;
                Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                Application.targetFrameRate = 60;
            }

            EnsureEventSystem();

            if (uiRoot == null) BuildUI();
            else ApplyOptionVisibility();

            // a UI made before the boost existed has no BOOST button: add one above DRIFT
            if (boost == null && uiRoot != null)
                boost = MakeButton(uiRoot.transform, "BOOST", new Vector2(1f, 0f), new Vector2(-200f, 720f), 170f);

            if (!runtimeUi) CaptureAutoLayout();

            modeManager = InputModeManager.Instance;
            SetVisible(modeManager.Mode == InputMode.Touch);
        }

        void Start()
        {
#if ENABLE_INPUT_SYSTEM
            if (useTilt && Accelerometer.current != null)
                InputSystem.EnableDevice(Accelerometer.current);
#endif
        }

        void SetVisible(bool visible)
        {
            Active = visible;
            if (uiRoot != null) uiRoot.SetActive(visible);
        }

        /// <summary>Hides the buttons that the current options make unnecessary.</summary>
        void ApplyOptionVisibility()
        {
            if (left != null) left.gameObject.SetActive(!useTilt);
            if (right != null) right.gameObject.SetActive(!useTilt);
            if (gas != null) gas.gameObject.SetActive(!autoAccelerate);
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ------------------------------------------------------------------ runtime fallback UI

        void BuildUI()
        {
            var canvasGo = new GameObject("Mobile Controls Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            uiRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform p = canvasGo.transform;
            Vector2 bl = new Vector2(0f, 0f);
            Vector2 br = new Vector2(1f, 0f);
            Vector2 tr = new Vector2(1f, 1f);

            // always created, so the pause menu can switch between buttons and tilt (ApplyOptionVisibility hides the unused ones)
            left = MakeButton(p, "<", bl, new Vector2(190f, 190f), 240f);
            right = MakeButton(p, ">", bl, new Vector2(470f, 190f), 240f);

            gas = MakeButton(p, "GAS", br, new Vector2(-200f, 230f), 300f); // hidden while auto-accelerate is on

            brake = MakeButton(p, "BRAKE", br,
                new Vector2(autoAccelerate ? -200f : -500f, 190f), autoAccelerate ? 280f : 220f);
            drift = MakeButton(p, "DRIFT", br,
                new Vector2(-200f, autoAccelerate ? 470f : 520f), 190f);
            lookBack = MakeButton(p, "BACK", tr, new Vector2(-110f, -110f), 130f);
            reset = MakeButton(p, "RESET", tr, new Vector2(-270f, -110f), 130f);
            pause = MakeButton(p, "PAUSE", new Vector2(0f, 1f), new Vector2(110f, -110f), 130f);

            runtimeUi = true;
            PositionRuntimeButtons();
            ApplyOptionVisibility();
        }

        /// <summary>Runtime-built UI only: BRAKE / DRIFT move depending on whether the GAS button is shown.</summary>
        void PositionRuntimeButtons()
        {
            bool auto = autoAccelerate;
            Place(brake, new Vector2(auto ? -200f : -500f, 190f), auto ? 280f : 220f);
            Place(drift, new Vector2(-200f, auto ? 470f : 520f), 190f);
        }

        static void Place(TouchHoldButton b, Vector2 pos, float size)
        {
            if (b == null) return;
            var rt = (RectTransform)b.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
        }

        TouchHoldButton MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pos, float size)
        {
            var go = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(TouchHoldButton));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            img.color = idleColor;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var trt = textGo.GetComponent<RectTransform>();
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.RoundToInt(size * 0.2f);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false; // never block the button underneath

            var btn = go.GetComponent<TouchHoldButton>();
            btn.Init(img, idleColor, heldColor);
            return btn;
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            // show the buttons only in Touch mode
            bool touchMode = modeManager == null || modeManager.Mode == InputMode.Touch;
            if (touchMode != Active)
            {
                SetVisible(touchMode);
                if (touchMode) tiltCalibrated = false; // re-zero the tilt when touch takes over again
            }

            if (!Active)
            {
                Throttle = Steer = 0f;
                Handbrake = LookBack = false;
                resetWasHeld = pauseWasHeld = boostWasHeld = false;
                return;
            }

            // ---- steering
            if (useTilt) Steer = ReadTilt(Time.unscaledDeltaTime);
            else Steer = (right != null && right.Held ? 1f : 0f) - (left != null && left.Held ? 1f : 0f);

            // ---- throttle
            bool gasOn = autoAccelerate || (gas != null && gas.Held);
            bool brakeOn = brake != null && brake.Held;
            Throttle = brakeOn ? -1f : (gasOn ? 1f : 0f);

            Handbrake = drift != null && drift.Held;
            LookBack = lookBack != null && lookBack.Held;

            // RESET fires once when the button goes down
            bool resetHeld = reset != null && reset.Held;
            if (resetHeld && !resetWasHeld) resetRequested = true;
            resetWasHeld = resetHeld;

            // PAUSE fires once when the button goes down
            bool pauseHeld = pause != null && pause.Held;
            if (pauseHeld && !pauseWasHeld) pauseRequested = true;
            pauseWasHeld = pauseHeld;

            // BOOST fires once when the button goes down
            bool boostHeld = boost != null && boost.Held;
            if (boostHeld && !boostWasHeld) boostRequested = true;
            boostWasHeld = boostHeld;
        }

        // ------------------------------------------------------------------ tilt

        /// <summary>Call from a "Calibrate" button: the current phone angle becomes "straight".</summary>
        public void CalibrateTilt()
        {
            tiltCalibrated = false;
        }

        float ReadTilt(float dt)
        {
#if ENABLE_INPUT_SYSTEM
            var acc = Accelerometer.current;
            if (acc == null) return 0f;

            Vector3 a = acc.acceleration.ReadValue();
            // Rotation of the phone around the screen normal, like turning a steering wheel (degrees).
            float angle = Mathf.Atan2(a.x, -a.y) * Mathf.Rad2Deg;

            if (!tiltCalibrated)
            {
                tiltZero = angle;
                tiltSmoothed = 0f;
                tiltCalibrated = true;
            }

            float delta = Mathf.DeltaAngle(tiltZero, angle);
            if (Mathf.Abs(delta) < tiltDeadzone) delta = 0f;
            else delta -= Mathf.Sign(delta) * tiltDeadzone;

            float target = Mathf.Clamp(delta / Mathf.Max(1f, tiltMaxAngle - tiltDeadzone), -1f, 1f);
            if (invertTilt) target = -target;

            tiltSmoothed = Mathf.Lerp(tiltSmoothed, target, 1f - Mathf.Exp(-tiltSmoothing * dt));
            return tiltSmoothed;
#else
            return 0f;
#endif
        }
    }
}
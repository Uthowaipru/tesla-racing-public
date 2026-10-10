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
    /// On-screen touch controls for the vehicle. Exposes Throttle / Steer / Handbrake / LookBack for VehicleInput.
    ///
    /// UI source:
    ///   - If "Ui Root" is assigned (made with the editor tool: Tools > Vehicle > Generate Mobile Controls UI,
    ///     or the button on this component) the buttons you see in the Hierarchy are used. Move, resize,
    ///     recolour or replace them freely.
    ///   - If it is empty, a default UI is built at runtime.
    ///
    /// The UI only shows on mobile devices, or in the editor when "Force Show In Editor" is on.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        [Header("Visibility")]
        public bool forceShowInEditor = false;

        [Header("Driving")]
        [Tooltip("Car always accelerates. The brake button brakes, then reverses. Hides the Gas button.")]
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

        [Header("Default colours (editor tool + runtime fallback)")]
        public Color idleColor = new Color(1f, 1f, 1f, 0.28f);
        public Color heldColor = new Color(1f, 1f, 1f, 0.6f);

        // ---- values read by VehicleInput
        public bool Active { get; private set; }
        public float Throttle { get; private set; }
        public float Steer { get; private set; }
        public bool Handbrake { get; private set; }
        public bool LookBack { get; private set; }

        float tiltZero, tiltSmoothed;
        bool tiltCalibrated;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            Active = Application.isMobilePlatform || (Application.isEditor && forceShowInEditor);

            if (uiRoot != null) uiRoot.SetActive(Active);
            if (!Active) return;

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
        }

        void Start()
        {
#if ENABLE_INPUT_SYSTEM
            if (Active && useTilt && Accelerometer.current != null)
                InputSystem.EnableDevice(Accelerometer.current);
#endif
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

            if (!useTilt)
            {
                left = MakeButton(p, "<", bl, new Vector2(190f, 190f), 240f);
                right = MakeButton(p, ">", bl, new Vector2(470f, 190f), 240f);
            }

            if (!autoAccelerate)
                gas = MakeButton(p, "GAS", br, new Vector2(-200f, 230f), 300f);

            brake = MakeButton(p, "BRAKE", br,
                new Vector2(autoAccelerate ? -200f : -500f, 190f), autoAccelerate ? 280f : 220f);
            drift = MakeButton(p, "DRIFT", br,
                new Vector2(-200f, autoAccelerate ? 470f : 520f), 190f);
            lookBack = MakeButton(p, "BACK", tr, new Vector2(-110f, -110f), 130f);
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
            if (!Active) return;

            // ---- steering
            if (useTilt) Steer = ReadTilt(Time.unscaledDeltaTime);
            else Steer = (right != null && right.Held ? 1f : 0f) - (left != null && left.Held ? 1f : 0f);

            // ---- throttle
            bool gasOn = autoAccelerate || (gas != null && gas.Held);
            bool brakeOn = brake != null && brake.Held;
            Throttle = brakeOn ? -1f : (gasOn ? 1f : 0f);

            Handbrake = drift != null && drift.Held;
            LookBack = lookBack != null && lookBack.Held;
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
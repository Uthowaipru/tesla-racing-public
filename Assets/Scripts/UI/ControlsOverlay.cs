using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VehicleSystem
{
    /// <summary>
    /// On-screen list of every hotkey, for Keyboard+Mouse mode and Gamepad mode (hidden in Touch mode,
    /// where the buttons are the hints).
    ///
    /// UI source:
    ///   - If "Ui Root" is assigned (made with the editor tool: Tools > Vehicle > Generate Controls Overlay UI,
    ///     or the button on this component) the objects you see in the Hierarchy / Canvas are used. Move, resize,
    ///     recolour, change fonts or re-parent them freely.
    ///   - If it is empty, a default overlay is built at runtime (LocalPlayerRig creates the component for you).
    ///
    /// H (keyboard) or Select / View (gamepad) collapses / expands the list.
    /// The text follows the real settings: "Accelerate" shows "Automatic" when auto-accelerate is on, and
    /// "Look around" only shows when the camera is in free-orbit mode. Only the TEXT is written at runtime,
    /// so your layout, fonts and colours are kept.
    /// </summary>
    public class ControlsOverlay : MonoBehaviour
    {
        [Header("Visibility")]
        public bool showOnStart = true;

        [Header("UI (filled in by the editor tool)")]
        [Tooltip("Parent object of the whole overlay (usually its Canvas). Hidden in Touch mode.")]
        public GameObject uiRoot;
        [Tooltip("Heading line. Also shows the 'press H to show' hint when collapsed.")]
        public Text title;
        [Tooltip("Object holding the two columns. Hidden when the list is collapsed.")]
        public GameObject rowsRoot;
        [Tooltip("Left column: what each control does.")]
        public Text labels;
        [Tooltip("Right column: the key / button for it.")]
        public Text keys;

        [Header("Default look (editor tool + runtime fallback)")]
        public Vector2 margin = new Vector2(30f, 30f);
        public int fontSize = 26;
        public Color panelColor = new Color(0f, 0f, 0f, 0.55f);
        public Color labelColor = Color.white;
        public Color keyColor = new Color(1f, 0.85f, 0.3f, 1f);

        [Header("Optional (assigned by LocalPlayerRig)")]
        [FormerlySerializedAs("vehicleInput")]
        public LocalPlayerInput playerInput;
        public VehicleCameraRig cameraRig;

        bool expanded;

        // what the UI currently shows, to know when to refresh
        InputMode shownMode = (InputMode)(-1);
        bool shownAuto, shownChase, shownExpanded;
        readonly List<(string label, string keys)> rows = new List<(string, string)>();

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            expanded = showOnStart;

            if (uiRoot == gameObject) uiRoot = null; // would switch this script off in Touch mode

            if (uiRoot == null || title == null || labels == null || keys == null || rowsRoot == null)
            {
                if (uiRoot != null) Destroy(uiRoot); // half-assigned: start clean
                BuildUI();
            }
        }

        /// <summary>Called by LocalPlayerRig so the overlay never has to search the scene.</summary>
        public void Bind(LocalPlayerInput input, VehicleCameraRig rig)
        {
            playerInput = input;
            cameraRig = rig;
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            InputMode mode = InputModeManager.Instance.Mode;
            bool visible = mode != InputMode.Touch;
            if (uiRoot.activeSelf != visible) uiRoot.SetActive(visible);
            if (!visible) { shownMode = mode; return; }

            // H / Select toggles. Ignored on the frame the mode just changed (that key press only woke the overlay up).
            if (mode == shownMode && TogglePressed()) expanded = !expanded;

            bool auto = playerInput != null && playerInput.AutoAccelerate;
            bool chase = cameraRig == null || cameraRig.chaseMode;

            if (mode != shownMode || auto != shownAuto || chase != shownChase || expanded != shownExpanded)
            {
                shownMode = mode; shownAuto = auto; shownChase = chase; shownExpanded = expanded;
                Refresh(mode, auto, chase, expanded);
            }
        }

        static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && pad.selectButton.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.H);
#endif
        }

        // ------------------------------------------------------------------ content

        void Refresh(InputMode mode, bool auto, bool chase, bool expand)
        {
            bool pad = mode == InputMode.Gamepad;
            string toggleKey = pad ? "Select / View" : "H";

            rows.Clear();
            if (pad)
            {
                rows.Add(("Accelerate", auto ? "Automatic" : "Right Trigger"));
                rows.Add(("Brake / Reverse", "Left Trigger"));
                rows.Add(("Steer", "Left Stick / D-Pad"));
                rows.Add(("Drift (handbrake)", "A / Cross  or  Right Bumper"));
                rows.Add(("Look back (hold)", "Right Stick Click"));
                rows.Add(("Reset vehicle", "Y / Triangle"));
                rows.Add(("Boost (battery)", "X / Square  or  Left Bumper"));
                rows.Add(("Pause", "Start / Menu"));
                if (!chase) rows.Add(("Look around", "Right Stick"));
            }
            else
            {
                rows.Add(("Accelerate", auto ? "Automatic" : "W / Up"));
                rows.Add(("Brake / Reverse", "S / Down"));
                rows.Add(("Steer", "A D / Left Right"));
                rows.Add(("Drift (handbrake)", "Space"));
                rows.Add(("Look back (hold)", "C"));
                rows.Add(("Reset vehicle", "R"));
                rows.Add(("Boost (battery)", "Left Shift"));
                rows.Add(("Pause", "Esc / P"));
                if (!chase) rows.Add(("Look around", "Mouse"));
            }
            rows.Add(("Hide / show this list", toggleKey));

            string name = pad ? "GAMEPAD" : "KEYBOARD + MOUSE";
            title.text = expand
                ? $"{name} CONTROLS"
                : $"{name} CONTROLS  -  press {toggleKey} to show";

            rowsRoot.SetActive(expand);
            if (!expand) return;

            var l = new StringBuilder();
            var k = new StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) { l.Append('\n'); k.Append('\n'); }
                l.Append(rows[i].label);
                k.Append(rows[i].keys);
            }
            labels.text = l.ToString();
            keys.text = k.ToString();
        }

        // ------------------------------------------------------------------ building the UI

        /// <summary>
        /// Creates the default overlay under this object and fills the UI fields.
        /// Used at runtime when no UI is assigned, and by the editor tool. Sample text is filled in
        /// so you can style it in the editor; it is replaced at runtime.
        /// </summary>
        public void BuildUI()
        {
            var canvasGo = NewUI("Controls Overlay Canvas", transform,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            uiRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // panel: grows with its content (remove the Content Size Fitter / layout groups for a fixed size)
            var panel = NewUI("Panel", canvasGo.transform, typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(margin.x, -margin.y);

            var img = panel.GetComponent<Image>();
            img.color = panelColor;
            img.raycastTarget = false;

            var v = panel.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(24, 24, 16, 16);
            v.spacing = 10f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;

            var fit = panel.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            title = NewText("Title", panel.transform, FontStyle.Bold, keyColor);

            rowsRoot = NewUI("Rows", panel.transform, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var h = rowsRoot.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 50f;
            h.childAlignment = TextAnchor.UpperLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;

            labels = NewText("Labels", rowsRoot.transform, FontStyle.Normal, labelColor);
            keys = NewText("Keys", rowsRoot.transform, FontStyle.Bold, keyColor);

            Refresh(InputMode.KeyboardMouse, true, true, true); // sample text
        }

        static GameObject NewUI(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) go.layer = ui;
            go.transform.SetParent(parent, false);
            return go;
        }

        Text NewText(string name, Transform parent, FontStyle style, Color color)
        {
            var go = NewUI(name, parent, typeof(RectTransform), typeof(Text));

            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
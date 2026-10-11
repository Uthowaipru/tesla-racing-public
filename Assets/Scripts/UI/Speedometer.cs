using System;
using UnityEngine;
using UnityEngine.UI;

namespace VehicleSystem
{
    /// <summary>
    /// Speed readout for the race scene: a big number, the unit, and a bar (plus an optional needle) that fills
    /// up to the car's current top speed. It follows the car the local player drives: LocalPlayerRig.Attach()
    /// calls SetVehicle(), so there is nothing to assign by hand.
    ///
    /// UI source (same idea as the other UI scripts):
    ///   - If "Ui Root" is assigned (made with Tools > Vehicle > Generate Speedometer UI, or the button on this
    ///     component) the objects in your Hierarchy are used: move, resize and restyle them freely.
    ///   - If it is empty, a default speedometer is built at runtime.
    /// Only the TEXT, the bar length, the colours of the number and bar, and the needle angle are written at runtime.
    ///
    /// Analog dial: draw your own dial image, add a needle Image as its child, and assign it to "Needle". The needle
    /// is rotated between the Min and Max angle (0 = pointing up, positive = counter-clockwise).
    /// </summary>
    public class Speedometer : MonoBehaviour
    {
        [Header("Vehicle (assigned by LocalPlayerRig)")]
        public VehicleController vehicle;

        [Header("Display")]
        public bool useMph = false;
        [Tooltip("How quickly the number follows the real speed. 0 = instant.")]
        [Range(0f, 40f)] public float smoothing = 14f;
        [Tooltip("Number and bar change colour above this fraction of the car's top speed.")]
        [Range(0.5f, 1f)] public float highFromRatio = 0.9f;

        [Header("UI (filled in by the editor tool)")]
        [Tooltip("Parent object of the whole speedometer (its Canvas).")]
        public GameObject uiRoot;
        public Text speedText;
        public Text unitText;
        [Tooltip("Resized at runtime (its right anchor = speed / top speed). Keep its Anchor Min at 0,0.")]
        public RectTransform barFill;
        [Tooltip("Optional analog needle, rotated by speed.")]
        public RectTransform needle;
        public float needleMinAngle = 135f;
        public float needleMaxAngle = -135f;

        [Header("Colours")]
        public Color textColor = Color.white;
        public Color barColor = new Color(1f, 0.85f, 0.3f, 1f);
        public Color highColor = new Color(1f, 0.35f, 0.25f, 1f);

        [Header("Default look (editor tool + runtime fallback)")]
        public int fontSize = 96;
        public Color panelColor = new Color(0f, 0f, 0f, 0.45f);

        float displayed;
        int lastShown = -1;
        string lastUnit;
        VehicleStats stats;
        Image barImage;

        /// <summary>Show the speed of this car (null = 0). Called by LocalPlayerRig.</summary>
        public void SetVehicle(VehicleController car)
        {
            vehicle = car;
            stats = car != null ? car.Stats : null;
            displayed = 0f;
            lastShown = -1;
        }

        void Awake()
        {
            if (uiRoot == gameObject) uiRoot = null; // would switch this script off

            if (uiRoot == null || speedText == null)
            {
                if (uiRoot != null) Destroy(uiRoot); // half-assigned: start clean
                BuildUI();
            }

            if (barFill != null) barImage = barFill.GetComponent<Image>();
            if (vehicle != null) SetVehicle(vehicle); // assigned in the inspector
        }

        void Update()
        {
            float kmh = vehicle != null ? vehicle.SpeedKmh : 0f;
            float target = useMph ? kmh * 0.621371f : kmh;

            float follow = smoothing <= 0f ? 1f : 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            displayed = Mathf.Lerp(displayed, target, follow);

            int whole = Mathf.RoundToInt(displayed);
            if (whole != lastShown)
            {
                lastShown = whole;
                speedText.text = whole.ToString();
            }

            if (unitText != null)
            {
                string unit = useMph ? "MPH" : "KM/H";
                if (unit != lastUnit) { lastUnit = unit; unitText.text = unit; }
            }

            // fraction of the car's top speed (it changes with upgrades, so it is read every frame)
            float topKmh = stats != null ? Mathf.Max(1f, stats.Get(VehicleStat.MaxSpeed)) : 100f;
            float shownKmh = useMph ? displayed / 0.621371f : displayed;
            float ratio = Mathf.Clamp01(shownKmh / topKmh);
            bool high = ratio >= highFromRatio;

            speedText.color = high ? highColor : textColor;

            if (barFill != null)
            {
                Vector2 max = barFill.anchorMax;
                max.x = ratio;
                barFill.anchorMax = max;
                if (barImage != null) barImage.color = high ? highColor : barColor;
            }

            if (needle != null)
                needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(needleMinAngle, needleMaxAngle, ratio));
        }

        // ------------------------------------------------------------------ building the UI

        /// <summary>
        /// Creates the default speedometer under this object and fills the UI fields. Used at runtime when no UI is
        /// assigned, and by the editor tool. Sample values are filled in so you can style it in the editor.
        /// </summary>
        public void BuildUI()
        {
            var canvasGo = NewUI("Speedometer Canvas", transform,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 92; // above the hotkey list (90) and reset counter (91), below the touch buttons (100) and pause menu (200)

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // the HUD must never block taps meant for the driving buttons
            canvasGo.GetComponent<GraphicRaycaster>().enabled = false;

            // ---- panel (bottom centre)
            var panel = NewUI("Speedometer Panel", canvasGo.transform, typeof(RectTransform), typeof(Image));
            Place((RectTransform)panel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                  new Vector2(0f, 40f), new Vector2(360f, 180f));
            var panelImg = panel.GetComponent<Image>();
            panelImg.color = panelColor;
            panelImg.raycastTarget = false;

            Vector2 topCentre = new Vector2(0.5f, 1f);

            // ---- number
            speedText = MakeText(panel.transform, "Speed", "0", FontStyle.Bold, textColor, fontSize, TextAnchor.MiddleCenter);
            Place((RectTransform)speedText.transform, topCentre, topCentre, topCentre, new Vector2(0f, -6f), new Vector2(340f, 112f));

            // ---- unit
            unitText = MakeText(panel.transform, "Unit", "KM/H", FontStyle.Bold, new Color(1f, 1f, 1f, 0.6f), 30, TextAnchor.MiddleCenter);
            Place((RectTransform)unitText.transform, topCentre, topCentre, topCentre, new Vector2(0f, -118f), new Vector2(340f, 32f));

            // ---- bar
            var back = NewUI("Bar Background", panel.transform, typeof(RectTransform), typeof(Image));
            Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                  new Vector2(0f, 10f), new Vector2(320f, 12f));
            var backImg = back.GetComponent<Image>();
            backImg.color = new Color(1f, 1f, 1f, 0.15f);
            backImg.raycastTarget = false;

            var fillGo = NewUI("Bar Fill", back.transform, typeof(RectTransform), typeof(Image));
            barFill = (RectTransform)fillGo.transform;
            Stretch(barFill);
            barFill.anchorMax = new Vector2(0.6f, 1f); // sample value; set at runtime
            barImage = fillGo.GetComponent<Image>();
            barImage.color = barColor;
            barImage.raycastTarget = false;
        }

        static Text MakeText(Transform parent, string name, string text, FontStyle style, Color color, int size, TextAnchor align)
        {
            var go = NewUI(name, parent, typeof(RectTransform), typeof(Text));
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static GameObject NewUI(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, components);
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) go.layer = ui;
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

namespace VehicleSystem
{
    /// <summary>
    /// Battery display for the local player's vehicle: one square per battery you can carry (filled = you have it) and a bar
    /// that drains while the boost is on. LocalPlayerRig.Attach() gives it the car (SetVehicle). The repair kit counter is
    /// the ResetCounterUI at the top of the screen.
    ///
    /// UI source (same idea as the other UI scripts):
    ///   - If "Ui Root" is assigned (Tools > Vehicle > Generate Pickup HUD, or the button on this component) the objects in
    ///     your Hierarchy are used: move, resize and restyle them freely. Add or remove squares in the list as you like.
    ///   - If it is empty, a default one is built at runtime.
    /// Only the square colours, the bar length and its visibility are written at runtime.
    /// </summary>
    public class PickupHud : MonoBehaviour
    {
        [Header("Vehicle (assigned by LocalPlayerRig)")]
        public VehicleBoost boost;

        [Header("UI (filled in by the editor tool)")]
        [Tooltip("Parent object of the whole panel (its Canvas). Hidden when the vehicle has no VehicleBoost.")]
        public GameObject uiRoot;
        [Tooltip("One per carriable battery. The ones beyond the vehicle's limit are hidden.")]
        public Image[] pips;
        [Tooltip("The bar and its fill: shown only while boosting. The fill's right anchor shows the time left.")]
        public GameObject boostBar;
        public RectTransform boostFill;

        [Header("Colours")]
        public Color fullColor = new Color(0.35f, 0.9f, 0.4f, 1f);
        public Color emptyColor = new Color(1f, 1f, 1f, 0.18f);
        public Color boostColor = new Color(1f, 0.85f, 0.3f, 1f);

        [Header("Default look (editor tool + runtime fallback)")]
        public int pipCount = 5;
        public int fontSize = 26;
        public Color panelColor = new Color(0f, 0f, 0f, 0.45f);

        /// <summary>Show the batteries of this car (null hides the panel). Called by LocalPlayerRig.</summary>
        public void SetVehicle(VehicleController car)
        {
            if (boost != null) boost.BatteriesChanged -= OnBatteries;

            boost = null;
            if (car != null) car.TryGetComponent(out boost);

            if (boost != null)
            {
                boost.BatteriesChanged += OnBatteries;
                OnBatteries(boost.Batteries);
            }

            if (uiRoot != null) uiRoot.SetActive(boost != null);
        }

        void Awake()
        {
            if (uiRoot == gameObject) uiRoot = null; // would switch this script off

            if (uiRoot == null || pips == null || pips.Length == 0)
            {
                if (uiRoot != null) Destroy(uiRoot); // half-assigned: start clean
                BuildUI();
            }

            var b = boost;                  // assigned in the inspector
            boost = null;
            if (b != null) SetVehicle(b.GetComponent<VehicleController>());
            else uiRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (boost != null) boost.BatteriesChanged -= OnBatteries;
        }

        void OnBatteries(int count)
        {
            int max = boost != null ? boost.maxBatteries : 0;
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                pips[i].gameObject.SetActive(i < max);
                pips[i].color = i < count ? fullColor : emptyColor;
            }
        }

        void Update()
        {
            if (boost == null || boostBar == null) return;

            bool on = boost.IsBoosting;
            if (boostBar.activeSelf != on) boostBar.SetActive(on);
            if (!on || boostFill == null) return;

            Vector2 max = boostFill.anchorMax;
            max.x = boost.BoostTimeTotal > 0f ? Mathf.Clamp01(boost.BoostTimeLeft / boost.BoostTimeTotal) : 0f;
            boostFill.anchorMax = max;
        }

        // ------------------------------------------------------------------ building the UI

        /// <summary>
        /// Creates the default panel under this object and fills the UI fields. Used at runtime when no UI is assigned,
        /// and by the editor tool. Sample values are filled in so you can style it in the editor.
        /// </summary>
        public void BuildUI()
        {
            var canvasGo = NewUI("Pickup HUD Canvas", transform,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 92;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.GetComponent<GraphicRaycaster>().enabled = false; // never blocks the driving buttons

            // ---- panel (right edge, above the buttons)
            var panel = NewUI("Battery Panel", canvasGo.transform, typeof(RectTransform), typeof(Image));
            Vector2 rightMid = new Vector2(1f, 0.5f);
            float width = Mathf.Max(260f, 24f + pipCount * 52f);
            Place((RectTransform)panel.transform, rightMid, rightMid, rightMid, new Vector2(-40f, 20f), new Vector2(width, 130f));
            var panelImg = panel.GetComponent<Image>();
            panelImg.color = panelColor;
            panelImg.raycastTarget = false;

            Vector2 topLeft = new Vector2(0f, 1f);

            var label = MakeText(panel.transform, "Label", "BATTERY  (boost)", FontStyle.Bold, new Color(1f, 1f, 1f, 0.8f), fontSize);
            Place((RectTransform)label.transform, topLeft, topLeft, topLeft, new Vector2(12f, -8f), new Vector2(width - 24f, 34f));
            label.alignment = TextAnchor.MiddleLeft;

            // ---- squares
            pips = new Image[pipCount];
            for (int i = 0; i < pipCount; i++)
            {
                var go = NewUI("Battery " + (i + 1), panel.transform, typeof(RectTransform), typeof(Image));
                Place((RectTransform)go.transform, topLeft, topLeft, topLeft, new Vector2(12f + i * 52f, -50f), new Vector2(44f, 44f));
                pips[i] = go.GetComponent<Image>();
                pips[i].color = i < 3 ? fullColor : emptyColor; // sample
                pips[i].raycastTarget = false;
            }

            // ---- boost time left
            var barBack = NewUI("Boost Bar", panel.transform, typeof(RectTransform), typeof(Image));
            Vector2 bottomCentre = new Vector2(0.5f, 0f);
            Place((RectTransform)barBack.transform, bottomCentre, bottomCentre, bottomCentre, new Vector2(0f, 10f), new Vector2(width - 24f, 14f));
            var barImg = barBack.GetComponent<Image>();
            barImg.color = new Color(1f, 1f, 1f, 0.15f);
            barImg.raycastTarget = false;
            boostBar = barBack;

            var fillGo = NewUI("Boost Fill", barBack.transform, typeof(RectTransform), typeof(Image));
            boostFill = (RectTransform)fillGo.transform;
            Stretch(boostFill);
            boostFill.anchorMax = new Vector2(0.6f, 1f); // sample value; set at runtime
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = boostColor;
            fillImg.raycastTarget = false;
        }

        static Text MakeText(Transform parent, string name, string text, FontStyle style, Color color, int size)
        {
            var go = NewUI(name, parent, typeof(RectTransform), typeof(Text));
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
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

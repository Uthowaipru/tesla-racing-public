using System;
using UnityEngine;
using UnityEngine.UI;

namespace VehicleSystem
{
    /// <summary>
    /// Health bar for the vehicle the local player drives. LocalPlayerRig.Attach() gives it the car (SetVehicle), so there
    /// is nothing to assign by hand. The bar hides itself when the car has no VehicleHealth.
    ///
    /// UI source (same idea as the other UI scripts):
    ///   - If "Ui Root" is assigned (Tools > Vehicle > Generate Health Bar UI, or the button on this component) the
    ///     objects in your Hierarchy are used: move, resize and restyle them freely.
    ///   - If it is empty, a default bar is built at runtime.
    /// Only the bar length, its colour and the number are written at runtime.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [Header("Vehicle (assigned by LocalPlayerRig)")]
        public VehicleHealth health;

        [Header("UI (filled in by the editor tool)")]
        [Tooltip("Parent object of the whole bar (its Canvas).")]
        public GameObject uiRoot;
        [Tooltip("Resized at runtime (its right anchor = health). Keep its Anchor Min at 0,0.")]
        public RectTransform fill;
        [Tooltip("Optional number on the bar.")]
        public Text valueText;

        [Header("Colours")]
        public Color fullColor = new Color(0.35f, 0.85f, 0.35f, 1f);
        public Color lowColor = new Color(0.95f, 0.25f, 0.2f, 1f);
        [Tooltip("The flash colour when damage is taken.")]
        public Color damageFlashColor = Color.white;
        public float flashTime = 0.15f;

        [Header("Default look (editor tool + runtime fallback)")]
        public int fontSize = 28;
        public Color panelColor = new Color(0f, 0f, 0f, 0.45f);

        Image fillImage;
        float shown = 1f, flash;
        int lastValue = -1;

        /// <summary>Show the health of this car (null hides the bar). Called by LocalPlayerRig.</summary>
        public void SetVehicle(VehicleController car)
        {
            if (health != null) health.Damaged -= OnDamaged;

            health = null;
            if (car != null) car.TryGetComponent(out health);

            if (health != null) health.Damaged += OnDamaged;

            shown = health != null ? health.Health01 : 0f;
            lastValue = -1;
            if (uiRoot != null) uiRoot.SetActive(health != null);
        }

        void Awake()
        {
            if (uiRoot == gameObject) uiRoot = null; // would switch this script off

            if (uiRoot == null || fill == null)
            {
                if (uiRoot != null) Destroy(uiRoot); // half-assigned: start clean
                BuildUI();
            }

            fillImage = fill.GetComponent<Image>();

            var h = health;                 // assigned in the inspector
            health = null;
            if (h != null) SetVehicle(h.GetComponent<VehicleController>());
            else uiRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        void OnDamaged(float amount, ImpactInfo info) => flash = flashTime;

        void Update()
        {
            if (health == null) return;

            float target = health.Health01;
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 2f); // eases down and up

            Vector2 max = fill.anchorMax;
            max.x = shown;
            fill.anchorMax = max;

            if (fillImage != null)
            {
                Color c = Color.Lerp(lowColor, fullColor, Mathf.Clamp01(shown * 1.4f));
                if (flash > 0f)
                {
                    flash -= Time.unscaledDeltaTime;
                    c = Color.Lerp(c, damageFlashColor, Mathf.Clamp01(flash / flashTime));
                }
                fillImage.color = c;
            }

            if (valueText != null)
            {
                int value = Mathf.CeilToInt(health.Health);
                if (value != lastValue) { lastValue = value; valueText.text = value.ToString(); }
            }
        }

        // ------------------------------------------------------------------ building the UI

        /// <summary>
        /// Creates the default health bar under this object and fills the UI fields. Used at runtime when no UI is
        /// assigned, and by the editor tool. Sample values are filled in so you can style it in the editor.
        /// </summary>
        public void BuildUI()
        {
            var canvasGo = NewUI("Health Bar Canvas", transform,
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

            // ---- panel (top centre, under the repair counter)
            var panel = NewUI("Health Panel", canvasGo.transform, typeof(RectTransform), typeof(Image));
            Vector2 topCentre = new Vector2(0.5f, 1f);
            Place((RectTransform)panel.transform, topCentre, topCentre, topCentre, new Vector2(0f, -100f), new Vector2(520f, 54f));
            var panelImg = panel.GetComponent<Image>();
            panelImg.color = panelColor;
            panelImg.raycastTarget = false;

            // ---- bar
            var back = NewUI("Bar Background", panel.transform, typeof(RectTransform), typeof(Image));
            var backRt = (RectTransform)back.transform;
            Stretch(backRt);
            backRt.offsetMin = new Vector2(8f, 8f);
            backRt.offsetMax = new Vector2(-8f, -8f);
            var backImg = back.GetComponent<Image>();
            backImg.color = new Color(1f, 1f, 1f, 0.15f);
            backImg.raycastTarget = false;

            var fillGo = NewUI("Bar Fill", back.transform, typeof(RectTransform), typeof(Image));
            fill = (RectTransform)fillGo.transform;
            Stretch(fill);
            fill.anchorMax = new Vector2(0.8f, 1f); // sample value; set at runtime
            fillImage = fillGo.GetComponent<Image>();
            fillImage.color = fullColor;
            fillImage.raycastTarget = false;

            // ---- number
            valueText = MakeText(back.transform, "Value", "100", FontStyle.Bold, Color.white, fontSize, TextAnchor.MiddleCenter);
            Stretch((RectTransform)valueText.transform);
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

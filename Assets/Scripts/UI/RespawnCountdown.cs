using System;
using UnityEngine;
using UnityEngine.UI;

namespace VehicleSystem
{
    /// <summary>
    /// "DESTROYED  Respawn in 3" in the middle of the screen while the local player's vehicle is destroyed.
    /// LocalPlayerRig.Attach() gives it the car (SetVehicle). It shows itself on VehicleHealth.Destroyed and hides on Respawned.
    ///
    /// UI source (same idea as the other UI scripts):
    ///   - If "Ui Root" is assigned (Tools > Vehicle > Generate Respawn Countdown UI, or the button on this component) the
    ///     objects in your Hierarchy are used: move, resize and restyle them freely.
    ///   - If it is empty, a default one is built at runtime.
    /// Only the texts are written at runtime.
    /// </summary>
    public class RespawnCountdown : MonoBehaviour
    {
        [Header("Vehicle (assigned by LocalPlayerRig)")]
        public VehicleHealth health;

        [Header("UI (filled in by the editor tool)")]
        [Tooltip("Parent object of the whole screen (its Canvas). Switched on while the vehicle is destroyed.")]
        public GameObject uiRoot;
        public Text titleText;
        public Text countdownText;

        [Header("Texts")]
        public string title = "DESTROYED";
        [Tooltip("{0} is replaced by the seconds left.")]
        public string countdownFormat = "Respawn in {0}";

        [Header("Default look (editor tool + runtime fallback)")]
        public int titleSize = 90;
        public int countdownSize = 60;
        public Color titleColor = new Color(1f, 0.3f, 0.25f, 1f);
        public Color dimColor = new Color(0f, 0f, 0f, 0.45f);

        int lastShown = -1;

        /// <summary>Show the countdown of this car (null = nothing). Called by LocalPlayerRig.</summary>
        public void SetVehicle(VehicleController car)
        {
            if (health != null)
            {
                health.Destroyed -= OnDestroyed;
                health.Respawned -= OnRespawned;
            }

            health = null;
            if (car != null) car.TryGetComponent(out health);

            if (health != null)
            {
                health.Destroyed += OnDestroyed;
                health.Respawned += OnRespawned;
            }

            SetShown(health != null && health.IsDestroyed);
        }

        void Awake()
        {
            if (uiRoot == gameObject) uiRoot = null; // would switch this script off

            if (uiRoot == null || titleText == null || countdownText == null)
            {
                if (uiRoot != null) Destroy(uiRoot); // half-assigned: start clean
                BuildUI();
            }

            var h = health;                 // assigned in the inspector
            health = null;
            if (h != null) SetVehicle(h.GetComponent<VehicleController>());
            else uiRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (health != null)
            {
                health.Destroyed -= OnDestroyed;
                health.Respawned -= OnRespawned;
            }
        }

        void OnDestroyed(ImpactInfo cause) => SetShown(true);
        void OnRespawned() => SetShown(false);

        void SetShown(bool shown)
        {
            if (uiRoot == null) return;
            uiRoot.SetActive(shown);
            lastShown = -1;
            if (shown) titleText.text = title;
        }

        void Update()
        {
            if (health == null || !uiRoot.activeSelf) return;

            int seconds = Mathf.Max(1, Mathf.CeilToInt(health.RespawnTimeLeft));
            if (seconds != lastShown)
            {
                lastShown = seconds;
                countdownText.text = string.Format(countdownFormat, seconds);
            }
        }

        // ------------------------------------------------------------------ building the UI

        /// <summary>
        /// Creates the default screen under this object and fills the UI fields. Used at runtime when no UI is assigned,
        /// and by the editor tool. Sample text is filled in so you can style it in the editor.
        /// </summary>
        public void BuildUI()
        {
            var canvasGo = NewUI("Respawn Countdown Canvas", transform,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.GetComponent<GraphicRaycaster>().enabled = false;

            // ---- dim the screen a little
            var dim = NewUI("Dim", canvasGo.transform, typeof(RectTransform), typeof(Image));
            Stretch((RectTransform)dim.transform);
            var dimImg = dim.GetComponent<Image>();
            dimImg.color = dimColor;
            dimImg.raycastTarget = false;

            Vector2 mid = new Vector2(0.5f, 0.5f);

            titleText = MakeText(canvasGo.transform, "Title", title, FontStyle.Bold, titleColor, titleSize);
            Place((RectTransform)titleText.transform, mid, new Vector2(0f, 60f), new Vector2(1400f, 130f));

            countdownText = MakeText(canvasGo.transform, "Countdown", string.Format(countdownFormat, 3), FontStyle.Bold, Color.white, countdownSize);
            Place((RectTransform)countdownText.transform, mid, new Vector2(0f, -50f), new Vector2(1400f, 100f));
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

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
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

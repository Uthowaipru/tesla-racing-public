using System;
using UnityEngine;
using UnityEngine.UI;

namespace VehicleSystem
{
    /// <summary>
    /// "WRONG WAY x2" while the local player drives on the oncoming road, and a "+50 CROSSING BONUS" pop-up when they switch
    /// roads. LocalPlayerRig gives it the WrongWayTracker (SetTracker). It never shows on a one-way road.
    ///
    /// UI source (same idea as the other UI scripts):
    ///   - If "Ui Root" is assigned (Tools > Vehicle > Generate Wrong Way Banner UI, or the button on this component) the
    ///     objects in your Hierarchy are used: move, resize and restyle them freely.
    ///   - If it is empty, a default one is built at runtime.
    /// Only the texts, their colours' transparency and visibility are written at runtime.
    /// </summary>
    public class WrongWayBanner : MonoBehaviour
    {
        [Header("Source (assigned by LocalPlayerRig)")]
        public WrongWayTracker tracker;

        [Header("UI (filled in by the editor tool)")]
        [Tooltip("Parent object of the banner (its Canvas).")]
        public GameObject uiRoot;
        public Text wrongWayText;
        public Text bonusText;

        [Header("Texts")]
        [Tooltip("{0} is replaced by the score multiplier.")]
        public string wrongWayFormat = "WRONG WAY  x{0}";
        [Tooltip("{0} is replaced by the bonus points.")]
        public string bonusFormat = "+{0}  CROSSING BONUS";
        [Tooltip("Pulses per second of the wrong way text.")]
        public float pulseSpeed = 3f;
        [Tooltip("Seconds the bonus pop-up stays on screen.")]
        public float bonusTime = 1.8f;

        [Header("Default look (editor tool + runtime fallback)")]
        public int wrongWaySize = 64;
        public int bonusSize = 48;
        public Color wrongWayColor = new Color(1f, 0.3f, 0.25f, 1f);
        public Color bonusColor = new Color(1f, 0.85f, 0.3f, 1f);

        float bonusTimer;

        /// <summary>The tracker to show (null = nothing). Called by LocalPlayerRig.</summary>
        public void SetTracker(WrongWayTracker newTracker)
        {
            Unsubscribe();
            tracker = newTracker;
            if (tracker != null)
            {
                tracker.CrossedToOncoming += OnCrossed;
                tracker.CrossedToForward += OnCrossed;
            }
            bonusTimer = 0f;
        }

        void Unsubscribe()
        {
            if (tracker == null) return;
            tracker.CrossedToOncoming -= OnCrossed;
            tracker.CrossedToForward -= OnCrossed;
        }

        void Awake()
        {
            if (uiRoot == gameObject) uiRoot = null; // would switch this script off

            if (uiRoot == null || wrongWayText == null || bonusText == null)
            {
                if (uiRoot != null) Destroy(uiRoot); // half-assigned: start clean
                BuildUI();
            }

            var t = tracker;               // assigned in the inspector
            tracker = null;
            SetTracker(t);

            wrongWayText.gameObject.SetActive(false);
            bonusText.gameObject.SetActive(false);
        }

        void OnDestroy() => Unsubscribe();

        void OnCrossed(int bonus)
        {
            if (bonus <= 0) return; // no bonus this time (cooldown, or switching back)
            bonusText.text = string.Format(bonusFormat, bonus);
            bonusTimer = bonusTime;
        }

        void Update()
        {
            bool wrongWay = tracker != null && tracker.IsWrongWay;

            if (wrongWayText.gameObject.activeSelf != wrongWay) wrongWayText.gameObject.SetActive(wrongWay);
            if (wrongWay)
            {
                wrongWayText.text = string.Format(wrongWayFormat, tracker.ScoreMultiplier.ToString("0.#"));
                Color c = wrongWayColor;
                c.a = Mathf.Lerp(0.55f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f));
                wrongWayText.color = c;
            }

            bool showBonus = bonusTimer > 0f;
            if (bonusText.gameObject.activeSelf != showBonus) bonusText.gameObject.SetActive(showBonus);
            if (showBonus)
            {
                bonusTimer -= Time.unscaledDeltaTime;
                Color c = bonusColor;
                c.a = Mathf.Clamp01(bonusTimer / 0.5f); // fades during the last half second
                bonusText.color = c;
            }
        }

        // ------------------------------------------------------------------ building the UI

        /// <summary>
        /// Creates the default banner under this object and fills the UI fields. Used at runtime when no UI is assigned, and by
        /// the editor tool. Sample text is filled in so you can style it in the editor.
        /// </summary>
        public void BuildUI()
        {
            var canvasGo = NewUI("Wrong Way Canvas", transform,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 93;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.GetComponent<GraphicRaycaster>().enabled = false; // never blocks the driving buttons

            Vector2 topCentre = new Vector2(0.5f, 1f);

            wrongWayText = MakeText(canvasGo.transform, "Wrong Way", string.Format(wrongWayFormat, "2"), wrongWayColor, wrongWaySize);
            Place((RectTransform)wrongWayText.transform, topCentre, new Vector2(0f, -200f), new Vector2(1200f, 90f));

            bonusText = MakeText(canvasGo.transform, "Crossing Bonus", string.Format(bonusFormat, 50), bonusColor, bonusSize);
            Place((RectTransform)bonusText.transform, topCentre, new Vector2(0f, -290f), new Vector2(1200f, 70f));
        }

        static Text MakeText(Transform parent, string name, string text, Color color, int size)
        {
            var go = NewUI(name, parent, typeof(RectTransform), typeof(Text));
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
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
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
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

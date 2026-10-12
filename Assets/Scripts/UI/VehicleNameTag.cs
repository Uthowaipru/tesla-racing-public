using System;
using UnityEngine;
using UnityEngine.UI;

namespace VehicleSystem
{
    /// <summary>
    /// Floating name and health bar above a vehicle, visible to every player. It turns to face the camera, keeps a readable
    /// size at any distance and hides while the vehicle is destroyed. It reads the vehicle's VehicleHealth, so other
    /// players' vehicles show their health as soon as the network feeds it (VehicleHealth.SetHealthFromNetwork).
    ///
    /// By default the local player's own vehicle has no tag (they already have the health bar on the HUD): tick
    /// "Show On Local Vehicle" to show it.
    ///
    /// UI source (same idea as the other UI scripts):
    ///   - Tools > Vehicle > Add Damage And Effects To Selected Vehicle builds the tag as real objects inside the vehicle
    ///     prefab. Move, resize and restyle them freely. At runtime the canvas lets go of the vehicle (so the vehicle's scale
    ///     does not shrink it) and follows it.
    ///   - If it is missing, a default one is built at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleController))]
    public class VehicleNameTag : MonoBehaviour
    {
        [Header("Name")]
        public string playerName = "Player";

        [Header("Visibility")]
        [Tooltip("Show the tag on the vehicle the local player drives.")]
        public bool showOnLocalVehicle = false;
        [Tooltip("Hide the tag when the camera is further away than this (meters).")]
        public float maxDistance = 60f;
        [Tooltip("Hide the tag while the vehicle is destroyed.")]
        public bool hideWhenDestroyed = true;

        [Header("Size")]
        [Tooltip("World size of one UI pixel at the reference distance.")]
        public float pixelSize = 0.005f;
        [Tooltip("The tag has its normal size at this distance (meters), and scales with distance around it.")]
        public float referenceDistance = 10f;
        public float minScale = 0.7f;
        public float maxScale = 3f;

        [Header("Position (when the UI is built)")]
        [Tooltip("Meters above the top of the vehicle.")]
        public float heightAboveVehicle = 0.5f;

        [Header("UI (filled in by the editor tool)")]
        [Tooltip("Parent object of the tag (a World Space canvas).")]
        public GameObject uiRoot;
        public Text nameText;
        [Tooltip("Resized at runtime (its right anchor = health). Keep its Anchor Min at 0,0.")]
        public RectTransform fill;

        [Header("Colours")]
        public Color fullColor = new Color(0.35f, 0.85f, 0.35f, 1f);
        public Color lowColor = new Color(0.95f, 0.25f, 0.2f, 1f);
        public Color nameColor = Color.white;

        VehicleHealth health;
        Image fillImage;
        Transform cam;
        bool isLocal, shown = true;
        float heightWorld;
        float lastRatio = -1f;

        public void SetName(string newName)
        {
            playerName = newName;
            if (nameText != null) nameText.text = newName;
        }

        /// <summary>Called by LocalPlayerRig: this is the vehicle the local player drives.</summary>
        public void SetIsLocalPlayer(bool value) => isLocal = value;

        void Awake()
        {
            health = GetComponent<VehicleHealth>();

            if (uiRoot == null || nameText == null || fill == null)
            {
                if (uiRoot != null) Destroy(uiRoot);
                BuildUI();
            }

            fillImage = fill.GetComponent<Image>();
            nameText.text = playerName;

            // let go of the vehicle: its scale must not shrink the tag, and it must not tilt with it
            heightWorld = uiRoot.transform.position.y - transform.position.y;
            uiRoot.transform.SetParent(null, true);
            uiRoot.transform.localScale = Vector3.one * pixelSize;
        }

        void OnDestroy()
        {
            if (uiRoot != null) Destroy(uiRoot);
        }

        void LateUpdate()
        {
            if (uiRoot == null) return;

            if (cam == null)
            {
                var main = Camera.main;
                if (main != null) cam = main.transform;
                else { SetShown(false); return; }
            }

            Vector3 position = transform.position + Vector3.up * heightWorld;
            float distance = Vector3.Distance(cam.position, position);
            bool inFront = Vector3.Dot(cam.forward, position - cam.position) > 0f;

            bool visible = (!isLocal || showOnLocalVehicle)
                           && distance <= maxDistance && inFront
                           && !(hideWhenDestroyed && health != null && health.IsDestroyed);
            SetShown(visible);
            if (!visible) return;

            uiRoot.transform.position = position;
            uiRoot.transform.rotation = cam.rotation;
            uiRoot.transform.localScale = Vector3.one * (pixelSize * Mathf.Clamp(distance / referenceDistance, minScale, maxScale));

            float ratio = health != null ? health.Health01 : 1f;
            if (!Mathf.Approximately(ratio, lastRatio))
            {
                lastRatio = ratio;
                Vector2 max = fill.anchorMax;
                max.x = ratio;
                fill.anchorMax = max;
                if (fillImage != null) fillImage.color = Color.Lerp(lowColor, fullColor, Mathf.Clamp01(ratio * 1.4f));
            }
        }

        void SetShown(bool value)
        {
            if (shown == value) return;
            shown = value;
            uiRoot.SetActive(value);
        }

        // ------------------------------------------------------------------ building the UI

        /// <summary>
        /// Creates the default tag under this vehicle (a World Space canvas above it) and fills the UI fields. Used at
        /// runtime when no UI is assigned, and by the editor tool. Sample values are filled in.
        /// </summary>
        public void BuildUI()
        {
            // where is the top of the vehicle?
            float top = 1.6f;
            bool has = false;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                top = has ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
                has = true;
            }
            float worldY = has ? top + heightAboveVehicle : transform.position.y + 2f;

            var canvasGo = NewUI("Name Tag Canvas", transform, typeof(RectTransform), typeof(Canvas));
            uiRoot = canvasGo;
            canvasGo.transform.position = new Vector3(transform.position.x, worldY, transform.position.z);
            canvasGo.transform.localScale = Vector3.one * pixelSize / Mathf.Max(0.0001f, transform.lossyScale.y); // looks right in the editor

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRt = (RectTransform)canvasGo.transform;
            canvasRt.sizeDelta = new Vector2(260f, 70f);

            Vector2 topCentre = new Vector2(0.5f, 1f);

            nameText = MakeText(canvasGo.transform, "Name", playerName, FontStyle.Bold, nameColor, 34);
            Place((RectTransform)nameText.transform, topCentre, new Vector2(0f, 0f), new Vector2(260f, 40f));

            var back = NewUI("Bar Background", canvasGo.transform, typeof(RectTransform), typeof(Image));
            Place((RectTransform)back.transform, topCentre, new Vector2(0f, -46f), new Vector2(220f, 16f));
            var backImg = back.GetComponent<Image>();
            backImg.color = new Color(0f, 0f, 0f, 0.6f);
            backImg.raycastTarget = false;

            var fillGo = NewUI("Bar Fill", back.transform, typeof(RectTransform), typeof(Image));
            fill = (RectTransform)fillGo.transform;
            Stretch(fill);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);
            fill.anchorMax = new Vector2(0.8f, 1f); // sample value; set at runtime
            fillImage = fillGo.GetComponent<Image>();
            fillImage.color = fullColor;
            fillImage.raycastTarget = false;
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
            rt.pivot = anchor;
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

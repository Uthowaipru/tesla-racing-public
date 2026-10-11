using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace VehicleSystem
{
    public enum VehicleStatBar { TopSpeed, Power, Grip, Braking }

    /// <summary>One stat row of the selector: a text line and a bar whose length follows the value.</summary>
    [Serializable]
    public class StatBarUI
    {
        public VehicleStatBar stat;
        public Text label;
        [Tooltip("Its right anchor is set at runtime: value / best value in the catalog. Keep its Anchor Min at 0,0.")]
        public RectTransform fill;
    }

    /// <summary>
    /// Vehicle selector screen: a spinning 3D preview, name, stat bars, left / right arrows and SELECT.
    ///
    ///   Browse  : < > buttons, A D / arrow keys, gamepad D-pad or left stick. Drag the picture to turn the car.
    ///   Select  : SELECT button, Enter / Space, gamepad A / Cross. Saves the id in PlayerProfile.
    ///   Back    : BACK button, Esc, gamepad B / Circle.
    ///
    /// Put this in your MENU scene, assign a VehicleCatalog, and set "Next Scene" (e.g. your map select or race
    /// scene). The race scene's VehicleSpawner reads the saved choice.
    ///
    /// UI source:
    ///   - If "Ui Root" is assigned (made with Tools > Vehicle > Generate Vehicle Select UI, or the button on this
    ///     component) the objects in your Hierarchy are used: move, resize, restyle, rename them freely.
    ///   - If it is empty, a default screen is built at runtime.
    /// Only the TEXT, the stat bar lengths and the preview picture are written at runtime. Button clicks are
    /// connected in code, so you never have to wire up OnClick events.
    /// </summary>
    public class VehicleSelectMenu : MonoBehaviour
    {
        [Header("Data")]
        public VehicleCatalog catalog;

        [Header("Flow")]
        [Tooltip("Scene loaded by SELECT (add it to the Build Settings). Empty = only raise the Confirmed event.")]
        public string nextScene = "";
        [Tooltip("Scene loaded by BACK. Empty = hide this screen and raise the Cancelled event.")]
        public string backScene = "";
        public bool wrapAround = true;
        [Tooltip("Show the screen when the scene starts. Off = call Show() yourself (e.g. from your main menu).")]
        public bool showOnStart = true;

        [Header("UI (filled in by the editor tool)")]
        [Tooltip("Parent object of the whole screen (its Canvas).")]
        public GameObject uiRoot;
        [Tooltip("Shows the 3D preview. Dragging it turns the vehicle.")]
        public RawImage previewImage;
        public Text nameText;
        public Text counterText;
        public Button prevButton;
        public Button nextButton;
        public Button selectButton;
        public Button backButton;
        public List<StatBarUI> statBars = new List<StatBarUI>();
        [Tooltip("The studio that renders the preview. Created for you if empty.")]
        public VehiclePreviewStage previewStage;

        [Header("Default look (editor tool + runtime fallback)")]
        public int fontSize = 36;
        public Color backgroundColor = new Color(0.07f, 0.08f, 0.11f, 1f);
        public Color panelColor = new Color(0.1f, 0.1f, 0.14f, 0.96f);
        public Color buttonColor = new Color(0.22f, 0.22f, 0.3f, 1f);
        public Color buttonHighlightColor = new Color(0.34f, 0.34f, 0.5f, 1f);
        public Color buttonPressedColor = new Color(0.5f, 0.5f, 0.7f, 1f);
        public Color accentColor = new Color(1f, 0.85f, 0.3f, 1f);
        [Tooltip("Colour of the preview picture in the editor, before the 3D picture exists.")]
        public Color previewPlaceholderColor = new Color(0.13f, 0.14f, 0.19f, 1f);

        /// <summary>Raised when the player confirms a vehicle (after it is saved in PlayerProfile).</summary>
        public event Action<VehicleDefinition> Confirmed;
        /// <summary>Raised when the player goes back.</summary>
        public event Action Cancelled;

        readonly List<VehicleDefinition> items = new List<VehicleDefinition>();
        int index;
        int lastHandledFrame = -1;

        public VehicleDefinition Current => index >= 0 && index < items.Count ? items[index] : null;

#if ENABLE_INPUT_SYSTEM
        InputAction prevAction, nextAction, confirmAction, backAction;
#endif

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            if (uiRoot == gameObject) uiRoot = null; // would switch this script off

            if (!UiComplete())
            {
                if (uiRoot != null) Destroy(uiRoot); // half-assigned: start clean
                BuildUI();
            }

            if (previewStage == null) previewStage = CreateStage();
            EnsureEventSystem();

            // the preview picture can be dragged to turn the car
            if (!previewImage.TryGetComponent(out VehiclePreviewDrag drag))
                drag = previewImage.gameObject.AddComponent<VehiclePreviewDrag>();
            drag.stage = previewStage;

            prevButton.onClick.AddListener(() => { Step(-1); ClearSelection(); });
            nextButton.onClick.AddListener(() => { Step(+1); ClearSelection(); });
            selectButton.onClick.AddListener(() => { Confirm(); ClearSelection(); });
            backButton.onClick.AddListener(() => { Back(); ClearSelection(); });

#if ENABLE_INPUT_SYSTEM
            prevAction = new InputAction("PrevVehicle", InputActionType.Button);
            prevAction.AddBinding("<Keyboard>/a");
            prevAction.AddBinding("<Keyboard>/leftArrow");
            prevAction.AddBinding("<Gamepad>/dpad/left");
            prevAction.AddBinding("<Gamepad>/leftStick/left");

            nextAction = new InputAction("NextVehicle", InputActionType.Button);
            nextAction.AddBinding("<Keyboard>/d");
            nextAction.AddBinding("<Keyboard>/rightArrow");
            nextAction.AddBinding("<Gamepad>/dpad/right");
            nextAction.AddBinding("<Gamepad>/leftStick/right");

            confirmAction = new InputAction("ConfirmVehicle", InputActionType.Button);
            confirmAction.AddBinding("<Keyboard>/enter");
            confirmAction.AddBinding("<Keyboard>/space");
            confirmAction.AddBinding("<Gamepad>/buttonSouth");

            backAction = new InputAction("BackFromVehicles", InputActionType.Button);
            backAction.AddBinding("<Keyboard>/escape");
            backAction.AddBinding("<Gamepad>/buttonEast");
#endif
        }

        void Start()
        {
            if (showOnStart) Show();
            else Hide();
        }

#if ENABLE_INPUT_SYSTEM
        void OnEnable()
        {
            prevAction?.Enable();
            nextAction?.Enable();
            confirmAction?.Enable();
            backAction?.Enable();
        }

        void OnDisable()
        {
            prevAction?.Disable();
            nextAction?.Disable();
            confirmAction?.Disable();
            backAction?.Disable();
        }

        void OnDestroy()
        {
            prevAction?.Dispose();
            nextAction?.Dispose();
            confirmAction?.Dispose();
            backAction?.Dispose();
        }
#endif

        bool UiComplete() =>
            uiRoot != null && previewImage != null && nameText != null && counterText != null &&
            prevButton != null && nextButton != null && selectButton != null && backButton != null;

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

        // A clicked button stays "selected" in the EventSystem, and Enter would then click it a second time.
        static void ClearSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        // ------------------------------------------------------------------ show / hide

        public void Show()
        {
            Reload();
            uiRoot.SetActive(true);
            if (previewStage != null) previewStage.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (uiRoot != null) uiRoot.SetActive(false);
            if (previewStage != null) previewStage.gameObject.SetActive(false);
        }

        void Reload()
        {
            items.Clear();
            if (catalog != null)
                for (int i = 0; i < catalog.vehicles.Count; i++)
                    if (catalog.vehicles[i] != null) items.Add(catalog.vehicles[i]);

            // start on what the player chose last time
            index = 0;
            string saved = PlayerProfile.SelectedVehicleId;
            for (int i = 0; i < items.Count; i++)
                if (items[i].id == saved) { index = i; break; }
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (uiRoot == null || !uiRoot.activeInHierarchy) return;

            bool prev, next, confirm, back;
#if ENABLE_INPUT_SYSTEM
            prev = prevAction.WasPressedThisFrame();
            next = nextAction.WasPressedThisFrame();
            confirm = confirmAction.WasPressedThisFrame();
            back = backAction.WasPressedThisFrame();
#else
            prev = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
            next = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
            confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
            back = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (prev) Step(-1);
            else if (next) Step(+1);
            else if (confirm) Confirm();
            else if (back) Back();
        }

        // ------------------------------------------------------------------ actions

        // one action per frame, so a key press and a UI click can never both fire
        bool TakeFrame()
        {
            if (lastHandledFrame == Time.frameCount) return false;
            lastHandledFrame = Time.frameCount;
            return true;
        }

        void Step(int direction)
        {
            if (items.Count < 2 || !TakeFrame()) return;

            index += direction;
            if (wrapAround) index = (index + items.Count) % items.Count;
            else index = Mathf.Clamp(index, 0, items.Count - 1);
            Refresh();
        }

        void Confirm()
        {
            var def = Current;
            if (def == null || !TakeFrame()) return;

            PlayerProfile.SelectedVehicleId = def.id;
            Confirmed?.Invoke(def);
            if (!string.IsNullOrEmpty(nextScene)) LoadScene(nextScene);
        }

        void Back()
        {
            if (!TakeFrame()) return;

            Cancelled?.Invoke();
            if (!string.IsNullOrEmpty(backScene)) LoadScene(backScene);
            else Hide();
        }

        void LoadScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"VehicleSelectMenu: scene '{sceneName}' is not in the Build Settings (or the name is wrong).", this);
                return;
            }
            SceneManager.LoadScene(sceneName);
        }

        // ------------------------------------------------------------------ content

        void Refresh()
        {
            int n = items.Count;

            selectButton.interactable = n > 0;
            prevButton.gameObject.SetActive(n > 1);
            nextButton.gameObject.SetActive(n > 1);

            if (n == 0)
            {
                nameText.text = "NO VEHICLES";
                counterText.text = "";
                previewStage.Show(null);
                Debug.LogWarning("VehicleSelectMenu: the catalog is empty or not assigned.", this);
                return;
            }

            var def = Current;
            nameText.text = def.displayName.ToUpperInvariant();
            counterText.text = $"{index + 1} / {n}";

            for (int i = 0; i < statBars.Count; i++)
            {
                var bar = statBars[i];
                if (bar == null) continue;

                float best = 0f;
                for (int v = 0; v < items.Count; v++) best = Mathf.Max(best, StatValue(items[v], bar.stat));
                float value = StatValue(def, bar.stat);

                if (bar.label != null) bar.label.text = $"{StatName(bar.stat)}   {StatText(def, bar.stat)}";
                if (bar.fill != null)
                {
                    Vector2 max = bar.fill.anchorMax;
                    max.x = best > 0f ? Mathf.Clamp(value / best, 0.04f, 1f) : 1f;
                    bar.fill.anchorMax = max;
                }
            }

            previewStage.Show(def);
            previewImage.texture = previewStage.Texture;
            previewImage.color = Color.white; // the editor shows a dark placeholder colour instead
        }

        static float StatValue(VehicleDefinition d, VehicleStatBar stat)
        {
            switch (stat)
            {
                case VehicleStatBar.TopSpeed: return d.maxSpeedKmh;
                case VehicleStatBar.Power:    return d.motorTorque;
                case VehicleStatBar.Grip:     return d.tireGrip;
                case VehicleStatBar.Braking:  return d.brakeTorque;
                default: return 0f;
            }
        }

        static string StatName(VehicleStatBar stat)
        {
            switch (stat)
            {
                case VehicleStatBar.TopSpeed: return "TOP SPEED";
                case VehicleStatBar.Power:    return "POWER";
                case VehicleStatBar.Grip:     return "GRIP";
                case VehicleStatBar.Braking:  return "BRAKING";
                default: return stat.ToString().ToUpperInvariant();
            }
        }

        static string StatText(VehicleDefinition d, VehicleStatBar stat)
        {
            switch (stat)
            {
                case VehicleStatBar.TopSpeed: return $"{d.maxSpeedKmh:0} km/h";
                case VehicleStatBar.Power:    return $"{d.motorTorque:0}";
                case VehicleStatBar.Grip:     return $"{d.tireGrip:0.00}";
                case VehicleStatBar.Braking:  return $"{d.brakeTorque:0}";
                default: return "";
            }
        }

        // ------------------------------------------------------------------ building the UI

        VehiclePreviewStage CreateStage()
        {
            var go = new GameObject("Vehicle Preview Stage");
            go.transform.SetParent(transform, false);
            var stage = go.AddComponent<VehiclePreviewStage>();
            stage.BuildStage();
            return stage;
        }

        /// <summary>
        /// Creates the default screen under this object (and the preview stage, if there is none) and fills the UI
        /// fields. Used at runtime when no UI is assigned, and by the editor tool. Sample text is filled in so you
        /// can style it in the editor; it is replaced at runtime.
        /// </summary>
        public void BuildUI()
        {
            if (previewStage == null) previewStage = CreateStage();

            var canvasGo = NewUI("Vehicle Select Canvas", transform,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasGo.transform;
            Vector2 mid = new Vector2(0.5f, 0.5f);

            // ---- background
            var bg = NewUI("Background", root, typeof(RectTransform), typeof(Image));
            Stretch((RectTransform)bg.transform);
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = backgroundColor;
            bgImg.raycastTarget = true;

            // ---- title
            var title = MakeText(root, "Title", "SELECT VEHICLE", FontStyle.Bold, accentColor, fontSize + 20, TextAnchor.MiddleCenter);
            Place((RectTransform)title.transform, mid, mid, mid, new Vector2(0f, 470f), new Vector2(1200f, 100f));

            // ---- 3D preview picture (drag to turn)
            var preview = NewUI("Preview", root, typeof(RectTransform), typeof(RawImage));
            Place((RectTransform)preview.transform, mid, mid, mid, new Vector2(-240f, 90f), new Vector2(800f, 600f));
            previewImage = preview.GetComponent<RawImage>();
            previewImage.color = previewPlaceholderColor;
            previewImage.raycastTarget = true;
            var drag = preview.AddComponent<VehiclePreviewDrag>();
            drag.stage = previewStage;

            // ---- arrows
            prevButton = MakeButton(root, "<", mid, new Vector2(-770f, 90f), new Vector2(150f, 150f), fontSize + 28);
            nextButton = MakeButton(root, ">", mid, new Vector2(290f, 90f), new Vector2(150f, 150f), fontSize + 28);

            // ---- name + counter
            nameText = MakeText(root, "Vehicle Name", "VEHICLE", FontStyle.Bold, Color.white, fontSize + 16, TextAnchor.MiddleCenter);
            Place((RectTransform)nameText.transform, mid, mid, mid, new Vector2(-240f, -260f), new Vector2(800f, 80f));

            counterText = MakeText(root, "Counter", "1 / 2", FontStyle.Normal, new Color(1f, 1f, 1f, 0.6f), fontSize - 6, TextAnchor.MiddleCenter);
            Place((RectTransform)counterText.transform, mid, mid, mid, new Vector2(-240f, -325f), new Vector2(400f, 50f));

            // ---- select / back
            selectButton = MakeButton(root, "SELECT", mid, new Vector2(-240f, -430f), new Vector2(460f, 110f), fontSize + 4);
            backButton = MakeButton(root, "BACK", new Vector2(0f, 0f), new Vector2(200f, 80f), new Vector2(240f, 90f), fontSize - 4);

            // ---- stats panel
            var panel = NewUI("Stats Panel", root, typeof(RectTransform), typeof(Image));
            Place((RectTransform)panel.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                  new Vector2(-60f, 90f), new Vector2(480f, 420f));
            var panelImg = panel.GetComponent<Image>();
            panelImg.color = panelColor;
            panelImg.raycastTarget = false;

            statBars = new List<StatBarUI>();
            var stats = (VehicleStatBar[])Enum.GetValues(typeof(VehicleStatBar));
            for (int i = 0; i < stats.Length; i++)
                statBars.Add(MakeStatBar(panel.transform, stats[i], i));
        }

        StatBarUI MakeStatBar(Transform parent, VehicleStatBar stat, int row)
        {
            var rowGo = NewUI(stat + " Row", parent, typeof(RectTransform));
            Place((RectTransform)rowGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                  new Vector2(0f, -30f - row * 98f), new Vector2(-60f, 86f));

            var label = MakeText(rowGo.transform, "Label", StatName(stat), FontStyle.Bold, Color.white, fontSize - 8, TextAnchor.MiddleLeft);
            Place((RectTransform)label.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                  Vector2.zero, new Vector2(0f, 40f));

            var back = NewUI("Bar Background", rowGo.transform, typeof(RectTransform), typeof(Image));
            Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                  new Vector2(0f, 4f), new Vector2(0f, 26f));
            var backImg = back.GetComponent<Image>();
            backImg.color = new Color(1f, 1f, 1f, 0.12f);
            backImg.raycastTarget = false;

            var fillGo = NewUI("Bar Fill", back.transform, typeof(RectTransform), typeof(Image));
            var fillRt = (RectTransform)fillGo.transform;
            Stretch(fillRt);
            fillRt.anchorMax = new Vector2(0.7f, 1f); // sample value; set at runtime
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = accentColor;
            fillImg.raycastTarget = false;

            return new StatBarUI { stat = stat, label = label, fill = fillRt };
        }

        Text MakeText(Transform parent, string name, string text, FontStyle style, Color color, int size, TextAnchor align)
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

        Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, int labelSize)
        {
            var go = NewUI(label + " Button", parent, typeof(RectTransform), typeof(Image), typeof(Button));
            Place((RectTransform)go.transform, anchor, anchor, new Vector2(0.5f, 0.5f), pos, size);

            var img = go.GetComponent<Image>();
            img.color = Color.white; // the Button tints this

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = buttonHighlightColor;
            colors.selectedColor = buttonHighlightColor;
            colors.pressedColor = buttonPressedColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var text = MakeText(go.transform, "Label", label, FontStyle.Bold, Color.white, labelSize, TextAnchor.MiddleCenter);
            Stretch((RectTransform)text.transform);
            return btn;
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

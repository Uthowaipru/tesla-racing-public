using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace AnimeToon
{
    /// <summary>The settings a row of the graphics menu can change.</summary>
    public enum AnimeGraphicsSetting
    {
        Resolution, Effects, FrameRate, Shadows, Bloom, Outlines, Detail, Clouds, Textures, SkidMarks
    }

    /// <summary>One row of the menu. Delete a row to remove a setting, or duplicate one and pick another setting.</summary>
    [Serializable]
    public class AnimeGraphicsRow
    {
        public AnimeGraphicsSetting setting;
        [Tooltip("Shown before the value, for example RESOLUTION.")]
        public string displayName;
        [Tooltip("The text that shows the name and the value.")]
        public Text label;
        [Tooltip("Toggle / cycle rows: the button that changes the setting. Leave empty on stepper rows.")]
        public Button button;
        [Tooltip("Stepper rows only: the - and + buttons.")]
        public Button minus;
        public Button plus;
    }

    /// <summary>One preset button (AUTO, LOW ...).</summary>
    [Serializable]
    public class AnimeGraphicsPreset
    {
        public AnimeQualityMode mode;
        public Button button;
        public Text label;
        [Tooltip("Shown under the buttons while this preset is active. {level} becomes LOW / MEDIUM / HIGH.")]
        [TextArea(1, 3)] public string description;
    }

    /// <summary>
    /// Standalone graphics settings screen. It does not depend on any other menu of your game.
    ///
    /// EDITABLE: run Tools > Anime Toon > Generate Graphics Menu UI once. That saves a prefab
    /// (Assets/Resources/AnimeToon/AnimeGraphicsMenu.prefab) made of ordinary UI objects: open it and change colors, fonts,
    /// sizes, positions, texts, sprites, remove or add rows. The script only needs the references listed below.
    /// The prefab is created automatically when the game starts and stays between scenes.
    ///
    /// Without that prefab nothing appears by itself. In code you can still call AnimeGraphicsMenu.Create() to get a
    /// default menu built at runtime, and AnimeGraphicsMenu.Open() / Close() / Toggle() from your own button.
    ///
    /// The panel pauses the game while it is open (switch off "Pause Game While Open" to keep it running).
    /// Changing any single setting switches to CUSTOM, starting from the preset that was active; the choice is saved.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimeGraphicsMenu : MonoBehaviour
    {
        public enum Corner { TopRight, TopLeft, BottomRight, BottomLeft }

        /// <summary>Optional: decides when the open button is visible (null = always, as long as Show Button is on).</summary>
        public static Func<bool> ShowButtonWhen;

        public static AnimeGraphicsMenu Instance { get; private set; }

        public static bool IsOpen
        {
            get { return Instance != null && Instance.panelRoot != null && Instance.panelRoot.activeSelf; }
        }

        [Header("Open button")]
        [Tooltip("Show the small GRAPHICS button. Off = open the menu from your own button with AnimeGraphicsMenu.Open().")]
        public bool showButton = true;
        public GameObject buttonRoot;
        public Button openButton;

        [Header("Panel")]
        public GameObject panelRoot;
        public Button backButton;
        public Text infoText;

        [Header("Presets")]
        public List<AnimeGraphicsPreset> presets = new List<AnimeGraphicsPreset>();

        [Header("Settings")]
        public List<AnimeGraphicsRow> rows = new List<AnimeGraphicsRow>();

        [Header("Colors")]
        public Color normalColor = new Color(0.22f, 0.22f, 0.3f, 1f);
        public Color highlightColor = new Color(0.34f, 0.34f, 0.5f, 1f);
        public Color pressedColor = new Color(0.5f, 0.5f, 0.7f, 1f);
        public Color selectedColor = new Color(1f, 0.85f, 0.3f, 1f);
        public Color textColor = Color.white;
        public Color selectedTextColor = new Color(0.1f, 0.1f, 0.14f, 1f);

        [Header("Behaviour")]
        public bool pauseGameWhileOpen = true;
        [Tooltip("Keep the menu when a new scene loads.")]
        public bool persistAcrossScenes = true;
        [Tooltip("Writes what the menu is doing to the Console: created, scale, visibility, destroyed (and in which scene). Turn it off once everything works.")]
        public bool debugLog = true;

        // used only when the default menu is built by code
        static Font defaultFont;
        float savedTimeScale = 1f;
        bool pausedByMenu;
        int lastVisible = -1;
        static bool quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            quitting = false;
        }

        static void MarkQuitting() { quitting = true; }

        // ------------------------------------------------------------------ static API

        /// <summary>Loads the prefab from Resources/AnimeToon if it exists. Nothing appears without it.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (Instance != null || FindFirstObjectByType<AnimeGraphicsMenu>() != null) return;

            var prefab = Resources.Load<GameObject>("AnimeToon/AnimeGraphicsMenu");
            if (prefab == null) return;

            Instantiate(prefab);
        }

        /// <summary>The menu, created with a default look built by code if there is none yet.</summary>
        public static AnimeGraphicsMenu Create()
        {
            if (Instance != null) return Instance;

            var existing = FindFirstObjectByType<AnimeGraphicsMenu>();
            if (existing != null) return existing;

            var go = new GameObject("Anime Graphics Menu");
            return go.AddComponent<AnimeGraphicsMenu>();
        }

        public static void Open()
        {
            Create().OpenPanel();
        }

        public static void Close()
        {
            if (Instance != null) Instance.ClosePanel();
        }

        public static void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        // ------------------------------------------------------------------ lifetime

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (panelRoot == null) BuildDefaultUI(); // nothing was set up: build the default menu

            if (persistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            Bind();
            panelRoot.SetActive(false);
            AnimeQuality.Changed += Refresh;

            // the open button needs an EventSystem to receive taps, so make sure there is one from the start
            EnsureEventSystem();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting -= MarkQuitting;
            Application.quitting += MarkQuitting;

            if (debugLog) StartCoroutine(DebugReport());
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem();
            Log("scene loaded: " + scene.name + ". The menu is still alive (it lives in scene '" + gameObject.scene.name + "').");
        }

        IEnumerator DebugReport()
        {
            yield return null; // let the canvases update
            yield return null;

            if (buttonRoot == null || panelRoot == null)
            {
                Log("ERROR: buttonRoot or panelRoot is not assigned in the prefab.");
                yield break;
            }

            FixZeroScale(buttonRoot);
            FixZeroScale(panelRoot);

            var canvas = buttonRoot.GetComponent<Canvas>();
            var rect = buttonRoot.transform as RectTransform;
            Log("ready. Open button: activeInHierarchy=" + buttonRoot.activeInHierarchy +
                ", canvas=" + (canvas != null ? canvas.renderMode + " order " + canvas.sortingOrder + " enabled " + canvas.enabled : "NONE") +
                ", scale=" + buttonRoot.transform.lossyScale + ", size=" + (rect != null ? rect.rect.size.ToString() : "?") +
                ", screen=" + Screen.width + "x" + Screen.height +
                ", EventSystem=" + (FindFirstObjectByType<EventSystem>() != null) +
                ", showButton=" + showButton + ", scene='" + gameObject.scene.name + "'");
        }

        void FixZeroScale(GameObject canvasObject)
        {
            if (canvasObject.transform.lossyScale.sqrMagnitude > 0.000001f) return;

            Log("WARNING: " + canvasObject.name + " still has zero scale at runtime. Setting it to 1.");
            canvasObject.transform.localScale = Vector3.one;
        }

        void Log(string message)
        {
            if (debugLog) Debug.Log("[AnimeGraphicsMenu] " + message, this);
        }

        void OnDisable()
        {
            if (Application.isPlaying && !quitting) Log("disabled (scene '" + gameObject.scene.name + "').");
            ReleasePause();
        }

        void OnDestroy()
        {
            if (Application.isPlaying && !quitting)
            {
                // scene 'DontDestroyOnLoad' means something called Destroy on it; any other scene name means its scene was unloaded
                Log("DESTROYED while the game is running (it lived in scene '" + gameObject.scene.name + "').");
            }

            ReleasePause();
            AnimeQuality.Changed -= Refresh;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (buttonRoot == null) return;

            bool show = showButton && (ShowButtonWhen == null || ShowButtonWhen());
            if (buttonRoot.activeSelf != show) buttonRoot.SetActive(show);

            if (debugLog)
            {
                int visible = buttonRoot.activeInHierarchy ? 1 : 0;
                if (visible != lastVisible)
                {
                    lastVisible = visible;
                    Log("open button is now " + (visible == 1 ? "VISIBLE" : "HIDDEN") + " (showButton=" + showButton + ", ShowButtonWhen " + (ShowButtonWhen == null ? "not set" : "set") + ").");
                }
            }

            if (ShowButtonWhen != null && !ShowButtonWhen() && IsOpen) ClosePanel();
        }

        void OpenPanel()
        {
            if (panelRoot == null) return;

            EnsureEventSystem();
            Refresh();
            panelRoot.SetActive(true);

            if (pauseGameWhileOpen && !pausedByMenu && Time.timeScale > 0f)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                pausedByMenu = true;
            }
        }

        void ClosePanel()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            ReleasePause();
        }

        void ReleasePause()
        {
            if (!pausedByMenu) return;
            pausedByMenu = false;
            if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = savedTimeScale;
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

        // ------------------------------------------------------------------ wiring (works for the prefab and the default menu)

        void Bind()
        {
            if (openButton != null) openButton.onClick.AddListener(Toggle);
            if (backButton != null) backButton.onClick.AddListener(Close);

            foreach (AnimeGraphicsPreset preset in presets)
            {
                if (preset == null || preset.button == null) continue;
                AnimeQualityMode mode = preset.mode;
                preset.button.onClick.AddListener(() =>
                {
                    AnimeQuality.SetMode(mode);
                    Refresh();
                });
            }

            foreach (AnimeGraphicsRow row in rows)
            {
                if (row == null) continue;
                AnimeGraphicsSetting setting = row.setting;
                if (row.button != null) row.button.onClick.AddListener(() => Change(setting, +1));
                if (row.minus != null) row.minus.onClick.AddListener(() => Change(setting, -1));
                if (row.plus != null) row.plus.onClick.AddListener(() => Change(setting, +1));
            }
        }

        void Change(AnimeGraphicsSetting setting, int direction)
        {
            AnimeQualityTier c = AnimeQuality.BeginCustom();

            switch (setting)
            {
                case AnimeGraphicsSetting.Resolution:
                    c.renderScale = Mathf.Clamp(Mathf.Round((c.renderScale + direction * 0.1f) * 10f) / 10f, 0.5f, 1f);
                    break;
                case AnimeGraphicsSetting.Effects:
                    c.particleScale = Mathf.Clamp(Mathf.Round((c.particleScale + direction * 0.1f) * 10f) / 10f, 0.1f, 1f);
                    break;
                case AnimeGraphicsSetting.FrameRate:
                    c.targetFps = c.targetFps <= 30 ? 60 : 30;
                    break;
                case AnimeGraphicsSetting.Shadows:
                    c.shadows = !c.shadows;
                    if (c.shadows && c.shadowDistance < 10f) c.shadowDistance = 25f;
                    break;
                case AnimeGraphicsSetting.Bloom:
                    c.bloom = !c.bloom;
                    break;
                case AnimeGraphicsSetting.Outlines:
                    c.outlines = !c.outlines;
                    break;
                case AnimeGraphicsSetting.Detail:
                    c.simpleShading = !c.simpleShading;
                    break;
                case AnimeGraphicsSetting.Clouds:
                    c.cloudQuality = (c.cloudQuality + 1) % 3;
                    break;
                case AnimeGraphicsSetting.Textures:
                    c.textureMipLimit = (c.textureMipLimit + 1) % 3;
                    break;
                case AnimeGraphicsSetting.SkidMarks:
                    c.skidMarks = !c.skidMarks;
                    break;
            }

            AnimeQuality.ApplyCustom();
            Refresh();
        }

        void Refresh()
        {
            if (panelRoot == null) return;

            foreach (AnimeGraphicsPreset preset in presets)
            {
                if (preset == null || preset.button == null) continue;
                SetSelected(preset.button, preset.label, AnimeQuality.Mode == preset.mode);
            }

            foreach (AnimeGraphicsRow row in rows)
            {
                if (row == null || row.label == null) continue;
                string name = string.IsNullOrEmpty(row.displayName) ? row.setting.ToString().ToUpperInvariant() : row.displayName;
                row.label.text = name + ": " + RowValue(row.setting);
            }

            if (infoText != null)
            {
                string description = "";
                foreach (AnimeGraphicsPreset preset in presets)
                    if (preset != null && preset.mode == AnimeQuality.Mode) description = preset.description;

                infoText.text = description.Replace("{level}", AnimeQuality.Level.ToString().ToUpperInvariant());
            }
        }

        static string RowValue(AnimeGraphicsSetting setting)
        {
            AnimeQualityTier t = AnimeQuality.Tier;
            switch (setting)
            {
                case AnimeGraphicsSetting.Resolution: return Mathf.RoundToInt(t.renderScale * 100f) + "%";
                case AnimeGraphicsSetting.Effects: return Mathf.RoundToInt(t.particleScale * 100f) + "%";
                case AnimeGraphicsSetting.FrameRate: return t.targetFps.ToString();
                case AnimeGraphicsSetting.Shadows: return OnOff(t.shadows);
                case AnimeGraphicsSetting.Bloom: return OnOff(t.bloom);
                case AnimeGraphicsSetting.Outlines: return OnOff(t.outlines);
                case AnimeGraphicsSetting.Detail: return t.simpleShading ? "LOW" : "HIGH";
                case AnimeGraphicsSetting.Clouds: return t.cloudQuality <= 0 ? "FULL" : t.cloudQuality == 1 ? "FLAT" : "OFF";
                case AnimeGraphicsSetting.Textures: return t.textureMipLimit <= 0 ? "FULL" : t.textureMipLimit == 1 ? "HALF" : "QUARTER";
                default: return OnOff(t.skidMarks);
            }
        }

        static string OnOff(bool value) { return value ? "ON" : "OFF"; }

        void SetSelected(Button button, Text text, bool selected)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? selectedColor : normalColor;
            colors.highlightedColor = selected ? selectedColor : highlightColor;
            colors.selectedColor = selected ? selectedColor : highlightColor;
            colors.pressedColor = pressedColor;
            button.colors = colors;
            if (text != null) text.color = selected ? selectedTextColor : textColor;
        }

        // ------------------------------------------------------------------ default menu (built by code, also used by the generator)

        public static Corner ButtonCorner = Corner.TopRight;
        public static Vector2 ButtonMargin = new Vector2(30f, 30f);

        /// <summary>
        /// Builds the default menu as ordinary UI objects under this object and fills in all the references.
        /// Tools > Anime Toon > Generate Graphics Menu UI calls this and saves the result as an editable prefab.
        /// </summary>
        public void BuildDefaultUI()
        {
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            presets.Clear();
            rows.Clear();

            // ---- open button
            buttonRoot = NewCanvas("Open Button Canvas", 590);
            buttonRoot.transform.SetParent(transform, false);

            Text openLabel;
            openButton = MakeButton(buttonRoot.transform, "GRAPHICS", 200f, 64f, 28, out openLabel, false);
            var openRect = (RectTransform)openButton.transform;
            openRect.sizeDelta = new Vector2(200f, 64f);

            Vector2 anchor;
            Vector2 position;
            switch (ButtonCorner)
            {
                case Corner.TopLeft: anchor = new Vector2(0f, 1f); position = new Vector2(ButtonMargin.x, -ButtonMargin.y); break;
                case Corner.BottomRight: anchor = new Vector2(1f, 0f); position = new Vector2(-ButtonMargin.x, ButtonMargin.y); break;
                case Corner.BottomLeft: anchor = new Vector2(0f, 0f); position = new Vector2(ButtonMargin.x, ButtonMargin.y); break;
                default: anchor = new Vector2(1f, 1f); position = new Vector2(-ButtonMargin.x, -ButtonMargin.y); break;
            }
            openRect.anchorMin = openRect.anchorMax = openRect.pivot = anchor;
            openRect.anchoredPosition = position;

            // ---- panel
            panelRoot = NewCanvas("Panel Canvas", 600);
            panelRoot.transform.SetParent(transform, false);

            var dim = NewUI("Dim", panelRoot.transform, typeof(RectTransform), typeof(Image));
            Stretch((RectTransform)dim.transform);
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var panel = NewUI("Panel", panelRoot.transform, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1420f, 820f);
            panel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 0.97f);

            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(34, 34, 26, 26);
            vlg.spacing = 14f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            Text title = MakeText(panel.transform, "Title", "GRAPHICS", 56, selectedColor);
            SetHeight(title.gameObject, 70f);

            // presets
            Transform presetRow = MakeRow(panel.transform, 12f, 70f);
            AddPreset(presetRow, "AUTO", AnimeQualityMode.Auto, "AUTO is using {level}. It lowers itself if the game runs slowly.");
            AddPreset(presetRow, "LOW", AnimeQualityMode.Low, "LOW: smaller picture, no shadows or bloom. For older phones.");
            AddPreset(presetRow, "MEDIUM", AnimeQualityMode.Medium, "MEDIUM: balanced. Shadows nearby, bloom and outlines on.");
            AddPreset(presetRow, "HIGH", AnimeQualityMode.High, "HIGH: sharpest picture, longer shadows, all effects.");
            AddPreset(presetRow, "CUSTOM", AnimeQualityMode.Custom, "CUSTOM: your own settings. Changing a setting starts from the preset you had.");

            infoText = MakeText(panel.transform, "Info", "", 28, new Color(0.8f, 0.8f, 0.9f));
            SetHeight(infoText.gameObject, 56f);

            // settings, two columns
            var grid = NewUI("Settings", panel.transform, typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            var glg = grid.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(660f, 66f);
            glg.spacing = new Vector2(16f, 12f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;
            glg.childAlignment = TextAnchor.UpperCenter;
            var gle = grid.GetComponent<LayoutElement>();
            gle.preferredHeight = 5 * 66f + 4 * 12f;
            gle.flexibleHeight = 0f;

            AddStepperRow(grid.transform, AnimeGraphicsSetting.Resolution, "RESOLUTION");
            AddStepperRow(grid.transform, AnimeGraphicsSetting.Effects, "EFFECTS");
            AddCycleRow(grid.transform, AnimeGraphicsSetting.FrameRate, "FRAME RATE");
            AddCycleRow(grid.transform, AnimeGraphicsSetting.Shadows, "SHADOWS");
            AddCycleRow(grid.transform, AnimeGraphicsSetting.Bloom, "BLOOM");
            AddCycleRow(grid.transform, AnimeGraphicsSetting.Outlines, "OUTLINES");
            AddCycleRow(grid.transform, AnimeGraphicsSetting.Detail, "DETAIL");
            AddCycleRow(grid.transform, AnimeGraphicsSetting.Clouds, "CLOUDS");
            AddCycleRow(grid.transform, AnimeGraphicsSetting.Textures, "TEXTURES");
            AddCycleRow(grid.transform, AnimeGraphicsSetting.SkidMarks, "SKID MARKS");

            var spacer = NewUI("Spacer", panel.transform, typeof(RectTransform), typeof(LayoutElement));
            spacer.GetComponent<LayoutElement>().flexibleHeight = 1f;

            Text backLabel;
            backButton = MakeButton(panel.transform, "BACK", 360f, 70f, 34, out backLabel, true);
        }

        void AddPreset(Transform row, string label, AnimeQualityMode mode, string description)
        {
            Text text;
            Button button = MakeButton(row, label, 250f, 70f, 32, out text, true);
            presets.Add(new AnimeGraphicsPreset { mode = mode, button = button, label = text, description = description });
        }

        void AddCycleRow(Transform parent, AnimeGraphicsSetting setting, string displayName)
        {
            Text text;
            Button button = MakeButton(parent, displayName, 660f, 66f, 30, out text, true);
            rows.Add(new AnimeGraphicsRow { setting = setting, displayName = displayName, button = button, label = text });
        }

        void AddStepperRow(Transform parent, AnimeGraphicsSetting setting, string displayName)
        {
            var row = NewUI(displayName + " Row", parent, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            Text ignored;
            Button minus = MakeButton(row.transform, "-", 100f, 66f, 40, out ignored, true);

            Text label = MakeText(row.transform, displayName + " Label", "", 30, textColor);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 440f;
            le.flexibleWidth = 1f;

            Button plus = MakeButton(row.transform, "+", 100f, 66f, 40, out ignored, true);

            rows.Add(new AnimeGraphicsRow { setting = setting, displayName = displayName, label = label, minus = minus, plus = plus });
        }

        // ------------------------------------------------------------------ ui helpers

        static GameObject NewUI(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, components);
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) go.layer = ui;
            go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject NewCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) go.layer = ui;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return go;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void SetHeight(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        Transform MakeRow(Transform parent, float spacing, float height)
        {
            var row = NewUI("Preset Row", parent, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            row.GetComponent<LayoutElement>().preferredHeight = height;
            return row.transform;
        }

        Text MakeText(Transform parent, string name, string text, int size, Color color)
        {
            var go = NewUI(name, parent, typeof(RectTransform), typeof(Text));
            var t = go.GetComponent<Text>();
            t.font = defaultFont;
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

        Button MakeButton(Transform parent, string label, float width, float height, int fontSize, out Text text, bool useLayoutElement)
        {
            var go = NewUI(label + " Button", parent, typeof(RectTransform), typeof(Image), typeof(Button));

            if (useLayoutElement)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = width;
                le.preferredHeight = height;
            }

            var img = go.GetComponent<Image>();
            img.color = Color.white; // the Button tints this

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = highlightColor;
            colors.selectedColor = highlightColor;
            colors.pressedColor = pressedColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            text = MakeText(go.transform, "Label", label, fontSize, textColor);
            Stretch((RectTransform)text.transform);
            return button;
        }
    }
}

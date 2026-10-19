using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using JetBrains.Annotations;

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
    /// Standalone graphics settings screen. It never shows up by itself and has no button of its own:
    /// you place it where you want and open it from your own button.
    ///
    /// SET UP
    ///   1. Tools > Anime Toon > Generate Graphics Menu UI  (saves a prefab made of ordinary UI objects)
    ///   2. Drag the prefab into the scene that has your settings screen (or Tools > Anime Toon > Add Graphics Menu To Scene).
    ///      Restyle it freely: colors, fonts, sizes, positions, texts, sprites, remove or add rows.
    ///   3. Link your own button: Button > OnClick > + > drag the menu object > AnimeGraphicsMenu > Show().
    ///      (Hide() and ToggleVisible() work the same way. The BACK button inside the menu hides it.)
    ///
    /// FROM CODE   AnimeGraphicsMenu.Open() / Close() / Toggle()
    ///   If no menu is in the scene, Open() builds a default one at that moment.
    ///
    /// The object with this script is its own overlay canvas, so it works in any scene. To put it inside one of your own
    /// canvases instead, drag it under that canvas; delete the Dim object if you do not want the dark background.
    /// Changing any single setting switches to CUSTOM, starting from the preset that was active; the choice is saved.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimeGraphicsMenu : MonoBehaviour
    {
        public static AnimeGraphicsMenu Instance { get; private set; }

        public static bool IsOpen
        {
            get { return Instance != null && Instance.panelRoot != null && Instance.panelRoot.activeSelf; }
        }

        [Header("Panel")]
        [Tooltip("Everything that is shown and hidden (dim background and panel). The object with this script stays active.")]
        public GameObject panelRoot;
        public Button backButton;
        public GameObject OptionsPanel;
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
        [Tooltip("Hide the menu when the scene starts (open it from your own button).")]
        public bool startHidden = true;
        [Tooltip("Freeze the game while the menu is open. Not needed when it is opened from a screen that is already paused.")]
        public bool pauseGameWhileOpen = false;
        [Tooltip("Keep this menu when a new scene loads. Only for a menu placed at the root of the scene.")]
        public bool persistAcrossScenes = false;
        [Tooltip("Write what the menu does (shown, hidden, destroyed) to the Console.")]
        public bool debugLog = false;

        static Font defaultFont;
        static bool quitting;
        float savedTimeScale = 1f;
        bool pausedByMenu;

        // ------------------------------------------------------------------ static API (from code)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            quitting = false;
        }

        static void MarkQuitting() { quitting = true; }

        /// <summary>The menu in the scene, or a default one built right now (hidden until you open it).</summary>
        public static AnimeGraphicsMenu Create()
        {
            if (Instance != null) return Instance;

            var existing = FindFirstObjectByType<AnimeGraphicsMenu>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var go = new GameObject("Anime Graphics Menu");
            return go.AddComponent<AnimeGraphicsMenu>();
        }

        public static void Open() { Create().Show(); }

        public static void Close() { if (Instance != null) Instance.Hide(); }

        public static void Toggle() { Create().ToggleVisible(); }

        // ------------------------------------------------------------------ show / hide (also usable from a Button's OnClick)

        public void Show()
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
            Log("shown.");
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            ReleasePause();
            Log("hidden.");
        }

        public void ToggleVisible()
        {
            if (panelRoot != null && panelRoot.activeSelf) Hide(); else Show();
        }

        // ------------------------------------------------------------------ lifetime

        void Awake()
        {
            Instance = this;

            if (panelRoot == null) BuildDefaultUI(); // nothing was set up: build the default menu

            if (persistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            Bind();
            if (startHidden) panelRoot.SetActive(false);

            AnimeQuality.Changed += Refresh;
            Application.quitting -= MarkQuitting;
            Application.quitting += MarkQuitting;
        }

        void OnDisable()
        {
            ReleasePause();
        }

        void OnDestroy()
        {
            if (Application.isPlaying && !quitting)
                Log("destroyed (it lived in scene '" + gameObject.scene.name + "').");

            ReleasePause();
            AnimeQuality.Changed -= Refresh;
            if (Instance == this) Instance = null;
        }

        void ReleasePause()
        {
            if (!pausedByMenu) return;
            pausedByMenu = false;
            if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = savedTimeScale;
        }

        void Log(string message)
        {
            if (debugLog) Debug.Log("[AnimeGraphicsMenu] " + message, this);
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

        // ------------------------------------------------------------------ wiring (same for the prefab and the default menu)

        
        void Bind()
        {
            backButton.onClick.AddListener(ShowOptionsPanel);

            if (backButton != null) backButton.onClick.AddListener(Hide);
            
            OptionsPanel.SetActive(true);

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
        void ShowOptionsPanel()
            {
                OptionsPanel.SetActive(true);
                panelRoot.SetActive(false);
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
                string rowName = string.IsNullOrEmpty(row.displayName) ? row.setting.ToString().ToUpperInvariant() : row.displayName;
                row.label.text = rowName + ": " + RowValue(row.setting);
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

        /// <summary>
        /// Builds the default menu as ordinary UI objects under this object and fills in all the references.
        /// Tools > Anime Toon > Generate Graphics Menu UI calls this and saves the result as an editable prefab.
        /// </summary>
        public void BuildDefaultUI()
        {
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            presets.Clear();
            rows.Clear();

            // this object is its own overlay canvas, so the menu works in any scene
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) gameObject.layer = ui;

            // everything that is shown / hidden lives under Content
            var content = NewUI("Content", transform, typeof(RectTransform));
            Stretch((RectTransform)content.transform);
            panelRoot = content;

            var dim = NewUI("Dim", content.transform, typeof(RectTransform), typeof(Image));
            Stretch((RectTransform)dim.transform);
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var panel = NewUI("Panel", content.transform, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
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
            backButton = MakeButton(panel.transform, "BACK", 360f, 70f, 34, out backLabel);

            Refresh(); // fills in the texts so they are visible while editing the prefab
        }

        void AddPreset(Transform row, string label, AnimeQualityMode mode, string description)
        {
            Text text;
            Button button = MakeButton(row, label, 250f, 70f, 32, out text);
            presets.Add(new AnimeGraphicsPreset { mode = mode, button = button, label = text, description = description });
        }

        void AddCycleRow(Transform parent, AnimeGraphicsSetting setting, string displayName)
        {
            Text text;
            Button button = MakeButton(parent, displayName, 660f, 66f, 30, out text);
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
            Button minus = MakeButton(row.transform, "-", 100f, 66f, 40, out ignored);

            Text label = MakeText(row.transform, displayName + " Label", "", 30, textColor);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 440f;
            le.flexibleWidth = 1f;

            Button plus = MakeButton(row.transform, "+", 100f, 66f, 40, out ignored);

            rows.Add(new AnimeGraphicsRow { setting = setting, displayName = displayName, label = label, minus = minus, plus = plus });
        }

        // ------------------------------------------------------------------ ui helpers

        static GameObject NewUI(string objectName, Transform parent, params Type[] components)
        {
            var go = new GameObject(objectName, components);
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) go.layer = ui;
            go.transform.SetParent(parent, false);
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

        Text MakeText(Transform parent, string objectName, string text, int size, Color color)
        {
            var go = NewUI(objectName, parent, typeof(RectTransform), typeof(Text));
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

        Button MakeButton(Transform parent, string label, float width, float height, int fontSize, out Text text)
        {
            var go = NewUI(label + " Button", parent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

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

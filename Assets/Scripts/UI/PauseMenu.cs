using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace VehicleSystem
{
    /// <summary>
    /// Pause menu:  1. RESUME   2. OPTIONS   3. MAIN MENU
    ///
    ///   Open / close : keyboard Esc or P, gamepad Start, mobile PAUSE button (LocalPlayerRig creates this component).
    ///   Navigate     : mouse / touch, or keyboard + gamepad (arrows / WASD / stick / D-pad, Enter or A to select).
    ///                  Esc / Start inside OPTIONS goes back one step.
    ///   Options      : master volume (- / +), auto-accelerate on / off, and steering with buttons or by tilting the phone
    ///                  (with a CALIBRATE TILT button). All are saved with PlayerPrefs.
    ///   Main menu    : loads "Main Menu Scene" (must be in File > Build Profiles / Build Settings).
    ///
    /// UI source:
    ///   - If "Ui Root" is assigned (made with Tools > Vehicle > Generate Pause Menu UI, or the button on this
    ///     component) the objects in your Hierarchy are used: move, resize, restyle, rename them freely.
    ///   - If it is empty, a default menu is built at runtime.
    /// Button clicks are connected in code, so you never have to wire up OnClick events.
    ///
    /// The editor tool lives at the bottom of this file, so this script can sit anywhere in Assets.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        /// <summary>True while the menu is open. LocalPlayerInput ignores the driving controls then.</summary>
        public static bool IsPaused { get; private set; }

        const string VolumeKey = "VehicleSystem.MasterVolume";
        const string AutoAccelerateKey = "VehicleSystem.AutoAccelerate";
        const string TiltSteeringKey = "VehicleSystem.TiltSteering";

        [Header("Behaviour")]
        [Tooltip("Scene loaded by MAIN MENU (add it to the Build Settings). Empty = scene with build index 0.")]
        public string mainMenuScene = "MainMenu";
        [Tooltip("Silence the game's audio while paused.")]
        public bool pauseAudio = true;
        [Tooltip("Mobile: open the menu automatically when the app goes to the background.")]
        public bool pauseWhenAppLosesFocus = true;
        [Range(0.05f, 0.5f)] public float volumeStep = 0.1f;
        [Tooltip("ON (single player): the game freezes while paused (Time.timeScale = 0). " +
                 "OFF (local / online multiplayer): the world keeps running, only this player's controls are blocked.")]
        public bool freezeTime = true;

        [Header("UI (filled in by the editor tool)")]
        [Tooltip("Parent object of the whole menu (its Canvas). Hidden while the game is running.")]
        public GameObject uiRoot;
        public GameObject mainPanel;
        public GameObject optionsPanel;
        public GameObject GraphicsPanel;
        public Button resumeButton;
        public Button optionsButton;
        public Button mainMenuButton;
        public Button volumeDownButton;
        public Button volumeUpButton;
        public Button autoAccelerateButton;
        [Tooltip("Optional. STEERING: BUTTONS / TILT. Created for you if missing (Add Steering Option in the Inspector puts it in the Hierarchy).")]
        public Button steeringButton;
        public Button graphicssettingsbutton;
        [Tooltip("Optional. Only shown while tilt steering is on.")]
        public Button calibrateButton;
        public Button backButton;
        public Text volumeText;
        public Text autoAccelerateText;
        [Tooltip("The label of the steering button. Found automatically if empty.")]
        public Text steeringText;

        [Header("Default look (editor tool + runtime fallback)")]
        public int fontSize = 36;
        public Color dimColor = new Color(0f, 0f, 0f, 0.65f);
        public Color panelColor = new Color(0.1f, 0.1f, 0.14f, 0.96f);
        public Color buttonColor = new Color(0.22f, 0.22f, 0.3f, 1f);
        public Color buttonHighlightColor = new Color(0.34f, 0.34f, 0.5f, 1f);
        public Color buttonPressedColor = new Color(0.5f, 0.5f, 0.7f, 1f);
        public Color accentColor = new Color(1f, 0.85f, 0.3f, 1f);

        [Header("Optional (assigned by LocalPlayerRig)")]
        public MobileControls mobile;
        [FormerlySerializedAs("vehicleInput")]
        public LocalPlayerInput playerInput;

        float volume = 1f;
        float prevTimeScale = 1f;
        bool timeFrozen;
        CursorLockMode prevLock;
        bool prevCursorVisible;

#if ENABLE_INPUT_SYSTEM
        InputAction pauseAction;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsPaused = false;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            IsPaused = false;

            if (uiRoot == gameObject) uiRoot = null; // would switch this script off

            if (!UiComplete())
            {
                if (uiRoot != null) Destroy(uiRoot); // half-assigned: start clean
                BuildUI();
            }

            EnsureEventSystem();

            CreateSteeringRows(); // menus built before the steering option existed get it here

            resumeButton.onClick.AddListener(Resume);
            optionsButton.onClick.AddListener(ShowOptions);
            graphicssettingsbutton.onClick.AddListener(ShowGraphicsMenu);
            mainMenuButton.onClick.AddListener(GoToMainMenu);
            volumeDownButton.onClick.AddListener(() => ChangeVolume(-volumeStep));
            volumeUpButton.onClick.AddListener(() => ChangeVolume(+volumeStep));
            autoAccelerateButton.onClick.AddListener(ToggleAutoAccelerate);
            steeringButton.onClick.AddListener(ToggleSteering);
            if (calibrateButton != null) calibrateButton.onClick.AddListener(CalibrateTilt);
            backButton.onClick.AddListener(ShowMain);

#if ENABLE_INPUT_SYSTEM
            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Keyboard>/p");
            pauseAction.AddBinding("<Gamepad>/start");
#endif

            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, AudioListener.volume));
            AudioListener.volume = volume;

            uiRoot.SetActive(false);
        }

        /// <summary>Called by LocalPlayerRig so the menu never has to search the scene.</summary>
        public void Bind(MobileControls mobileControls, LocalPlayerInput input)
        {
            mobile = mobileControls;
            playerInput = input;
        }

        void Start()
        {
            if (PlayerPrefs.HasKey(AutoAccelerateKey))
                SetAutoAccelerate(PlayerPrefs.GetInt(AutoAccelerateKey) == 1);

            if (PlayerPrefs.HasKey(TiltSteeringKey))
                SetTilt(PlayerPrefs.GetInt(TiltSteeringKey) == 1);
        }

#if ENABLE_INPUT_SYSTEM
        void OnEnable() => pauseAction?.Enable();
        void OnDisable() => pauseAction?.Disable();
#endif

        void OnDestroy()
        {
            if (IsPaused) Cleanup(true);
#if ENABLE_INPUT_SYSTEM
            pauseAction?.Dispose();
#endif
        }

        bool UiComplete() =>
            uiRoot != null && mainPanel != null && optionsPanel != null &&
            resumeButton != null && optionsButton != null && mainMenuButton != null &&
            volumeDownButton != null && volumeUpButton != null && autoAccelerateButton != null && backButton != null &&
            volumeText != null && autoAccelerateText != null;

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

        // ------------------------------------------------------------------ update

        void Update()
        {
            bool toggle;
#if ENABLE_INPUT_SYSTEM
            toggle = pauseAction.WasPressedThisFrame();
#else
            toggle = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) ||
                     Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
            if (mobile != null && mobile.ConsumePause()) toggle = true;

            if (!toggle) return;

            if (!IsPaused) Pause();
            else if (optionsPanel.activeSelf) ShowMain(); // one step back
            else Resume();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && pauseWhenAppLosesFocus && Application.isMobilePlatform && !IsPaused && uiRoot != null)
                Pause();
        }

        // ------------------------------------------------------------------ pause / resume

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;

            if (freezeTime)
            {
                prevTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                timeFrozen = true;
            }
            if (pauseAudio) AudioListener.pause = true;

            prevLock = Cursor.lockState;
            prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            uiRoot.SetActive(true);
            ShowMain();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            uiRoot.SetActive(false);
            Cleanup(true);
        }

        void Cleanup(bool restoreCursor)
        {
            IsPaused = false;
            if (timeFrozen)
            {
                Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
                timeFrozen = false;
            }
            AudioListener.pause = false;

            if (restoreCursor)
            {
                Cursor.lockState = prevLock;
                Cursor.visible = prevCursorVisible;
            }

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void GoToMainMenu()
        {
            bool byName = !string.IsNullOrEmpty(mainMenuScene);
            if (byName && !Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogError($"PauseMenu: scene '{mainMenuScene}' is not in the Build Settings (or the name is wrong).", this);
                return;
            }

            Cleanup(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (byName) SceneManager.LoadScene(mainMenuScene);
            else SceneManager.LoadScene(0);
        }

        // ------------------------------------------------------------------ panels

        void ShowMain()
        {
            mainPanel.SetActive(true);
            optionsPanel.SetActive(false);
            Select(resumeButton);
        }

        void ShowOptions()
        {
            mainPanel.SetActive(false);
            optionsPanel.SetActive(true);
            RefreshOptionTexts();
            Select(volumeUpButton);
        }

        void ShowGraphicsMenu()
        {
            mainPanel.SetActive(false);
            optionsPanel.SetActive(false);
            GraphicsPanel.SetActive(true);
            RefreshOptionTexts();
            Select(volumeUpButton);
        }


        static void Select(Button b)
        {
            if (EventSystem.current == null || b == null) return;
            EventSystem.current.SetSelectedGameObject(null); // makes sure the highlight refreshes
            EventSystem.current.SetSelectedGameObject(b.gameObject);
        }

        // ------------------------------------------------------------------ options

        void ChangeVolume(float delta)
        {
            volume = Mathf.Clamp01(Mathf.Round((volume + delta) * 20f) / 20f);
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(VolumeKey, volume);
            RefreshOptionTexts();
        }

        void ToggleAutoAccelerate()
        {
            SetAutoAccelerate(!GetAutoAccelerate());
            PlayerPrefs.SetInt(AutoAccelerateKey, GetAutoAccelerate() ? 1 : 0); // saves what really took effect
            RefreshOptionTexts();
        }

        bool GetAutoAccelerate()
        {
            if (playerInput != null) return playerInput.AutoAccelerate;
            return mobile == null || mobile.autoAccelerate;
        }

        void SetAutoAccelerate(bool value)
        {
            if (mobile != null) mobile.SetAutoAccelerate(value);
            else if (playerInput != null) playerInput.autoAccelerateWithoutMobileUI = value;
        }

        void ToggleSteering()
        {
            SetTilt(!GetTilt());
            PlayerPrefs.SetInt(TiltSteeringKey, GetTilt() ? 1 : 0); // saves what really took effect
            RefreshOptionTexts();
        }

        bool GetTilt() => mobile != null && mobile.useTilt;

        void SetTilt(bool value)
        {
            if (mobile != null) mobile.SetUseTilt(value);
        }

        void CalibrateTilt()
        {
            if (mobile != null) mobile.CalibrateTilt(); // the phone angle at the next frame becomes "straight"
        }

        void RefreshOptionTexts()
        {
            volumeText.text = $"VOLUME  {Mathf.RoundToInt(volume * 100f)}%";
            autoAccelerateText.text = $"AUTO-ACCELERATE: {(GetAutoAccelerate() ? "ON" : "OFF")}";

            bool tilt = GetTilt();
            bool available = mobile != null && mobile.TiltAvailable;
            if (steeringText != null)
                steeringText.text = tilt ? "STEERING: TILT" : available ? "STEERING: BUTTONS" : "STEERING: BUTTONS (no tilt sensor)";
            steeringButton.interactable = available || tilt;
            if (calibrateButton != null) calibrateButton.gameObject.SetActive(tilt);
        }

        /// <summary>
        /// Adds the STEERING option (buttons / tilt) and the CALIBRATE TILT button to the options panel, just below
        /// AUTO-ACCELERATE. Does nothing for the ones that already exist. Used at runtime for older menus and by the editor.
        /// </summary>
        public void CreateSteeringRows()
        {
            if (optionsPanel == null || autoAccelerateButton == null) return;

            int index = autoAccelerateButton.transform.GetSiblingIndex() + 1;

            if (steeringButton == null)
            {
                steeringButton = MakeButton(optionsPanel.transform, "STEERING: BUTTONS", 560f, 90f);
                steeringButton.transform.SetSiblingIndex(index++);
            }
            if (steeringText == null) steeringText = steeringButton.GetComponentInChildren<Text>();

            if (calibrateButton == null)
            {
                calibrateButton = MakeButton(optionsPanel.transform, "CALIBRATE TILT", 560f, 90f);
                calibrateButton.transform.SetSiblingIndex(index);
                calibrateButton.gameObject.SetActive(false); // shown while tilt steering is on
            }
        }

        // ------------------------------------------------------------------ building the UI

        /// <summary>
        /// Creates the default menu under this object and fills the UI fields. Used at runtime when no UI is
        /// assigned, and by the editor tool (sample text is filled in; replaced at runtime).
        /// </summary>
        public void BuildUI()
        {
            var canvasGo = NewUI("Pause Menu Canvas", transform,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // above the touch buttons (100) and the other overlays

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // full-screen dimmer: also stops taps from reaching the touch buttons underneath
            var dim = NewUI("Dimmer", canvasGo.transform, typeof(RectTransform), typeof(Image));
            var drt = (RectTransform)dim.transform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = drt.offsetMax = Vector2.zero;
            var dimImg = dim.GetComponent<Image>();
            dimImg.color = dimColor;
            dimImg.raycastTarget = true;

            // ---- main panel
            mainPanel = MakePanel("Main Panel", canvasGo.transform);
            MakeTitle(mainPanel.transform, "PAUSED");
            resumeButton = MakeButton(mainPanel.transform, "RESUME", 560f, 90f);
            optionsButton = MakeButton(mainPanel.transform, "OPTIONS", 560f, 90f);
            mainMenuButton = MakeButton(mainPanel.transform, "MAIN MENU", 560f, 90f);

            // ---- options panel
            optionsPanel = MakePanel("Options Panel", canvasGo.transform);
            MakeTitle(optionsPanel.transform, "OPTIONS");

            var row = NewUI("Volume Row", optionsPanel.transform, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 20f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;

            volumeDownButton = MakeButton(row.transform, "-", 90f, 90f);
            volumeText = MakeText(row.transform, "Volume Text", "VOLUME  100%", FontStyle.Bold, Color.white, 340f);
            volumeUpButton = MakeButton(row.transform, "+", 90f, 90f);

            autoAccelerateButton = MakeButton(optionsPanel.transform, "AUTO-ACCELERATE: ON", 560f, 90f);
            autoAccelerateText = autoAccelerateButton.GetComponentInChildren<Text>();
            CreateSteeringRows();
            backButton = MakeButton(optionsPanel.transform, "BACK", 560f, 90f);

            optionsPanel.SetActive(false); // switch it on in the Hierarchy to style it
        }

        GameObject MakePanel(string name, Transform parent)
        {
            var panel = NewUI(name, parent, typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));

            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            panel.GetComponent<Image>().color = panelColor;

            var v = panel.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(60, 60, 44, 44);
            v.spacing = 20f;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;

            var fit = panel.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        void MakeTitle(Transform parent, string text) =>
            MakeText(parent, "Title", text, FontStyle.Bold, accentColor, 0f, fontSize + 14);

        Text MakeText(Transform parent, string name, string text, FontStyle style, Color color,
                      float preferredWidth = 0f, int size = 0)
        {
            var go = NewUI(name, parent, typeof(RectTransform), typeof(Text));
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size > 0 ? size : fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;

            if (preferredWidth > 0f)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = preferredWidth;
            }
            return t;
        }

        Button MakeButton(Transform parent, string label, float width, float height)
        {
            var go = NewUI(label + " Button", parent,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

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

            var text = MakeText(go.transform, "Label", label, FontStyle.Bold, Color.white);
            var trt = (RectTransform)text.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            return btn;
        }

        static GameObject NewUI(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) go.layer = ui;
            go.transform.SetParent(parent, false);
            return go;
        }
    }

#if UNITY_EDITOR
    namespace EditorTools
    {
        /// <summary>Editor tool: Tools > Vehicle > Generate Pause Menu UI (builds editable objects in the Hierarchy).</summary>
        public static class PauseMenuBuilder
        {
            [MenuItem("Tools/Vehicle/Generate Pause Menu UI")]
            static void CreateFromToolsMenu() => Build(null);

            [MenuItem("GameObject/Vehicle/Pause Menu UI", false, 23)]
            static void CreateFromHierarchyMenu() => Build(null);

            public static void Build(PauseMenu target)
            {
                var menu = target != null ? target : Object.FindFirstObjectByType<PauseMenu>();

                if (menu != null && menu.uiRoot != null)
                {
                    if (menu.uiRoot == menu.gameObject)
                    {
                        EditorUtility.DisplayDialog("Pause Menu",
                            "Ui Root points at the Pause Menu object itself. Clear that field or point it at a child object, then try again.",
                            "OK");
                        return;
                    }

                    if (!EditorUtility.DisplayDialog("Pause Menu",
                            "A pause menu UI already exists. Rebuild it from scratch?\nAny changes you made to it will be lost.",
                            "Rebuild", "Cancel"))
                        return;

                    Undo.DestroyObjectImmediate(menu.uiRoot);
                }

                if (menu == null)
                {
                    var go = new GameObject("Pause Menu");
                    Undo.RegisterCreatedObjectUndo(go, "Create Pause Menu");
                    menu = Undo.AddComponent<PauseMenu>(go);
                }

                Undo.RecordObject(menu, "Generate Pause Menu UI");
                menu.BuildUI();
                Undo.RegisterCreatedObjectUndo(menu.uiRoot, "Create Pause Menu UI");

                EditorUtility.SetDirty(menu);
                if (menu.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);

                Selection.activeGameObject = menu.uiRoot;
                EditorGUIUtility.PingObject(menu.uiRoot);
            }
        }

        [CustomEditor(typeof(PauseMenu))]
        public class PauseMenuEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                var menu = (PauseMenu)target;
                EditorGUILayout.Space();

                if (menu.uiRoot == null)
                {
                    EditorGUILayout.HelpBox(
                        "No UI assigned. A default menu is built at runtime. Generate it now to edit it in the Hierarchy / Canvas.",
                        MessageType.Info);
                    if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28)))
                        PauseMenuBuilder.Build(menu);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "The Options Panel starts switched off. Tick it on in the Hierarchy to style it, then switch it off again. " +
                        "Panels use a Content Size Fitter: remove it (and the layout groups) for a fixed size.",
                        MessageType.None);
                    if (GUILayout.Button("Select UI Root"))
                    {
                        Selection.activeGameObject = menu.uiRoot;
                        EditorGUIUtility.PingObject(menu.uiRoot);
                    }
                    if (menu.steeringButton == null && GUILayout.Button("Add Steering Option (keeps your edits)"))
                    {
                        Undo.RecordObject(menu, "Add Steering Option");
                        menu.CreateSteeringRows();
                        if (menu.steeringButton != null) Undo.RegisterCreatedObjectUndo(menu.steeringButton.gameObject, "Add Steering Option");
                        if (menu.calibrateButton != null) Undo.RegisterCreatedObjectUndo(menu.calibrateButton.gameObject, "Add Calibrate Button");
                        EditorUtility.SetDirty(menu);
                        if (menu.gameObject.scene.IsValid())
                            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
                    }
                    if (GUILayout.Button("Rebuild Default UI (replaces your edits)"))
                        PauseMenuBuilder.Build(menu);
                }
            }
        }
    }
#endif
}

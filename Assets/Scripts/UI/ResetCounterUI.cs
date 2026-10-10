using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace VehicleSystem
{
    /// <summary>
    /// "RESETS LEFT: 2 / 3" counter for the limited manual reset (VehicleRecovery). Shown in every input mode,
    /// hidden when the limit is switched off. Pulses when a reset is used and turns red when none are left.
    ///
    /// UI source:
    ///   - If "Ui Root" is assigned (made with Tools > Vehicle > Generate Reset Counter UI, or the button on this
    ///     component) the objects in your Hierarchy are used: move, resize, restyle them freely.
    ///   - If it is empty, a default counter is built at runtime (LocalPlayerRig creates the component for you).
    ///
    /// The editor tool lives at the bottom of this file, so this script can sit anywhere in Assets.
    /// </summary>
    public class ResetCounterUI : MonoBehaviour
    {
        [Header("UI (filled in by the editor tool)")]
        [Tooltip("Parent object of the whole counter (usually its Canvas). Hidden when the reset is not limited.")]
        public GameObject uiRoot;
        public Text counterText;

        [Header("Text")]
        [Tooltip("{0} = resets left, {1} = resets per match.")]
        public string format = "RESETS LEFT: {0} / {1}";
        public Color normalColor = Color.white;
        [Tooltip("Colour when no resets are left, or while the counter pulses after a refused reset.")]
        public Color emptyColor = new Color(1f, 0.35f, 0.3f, 1f);
        [Tooltip("How long the counter pulses (seconds).")]
        public float pulseTime = 0.6f;

        [Header("Default look (editor tool + runtime fallback)")]
        [Tooltip("Distance from the top edge of the screen.")]
        public float topMargin = 30f;
        public int fontSize = 30;
        public Color panelColor = new Color(0f, 0f, 0f, 0.55f);

        [Header("Optional (assigned by LocalPlayerRig)")]
        public VehicleRecovery recovery;

        int shownLeft = -1, shownMax = -1;
        float pulse;
        VehicleRecovery subscribedTo;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            if (uiRoot == gameObject) uiRoot = null; // would switch this script off

            if (uiRoot == null || counterText == null)
            {
                if (uiRoot != null) Destroy(uiRoot);
                BuildUI();
            }
        }

        void Start() => SetRecovery(recovery); // also covers a recovery assigned in the inspector

        void OnDestroy() => Unsubscribe();

        /// <summary>Show the resets of this car (null hides the counter). Called by LocalPlayerRig.Attach().</summary>
        public void SetRecovery(VehicleRecovery value)
        {
            Unsubscribe();
            recovery = value;
            if (recovery != null)
            {
                recovery.ManualResetDenied += OnDenied;
                subscribedTo = recovery;
            }
            shownLeft = shownMax = -1;
        }

        void Unsubscribe()
        {
            if (subscribedTo != null) subscribedTo.ManualResetDenied -= OnDenied;
            subscribedTo = null;
        }

        void OnDenied() => pulse = pulseTime;

        // ------------------------------------------------------------------ update

        void Update()
        {
            bool show = recovery != null && recovery.limitManualResets;
            if (uiRoot.activeSelf != show) uiRoot.SetActive(show);
            if (!show) return;

            int left = recovery.ManualResetsLeft;
            int max = recovery.maxManualResets;
            if (left != shownLeft || max != shownMax)
            {
                if (shownLeft >= 0 && left < shownLeft) pulse = pulseTime * 0.6f; // a reset was just used
                shownLeft = left;
                shownMax = max;
                counterText.text = string.Format(format, left, max);
            }

            pulse = Mathf.Max(0f, pulse - Time.unscaledDeltaTime);
            float k = pulseTime > 0f ? pulse / pulseTime : 0f;
            counterText.transform.localScale = Vector3.one * (1f + 0.3f * k);
            counterText.color = (left <= 0 || k > 0f) ? emptyColor : normalColor;
        }

        // ------------------------------------------------------------------ building the UI

        /// <summary>
        /// Creates the default counter under this object and fills the UI fields. Used at runtime when no UI
        /// is assigned, and by the editor tool (sample text is filled in; replaced at runtime).
        /// </summary>
        public void BuildUI()
        {
            var canvasGo = NewUI("Reset Counter Canvas", transform,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            uiRoot = canvasGo;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 91;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = NewUI("Panel", canvasGo.transform, typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);   // top center: clear of both the touch buttons
            prt.pivot = new Vector2(0.5f, 1f);                       // and the controls overlay
            prt.anchoredPosition = new Vector2(0f, -topMargin);

            var img = panel.GetComponent<Image>();
            img.color = panelColor;
            img.raycastTarget = false;

            var v = panel.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(28, 28, 10, 10);
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;

            var fit = panel.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = NewUI("Counter Text", panel.transform, typeof(RectTransform), typeof(Text));
            counterText = textGo.GetComponent<Text>();
            counterText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            counterText.fontSize = fontSize;
            counterText.fontStyle = FontStyle.Bold;
            counterText.alignment = TextAnchor.MiddleCenter;
            counterText.color = normalColor;
            counterText.horizontalOverflow = HorizontalWrapMode.Overflow;
            counterText.verticalOverflow = VerticalWrapMode.Overflow;
            counterText.raycastTarget = false;
            counterText.text = string.Format(format, 3, 3); // sample text
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
        /// <summary>Editor tool: Tools > Vehicle > Generate Reset Counter UI (builds editable objects in the Hierarchy).</summary>
        public static class ResetCounterUIBuilder
        {
            [MenuItem("Tools/Vehicle/Generate Reset Counter UI")]
            static void CreateFromToolsMenu() => Build(null);

            [MenuItem("GameObject/Vehicle/Reset Counter UI", false, 22)]
            static void CreateFromHierarchyMenu() => Build(null);

            public static void Build(ResetCounterUI target)
            {
                var counter = target != null ? target : Object.FindFirstObjectByType<ResetCounterUI>();

                if (counter != null && counter.uiRoot != null)
                {
                    if (counter.uiRoot == counter.gameObject)
                    {
                        EditorUtility.DisplayDialog("Reset Counter",
                            "Ui Root points at the Reset Counter object itself. Clear that field or point it at a child object, then try again.",
                            "OK");
                        return;
                    }

                    if (!EditorUtility.DisplayDialog("Reset Counter",
                            "A counter UI already exists. Rebuild it from scratch?\nAny changes you made to it will be lost.",
                            "Rebuild", "Cancel"))
                        return;

                    Undo.DestroyObjectImmediate(counter.uiRoot);
                }

                if (counter == null)
                {
                    var go = new GameObject("Reset Counter");
                    Undo.RegisterCreatedObjectUndo(go, "Create Reset Counter");
                    counter = Undo.AddComponent<ResetCounterUI>(go);
                }

                Undo.RecordObject(counter, "Generate Reset Counter UI");
                counter.BuildUI();
                Undo.RegisterCreatedObjectUndo(counter.uiRoot, "Create Reset Counter UI");

                EditorUtility.SetDirty(counter);
                if (counter.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(counter.gameObject.scene);

                Selection.activeGameObject = counter.uiRoot;
                EditorGUIUtility.PingObject(counter.uiRoot);
            }
        }

        [CustomEditor(typeof(ResetCounterUI))]
        public class ResetCounterUIEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                var counter = (ResetCounterUI)target;
                EditorGUILayout.Space();

                if (counter.uiRoot == null)
                {
                    EditorGUILayout.HelpBox(
                        "No UI assigned. A default counter is built at runtime. Generate it now to edit it in the Hierarchy / Canvas.",
                        MessageType.Info);
                    if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28)))
                        ResetCounterUIBuilder.Build(counter);
                }
                else
                {
                    if (GUILayout.Button("Select UI Root"))
                    {
                        Selection.activeGameObject = counter.uiRoot;
                        EditorGUIUtility.PingObject(counter.uiRoot);
                    }
                    if (GUILayout.Button("Rebuild Default UI (replaces your edits)"))
                        ResetCounterUIBuilder.Build(counter);
                }
            }
        }
    }
#endif
}

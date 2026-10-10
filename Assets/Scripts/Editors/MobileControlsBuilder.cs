#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace VehicleSystem.EditorTools
{
    /// <summary>
    /// Generates the mobile touch UI as real objects in the Hierarchy so you can edit it.
    /// Menu: Tools > Vehicle > Generate Mobile Controls UI   (or the buttons on the MobileControls component)
    /// IMPORTANT: this file must live in a folder named "Editor" (e.g. Assets/Scripts/Editor).
    /// </summary>
    public static class MobileControlsBuilder
    {
        [MenuItem("Tools/Vehicle/Generate Mobile Controls UI")]
        static void CreateFromToolsMenu() => Build(null);

        [MenuItem("GameObject/Vehicle/Mobile Controls UI", false, 20)]
        static void CreateFromHierarchyMenu() => Build(null);

        /// <summary>Builds the whole UI from scratch (asks first if one already exists).</summary>
        public static void Build(MobileControls target)
        {
            var mc = target != null ? target : Object.FindFirstObjectByType<MobileControls>();

            if (mc != null && mc.uiRoot != null)
            {
                if (mc.uiRoot == mc.gameObject)
                {
                    EditorUtility.DisplayDialog("Mobile Controls",
                        "Ui Root points at the Mobile Controls object itself. Clear that field or point it at a child object, then try again.",
                        "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Mobile Controls",
                        "A mobile UI already exists. Rebuild it from scratch?\nAny changes you made to its buttons will be lost.",
                        "Rebuild", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(mc.uiRoot);
            }

            if (mc == null)
            {
                var rootGo = new GameObject("Mobile Controls");
                Undo.RegisterCreatedObjectUndo(rootGo, "Create Mobile Controls");
                mc = Undo.AddComponent<MobileControls>(rootGo);
            }

            Undo.RecordObject(mc, "Generate Mobile Controls UI");

            // ---- canvas
            var canvasGo = NewUI("Mobile Controls Canvas", mc.transform,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            mc.uiRoot = canvasGo;
            CreateMissingButtons(mc);

            Finish(mc, canvasGo);
        }

        /// <summary>Creates only the buttons that are missing. Existing (edited) buttons are left alone.</summary>
        public static void AddMissingButtons(MobileControls mc)
        {
            if (mc.uiRoot == null) { Build(mc); return; }

            Undo.RecordObject(mc, "Add Missing Mobile Buttons");
            CreateMissingButtons(mc);
            Finish(mc, mc.uiRoot);
        }

        static void CreateMissingButtons(MobileControls mc)
        {
            Transform p = mc.uiRoot.transform;
            Vector2 bl = new Vector2(0f, 0f);
            Vector2 br = new Vector2(1f, 0f);
            Vector2 tr = new Vector2(1f, 1f);
            bool auto = mc.autoAccelerate;

            if (mc.left == null) mc.left = MakeButton(p, "<", bl, new Vector2(190f, 190f), 240f, mc, !mc.useTilt);
            if (mc.right == null) mc.right = MakeButton(p, ">", bl, new Vector2(470f, 190f), 240f, mc, !mc.useTilt);
            if (mc.gas == null) mc.gas = MakeButton(p, "GAS", br, new Vector2(-200f, 230f), 300f, mc, !auto);
            if (mc.brake == null) mc.brake = MakeButton(p, "BRAKE", br, new Vector2(auto ? -200f : -500f, 190f), auto ? 280f : 220f, mc, true);
            if (mc.drift == null) mc.drift = MakeButton(p, "DRIFT", br, new Vector2(-200f, auto ? 470f : 520f), 190f, mc, true);
            if (mc.lookBack == null) mc.lookBack = MakeButton(p, "BACK", tr, new Vector2(-110f, -110f), 130f, mc, true);
            if (mc.reset == null) mc.reset = MakeButton(p, "RESET", tr, new Vector2(-270f, -110f), 130f, mc, true);
        }

        static void Finish(MobileControls mc, GameObject select)
        {
            EditorUtility.SetDirty(mc);
            if (mc.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(mc.gameObject.scene);

            Selection.activeGameObject = select;
            EditorGUIUtility.PingObject(select);
        }

        static TouchHoldButton MakeButton(Transform parent, string label, Vector2 anchor, Vector2 pos,
                                          float size, MobileControls mc, bool active)
        {
            var go = NewUI(label + " Button", parent, typeof(RectTransform), typeof(Image), typeof(TouchHoldButton));

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); // rounded corners
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            img.color = mc.idleColor;

            var textGo = NewUI("Label", go.transform, typeof(RectTransform), typeof(Text));
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.RoundToInt(size * 0.2f);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false; // never block the button underneath

            var btn = go.GetComponent<TouchHoldButton>();
            btn.targetImage = img;
            btn.heldColor = mc.heldColor;

            go.SetActive(active);
            return btn;
        }

        static GameObject NewUI(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            go.layer = LayerMask.NameToLayer("UI");
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go;
        }
    }

    [CustomEditor(typeof(MobileControls))]
    public class MobileControlsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var mc = (MobileControls)target;
            EditorGUILayout.Space();

            if (mc.uiRoot == null)
            {
                EditorGUILayout.HelpBox(
                    "No UI assigned. A default UI is built at runtime. Generate it now to edit it in the Hierarchy.",
                    MessageType.Info);
                if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28)))
                    MobileControlsBuilder.Build(mc);
            }
            else
            {
                if (GUILayout.Button("Select UI Root"))
                {
                    Selection.activeGameObject = mc.uiRoot;
                    EditorGUIUtility.PingObject(mc.uiRoot);
                }
                if (GUILayout.Button("Add Missing Buttons (keeps your edits)"))
                    MobileControlsBuilder.AddMissingButtons(mc);
                if (GUILayout.Button("Rebuild Default UI (replaces your edits)"))
                    MobileControlsBuilder.Build(mc);
            }
        }
    }
}
#endif
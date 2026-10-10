#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VehicleSystem.EditorTools
{
    /// <summary>
    /// Generates the hotkey overlay as real objects in the Hierarchy so you can edit it on the Canvas.
    /// Menu: Tools > Vehicle > Generate Controls Overlay UI   (or the buttons on the ControlsOverlay component)
    /// IMPORTANT: this file must live in a folder named "Editor" (e.g. Assets/Scripts/Editor).
    /// </summary>
    public static class ControlsOverlayBuilder
    {
        [MenuItem("Tools/Vehicle/Generate Controls Overlay UI")]
        static void CreateFromToolsMenu() => Build(null);

        [MenuItem("GameObject/Vehicle/Controls Overlay UI", false, 21)]
        static void CreateFromHierarchyMenu() => Build(null);

        /// <summary>Builds the whole overlay from scratch (asks first if one already exists).</summary>
        public static void Build(ControlsOverlay target)
        {
            var overlay = target != null ? target : Object.FindFirstObjectByType<ControlsOverlay>();

            if (overlay != null && overlay.uiRoot != null)
            {
                if (overlay.uiRoot == overlay.gameObject)
                {
                    EditorUtility.DisplayDialog("Controls Overlay",
                        "Ui Root points at the Controls Overlay object itself. Clear that field or point it at a child object, then try again.",
                        "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Controls Overlay",
                        "An overlay UI already exists. Rebuild it from scratch?\nAny changes you made to it will be lost.",
                        "Rebuild", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(overlay.uiRoot);
            }

            if (overlay == null)
            {
                var go = new GameObject("Controls Overlay");
                Undo.RegisterCreatedObjectUndo(go, "Create Controls Overlay");
                overlay = Undo.AddComponent<ControlsOverlay>(go);
            }

            Undo.RecordObject(overlay, "Generate Controls Overlay UI");
            overlay.BuildUI();
            Undo.RegisterCreatedObjectUndo(overlay.uiRoot, "Create Controls Overlay UI");

            EditorUtility.SetDirty(overlay);
            if (overlay.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(overlay.gameObject.scene);

            Selection.activeGameObject = overlay.uiRoot;
            EditorGUIUtility.PingObject(overlay.uiRoot);
        }
    }

    [CustomEditor(typeof(ControlsOverlay))]
    public class ControlsOverlayEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var overlay = (ControlsOverlay)target;
            EditorGUILayout.Space();

            if (overlay.uiRoot == null)
            {
                EditorGUILayout.HelpBox(
                    "No UI assigned. A default overlay is built at runtime. Generate it now to edit it in the Hierarchy / Canvas.",
                    MessageType.Info);
                if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28)))
                    ControlsOverlayBuilder.Build(overlay);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Only the text is written at runtime. Layout, fonts and colours are yours. " +
                    "The Panel has a Content Size Fitter: remove it (and the layout groups) if you want a fixed size.",
                    MessageType.None);
                if (GUILayout.Button("Select UI Root"))
                {
                    Selection.activeGameObject = overlay.uiRoot;
                    EditorGUIUtility.PingObject(overlay.uiRoot);
                }
                if (GUILayout.Button("Rebuild Default UI (replaces your edits)"))
                    ControlsOverlayBuilder.Build(overlay);
            }
        }
    }
}
#endif

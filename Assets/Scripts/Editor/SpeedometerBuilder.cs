#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only. Lives in an "Editor" folder, so it is never included in a build.
namespace VehicleSystem.EditorTools
{
    /// <summary>
    /// Builds the speedometer as real objects in the open scene, so you can move, resize and restyle it.
    ///   Tools > Vehicle > Generate Speedometer UI
    ///   or right-click in the Hierarchy > Vehicle > Speedometer UI
    ///   or the "Generate Editable UI" button on the Speedometer component.
    /// </summary>
    public static class SpeedometerBuilder
    {
        [MenuItem("Tools/Vehicle/Generate Speedometer UI")]
        static void CreateFromToolsMenu() => Build(null);

        [MenuItem("GameObject/Vehicle/Speedometer UI", false, 25)]
        static void CreateFromHierarchyMenu() => Build(null);

        public static void Build(Speedometer target)
        {
            var speedometer = target != null ? target : Object.FindFirstObjectByType<Speedometer>();

            if (speedometer != null && speedometer.uiRoot != null)
            {
                if (speedometer.uiRoot == speedometer.gameObject)
                {
                    EditorUtility.DisplayDialog("Speedometer",
                        "Ui Root points at the Speedometer object itself. Clear that field or point it at a child object, then try again.",
                        "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Speedometer",
                        "A speedometer UI already exists. Rebuild it from scratch?\nAny changes you made to it will be lost.",
                        "Rebuild", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(speedometer.uiRoot);
            }

            if (speedometer == null)
            {
                var go = new GameObject("Speedometer");
                Undo.RegisterCreatedObjectUndo(go, "Create Speedometer");
                speedometer = Undo.AddComponent<Speedometer>(go);
            }

            Undo.RecordObject(speedometer, "Generate Speedometer UI");
            speedometer.BuildUI();
            Undo.RegisterCreatedObjectUndo(speedometer.uiRoot, "Create Speedometer UI");

            EditorUtility.SetDirty(speedometer);
            if (speedometer.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(speedometer.gameObject.scene);

            Selection.activeGameObject = speedometer.uiRoot;
            EditorGUIUtility.PingObject(speedometer.uiRoot);
        }
    }

    [CustomEditor(typeof(Speedometer))]
    public class SpeedometerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var speedometer = (Speedometer)target;
            EditorGUILayout.Space();

            if (speedometer.uiRoot == null)
            {
                EditorGUILayout.HelpBox(
                    "No UI assigned. A default speedometer is built at runtime. Generate it now to edit it in the Hierarchy / Canvas.",
                    MessageType.Info);
                if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28)))
                    SpeedometerBuilder.Build(speedometer);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Only the text, the bar length, the colours of the number and bar, and the needle angle are written at runtime. " +
                    "Layout and fonts are yours. LocalPlayerRig gives it the car to follow.",
                    MessageType.None);
                if (GUILayout.Button("Select UI Root"))
                {
                    Selection.activeGameObject = speedometer.uiRoot;
                    EditorGUIUtility.PingObject(speedometer.uiRoot);
                }
                if (GUILayout.Button("Rebuild Default UI (replaces your edits)"))
                    SpeedometerBuilder.Build(speedometer);
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only. Lives in an "Editor" folder, so it is never included in a build.
namespace VehicleSystem.EditorTools
{
    /// <summary>
    /// Builds the vehicle selector screen (and its 3D preview stage) as real objects in the open scene, so you can
    /// move, resize and restyle everything in the Hierarchy / Scene view.
    ///   Tools > Vehicle > Generate Vehicle Select UI
    ///   or right-click in the Hierarchy > Vehicle > Vehicle Select UI
    ///   or the "Generate Editable UI" button on the VehicleSelectMenu component.
    /// </summary>
    public static class VehicleSelectMenuBuilder
    {
        [MenuItem("Tools/Vehicle/Generate Vehicle Select UI")]
        static void CreateFromToolsMenu() => Build(null);

        [MenuItem("GameObject/Vehicle/Vehicle Select UI", false, 24)]
        static void CreateFromHierarchyMenu() => Build(null);

        public static void Build(VehicleSelectMenu target)
        {
            var menu = target != null ? target : Object.FindFirstObjectByType<VehicleSelectMenu>();

            if (menu != null && menu.uiRoot != null)
            {
                if (menu.uiRoot == menu.gameObject)
                {
                    EditorUtility.DisplayDialog("Vehicle Select Menu",
                        "Ui Root points at the Vehicle Select Menu object itself. Clear that field or point it at a child object, then try again.",
                        "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Vehicle Select Menu",
                        "A selector UI already exists. Rebuild it from scratch?\nAny changes you made to it will be lost.\n(The 3D preview stage is kept.)",
                        "Rebuild", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(menu.uiRoot);
            }

            if (menu == null)
            {
                var go = new GameObject("Vehicle Select Menu");
                Undo.RegisterCreatedObjectUndo(go, "Create Vehicle Select Menu");
                menu = Undo.AddComponent<VehicleSelectMenu>(go);
            }

            var stageBefore = menu.previewStage;

            Undo.RecordObject(menu, "Generate Vehicle Select UI");
            menu.BuildUI();
            Undo.RegisterCreatedObjectUndo(menu.uiRoot, "Create Vehicle Select UI");
            if (stageBefore == null && menu.previewStage != null)
                Undo.RegisterCreatedObjectUndo(menu.previewStage.gameObject, "Create Vehicle Preview Stage");

            EditorUtility.SetDirty(menu);
            if (menu.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);

            Selection.activeGameObject = menu.uiRoot;
            EditorGUIUtility.PingObject(menu.uiRoot);
        }
    }

    [CustomEditor(typeof(VehicleSelectMenu))]
    public class VehicleSelectMenuEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var menu = (VehicleSelectMenu)target;
            EditorGUILayout.Space();

            if (menu.catalog == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Vehicle Catalog (Assets > Create > Vehicle System > Vehicle Catalog) and add your vehicles to it.",
                    MessageType.Warning);
            }

            if (menu.uiRoot == null)
            {
                EditorGUILayout.HelpBox(
                    "No UI assigned. A default screen is built at runtime. Generate it now to edit it in the Hierarchy / Canvas.",
                    MessageType.Info);
                if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28)))
                    VehicleSelectMenuBuilder.Build(menu);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Only the text, the stat bar lengths and the preview picture are written at runtime. " +
                    "Layout, fonts and colours are yours. The 3D studio is the 'Vehicle Preview Stage' object.",
                    MessageType.None);
                if (GUILayout.Button("Select UI Root"))
                {
                    Selection.activeGameObject = menu.uiRoot;
                    EditorGUIUtility.PingObject(menu.uiRoot);
                }
                if (GUILayout.Button("Rebuild Default UI (replaces your edits)"))
                    VehicleSelectMenuBuilder.Build(menu);
            }
        }
    }

    [CustomEditor(typeof(VehiclePreviewStage))]
    public class VehiclePreviewStageEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var stage = (VehiclePreviewStage)target;
            EditorGUILayout.Space();

            if (stage.turntable == null || stage.previewCamera == null || stage.stageLight == null)
            {
                EditorGUILayout.HelpBox(
                    "Some stage objects are missing. They are created at runtime, or create them now to edit them in the Hierarchy.",
                    MessageType.Info);
                if (GUILayout.Button("Create Missing Stage Objects", GUILayout.Height(26)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(stage.gameObject, "Create Preview Stage Objects");
                    stage.BuildStage();
                    EditorUtility.SetDirty(stage);
                    if (stage.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "The preview camera is switched off here on purpose: it only renders while the game runs. " +
                    "Tune Camera Elevation, Frame Padding and the light; spawn a vehicle in Play Mode to see the result.",
                    MessageType.None);
            }
        }
    }
}
#endif

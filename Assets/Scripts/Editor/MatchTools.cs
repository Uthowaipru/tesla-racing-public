#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only. Lives in an "Editor" folder, so it is never included in a build.
namespace VehicleSystem.EditorTools
{
    /// <summary>
    /// GameObject > Vehicle > Match Manager        creates the match (timer, countdown, scoring) in the scene.
    /// Tools > Vehicle > Generate Match HUD UI     builds the match HUD (time, points, position, countdown, pop-ups, results) as
    ///                                             real objects you can restyle (or the button on the Match Hud component).
    /// </summary>
    public static class MatchTools
    {
        [MenuItem("GameObject/Vehicle/Match Manager", false, 33)]
        static void CreateMatchManager()
        {
            var existing = Object.FindFirstObjectByType<MatchManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var go = new GameObject("Match Manager");
            Undo.RegisterCreatedObjectUndo(go, "Create Match Manager");
            var manager = Undo.AddComponent<MatchManager>(go);
            manager.road = Object.FindFirstObjectByType<RoadGenerator>();
            manager.wrongWayTracker = Object.FindFirstObjectByType<WrongWayTracker>();

            if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/Vehicle/Generate Match HUD UI")]
        static void CreateFromToolsMenu() => BuildHud(null);

        [MenuItem("GameObject/Vehicle/Match HUD UI", false, 34)]
        static void CreateFromHierarchyMenu() => BuildHud(null);

        public static void BuildHud(MatchHud target)
        {
            var hud = target != null ? target : Object.FindFirstObjectByType<MatchHud>();

            if (hud != null && hud.uiRoot != null)
            {
                if (hud.uiRoot == hud.gameObject)
                {
                    EditorUtility.DisplayDialog("Match HUD",
                        "Ui Root points at the Match HUD object itself. Clear that field or point it at a child object, then try again.", "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Match HUD",
                        "A match HUD already exists. Rebuild it from scratch?\nAny changes you made to it will be lost.", "Rebuild", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(hud.uiRoot);
            }

            if (hud == null)
            {
                var go = new GameObject("Match HUD");
                Undo.RegisterCreatedObjectUndo(go, "Create Match HUD");
                hud = Undo.AddComponent<MatchHud>(go);
            }

            Undo.RecordObject(hud, "Generate Match HUD UI");
            hud.BuildUI();
            Undo.RegisterCreatedObjectUndo(hud.uiRoot, "Create Match HUD UI");

            EditorUtility.SetDirty(hud);
            if (hud.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);

            Selection.activeGameObject = hud.uiRoot;
            EditorGUIUtility.PingObject(hud.uiRoot);
        }
    }

    [CustomEditor(typeof(MatchHud))]
    public class MatchHudEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var hud = (MatchHud)target;
            EditorGUILayout.Space();

            if (hud.uiRoot == null)
            {
                EditorGUILayout.HelpBox("No UI assigned. A default HUD is built at runtime. Generate it now to edit it in the Hierarchy / Canvas.", MessageType.Info);
                if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28))) MatchTools.BuildHud(hud);
            }
            else
            {
                EditorGUILayout.HelpBox("The results screen is switched off in the editor; switch 'Results Panel' on to restyle it, then off again.", MessageType.None);
                if (GUILayout.Button("Select UI Root")) { Selection.activeGameObject = hud.uiRoot; EditorGUIUtility.PingObject(hud.uiRoot); }
                if (GUILayout.Button("Rebuild Default UI (replaces your edits)")) MatchTools.BuildHud(hud);
            }
        }
    }

    [CustomEditor(typeof(MatchManager))]
    public class MatchManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Vehicles join the match through VehicleSpawner automatically. The timer, the countdown and every point value are above. " +
                "Add a Match HUD (Tools > Vehicle > Generate Match HUD UI) to see them; LocalPlayerRig creates a default one if you do not.",
                MessageType.None);
        }
    }
}
#endif

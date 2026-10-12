#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only. Lives in an "Editor" folder, so it is never included in a build.
namespace VehicleSystem.EditorTools
{
    /// <summary>
    /// Tools > Vehicle > Add Damage And Effects To Selected Vehicle
    ///   Select a vehicle (in a scene or in prefab mode). It adds the grip assist, impact sensor, health, effects and name tag
    ///   components, and builds the floating name tag as real objects in the vehicle. The vehicle already shows the project's
    ///   default particle effects without any further setup.
    ///
    /// Tools > Vehicle > Create Editable Effects On Selected Vehicle
    ///   Only when this vehicle should have its OWN particles: puts editable copies of the default effects inside the
    ///   vehicle (under "Effects"), where you can restyle them or replace them with your own.
    /// </summary>
    public static class VehicleDamageSetup
    {
        const string AddPath = "Tools/Vehicle/Add Damage And Effects To Selected Vehicle";
        const string EditablePath = "Tools/Vehicle/Create Editable Effects On Selected Vehicle";

        [MenuItem(AddPath, true)]
        static bool ValidateAdd() => FindVehicle(Selection.activeGameObject) != null;

        [MenuItem(EditablePath, true)]
        static bool ValidateEditable() => FindVehicle(Selection.activeGameObject) != null;

        [MenuItem(AddPath)]
        static void AddComponents()
        {
            var vehicle = FindVehicle(Selection.activeGameObject);
            if (vehicle == null) return;

            var go = vehicle.gameObject;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add Damage And Effects");

            DefaultEffectsBuilder.EnsureDefaults();

            if (go.GetComponent<VehicleGripAssist>() == null) Undo.AddComponent<VehicleGripAssist>(go);
            if (go.GetComponent<VehicleImpactSensor>() == null) Undo.AddComponent<VehicleImpactSensor>(go);
            if (go.GetComponent<VehicleHealth>() == null) Undo.AddComponent<VehicleHealth>(go);
            if (go.GetComponent<VehicleEffects>() == null) Undo.AddComponent<VehicleEffects>(go);
            if (go.GetComponent<VehicleBoost>() == null) Undo.AddComponent<VehicleBoost>(go);

            var tag = go.GetComponent<VehicleNameTag>();
            if (tag == null) tag = Undo.AddComponent<VehicleNameTag>(go);
            if (tag.uiRoot == null)
            {
                Undo.RecordObject(tag, "Build Name Tag");
                tag.BuildUI();
                Undo.RegisterCreatedObjectUndo(tag.uiRoot, "Create Name Tag");
                EditorUtility.SetDirty(tag);
            }

            MarkDirty(go);
            Selection.activeGameObject = go;
            Debug.Log("Vehicle Damage Setup: components added to '" + go.name + "'. The vehicle uses the default effects until you give it its own.", go);
        }

        [MenuItem(EditablePath)]
        static void CreateEditableEffects()
        {
            var vehicle = FindVehicle(Selection.activeGameObject);
            if (vehicle == null) return;

            DefaultEffectsBuilder.EnsureDefaults();
            var defaults = AssetDatabase.LoadAssetAtPath<VehicleEffectsDefaults>(DefaultEffectsBuilder.AssetPath);
            if (defaults == null)
            {
                Debug.LogWarning("Vehicle Damage Setup: the default effects do not exist yet. Run Tools > Vehicle > Create Default Effects.");
                return;
            }

            var go = vehicle.gameObject;
            var effects = go.GetComponent<VehicleEffects>();
            if (effects == null) effects = Undo.AddComponent<VehicleEffects>(go);
            Undo.RecordObject(effects, "Create Editable Effects");

            Bounds b = LocalBounds(go);
            Transform root = go.transform.Find("Effects");
            if (root == null)
            {
                var rootGo = new GameObject("Effects");
                Undo.RegisterCreatedObjectUndo(rootGo, "Create Effects Root");
                rootGo.transform.SetParent(go.transform, false);
                root = rootGo.transform;
            }

            Vector3 rear = new Vector3(b.center.x, b.min.y + 0.2f, b.min.z);
            Vector3 top = new Vector3(b.center.x, b.min.y + b.size.y * 0.55f, b.center.z);

            if (effects.trail == null) effects.trail = Copy(defaults.trail, "Trail FX", root, rear, Quaternion.Euler(0f, 180f, 0f));
            if (effects.hitSparks == null) effects.hitSparks = Copy(defaults.hitSparks, "Hit Sparks FX", root, b.center, Quaternion.identity);
            if (effects.hitDust == null) effects.hitDust = Copy(defaults.hitDust, "Hit Dust FX", root, b.center, Quaternion.identity);
            if (effects.lowHealthSmoke == null) effects.lowHealthSmoke = Copy(defaults.lowHealthSmoke, "Low Health Smoke FX", root, top, Quaternion.Euler(-90f, 0f, 0f));
            if (effects.explosion == null) effects.explosion = Copy(defaults.explosion, "Explosion FX", root, b.center, Quaternion.identity);

            EditorUtility.SetDirty(effects);
            MarkDirty(go);
            Selection.activeGameObject = root.gameObject;
        }

        // ------------------------------------------------------------------ helpers

        static ParticleSystem Copy(ParticleSystem prefab, string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            if (prefab == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); // own copy
            Undo.RegisterCreatedObjectUndo(instance, "Create " + name);

            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = Vector3.one;

            var ps = instance.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        static VehicleController FindVehicle(GameObject selected)
        {
            if (selected == null) return null;
            var vc = selected.GetComponentInChildren<VehicleController>(true);
            return vc != null ? vc : selected.GetComponentInParent<VehicleController>();
        }

        static void MarkDirty(GameObject go)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
            else if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
        }

        /// <summary>Bounds of the vehicle's meshes in its own frame.</summary>
        static Bounds LocalBounds(GameObject go)
        {
            bool has = false;
            Bounds b = new Bounds(Vector3.up * 0.8f, new Vector3(1.5f, 1.6f, 3f));
            var t = go.transform;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

                Bounds w = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = w.center + Vector3.Scale(w.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 local = t.InverseTransformPoint(corner);
                    if (!has) { b = new Bounds(local, Vector3.zero); has = true; }
                    else b.Encapsulate(local);
                }
            }
            return b;
        }
    }

    // ====================================================================== HUD builders

    /// <summary>Tools > Vehicle > Generate Health Bar UI (or the button on the Health Bar component).</summary>
    public static class HealthBarBuilder
    {
        [MenuItem("Tools/Vehicle/Generate Health Bar UI")]
        static void CreateFromToolsMenu() => Build(null);

        [MenuItem("GameObject/Vehicle/Health Bar UI", false, 26)]
        static void CreateFromHierarchyMenu() => Build(null);

        public static void Build(HealthBar target)
        {
            var bar = target != null ? target : Object.FindFirstObjectByType<HealthBar>();

            if (bar != null && bar.uiRoot != null)
            {
                if (bar.uiRoot == bar.gameObject)
                {
                    EditorUtility.DisplayDialog("Health Bar",
                        "Ui Root points at the Health Bar object itself. Clear that field or point it at a child object, then try again.", "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Health Bar",
                        "A health bar UI already exists. Rebuild it from scratch?\nAny changes you made to it will be lost.",
                        "Rebuild", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(bar.uiRoot);
            }

            if (bar == null)
            {
                var go = new GameObject("Health Bar");
                Undo.RegisterCreatedObjectUndo(go, "Create Health Bar");
                bar = Undo.AddComponent<HealthBar>(go);
            }

            Undo.RecordObject(bar, "Generate Health Bar UI");
            bar.BuildUI();
            Undo.RegisterCreatedObjectUndo(bar.uiRoot, "Create Health Bar UI");

            EditorUtility.SetDirty(bar);
            if (bar.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(bar.gameObject.scene);

            Selection.activeGameObject = bar.uiRoot;
            EditorGUIUtility.PingObject(bar.uiRoot);
        }
    }

    /// <summary>Tools > Vehicle > Generate Respawn Countdown UI (or the button on the Respawn Countdown component).</summary>
    public static class RespawnCountdownBuilder
    {
        [MenuItem("Tools/Vehicle/Generate Respawn Countdown UI")]
        static void CreateFromToolsMenu() => Build(null);

        [MenuItem("GameObject/Vehicle/Respawn Countdown UI", false, 27)]
        static void CreateFromHierarchyMenu() => Build(null);

        public static void Build(RespawnCountdown target)
        {
            var countdown = target != null ? target : Object.FindFirstObjectByType<RespawnCountdown>();

            if (countdown != null && countdown.uiRoot != null)
            {
                if (countdown.uiRoot == countdown.gameObject)
                {
                    EditorUtility.DisplayDialog("Respawn Countdown",
                        "Ui Root points at the Respawn Countdown object itself. Clear that field or point it at a child object, then try again.", "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Respawn Countdown",
                        "A countdown UI already exists. Rebuild it from scratch?\nAny changes you made to it will be lost.",
                        "Rebuild", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(countdown.uiRoot);
            }

            if (countdown == null)
            {
                var go = new GameObject("Respawn Countdown");
                Undo.RegisterCreatedObjectUndo(go, "Create Respawn Countdown");
                countdown = Undo.AddComponent<RespawnCountdown>(go);
            }

            Undo.RecordObject(countdown, "Generate Respawn Countdown UI");
            countdown.BuildUI();
            Undo.RegisterCreatedObjectUndo(countdown.uiRoot, "Create Respawn Countdown UI");

            EditorUtility.SetDirty(countdown);
            if (countdown.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(countdown.gameObject.scene);

            Selection.activeGameObject = countdown.uiRoot;
            EditorGUIUtility.PingObject(countdown.uiRoot);
        }
    }

    // ====================================================================== inspectors

    [CustomEditor(typeof(HealthBar))]
    public class HealthBarEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var bar = (HealthBar)target;
            EditorGUILayout.Space();

            if (bar.uiRoot == null)
            {
                EditorGUILayout.HelpBox("No UI assigned. A default bar is built at runtime. Generate it now to edit it in the Hierarchy / Canvas.", MessageType.Info);
                if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28))) HealthBarBuilder.Build(bar);
            }
            else
            {
                if (GUILayout.Button("Select UI Root")) { Selection.activeGameObject = bar.uiRoot; EditorGUIUtility.PingObject(bar.uiRoot); }
                if (GUILayout.Button("Rebuild Default UI (replaces your edits)")) HealthBarBuilder.Build(bar);
            }
        }
    }

    [CustomEditor(typeof(RespawnCountdown))]
    public class RespawnCountdownEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var countdown = (RespawnCountdown)target;
            EditorGUILayout.Space();

            if (countdown.uiRoot == null)
            {
                EditorGUILayout.HelpBox("No UI assigned. A default countdown is built at runtime. Generate it now to edit it in the Hierarchy / Canvas.", MessageType.Info);
                if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28))) RespawnCountdownBuilder.Build(countdown);
            }
            else
            {
                if (GUILayout.Button("Select UI Root")) { Selection.activeGameObject = countdown.uiRoot; EditorGUIUtility.PingObject(countdown.uiRoot); }
                if (GUILayout.Button("Rebuild Default UI (replaces your edits)")) RespawnCountdownBuilder.Build(countdown);
            }
        }
    }

    [CustomEditor(typeof(VehicleNameTag))]
    public class VehicleNameTagEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var tag = (VehicleNameTag)target;
            EditorGUILayout.Space();

            if (tag.uiRoot == null)
            {
                EditorGUILayout.HelpBox("No tag UI yet. It is built at runtime. Generate it now to edit it inside this vehicle.", MessageType.Info);
                if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28))) Build(tag);
            }
            else
            {
                if (GUILayout.Button("Select UI Root")) { Selection.activeGameObject = tag.uiRoot; EditorGUIUtility.PingObject(tag.uiRoot); }
                if (GUILayout.Button("Rebuild Default UI (replaces your edits)"))
                {
                    Undo.DestroyObjectImmediate(tag.uiRoot);
                    Build(tag);
                }
            }
        }

        static void Build(VehicleNameTag tag)
        {
            Undo.RecordObject(tag, "Build Name Tag");
            tag.BuildUI();
            Undo.RegisterCreatedObjectUndo(tag.uiRoot, "Create Name Tag");
            EditorUtility.SetDirty(tag);
            if (tag.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(tag.gameObject.scene);
        }
    }

    [CustomEditor(typeof(VehicleEffects))]
    public class VehicleEffectsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Empty slots use the project's default effects (Assets/Resources/VehicleSystem). To give this vehicle its own, " +
                "assign your ParticleSystem (an object in this vehicle, or a prefab) to a slot, or create editable copies below.",
                MessageType.Info);

            if (GUILayout.Button("Create Editable Effects On This Vehicle"))
            {
                Selection.activeGameObject = ((VehicleEffects)target).gameObject;
                EditorApplication.ExecuteMenuItem("Tools/Vehicle/Create Editable Effects On Selected Vehicle");
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Editor-only. Lives in an "Editor" folder, so it is never included in a build.
namespace VehicleSystem.EditorTools
{
    /// <summary>
    /// Creates the two pickup prefabs (Battery Pickup, Repair Kit Pickup) once by itself when the project loads, from simple
    /// shapes and colour materials that work in any render pipeline. They land in Assets/VehicleSystem/Pickups: replace the
    /// models with your own art any time, the Pickup component on the root is what matters.
    ///
    ///   Right-click in the Hierarchy > Vehicle > Battery Pickup / Repair Kit Pickup puts one in the scene (to test: set
    ///   "Respawn Time" to a few seconds so it comes back).
    ///   Tools > Vehicle > Create Pickup Prefabs recreates the prefabs.
    /// </summary>
    [InitializeOnLoad]
    public static class PickupPrefabBuilder
    {
        const string Folder = "Assets/VehicleSystem/Pickups";
        const string BatteryPath = Folder + "/Battery Pickup.prefab";
        const string RepairPath = Folder + "/Repair Kit Pickup.prefab";

        static PickupPrefabBuilder()
        {
            EditorApplication.delayCall += EnsurePrefabs;
        }

        static void EnsurePrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BatteryPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(RepairPath) != null) return;
            Create();
        }

        [MenuItem("Tools/Vehicle/Create Pickup Prefabs")]
        static void CreateFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BatteryPath) != null &&
                !EditorUtility.DisplayDialog("Pickup Prefabs",
                    "The pickup prefabs already exist. Recreate them?\nChanges you made to them will be lost.", "Recreate", "Cancel"))
                return;
            Create();
        }

        [MenuItem("GameObject/Vehicle/Battery Pickup", false, 30)]
        static void PlaceBattery() => Place(BatteryPath);

        [MenuItem("GameObject/Vehicle/Repair Kit Pickup", false, 31)]
        static void PlaceRepairKit() => Place(RepairPath);

        static void Place(string path)
        {
            EnsurePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Create Pickup");

            var view = SceneView.lastActiveSceneView;
            instance.transform.position = view != null ? view.pivot : Vector3.zero;
            Selection.activeGameObject = instance;
        }

        // ------------------------------------------------------------------ building

        static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/VehicleSystem")) AssetDatabase.CreateFolder("Assets", "VehicleSystem");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/VehicleSystem", "Pickups");

            Material green = MakeMaterial("Pickup Green", new Color(0.2f, 0.85f, 0.3f));
            Material yellow = MakeMaterial("Pickup Yellow", new Color(1f, 0.85f, 0.2f));
            Material red = MakeMaterial("Pickup Red", new Color(0.9f, 0.2f, 0.2f));
            Material white = MakeMaterial("Pickup White", Color.white);

            // ---- battery: a green cell with a yellow cap
            var battery = NewPickup("Battery Pickup", PickupType.Battery, out Transform batteryVisual);
            Prim(PrimitiveType.Cylinder, "Body", batteryVisual, Vector3.zero, new Vector3(0.45f, 0.38f, 0.45f), green);
            Prim(PrimitiveType.Cylinder, "Cap", batteryVisual, new Vector3(0f, 0.44f, 0f), new Vector3(0.2f, 0.07f, 0.2f), yellow);
            Save(battery, BatteryPath);

            // ---- repair kit: a red box with a white cross
            var kit = NewPickup("Repair Kit Pickup", PickupType.RepairKit, out Transform kitVisual);
            Prim(PrimitiveType.Cube, "Box", kitVisual, Vector3.zero, new Vector3(0.75f, 0.5f, 0.75f), red);
            Prim(PrimitiveType.Cube, "Cross A", kitVisual, new Vector3(0f, 0.28f, 0f), new Vector3(0.5f, 0.06f, 0.14f), white);
            Prim(PrimitiveType.Cube, "Cross B", kitVisual, new Vector3(0f, 0.28f, 0f), new Vector3(0.14f, 0.06f, 0.5f), white);
            Save(kit, RepairPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Pickups: prefabs created in " + Folder + ". Drop them on the road, or replace the models with your own art.");
        }

        static GameObject NewPickup(string name, PickupType type, out Transform visual)
        {
            var root = new GameObject(name);

            var trigger = root.AddComponent<SphereCollider>(); // Pickup requires a collider, so this goes first
            trigger.isTrigger = true;
            trigger.radius = 1.3f;
            trigger.center = new Vector3(0f, 0.9f, 0f);

            var pickup = root.AddComponent<Pickup>();
            pickup.type = type;

            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(root.transform, false);
            visualGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            pickup.visual = visualGo.transform;

            visual = visualGo.transform;
            return root;
        }

        static void Prim(PrimitiveType shape, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // the pickup's own trigger does the work
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        static Material MakeMaterial(string name, Color color)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            // the default material of the project's render pipeline, so it is never pink
            Shader shader = null;
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultMaterial != null) shader = pipeline.defaultMaterial.shader;
            if (shader == null) shader = Shader.Find("Standard");

            material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }

    /// <summary>Tools > Vehicle > Generate Pickup HUD (or the button on the Pickup Hud component).</summary>
    public static class PickupHudBuilder
    {
        [MenuItem("Tools/Vehicle/Generate Pickup HUD")]
        static void CreateFromToolsMenu() => Build(null);

        [MenuItem("GameObject/Vehicle/Pickup HUD", false, 28)]
        static void CreateFromHierarchyMenu() => Build(null);

        public static void Build(PickupHud target)
        {
            var hud = target != null ? target : Object.FindFirstObjectByType<PickupHud>();

            if (hud != null && hud.uiRoot != null)
            {
                if (hud.uiRoot == hud.gameObject)
                {
                    EditorUtility.DisplayDialog("Pickup HUD",
                        "Ui Root points at the Pickup HUD object itself. Clear that field or point it at a child object, then try again.", "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Pickup HUD",
                        "A pickup HUD already exists. Rebuild it from scratch?\nAny changes you made to it will be lost.", "Rebuild", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(hud.uiRoot);
            }

            if (hud == null)
            {
                var go = new GameObject("Pickup HUD");
                Undo.RegisterCreatedObjectUndo(go, "Create Pickup HUD");
                hud = Undo.AddComponent<PickupHud>(go);
            }

            Undo.RecordObject(hud, "Generate Pickup HUD");
            hud.BuildUI();
            Undo.RegisterCreatedObjectUndo(hud.uiRoot, "Create Pickup HUD UI");

            EditorUtility.SetDirty(hud);
            if (hud.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);

            Selection.activeGameObject = hud.uiRoot;
            EditorGUIUtility.PingObject(hud.uiRoot);
        }
    }

    [CustomEditor(typeof(PickupHud))]
    public class PickupHudEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var hud = (PickupHud)target;
            EditorGUILayout.Space();

            if (hud.uiRoot == null)
            {
                EditorGUILayout.HelpBox("No UI assigned. A default panel is built at runtime. Generate it now to edit it in the Hierarchy / Canvas.", MessageType.Info);
                if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28))) PickupHudBuilder.Build(hud);
            }
            else
            {
                if (GUILayout.Button("Select UI Root")) { Selection.activeGameObject = hud.uiRoot; EditorGUIUtility.PingObject(hud.uiRoot); }
                if (GUILayout.Button("Rebuild Default UI (replaces your edits)")) PickupHudBuilder.Build(hud);
            }
        }
    }
}
#endif

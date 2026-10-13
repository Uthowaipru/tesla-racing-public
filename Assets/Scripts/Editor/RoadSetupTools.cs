#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Editor-only. Lives in an "Editor" folder, so it is never included in a build.
namespace VehicleSystem.EditorTools
{
    /// <summary>
    /// Creates simple default road pieces and traffic vehicles (made of plain shapes and colours, so they work in any render
    /// pipeline) once by itself when the project loads. They land in Assets/VehicleSystem/Road. They are placeholders: build your
    /// own Dhaka streets and vehicles as prefabs with the same components and swap them in.
    ///
    ///   GameObject > Vehicle > Road System   creates a Road System (road + traffic + pickups) in the scene, filled with these.
    ///   Tools > Vehicle > Create Road Pieces recreates the default prefabs.
    /// </summary>
    [InitializeOnLoad]
    public static class RoadPrefabBuilder
    {
        const string Folder = "Assets/VehicleSystem/Road";
        public const string StraightPath = Folder + "/Road Straight.prefab";
        public const string CurveLeftPath = Folder + "/Road Curve Left.prefab";
        public const string CurveRightPath = Folder + "/Road Curve Right.prefab";
        public const string CarPath = Folder + "/Traffic Car.prefab";
        public const string CngPath = Folder + "/Traffic CNG.prefab";
        public const string BusPath = Folder + "/Traffic Bus.prefab";
        public const string TruckPath = Folder + "/Traffic Truck.prefab";

        const string BatteryPickupPath = "Assets/VehicleSystem/Pickups/Battery Pickup.prefab";
        const string RepairPickupPath = "Assets/VehicleSystem/Pickups/Repair Kit Pickup.prefab";

        static RoadPrefabBuilder()
        {
            EditorApplication.delayCall += Ensure;
        }

        static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StraightPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(CarPath) != null) return;
            Create();
        }

        [MenuItem("Tools/Vehicle/Create Road Pieces")]
        static void CreateFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StraightPath) != null &&
                !EditorUtility.DisplayDialog("Road Pieces",
                    "The default road pieces and traffic vehicles already exist. Recreate them?\nChanges you made to them will be lost.",
                    "Recreate", "Cancel"))
                return;
            Create();
        }

        [MenuItem("GameObject/Vehicle/Road System", false, 29)]
        static void CreateRoadSystem()
        {
            Ensure();

            var go = new GameObject("Road System");
            Undo.RegisterCreatedObjectUndo(go, "Create Road System");
            var generator = Undo.AddComponent<RoadGenerator>(go);
            Undo.AddComponent<TrafficManager>(go);
            Undo.AddComponent<PickupPlacer>(go);

            FillDefaults(generator);

            if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
        }

        /// <summary>Assigns the default road pieces, traffic vehicles and pickups to the Road System on this object.</summary>
        public static void FillDefaults(RoadGenerator generator)
        {
            var traffic = generator.GetComponent<TrafficManager>();
            var pickups = generator.GetComponent<PickupPlacer>();

            Undo.RecordObject(generator, "Fill Road Defaults");
            generator.chunkPrefabs = Compact(Load<RoadChunk>(StraightPath), Load<RoadChunk>(CurveLeftPath), Load<RoadChunk>(CurveRightPath));
            EditorUtility.SetDirty(generator);

            if (traffic != null)
            {
                Undo.RecordObject(traffic, "Fill Road Defaults");
                traffic.road = generator;
                traffic.vehiclePrefabs = Compact(Load<TrafficVehicle>(CarPath), Load<TrafficVehicle>(CngPath),
                                                 Load<TrafficVehicle>(BusPath), Load<TrafficVehicle>(TruckPath));
                EditorUtility.SetDirty(traffic);
            }

            if (pickups != null)
            {
                Undo.RecordObject(pickups, "Fill Road Defaults");
                pickups.road = generator;
                pickups.batteryPrefab = Load<Pickup>(BatteryPickupPath);
                pickups.repairKitPrefab = Load<Pickup>(RepairPickupPath);
                EditorUtility.SetDirty(pickups);
            }
        }

        static T Load<T>(string path) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return go != null ? go.GetComponent<T>() : null;
        }

        static T[] Compact<T>(params T[] items) where T : Object
        {
            var list = new List<T>();
            foreach (var item in items) if (item != null) list.Add(item);
            return list.ToArray();
        }

        // ------------------------------------------------------------------ building

        static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/VehicleSystem")) AssetDatabase.CreateFolder("Assets", "VehicleSystem");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/VehicleSystem", "Road");

            Material asphalt = MakeMaterial("Road Asphalt", new Color(0.22f, 0.22f, 0.24f));
            Material curb = MakeMaterial("Road Curb", new Color(0.62f, 0.6f, 0.56f));
            Material marking = MakeMaterial("Road Marking", new Color(0.92f, 0.92f, 0.85f));
            Material glass = MakeMaterial("Traffic Glass", new Color(0.12f, 0.16f, 0.2f));

            // a piece is 4 segments of 15 m; a curve turns 7.5 degrees per segment (30 in total)
            MakeChunk("Road Straight", StraightPath, 0f, 3f, 0, asphalt, curb, marking);
            MakeChunk("Road Curve Left", CurveLeftPath, -7.5f, 1f, 2, asphalt, curb, marking);
            MakeChunk("Road Curve Right", CurveRightPath, 7.5f, 1f, 2, asphalt, curb, marking);

            MakeTraffic("Traffic Car", CarPath, new Vector3(1.8f, 1.5f, 4.2f), 1100f, MakeMaterial("Traffic Blue", new Color(0.2f, 0.4f, 0.85f)), glass, 0, -1, 5f);
            MakeTraffic("Traffic CNG", CngPath, new Vector3(1.4f, 1.7f, 2.6f), 600f, MakeMaterial("Traffic Green", new Color(0.2f, 0.7f, 0.35f)), glass, 0, -1, 3f);
            MakeTraffic("Traffic Bus", BusPath, new Vector3(2.4f, 3f, 11f), 7000f, MakeMaterial("Traffic Red", new Color(0.85f, 0.2f, 0.2f)), glass, 0, 0, 1f);
            MakeTraffic("Traffic Truck", TruckPath, new Vector3(2.4f, 2.9f, 7f), 5000f, MakeMaterial("Traffic Orange", new Color(0.95f, 0.55f, 0.15f)), glass, 0, 1, 1f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Road: default road pieces and traffic vehicles created in " + Folder + ". Replace them with your own art any time.");
        }

        static void MakeChunk(string name, string path, float turnPerSegment, float weight, int firstIndex,
                              Material asphalt, Material curb, Material marking)
        {
            const int segments = 4;
            const float segmentLength = 15f;
            const int lanes = 3;
            const float laneWidth = 3.2f;
            float width = lanes * laneWidth;

            var root = new GameObject(name);
            var chunk = root.AddComponent<RoadChunk>();
            chunk.laneCount = lanes;
            chunk.laneWidth = laneWidth;
            chunk.weight = weight;
            chunk.firstIndex = firstIndex;

            var points = new List<Transform>();
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            points.Add(MakePoint(root.transform, "P0", position));

            for (int i = 0; i < segments; i++)
            {
                rotation *= Quaternion.Euler(0f, turnPerSegment * 0.5f, 0f);
                Vector3 forward = rotation * Vector3.forward;
                Vector3 right = rotation * Vector3.right;
                Vector3 middle = position + forward * (segmentLength * 0.5f);
                float pieceLength = segmentLength + 0.6f; // a little overlap, so bends have no gaps

                Prim(PrimitiveType.Cube, "Road " + i, root.transform, middle + Vector3.down * 0.1f, rotation,
                     new Vector3(width, 0.2f, pieceLength), asphalt, true);
                Prim(PrimitiveType.Cube, "Curb Left " + i, root.transform, middle - right * (width * 0.5f + 0.25f) + Vector3.up * 0.15f, rotation,
                     new Vector3(0.5f, 0.5f, pieceLength), curb, true);
                Prim(PrimitiveType.Cube, "Curb Right " + i, root.transform, middle + right * (width * 0.5f + 0.25f) + Vector3.up * 0.15f, rotation,
                     new Vector3(0.5f, 0.5f, pieceLength), curb, true);

                for (int d = 1; d < lanes; d++)
                {
                    float x = (d - lanes * 0.5f) * laneWidth;
                    Prim(PrimitiveType.Cube, "Lane Mark " + d, root.transform, middle + right * x + Vector3.up * 0.011f, rotation,
                         new Vector3(0.12f, 0.02f, 6f), marking, false);
                }

                position += forward * segmentLength;
                rotation *= Quaternion.Euler(0f, turnPerSegment * 0.5f, 0f);
                points.Add(MakePoint(root.transform, "P" + (i + 1), position));
            }

            chunk.centerline = points.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        static void MakeTraffic(string name, string path, Vector3 size, float mass, Material body, Material glass,
                                int minLane, int maxLane, float weight)
        {
            var root = new GameObject(name);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var box = root.AddComponent<BoxCollider>();
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);

            Prim(PrimitiveType.Cube, "Body", root.transform, new Vector3(0f, size.y * 0.35f, 0f), Quaternion.identity,
                 new Vector3(size.x, size.y * 0.7f, size.z), body, false);
            Prim(PrimitiveType.Cube, "Cabin", root.transform, new Vector3(0f, size.y * 0.82f, -size.z * 0.05f), Quaternion.identity,
                 new Vector3(size.x * 0.9f, size.y * 0.36f, size.z * 0.55f), glass, false);

            var vehicle = root.AddComponent<TrafficVehicle>();
            vehicle.length = size.z;
            vehicle.weight = weight;
            vehicle.minLane = minLane;
            vehicle.maxLane = maxLane;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        static Transform MakePoint(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        static void Prim(PrimitiveType shape, string name, Transform parent, Vector3 position, Quaternion rotation,
                         Vector3 scale, Material material, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
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

    [CustomEditor(typeof(RoadGenerator))]
    public class RoadGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var generator = (RoadGenerator)target;
            EditorGUILayout.Space();

            if (generator.chunkPrefabs == null || generator.chunkPrefabs.Length == 0)
            {
                EditorGUILayout.HelpBox("No road pieces yet. Fill in the default pieces, traffic and pickups, or assign your own prefabs.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Same seed = same road on every device. Offline levels use a fixed seed; a room uses the host's seed. " +
                    "Add TrafficManager and PickupPlacer next to this component for traffic and pickups.",
                    MessageType.None);
            }

            if (GUILayout.Button("Fill With Default Pieces, Traffic And Pickups"))
                RoadPrefabBuilder.FillDefaults(generator);
        }
    }
}
#endif

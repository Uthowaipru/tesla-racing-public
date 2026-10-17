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
    /// Creates simple default road pieces (one-way and two-way) and traffic vehicles, made of plain shapes and colours so they
    /// work in any render pipeline, once by itself when the project loads. They land in Assets/VehicleSystem/Road. They are
    /// placeholders: build your own Dhaka streets and vehicles as prefabs with the same components and swap them in.
    ///
    ///   GameObject > Vehicle > Road System   creates a Road System (road + traffic + pickups + wrong-way tracker) in the scene,
    ///                                         filled with these. Tick "Two Way Road" on the RoadGenerator to use the two-way pieces.
    ///   Tools > Vehicle > Create Road Pieces recreates all the default prefabs.
    /// </summary>
    [InitializeOnLoad]
    public static class RoadPrefabBuilder
    {
        const string Folder = "Assets/VehicleSystem/Road";

        // one-way pieces
        public const string StraightPath = Folder + "/Road Straight.prefab";
        public const string CurveLeftPath = Folder + "/Road Curve Left.prefab";
        public const string CurveRightPath = Folder + "/Road Curve Right.prefab";
        // two-way pieces
        public const string TwoWayStraightPath = Folder + "/Road Two-Way Straight.prefab";
        public const string TwoWayCurveLeftPath = Folder + "/Road Two-Way Curve Left.prefab";
        public const string TwoWayCurveRightPath = Folder + "/Road Two-Way Curve Right.prefab";
        public const string TwoWayCrossoverPath = Folder + "/Road Two-Way Crossover.prefab";
        // traffic
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

        static bool Exists(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;

        /// <summary>Creates only the groups that are missing, so older projects get the new pieces without losing their edits.</summary>
        static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

            bool oneWay = Exists(StraightPath);
            bool traffic = Exists(CarPath) && AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Traffic Light.mat") != null;
            bool twoWay = Exists(TwoWayStraightPath);
            if (oneWay && traffic && twoWay) return;

            Prepare();
            if (!oneWay) CreateOneWay();
            if (!traffic) CreateTraffic();
            if (!twoWay) CreateTwoWay();
            Finish();
        }

        [MenuItem("Tools/Vehicle/Create Road Pieces")]
        static void CreateFromMenu()
        {
            if (Exists(StraightPath) &&
                !EditorUtility.DisplayDialog("Road Pieces",
                    "The default road pieces and traffic vehicles already exist. Recreate them?\nChanges you made to them will be lost.",
                    "Recreate", "Cancel"))
                return;

            Prepare();
            CreateOneWay();
            CreateTraffic();
            CreateTwoWay();
            Finish();
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
            Undo.AddComponent<WrongWayTracker>(go);

            FillDefaults(generator);

            if (go.scene.IsValid()) EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
        }

        /// <summary>Assigns the default road pieces, traffic vehicles and pickups to the Road System on this object.</summary>
        public static void FillDefaults(RoadGenerator generator)
        {
            // add what an older Road System is missing
            var go = generator.gameObject;
            var traffic = go.GetComponent<TrafficManager>();
            if (traffic == null) traffic = Undo.AddComponent<TrafficManager>(go);
            var pickups = go.GetComponent<PickupPlacer>();
            if (pickups == null) pickups = Undo.AddComponent<PickupPlacer>(go);
            var tracker = go.GetComponent<WrongWayTracker>();
            if (tracker == null) tracker = Undo.AddComponent<WrongWayTracker>(go);

            Undo.RecordObject(generator, "Fill Road Defaults");
            generator.chunkPrefabs = Compact(Load<RoadChunk>(StraightPath), Load<RoadChunk>(CurveLeftPath), Load<RoadChunk>(CurveRightPath));
            generator.twoWayChunkPrefabs = Compact(Load<RoadChunk>(TwoWayStraightPath), Load<RoadChunk>(TwoWayCurveLeftPath),
                                                   Load<RoadChunk>(TwoWayCurveRightPath), Load<RoadChunk>(TwoWayCrossoverPath));
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

            if (tracker != null)
            {
                Undo.RecordObject(tracker, "Fill Road Defaults");
                tracker.road = generator;
                EditorUtility.SetDirty(tracker);
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

        static Material asphalt, curb, marking, glass, headlight, marker;

        static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder("Assets/VehicleSystem")) AssetDatabase.CreateFolder("Assets", "VehicleSystem");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/VehicleSystem", "Road");

            asphalt = MakeMaterial("Road Asphalt", new Color(0.22f, 0.22f, 0.24f));
            curb = MakeMaterial("Road Curb", new Color(0.62f, 0.6f, 0.56f));
            marking = MakeMaterial("Road Marking", new Color(0.92f, 0.92f, 0.85f));
            glass = MakeMaterial("Traffic Glass", new Color(0.12f, 0.16f, 0.2f));
            headlight = MakeMaterial("Traffic Light", new Color(1f, 0.95f, 0.6f));
            marker = MakeMaterial("Road Crossover Marker", new Color(1f, 0.72f, 0.1f));
        }

        static void Finish()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Road: default road pieces and traffic vehicles are in " + Folder + ". Replace them with your own art any time.");
        }

        // a piece is 4 segments of 15 m; a curve turns 7.5 degrees per segment (30 in total)
        static void CreateOneWay()
        {
            MakeChunk("Road Straight", StraightPath, 0f, 3f, 0, 3, 0, 0f, false);
            MakeChunk("Road Curve Left", CurveLeftPath, -7.5f, 1f, 2, 3, 0, 0f, false);
            MakeChunk("Road Curve Right", CurveRightPath, 7.5f, 1f, 2, 3, 0, 0f, false);
        }

        // two lanes each way with a median (2.4 m); the crossover has an opening in it
        static void CreateTwoWay()
        {
            MakeChunk("Road Two-Way Straight", TwoWayStraightPath, 0f, 3f, 0, 2, 2, 2.4f, false);
            MakeChunk("Road Two-Way Curve Left", TwoWayCurveLeftPath, -7.5f, 1f, 2, 2, 2, 2.4f, false);
            MakeChunk("Road Two-Way Curve Right", TwoWayCurveRightPath, 7.5f, 1f, 2, 2, 2, 2.4f, false);
            MakeChunk("Road Two-Way Crossover", TwoWayCrossoverPath, 0f, 1f, 0, 2, 2, 2.4f, true);
        }

        static void CreateTraffic()
        {
            MakeTraffic("Traffic Car", CarPath, new Vector3(1.8f, 1.5f, 4.2f), 1100f, MakeMaterial("Traffic Blue", new Color(0.2f, 0.4f, 0.85f)), 0, -1, 5f);
            MakeTraffic("Traffic CNG", CngPath, new Vector3(1.4f, 1.7f, 2.6f), 600f, MakeMaterial("Traffic Green", new Color(0.2f, 0.7f, 0.35f)), 0, -1, 3f);
            MakeTraffic("Traffic Bus", BusPath, new Vector3(2.4f, 3f, 11f), 7000f, MakeMaterial("Traffic Red", new Color(0.85f, 0.2f, 0.2f)), 0, 0, 1f);
            MakeTraffic("Traffic Truck", TruckPath, new Vector3(2.4f, 2.9f, 7f), 5000f, MakeMaterial("Traffic Orange", new Color(0.95f, 0.55f, 0.15f)), 0, 1, 1f);
        }

        static void MakeChunk(string name, string path, float turnPerSegment, float weight, int firstIndex,
                              int forwardLanes, int oncomingLanes, float medianWidth, bool crossover)
        {
            const int segments = 4;
            const float segmentLength = 15f;
            const float laneWidth = 3.2f;

            float forwardWidth = forwardLanes * laneWidth;
            float oncomingWidth = oncomingLanes * laneWidth;
            float median = oncomingLanes > 0 ? medianWidth : 0f;
            float total = forwardWidth + median + oncomingWidth;
            float left = -total * 0.5f;

            var root = new GameObject(name);
            var chunk = root.AddComponent<RoadChunk>();
            chunk.laneCount = forwardLanes;
            chunk.laneWidth = laneWidth;
            chunk.oncomingLaneCount = oncomingLanes;
            chunk.medianWidth = medianWidth;
            chunk.isCrossover = crossover;
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
                     new Vector3(total, 0.2f, pieceLength), asphalt, true);
                Prim(PrimitiveType.Cube, "Curb Left " + i, root.transform, middle - right * (total * 0.5f + 0.25f) + Vector3.up * 0.15f, rotation,
                     new Vector3(0.5f, 0.5f, pieceLength), curb, true);
                Prim(PrimitiveType.Cube, "Curb Right " + i, root.transform, middle + right * (total * 0.5f + 0.25f) + Vector3.up * 0.15f, rotation,
                     new Vector3(0.5f, 0.5f, pieceLength), curb, true);

                // lane lines of each road
                for (int d = 1; d < forwardLanes; d++)
                    Prim(PrimitiveType.Cube, "Lane Mark", root.transform, middle + right * (left + d * laneWidth) + Vector3.up * 0.011f, rotation,
                         new Vector3(0.12f, 0.02f, 6f), marking, false);
                for (int d = 1; d < oncomingLanes; d++)
                    Prim(PrimitiveType.Cube, "Lane Mark", root.transform, middle + right * (left + forwardWidth + median + d * laneWidth) + Vector3.up * 0.011f, rotation,
                         new Vector3(0.12f, 0.02f, 6f), marking, false);

                // the median between the two roads; a crossover piece has an opening in the middle two segments,
                // marked by tall yellow posts so players can see where to cross
                bool opening = crossover && (i == 1 || i == 2);
                if (crossover && i == 1)
                    Prim(PrimitiveType.Cube, "Crossover Post A", root.transform, position + right * (left + forwardWidth + median * 0.5f) + Vector3.up * 1.25f, rotation,
                         new Vector3(0.5f, 2.5f, 0.5f), marker, false);
                if (crossover && i == 2)
                    Prim(PrimitiveType.Cube, "Crossover Post B", root.transform, position + forward * segmentLength + right * (left + forwardWidth + median * 0.5f) + Vector3.up * 1.25f, rotation,
                         new Vector3(0.5f, 2.5f, 0.5f), marker, false);
                if (oncomingLanes > 0 && !opening)
                    Prim(PrimitiveType.Cube, "Median " + i, root.transform, middle + right * (left + forwardWidth + median * 0.5f) + Vector3.up * 0.15f, rotation,
                         new Vector3(median, 0.5f, pieceLength), curb, true);

                position += forward * segmentLength;
                rotation *= Quaternion.Euler(0f, turnPerSegment * 0.5f, 0f);
                points.Add(MakePoint(root.transform, "P" + (i + 1), position));
            }

            chunk.centerline = points.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        static void MakeTraffic(string name, string path, Vector3 size, float mass, Material body, int minLane, int maxLane, float weight)
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

            // two headlights at the front (+Z): oncoming traffic is easy to see coming
            for (int side = -1; side <= 1; side += 2)
                Prim(PrimitiveType.Cube, side < 0 ? "Headlight Left" : "Headlight Right", root.transform,
                     new Vector3(side * size.x * 0.3f, size.y * 0.3f, size.z * 0.5f), Quaternion.identity,
                     new Vector3(size.x * 0.2f, size.y * 0.14f, 0.08f), headlight, false);

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

            bool noPieces = generator.chunkPrefabs == null || generator.chunkPrefabs.Length == 0;
            bool noTwoWay = generator.twoWayRoad && (generator.twoWayChunkPrefabs == null || generator.twoWayChunkPrefabs.Length == 0);

            if (noPieces || noTwoWay)
            {
                EditorGUILayout.HelpBox("Road pieces are missing. Fill in the default pieces, traffic and pickups, or assign your own prefabs.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Same seed = same road on every device. Offline levels use a fixed seed; a room uses the host's seed. " +
                    "Tick 'Two Way Road' for oncoming lanes, crossovers and wrong-way scoring in this scene. " +
                    "TrafficManager, PickupPlacer and WrongWayTracker next to this component hold their own settings.",
                    MessageType.None);
            }

            if (GUILayout.Button("Fill With Default Pieces, Traffic And Pickups"))
                RoadPrefabBuilder.FillDefaults(generator);
        }
    }

    [CustomEditor(typeof(WrongWayTracker))]
    public class WrongWayTrackerEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (Application.isPlaying)
                EditorGUILayout.HelpBox(((WrongWayTracker)target).DebugStatus(), MessageType.Info);
            else
                EditorGUILayout.HelpBox("Press Play to see here whether the road, the two-way pieces and the followed vehicle are all found.", MessageType.None);
        }
    }

    /// <summary>Tools > Vehicle > Generate Wrong Way Banner UI (or the button on the Wrong Way Banner component).</summary>
    public static class WrongWayBannerBuilder
    {
        [MenuItem("Tools/Vehicle/Generate Wrong Way Banner UI")]
        static void CreateFromToolsMenu() => Build(null);

        [MenuItem("GameObject/Vehicle/Wrong Way Banner UI", false, 32)]
        static void CreateFromHierarchyMenu() => Build(null);

        public static void Build(WrongWayBanner target)
        {
            var banner = target != null ? target : Object.FindFirstObjectByType<WrongWayBanner>();

            if (banner != null && banner.uiRoot != null)
            {
                if (banner.uiRoot == banner.gameObject)
                {
                    EditorUtility.DisplayDialog("Wrong Way Banner",
                        "Ui Root points at the Wrong Way Banner object itself. Clear that field or point it at a child object, then try again.", "OK");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Wrong Way Banner",
                        "A banner UI already exists. Rebuild it from scratch?\nAny changes you made to it will be lost.", "Rebuild", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(banner.uiRoot);
            }

            if (banner == null)
            {
                var go = new GameObject("Wrong Way Banner");
                Undo.RegisterCreatedObjectUndo(go, "Create Wrong Way Banner");
                banner = Undo.AddComponent<WrongWayBanner>(go);
            }

            Undo.RecordObject(banner, "Generate Wrong Way Banner UI");
            banner.BuildUI();
            Undo.RegisterCreatedObjectUndo(banner.uiRoot, "Create Wrong Way Banner UI");

            EditorUtility.SetDirty(banner);
            if (banner.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(banner.gameObject.scene);

            Selection.activeGameObject = banner.uiRoot;
            EditorGUIUtility.PingObject(banner.uiRoot);
        }
    }

    [CustomEditor(typeof(WrongWayBanner))]
    public class WrongWayBannerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var banner = (WrongWayBanner)target;
            EditorGUILayout.Space();

            if (banner.uiRoot == null)
            {
                EditorGUILayout.HelpBox("No UI assigned. A default banner is built at runtime. Generate it now to edit it in the Hierarchy / Canvas.", MessageType.Info);
                if (GUILayout.Button("Generate Editable UI", GUILayout.Height(28))) WrongWayBannerBuilder.Build(banner);
            }
            else
            {
                if (GUILayout.Button("Select UI Root")) { Selection.activeGameObject = banner.uiRoot; EditorGUIUtility.PingObject(banner.uiRoot); }
                if (GUILayout.Button("Rebuild Default UI (replaces your edits)")) WrongWayBannerBuilder.Build(banner);
            }
        }
    }
}
#endif

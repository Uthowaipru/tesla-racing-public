using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// A tiny photo studio for the vehicle selector: a turntable, a camera and a light, placed far away from
    /// everything else. The camera renders into a RenderTexture that the menu shows on a RawImage.
    ///
    /// Show(definition) puts a copy of that vehicle on the turntable. If the definition has no Preview Prefab, the
    /// normal vehicle prefab is copied and its scripts, colliders and rigidbody are removed from the COPY, so it
    /// is just a model that spins (the prefab itself is never touched, and nothing drives off or falls over).
    ///
    /// UI source (same idea as the other UI scripts):
    ///   - Tools > Vehicle > Generate Vehicle Select UI (or the button on VehicleSelectMenu) creates this stage as
    ///     real objects in the Hierarchy: "Turntable", "Preview Camera", "Stage Light". Move or tune them freely.
    ///   - If they are missing, they are created at runtime.
    ///
    /// Optional: create a Layer named "VehiclePreview". The stage then only renders and lights objects on that layer,
    /// so it can never pick up anything else in your menu scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehiclePreviewStage : MonoBehaviour
    {
        [Header("Stage objects (filled in by the editor tool)")]
        public Transform turntable;
        public Camera previewCamera;
        public Light stageLight;

        [Header("Placement")]
        [Tooltip("Where the stage sits in the world. Far from the rest of the scene, so the main camera never sees it.")]
        public Vector3 stageWorldPosition = new Vector3(0f, -1000f, 0f);
        [Tooltip("Optional layer name. If the layer exists, the stage camera and light only use it.")]
        public string previewLayerName = "VehiclePreview";

        [Header("Spin")]
        [Tooltip("Degrees per second. 0 = no automatic spin.")]
        public float rotateSpeed = 25f;
        [Tooltip("Degrees of turn per pixel of drag.")]
        public float dragSensitivity = 0.4f;
        [Tooltip("Seconds after letting go before the automatic spin starts again.")]
        public float resumeDelay = 1.5f;

        [Header("Camera")]
        public Color backgroundColor = new Color(0.13f, 0.14f, 0.19f, 1f);
        public float fieldOfView = 30f;
        [Tooltip("How far above the vehicle the camera looks down from (degrees).")]
        [Range(-10f, 60f)] public float cameraElevation = 14f;
        [Tooltip("ON: the camera backs away until any vehicle fits. OFF: uses Fixed Distance.")]
        public bool autoFrame = true;
        [Tooltip("Lower = the vehicle fills more of the picture.")]
        [Range(0.5f, 1.5f)] public float framePadding = 0.85f;
        public float fixedDistance = 8f;

        [Header("Picture")]
        [Tooltip("Size of the RenderTexture. Use the same shape as the picture in the menu (default 4:3).")]
        public Vector2Int textureSize = new Vector2Int(1024, 768);

        RenderTexture rt;
        GameObject modelRoot;
        Vector3 focus;
        float radius = 2.5f;
        bool ready, dragging;
        float resumeAt;
        int layer = -1;

        /// <summary>The picture the camera renders. Show it on a RawImage.</summary>
        public RenderTexture Texture
        {
            get { EnsureReady(); return rt; }
        }

        // ------------------------------------------------------------------ stage objects

        /// <summary>
        /// Creates the turntable, camera and light that are still missing, under this object.
        /// Used at runtime when nothing is assigned, and by the editor tool.
        /// </summary>
        public void BuildStage()
        {
            transform.position = stageWorldPosition;

            if (turntable == null)
            {
                var go = new GameObject("Turntable");
                go.transform.SetParent(transform, false);
                turntable = go.transform;
            }

            if (previewCamera == null)
            {
                var go = new GameObject("Preview Camera");
                go.transform.SetParent(transform, false);
                previewCamera = go.AddComponent<Camera>();
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = backgroundColor;
                previewCamera.fieldOfView = fieldOfView;
                previewCamera.nearClipPlane = 0.1f;
                previewCamera.farClipPlane = 200f;
                previewCamera.depth = -50f;
                previewCamera.enabled = false; // switched on at runtime, once it has a RenderTexture to draw into
                go.transform.position = transform.position + new Vector3(0f, 2f, -8f);
                go.transform.LookAt(transform.position + Vector3.up * 0.8f);
            }

            if (stageLight == null)
            {
                var go = new GameObject("Stage Light");
                go.transform.SetParent(transform, false);
                go.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
                stageLight = go.AddComponent<Light>();
                stageLight.type = LightType.Directional;
                stageLight.intensity = 1.2f;
                stageLight.shadows = LightShadows.None;
            }
        }

        void EnsureReady()
        {
            if (ready) return;

            if (turntable == null || previewCamera == null || stageLight == null) BuildStage();

            if (rt == null)
            {
                rt = new RenderTexture(Mathf.Max(64, textureSize.x), Mathf.Max(64, textureSize.y), 24, RenderTextureFormat.ARGB32)
                {
                    name = "Vehicle Preview",
                    antiAliasing = 4
                };
            }

            layer = string.IsNullOrEmpty(previewLayerName) ? -1 : LayerMask.NameToLayer(previewLayerName);

            previewCamera.targetTexture = rt;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = backgroundColor;
            previewCamera.fieldOfView = fieldOfView;
            if (layer >= 0)
            {
                previewCamera.cullingMask = 1 << layer;
                stageLight.cullingMask = 1 << layer;
            }
            previewCamera.enabled = true;

            ready = true;
        }

        void OnDestroy()
        {
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }
        }

        // ------------------------------------------------------------------ showing a vehicle

        /// <summary>Puts a spinning copy of this vehicle on the turntable (null = empty stage).</summary>
        public void Show(VehicleDefinition def)
        {
            EnsureReady();
            ClearModel();

            turntable.localRotation = Quaternion.identity;
            dragging = false;
            resumeAt = 0f;

            if (def == null) return;

            bool visualOnly = def.previewPrefab != null;
            GameObject source = visualOnly ? def.previewPrefab : def.prefab;
            if (source == null) return;

            // Build the copy under an INACTIVE holder: its scripts never run (no Awake, no physics) before we strip them.
            var holder = new GameObject("Holder");
            holder.SetActive(false);
            holder.transform.SetParent(turntable, false);

            var model = Instantiate(source, holder.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // A hand-made Preview Prefab keeps its own (harmless) scripts, but physics is always removed:
            // a WheelCollider without a Rigidbody only produces warnings.
            StripToVisuals(model, removeAllScripts: !visualOnly);

            model.transform.SetParent(turntable, false); // now it becomes active
            Destroy(holder);

            if (layer >= 0) SetLayer(model, layer);

            // stand it on the turntable, centred on its pivot
            Bounds b = CalculateBounds(model);
            Vector3 shift = new Vector3(turntable.position.x - b.center.x,
                                        turntable.position.y - b.min.y,
                                        turntable.position.z - b.center.z);
            model.transform.position += shift;
            b.center += shift;

            modelRoot = model;
            focus = b.center;
            radius = Mathf.Max(0.5f, b.extents.magnitude);
            FrameCamera();
        }

        void ClearModel()
        {
            if (modelRoot == null) return;
            modelRoot.SetActive(false);
            Destroy(modelRoot);
            modelRoot = null;
        }

        void FrameCamera()
        {
            float distance = fixedDistance;
            if (autoFrame)
                distance = Mathf.Max(1f, radius * framePadding / Mathf.Sin(fieldOfView * 0.5f * Mathf.Deg2Rad));

            Vector3 dir = transform.rotation * (Quaternion.Euler(cameraElevation, 0f, 0f) * Vector3.back);
            previewCamera.transform.position = focus + dir * distance;
            previewCamera.transform.LookAt(focus);
            previewCamera.fieldOfView = fieldOfView;
        }

        // ------------------------------------------------------------------ spin

        void Update()
        {
            if (turntable == null || modelRoot == null) return;
            if (dragging || Time.unscaledTime < resumeAt || rotateSpeed == 0f) return;
            turntable.Rotate(0f, rotateSpeed * Time.unscaledDeltaTime, 0f, Space.World);
        }

        public void BeginDrag() => dragging = true;

        public void AddDrag(float deltaPixelsX)
        {
            if (turntable != null) turntable.Rotate(0f, -deltaPixelsX * dragSensitivity, 0f, Space.World);
        }

        public void EndDrag()
        {
            dragging = false;
            resumeAt = Time.unscaledTime + resumeDelay;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Removes everything that is not a picture from a copy of a gameplay prefab.</summary>
        static void StripToVisuals(GameObject root, bool removeAllScripts)
        {
            // gameplay scripts first, in the order they depend on each other
            DestroyAll<VehicleDriver>(root);
            DestroyAll<VehicleRecovery>(root);
            DestroyAll<VehicleController>(root);
            DestroyAll<VehicleStats>(root);
            if (removeAllScripts) DestroyAll<MonoBehaviour>(root);

            // then sound, physics (WheelCollider is a Collider too) and the rigidbody
            DestroyAll<AudioSource>(root);
            DestroyAll<AudioListener>(root);
            DestroyAll<Collider>(root);
            DestroyAll<Rigidbody>(root);
        }

        static void DestroyAll<T>(GameObject root) where T : Component
        {
            foreach (var c in root.GetComponentsInChildren<T>(true))
                if (c != null) DestroyImmediate(c);
        }

        static Bounds CalculateBounds(GameObject root)
        {
            bool has = false;
            Bounds b = new Bounds(root.transform.position, Vector3.zero);

            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }

            if (!has) b = new Bounds(root.transform.position + Vector3.up * 0.8f, new Vector3(2f, 1.6f, 4f));
            return b;
        }

        static void SetLayer(GameObject go, int newLayer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = newLayer;
        }
    }
}

using System;
using UnityEngine;

namespace AnimeToon
{
    /// <summary>
    /// Light shafts ("god rays"): a few large soft additive beams around the camera that follow the main light.
    /// Cheap: one mesh, one draw call, no post-processing. Put it on an empty object (Tools > Anime Toon > Add Light Shafts
    /// To Scene does everything) and give it a material that uses the Anime/LightShaft shader.
    ///
    /// The beams are strongest when the sun is low (use the style controller's Dusk), gentle at noon, and invisible when the
    /// sun is below the horizon. They are turned off by the Low quality tier ("Light Shafts" in the tier).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class AnimeLightShafts : MonoBehaviour
    {
        [Header("Material")]
        [Tooltip("A material using the Anime/LightShaft shader.")]
        public Material material;

        [Header("Beams")]
        [Range(1, 60)] public int beamCount = 18;
        [Tooltip("How far from the middle (the camera) the beams stand, min and max, in meters.")]
        public Vector2 ringRadius = new Vector2(12f, 70f);
        public Vector2 beamWidth = new Vector2(4f, 10f);
        public Vector2 beamLength = new Vector2(35f, 80f);
        [Tooltip("Height of the beams' feet. Put it at the ground level of your scene.")]
        public float anchorHeight = 0f;
        public int seed = 7;

        [Header("Behaviour")]
        [Tooltip("Keep the beams around the camera (horizontally) while the game runs.")]
        public bool followCamera = true;
        [Tooltip("Obey the quality tier: off on tiers that have Light Shafts unticked.")]
        public bool respectQuality = true;

        Mesh mesh;
        bool dirty = true;
        float checkTimer;
        MeshRenderer meshRenderer;

        void OnEnable() { dirty = true; }
        void OnValidate() { dirty = true; }

        void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }

        void Update()
        {
            if (dirty || mesh == null) Rebuild();

            if (followCamera && Application.isPlaying)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 p = cam.transform.position;
                    transform.position = new Vector3(p.x, transform.position.y, p.z);
                }
            }

            checkTimer -= Time.unscaledDeltaTime;
            if (checkTimer <= 0f)
            {
                checkTimer = 0.5f;
                bool on = material != null && (!respectQuality || AnimeQuality.Tier.lightShafts);
                if (meshRenderer != null) meshRenderer.enabled = on;
            }
        }

        [ContextMenu("Rebuild Beams")]
        public void Rebuild()
        {
            dirty = false;
            meshRenderer = GetComponent<MeshRenderer>();

            if (mesh == null)
            {
                mesh = new Mesh { name = "Light Shafts", hideFlags = HideFlags.HideAndDontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            mesh.Clear();

            int count = Mathf.Max(1, beamCount);
            var vertices = new Vector3[count * 4];
            var uvs = new Vector2[count * 4];
            var data = new System.Collections.Generic.List<Vector4>(count * 4);
            var triangles = new int[count * 6];
            var random = new System.Random(seed);

            for (int i = 0; i < count; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                float radius = Mathf.Lerp(ringRadius.x, ringRadius.y, (float)random.NextDouble());
                float width = Mathf.Lerp(beamWidth.x, beamWidth.y, (float)random.NextDouble());
                float length = Mathf.Lerp(beamLength.x, beamLength.y, (float)random.NextDouble());
                float beamSeed = (float)random.NextDouble();

                // all four corners share the anchor; the shader spreads them out
                var anchor = new Vector3(Mathf.Cos(angle) * radius, anchorHeight, Mathf.Sin(angle) * radius);
                int v = i * 4;
                for (int k = 0; k < 4; k++)
                {
                    vertices[v + k] = anchor;
                    data.Add(new Vector4(width, length, beamSeed, 0f));
                }
                uvs[v + 0] = new Vector2(-1f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(-1f, 1f);
                uvs[v + 3] = new Vector2(1f, 1f);

                int t = i * 6;
                triangles[t + 0] = v + 0; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, data);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2000f, 2000f, 2000f)); // never culled: the shader moves the corners

            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
    }
}

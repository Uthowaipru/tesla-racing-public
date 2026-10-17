using System.Collections;
using UnityEngine;

namespace AnimeToon
{
    /// <summary>
    /// Anime action effects for a camera: speed lines, a field-of-view kick, speed streaks flying past,
    /// an impact flash, camera shake and an optional hit-stop. Contains no game code: feed it from anywhere
    /// with SetSpeed01 / SetBoosting / Impact (a bridge script does this for a vehicle game).
    ///
    /// Everything costs nothing while idle (the screen quad is disabled when no effect is showing).
    /// If your own camera script also sets the field of view every frame, untick Control Fov.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class AnimeSpeedFx : MonoBehaviour
    {
        [Header("Input (set from code)")]
        [Range(0f, 1f)] public float speed01;
        public bool boosting;

        [Header("Field of view")]
        public bool controlFov = true;
        [Tooltip("0 = use the camera's FOV when the game starts.")]
        public float baseFov = 0f;
        public float fovSpeedAdd = 8f;
        public float fovBoostAdd = 10f;
        public float fovSmoothing = 6f;

        [Header("Screen speed lines")]
        public bool useSpeedLines = true;
        [Range(0f, 1f)] public float linesStartSpeed01 = 0.6f;
        [Range(0f, 1f)] public float maxLineIntensity = 0.5f;
        [Range(0f, 1f)] public float boostLineIntensity = 0.85f;
        public Color lineColor = Color.white;
        public float linesSmoothing = 5f;

        [Header("Speed streaks (particles flying past the camera)")]
        [Tooltip("A prefab or scene ParticleSystem, e.g. AnimeSpeedStreaks from the VFX pack. Optional.")]
        public ParticleSystem speedStreaks;
        [Range(0f, 1f)] public float streaksStartSpeed01 = 0.45f;
        public float maxStreakRate = 45f;
        public float boostStreakMultiplier = 1.6f;

        [Header("Impacts")]
        public Color flashColor = Color.white;
        [Range(0f, 1f)] public float flashMax = 0.7f;
        public float flashDecay = 7f;
        public float shakeMaxDegrees = 2.2f;
        public float shakeDecay = 6f;
        [Tooltip("Freeze the game for a moment on big hits (0 = off).")]
        public float hitStopSeconds = 0f;
        [Range(0f, 1f)] public float hitStopMinStrength = 0.7f;

        [Header("Setup")]
        [Tooltip("Anime/ScreenFX. Filled automatically in the editor; keeping the reference stops it being stripped from builds.")]
        public Shader screenFxShader;

        static readonly int SpeedLinesId = Shader.PropertyToID("_SpeedLines");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int LineColorId = Shader.PropertyToID("_LineColor");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        Camera cam;
        MeshRenderer quad;
        Material quadMaterial;
        GameObject quadObject;

        float lines, flash, shake, fovOffset;
        Quaternion lastShake = Quaternion.identity;
        bool hitStopping;
        bool fovCaptured;

        void Reset()
        {
            screenFxShader = Shader.Find("Anime/ScreenFX");
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            CreateQuad();

            if (speedStreaks != null && !speedStreaks.gameObject.scene.IsValid())
            {
                speedStreaks = Instantiate(speedStreaks, transform);
                speedStreaks.transform.localPosition = Vector3.zero;
                speedStreaks.transform.localRotation = Quaternion.identity;
            }
        }

        void OnEnable()
        {
            if (speedStreaks != null) speedStreaks.Play();
        }

        void OnDisable()
        {
            if (quad != null) quad.enabled = false;
            transform.localRotation = transform.localRotation * Quaternion.Inverse(lastShake);
            lastShake = Quaternion.identity;
            if (controlFov && fovCaptured && cam != null) cam.fieldOfView = baseFov;
        }

        void OnDestroy()
        {
            if (quadMaterial != null) Destroy(quadMaterial);
            if (quadObject != null) Destroy(quadObject);
        }

        void CreateQuad()
        {
            Shader shader = screenFxShader != null ? screenFxShader : Shader.Find("Anime/ScreenFX");
            if (shader == null)
            {
                Debug.LogWarning("AnimeSpeedFx: shader 'Anime/ScreenFX' not found, screen effects are off.", this);
                return;
            }

            quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadObject.name = "AnimeScreenFX";
            var collider = quadObject.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            quadObject.transform.SetParent(transform, false);
            quadObject.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(1f, cam.nearClipPlane + 0.5f));
            quadObject.transform.localRotation = Quaternion.identity;
            quadObject.transform.localScale = Vector3.one;
            quadObject.layer = gameObject.layer;

            quadMaterial = new Material(shader) { name = "AnimeScreenFX (instance)" };
            quad = quadObject.GetComponent<MeshRenderer>();
            quad.sharedMaterial = quadMaterial;
            quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.receiveShadows = false;
            quad.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            quad.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            quad.enabled = false;
        }

        // ------------------------------------------------------------------ public API

        public void SetSpeed01(float value) { speed01 = Mathf.Clamp01(value); }
        public void SetBoosting(bool value) { boosting = value; }

        /// <summary>A hit. strength01 is 0..1 (for example speed at impact / top speed).</summary>
        public void Impact(float strength01)
        {
            float s = Mathf.Clamp01(strength01);
            flash = Mathf.Max(flash, s * flashMax);
            shake = Mathf.Max(shake, s);

            if (hitStopSeconds > 0f && s >= hitStopMinStrength && !hitStopping)
                StartCoroutine(HitStop());
        }

        IEnumerator HitStop()
        {
            float previous = Time.timeScale;
            if (previous <= 0.01f) yield break;

            hitStopping = true;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(hitStopSeconds);
            if (Mathf.Approximately(Time.timeScale, 0.05f)) Time.timeScale = previous;
            hitStopping = false;
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            float dt = Time.deltaTime;
            float sp = Mathf.Clamp01(speed01);

            // speed lines
            float linesTarget = 0f;
            if (useSpeedLines)
            {
                linesTarget = Mathf.InverseLerp(linesStartSpeed01, 1f, sp) * maxLineIntensity;
                if (boosting) linesTarget = Mathf.Max(linesTarget, boostLineIntensity);
            }
            lines = Mathf.Lerp(lines, linesTarget, 1f - Mathf.Exp(-linesSmoothing * dt));
            flash = Mathf.MoveTowards(flash, 0f, flashDecay * dt);
            shake = Mathf.MoveTowards(shake, 0f, shakeDecay * dt);

            if (quad != null)
            {
                bool visible = lines > 0.01f || flash > 0.01f;
                quad.enabled = visible;
                if (visible)
                {
                    quadMaterial.SetFloat(SpeedLinesId, lines);
                    quadMaterial.SetFloat(FlashId, flash);
                    quadMaterial.SetColor(LineColorId, lineColor);
                    quadMaterial.SetColor(FlashColorId, flashColor);
                }
            }

            // streaks
            if (speedStreaks != null)
            {
                var emission = speedStreaks.emission;
                float k = Mathf.InverseLerp(streaksStartSpeed01, 1f, sp);
                emission.rateOverTime = k * maxStreakRate * (boosting ? boostStreakMultiplier : 1f);
            }

            // field of view offset (applied in LateUpdate)
            float fovTarget = fovSpeedAdd * sp * sp + (boosting ? fovBoostAdd : 0f);
            fovOffset = Mathf.Lerp(fovOffset, fovTarget, 1f - Mathf.Exp(-fovSmoothing * dt));
        }

        void LateUpdate()
        {
            if (controlFov && cam != null)
            {
                if (!fovCaptured)
                {
                    if (baseFov <= 0f) baseFov = cam.fieldOfView;
                    fovCaptured = true;
                }
                cam.fieldOfView = baseFov + fovOffset;
            }

            // shake as a small rotation jitter; removes last frame's jitter first so it never accumulates
            Quaternion next = Quaternion.identity;
            if (shake > 0.001f && shakeMaxDegrees > 0f)
            {
                float t = Time.unscaledTime * 35f;
                float amount = shake * shake * shakeMaxDegrees;
                next = Quaternion.Euler(
                    (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * amount,
                    (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 2f * amount,
                    (Mathf.PerlinNoise(t, 7.7f) - 0.5f) * 2f * amount * 0.5f);
            }

            if (next != Quaternion.identity || lastShake != Quaternion.identity)
            {
                transform.localRotation = transform.localRotation * Quaternion.Inverse(lastShake) * next;
                lastShake = next;
            }
        }
    }
}

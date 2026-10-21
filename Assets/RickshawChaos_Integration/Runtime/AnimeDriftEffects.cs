using System.Collections.Generic;
using UnityEngine;
using VehicleSystem;

namespace AnimeToon.Integration
{
    /// <summary>
    /// GAME SPECIFIC (Rickshaw Chaos / VehicleSystem). Anime drift effects for one vehicle:
    ///
    ///   Tire smoke   toon puffs at the wheels that are sliding
    ///   Sparks       a few stretched sparks at the hardest slides (optional)
    ///   Skid marks   dark ribbons on the road behind the sliding wheels, fading after a few seconds
    ///
    /// It reads each wheel's sideways slip from its WheelCollider and the handbrake from VehicleController, so it
    /// follows whatever the physics does. It is added to every vehicle automatically by AnimeDriftBootstrap and takes
    /// its effects from Resources/VehicleSystem/AnimeDriftEffectsDefaults (Tools > Vehicle > Install Anime Drift Effects).
    /// Nothing happens farther than "Effect Distance" from the camera, to save frames on phones.
    ///
    /// Tuning: tick "Log Slip" and drift once; the console shows the slip values so you can set Slip Start / Slip Full.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleController))]
    public class AnimeDriftEffects : MonoBehaviour
    {
        [Header("Effects (empty = project defaults)")]
        public ParticleSystem smoke;
        public ParticleSystem sparks;
        public Material skidMaterial;
        public bool useDefaultsForEmptySlots = true;

        [Header("Which wheels")]
        [Tooltip("Rear (handbrake) wheels only. If no wheel has Handbrake ticked, the drive wheels are used.")]
        public bool onlyHandbrakeWheels = true;

        [Header("When it counts as drifting")]
        [Tooltip("No effects below this speed (m/s).")]
        public float minSpeed = 3f;
        [Tooltip("Wheel sideways slip where the effects start.")]
        public float slipStart = 0.45f;
        [Tooltip("Wheel sideways slip where the effects are at full strength.")]
        public float slipFull = 1.0f;
        [Tooltip("Wheelspin / locked wheels also count by this share. 0 = sideways slip only.")]
        public float forwardSlipWeight = 0f;
        [Tooltip("Holding the handbrake while moving counts as drifting even before the slip builds up.")]
        public bool handbrakeCounts = true;

        [Header("Smoke")]
        public bool useSmoke = true;
        [Tooltip("Puffs per second per wheel at full drift.")]
        public float smokeRate = 30f;
        public Vector2 smokeSize = new Vector2(0.7f, 1.2f);
        public float smokeLift = 1.4f;

        [Header("Sparks")]
        public bool useSparks = true;
        [Range(0f, 1f)] public float sparksFrom = 0.85f;
        public float sparksRate = 12f;

        [Header("Skid marks")]
        public bool useSkidMarks = true;
        public float skidLifetime = 3.5f;
        public float skidWidth = 0.22f;
        [Tooltip("Lift above the road, so the mark does not flicker against it.")]
        public float skidHeight = 0.04f;
        public float skidMinVertexDistance = 0.3f;
        [Tooltip("How many separate marks each wheel can have on the road at once.")]
        [Range(1, 10)] public int maxMarksPerWheel = 4;

        [Header("Performance / debug")]
        [Tooltip("Farther than this from the camera nothing is drawn.")]
        public float effectDistance = 45f;
        public bool logSlip = false;

        /// <summary>0..1, the strongest drift among the wheels right now. Other scripts can read it.</summary>
        public float Drift01 { get; private set; }

        class WheelFx
        {
            public WheelSetup wheel;
            public float smokeAcc;
            public float sparkAcc;
            public readonly List<TrailRenderer> trails = new List<TrailRenderer>();
            public readonly List<float> freeAt = new List<float>();
            public int active = -1;
        }

        static bool warned;

        readonly List<WheelFx> wheelFx = new List<WheelFx>();
        VehicleController vehicle;
        VehicleHealth health;
        Rigidbody body;
        Transform effectsRoot;
        Transform marksRoot;
        Camera cam;
        bool ready;
        float logTimer;
        float maxSlipSeen;

        void Awake()
        {
            vehicle = GetComponent<VehicleController>();
            health = GetComponent<VehicleHealth>();
            body = GetComponent<Rigidbody>();
        }

        void OnDisable()
        {
            for (int i = 0; i < wheelFx.Count; i++) EndSkid(wheelFx[i]);
            Drift01 = 0f;
        }

        void OnDestroy()
        {
            // let the marks fade out on their own instead of vanishing with the vehicle
            if (marksRoot != null) Destroy(marksRoot.gameObject, skidLifetime + 0.5f);
        }

        // ------------------------------------------------------------------ setup

        void Setup()
        {
            ready = true;

            var defaults = useDefaultsForEmptySlots ? AnimeDriftEffectsDefaults.Load() : null;

            smoke = Resolve(smoke, defaults != null ? defaults.smoke : null);
            sparks = Resolve(sparks, defaults != null ? defaults.sparks : null);
            if (skidMaterial == null && defaults != null) skidMaterial = defaults.skid;

            if (smoke == null && sparks == null && skidMaterial == null)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("AnimeDriftEffects: no effects found. In the Unity editor run Tools > Vehicle > Install Anime Drift Effects.", this);
                }
                enabled = false;
                return;
            }

            // choose the wheels
            var chosen = new List<WheelSetup>();
            if (vehicle.wheels != null)
            {
                if (onlyHandbrakeWheels)
                {
                    foreach (var w in vehicle.wheels) if (w != null && w.collider != null && w.handbrake) chosen.Add(w);
                    if (chosen.Count == 0)
                        foreach (var w in vehicle.wheels) if (w != null && w.collider != null && w.drive) chosen.Add(w);
                }
                if (chosen.Count == 0)
                    foreach (var w in vehicle.wheels) if (w != null && w.collider != null) chosen.Add(w);
            }

            foreach (var w in chosen) wheelFx.Add(new WheelFx { wheel = w });
        }

        ParticleSystem Resolve(ParticleSystem slot, ParticleSystem fallback)
        {
            ParticleSystem source = slot != null ? slot : fallback;
            if (source == null) return null;

            // a particle system that is already part of this vehicle: use it as it is
            if (slot != null && slot.gameObject.scene.IsValid()) return slot;

            if (effectsRoot == null)
            {
                var go = new GameObject("Drift Effects");
                go.transform.SetParent(transform, false);
                effectsRoot = go.transform;
            }

            ParticleSystem instance = Instantiate(source, effectsRoot);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.Play();
            return instance;
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!ready)
            {
                Setup();
                if (!ready || !enabled) return;
            }
            if (wheelFx.Count == 0) return;

            float dt = Time.deltaTime;
            bool near = IsNearCamera();
            bool dead = health != null && health.IsDestroyed;
            float speed = Mathf.Abs(vehicle.ForwardSpeed);
            float speedFactor = Mathf.Clamp01((speed - minSpeed) / 3f);
            Vector3 velocity = BodyVelocity();
            float best = 0f;

            for (int i = 0; i < wheelFx.Count; i++)
            {
                WheelFx f = wheelFx[i];
                float amount = 0f;
                Vector3 point = Vector3.zero;
                Vector3 normal = Vector3.up;

                if (near && !dead && speedFactor > 0f && f.wheel.collider != null &&
                    f.wheel.collider.GetGroundHit(out WheelHit hit))
                {
                    float slip = Mathf.Max(Mathf.Abs(hit.sidewaysSlip), Mathf.Abs(hit.forwardSlip) * forwardSlipWeight);
                    if (slip > maxSlipSeen) maxSlipSeen = slip;

                    amount = Mathf.InverseLerp(slipStart, Mathf.Max(slipStart + 0.01f, slipFull), slip);
                    if (handbrakeCounts && f.wheel.handbrake) amount = Mathf.Max(amount, vehicle.HandbrakeAmount);
                    amount *= speedFactor;

                    point = hit.point;
                    normal = hit.normal;
                }

                if (amount > best) best = amount;

                // smoke
                if (useSmoke && smoke != null && amount > 0.05f)
                {
                    f.smokeAcc += smokeRate * amount * dt * AnimeQuality.ParticleScale;
                    int n = Mathf.Min(Mathf.FloorToInt(f.smokeAcc), 3);
                    f.smokeAcc = Mathf.Min(f.smokeAcc - Mathf.Floor(f.smokeAcc), 1f);
                    for (int k = 0; k < n; k++) EmitSmoke(point, normal, amount, velocity);
                }
                else f.smokeAcc = 0f;

                // sparks, only at the hardest slides
                if (useSparks && sparks != null && AnimeQuality.DriftSparks && amount >= sparksFrom)
                {
                    f.sparkAcc += sparksRate * amount * dt * AnimeQuality.ParticleScale;
                    int n = Mathf.Min(Mathf.FloorToInt(f.sparkAcc), 3);
                    f.sparkAcc = Mathf.Min(f.sparkAcc - Mathf.Floor(f.sparkAcc), 1f);
                    for (int k = 0; k < n; k++) EmitSpark(point, normal, velocity);
                }
                else f.sparkAcc = 0f;

                // skid marks (a little hysteresis so a mark does not flicker on and off)
                if (useSkidMarks && skidMaterial != null && AnimeQuality.SkidMarks)
                {
                    bool sliding = f.active >= 0 ? amount > 0.1f : amount > 0.2f;
                    if (sliding)
                    {
                        if (f.active < 0) StartSkid(f, point, normal);
                        else MoveSkid(f.trails[f.active], point, normal);
                    }
                    else EndSkid(f);
                }
                else EndSkid(f);
            }

            Drift01 = best;

            if (logSlip)
            {
                logTimer += dt;
                if (logTimer >= 1f)
                {
                    logTimer = 0f;
                    if (maxSlipSeen > 0.05f)
                        Debug.Log("AnimeDriftEffects: strongest wheel slip in the last second " + maxSlipSeen.ToString("F2") +
                                  " (effects start at " + slipStart + ", full at " + slipFull + ")", this);
                    maxSlipSeen = 0f;
                }
            }
        }

        bool IsNearCamera()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return true;
            return (cam.transform.position - transform.position).sqrMagnitude <= effectDistance * effectDistance;
        }

        Vector3 BodyVelocity()
        {
            if (body == null) return Vector3.zero;
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }

        // ------------------------------------------------------------------ smoke and sparks

        void EmitSmoke(Vector3 point, Vector3 normal, float amount, Vector3 velocity)
        {
            Vector3 heading = velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, heading);

            var p = new ParticleSystem.EmitParams();
            p.position = point + normal * 0.1f + Random.insideUnitSphere * 0.08f;
            p.velocity = Vector3.up * (Random.Range(0.6f, 1.2f) * smokeLift)
                       + side * Random.Range(-0.8f, 0.8f)
                       + velocity * 0.12f;
            p.startSize = Random.Range(smokeSize.x, smokeSize.y) * Mathf.Lerp(0.8f, 1.3f, amount);
            smoke.Emit(p, 1);
        }

        void EmitSpark(Vector3 point, Vector3 normal, Vector3 velocity)
        {
            Vector3 heading = velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward;

            var p = new ParticleSystem.EmitParams();
            p.position = point + normal * 0.08f;
            p.velocity = -heading * Random.Range(1f, 3f)
                       + Vector3.up * Random.Range(1.5f, 3.5f)
                       + Random.insideUnitSphere * 1.2f;
            sparks.Emit(p, 1);
        }

        // ------------------------------------------------------------------ skid marks

        TrailRenderer MakeTrail()
        {
            if (marksRoot == null)
            {
                // not a child of the vehicle: the marks stay on the road while the vehicle drives away
                marksRoot = new GameObject("Skid Marks (" + name + ")").transform;
            }

            var go = new GameObject("Skid");
            go.transform.SetParent(marksRoot, false);

            var t = go.AddComponent<TrailRenderer>();
            t.time = skidLifetime;
            t.minVertexDistance = skidMinVertexDistance;
            t.widthMultiplier = skidWidth;
            t.alignment = LineAlignment.TransformZ;   // the ribbon lies flat when the transform's Z points along the road normal
            t.textureMode = LineTextureMode.Stretch;
            t.numCapVertices = 0;
            t.autodestruct = false;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
            t.sharedMaterial = skidMaterial;

            // solid for most of its life, then it erodes away (the toon shader turns this alpha into erosion)
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            t.colorGradient = gradient;

            t.emitting = false;
            return t;
        }

        void MoveSkid(TrailRenderer t, Vector3 point, Vector3 normal)
        {
            t.transform.SetPositionAndRotation(
                point + normal * skidHeight,
                Quaternion.LookRotation(normal, transform.forward));
        }

        void StartSkid(WheelFx f, Vector3 point, Vector3 normal)
        {
            int index = -1;

            // 1) a mark that has finished its life can be reused
            for (int i = 0; i < f.trails.Count; i++)
                if (i != f.active && Time.time >= f.freeAt[i]) { index = i; break; }

            // 2) otherwise make a new one, up to the limit
            if (index < 0 && f.trails.Count < maxMarksPerWheel)
            {
                f.trails.Add(MakeTrail());
                f.freeAt.Add(0f);
                index = f.trails.Count - 1;
            }

            // 3) otherwise take the oldest one
            if (index < 0)
            {
                float oldest = float.MaxValue;
                for (int i = 0; i < f.trails.Count; i++)
                    if (i != f.active && f.freeAt[i] < oldest) { oldest = f.freeAt[i]; index = i; }
            }

            if (index < 0) return;

            TrailRenderer t = f.trails[index];
            t.time = skidLifetime;
            MoveSkid(t, point, normal);
            t.Clear();
            t.emitting = true;
            f.active = index;
        }

        void EndSkid(WheelFx f)
        {
            if (f.active < 0) return;

            TrailRenderer t = f.trails[f.active];
            if (t != null) t.emitting = false;
            f.freeAt[f.active] = Time.time + skidLifetime;
            f.active = -1;
        }
    }
}

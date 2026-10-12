using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Particle effects for one vehicle.
    ///
    ///   Trail             dust / exhaust puffs behind the vehicle, stronger the faster it goes
    ///   Hit Sparks        burst at the contact point of an impact
    ///   Hit Dust          burst at the contact point of an impact
    ///   Low Health Smoke  appears below "Smoke Below Health"
    ///   Explosion         when the vehicle is destroyed
    ///
    /// CUSTOM PER VEHICLE: assign your own ParticleSystem to a slot. It can be an object inside the vehicle prefab (placed
    /// where you want it) or a prefab asset (it is instantiated on the vehicle for you). Optional "Anchor" transforms say
    /// where the trail, smoke and explosion sit and which way they point (+Z of the anchor).
    ///
    /// DEFAULTS: every slot you leave empty uses the project default from Resources/VehicleSystem/VehicleEffectsDefaults
    /// (switch off with "Use Defaults For Empty Slots").
    ///
    /// Keep particle counts low: this runs on phones.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleController))]
    public class VehicleEffects : MonoBehaviour
    {
        [Header("Effects: leave a slot empty to use the default")]
        public ParticleSystem trail;
        public ParticleSystem hitSparks;
        public ParticleSystem hitDust;
        public ParticleSystem lowHealthSmoke;
        public ParticleSystem explosion;
        [Tooltip("Fill empty slots from the project defaults.")]
        public bool useDefaultsForEmptySlots = true;

        [Header("Where the continuous effects sit (optional)")]
        [Tooltip("Empty = behind the vehicle at ground level, pointing backward.")]
        public Transform trailAnchor;
        [Tooltip("Empty = above the middle of the vehicle, pointing up.")]
        public Transform smokeAnchor;
        [Tooltip("Empty = the middle of the vehicle.")]
        public Transform explosionAnchor;

        [Header("Trail")]
        [Tooltip("Particles per second at top speed.")]
        public float trailRateAtTopSpeed = 45f;
        [Tooltip("No trail below this speed (m/s).")]
        public float trailMinSpeed = 3f;
        [Tooltip("The trail is this many times denser while the battery boost is on.")]
        public float boostTrailMultiplier = 2.5f;

        [Header("Hits")]
        public int minBurst = 8;
        public int maxBurst = 45;
        [Tooltip("Sparks only from this tier up.")]
        public SpeedTier sparksFromTier = SpeedTier.Fast;

        [Header("Low health")]
        [Range(0.05f, 1f)] public float smokeBelowHealth = 0.35f;
        public float smokeRate = 18f;

        VehicleController vehicle;
        VehicleImpactSensor sensor;
        VehicleHealth health;
        VehicleBoost boost;
        Transform defaultsRoot;

        void Awake()
        {
            vehicle = GetComponent<VehicleController>();
            sensor = GetComponent<VehicleImpactSensor>();
            health = GetComponent<VehicleHealth>();
            boost = GetComponent<VehicleBoost>();

            var defaults = useDefaultsForEmptySlots ? VehicleEffectsDefaults.Load() : null;
            if (useDefaultsForEmptySlots && defaults == null)
                Debug.LogWarning("VehicleEffects: no default effects found. In the Unity editor run Tools > Vehicle > Create Default Effects.", this);

            Bounds b = LocalBounds();
            Vector3 rear = new Vector3(b.center.x, b.min.y + 0.2f, b.min.z);
            Vector3 top = new Vector3(b.center.x, b.min.y + b.size.y * 0.55f, b.center.z);

            Resolve(ref trail, defaults != null ? defaults.trail : null, trailAnchor, rear, Quaternion.Euler(0f, 180f, 0f));
            Resolve(ref lowHealthSmoke, defaults != null ? defaults.lowHealthSmoke : null, smokeAnchor, top, Quaternion.Euler(-90f, 0f, 0f));
            Resolve(ref explosion, defaults != null ? defaults.explosion : null, explosionAnchor, b.center, Quaternion.identity);
            Resolve(ref hitSparks, defaults != null ? defaults.hitSparks : null, null, b.center, Quaternion.identity);
            Resolve(ref hitDust, defaults != null ? defaults.hitDust : null, null, b.center, Quaternion.identity);
        }

        void OnEnable()
        {
            if (sensor != null) sensor.Impact += OnImpact;
            if (health != null)
            {
                health.Destroyed += OnDestroyed;
                health.Respawned += OnRespawned;
            }

            // looping systems with no emission of their own: the code below sets their rate or calls Emit()
            if (trail != null) trail.Play();
            if (lowHealthSmoke != null) lowHealthSmoke.Play();
            if (hitSparks != null) hitSparks.Play();
            if (hitDust != null) hitDust.Play();
        }

        void OnDisable()
        {
            if (sensor != null) sensor.Impact -= OnImpact;
            if (health != null)
            {
                health.Destroyed -= OnDestroyed;
                health.Respawned -= OnRespawned;
            }
        }

        // ------------------------------------------------------------------ slots

        /// <summary>
        /// A slot can hold (a) nothing -> the default prefab, (b) an object that is already part of the vehicle -> used as is,
        /// or (c) a prefab asset -> instantiated on the vehicle at the anchor / computed position.
        /// </summary>
        void Resolve(ref ParticleSystem slot, ParticleSystem defaultPrefab, Transform anchor, Vector3 localPosition, Quaternion localRotation)
        {
            ParticleSystem source = slot != null ? slot : defaultPrefab;
            if (source == null) return;

            if (slot != null && slot.gameObject.scene.IsValid()) return; // a scene object of this vehicle: leave it alone

            Transform parent = anchor != null ? anchor : DefaultsRoot();
            ParticleSystem instance = Instantiate(source, parent);
            if (anchor != null)
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                instance.transform.localPosition = localPosition;
                instance.transform.localRotation = localRotation;
            }
            instance.transform.localScale = Vector3.one;
            slot = instance;
        }

        Transform DefaultsRoot()
        {
            if (defaultsRoot == null)
            {
                var go = new GameObject("Effects (default)");
                go.transform.SetParent(transform, false);
                defaultsRoot = go.transform;
            }
            return defaultsRoot;
        }

        /// <summary>Bounds of the vehicle's meshes in its own frame.</summary>
        Bounds LocalBounds()
        {
            bool has = false;
            Bounds b = new Bounds(Vector3.up * 0.8f, new Vector3(1.5f, 1.6f, 3f));

            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

                Bounds w = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = w.center + Vector3.Scale(w.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 local = transform.InverseTransformPoint(corner);
                    if (!has) { b = new Bounds(local, Vector3.zero); has = true; }
                    else b.Encapsulate(local);
                }
            }
            return b;
        }

        // ------------------------------------------------------------------ playing

        void Update()
        {
            bool destroyed = health != null && health.IsDestroyed;

            // trail: only while rolling on the ground
            if (trail != null)
            {
                float speed = Mathf.Abs(vehicle.ForwardSpeed);
                float top = vehicle.Stats != null ? Mathf.Max(1f, vehicle.Stats.Get(VehicleStat.MaxSpeed) / 3.6f) : 15f;
                bool on = !destroyed && vehicle.IsGrounded && speed > trailMinSpeed;

                var emission = trail.emission;
                float boosted = boost != null && boost.IsBoosting ? boostTrailMultiplier : 1f;
                emission.rateOverTime = on ? Mathf.Clamp01(speed / top) * trailRateAtTopSpeed * boosted : 0f;
            }

            // smoke when badly damaged
            if (lowHealthSmoke != null && health != null)
            {
                var emission = lowHealthSmoke.emission;
                bool smoking = !destroyed && health.Health01 < smokeBelowHealth;
                emission.rateOverTime = smoking ? smokeRate * (1f - health.Health01 / smokeBelowHealth) + 4f : 0f;
            }
        }

        void OnImpact(ImpactInfo info)
        {
            int count = Mathf.RoundToInt(Mathf.Lerp(minBurst, maxBurst, Mathf.Clamp01(info.speedRatio)));

            if (hitDust != null) Burst(hitDust, info.point, count);
            if (hitSparks != null && info.tier >= sparksFromTier) Burst(hitSparks, info.point, count);
        }

        static void Burst(ParticleSystem ps, Vector3 position, int count)
        {
            ps.transform.position = position;
            ps.Emit(count);
        }

        void OnDestroyed(ImpactInfo cause)
        {
            if (explosion != null)
            {
                if (explosionAnchor == null) explosion.transform.position = transform.position + Vector3.up * 0.6f;
                explosion.Play();
            }
            if (lowHealthSmoke != null) lowHealthSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void OnRespawned()
        {
            if (lowHealthSmoke != null) lowHealthSmoke.Play();
        }
    }
}

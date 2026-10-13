using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Health bar instead of visual damage. Impacts from VehicleImpactSensor take health away depending on speed and
    /// on which side was hit. When health runs out (or a high-speed head-on crash happens) the vehicle is destroyed:
    /// it disappears for a few seconds, then respawns at the last safe place with full health and a short period
    /// of invulnerability.
    ///
    /// Repair: a manual recovery (VehicleRecovery.ManualRecovered) restores a share of the health.
    /// Scoring listens to Destroyed (penalty) and Damaged. Nothing in here knows about points.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleController))]
    [RequireComponent(typeof(VehicleImpactSensor))]
    public class VehicleHealth : MonoBehaviour
    {
        [Header("Health")]
        public float maxHealth = 100f;

        [Header("Damage")]
        [Tooltip("X = impact speed / top speed, Y = share of max health lost. Default: ~10% at half speed, ~40% at 80%, all of it at full speed.")]
        public AnimationCurve damageBySpeed = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.15f, 0f), new Keyframe(0.5f, 0.1f),
            new Keyframe(0.8f, 0.4f), new Keyframe(1f, 1f));
        [Tooltip("Damage multiplier per side that was hit.")]
        public float frontMultiplier = 1f;
        public float sideMultiplier = 0.4f;
        public float rearMultiplier = 0.6f;
        [Tooltip("A head-on crash at this tier or faster destroys the vehicle at once.")]
        public bool headOnKills = true;
        public SpeedTier headOnKillTier = SpeedTier.FullThrottle;
        [Tooltip("Impacts closer together than this (seconds) count once.")]
        public float hitCooldown = 0.2f;

        [Header("Destroyed / respawn")]
        [Tooltip("Seconds between being destroyed and the respawn.")]
        public float respawnDelay = 3f;
        [Tooltip("Seconds of protection after respawning (the vehicle blinks).")]
        public float respawnInvulnerability = 2.5f;

        [Header("Repair")]
        [Tooltip("Share of max health a manual recovery (repair kit) restores.")]
        [Range(0f, 1f)] public float repairHealFraction = 0.35f;

        public float Health { get; private set; }
        public float Health01 => maxHealth > 0f ? Mathf.Clamp01(Health / maxHealth) : 0f;
        public bool IsDestroyed { get; private set; }
        public bool IsInvulnerable => Time.time < invulnerableUntil;
        /// <summary>Seconds until the respawn while the vehicle is destroyed (0 otherwise).</summary>
        public float RespawnTimeLeft { get; private set; }

        /// <summary>(health, maxHealth) whenever health changes.</summary>
        public event Action<float, float> HealthChanged;
        /// <summary>Damage taken (amount, impact).</summary>
        public event Action<float, ImpactInfo> Damaged;
        /// <summary>The vehicle was destroyed: apply the penalty here.</summary>
        public event Action<ImpactInfo> Destroyed;
        public event Action Respawned;

        VehicleImpactSensor sensor;
        VehicleRecovery recovery;
        VehicleDriver driver;
        Rigidbody rb;

        readonly List<Renderer> visuals = new List<Renderer>();
        readonly List<Collider> bodyColliders = new List<Collider>();
        float invulnerableUntil, nextHitTime;
        bool blinking;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sensor = GetComponent<VehicleImpactSensor>();
            recovery = GetComponent<VehicleRecovery>();
            driver = GetComponent<VehicleDriver>();
            Health = maxHealth;

            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r is MeshRenderer || r is SkinnedMeshRenderer) visuals.Add(r);   // not particles / trails

            foreach (var c in GetComponentsInChildren<Collider>(true))
                if (!(c is WheelCollider)) bodyColliders.Add(c);
        }

        void OnEnable()
        {
            sensor.Impact += OnImpact;
            if (recovery != null) recovery.ManualRecovered += OnManualRecovered;
        }

        void OnDisable()
        {
            sensor.Impact -= OnImpact;
            if (recovery != null) recovery.ManualRecovered -= OnManualRecovered;
        }

        void Start() => HealthChanged?.Invoke(Health, maxHealth);

        // ------------------------------------------------------------------ taking damage

        void OnImpact(ImpactInfo info)
        {
            if (IsDestroyed || IsInvulnerable || Time.time < nextHitTime) return;
            nextHitTime = Time.time + hitCooldown;

            bool headOn = headOnKills && info.zone == HitZone.Front && info.tier >= headOnKillTier;
            if (headOn)
            {
                ApplyDamage(Health, info);
                return;
            }

            float zone = info.zone == HitZone.Front ? frontMultiplier : info.zone == HitZone.Rear ? rearMultiplier : sideMultiplier;
            float amount = damageBySpeed.Evaluate(info.speedRatio) * maxHealth * zone;
            if (amount > 0.01f) ApplyDamage(amount, info);
        }

        /// <summary>Takes health away (also usable by hazards that are not collisions).</summary>
        public void ApplyDamage(float amount, ImpactInfo info = default)
        {
            if (IsDestroyed || amount <= 0f) return;

            Health = Mathf.Max(0f, Health - amount);
            Damaged?.Invoke(amount, info);
            HealthChanged?.Invoke(Health, maxHealth);

            if (Health <= 0f) StartCoroutine(DestroyAndRespawn(info));
        }

        // ------------------------------------------------------------------ healing

        public void Heal(float amount)
        {
            if (IsDestroyed || amount <= 0f) return;
            Health = Mathf.Min(maxHealth, Health + amount);
            HealthChanged?.Invoke(Health, maxHealth);
        }

        /// <summary>
        /// For vehicles of OTHER players: the network sets the health the owner reports, so name tags and bars match.
        /// It only updates the number and raises HealthChanged; it never destroys or respawns anything.
        /// </summary>
        public void SetHealthFromNetwork(float value)
        {
            Health = Mathf.Clamp(value, 0f, maxHealth);
            HealthChanged?.Invoke(Health, maxHealth);
        }

        /// <summary>Full health again (call at the start of a match).</summary>
        public void ResetHealth()
        {
            Health = maxHealth;
            HealthChanged?.Invoke(Health, maxHealth);
        }

        void OnManualRecovered(bool teleported) => Heal(maxHealth * repairHealFraction);

        // ------------------------------------------------------------------ destroyed and respawn

        IEnumerator DestroyAndRespawn(ImpactInfo cause)
        {
            IsDestroyed = true;
            Destroyed?.Invoke(cause);

            // the wreck: invisible, harmless and still
            if (driver != null) driver.inputEnabled = false;
            ZeroVelocity();
            rb.isKinematic = true;
            SetVisible(false);
            SetBodyCollidersEnabled(false);

            float wait = respawnDelay;
            while (wait > 0f)
            {
                RespawnTimeLeft = wait;
                wait -= Time.deltaTime;
                yield return null;
            }
            RespawnTimeLeft = 0f;

            rb.isKinematic = false;
            if (recovery != null) recovery.TeleportToLastSafePoint();
            SetBodyCollidersEnabled(true);
            SetVisible(true);

            Health = maxHealth;
            IsDestroyed = false;
            invulnerableUntil = Time.time + respawnInvulnerability;
            if (driver != null) driver.inputEnabled = true;

            HealthChanged?.Invoke(Health, maxHealth);
            Respawned?.Invoke();
        }

        void Update()
        {
            if (IsDestroyed) return;

            // blink while protected
            if (IsInvulnerable)
            {
                blinking = true;
                SetVisible(Mathf.FloorToInt(Time.time * 12f) % 2 == 0);
            }
            else if (blinking)
            {
                blinking = false;
                SetVisible(true);
            }
        }

        void SetVisible(bool visible)
        {
            for (int i = 0; i < visuals.Count; i++)
                if (visuals[i] != null) visuals[i].enabled = visible;
        }

        void SetBodyCollidersEnabled(bool enabledState)
        {
            for (int i = 0; i < bodyColliders.Count; i++)
                if (bodyColliders[i] != null) bodyColliders[i].enabled = enabledState;
        }

        void ZeroVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
        }
    }
}

using UnityEngine;
using VehicleSystem;

namespace AnimeToon.Integration
{
    /// <summary>
    /// GAME SPECIFIC (Rickshaw Chaos / VehicleSystem). Not part of the universal AnimeToon pack.
    /// Feeds the camera effects (AnimeSpeedFx) from the player's vehicle and adds the extra impact
    /// effects (comic starburst + ring) that VehicleEffects has no slot for.
    ///
    /// Put it on the camera next to AnimeSpeedFx. It finds the player vehicle by itself (tag "Player", or the only
    /// VehicleController in the scene) and re-finds it after a respawn / new spawn. You can also call SetTarget().
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimeVehicleFxBridge : MonoBehaviour
    {
        [Header("Wiring")]
        public AnimeSpeedFx fx;
        [Tooltip("Optional. Leave empty to find the player vehicle automatically.")]
        public VehicleController target;
        public string playerTag = "Player";

        [Header("Extra impact effects (prefabs from the VFX pack, optional)")]
        public ParticleSystem impactBurstPrefab;
        public ParticleSystem shockRingPrefab;
        public float burstSize = 3f;
        public float ringSize = 4f;
        [Tooltip("Ignore tiny bumps: impacts below this speed ratio (0..1) get no burst, flash or shake.")]
        [Range(0f, 1f)] public float minImpactRatio = 0.3f;

        VehicleImpactSensor sensor;
        VehicleHealth health;
        VehicleBoost boost;
        ParticleSystem burst;
        ParticleSystem ring;
        float findTimer;

        void Awake()
        {
            if (fx == null) fx = GetComponent<AnimeSpeedFx>();
            burst = Spawn(impactBurstPrefab);
            ring = Spawn(shockRingPrefab);
        }

        void OnEnable()
        {
            if (target != null) Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        static ParticleSystem Spawn(ParticleSystem prefab)
        {
            if (prefab == null) return null;
            ParticleSystem instance = prefab.gameObject.scene.IsValid() ? prefab : Instantiate(prefab);
            instance.Play();
            return instance;
        }

        public void SetTarget(VehicleController vehicle)
        {
            if (vehicle == target && sensor != null) return;
            Unsubscribe();
            target = vehicle;
            if (target != null) Subscribe();
        }

        void Subscribe()
        {
            if (target == null) return;
            sensor = target.GetComponent<VehicleImpactSensor>();
            health = target.GetComponent<VehicleHealth>();
            boost = target.GetComponent<VehicleBoost>();
            if (sensor != null) sensor.Impact += OnImpact;
            if (health != null) health.Destroyed += OnDestroyed;
        }

        void Unsubscribe()
        {
            if (sensor != null) sensor.Impact -= OnImpact;
            if (health != null) health.Destroyed -= OnDestroyed;
            sensor = null;
            health = null;
            boost = null;
        }

        void Update()
        {
            if (target == null)
            {
                findTimer -= Time.unscaledDeltaTime;
                if (findTimer <= 0f)
                {
                    findTimer = 0.5f;
                    FindTarget();
                }
                if (fx != null)
                {
                    fx.SetSpeed01(0f);
                    fx.SetBoosting(false);
                }
                return;
            }

            if (fx == null) return;

            float top = target.Stats != null ? Mathf.Max(1f, target.Stats.Get(VehicleStat.MaxSpeed) / 3.6f) : 15f;
            fx.SetSpeed01(Mathf.Clamp01(Mathf.Abs(target.ForwardSpeed) / top));
            fx.SetBoosting(boost != null && boost.IsBoosting && !(health != null && health.IsDestroyed));
        }

        void FindTarget()
        {
            VehicleController found = null;

            try
            {
                GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged != null)
                {
                    found = tagged.GetComponent<VehicleController>();
                    if (found == null) found = tagged.GetComponentInParent<VehicleController>();
                    if (found == null) found = tagged.GetComponentInChildren<VehicleController>();
                }
            }
            catch (UnityException)
            {
                // the tag does not exist in this project: fall through to the single-vehicle check
            }

            if (found == null)
            {
                VehicleController[] all = FindObjectsByType<VehicleController>(FindObjectsSortMode.None);
                if (all.Length == 1) found = all[0];
            }

            if (found != null) SetTarget(found);
        }

        void OnImpact(ImpactInfo info)
        {
            float strength = Mathf.Clamp01(info.speedRatio);
            if (strength < minImpactRatio) return;

            if (fx != null) fx.Impact(strength);

            if (burst != null)
            {
                burst.transform.position = info.point;
                var p = new ParticleSystem.EmitParams { startSize = burstSize * Mathf.Lerp(0.6f, 1.3f, strength) };
                burst.Emit(p, 1);
            }

            if (ring != null)
            {
                ring.transform.position = info.point;
                var p = new ParticleSystem.EmitParams { startSize = ringSize * Mathf.Lerp(0.7f, 1.4f, strength) };
                ring.Emit(p, 1);
            }
        }

        void OnDestroyed(ImpactInfo cause)
        {
            if (fx != null) fx.Impact(1f);
        }
    }
}

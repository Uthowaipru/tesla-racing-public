using System;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Battery boost. The vehicle carries a few batteries (collected from the road, reset every match, never saved).
    /// BOOST spends one for a short burst of acceleration, top speed and collision power: used to catch up with other
    /// players, or to hit traffic harder.
    ///
    /// The effect is a set of temporary stat modifiers on VehicleStats (the same system the upgrades use), plus a
    /// temporary mass increase so hits push traffic harder. Upgrades can make the boost better through
    /// "Duration Multiplier" and "Power Multiplier".
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleController))]
    public class VehicleBoost : MonoBehaviour
    {
        [Header("Batteries")]
        [Tooltip("Batteries at the start of a match.")]
        public int startingBatteries = 1;
        [Tooltip("Most batteries that can be carried.")]
        public int maxBatteries = 3;

        [Header("Boost")]
        [Tooltip("Seconds one battery lasts.")]
        public float duration = 2.5f;
        [Tooltip("Extra motor power (acceleration). 0.8 = +80%.")]
        public float torqueBonus = 0.8f;
        [Tooltip("Extra top speed. 0.3 = +30%.")]
        public float topSpeedBonus = 0.3f;
        [Tooltip("Mass multiplier while boosting: a heavier vehicle pushes traffic harder. 1 = off.")]
        public float massMultiplier = 1.25f;

        [Header("Upgrades (set by the upgrade system)")]
        public float durationMultiplier = 1f;
        public float powerMultiplier = 1f;

        public int Batteries { get; private set; }
        public bool IsBoosting { get; private set; }
        public float BoostTimeLeft { get; private set; }
        public float BoostTimeTotal { get; private set; }

        /// <summary>The number of batteries changed.</summary>
        public event Action<int> BatteriesChanged;
        public event Action BoostStarted;
        public event Action BoostEnded;

        VehicleController vehicle;
        VehicleStats stats;
        VehicleHealth health;
        Rigidbody rb;
        float baseMass;

        void Awake()
        {
            vehicle = GetComponent<VehicleController>();
            stats = vehicle.Stats;
            health = GetComponent<VehicleHealth>();
            rb = GetComponent<Rigidbody>();
            baseMass = rb != null ? rb.mass : 1f;
            Batteries = Mathf.Clamp(startingBatteries, 0, maxBatteries);
        }

        void OnEnable()
        {
            if (health != null) health.Destroyed += OnDestroyed;
        }

        void OnDisable()
        {
            if (health != null) health.Destroyed -= OnDestroyed;
            if (IsBoosting) EndBoost();
        }

        void Start() => BatteriesChanged?.Invoke(Batteries);

        void OnDestroyed(ImpactInfo cause)
        {
            if (IsBoosting) EndBoost();
        }

        void Update()
        {
            if (!IsBoosting) return;

            BoostTimeLeft -= Time.deltaTime;
            if (BoostTimeLeft <= 0f) EndBoost();
        }

        // ------------------------------------------------------------------ batteries

        /// <summary>A battery was collected. Returns false (and takes nothing) when already full.</summary>
        public bool AddBattery(int amount = 1)
        {
            if (Batteries >= maxBatteries) return false;
            Batteries = Mathf.Min(maxBatteries, Batteries + Mathf.Max(1, amount));
            BatteriesChanged?.Invoke(Batteries);
            return true;
        }

        /// <summary>Start of a match: back to the starting batteries, no boost.</summary>
        public void ResetBatteries()
        {
            if (IsBoosting) EndBoost();
            Batteries = Mathf.Clamp(startingBatteries, 0, maxBatteries);
            BatteriesChanged?.Invoke(Batteries);
        }

        // ------------------------------------------------------------------ boosting

        /// <summary>Spends one battery. Does nothing (returns false) with no battery, while boosting, or when destroyed.</summary>
        public bool TryBoost()
        {
            if (IsBoosting || Batteries <= 0) return false;
            if (health != null && health.IsDestroyed) return false;

            Batteries--;
            BatteriesChanged?.Invoke(Batteries);

            BoostTimeTotal = duration * Mathf.Max(0.1f, durationMultiplier);
            BoostTimeLeft = BoostTimeTotal;
            IsBoosting = true;

            if (stats != null)
            {
                float power = Mathf.Max(0f, powerMultiplier);
                stats.AddModifier(new StatModifier(VehicleStat.MotorTorque, ModifierType.PercentAdd, torqueBonus * power, this, "boost"));
                stats.AddModifier(new StatModifier(VehicleStat.MaxSpeed, ModifierType.PercentAdd, topSpeedBonus * power, this, "boost"));
            }
            if (rb != null) rb.mass = baseMass * Mathf.Max(0.1f, massMultiplier);

            BoostStarted?.Invoke();
            return true;
        }

        void EndBoost()
        {
            IsBoosting = false;
            BoostTimeLeft = 0f;

            if (stats != null) stats.RemoveAllFromSource(this);
            if (rb != null) rb.mass = baseMass;

            BoostEnded?.Invoke();
        }
    }
}

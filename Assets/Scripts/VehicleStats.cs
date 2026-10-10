using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>Every tunable the upgrade/equipment system is allowed to touch.</summary>
    public enum VehicleStat
    {
        MotorTorque,      // total engine torque (Nm), split across drive wheels
        MaxSpeed,         // km/h
        BrakeTorque,      // total brake torque, split across brake wheels
        SteerAngle,       // max steering angle (deg)
        TireGrip,         // multiplier on wheel friction stiffness (1 = default)
        SuspensionSpring, // spring strength
        SuspensionDamper, // damping
        SuspensionTravel, // suspension distance (m)
        AntiRoll          // anti-roll bar strength (per axle with a left+right wheel)
    }

    public enum ModifierType
    {
        Flat,       // +value
        PercentAdd, // +value as fraction (0.1 = +10%), all PercentAdd are summed first
        Multiply    // x value
    }

    /// <summary>
    /// One upgrade effect. Create these when an item is equipped, and remove them
    /// (by reference or by Source) when it is unequipped.
    /// </summary>
    public class StatModifier
    {
        public readonly VehicleStat Stat;
        public readonly ModifierType Type;
        public readonly float Value;
        public readonly object Source; // e.g. the equipment item that created it

        public StatModifier(VehicleStat stat, ModifierType type, float value, object source = null)
        {
            Stat = stat;
            Type = type;
            Value = value;
            Source = source;
        }
    }

    /// <summary>
    /// Holds base values + active modifiers. The controller only ever reads Get(),
    /// so any future system (tires, suspension kits, engines, buffs) just adds modifiers.
    /// Final = (Base + Flat) * (1 + sum(PercentAdd)) * product(Multiply)
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleStats : MonoBehaviour
    {
        [Header("Base Values")]
        public float motorTorque = 600f;
        public float maxSpeedKmh = 55f;
        public float brakeTorque = 1800f;
        public float steerAngle = 32f;
        public float tireGrip = 1f;
        public float suspensionSpring = 18000f;
        public float suspensionDamper = 1800f;
        public float suspensionTravel = 0.25f;
        public float antiRoll = 3000f;

        /// <summary>Fired whenever a modifier is added/removed or a base value is edited at runtime.</summary>
        public event Action OnStatsChanged;

        static readonly int StatCount = Enum.GetValues(typeof(VehicleStat)).Length;

        readonly List<StatModifier> modifiers = new List<StatModifier>();
        readonly float[] cache = new float[StatCount];
        bool dirty = true;

        public float Get(VehicleStat stat)
        {
            if (dirty) Rebuild();
            return cache[(int)stat];
        }

        public float GetBase(VehicleStat stat)
        {
            switch (stat)
            {
                case VehicleStat.MotorTorque:      return motorTorque;
                case VehicleStat.MaxSpeed:         return maxSpeedKmh;
                case VehicleStat.BrakeTorque:      return brakeTorque;
                case VehicleStat.SteerAngle:       return steerAngle;
                case VehicleStat.TireGrip:         return tireGrip;
                case VehicleStat.SuspensionSpring: return suspensionSpring;
                case VehicleStat.SuspensionDamper: return suspensionDamper;
                case VehicleStat.SuspensionTravel: return suspensionTravel;
                case VehicleStat.AntiRoll:         return antiRoll;
                default: return 0f;
            }
        }

        public void AddModifier(StatModifier mod)
        {
            if (mod == null) return;
            modifiers.Add(mod);
            Refresh();
        }

        public bool RemoveModifier(StatModifier mod)
        {
            bool removed = modifiers.Remove(mod);
            if (removed) Refresh();
            return removed;
        }

        /// <summary>Removes everything an item added (call this when unequipping).</summary>
        public int RemoveAllFromSource(object source)
        {
            int removed = modifiers.RemoveAll(m => m.Source == source);
            if (removed > 0) Refresh();
            return removed;
        }

        /// <summary>Force a recalculation + notify listeners.</summary>
        public void Refresh()
        {
            dirty = true;
            OnStatsChanged?.Invoke();
        }

        void OnValidate()
        {
            // Lets you tweak base values in the inspector while playing.
            if (Application.isPlaying) Refresh(); else dirty = true;
        }

        void Rebuild()
        {
            for (int i = 0; i < StatCount; i++)
            {
                var stat = (VehicleStat)i;
                float flat = 0f, pct = 0f, mult = 1f;

                for (int m = 0; m < modifiers.Count; m++)
                {
                    var mod = modifiers[m];
                    if (mod.Stat != stat) continue;
                    switch (mod.Type)
                    {
                        case ModifierType.Flat:       flat += mod.Value; break;
                        case ModifierType.PercentAdd: pct  += mod.Value; break;
                        case ModifierType.Multiply:   mult *= mod.Value; break;
                    }
                }

                cache[i] = Mathf.Max(0f, (GetBase(stat) + flat) * (1f + pct) * mult);
            }
            dirty = false;
        }
    }
}

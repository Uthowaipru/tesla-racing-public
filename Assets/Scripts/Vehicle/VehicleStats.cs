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
        public readonly string Id;     // optional stable id (StatModifierDef.id) so it can be saved / sent over the network

        public StatModifier(VehicleStat stat, ModifierType type, float value, object source = null, string id = null)
        {
            Stat = stat;
            Type = type;
            Value = value;
            Source = source;
            Id = id;
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
        [Header("Data")]
        [Tooltip("The car's data asset. When assigned, the base values come from it (single source of truth) and the " +
                 "values below are ignored. VehicleSpawner assigns it automatically when the car is spawned.")]
        public VehicleDefinition definition;

        [Header("Base Values (only used when no Definition is assigned)")]
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

        void OnEnable() => VehicleDefinition.ValuesChanged += OnDefinitionChanged;
        void OnDisable() => VehicleDefinition.ValuesChanged -= OnDefinitionChanged;

        // editing the data asset while playing updates every car that uses it
        void OnDefinitionChanged(VehicleDefinition changed)
        {
            if (changed == definition) Refresh();
        }

        /// <summary>The base value before modifiers: from the Definition if there is one, else from this component.</summary>
        public float GetBase(VehicleStat stat)
        {
            VehicleDefinition d = definition;
            switch (stat)
            {
                case VehicleStat.MotorTorque:      return d != null ? d.motorTorque : motorTorque;
                case VehicleStat.MaxSpeed:         return d != null ? d.maxSpeedKmh : maxSpeedKmh;
                case VehicleStat.BrakeTorque:      return d != null ? d.brakeTorque : brakeTorque;
                case VehicleStat.SteerAngle:       return d != null ? d.steerAngle : steerAngle;
                case VehicleStat.TireGrip:         return d != null ? d.tireGrip : tireGrip;
                case VehicleStat.SuspensionSpring: return d != null ? d.suspensionSpring : suspensionSpring;
                case VehicleStat.SuspensionDamper: return d != null ? d.suspensionDamper : suspensionDamper;
                case VehicleStat.SuspensionTravel: return d != null ? d.suspensionTravel : suspensionTravel;
                case VehicleStat.AntiRoll:         return d != null ? d.antiRoll : antiRoll;
                default: return 0f;
            }
        }

        /// <summary>
        /// Makes the car use a VehicleDefinition (its data asset) as the source of its base values.
        /// Existing modifiers are kept. VehicleSpawner calls this right after spawning.
        /// </summary>
        public void ApplyDefinition(VehicleDefinition def)
        {
            if (def == null) return;
            definition = def;
            Refresh();
        }

        public void AddModifier(StatModifier mod)
        {
            if (mod == null) return;
            modifiers.Add(mod);
            Refresh();
        }

        /// <summary>Adds a modifier asset. Use the same "source" with RemoveAllFromSource() to take it off again.</summary>
        public StatModifier AddModifier(StatModifierDef def, object source = null)
        {
            if (def == null) return null;
            var mod = def.CreateModifier(source);
            AddModifier(mod);
            return mod;
        }

        /// <summary>Ids of the active modifiers that have one (what you save or send over the network).</summary>
        public void GetModifierIds(List<string> into)
        {
            if (into == null) return;
            for (int i = 0; i < modifiers.Count; i++)
                if (!string.IsNullOrEmpty(modifiers[i].Id)) into.Add(modifiers[i].Id);
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

using System.Collections.Generic;
using UnityEngine;
using VehicleSystem;

namespace AnimeToon.Integration
{
    /// <summary>
    /// GAME SPECIFIC. Makes the particles of VehicleEffects follow the quality tier, without touching VehicleEffects:
    ///   - the trail and the low-health smoke (their rate is set by VehicleEffects every frame) are scaled in LateUpdate
    ///   - every effect (trail, sparks, dust, smoke, explosion and its children) gets its particle cap scaled, which also
    ///     limits the burst effects (hits, explosion)
    /// The scale is the tier's "Effects" value. It is added to vehicles automatically by AnimeDriftBootstrap.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)] // after VehicleEffects.Update
    public class AnimeVehicleEffectsQuality : MonoBehaviour
    {
        class Entry
        {
            public ParticleSystem system;
            public int originalMax;
            public bool rateDriven;     // VehicleEffects writes its emission rate every frame
            public float lastWritten = -1f;
        }

        readonly List<Entry> entries = new List<Entry>();
        VehicleEffects effects;
        bool ready;
        float appliedScale = -1f;

        void Awake()
        {
            if (!TryGetComponent(out effects)) enabled = false;
        }

        void LateUpdate()
        {
            if (!ready)
            {
                Setup();
                if (!ready) return;
            }

            float scale = AnimeQuality.ParticleScale;

            if (!Mathf.Approximately(scale, appliedScale))
            {
                appliedScale = scale;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].system == null) continue;
                    var main = entries[i].system.main;
                    main.maxParticles = Mathf.Max(2, Mathf.RoundToInt(entries[i].originalMax * scale));
                }
            }

            if (scale > 0.999f) return;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (!e.rateDriven || e.system == null) continue;

                var emission = e.system.emission;
                float current = emission.rateOverTime.constant;

                // VehicleEffects did not write a new rate this frame: ours is still in place, do not scale it twice
                if (Mathf.Abs(current - e.lastWritten) < 0.0001f) continue;

                e.lastWritten = current * scale;
                emission.rateOverTime = e.lastWritten;
            }
        }

        void Setup()
        {
            // VehicleEffects fills its slots in Awake, so look only once the first frame has run
            var seen = new HashSet<ParticleSystem>();
            Add(effects.trail, true, seen);
            Add(effects.lowHealthSmoke, true, seen);
            Add(effects.hitSparks, false, seen);
            Add(effects.hitDust, false, seen);
            Add(effects.explosion, false, seen);
            ready = true;
        }

        void Add(ParticleSystem root, bool rateDriven, HashSet<ParticleSystem> seen)
        {
            if (root == null) return;

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!seen.Add(ps)) continue;
                entries.Add(new Entry
                {
                    system = ps,
                    originalMax = ps.main.maxParticles,
                    rateDriven = rateDriven && ps == root
                });
            }
        }
    }
}

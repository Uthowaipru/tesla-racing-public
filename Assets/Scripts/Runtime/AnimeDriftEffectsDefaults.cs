using UnityEngine;

namespace AnimeToon.Integration
{
    /// <summary>
    /// GAME SPECIFIC (Rickshaw Chaos / VehicleSystem). The project-wide default drift effects.
    /// AnimeDriftEffects uses these for every slot a vehicle leaves empty. Created in Assets/Resources/VehicleSystem
    /// by Tools > Vehicle > Install Anime Drift Effects.
    /// </summary>
    public class AnimeDriftEffectsDefaults : ScriptableObject
    {
        public const string ResourcePath = "VehicleSystem/AnimeDriftEffectsDefaults";

        public ParticleSystem smoke;
        public ParticleSystem sparks;
        public Material skid;

        static AnimeDriftEffectsDefaults cached;

        public static AnimeDriftEffectsDefaults Load()
        {
            if (cached == null) cached = Resources.Load<AnimeDriftEffectsDefaults>(ResourcePath);
            return cached;
        }
    }
}

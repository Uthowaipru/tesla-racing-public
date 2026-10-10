using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// An upgrade / boost / handicap as an asset. Because it has a stable id, only the id needs to be saved
    /// or sent over the network, and every device rebuilds the same stats from it.
    /// Create with Assets > Create > Vehicle System > Stat Modifier.
    /// </summary>
    [CreateAssetMenu(menuName = "Vehicle System/Stat Modifier", fileName = "NewModifier")]
    public class StatModifierDef : ScriptableObject
    {
        [Tooltip("Stable unique id (e.g. \"engine_v2\"). Never change it once released.")]
        public string id = "modifier";
        public string displayName = "Modifier";
        public VehicleStat stat = VehicleStat.MotorTorque;
        public ModifierType type = ModifierType.PercentAdd;
        [Tooltip("Flat: +value. PercentAdd: 0.1 = +10%. Multiply: x value.")]
        public float value = 0.1f;

        /// <summary>Creates the runtime modifier. Pass the same "source" later to remove it again.</summary>
        public StatModifier CreateModifier(object source = null) => new StatModifier(stat, type, value, source, id);

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) id = name;
        }
    }
}

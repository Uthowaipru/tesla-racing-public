using UnityEngine;

namespace VehicleSystem
{
    /// <summary>One race map as data. Create with Assets > Create > Vehicle System > Map Definition.</summary>
    [CreateAssetMenu(menuName = "Vehicle System/Map Definition", fileName = "NewMap")]
    public class MapDefinition : ScriptableObject
    {
        [Tooltip("Stable unique id (e.g. \"desert_loop\"). Sent over the network, never change it once released.")]
        public string id = "map";
        public string displayName = "Map";
        public Sprite thumbnail;

        [Tooltip("Scene name. The scene must be added to the Build Profiles / Build Settings.")]
        public string sceneName;

        [Min(1)] public int defaultLaps = 3;
        [Min(1)] public int maxPlayers = 4;

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) id = name;
        }
    }
}

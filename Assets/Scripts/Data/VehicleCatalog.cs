using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// The list of every vehicle in the game. The selector menu shows these, and the spawner turns a saved
    /// vehicle id back into a prefab with it (in any scene, and later for remote players' ids).
    /// Create with Assets > Create > Vehicle System > Vehicle Catalog, then drag your VehicleDefinitions in.
    /// </summary>
    [CreateAssetMenu(menuName = "Vehicle System/Vehicle Catalog", fileName = "VehicleCatalog")]
    public class VehicleCatalog : ScriptableObject
    {
        public List<VehicleDefinition> vehicles = new List<VehicleDefinition>();

        /// <summary>The first vehicle that is set up (used when nothing has been chosen yet).</summary>
        public VehicleDefinition First
        {
            get
            {
                for (int i = 0; i < vehicles.Count; i++)
                    if (vehicles[i] != null) return vehicles[i];
                return null;
            }
        }

        /// <summary>The vehicle with this id, or null.</summary>
        public VehicleDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < vehicles.Count; i++)
                if (vehicles[i] != null && vehicles[i].id == id) return vehicles[i];
            return null;
        }
    }
}

using System;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// What this player has chosen, remembered between scenes and between sessions (PlayerPrefs).
    /// The menu scene writes it, the race scene reads it. Later it will also hold the chosen modifier ids.
    /// Only ids are stored, so it keeps working when assets are renamed or moved.
    /// </summary>
    public static class PlayerProfile
    {
        const string VehicleKey = "VehicleSystem.SelectedVehicle";
        const string MapKey = "VehicleSystem.SelectedMap";

        /// <summary>Raised after any value changes.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Changed = null;

        public static string SelectedVehicleId
        {
            get => PlayerPrefs.GetString(VehicleKey, "");
            set { Save(VehicleKey, value); }
        }

        public static string SelectedMapId
        {
            get => PlayerPrefs.GetString(MapKey, "");
            set { Save(MapKey, value); }
        }

        /// <summary>The chosen vehicle, or the catalog's first one if nothing valid is saved yet.</summary>
        public static VehicleDefinition GetSelectedVehicle(VehicleCatalog catalog)
        {
            if (catalog == null) return null;
            var chosen = catalog.Get(SelectedVehicleId);
            return chosen != null ? chosen : catalog.First;
        }

        static void Save(string key, string value)
        {
            PlayerPrefs.SetString(key, value ?? "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}

using System;
using System.Collections.Generic;

namespace VehicleSystem
{
    /// <summary>Who controls a slot's car.</summary>
    public enum SlotController
    {
        LocalHuman,  // this device's player
        RemoteHuman, // another phone (local network or online)
        AI           // bot
    }

    /// <summary>One racer in a match. Plain data, so it can be built by any lobby and sent over any transport.</summary>
    [Serializable]
    public class PlayerSlot
    {
        public int slotId;
        public string displayName;
        public string vehicleId;                              // VehicleDefinition.id
        public List<string> modifierIds = new List<string>(); // StatModifierDef.id
        public SlotController controller = SlotController.LocalHuman;
        public ulong networkClientId;                         // RemoteHuman only
    }

    /// <summary>
    /// Everything needed to start a race: the map, the laps and who is racing.
    /// Offline, local network and online lobbies all end by building one of these, so the spawner and race
    /// logic never need to know how the match was created.
    /// </summary>
    [Serializable]
    public class RaceConfig
    {
        public string mapId;
        public int laps = 3;
        public List<PlayerSlot> slots = new List<PlayerSlot>();
    }
}

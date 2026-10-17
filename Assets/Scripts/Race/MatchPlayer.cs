using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>One racer in the current match: who, their vehicle and their score. MatchManager keeps one per vehicle.</summary>
    public class MatchPlayer
    {
        public int id;
        public string name;
        public VehicleController vehicle;
        /// <summary>True for the vehicle this device's player drives.</summary>
        public bool isLocal;

        // ---- score and statistics
        public int points;
        public float distance;
        public int npcBumps;
        public int rivalRams;
        public int overtakes;
        public int timesDestroyed;
        public int maxChain;

        // ---- used by MatchManager
        internal VehicleImpactSensor sensor;
        internal VehicleHealth health;
        internal VehicleDriver driver;
        internal System.Action<ImpactInfo> impactHandler;
        internal System.Action<ImpactInfo> destroyedHandler;
        internal float distanceCarry, previousSpeed, s;
        internal bool hasS, hasLastPosition;
        internal Vector3 lastPosition;
        internal int chain;
        internal float lastChainTime = -999f;

        internal void ResetScore()
        {
            points = 0;
            distance = distanceCarry = 0f;
            npcBumps = rivalRams = overtakes = timesDestroyed = maxChain = chain = 0;
            lastChainTime = -999f;
            hasLastPosition = false;
        }
    }
}

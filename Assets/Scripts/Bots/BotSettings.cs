using System;
using UnityEngine;

namespace VehicleSystem
{
    public enum BotDifficulty { Easy, Normal, Hard, Custom }

    /// <summary>
    /// How a bot drives. Every number is editable: pick Easy / Normal / Hard on the VehicleSpawner for a ready set, or choose Custom
    /// and set your own. Each bot gets a little random variation of its speed and aggression, so a group of bots is not identical.
    /// </summary>
    [Serializable]
    public class BotSettings
    {
        [Header("Speed (share of the vehicle's top speed)")]
        [Range(0.3f, 1f)] public float minSpeedFraction = 0.7f;
        [Range(0.3f, 1f)] public float maxSpeedFraction = 0.85f;
        [Tooltip("How hard the bot pushes the throttle for a speed difference.")]
        public float throttleGain = 0.6f;

        [Header("Steering")]
        [Tooltip("How far ahead on the road the bot aims (meters), plus the extra per m/s of speed.")]
        public float lookAhead = 6f;
        public float lookAheadPerSpeed = 0.5f;
        [Tooltip("Angle (degrees) to the target that gives full steering.")]
        public float steerFullAngle = 25f;
        [Tooltip("How quickly the steering follows (higher = sharper).")]
        public float steerResponse = 8f;
        [Tooltip("How fast the bot slides sideways when changing lane (m/s).")]
        public float laneChangeSpeed = 3f;

        [Header("Traffic")]
        [Tooltip("How far ahead the bot looks for slower vehicles: this + speed x the next value (meters).")]
        public float scanDistance = 12f;
        public float scanTimeAhead = 1.2f;
        [Tooltip("An obstacle closer than this + speed x 0.6 makes the bot look for another lane.")]
        public float laneChangeDistance = 14f;
        [Tooltip("The other lane must be this much freer (meters) before the bot changes.")]
        public float laneChangeMargin = 6f;
        public float decisionInterval = 0.25f;
        [Tooltip("Gap (meters) the bot keeps behind a vehicle it cannot pass.")]
        public float followGap = 6f;
        public float followGain = 0.8f;
        [Tooltip("Closer than this and the bot brakes hard (meters).")]
        public float emergencyGap = 3.5f;

        [Header("Aggression: bumping NPC traffic from the side for points")]
        [Range(0f, 1f)] public float aggression = 0.35f;
        [Tooltip("Seconds a bump attempt can last.")]
        public float bumpDuration = 1.5f;
        [Tooltip("Seconds between two bump attempts.")]
        public float bumpCooldown = 3f;
        [Tooltip("Distance (meters) between the centres of the two vehicles the bot steers for: small = a real hit.")]
        public float bumpOverlap = 1.5f;
        [Tooltip("How fast the bot steers sideways during a bump (m/s).")]
        public float bumpLateralSpeed = 5f;

        public static BotSettings Preset(BotDifficulty difficulty)
        {
            var s = new BotSettings();
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    s.minSpeedFraction = 0.5f; s.maxSpeedFraction = 0.68f;
                    s.aggression = 0.1f; s.laneChangeDistance = 18f; s.steerResponse = 6f;
                    break;
                case BotDifficulty.Hard:
                    s.minSpeedFraction = 0.85f; s.maxSpeedFraction = 0.97f;
                    s.aggression = 0.65f; s.laneChangeDistance = 11f; s.steerResponse = 10f; s.followGap = 4.5f;
                    break;
            }
            return s;
        }
    }
}

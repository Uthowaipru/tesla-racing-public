using System;

namespace VehicleSystem
{
    /// <summary>
    /// One frame of driving intent. This is the ONLY thing that tells a vehicle what to do, no matter
    /// who produces it: the local player (LocalPlayerInput), an AI bot, a replay, or the network.
    /// </summary>
    [Serializable]
    public struct DriveInput
    {
        /// <summary>-1..1. Opposite to the current motion = brake, then reverse.</summary>
        public float throttle;
        /// <summary>-1..1. Negative = left.</summary>
        public float steer;
        public bool handbrake;

        public DriveInput(float throttle, float steer, bool handbrake)
        {
            this.throttle = throttle;
            this.steer = steer;
            this.handbrake = handbrake;
        }

        public static DriveInput Neutral => default;
    }

    /// <summary>
    /// Anything that can drive a vehicle. VehicleDriver asks its source once per frame.
    /// Implement this for AI, replays or network input.
    /// </summary>
    public interface IDriveInputSource
    {
        DriveInput Read();
    }
}

using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Sits on every vehicle (player, bot or remote). Each frame it asks its IDriveInputSource for a
    /// DriveInput and hands it to the VehicleController. It also lets VehicleRecovery take over while it
    /// is backing the car out of a stuck spot.
    ///
    /// The vehicle itself never knows who is driving, so the same prefab works for the local player,
    /// AI bots and network-controlled cars. LocalPlayerRig adds this component and sets the source for the
    /// local player's car; a spawner does the same with an AI or network source for the others.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleController))]
    public class VehicleDriver : MonoBehaviour
    {
        [Tooltip("Off = the car gets neutral input (countdown, finished cars, paused menus).")]
        public bool inputEnabled = true;

        VehicleController vehicle;
        VehicleRecovery recovery;
        IDriveInputSource source;

        /// <summary>Who is driving right now (null = nobody, the car holds still).</summary>
        public IDriveInputSource Source => source;

        /// <summary>The input that was sent to the vehicle this frame (after recovery overrides).</summary>
        public DriveInput LastInput { get; private set; }

        public void SetSource(IDriveInputSource newSource) => source = newSource;

        void Awake()
        {
            vehicle = GetComponent<VehicleController>();
            recovery = GetComponent<VehicleRecovery>();
        }

        void Update()
        {
            DriveInput input = (inputEnabled && source != null) ? source.Read() : DriveInput.Neutral;

            input.throttle = Mathf.Clamp(input.throttle, -1f, 1f);
            input.steer = Mathf.Clamp(input.steer, -1f, 1f);

            if (recovery != null)
            {
                recovery.DriveInput = input.throttle; // the driver's own intent, used by "stuck" detection

                // while backing out of a stuck spot the recovery script drives the car
                if (recovery.TryGetOverride(out float overrideThrottle, out float overrideSteer))
                {
                    input.throttle = overrideThrottle;
                    input.steer = overrideSteer;
                    input.handbrake = false;
                }
            }

            LastInput = input;
            vehicle.SetInput(input);
        }
    }
}

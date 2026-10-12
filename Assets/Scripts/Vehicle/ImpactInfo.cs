using UnityEngine;

namespace VehicleSystem
{
    /// <summary>Which part of THIS vehicle was hit, from the direction of the contact.</summary>
    public enum HitZone { Front, Rear, Side }

    /// <summary>How fast the impact was, as a share of this vehicle's own top speed.</summary>
    public enum SpeedTier
    {
        Cruising = 1,      // below "Fast From": no collision reward
        Fast = 2,          // normal reward
        FullThrottle = 3   // maximum reward
    }

    /// <summary>Everything the damage, effects and (later) scoring systems need to know about one impact.</summary>
    public struct ImpactInfo
    {
        /// <summary>Closing speed along the contact direction (m/s).</summary>
        public float impactSpeed;
        /// <summary>impactSpeed / this vehicle's top speed (0..1+).</summary>
        public float speedRatio;
        public SpeedTier tier;
        public HitZone zone;
        /// <summary>World position of the contact.</summary>
        public Vector3 point;
        /// <summary>World direction of the contact surface, pointing into this vehicle.</summary>
        public Vector3 normal;
        public Collider other;
        /// <summary>Null when the other object is static (wall, building).</summary>
        public Rigidbody otherBody;
        /// <summary>Set when the other object is another vehicle with a VehicleController (a rival).</summary>
        public VehicleController otherVehicle;
    }
}

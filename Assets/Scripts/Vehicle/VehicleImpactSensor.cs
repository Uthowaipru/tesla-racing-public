using System;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Turns Unity's collision callbacks into one clean event per impact: how hard (speed tier), which side of the
    /// vehicle (front / rear / side) and what was hit. VehicleHealth, VehicleEffects and later the scoring system all
    /// listen to it, so collisions are classified in exactly one place.
    ///
    /// Ground contact and very light touches are ignored. Works with compound colliders: the callback arrives on the
    /// object that holds the Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleController))]
    public class VehicleImpactSensor : MonoBehaviour
    {
        [Header("Filter")]
        [Tooltip("Impacts slower than this (m/s) are ignored: touching, scraping along a wall.")]
        public float minImpactSpeed = 2f;
        [Tooltip("A surface whose normal points up more than this is ground (or the roof of something), not a hit.")]
        [Range(0.3f, 1f)] public float groundNormalY = 0.7f;
        [Tooltip("Objects on these layers never count as hits (e.g. the road).")]
        public LayerMask ignoreLayers = 0;

        [Header("Hit zones")]
        [Tooltip("A contact within this angle of the vehicle's nose or tail counts as Front / Rear, everything else is Side.")]
        [Range(10f, 80f)] public float frontRearHalfAngle = 40f;

        [Header("Speed tiers (share of the vehicle's own top speed)")]
        [Range(0.1f, 0.9f)] public float fastFrom = 0.5f;
        [Range(0.2f, 1f)] public float fullThrottleFrom = 0.8f;

        /// <summary>Raised once per impact.</summary>
        public event Action<ImpactInfo> Impact;

        VehicleController vehicle;

        void Awake() => vehicle = GetComponent<VehicleController>();

        void OnCollisionEnter(Collision collision)
        {
            if (((1 << collision.gameObject.layer) & ignoreLayers.value) != 0) return;

            int count = collision.contactCount;
            if (count == 0) return;

            Vector3 relative = collision.relativeVelocity;

            // the strongest non-ground contact
            bool found = false;
            ContactPoint best = default;
            float bestSpeed = 0f;
            for (int i = 0; i < count; i++)
            {
                ContactPoint cp = collision.GetContact(i);
                if (cp.normal.y > groundNormalY) continue;

                float speed = Mathf.Abs(Vector3.Dot(relative, cp.normal));
                if (!found || speed > bestSpeed) { best = cp; bestSpeed = speed; found = true; }
            }

            if (!found || bestSpeed < minImpactSpeed) return;

            float topSpeed = vehicle.Stats != null ? Mathf.Max(1f, vehicle.Stats.Get(VehicleStat.MaxSpeed) / 3.6f) : 15f;
            float ratio = bestSpeed / topSpeed;

            var info = new ImpactInfo
            {
                impactSpeed = bestSpeed,
                speedRatio = ratio,
                tier = ratio >= fullThrottleFrom ? SpeedTier.FullThrottle : ratio >= fastFrom ? SpeedTier.Fast : SpeedTier.Cruising,
                zone = ZoneOf(best.normal),
                point = best.point,
                normal = best.normal,
                other = collision.collider,
                otherBody = collision.rigidbody,
                otherVehicle = collision.collider != null ? collision.collider.GetComponentInParent<VehicleController>() : null
            };

            Impact?.Invoke(info);
        }

        HitZone ZoneOf(Vector3 worldNormal)
        {
            // the normal points from the other object into us: an object in front gives a normal pointing backward
            Vector3 local = transform.InverseTransformDirection(worldNormal);
            Vector2 flat = new Vector2(local.x, local.z);
            if (flat.sqrMagnitude < 0.0001f) return HitZone.Side;
            flat.Normalize();

            float limit = Mathf.Cos(frontRearHalfAngle * Mathf.Deg2Rad);
            if (-flat.y >= limit) return HitZone.Front;
            if (flat.y >= limit) return HitZone.Rear;
            return HitZone.Side;
        }
    }
}

using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// The void below the road. A racer (player or bot) that falls below the kill height is destroyed and respawns on the road
    /// like after any other destruction (the match gives the usual penalty). RoadGenerator adds this automatically.
    ///
    /// The kill height is "Depth" meters below the road (the height of the RoadGenerator object), or a fixed height in the world if
    /// your road is not flat.
    /// </summary>
    public class VoidKillZone : MonoBehaviour
    {
        public RoadGenerator road;
        [Tooltip("Meters below the road where the void kills.")]
        public float depth = 20f;
        [Tooltip("Use a fixed height in the world instead (for a road that is not flat).")]
        public bool useFixedHeight = false;
        public float fixedHeight = -30f;
        [Tooltip("Seconds between checks.")]
        public float checkInterval = 0.2f;

        float timer;

        /// <summary>The world height below which a vehicle is destroyed.</summary>
        public float KillHeight => useFixedHeight ? fixedHeight : (road != null ? road.transform.position.y : transform.position.y) - depth;

        void OnEnable()
        {
            if (road == null) road = GetComponent<RoadGenerator>();
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = checkInterval;

            float limit = KillHeight;
            var vehicles = VehicleController.All;
            for (int i = 0; i < vehicles.Count; i++)
            {
                VehicleController v = vehicles[i];
                if (v == null || v.transform.position.y >= limit) continue;
                if (v.TryGetComponent(out VehicleHealth health)) health.Kill();
            }
        }
    }
}

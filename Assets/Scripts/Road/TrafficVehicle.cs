using System;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// One NPC vehicle (car, CNG, bus, truck). It follows a lane of the road at a fixed speed. Everything in a lane drives
    /// at the same speed, so NPCs never run into each other. All traffic goes the same way as the player.
    ///
    /// It is a normal dynamic Rigidbody that steers itself along the road, so the player really pushes it. When a player's
    /// vehicle hits it hard enough it is KNOCKED: it stops driving, tumbles for a few seconds and then vanishes.
    ///
    /// Prefab setup: a Rigidbody, a collider and the model (the editor creates simple ones: Tools > Vehicle > Create Road
    /// Pieces). Heavier vehicles (bus, truck) just need a bigger Rigidbody mass. The origin should be at ground level, in the
    /// middle of the vehicle.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficVehicle : MonoBehaviour
    {
        public enum State { Driving, Knocked }

        [Header("Spawning")]
        [Tooltip("How likely this vehicle is chosen (0 = never).")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Lanes it may drive in, counted from the left (0). Max -1 = up to the last lane. Heavy vehicles: lane 0 only.")]
        public int minLane = 0;
        public int maxLane = -1;
        [Tooltip("Length in meters, so vehicles keep their distance.")]
        public float length = 4.5f;
        [Tooltip("Height of the model's origin above the road.")]
        public float groundOffset = 0.05f;

        [Header("Driving")]
        public float acceleration = 6f;
        [Tooltip("How fast it can turn to follow the road (degrees per second).")]
        public float turnRate = 120f;
        [Tooltip("Looks this far ahead along its lane (meters).")]
        public float lookAhead = 8f;
        [Tooltip("Lowers the centre of mass so it does not tip over when pushed (meters above the origin).")]
        public float centerOfMassHeight = 0.4f;

        [Header("Being hit")]
        [Tooltip("A player's vehicle hitting it faster than this (m/s) knocks it.")]
        public float knockSpeed = 3f;
        [Tooltip("Seconds it tumbles before it vanishes.")]
        public float knockLifetime = 4f;
        [Tooltip("Seconds it takes to shrink away at the end.")]
        public float shrinkTime = 0.4f;

        /// <summary>The vehicle was knocked by a player (scoring and effects can listen).</summary>
        public event Action<TrafficVehicle> Knocked;

        public State CurrentState { get; private set; }
        /// <summary>Distance along the road.</summary>
        public float S { get; private set; }
        public bool Expired { get; private set; }
        public bool RewardClaimed { get; private set; }
        [HideInInspector] public TrafficVehicle sourcePrefab;

        Rigidbody rb;
        RoadPath path;
        float laneOffset, speed, knockTimer;
        Vector3 baseScale;

        /// <summary>Scoring calls this: true only the first time, so one NPC rewards once.</summary>
        public bool TryClaimReward()
        {
            if (RewardClaimed) return false;
            RewardClaimed = true;
            return true;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            baseScale = transform.localScale;
            rb.centerOfMass = Vector3.up * centerOfMassHeight;
        }

        /// <summary>Puts the vehicle on the road at distance s in a lane and starts it driving.</summary>
        public void Begin(RoadPath roadPath, float s, float offset, float laneSpeed)
        {
            path = roadPath;
            S = s;
            laneOffset = offset;
            speed = laneSpeed;

            CurrentState = State.Driving;
            Expired = false;
            RewardClaimed = false;
            knockTimer = 0f;
            transform.localScale = baseScale;

            Vector3 tangent = path.Tangent(s);
            Vector3 position = path.Evaluate(s) + path.Right(s) * offset + Vector3.up * groundOffset;
            Quaternion rotation = Quaternion.LookRotation(tangent, Vector3.up);

            gameObject.SetActive(true);
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            transform.SetPositionAndRotation(position, rotation);
            rb.position = position;
            rb.rotation = rotation;
            SetVelocity(tangent * speed);
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 Velocity()
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        void SetVelocity(Vector3 value)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = value;
#else
            rb.velocity = value;
#endif
        }

        void FixedUpdate()
        {
            if (path == null) return;
            float dt = Time.fixedDeltaTime;

            if (CurrentState == State.Knocked)
            {
                knockTimer += dt;
                if (knockTimer >= knockLifetime) Expired = true;
                return;
            }

            // where am I on the road, and where do I aim?
            S = path.ProjectS(rb.position, S);
            float aheadS = Mathf.Min(S + lookAhead, path.EndS);
            Vector3 target = path.Evaluate(aheadS) + path.Right(aheadS) * laneOffset;

            Vector3 toTarget = Vector3.ProjectOnPlane(target - rb.position, Vector3.up);
            Vector3 direction = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : path.Tangent(S);

            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(direction, Vector3.up), turnRate * dt));

            Vector3 velocity = Velocity();
            Vector3 flat = Vector3.ProjectOnPlane(velocity, Vector3.up);
            flat = Vector3.MoveTowards(flat, direction * speed, acceleration * dt);
            SetVelocity(flat + Vector3.up * velocity.y);
        }

        void Update()
        {
            // shrink away at the end of a knock
            if (CurrentState != State.Knocked) return;

            float left = knockLifetime - knockTimer;
            if (left < shrinkTime) transform.localScale = baseScale * Mathf.Clamp01(left / Mathf.Max(0.01f, shrinkTime));
        }

        void OnCollisionEnter(Collision collision)
        {
            if (CurrentState != State.Driving) return;

            // only a player's vehicle knocks it (other NPCs and walls do not)
            Rigidbody other = collision.rigidbody;
            if (other == null || !other.TryGetComponent(out VehicleController _)) return;
            if (collision.relativeVelocity.magnitude < knockSpeed) return;

            CurrentState = State.Knocked;
            knockTimer = 0f;
            rb.constraints = RigidbodyConstraints.None; // free to tumble
            Knocked?.Invoke(this);
        }
    }
}

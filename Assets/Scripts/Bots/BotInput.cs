using System;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// An AI driver. It is just another input source, like the player's keyboard: it produces a DriveInput that the vehicle's
    /// VehicleDriver feeds to the vehicle, so a bot uses the same vehicle prefab, the same physics, health, recovery and respawn.
    ///
    /// On the road it:
    ///   - follows a lane at a share of its vehicle's top speed (each bot a little different),
    ///   - looks ahead for slower NPC traffic and other racers, changes to a freer lane, or follows at a gap,
    ///   - sometimes steers into an NPC beside it to score a side bump (the "aggression" setting).
    /// It keeps to the normal road (never uses oncoming lanes). Stuck vehicles are handled by VehicleRecovery as for players.
    ///
    /// VehicleSpawner adds it for AI slots. Give it settings with Configure().
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleController))]
    public class BotInput : MonoBehaviour, IDriveInputSource
    {
        public BotSettings settings = new BotSettings();
        [Tooltip("Length of the vehicle (meters), used for gaps.")]
        public float vehicleLength = 3.2f;

        RoadGenerator road;
        TrafficManager traffic;
        VehicleController vehicle;
        System.Random rng = new System.Random(1);

        DriveInput current;
        float speedFraction = 0.8f, aggression = 0.3f;
        float s, lateralTarget, steerSmoothed, decisionTimer, bumpTimer, bumpCooldownTimer;
        bool hasS, laneChosen;
        int targetLane;
        TrafficVehicle bumpTarget, lastBumpTarget;

        struct Obstacle { public bool exists; public float gap; public float speed; }

        public DriveInput Read() => current;

        /// <summary>Sets the road, traffic and settings, and rolls this bot's own speed and aggression.</summary>
        public void Configure(BotSettings botSettings, RoadGenerator roadGenerator, TrafficManager trafficManager, int seed)
        {
            settings = botSettings ?? new BotSettings();
            road = roadGenerator;
            traffic = trafficManager;
            rng = new System.Random(seed);

            speedFraction = Mathf.Lerp(settings.minSpeedFraction, settings.maxSpeedFraction, (float)rng.NextDouble());
            aggression = Mathf.Clamp01(settings.aggression * (0.7f + 0.6f * (float)rng.NextDouble()));
        }

        void Awake() => vehicle = GetComponent<VehicleController>();

        void FixedUpdate()
        {
            if (road == null || !road.IsStarted || road.Path.Count < 2)
            {
                current = DriveInput.Neutral;
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector3 position = transform.position;

            float lateral = road.LateralOffset(position, hasS ? s : -1f, out s);
            hasS = true;

            float speed = Mathf.Max(0f, vehicle.ForwardSpeed);
            float top = vehicle.Stats != null ? vehicle.Stats.Get(VehicleStat.MaxSpeed) / 3.6f : 15f;
            int lanes = Mathf.Max(1, road.ForwardLaneCount);

            if (!laneChosen)
            {
                targetLane = NearestLane(lateral, lanes);
                lateralTarget = road.LaneOffset(targetLane);
                laneChosen = true;
            }

            // ---- decide about lanes a few times per second
            decisionTimer -= dt;
            if (decisionTimer <= 0f)
            {
                decisionTimer = settings.decisionInterval;
                ChooseLane(speed, lanes);
            }

            bumpCooldownTimer -= dt;
            bumpTimer -= dt;
            if (bumpTarget == null && bumpCooldownTimer <= 0f && aggression > 0f && speed > top * 0.55f) TryStartBump(speed, lateral);

            // ---- where to be sideways
            float laneOffset = road.LaneOffset(targetLane);
            float lateralSpeed = settings.laneChangeSpeed;
            float desired = laneOffset;

            if (bumpTarget != null)
            {
                float ds = bumpTarget.S - s;
                if (bumpTimer <= 0f || ds < -7f || !bumpTarget.gameObject.activeInHierarchy || bumpTarget.CurrentState != TrafficVehicle.State.Driving)
                {
                    bumpTarget = null; // passed it (or hit it, or gave up)
                    bumpCooldownTimer = settings.bumpCooldown;
                }
                else if (ds < 8f)
                {
                    // lean into its side: stop short of its lane centre by the overlap distance
                    float side = lateral < bumpTarget.LaneOffset ? -1f : 1f;
                    desired = bumpTarget.LaneOffset + side * settings.bumpOverlap;
                    lateralSpeed = settings.bumpLateralSpeed;
                }
            }
            lateralTarget = Mathf.MoveTowards(lateralTarget, desired, lateralSpeed * dt);

            // ---- speed: cruise, or follow what is in the way
            Obstacle ahead = Scan(laneOffset, settings.scanDistance + speed * settings.scanTimeAhead, lateral);
            float desiredSpeed = top * speedFraction;
            if (ahead.exists && bumpTarget == null && ahead.gap < settings.followGap + speed * 0.5f)
                desiredSpeed = Mathf.Min(desiredSpeed, ahead.speed + (ahead.gap - settings.followGap) * settings.followGain);

            float throttle = Mathf.Clamp((desiredSpeed - speed) * settings.throttleGain, -1f, 1f);
            if (speed < 1f && throttle < 0f) throttle = 0f; // a bot never reverses
            if (ahead.exists && bumpTarget == null && ahead.gap < settings.emergencyGap && speed > ahead.speed + 1f) throttle = -1f;

            // ---- steering toward the point ahead on the road, at the wanted sideways position
            float lookAhead = settings.lookAhead + speed * settings.lookAheadPerSpeed;
            float aheadS = Mathf.Min(s + lookAhead, road.Path.EndS);
            Vector3 target = road.Path.Evaluate(aheadS) + road.Path.Right(aheadS) * lateralTarget;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Vector3 toTarget = Vector3.ProjectOnPlane(target - position, Vector3.up);
            float angle = toTarget.sqrMagnitude > 0.01f && forward.sqrMagnitude > 0.01f ? Vector3.SignedAngle(forward, toTarget, Vector3.up) : 0f;

            float steer = Mathf.Clamp(angle / Mathf.Max(1f, settings.steerFullAngle), -1f, 1f);
            steerSmoothed = Mathf.Lerp(steerSmoothed, steer, 1f - Mathf.Exp(-settings.steerResponse * dt));

            current = new DriveInput(throttle, steerSmoothed, false);
        }

        // ------------------------------------------------------------------ lanes

        int NearestLane(float lateral, int lanes)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < lanes; i++)
            {
                float d = Mathf.Abs(road.LaneOffset(i) - lateral);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        void ChooseLane(float speed, int lanes)
        {
            float range = settings.scanDistance + speed * settings.scanTimeAhead;
            float lateralNow = road.LaneOffset(targetLane);

            Obstacle here = Scan(lateralNow, range, lateralNow);
            if (!here.exists || here.gap > settings.laneChangeDistance + speed * 0.6f) return; // free enough

            int best = targetLane;
            float bestFree = here.gap;
            for (int step = -1; step <= 1; step += 2)
            {
                int lane = targetLane + step;
                if (lane < 0 || lane >= lanes) continue;

                float offset = road.LaneOffset(lane);
                if (SideBlocked(offset)) continue; // someone is right beside us in that lane

                Obstacle o = Scan(offset, range, offset);
                float free = o.exists ? o.gap : range + 100f;
                if (free > bestFree + settings.laneChangeMargin) { best = lane; bestFree = free; }
            }
            targetLane = best;
        }

        // ------------------------------------------------------------------ looking at what is on the road

        /// <summary>The nearest vehicle ahead in the lane at "laneOffset" (NPC traffic and other racers).</summary>
        Obstacle Scan(float laneOffset, float range, float ownLateral)
        {
            var result = new Obstacle { exists = false, gap = range, speed = 0f };
            float occupancy = road.LaneWidth * 0.75f;

            if (traffic != null)
            {
                var list = traffic.ActiveVehicles;
                for (int i = 0; i < list.Count; i++)
                {
                    TrafficVehicle n = list[i];
                    if (n == null || n.Direction < 0 || n.CurrentState != TrafficVehicle.State.Driving) continue;
                    if (Mathf.Abs(n.LaneOffset - laneOffset) > occupancy) continue;

                    float gap = n.S - s - (n.length + vehicleLength) * 0.5f;
                    if (gap < -vehicleLength || gap >= result.gap) continue;

                    result.exists = true;
                    result.gap = Mathf.Max(0f, gap);
                    result.speed = n.Speed;
                }
            }

            var racers = VehicleController.All;
            for (int i = 0; i < racers.Count; i++)
            {
                VehicleController other = racers[i];
                if (other == null || other == vehicle) continue;

                float otherLateral = road.LateralOffset(other.transform.position, s, out float otherS);
                if (Mathf.Abs(otherLateral - laneOffset) > occupancy) continue;

                float gap = otherS - s - vehicleLength;
                if (gap < -vehicleLength || gap >= result.gap) continue;

                result.exists = true;
                result.gap = Mathf.Max(0f, gap);
                result.speed = Mathf.Max(0f, other.ForwardSpeed);
            }

            return result;
        }

        /// <summary>Is a vehicle beside us (or just behind / ahead) in the lane at "laneOffset", so a lane change would hit it?</summary>
        bool SideBlocked(float laneOffset)
        {
            float occupancy = road.LaneWidth * 0.75f;
            float window = vehicleLength + 3f;

            if (traffic != null)
            {
                var list = traffic.ActiveVehicles;
                for (int i = 0; i < list.Count; i++)
                {
                    TrafficVehicle n = list[i];
                    if (n == null || n.Direction < 0) continue;
                    if (Mathf.Abs(n.LaneOffset - laneOffset) <= occupancy && Mathf.Abs(n.S - s) < window + n.length * 0.5f) return true;
                }
            }

            var racers = VehicleController.All;
            for (int i = 0; i < racers.Count; i++)
            {
                VehicleController other = racers[i];
                if (other == null || other == vehicle) continue;

                float otherLateral = road.LateralOffset(other.transform.position, s, out float otherS);
                if (Mathf.Abs(otherLateral - laneOffset) <= occupancy && Mathf.Abs(otherS - s) < window) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ bumping NPCs for points

        /// <summary>Picks a slower NPC in the next lane that is about to be passed, and (by chance) decides to lean into it.</summary>
        void TryStartBump(float speed, float lateral)
        {
            if (traffic == null) return;

            var list = traffic.ActiveVehicles;
            for (int i = 0; i < list.Count; i++)
            {
                TrafficVehicle n = list[i];
                if (n == null || n.Direction < 0 || n == lastBumpTarget || n.RewardClaimed) continue;
                if (n.CurrentState != TrafficVehicle.State.Driving) continue;

                float ds = n.S - s;
                if (ds < 4f || ds > 16f || speed < n.Speed + 2f) continue;

                float lateralGap = Mathf.Abs(n.LaneOffset - lateral);
                if (lateralGap < road.LaneWidth * 0.6f || lateralGap > road.LaneWidth * 1.6f) continue; // the lane right beside us

                lastBumpTarget = n; // decide once per NPC
                if (rng.NextDouble() > aggression) continue;

                bumpTarget = n;
                bumpTimer = settings.bumpDuration;
                return;
            }
        }
    }
}

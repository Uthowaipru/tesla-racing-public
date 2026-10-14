using System;
using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Watches which road the local player is on. On a two-way road the player starts on the normal (forward) road; being on the
    /// ONCOMING road is "wrong way": risky, so it pays more. Crossing from one road to the other (only possible at a crossover
    /// piece, where the median has an opening) can give a bonus.
    ///
    /// This only tracks and announces; it holds no score. The match score reads:
    ///   ScoreMultiplier       (multiply the distance points by it while driving wrong way)
    ///   CrossedToOncoming / CrossedToForward  (events with the bonus points to give: 0 = none this time)
    /// Every number is a field on this component: change it per scene.
    ///
    /// It does nothing on a one-way road.
    /// </summary>
    public class WrongWayTracker : MonoBehaviour
    {
        public RoadGenerator road;

        [Header("Wrong way (driving on the oncoming road)")]
        [Tooltip("Distance points are worth this many times more while driving on the oncoming road.")]
        public float wrongWayDistanceMultiplier = 2f;

        [Header("Crossing bonus (points)")]
        [Tooltip("Bonus for switching from the normal road to the oncoming road.")]
        public int crossToOncomingBonus = 50;
        [Tooltip("Bonus for switching back to the normal road.")]
        public int crossBackBonus = 0;
        [Tooltip("A crossing within this many seconds of the one before gives no bonus (stops farming by switching back and forth).")]
        public float crossingCooldown = 6f;

        [Header("Detection")]
        [Tooltip("The vehicle has to be this far (meters) beyond the median before the side changes, so edges do not flicker.")]
        public float switchMargin = 0.75f;
        [Tooltip("A jump of more than this in one frame (a respawn or recovery teleport) changes the side without any bonus.")]
        public float teleportDistance = 15f;

        public RoadSide Side { get; private set; } = RoadSide.Forward;
        public bool IsWrongWay => Side == RoadSide.Oncoming;
        /// <summary>1 on the normal road, the wrong-way multiplier on the oncoming road.</summary>
        public float ScoreMultiplier => IsWrongWay ? wrongWayDistanceMultiplier : 1f;
        /// <summary>Meters driven on the oncoming road since the last ResetStats().</summary>
        public float WrongWayDistance { get; private set; }
        public int CrossingCount { get; private set; }

        /// <summary>true = now driving wrong way, false = back on the normal road.</summary>
        public event Action<bool> WrongWayChanged;
        /// <summary>The player switched to the oncoming road. The number is the bonus to give (0 = none).</summary>
        public event Action<int> CrossedToOncoming;
        public event Action<int> CrossedToForward;

        float sHint;
        float lastCrossingTime = -999f;
        Vector3 lastPosition;
        bool hasLastPosition;

        /// <summary>Start of a match: forget distances, crossings and the side.</summary>
        public void ResetStats()
        {
            WrongWayDistance = 0f;
            CrossingCount = 0;
            lastCrossingTime = -999f;
            hasLastPosition = false;
            if (Side != RoadSide.Forward) SetSide(RoadSide.Forward, 0, silent: true);
        }

        void Update()
        {
            if (road == null || !road.IsStarted || road.target == null) return;

            // nothing to track on a one-way road
            if (road.OncomingLaneCount <= 0)
            {
                if (Side != RoadSide.Forward) SetSide(RoadSide.Forward, 0, silent: true);
                hasLastPosition = false;
                return;
            }

            Vector3 position = road.target.position;
            bool teleported = hasLastPosition && (position - lastPosition).magnitude > teleportDistance;
            float step = hasLastPosition && !teleported ? Vector3.ProjectOnPlane(position - lastPosition, Vector3.up).magnitude : 0f;

            float lateral = road.LateralOffset(position, teleported ? -1f : sHint, out sHint);
            float limit = road.MedianWidth * 0.5f + switchMargin;

            // hysteresis: only change side once clearly beyond the median
            RoadSide wanted = Side;
            if (teleported) wanted = lateral > 0f ? RoadSide.Oncoming : RoadSide.Forward;
            else if (Side == RoadSide.Forward && lateral > limit) wanted = RoadSide.Oncoming;
            else if (Side == RoadSide.Oncoming && lateral < -limit) wanted = RoadSide.Forward;

            if (wanted != Side)
            {
                int bonus = 0;
                if (!teleported && Time.time - lastCrossingTime >= crossingCooldown)
                    bonus = wanted == RoadSide.Oncoming ? crossToOncomingBonus : crossBackBonus;

                SetSide(wanted, bonus, silent: false, teleported: teleported);
            }

            if (IsWrongWay) WrongWayDistance += step;

            lastPosition = position;
            hasLastPosition = true;
        }

        void SetSide(RoadSide side, int bonus, bool silent, bool teleported = false)
        {
            Side = side;
            if (silent) return;

            WrongWayChanged?.Invoke(IsWrongWay);

            if (teleported) return; // a respawn is not a crossing

            CrossingCount++;
            lastCrossingTime = Time.time;
            if (side == RoadSide.Oncoming) CrossedToOncoming?.Invoke(bonus);
            else CrossedToForward?.Invoke(bonus);
        }
    }
}

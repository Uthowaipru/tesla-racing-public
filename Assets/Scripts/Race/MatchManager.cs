using System;
using System.Collections.Generic;
using UnityEngine;

namespace VehicleSystem
{
    public enum MatchState { Waiting, Countdown, Running, Finished }

    public enum ScoreReason { Distance, NpcBump, Chain, RivalRam, Overtake, Crossing, Destroyed, WrongWay }

    /// <summary>What the match gave the local player. The profile (coins, cash) will read this.</summary>
    public struct MatchResult
    {
        public int points;
        public int rank;
        public int playerCount;
        public float distance;
        public float wrongWayDistance;
        public int npcBumps;
        public int rivalRams;
        public int overtakes;
        public int timesDestroyed;
        public int maxChain;
        public int coins;
        public int cash;
    }

    /// <summary>
    /// Runs one match: the countdown, the timer and the score. There is no finish line: the match ends when the time is up and
    /// the player with the most points wins.
    ///
    /// Points (all values are fields below, so every scene can differ):
    ///   - every 100 m driven: +10 (times the wrong-way multiplier on a two-way road)
    ///   - side bump into NPC traffic at speed: +20 (fast) / +30 (full throttle); each NPC pays once; slow bumps pay nothing
    ///   - chain: several paid bumps within 4 seconds earn a bonus
    ///   - ramming a rival: +20 from behind, +25 from the side; overtaking a rival: +10
    ///   - crossing to the oncoming road: the bonus of the WrongWayTracker
    ///   - being destroyed: -50
    /// Speed tiers use the player's OWN speed compared with their vehicle's top speed (the thresholds of its VehicleImpactSensor).
    ///
    /// Vehicles join with RegisterPlayer() (VehicleSpawner does it). If nobody has joined, the vehicle of the LocalPlayerRig does.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class MatchManager : MonoBehaviour
    {
        public enum MatchLength { Minutes3, Minutes6, Minutes10, Minutes12, Custom }

        [Header("Match")]
        public MatchLength length = MatchLength.Minutes3;
        [Tooltip("Used when Length is Custom.")]
        public float customSeconds = 120f;
        public float countdownSeconds = 3f;
        [Tooltip("Start the countdown when the scene starts. Off = call Begin() yourself (for example when everybody in a room is ready).")]
        public bool autoStart = true;

        [Header("Distance")]
        public float metersPerReward = 100f;
        public int pointsPerReward = 10;
        [Tooltip("A jump bigger than this in one frame (a respawn) does not count as driven distance.")]
        public float teleportDistance = 15f;

        [Header("NPC side bumps")]
        public int fastBumpPoints = 20;
        public int fullThrottleBumpPoints = 30;

        [Header("Chain: paid bumps within the window")]
        public float chainWindow = 4f;
        public int chain2Bonus = 5;
        public int chain3Bonus = 10;
        public int chain4Bonus = 20;
        [Tooltip("From the 5th bump in a chain, EVERY further bump adds this.")]
        public int chain5PlusBonus = 30;

        [Header("Rivals (other players)")]
        public int rearRamPoints = 20;
        public int sideRamPoints = 25;
        public int overtakePoints = 10;
        [Tooltip("The same rival pays a ram only once in this many seconds (stops farming).")]
        public float ramCooldown = 6f;
        [Tooltip("The same rival pays an overtake only once in this many seconds.")]
        public float overtakeCooldown = 8f;
        [Tooltip("Meters ahead or behind a rival you must be before the order counts as changed.")]
        public float overtakeMargin = 2f;
        [Range(10f, 80f)] public float rammedAreaHalfAngle = 40f;

        [Header("Penalty")]
        public int destroyedPenalty = 50;
        public bool allowNegativeScore = false;

        [Header("After the match (shown on the results screen)")]
        public int pointsPerCoin = 10;
        public int pointsPerCash = 40;

        [Header("Sources (found in the scene if empty)")]
        public RoadGenerator road;
        public WrongWayTracker wrongWayTracker;

        public MatchState State { get; private set; } = MatchState.Waiting;
        /// <summary>Seconds of the match left (the full length before it starts).</summary>
        public float TimeLeft { get; private set; }
        public float CountdownLeft { get; private set; }
        public MatchPlayer LocalPlayer { get; private set; }
        public MatchResult Result { get; private set; }
        public IReadOnlyList<MatchPlayer> Players => players;

        public float DurationSeconds
        {
            get
            {
                switch (length)
                {
                    case MatchLength.Minutes3: return 180f;
                    case MatchLength.Minutes6: return 360f;
                    case MatchLength.Minutes10: return 600f;
                    case MatchLength.Minutes12: return 720f;
                    default: return Mathf.Max(10f, customSeconds);
                }
            }
        }

        public event Action<MatchState> StateChanged;
        /// <summary>A player got points (negative = lost): popups and effects listen.</summary>
        public event Action<MatchPlayer, int, ScoreReason> PointsAwarded;
        public event Action<MatchResult> Finished;

        readonly List<MatchPlayer> players = new List<MatchPlayer>();
        readonly Dictionary<VehicleController, MatchPlayer> byVehicle = new Dictionary<VehicleController, MatchPlayer>();
        readonly Dictionary<int, float> pairCooldown = new Dictionary<int, float>();   // ram and overtake cooldowns per pair
        readonly Dictionary<int, int> pairOrder = new Dictionary<int, int>();           // who is ahead of whom (-1 / +1)
        int nextId = 1;

        // ------------------------------------------------------------------ players

        /// <summary>Adds a vehicle to the match (call it once per vehicle, when it spawns).</summary>
        public MatchPlayer RegisterPlayer(VehicleController vehicle, string playerName, bool isLocal)
        {
            if (vehicle == null) return null;
            if (byVehicle.TryGetValue(vehicle, out var existing)) return existing;

            var player = new MatchPlayer { id = nextId++, name = playerName, vehicle = vehicle, isLocal = isLocal };
            vehicle.TryGetComponent(out player.sensor);
            vehicle.TryGetComponent(out player.health);
            vehicle.TryGetComponent(out player.driver);

            if (player.sensor != null)
            {
                player.impactHandler = info => OnImpact(player, info);
                player.sensor.Impact += player.impactHandler;
            }
            if (player.health != null)
            {
                player.destroyedHandler = info => OnDestroyed(player);
                player.health.Destroyed += player.destroyedHandler;
            }

            players.Add(player);
            byVehicle[vehicle] = player;
            if (isLocal) LocalPlayer = player;

            SetInput(player, State == MatchState.Running);
            return player;
        }

        public void UnregisterPlayer(VehicleController vehicle)
        {
            if (vehicle == null || !byVehicle.TryGetValue(vehicle, out var player)) return;

            if (player.sensor != null && player.impactHandler != null) player.sensor.Impact -= player.impactHandler;
            if (player.health != null && player.destroyedHandler != null) player.health.Destroyed -= player.destroyedHandler;

            players.Remove(player);
            byVehicle.Remove(vehicle);
            if (LocalPlayer == player) LocalPlayer = null;
        }

        static void SetInput(MatchPlayer player, bool enabled)
        {
            if (player.driver != null) player.driver.inputEnabled = enabled;
        }

        /// <summary>1 = the most points.</summary>
        public int GetRank(MatchPlayer player)
        {
            int rank = 1;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != player && players[i].points > player.points) rank++;
            return rank;
        }

        // ------------------------------------------------------------------ flow

        void Start()
        {
            if (road == null) road = FindFirstObjectByType<RoadGenerator>();
            if (wrongWayTracker == null) wrongWayTracker = FindFirstObjectByType<WrongWayTracker>();

            // a vehicle placed in the scene instead of spawned
            if (players.Count == 0)
            {
                var rig = FindFirstObjectByType<LocalPlayerRig>();
                if (rig != null && rig.Vehicle != null) RegisterPlayer(rig.Vehicle, "You", true);
            }

            if (wrongWayTracker != null)
            {
                wrongWayTracker.CrossedToOncoming += OnCrossing;
                wrongWayTracker.CrossedToForward += OnCrossing;
            }

            TimeLeft = DurationSeconds;
            if (autoStart) Begin();
        }

        void OnDestroy()
        {
            if (wrongWayTracker != null)
            {
                wrongWayTracker.CrossedToOncoming -= OnCrossing;
                wrongWayTracker.CrossedToForward -= OnCrossing;
            }
            for (int i = players.Count - 1; i >= 0; i--) UnregisterPlayer(players[i].vehicle);
        }

        /// <summary>Resets everybody and starts the countdown.</summary>
        public void Begin()
        {
            TimeLeft = DurationSeconds;
            CountdownLeft = Mathf.Max(0f, countdownSeconds);
            pairCooldown.Clear();
            pairOrder.Clear();

            for (int i = 0; i < players.Count; i++)
            {
                MatchPlayer p = players[i];
                p.ResetScore();
                SetInput(p, false);

                if (p.vehicle.TryGetComponent(out VehicleBoost boost)) boost.ResetBatteries();
                if (p.vehicle.TryGetComponent(out VehicleRecovery recovery)) recovery.ResetManualUses();
                if (p.health != null) p.health.ResetHealth();
            }
            if (wrongWayTracker != null) wrongWayTracker.ResetStats();

            SetState(CountdownLeft > 0f ? MatchState.Countdown : MatchState.Running);
            if (State == MatchState.Running) StartRunning();
        }

        void StartRunning()
        {
            for (int i = 0; i < players.Count; i++) SetInput(players[i], true);
        }

        public void Finish()
        {
            if (State == MatchState.Finished) return;

            for (int i = 0; i < players.Count; i++) SetInput(players[i], false);
            SetState(MatchState.Finished);

            if (LocalPlayer != null)
            {
                Result = new MatchResult
                {
                    points = LocalPlayer.points,
                    rank = GetRank(LocalPlayer),
                    playerCount = players.Count,
                    distance = LocalPlayer.distance,
                    wrongWayDistance = wrongWayTracker != null ? wrongWayTracker.WrongWayDistance : 0f,
                    npcBumps = LocalPlayer.npcBumps,
                    rivalRams = LocalPlayer.rivalRams,
                    overtakes = LocalPlayer.overtakes,
                    timesDestroyed = LocalPlayer.timesDestroyed,
                    maxChain = LocalPlayer.maxChain,
                    coins = pointsPerCoin > 0 ? LocalPlayer.points / pointsPerCoin : 0,
                    cash = pointsPerCash > 0 ? LocalPlayer.points / pointsPerCash : 0
                };
                Finished?.Invoke(Result);
            }
        }

        void SetState(MatchState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (State == MatchState.Countdown)
            {
                CountdownLeft -= dt;
                if (CountdownLeft <= 0f)
                {
                    CountdownLeft = 0f;
                    SetState(MatchState.Running);
                    StartRunning();
                }
                return;
            }

            if (State == MatchState.Finished)
            {
                // a vehicle that respawns after the final whistle must not get its controls back
                for (int i = 0; i < players.Count; i++) SetInput(players[i], false);
                return;
            }

            if (State != MatchState.Running) return;

            TimeLeft -= dt;
            UpdatePlayers();
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                Finish();
            }
        }

        // ------------------------------------------------------------------ points

        public void AddPoints(MatchPlayer player, int points, ScoreReason reason)
        {
            if (player == null || points == 0) return;

            player.points += points;
            if (!allowNegativeScore && player.points < 0) player.points = 0;
            PointsAwarded?.Invoke(player, points, reason);
        }

        void UpdatePlayers()
        {
            float multiplier = wrongWayTracker != null ? wrongWayTracker.ScoreMultiplier : 1f;

            for (int i = 0; i < players.Count; i++)
            {
                MatchPlayer p = players[i];
                bool destroyed = p.health != null && p.health.IsDestroyed;

                Vector3 position = p.vehicle.transform.position;
                bool teleported = false;

                if (p.hasLastPosition)
                {
                    float step = Vector3.ProjectOnPlane(position - p.lastPosition, Vector3.up).magnitude;
                    teleported = step > teleportDistance;

                    if (!teleported && !destroyed && step > 0f)
                    {
                        p.distance += step;
                        p.distanceCarry += step;
                        while (p.distanceCarry >= metersPerReward)
                        {
                            p.distanceCarry -= metersPerReward;
                            float scale = p.isLocal ? multiplier : 1f; // wrong-way bonus: only tracked for this device's player
                            // distance driven on the oncoming road pays more and is labelled "WRONG WAY"
                            AddPoints(p, Mathf.RoundToInt(pointsPerReward * scale), scale > 1.01f ? ScoreReason.WrongWay : ScoreReason.Distance);
                        }
                    }
                }
                p.lastPosition = position;
                p.hasLastPosition = true;

                if (teleported) ForgetPairs(p);

                // where am I on the road? (for overtakes)
                if (road != null && road.IsStarted)
                {
                    p.s = road.Path.ProjectS(position, p.hasS && !teleported ? p.s : -1f);
                    p.hasS = true;
                }

                p.previousSpeed = Mathf.Abs(p.vehicle.ForwardSpeed);
            }

            if (road != null && road.IsStarted && players.Count > 1) CheckOvertakes();
        }

        void OnCrossing(int bonus)
        {
            if (State != MatchState.Running || LocalPlayer == null || bonus <= 0) return;
            AddPoints(LocalPlayer, bonus, ScoreReason.Crossing);
        }

        void OnDestroyed(MatchPlayer player)
        {
            if (State != MatchState.Running) return;

            player.timesDestroyed++;
            player.chain = 0;
            AddPoints(player, -destroyedPenalty, ScoreReason.Destroyed);
        }

        // ------------------------------------------------------------------ impacts

        void OnImpact(MatchPlayer player, ImpactInfo info)
        {
            if (State != MatchState.Running) return;

            // ---- NPC traffic: a side bump at speed pays once per vehicle
            TrafficVehicle npc = info.other != null ? info.other.GetComponentInParent<TrafficVehicle>() : null;
            if (npc != null)
            {
                if (info.zone != HitZone.Side) return;

                SpeedTier tier = OwnTier(player);
                int points = tier == SpeedTier.FullThrottle ? fullThrottleBumpPoints : tier == SpeedTier.Fast ? fastBumpPoints : 0;
                if (points <= 0 || !npc.TryClaimReward()) return;

                player.npcBumps++;
                AddPoints(player, points, ScoreReason.NpcBump);
                RegisterChain(player);
                return;
            }

            // ---- a rival: ram them from behind or from the side with your nose
            if (info.otherVehicle != null && byVehicle.TryGetValue(info.otherVehicle, out MatchPlayer rival) && rival != player)
            {
                if (info.zone != HitZone.Front) return;
                if (Vector3.Dot(player.vehicle.transform.forward, rival.vehicle.transform.forward) < 0.3f) return; // head-on: no reward
                if (OwnTier(player) == SpeedTier.Cruising) return;

                int key = PairKey(player, rival, 0);
                if (pairCooldown.TryGetValue(key, out float until) && Time.time < until) return;

                // the rival's own surface at the contact, in the rival's frame
                Vector3 n = rival.vehicle.transform.InverseTransformDirection(info.normal);
                float limit = Mathf.Cos(rammedAreaHalfAngle * Mathf.Deg2Rad);
                int points = 0;
                if (-n.z >= limit) points = rearRamPoints;
                else if (Mathf.Abs(n.z) < limit) points = sideRamPoints;
                if (points <= 0) return;

                pairCooldown[key] = Time.time + ramCooldown;
                player.rivalRams++;
                AddPoints(player, points, ScoreReason.RivalRam);
            }
        }

        SpeedTier OwnTier(MatchPlayer player)
        {
            float top = player.vehicle.Stats != null ? player.vehicle.Stats.Get(VehicleStat.MaxSpeed) / 3.6f : 15f;
            float speed = Mathf.Max(player.previousSpeed, Mathf.Abs(player.vehicle.ForwardSpeed)); // before the crash, and now
            float ratio = speed / Mathf.Max(1f, top);

            float fast = player.sensor != null ? player.sensor.fastFrom : 0.5f;
            float full = player.sensor != null ? player.sensor.fullThrottleFrom : 0.8f;
            return ratio >= full ? SpeedTier.FullThrottle : ratio >= fast ? SpeedTier.Fast : SpeedTier.Cruising;
        }

        void RegisterChain(MatchPlayer player)
        {
            float now = Time.time;
            player.chain = now - player.lastChainTime <= chainWindow ? player.chain + 1 : 1;
            player.lastChainTime = now;
            player.maxChain = Mathf.Max(player.maxChain, player.chain);

            int bonus = player.chain == 2 ? chain2Bonus : player.chain == 3 ? chain3Bonus : player.chain == 4 ? chain4Bonus : player.chain >= 5 ? chain5PlusBonus : 0;
            if (bonus > 0) AddPoints(player, bonus, ScoreReason.Chain);
        }

        // ------------------------------------------------------------------ overtakes

        static int PairKey(MatchPlayer a, MatchPlayer b, int kind) => (kind << 28) | (a.id << 14) | b.id;

        void CheckOvertakes()
        {
            for (int i = 0; i < players.Count; i++)
            {
                for (int j = 0; j < players.Count; j++)
                {
                    if (i == j) continue;
                    MatchPlayer a = players[i], b = players[j];
                    if (!a.hasS || !b.hasS) continue;
                    if ((a.health != null && a.health.IsDestroyed) || (b.health != null && b.health.IsDestroyed)) continue;

                    float diff = a.s - b.s; // positive = a is ahead
                    int key = PairKey(a, b, 1);
                    pairOrder.TryGetValue(key, out int order);

                    if (diff > overtakeMargin)
                    {
                        pairOrder[key] = 1;
                        if (order == -1) PayOvertake(a, b);
                    }
                    else if (diff < -overtakeMargin)
                    {
                        pairOrder[key] = -1;
                    }
                }
            }
        }

        void PayOvertake(MatchPlayer overtaker, MatchPlayer overtaken)
        {
            int key = PairKey(overtaker, overtaken, 2);
            if (pairCooldown.TryGetValue(key, out float until) && Time.time < until) return;

            pairCooldown[key] = Time.time + overtakeCooldown;
            overtaker.overtakes++;
            AddPoints(overtaker, overtakePoints, ScoreReason.Overtake);
        }

        /// <summary>After a respawn the road order is unknown: forget it, so a teleport is never an overtake.</summary>
        void ForgetPairs(MatchPlayer player)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == player) continue;
                pairOrder.Remove(PairKey(player, players[i], 1));
                pairOrder.Remove(PairKey(players[i], player, 1));
            }
        }
    }
}

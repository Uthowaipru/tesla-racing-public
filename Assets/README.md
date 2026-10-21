# Vehicle scripts: refactor for multi-vehicle / multi-map / multiplayer

Drop the `Scripts` folder into `Assets/` (replace the old `Assets/Scripts`).
Every moved script keeps its `.meta` file, so scene and prefab references survive.
`VehicleInput.cs.meta` was reused for `LocalPlayerInput.cs` (same GUID), so the old component on your car becomes
`LocalPlayerInput` automatically.

## Folder layout

```
Scripts/
  Vehicle/   VehicleController, VehicleStats, VehicleRecovery, VehicleDriver, DriveInput, VehicleGripAssist, VehicleImpactSensor, VehicleHealth, VehicleEffects, VehicleBoost, ImpactInfo (new)
  Input/     LocalPlayerInput (was VehicleInput), InputModeManager, MobileControls, TouchHoldButton
  Camera/    VehicleCameraRig
  Player/    LocalPlayerRig (new), PlayerProfile (new: remembers the chosen vehicle)
  UI/        PauseMenu, ControlsOverlay, ResetCounterUI, Speedometer, HealthBar, RespawnCountdown, VehicleNameTag, PickupHud, WrongWayBanner, MatchHud, MatchResultsFlow (new)
  Menu/      VehicleSelectMenu, VehiclePreviewStage, VehiclePreviewDrag (new: vehicle selector with 3D preview)
  Data/      VehicleDefinition, VehicleCatalog, StatModifierDef, MapDefinition, VehicleEffectsDefaults (new ScriptableObjects)
  Race/      RaceConfig, PlayerSlot, VehicleSpawner, MatchManager, MatchPlayer (new)
  Pickups/   Pickup (battery, repair kit)
  Bots/      BotInput, BotSettings
  Monetization/ RewardedAds (interface + static), DevRewardedAdProvider, LocalWallet (new)
  Road/      SeededRandom, RoadChunk, RoadPath, RoadGenerator, TrafficVehicle, TrafficManager, PickupPlacer, WrongWayTracker (new)
  Editor/    ControlsOverlayBuilder, MobileControlsBuilder, VehicleSelectMenuBuilder, SpeedometerBuilder, VehicleStatsEditor
```

## What changed and why

| Problem | Fix |
|---|---|
| Every car read your keyboard (`VehicleInput` was on each car) | Input is now one `LocalPlayerInput` per player. Cars get a `VehicleDriver` that reads any `IDriveInputSource` (player, AI, network). |
| `VehicleInput` created the pause menu, overlay and reset counter | `LocalPlayerRig` owns the player's UI, camera and input and wires them together. |
| `FindFirstObjectByType` in 6 runtime scripts | Removed from camera, recovery, pause menu, overlay, reset counter. They are bound explicitly (`Bind`, `SetRecovery`, `Attach`). The only scene search left is `LocalPlayerRig`'s one-time startup lookup, which you can switch off for multiplayer. |
| Other cars pushed your camera in | `VehicleController.All` registry; the camera tags every racer's colliders, including cars that spawn later. |
| Pause froze the whole world | `PauseMenu.freezeTime` (default on). Turn off for local/online multiplayer: only this player's controls are blocked. |
| Stats lived only on each car component | `VehicleDefinition` asset + `VehicleStats.ApplyDefinition()`. |
| Modifiers could not be saved or sent | `StatModifier.Id`, `StatModifierDef` asset, `VehicleStats.GetModifierIds()`. |
| Reset counter only worked if it found the car itself | `ResetCounterUI.SetRecovery()`; works with an inspector-assigned recovery too. |

`VehicleController` gained `SetInput(DriveInput)` (the old 3-argument `SetInput` still works).

## Upgrading your existing scene (single player)

1. Replace the scripts folder.
2. Create an empty GameObject named `Local Player Rig` and add **LocalPlayerRig**. That is all: it finds your
   existing camera rig, mobile controls, pause menu, overlay and counter, creates missing ones, and attaches
   the first `VehicleController` in the scene.
3. Optional clean-up: your old `PauseMenu` / `ControlsOverlay` keep their settings (the renamed fields use
   `FormerlySerializedAs`).

## Using it for multiplayer later

- Make a `LocalPlayerRig` prefab with its parts assigned. Turn off **Find Scene Objects** and **Auto Attach Scene Vehicle**.
- Your spawner instantiates `VehicleDefinition.prefab`, calls `stats.ApplyDefinition(def)`, adds modifiers from ids,
  and for the local player's car calls `rig.Attach(car)`.
- Remote and AI cars get a `VehicleDriver` and their own `IDriveInputSource` (network or bot). No rig.
- Set `PauseMenu.freezeTime = false` in any networked match.
- Lobbies (offline, LAN, online) all end by building a `RaceConfig`.

## Health, impacts and effects (latest)

- **VehicleImpactSensor** classifies every collision once: speed tier (share of the vehicle's own top speed), hit zone
  (front / rear / side) and what was hit. Health, effects and later scoring all listen to its `Impact` event.
- **VehicleHealth** is the health bar: damage from impact speed and side, a high-speed head-on crash destroys at once, then the
  vehicle disappears, waits `Respawn Delay` and respawns at the last safe place with full health and a short blinking
  invulnerability. A manual recovery (repair kit) restores `Repair Heal Fraction` (35%). Events: `Damaged`, `Destroyed`
  (the -50 penalty goes here), `Respawned`.
- **VehicleEffects** drives the particles: trail behind the vehicle, hit sparks, hit dust, low-health smoke, explosion.
  **Per vehicle:** assign your own ParticleSystem to a slot (an object inside the vehicle, or a prefab asset), optionally with
  anchor transforms for where the trail/smoke/explosion sit. **Empty slots use the project defaults** from
  `Assets/Resources/VehicleSystem/VehicleEffectsDefaults`, which the editor creates by itself (prefabs + material +
  soft particle texture in `Assets/VehicleSystem/Effects`). Tools > Vehicle > Create Default Effects recreates them;
  Tools > Vehicle > Create Editable Effects On Selected Vehicle gives one vehicle its own editable copies.
- **RespawnCountdown** HUD: "DESTROYED / Respawn in 3" while the local vehicle is destroyed.
- **VehicleNameTag**: floating name + health bar above every vehicle (World Space canvas, faces the camera, hides when
  destroyed or far away). The local player's own vehicle has none unless `Show On Local Vehicle` is on. Other players' health
  comes from `VehicleHealth.SetHealthFromNetwork()` once netcode is in.
- **HealthBar** HUD (created by LocalPlayerRig; Tools > Vehicle > Generate Health Bar UI to edit it).
- **Setup:** open the car prefab, select it, run **Tools > Vehicle > Add Damage And Effects To Selected Vehicle**. It adds the
  components and builds the name tag as objects in the vehicle. Running it again only fills in what is missing.

## Procedural road and traffic (latest)

- **RoadGenerator** builds an endless road from a few prefab pieces (**RoadChunk**), driven by a **seed**: same seed = same road on every
  device (own random generator, `SeededRandom`). Which piece comes next depends only on (seed, piece number). Only a few pieces
  exist around the local vehicle (`Chunks Ahead` / `Chunks Behind`) and they are recycled. The road may not bend past `Max Heading`.
  A room uses the host's seed (`Begin(seed)` with `Auto Start` off); offline levels use a fixed seed.
- **RoadChunk** (prefab): entry at the origin, road along +Z, optional `Centerline` points, `Lane Count`, `Lane Width`, `Weight`,
  `First Index` (use it to start with straight pieces). All pieces must share the lane layout.
- **TrafficManager** fills each new piece from the seed. Every lane has its own speed (slow on the left, fast on the right) and
  everything in a lane drives at it, so NPCs never run into each other. Density / speed multipliers are the difficulty knobs.
- **TrafficVehicle**: a Rigidbody that steers itself along its lane. A hard hit from a player knocks it (tumbles, then shrinks away).
  `TryClaimReward()` is for scoring (one reward per NPC).
- **PickupPlacer** places batteries and repair kits per piece from the seed (each phone has its own copy).
- **Setup:** GameObject > Vehicle > Road System. Default placeholder pieces/vehicles are created by the editor in
  `Assets/VehicleSystem/Road` (Tools > Vehicle > Create Road Pieces recreates them). `VehicleSpawner` starts vehicles on the road
  (`Spawn On Road Start`); `LocalPlayerRig` makes the road follow the local vehicle, and "off the road" counts as not safe for the
  repair kit (`VehicleRecovery.isPositionSafe`).

## Camera modes, leaderboard columns, test ads (latest)

- **Camera:** chase (default) -> hold look-back (C / right-stick click / BACK button) works in every mode -> tap **V / D-pad up / CAM** once = FREE camera
  (chase mode off; turn it with the mouse, the right stick or a finger drag on the phone) -> tap again = back to chase. `VehicleCameraRig.ToggleFreeCamera()`,
  `FreeCamera`, `FreeCameraChanged`. The CAM button is created at the top (left of RESET); to get it in the Hierarchy use **Add Missing Buttons** on Mobile Controls.
- **Cursor:** the finish screens free the mouse cursor on PC (`VehicleCameraRig.SetUiNeedsCursor(true/false)`; any menu can use it).
- **Leaderboard** now also shows the vehicle and the rams. Rebuild the Match Results UI (Inspector > Rebuild Default UI) to get the new columns.
- **Ads:** the pretend ad now works in EVERY build (the phone too), marked "test ad"; turn `Simulate Ads Without Sdk` off before publishing. `Max Doubles Per Day` is 0
  (no limit) by default.

## Road around every racer, void, rival rams, finish sequence

- **The road is built around EVERY racer** (`RoadGenerator.AddTarget`; the spawner adds bots, the MatchManager adds all players): ahead of the
  foremost, kept behind the last (`Max Chunk Span` limits the pieces kept). Traffic is cleaned up behind the LAST racer.
- **VoidKillZone** (added by the RoadGenerator; `Add Void Kill Zone` to turn off): a racer below `Depth` m under the road is destroyed and
  respawns. A respawn never lands off the road: remembered safe places that are not on the road are skipped, and if none is usable the road
  supplies a place on it (`VehicleRecovery.fallbackPose`, `RoadGenerator.GetRoadPoseNear`). The usual -50 applies.
- **Rival rams (MatchManager > Rivals):** with `Rival Kills` ON, a racer hit by another racer's NOSE at `Rival Kill Tier` speed or faster is destroyed
  and the rammer is not hurt and is paid the ram points. Nose to nose: the faster wins (`Head On Win Points`, `Head On Speed Margin`), the other
  is destroyed; too close to call = normal damage. NPC traffic keeps the old rule (head-on at full speed destroys you). Implemented with
  `VehicleHealth.impactResolver` (verdicts Normal / Immune / Kill).
- **MatchResultsFlow** (after the whistle): TIME UP -> winner cutscene (name + 3D vehicle, tap to skip; `CutsceneRequested(winner, done)` lets a
  custom or bought cutscene take over) -> leaderboard (position, player, score, distance, side bumps, best chain) -> rewards (coins and cash count
  up) with a "double your rewards" rewarded-ad offer -> CLAIM -> PLAY AGAIN / MAIN MENU.
- **Rewarded ads:** `IRewardedAdProvider` + `RewardedAds.Provider`. With no ad SDK, the editor and development builds use a pretend ad
  (`Simulate Ads Without Sdk`); release builds simply do not offer it. The offer is made at most `Max Doubles Per Day` times. Plug AdMob in later by
  implementing the interface and setting `RewardedAds.Provider` at startup.
- **LocalWallet** is a stand-in (PlayerPrefs) that takes the claimed coins and cash until the profile exists; `RewardsClaimed(coins, cash)` is the
  event the profile will use.

## Bots

- **BotInput** is an AI input source (like the keyboard, but for a bot): the bot vehicle is the same prefab with the same physics, health,
  recovery and respawn, driven through a `VehicleDriver`. It follows a lane at a share of its top speed, looks ahead for slower NPC traffic and
  other racers, changes to a freer lane (or follows at a gap, brakes hard when too close), and with `Aggression` leans into an NPC beside it for a
  side bump. Bots keep to the normal road, never reverse, and never collect pickups (those are the local player's).
- **Setup:** `VehicleSpawner > Bots`: `Bot Count`, `Bot Difficulty` (Easy / Normal / Hard / Custom with every number in `BotSettings`). Bots take
  the next starting slots, use the catalog vehicles in turn, show "Bot 1..." on their name tags and join the match, so the position on the HUD
  counts them and rams / overtakes between racers now score.
- Each bot gets a small seeded variation of speed and aggression.

## Match: timer, countdown and score

- **MatchManager** runs one match: countdown (input locked), a timer (3 / 6 / 10 / 12 min or custom) and the score. No finish line: when the time is up the
  most points wins. Vehicles join through `VehicleSpawner` (or, if none did, the `LocalPlayerRig` vehicle joins). At the start every
  vehicle gets its batteries, repair kits and health reset; `Begin()` can be called by hand (`Auto Start` off), for example when a room is ready.
- **Points (every value is a field, per scene):** 100 m = +10 (x the WrongWayTracker multiplier), NPC side bump +20 / +30 (once per NPC; slow
  bumps pay nothing), chain bonus (+5 / +10 / +20 / +30 per bump from the 5th, within 4 s), rival ram +20 behind / +25 side, overtake +10,
  crossing bonus (from WrongWayTracker), destroyed -50. Speed tiers use the player's OWN speed against their top speed (the thresholds of
  the VehicleImpactSensor).
- **Events:** `StateChanged`, `PointsAwarded(player, points, reason)`, `Finished(MatchResult)`. `MatchResult` has score, rank, distance,
  statistics and the coins (10 points each) and cash (40 points each) the match gave. Saving them is the profile's job (not built yet).
- **MatchHud:** time, points and position, 3-2-1-GO, "+20 SIDE BUMP" pop-ups and the results screen with PLAY AGAIN / MAIN MENU
  (created by LocalPlayerRig when a MatchManager exists; Tools > Vehicle > Generate Match HUD UI to edit it).
- **Setup:** GameObject > Vehicle > Match Manager.

## Two-way road (optional, per scene)

Tick **Two Way Road** on the RoadGenerator in any scene to switch it on; leave it off for the one-way road. Seeded content of the normal road is
unchanged by this switch (oncoming traffic and pickups use their own random channels).

- **Pieces:** `Two Way Chunk Prefabs` (forward lanes + median + `Oncoming Lane Count`; crossover pieces have `Is Crossover` ticked and an
  opening in the median). Crossovers appear from `First Crossover Index`, then every `Min`..`Max Pieces Between Crossovers` (from the seed).
  Default placeholder pieces: 2 lanes each way, 2.4 m median.
- **Oncoming traffic** (`TrafficManager`): own density, lane speeds, start-clear distance and optional vehicle list. Vehicles drive toward the
  player on the oncoming lanes (slow lane on the outer edge). `PickupPlacer` has extra battery / repair kit chances on the oncoming side.
- **WrongWayTracker** (on the Road System): which side the local player is on (`Side`, `IsWrongWay`), `ScoreMultiplier` (distance points while
  wrong way), crossing bonus events (`CrossedToOncoming(bonus)`, `CrossedToForward(bonus)`), cooldown, hysteresis, teleport handling.
  It holds no score: the match score reads it.
- **WrongWayBanner** HUD: "WRONG WAY x2" and "+50 CROSSING BONUS" (created by LocalPlayerRig when a tracker exists).
- **If the banner or points do not appear:** the tracker needs (1) **Two Way Road** ticked on the RoadGenerator, (2) the two-way pieces assigned
  (select the Road System > Fill With Default Pieces...), and (3) for points, a **MatchManager** in the scene. A Road System made earlier gets
  its WrongWayTracker automatically; the tracker's Inspector shows live status while playing. The oncoming road can only be entered at a crossover
  piece (first one at piece `First Crossover Index`, 3 by default = about 180 m; set it to 1 to test). Wrong-way distance points show as "+20 WRONG WAY".
- Head-on damage is unchanged: `VehicleHealth > Head On Kills / Head On Kill Tier` decide how deadly oncoming traffic is.

## Pickups and boost

- **VehicleBoost** (on the car): carries batteries (start 1, max 3, reset every match, never saved). BOOST spends one for
  2.5 s of extra acceleration (+80%), top speed (+30%) and mass (x1.25, harder hits). It is built from temporary stat modifiers,
  so upgrades can improve it through `Duration Multiplier` / `Power Multiplier`. The trail gets denser while boosting.
- **BOOST input:** Left Shift, gamepad X/Square or Left bumper, and a touch BOOST button. To get the button as a real object in the
  Hierarchy (so you can place it), select the Mobile Controls object and click **Add Missing Buttons (keeps your edits)**; a
  rebuild includes it too. Without that, a BOOST button is created at runtime above DRIFT.
- **GAS button:** hidden while auto-accelerate is on (the default); it exists in the Hierarchy as an inactive "GAS Button". Pause >
  Options > Auto accelerate OFF shows it, and BRAKE / DRIFT move to the "Layout when auto-accelerate is OFF" values.
- **Pickup** (battery / repair kit): trigger collider on the road. It is only taken when there is room (carry limit), and only by the
  local player's vehicle (`Local Player Only`, because every phone has its own copy of the road's pickups). A repair kit adds
  a manual recovery charge (`VehicleRecovery.AddManualUse`); the repair counter is now labelled "REPAIR KITS".
- **PickupHud:** battery squares and a boost time bar (created by LocalPlayerRig; Tools > Vehicle > Generate Pickup HUD to edit).
- **Prefabs:** created automatically in `Assets/VehicleSystem/Pickups`. Right-click in the Hierarchy > Vehicle > Battery Pickup /
  Repair Kit Pickup to place one (set `Respawn Time` to test). Replace the models with your own art any time.

## Drift and repair kit rules (latest)

- **VehicleGripAssist > Drift angle limit:** while the handbrake is held (and `Drift Tail` seconds after), the travel direction may not stray
  more than `Max Drift Angle` (45) from where the nose points, so a three-wheeler can no longer spin and slide backward. Values to
  tune on the vehicle first: `Handbrake Slide Grip` 0.55 -> 0.75 to 0.85, `Handbrake Torque` 4000 -> 1500 to 2500,
  GripAssist `Handbrake Factor` 0.2 -> 0.4 to 0.5.
- **Repair kit:** in a safe place it only heals (and is not used at all when health is full); when the vehicle is stuck, tipped
  over, stranded in the air or on an invalid position it also teleports to the last safe place. Settings are on VehicleRecovery
  ("Repair kit: heal only, or teleport too?"). `isPositionSafe` is a hook for the road system ("off the road = not safe").
  `ManualRecovered` now carries a bool: true when the vehicle was teleported.

## Steering option: buttons or tilt

Pause > Options now has **STEERING: BUTTONS / TILT** (saved) and, while tilt is on, **CALIBRATE TILT** (the phone angle at that
moment becomes "straight"). Tilt needs a phone with an accelerometer and the new Input System; on other devices the option is
greyed out and the buttons stay. Menus built earlier get the two buttons at runtime; to put them in the Hierarchy select the
Pause Menu object and click **Add Steering Option (keeps your edits)**. Tilt tuning (max angle, dead zone, smoothing, invert)
is on the Mobile Controls component.

## Recovery and grip

- **Manual recovery (R / RESET):** now teleports the car to the last safe place it drove through (at least `Safe Point Min Age`
  seconds old, free of obstacles, facing the direction of travel), upright and on the ground. No lift, no move backward.
  It costs a repair charge (`Starting Manual Resets`, carry limit `Max Manual Resets`; `AddManualUse()` is for repair-kit
  pickups; `ManualRecovered` fires for the future +35% health). `TeleportToLastSafePoint()` is free and meant for respawn.
- **Auto recovery:** unchanged (reverse out when stuck, upright when flipped). The fixed lift is gone: if no clear spot is
  found it uses a safe place, else stands the car upright on the ground where it is.
- **Removed fields:** Lift Height, Manual Back Distance, Auto Fallback Lift, Safe Point Count (now `Safe History Seconds`).
- **VehicleGripAssist:** add it next to `VehicleController` for sticky cornering that still drifts with the handbrake.

## Vehicle selector (menu scene) with 3D rotating preview

Flow: `MainMenu` scene (selector) -> race scene. The choice is saved by id in `PlayerProfile` (PlayerPrefs), and the
race scene's `VehicleSpawner` turns that id back into a car through the `VehicleCatalog`.

Set up (once):
1. Create one `VehicleDefinition` per car (Assets > Create > Vehicle System > Vehicle Definition): id, name, prefab, stats.
   Optional: a model-only `Preview Prefab`. Without it the normal prefab is copied and stripped of scripts and physics.
2. Create a `VehicleCatalog` (Assets > Create > Vehicle System > Vehicle Catalog) and drag both definitions into it.
3. Menu scene: Tools > Vehicle > Generate Vehicle Select UI. Assign the catalog and set **Next Scene** to your race scene.
   Everything it creates (Canvas, buttons, stat bars, and the "Vehicle Preview Stage" with its Turntable, Preview Camera
   and Stage Light) is a normal object in the Hierarchy. Move, resize and restyle freely; only text, bar lengths and the
   preview picture are written at runtime.
4. Race scene: empty GameObject with `VehicleSpawner` (catalog + spawn point). Keep your `LocalPlayerRig`, and turn off
   its **Auto Attach Scene Vehicle** so the spawner attaches the car.
5. Add both scenes to Build Settings (File > Build Profiles).

Notes:
- The preview camera is switched off in the editor on purpose; it renders only in Play Mode.
- Optional: create a Layer named `VehiclePreview` so the preview stage never renders or lights anything else.
- Controls: A/D or arrows or D-pad to browse, Enter/Space/A to select, Esc/B to go back, drag the picture to turn the car.

## Stats: one source of truth

`VehicleStats` has a **Definition** field. When it is set, the car's base values come from that `VehicleDefinition` asset and
the car's own numbers are ignored (and hidden in the inspector). `VehicleSpawner` sets it automatically; set it on the prefab
too so placed/test cars behave the same. Editing the asset while playing updates every car that uses it.

One-time migration per car: select the `VehicleDefinition` > Inspector three-dots menu > **Copy Stats From Prefab**
(copies the numbers currently on the car into the asset), then drag the definition into the prefab's `VehicleStats > Definition`.
`VehicleStats` itself stays on the car: it holds the runtime modifiers and the computed values, and `VehicleController` requires it.
The controller's own tuning (steering curve, handbrake, torque curve ...) still lives on each car prefab.

## Speedometer

`Speedometer` (UI/) shows speed, unit (km/h or mph) and a bar that fills to the car's current top speed. `LocalPlayerRig`
creates it and gives it the car, so there is nothing to assign. To edit it in the Hierarchy: Tools > Vehicle > Generate
Speedometer UI. Optional analog dial: assign your own needle to **Needle**.

## Not included yet

`RaceManager` (countdown, checkpoints, laps), a map selector, modifier loading in the spawner, the room services and netcode.

## Verification note

These files were refactored and structurally checked (brace/`#if` balance, no leftover references to removed
members) outside Unity, so they were **not compiled in the Unity editor**. Open the project and check the Console
for errors on first import.

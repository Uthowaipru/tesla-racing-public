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
  UI/        PauseMenu, ControlsOverlay, ResetCounterUI, Speedometer, HealthBar, RespawnCountdown, VehicleNameTag, PickupHud (new)
  Menu/      VehicleSelectMenu, VehiclePreviewStage, VehiclePreviewDrag (new: vehicle selector with 3D preview)
  Data/      VehicleDefinition, VehicleCatalog, StatModifierDef, MapDefinition, VehicleEffectsDefaults (new ScriptableObjects)
  Race/      RaceConfig, PlayerSlot, VehicleSpawner
  Pickups/   Pickup (battery, repair kit)
  Road/      SeededRandom, RoadChunk, RoadPath, RoadGenerator, TrafficVehicle, TrafficManager, PickupPlacer (new)
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

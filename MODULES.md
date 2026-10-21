# MODULES — Rickshaw Chaos: Dhaka

> Target assembly layout for the decouple. **Current state: zero `.asmdef` files.**
> Every script compiles into `Assembly-CSharp` / `Assembly-CSharp-Editor` today.
> Read `ARCHITECTURE.md` first, and `DECOUPLING_PLAN.md` for the measured dependency analysis.

---

## 1. Current folder inventory (verified 2026-10-21)

| Folder | Files | LOC | Namespace |
|---|---:|---:|---|
| `Scripts/UI` | 12 | 3612 | `VehicleSystem` |
| `Scripts/Vehicle` | 11 | 2300 | `VehicleSystem` |
| `Scripts/Editor` | 10 | 2071 | `VehicleSystem.EditorTools` |
| `AnimeToon/Runtime` | 8 | 1930 | `AnimeToon` |
| `AnimeToon/Editor` | 9 | 1788 | `AnimeToon.Editor` |
| `Scripts/Road` | 9 | 1523 | `VehicleSystem` |
| `Scripts/Race` | 4 | 888 | `VehicleSystem` |
| `Scripts/Menu` | 3 | 878 | `VehicleSystem` |
| `Scripts/Input` | 4 | 854 | `VehicleSystem` |
| `Scripts/Bots` | 2 | 359 | `VehicleSystem` |
| `Scripts/Player` | 2 | 334 | `VehicleSystem` |
| `Scripts/Data` | 5 | 206 | `VehicleSystem` |
| `Scripts/Pickups` | 1 | 114 | `VehicleSystem` |
| `Scripts/Monetization` | 3 | 92 | `VehicleSystem` |
| `Scripts/Camera` | 1 | 585 | `VehicleSystem` |
| `RickshawChaos_Integration/Runtime` | 5 | — | `AnimeToon.Integration` |
| `RickshawChaos_Integration/Editor` | 2 | — | `VehicleSystem.EditorTools` |

> `RickshawChaos_Integration/Editor/` still declares namespace `VehicleSystem.EditorTools`. Move it to
> `AnimeToon.Editor` or a dedicated `RickshawChaos.Integration.Editor` when the assembly is created.

---

## 2. The measured dependency graph

Method: parse every `.cs`, build a type→type reference graph across **different files**, find
mutually-referencing groups.

```
cross-file mutual type pairs : 63
cross-file type SCCs          :  6
SCCs spanning >1 folder       :  2   <-- the only real assembly blockers
```

### Blocker A — the 43-type blob

One strongly-connected group of **43 types across 10 folders**. Every one of these types must live in
the same assembly *until* the mutual references are broken.

```
BotDifficulty  BotInput  BotSettings          (Bots)
ControlsOverlay  HealthBar  MatchHud            (UI)
MatchResultsFlow  PauseMenu  PickupHud          (UI)
ResetCounterUI  RespawnCountdown  Speedometer   (UI)
VehicleNameTag  WrongWayBanner                 (UI)
LocalPlayerInput  MobileControls  InputMode     (Input)
InputModeManager  TouchHoldButton               (Input)
LocalPlayerRig                                   (Player)
MatchManager  MatchPlayer  VehicleSpawner       (Race)
PickupPlacer  RoadChunk  RoadGenerator          (Road)
RoadSide  TrafficManager  VoidKillZone          (Road)
WrongWayTracker                                  (Road)
VehicleController  VehicleDriver                (Vehicle)
VehicleEffects  VehicleHealth                   (Vehicle)
VehicleImpactSensor  VehicleRecovery            (Vehicle)
VehicleStats                                     (Vehicle)
DriveInput  IDriveInputSource                    (Vehicle)
VehicleDefinition  VehicleEffectsDefaults       (Data)
LocalWallet                                      (Monetization)
VehicleCameraRig                                 (Camera)
```

### Blocker B — the 4-type stats blob

```
ModifierType  StatModifier  VehicleStat     (Scripts/Vehicle/VehicleStats.cs)
StatModifierDef                              (Scripts/Data/StatModifierDef.cs)
```
Small and self-contained — **this is the cheapest win in the whole project.**

### The single biggest lever: `LocalPlayerRig`

`LocalPlayerRig` has **mutual-reference degree 18** — it is the hub of the 43-type blob. It reaches
into UI, Camera, Race, Road, Input and Vehicle, and they all reach back.

Fixing `LocalPlayerRig` alone removes **11 of the 19 edges** needed to shatter the blob.

---

## 3. Minimum cut set — the 19 edges to break

A greedy minimum-cut over the cross-file mutual graph. Breaking these 19 edges splits the 43-type
blob into the four groups in §4. Each row is one refactor task.

| # | Edge | How to break it |
|---:|---|---|
| 1 | `LocalPlayerRig` ↔ `MatchManager` | `LocalPlayerRig` should read match state through a small interface or an event, not call `MatchManager` directly. |
| 2 | `VehicleHealth` ↔ `VehicleRecovery` | Recovery should subscribe to `VehicleHealth.Destroyed` instead of holding the reference. |
| 3 | `LocalPlayerRig` ↔ `MatchResultsFlow` | Results flow registers itself with the rig, or the rig exposes an `IMatchUi` interface. |
| 4 | `MobileControls` ↔ `TouchHoldButton` | `TouchHoldButton` should raise a plain event / expose an `IsHeld` bool that `MobileControls` reads. |
| 5 | `RoadSide` ↔ `WrongWayTracker` | `WrongWayTracker` should consume road data via an interface or a plain struct, not the `RoadSide` type. |
| 6 | `LocalPlayerRig` ↔ `MatchHud` | same as #1 — HUD behind an interface. |
| 7 | `LocalPlayerRig` ↔ `VehicleCameraRig` | camera behind an interface, or the camera finds the rig. |
| 8 | `RoadChunk` ↔ `RoadGenerator` | `RoadChunk` should not reference the generator; pass data in. |
| 9 | `LocalPlayerRig` ↔ `ResetCounterUI` | UI behind an interface. |
| 10 | `LocalPlayerRig` ↔ `PickupHud` | UI behind an interface. |
| 11 | `HealthBar` ↔ `LocalPlayerRig` | `HealthBar` should read `VehicleHealth` directly instead of the rig. |
| 12 | `RoadGenerator` ↔ `VoidKillZone` | `VoidKillZone` should take plain data (centre line, width) rather than the generator. |
| 13 | `LocalPlayerRig` ↔ `WrongWayBanner` | UI behind an interface. |
| 14 | `RoadGenerator` ↔ `TrafficManager` | traffic should be driven by data, not by the generator type. |
| 15 | `LocalPlayerRig` ↔ `Speedometer` | UI behind an interface. |
| 16 | `LocalPlayerRig` ↔ `VehicleNameTag` | UI behind an interface. |
| 17 | `LocalPlayerRig` ↔ `RespawnCountdown` | UI behind an interface. |
| 18 | `PickupPlacer` ↔ `RoadGenerator` | `PickupPlacer` should take data, not the generator type. |
| 19 | `VehicleController` ↔ `VehicleDriver` | driver should read the controller through a narrow interface. |

Plus **2 more** for Blocker B:

| # | Edge | How to break it |
|---:|---|---|
| 20 | `StatModifierDef` ↔ `StatModifier`/`VehicleStat` | move `ModifierType`, `StatModifier`, `VehicleStat` into `Scripts/Data/` (or a new `VehicleSystem.Stats` assembly) so Data has no outbound edge to Vehicle. |
| 21 | `VehicleDefinition` ↔ `VehicleStats` | `VehicleDefinition` should hold plain numbers, not the `VehicleStats` component. |

---

## 4. What the graph looks like after the cut

| Group | Types | Folders involved |
|---:|---|---|
| A | 13 | Input 4, UI 2, Vehicle 4, Player 1, Road 2 |
| B | 8 | Race 3, Data 1, Bots 3, Vehicle 1 |
| C | 4 | Vehicle 3, Data 1 (the stats blob) |
| D | 2 | UI 1, Monetization 1 (`LocalWallet` ↔ `MatchResultsFlow`) |

Each group still spans folders, so each group becomes **one assembly** (or is split further by
re-cutting). That is the whole decoupling in four assemblies plus the leaf ones.

---

## 5. Target assembly layout

| # | Assembly name | Folder(s) | Files | May reference |
|---:|---|---|---:|---|
| 0 | `VehicleSystem.Core` | `Assets/Scripts/Core/` | ~3 | *nothing* |
| 1 | `VehicleSystem.Data` | `Assets/Scripts/Data/` | 5 | Core |
| 2 | `VehicleSystem.Vehicle` | `Assets/Scripts/Vehicle/` | 11 | Core, Data |
| 3 | `VehicleSystem.Camera` | `Assets/Scripts/Camera/` | 1 | Core, Vehicle |
| 4 | `VehicleSystem.Input` | `Assets/Scripts/Input/` | 4 | Core |
| 5 | `VehicleSystem.Road` | `Assets/Scripts/Road/` | 9 | Core, Data, Vehicle |
| 6 | `VehicleSystem.Pickups` | `Assets/Scripts/Pickups/` | 1 | Core, Vehicle |
| 7 | `VehicleSystem.Bots` | `Assets/Scripts/Bots/` | 2 | Core, Vehicle, Road |
| 8 | `VehicleSystem.UI` | `Assets/Scripts/UI/` | 12 | Core, Vehicle, Data |
| 9 | `VehicleSystem.Menu` | `Assets/Scripts/Menu/` | 3 | Core, Data, Vehicle, UI |
| 10 | `VehicleSystem.Race` | `Assets/Scripts/Race/` | 4 | Core, Data, Vehicle, Bots, Road |
| 11 | `VehicleSystem.Player` | `Assets/Scripts/Player/` | 2 | Core, Vehicle, Input, UI, Camera, Road, Race, Data |
| 12 | `VehicleSystem.Monetization` | `Assets/Scripts/Monetization/` | 3 | Core |
| 13 | `RickshawChaos.Integration` | `Assets/RickshawChaos_Integration/Runtime/` | 5 | AnimeToon, VehicleSystem.* |
| 14 | `AnimeToon` | `Assets/AnimeToon/Runtime/` | 8 | *nothing* (leaf) |
| 15 | `VehicleSystem.EditorTools` | `Assets/Scripts/Editor/` | 10 | all runtime assemblies |
| 16 | `AnimeToon.Editor` | `Assets/AnimeToon/Editor/` | 9 | AnimeToon |
| 17 | `RickshawChaos.Integration.Editor` | `Assets/RickshawChaos_Integration/Editor/` | 2 | RickshawChaos.Integration, AnimeToon |

### `VehicleSystem.Core` — the new module

Create `Assets/Scripts/Core/` and move **three files** (with their `.meta`):

| File | From | Why it belongs in Core |
|---|---|---|
| `DriveInput.cs` | `Scripts/Vehicle/` | The input contract. `DriveInput`, `IDriveInputSource`. Used by Bots, Input, Vehicle — all three must depend on Core instead of on each other. |
| `ImpactInfo.cs` | `Scripts/Vehicle/` | The impact contract. `ImpactInfo`, `SpeedTier`, `HitZone`, `ImpactVerdict`. Used by Race, UI, Vehicle. |
| `SeededRandom.cs` | `Scripts/Road/` | The deterministic RNG. Used by Road + Pickups. Depends on nothing. |

> **Note:** moving these three files does **not** by itself break the 43-type blob — it changes which
> edges cross a boundary but `LocalPlayerRig` still ties everything together. It is worth doing first
> because it makes the remaining edges easier to see and to compile-break one at a time.

---

## 6. Hidden dependencies the compiler will not catch

These survive assembly splits silently. Listed so you can decide deliberately.

| Kind | Location | Count |
|---|---|---|
| `FindFirstObjectByType` | `Scripts/Player/LocalPlayerRig.cs` | 17 |
| | `Scripts/Editor/` | 13 |
| | `Scripts/Race/` | 7 |
| | `Scripts/UI/` | 6 |
| | `Scripts/Road/` | 3 |
| | `Scripts/Input/` | 2 |
| | `AnimeToon/Runtime/` | 2 |
| | `AnimeToon/Editor/` | 2 |
| | `Scripts/Menu/` | 1 |
| `Resources.Load` | `Scripts/Data/VehicleEffectsDefaults.cs:28`, `AnimeToon/Runtime/`, `RickshawChaos_Integration/Runtime/AnimeDriftEffectsDefaults.cs:22` | 3 |
| `Camera.main` | `Scripts/Camera/`, `Scripts/UI/`, `RickshawChaos_Integration/Runtime/`, `AnimeToon/Runtime/`, `Scripts/Editor/` | 5 |
| `GameObject.Find` / `FindWithTag` | `RickshawChaos_Integration/Runtime/` | 2 |

### Statics / singletons

| Static | File:line | Module |
|---|---|---|
| `VehicleController.All` | `Vehicle/VehicleController.cs:87` | Vehicle |
| `VehicleController.VehicleEnabled` (event) | `Vehicle/VehicleController.cs` | Vehicle |
| `RewardedAds.Provider` | `Monetization/RewardedAds.cs:23` | Monetization |
| `LocalWallet` | `Monetization/LocalWallet.cs:19,21` | Monetization |
| `InputModeManager` | `Input/InputModeManager.cs:29` | Input |
| `MatchResultsFlow` | `UI/MatchResultsFlow.cs:473` | UI |
| `AnimeGraphicsMenu` | `AnimeToon/Runtime/` | AnimeToon |
| `AnimeQuality` | `AnimeToon/Runtime/` | AnimeToon |
| `AnimeMaterialPresets` | `AnimeToon/Editor/` | AnimeToon.Editor |
| `AnimeDriftBootstrap` / `AnimeDriftEffects` / `AnimeDriftEffectsDefaults` | `RickshawChaos_Integration/Runtime/` | Integration |

---

## 7. Existing interfaces — the pattern to copy

| Interface | Declared in | Members | Used by |
|---|---|---|---|
| `IDriveInputSource` | `Scripts/Vehicle/DriveInput.cs` | `DriveInput Read();` | `BotInput`, `LocalPlayerInput`, `VehicleDriver` |
| `IRewardedAdProvider` | `Scripts/Monetization/RewardedAds.cs` | `bool IsReady { get; }`, `void Show(string, Action<bool>)` | `RewardedAds` |

When you break edge #19 (`VehicleController` ↔ `VehicleDriver`) or #4 (`MobileControls` ↔
`TouchHoldButton`), copy the `IDriveInputSource` shape: a one-method interface in Core, implemented by
the concrete class, consumed by the caller.

---

## 8. Suggested order of work

Each row = one conversation = one compile check. Do **not** do them all in one session.

| Step | Work | Risk |
|---:|---|---|
| 1 | Write `ARCHITECTURE.md`, `MODULES.md`, `CONTRACTS.md` (done) | none |
| 2 | Extract `AnimeToon` + `AnimeToon.Editor` asmdefs | **low** — clean leaf, no cycles |
| 3 | Extract `RickshawChaos.Integration` + its Editor asmdefs | **low** — depends only on AnimeToon + Vehicle |
| 4 | Extract `VehicleSystem.EditorTools` asmdef | **low** — it is a sink |
| 5 | Move the 3 Core files, create `VehicleSystem.Core` | **low** — pure file move |
| 6 | Break Blocker B (stats, edges 20–21) → `VehicleSystem.Data` | **medium** — 4 types, 2 edges |
| 7 | Break the `LocalPlayerRig` edges (1, 3, 6, 7, 9, 10, 11, 13, 15, 16, 17) | **high** — 11 edges, the hub |
| 8 | Break the Vehicle edges (2, 19) | **medium** |
| 9 | Break the Road edges (5, 8, 12, 14, 18) | **medium** |
| 10 | Break the remaining Input/UI/Monetization edges (4, 20) | **medium** |
| 11 | Create the remaining asmdefs | **medium** — compile errors will surface here |

Steps 2–5 are safe and can be done first to prove the toolchain works before touching any cycle.

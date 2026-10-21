# ARCHITECTURE — Rickshaw Chaos: Dhaka

> Unity 6.3 LTS (6000.3.11f1) · URP 17.3.0 · Android
> Repo: `github.com/Uthowaipru/tesla-racing` (private) · `tesla-racing-public` (public mirror)
> Product name in `ProjectSettings.asset`: **Rickshaw Chaos: Dhaka**

---

## 1. The three layers

The project is deliberately split into three layers. **Dependencies point downward only.**

```
┌──────────────────────────────────────────────────────────────┐
│  LAYER 1 — GAME            Assets/Scripts/                   │
│  The rickshaw game itself: vehicle, road, traffic, match,     │
│  UI, input, bots, economy.                                    │
└───────────────┬──────────────────────────────────────────────┘
                │ may reference ↓
┌───────────────┴──────────────────────────────────────────────┐
│  LAYER 2 — INTEGRATION     Assets/RickshawChaos_Integration/  │
│  The glue between the game and the anime toolkit.             │
│  Namespace: AnimeToon.Integration                             │
└───────────────┬──────────────────────────────────────────────┘
                │ may reference ↓
┌───────────────┴──────────────────────────────────────────────┐
│  LAYER 3 — TOOLKIT         Assets/AnimeToon/                  │
│  A reusable anime toon rendering toolkit. Knows NOTHING       │
│  about rickshaws. Namespace: AnimeToon                        │
└──────────────────────────────────────────────────────────────┘
```

**The rule that makes this work:** Layer 3 must never reference Layer 1 or 2. Layer 2 may reference
Layer 1 and Layer 3. Layer 1 may reference Layer 2 and Layer 3.

`Assets/AnimeToon/` currently references **zero** `VehicleSystem` types — this is verified and must
stay true. It is the cleanest boundary in the project and the easiest assembly to extract.

---

## 2. Why `RickshawChaos_Integration/` is a TOP-LEVEL folder, not inside `Scripts/`

The bridge scripts were moved out of `Assets/Scripts/` into `Assets/RickshawChaos_Integration/`.
Four reasons, in priority order:

1. **Layer separation must be visible in the folder tree.** If the bridge lived inside `Assets/Scripts/`,
   nobody could tell "game code" from "glue code" without reading namespaces. A top-level sibling makes
   the three layers obvious at a glance.
2. **The AnimeToon handout specifies it.** The shader/toolkit account's own plan puts the integration at
   `Assets/RickshawChaos_Integration/{Runtime,Editor}`. Matching that avoids contradicting the
   authoritative document.
3. **Future refactors must not sweep it up.** "Move `Assets/Scripts/*` into per-module folders" would
   otherwise drag the bridge along and leave `Assets/Scripts/Editor/` (VehicleSystem tools) mixed with
   `Assets/RickshawChaos_Integration/Editor/` (AnimeToon tools). Two folders both named `Editor` in
   different parents is already confusing; two named `Editor` inside one parent is worse.
4. **The toolkit stays portable.** You could copy `Assets/AnimeToon/` + `Assets/RickshawChaos_Integration/`
   into another project and get the whole anime system in one move.

### Where each bridge file lives now

| File | Folder | Referenced by scene/prefab? |
|---|---|---|
| `AnimeVehicleFxBridge.cs` | `RickshawChaos_Integration/Runtime/` | **YES — `Scenes/Game 1.unity`, `Scenes/Game 2.unity`** (Main Camera) |
| `AnimeSpeedFx.cs` | `AnimeToon/Runtime/` | YES — sibling component on the same Main Camera |
| `AnimeDriftBootstrap.cs` | `RickshawChaos_Integration/Runtime/` | no (runtime `AddComponent`) |
| `AnimeDriftEffects.cs` | `RickshawChaos_Integration/Runtime/` | no (runtime `AddComponent`) |
| `AnimeDriftEffectsDefaults.cs` | `RickshawChaos_Integration/Runtime/` | no (ScriptableObject) |
| `AnimeVehicleEffectsQuality.cs` | `RickshawChaos_Integration/Runtime/` | no (runtime `AddComponent`) |
| `AnimeVehicleEffectsInstaller.cs` | `RickshawChaos_Integration/Editor/` | no (MenuItem tool) |
| `AnimeDriftEffectsInstaller.cs` | `RickshawChaos_Integration/Editor/` | no (MenuItem tool) |

> ⚠️ **`AnimeVehicleFxBridge` is NOT optional.** It is an added component on the Main Camera in two of
> the four build scenes. Losing its `.meta` produces `Missing (MonoBehaviour)` on the camera in
> `Game 1.unity` and `Game 2.unity`. The AnimeToon handout's claim that it "may be unused" is wrong.

---

## 3. Five architectural rules

1. **Dependencies go one way.**
   `Core → Data → Vehicle → Race/Player → UI`. Never the reverse. UI may *read* game state; game code
   must never call into UI.

2. **Shared contracts live in Core, which depends on nothing.**
   `Core` holds `DriveInput`, `IDriveInputSource`, `ImpactInfo`, `SpeedTier`, `HitZone`, `ImpactVerdict`,
   `SeededRandom` and the shared enums. If two modules need to talk, the contract goes in Core.

3. **ONE wiring place per subsystem.**
   `LocalPlayerRig` wires the player vehicle + input + UI + camera + road. `VehicleSpawner` wires the
   match. `MatchManager` wires the match rules. Wiring code is allowed to know everything; gameplay
   code is not.

4. **Small public surfaces.**
   A module exposes the smallest set of types other modules may use. Everything else is `internal` or
   `private`.

5. **Numbers in assets, behaviour in code.**
   `VehicleDefinition`, `StatModifierDef`, `RaceConfig`, `BotSettings`, `VehicleEffectsDefaults` are
   ScriptableObjects. Tuning values live in `.asset` files, never as hard-coded constants in scripts.

---

## 4. What the boundaries are made of

Real decoupling needs all four of these together. ScriptableObjects alone do nothing.

| Tool | Purpose | Example in this project |
|---|---|---|
| **Interfaces** | One implementation-agnostic contract | `IDriveInputSource`, `IRewardedAdProvider` |
| **Events** | Gameplay announces, UI listens | `VehicleHealth.Destroyed`, `VehicleImpactSensor.Impact`, `VehicleController.VehicleEnabled`, `AnimeQuality.Changed` |
| **Data assets** | Shared configuration | `VehicleDefinition`, `StatModifierDef`, `RaceConfig` |
| **Assembly definitions** | The compiler enforces the boundary | `*.asmdef` files — **there are currently ZERO** |

Unity refuses to compile a module that uses a type it is not allowed to. That is the only mechanism that
actually *keeps* modules apart once several people or AIs are writing code.

---

## 5. Namespaces

| Namespace | Folder | Files |
|---|---|---:|
| `VehicleSystem` | `Assets/Scripts/{Bots,Camera,Data,Input,Menu,Monetization,Pickups,Player,Race,Road,UI,Vehicle}` | 56 |
| `VehicleSystem.EditorTools` | `Assets/Scripts/Editor/` | 10 |
| `AnimeToon` | `Assets/AnimeToon/Runtime/` | 8 |
| `AnimeToon.Editor` | `Assets/AnimeToon/Editor/` | 9 |
| `AnimeToon.Integration` | `Assets/RickshawChaos_Integration/Runtime/` | 5 |
| *(none)* | `Assets/AnimeToon/Editor/AnimeSmoothNormalBaker.cs`, `Assets/Scripts/UI/setactiveobject.cs`, `Assets/TutorialInfo/Scripts/**` | 4 |

Namespaces are already clean — **do not rename them.** Renaming is not required for assembly
boundaries and adds a large diff for no benefit.

---

## 6. Unity rules you must not break

1. **Never move, rename or delete a `.cs` without its `.meta`.**
   Scenes and prefabs reference `MonoBehaviour`s by **GUID**, not by name. Moving a file is safe only if
   the `.meta` moves with it.

2. **`VehicleInput.cs.meta` was reused for `LocalPlayerInput.cs`** (same GUID, `0d0f...`). The old
   component on your car therefore became `LocalPlayerInput` automatically. Do not "fix" this by
   generating a new GUID.

3. **`internal` breaks at assembly boundaries.** Today everything is one assembly, so `internal` is
   free. The moment you split, every cross-boundary `internal` access is a compile error. Plan for
   `public`, or `[assembly: InternalsVisibleTo("...")]`.

4. **Editor assemblies need `includePlatforms: ["Editor"]` AND explicit `references`.**
   `Assets/Scripts/Editor/` and `Assets/AnimeToon/Editor/` both reach into runtime code.

5. **`Resources.Load` and `Camera.main` are global lookups.** They survive assembly splits but the
   compiler will *not* tell you about them. Known instances are listed in `MODULES.md`.

6. **Input:** `activeInputHandler: 1` → **Input System package only**. Legacy `UnityEngine.Input` calls
   are all inside `#if ENABLE_LEGACY_INPUT_MANAGER` branches and are compiled out. Do not add new
   legacy `Input` calls.

7. **Verification after every step:** compile clean → Console has no `Missing (MonoBehaviour)` →
   all 4 build scenes open → `git status` shows **no modified `.meta`** → playtest.

---

## 7. Build scenes and the acceptance test

`Assets/Scenes/` → `MainMenu.unity`, `Game.unity`, `Game 1.unity`, `Game 2.unity`
(registered in `ProjectSettings/EditorBuildSettings.asset`).

**A refactor is done when:** all four scenes open without errors, all 42 non-TMP prefabs still resolve,
the game boots from `MainMenu` → vehicle select → race → a vehicle spawns, drives, bots race and the
HUD updates.

---

## 8. Known-stale statements — do not trust these

| Where | What it says | Reality |
|---|---|---|
| `Assets/README.md` (last line) | "these files … were **not compiled in the Unity editor**" | Outdated. They compile clean — 0 CS errors, `Assembly-CSharp.dll` builds on every change. |
| AnimeToon handout, last line | "None of this code has been compiled by its author" | Outdated, same as above. |
| AnimeToon handout | `AnimeVehicleFxBridge` is "optional, may be unused" | **Wrong.** It is on the Main Camera in `Game 1.unity` and `Game 2.unity`. |
| AnimeToon handout | recommends `Assets/RickshawChaos_Integration/` | **Done.** The bridge now lives there. |
| `Assets/README.md` folder list | omits `Scripts/Runtime/` | The bridge was there; it has since moved to `Assets/RickshawChaos_Integration/`. |

---

## 9. Out of scope — do not "fix" these

- **10 broken script GUIDs** — 4 HDRP built-ins, 4 orphan custom volume components in the unused
  `Assets/Settings/DefaultVolumeProfile.asset`, 2 stale TextMeshPro 2.x package GUIDs. All vendor or
  sample content. Full list in `DECOUPLING_PLAN.md` §4.
- `Assets/Settings/DefaultVolumeProfile.asset` — an orphan; the project uses a custom anime volume profile.
- `Assets/_Recovery/0.unity` — orphan scene, not in build settings.
- `Assets/TextMesh Pro/Examples & Extras/` — 34 vendor scripts. Never edit. TMP now lives inside
  `com.unity.ugui` 2.0.0; the standalone `com.unity.textmeshpro` package is gone.
- 6 dangling Git-LFS objects — unreachable from HEAD, missing from both repos. Pre-existing, harmless.

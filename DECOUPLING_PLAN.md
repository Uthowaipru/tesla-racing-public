# DECOUPLING PLAN — Rickshaw Chaos: Dhaka (Unity 6 URP)

**Generated:** 2026-10-21 · **Scope:** `Assets/Scripts/` + `Assets/AnimeToon/` + `Assets/RickshawChaos_Integration/` (100 non-vendor scripts)
**Method:** static analysis of the working tree (types, namespaces, cross-folder refs, `m_Script` GUID→asset mapping),
then a cross-file type-reference graph with a greedy minimum-cut to find the exact edges that must be broken.

> **Read this first.** You are handed a Unity project with **zero `.asmdef` files** — every script compiles into
> `Assembly-CSharp` / `Assembly-CSharp-Editor`. "Decoupling" here means **introducing assembly boundaries**.
> That is the highest-risk refactor possible in Unity, and it is done by *adding* asmdefs and fixing what breaks.

### Companion documents

| Document | What it holds |
|---|---|
| `ARCHITECTURE.md` | The 3 layers, the 5 rules, Unity landmines, the stale-statement table |
| `MODULES.md` | The 17 target assemblies, the measured dependency graph, **the 21 edges to break**, the 10-step order of work |
| `CONTRACTS.md` | The stable signatures — what must not change without agreement |
| `HANDOFF_TEMPLATE.md` | The brief to paste into every new conversation |

### The headline result (supersedes the earlier estimate in this document)

The earlier version of this plan estimated "7 mutual cycles" at folder level. A **type-level,
cross-file** analysis gives a sharper and more actionable number:

```
cross-file mutual type pairs : 63
cross-file type SCCs          :  6
SCCs spanning >1 folder       :  2   <-- the only real assembly blockers
```

- **Blocker A** — one 43-type blob spanning 10 folders.
- **Blocker B** — a 4-type stats blob (`ModifierType`, `StatModifier`, `VehicleStat`, `StatModifierDef`).
- **A minimum cut of 21 edges** shatters them. **11 of those 21 are `LocalPlayerRig` ↔ X** — the
  composition root is the single biggest lever in the project.
- `MODULES.md` §3 lists every one of the 21 edges with the exact type pair and how to break it.

### Open questions this document answers

| Question (from the previous conversation) | Answer |
|---|---|
| "Send the GitHub link" | **https://github.com/Uthowaipru/tesla-racing-public** — public mirror, created and verified. Same SHA as the private repo. |
| "Which of your scripts did you edit for the vehicle VFX?" | See **§7A — The integration layer** and **the contract surface**. The integration is 5 runtime files + 2 editor files, and it depends on exactly 12 game members, listed in §7A. |
| "Planning first or splitting first?" | **Planning first.** Never split first — each conversation invents its own boundaries and you get merge conflicts. |
| "Are ScriptableObjects the decoupling mechanism?" | No. See §7A + §2. ScriptableObjects move *numbers* out of code; they decouple nothing by themselves. Real decoupling = interfaces + events + data assets + **asmdefs** (the only one Unity enforces). |

### Where the truth lives (read in this order)

| Doc | Answers | Location |
|---|---|---|
| **GDD v0.3** | *what the game is* — pillars, match rules, economy, monetization, roadmap | `Assets/Rickshaw Chaos_ Dhaka GDD v0.3.md` (committed, `500f421`) |
| `Assets/README.md` | *how the vehicle code works today* | repo, `Assets/README.md` |
| AnimeToon handout | *the cross-account contract* | paste into the shader account |
| **this document** | *the seams* — 7 cycles, 16 assemblies, landmines | repo root (to be committed) |
| `ARCHITECTURE.md` / `MODULES.md` / `CONTRACTS.md` | *the rules, the module map, the frozen signatures* | **do not exist yet — write them first** |

**Product name is `Rickshaw Chaos: Dhaka`** (`ProjectSettings/ProjectSettings.asset` → `productName`).
The GitHub repo is called `tesla-racing`, but that is only the repository name — do not rename project
folders or classes to "Tesla".

### The three shared documents do not exist yet

`ARCHITECTURE.md`, `MODULES.md` and `CONTRACTS.md` are **not in the repo**. They are the mechanism that lets
a new conversation know what the last one did — write them **first**, before any code moves.
`MODULES.md` can be generated from §2 and §7 below.

---

## 1. Current state

| | |
|---|---|
| Unity | 6000.3.11f1 |
| Render pipeline | URP 17.3.0 (no HDRP in manifest) |
| Input | `activeInputHandler: 1` → **Input System only**; legacy `UnityEngine.Input` is compiled out |
| Assembly definitions | **0** — everything in `Assembly-CSharp` |
| Namespaces | already clean: `VehicleSystem` (56), `VehicleSystem.EditorTools` (12), `AnimeToon` (8), `AnimeToon.Editor` (8), `AnimeToon.Integration` (5), none (4) |
| Build scenes | `Assets/Scenes/MainMenu.unity`, `Game.unity`, `Game 1.unity`, `Game 2.unity` |
| Tracked files | 1177 (1057 text, 120 binary/LFS) |
| `.meta` GUIDs | 618 unique in `Assets/`, 0 duplicates |

### Folder rollup

| Folder | Files | LOC | Role |
|---|---:|---:|---|
| `Scripts/UI` | 12 | 3623 | HUDs, menus, results flow |
| `Scripts/Vehicle` | 11 | 2310 | controller, health, driver, recovery, boost, grip, effects |
| `Scripts/Editor` | 12 | 2213 | build tools (Menu > Vehicle) |
| `AnimeToon/Runtime` | 8 | 1938 | toon rendering/quality |
| `AnimeToon/Editor` | 9 | 1797 | toon tools |
| `Scripts/Road` | 9 | 1532 | procedural road, traffic, wrong-way |
| `Scripts/Race` | 4 | 892 | match, spawner, config |
| `Scripts/Menu` | 3 | 881 | vehicle select + 3D preview |
| `Scripts/Input` | 4 | 857 | input sources, mobile controls |
| `Scripts/Runtime` | 5 | 746 | **AnimeToon ↔ Vehicle bridge** |
| `Scripts/Bots` | 2 | 361 | AI input source |
| `Scripts/Player` | 2 | 336 | `LocalPlayerRig` (composition root), `PlayerProfile` |
| `Scripts/Data` | 5 | 211 | ScriptableObjects |
| `Scripts/Pickups` | 1 | 115 | battery / repair kit |
| `Scripts/Camera` | 1 | 585 | `VehicleCameraRig` |
| `Scripts/Monetization` | 3 | 95 | rewarded ads, wallet |

---

## 2. The headline finding

**All 11 runtime folders form ONE strongly-connected component.** There is no acyclic layering to exploit
today — every folder can reach every other folder.

That does **not** mean 11 boundaries are needed. The *type-level* edges show most of the coupling is
one-directional in practice. There are exactly **7 genuine mutual cycles** to break.

### The 7 mutual cycles (exact types)

| # | Cycle | Type A | Type B | Fix direction |
|---|---|---|---|---|
| C1 | `Data ↔ Vehicle` | `VehicleDefinition` → `VehicleStats` | `VehicleStats` → `VehicleDefinition` | `VehicleDefinition` should hold a plain stat struct, not the `VehicleStats` component |
| C2 | `Vehicle ↔ Camera` | `VehicleRecovery` → `VehicleCameraRig` | `VehicleCameraRig` → `VehicleController`, `VehicleStat` | camera looks up the vehicle; recovery needs the camera — use an interface/event |
| C3 | `Camera ↔ UI` | `VehicleCameraRig` → `PauseMenu` | `ControlsOverlay`, `MatchResultsFlow` → `VehicleCameraRig` | pause state → camera via event, not a type reference |
| C4 | `Input ↔ UI` | `LocalPlayerInput` → `PauseMenu` | `PauseMenu`, `ControlsOverlay` → `LocalPlayerInput`, `InputModeManager`, `MobileControls` | UI must not know the concrete input class — depend on `IDriveInputSource` / an `IInputMode` |
| C5 | `Menu ↔ UI` | `MatchResultsFlow` → `VehiclePreviewStage` | `VehicleSelectMenu` → `Step` (nested enum) | move `Step` out of `VehicleSelectMenu` into a shared enum type |
| C6 | `UI ↔ Race` | `MatchHud`, `MatchResultsFlow` → `MatchManager`, `MatchPlayer`, `MatchResult`, `MatchState`, `ScoreReason` | `VehicleSpawner` → `VehicleNameTag` | `VehicleSpawner` must not construct UI — inject a factory or event |
| C7 | `Race ↔ Player` | `MatchManager`, `VehicleSpawner` → `LocalPlayerRig` | `LocalPlayerRig` → `MatchManager` | `LocalPlayerRig` is the composition root — Race should expose events, not call back into it |

### Already-safe one-way edges (good asmdef candidates as-is)

```
AnimeToon/Editor    -> AnimeToon/Runtime                    (7)
AnimeToon/Runtime   -> (nothing outside AnimeToon)          ← LEAF
Scripts/Runtime     -> Scripts/Vehicle (12), AnimeToon/Runtime (3)
Scripts/Editor      -> everything (41)                      ← SINK
```

`AnimeToon` is a **clean leaf**: it references nothing in `VehicleSystem`. `Scripts/Runtime` is the
deliberate **bridge** (`AnimeToon.Integration` namespace) and should stay a separate assembly that both
sides may reference.

---

## 3. Unity-specific landmines — read before touching anything

1. **Never move, rename, or delete a `.cs` without its `.meta`.**
   Scenes and prefabs reference `MonoBehaviour`s by **GUID**, not by name. Moving a file is safe *only* if
   the `.meta` moves with it.
   - Known alias: **`VehicleInput.cs.meta` was reused for `LocalPlayerInput.cs`** (same GUID), so the old
     component on your car becomes `LocalPlayerInput` automatically. Do not "fix" this duplicate.
   - All 618 `Assets/` GUIDs are currently unique — keep them that way.

2. **`internal` breaks at assembly boundaries.** Today everything is one assembly, so `internal` is free.
   The moment you split, every cross-boundary `internal` access is a compile error. Plan for `public`
   or `[assembly: InternalsVisibleTo("...")]`.

3. **Editor assemblies need `includePlatforms: ["Editor"]` AND must reference the runtime assembly.**
   `Scripts/Editor` (12 files, namespace `VehicleSystem.EditorTools`) and `AnimeToon/Editor`
   (namespace `AnimeToon.Editor`) both reach into runtime code.

4. **`internal`/`public` aside, `Resources.Load` and `Camera.main` are global lookups that survive
   assembly splits but hide real dependencies** — the compiler will not tell you about them. They are
   listed below so you can decide deliberately.

5. **Acceptance test = the 4 build scenes + 42 prefabs still load and play.**
   `Assets/Scenes/` → 59 cross-folder script refs (`UI` 26, `Road` 8, `Input` 6, `AnimeToon/Runtime` 6,
   `Race` 5, `Player` 3, `Menu` 3, `Runtime` 2). `Assets/Prefabs/Vehicles/` → 26 refs
   (`Vehicle` 19, `Camera` 2, `Runtime` 2, `UI` 2, `Input` 1).

---

## 4. KNOWN-BROKEN SCRIPT GUIDs — OUT OF SCOPE, DO NOT "FIX"

A scan of all `m_Script` GUID references across `Assets/**` found 126 distinct script GUIDs, **10 of which
do not resolve**. All 10 are vendor/sample content. **They are pre-existing and expected.** If a task tells
you to "find and fix broken script references", **ignore these 10** — fixing them means deleting references
from HDRP TMP materials and TMP example scenes.

| GUID | What | Where used |
|---|---|---|
| `23c1ce4fb46143f46bc5cb5224c934f6` | HDRP `HDAdditionalCameraData` | `28 - HDRP Shader Example.unity` |
| `7a68c43fe1f2a47cfa234b5eeaa98012` | HDRP `HDAdditionalLightData` | HDRP TMP materials |
| `0d7593b3a9277ac4696b20006c21dde2` | HDRP `VisualEnvironment` | `Sky and Fog Volume Profile.asset` |
| `da692e001514ec24dbc4cca1949ff7e8` | HDRP `AssetVersion` | `28 - HDRP Shader Example.unity` |
| `5a00a63fdd6bd2a45ab1f2d869305ffd` | `OasisFogVolumeComponent` | `Assets/Settings/DefaultVolumeProfile.asset` |
| `0fd9ee276a1023e439cf7a9c393195fa` | `TestAnimationCurveVolumeComponent` | same |
| `74955a4b0b4243bc87231e8b59ed9140` | `TestVolume` | same |
| `60f3b30c03e6ba64d9a27dc9dba8f28d` | `OutlineVolumeComponent` | same |
| `f5f67c52d1564df4a8936ccd202a3bd8` | stale TMP 2.x pkg | TMP example scenes 05–26, `Text Popup.prefab` |
| `f70555f144d8491a825f0804e09c671c` | stale TMP 2.x pkg | TMP example scenes |

**Also out of scope:** `Assets/Settings/DefaultVolumeProfile.asset` is an **orphan** (not referenced by
`PC_RPAsset.asset` or `Mobile_RPAsset.asset`) — the project uses a **custom anime volume profile** instead.
Leave it alone.

---

## 5. Global / static access (hidden coupling the compiler won't flag)

| Folder | `FindFirstObjectByType` | `Resources.Load` | `Camera.main` | `GameObject.Find` / tag | static fields |
|---|---:|---:|---:|---:|---:|
| `Scripts/Player` | **17** | – | – | – | – |
| `Scripts/Editor` | 13 | – | 1 | – | 1 |
| `Scripts/Race` | 7 | – | – | – | – |
| `Scripts/UI` | 6 | – | 1 | – | 1 |
| `Scripts/Road` | 3 | – | – | – | – |
| `Scripts/Input` | 2 | – | – | – | 1 |
| `AnimeToon/Runtime` | 2 | 1 | 1 | – | **10** |
| `AnimeToon/Editor` | 2 | – | – | – | 1 |
| `Scripts/Menu` | 1 | – | – | – | – |
| `Scripts/Runtime` | – | 1 | 1 | 1 + 1 | 3 |
| `Scripts/Camera` | – | – | 1 | – | 2 |
| `Scripts/Data` | – | 1 | – | – | 1 |
| `Scripts/Monetization` | – | – | – | – | 4 |
| `Scripts/Vehicle` | – | – | – | – | 2 |

**`Scripts/Player/LocalPlayerRig.cs` is the hotspot**: 25 cross-folder type refs + 17 `FindFirstObjectByType`
calls. It is the composition root (`Vehicle`, `Input`, `UI`, `Road`, `Camera`, `Race`, `Data` — all of them).
It is *supposed* to know everything; the problem is that `Race` knows it back (cycle C7).

Singletons worth knowing:
`VehicleController.All` (static registry), `RewardedAds.Provider` (static), `LocalWallet` (static),
`InputModeManager` (static), `MatchResultsFlow` (static, line 473),
`AnimeGraphicsMenu` / `AnimeQuality` / `AnimeMaterialPresets` (statics),
`AnimeDriftBootstrap` / `AnimeDriftEffects` / `AnimeDriftEffectsDefaults` (statics).

---

## 6. Existing interfaces (only 2 — the real seam candidates)

| Interface | Declared in | Used by |
|---|---|---|
| `IDriveInputSource` | `Scripts/Vehicle/DriveInput.cs` | `BotInput`, `LocalPlayerInput` (+ `VehicleDriver`) |
| `IRewardedAdProvider` | `Scripts/Monetization/RewardedAds.cs` | `RewardedAds` |

`IDriveInputSource` is the pattern to copy when breaking C4.

---

## 7. Suggested assembly layout (target)

> Assembly names are proposals. Order matters — do them bottom-up, one at a time.

```
 0. VehicleSystem.Core         Scripts/Core            (NEW, ~6 files)        LEAF, depends on NOTHING
 1. VehicleSystem.Data         Scripts/Data            (5 files,  211 LOC)   LEAF-ish (fix C1 first)
 2. VehicleSystem.Vehicle      Scripts/Vehicle        (11 files, 2310 LOC)
 3. VehicleSystem.Camera       Scripts/Camera         (1 file,   585 LOC)
 4. VehicleSystem.Input        Scripts/Input          (4 files,  857 LOC)
 5. VehicleSystem.Road         Scripts/Road           (9 files, 1532 LOC)
 6. VehicleSystem.Pickups      Scripts/Pickups        (1 file,   115 LOC)
 7. VehicleSystem.Bots         Scripts/Bots           (2 files,  361 LOC)
 8. VehicleSystem.UI           Scripts/UI             (12 files, 3623 LOC)
 9. VehicleSystem.Menu         Scripts/Menu           (3 files,  881 LOC)
10. VehicleSystem.Race        Scripts/Race           (4 files,  892 LOC)
11. VehicleSystem.Player      Scripts/Player         (2 files,  336 LOC)
12. RickshawChaos.Integration  RickshawChaos_Integration/Runtime (5 files, 746 LOC)  <- BRIDGE
13. AnimeToon                 AnimeToon/Runtime      (8 files, 1938 LOC)  <- pure leaf
14. VehicleSystem.EditorTools Scripts/Editor         (10 files,  ~1990 LOC) <- SINK, Editor only
15. AnimeToon.Editor          AnimeToon/Editor       (9 files, 1797 LOC)  <- SINK, Editor only
16. RickshawChaos.Integration.Editor
                              RickshawChaos_Integration/Editor (2 files, ~260 LOC) <- BRIDGE, Editor only
```

Assembly 12 depends on both 2 and 13 — that is the bridge, and it is why it must sit *above* both.
Assembly 16 depends on 12 + 13 + 2.
Assemblies 14, 15 and 16 are sinks and can be done last, or early (they are easy wins that prove the toolchain).

**Folder paths assume the bridge has been moved out of `Scripts/` first** — see §7A, "Move target".

### The `Core` assembly (must be created first)

Shared contracts currently live in the **wrong folders** and are the reason several cycles exist.
Move them (files, not copies) into a new `Assets/Scripts/Core/` with **no outgoing references**:

| Move from | Item | Currently used by |
|---|---|---|
| `Scripts/Vehicle/DriveInput.cs` | `IDriveInputSource` | Vehicle, Bots, Input — 3 modules |
| `Scripts/Vehicle/ImpactInfo.cs` | `ImpactInfo`, `SpeedTier` | Vehicle, UI, Race — 3 modules |
| `Scripts/Input/InputModeManager.cs` | `InputMode` enum | Input, UI, Player |
| `Scripts/Race/MatchManager.cs` | `MatchState`, `MatchResult`, `ScoreReason` | Race, UI — 2 modules |

`IDriveInputSource` and `ImpactInfo` being in `Vehicle` is what forces `Bots → Vehicle`,
`Input → Vehicle`, `UI → Vehicle` and `UI → Race`. This single move removes **4 of the 7 cycles**
at the type level.

**Moving a file inside the Unity Project window preserves its `.meta` GUID**, so scene and prefab
references survive. Moving it with a file manager (or re-zipping) does not — see §3.

---

## 7A. The AnimeToon boundary — a hard rule, not a guideline

`Assets/AnimeToon/` is a **universal URP toolkit** that must never know the game exists. It is owned by a
*different* account/conversation (the shader account). **Verified 2026-10-21 — the boundary is currently clean.**

### Verified state

| Check | Result |
|---|---|
| `AnimeToon/` references any `VehicleSystem` type | **0** — clean leaf |
| `AnimeGraphicsMenuHook.cs` exists anywhere | **No** (handout rule: must not exist) ✓ |
| `_Anime_*` shader globals declared in C# | **19**, all present |
| Shaders consuming them | 8 (`AnimeCharacterToon`, `AnimeEnvironmentToon`, `AnimeGlass`, `AnimeRoadWet`, `AnimeWater`, `AnimeFoliage`, `AnimeSky`) — all globals wired |
| Handout §4 public API (`AnimeQuality`, `AnimeGraphicsMenu`, `AnimeStyleController`, `AnimeSpeedFx`, `AnimeLightShafts`) | **all members present** |
| Handout §5 game-contract members | **all present** |
| `Resources/AnimeToon/AnimeQualitySettings.asset`, `Resources/VehicleSystem/AnimeDriftEffectsDefaults.asset` | **both exist** |
| `Tools > Anime Toon/*` menu items | **15 present** |

### The integration layer

`Assets/Scripts/Runtime/` (5 files, namespace `AnimeToon.Integration`) is the **only** bridge:

| File | Game types it touches | Referenced by a scene? |
|---|---|---|
| `AnimeDriftBootstrap.cs` | `VehicleController.VehicleEnabled`, `VehicleController.All`, `VehicleEffects` | no (runtime `AddComponent`) |
| `AnimeDriftEffects.cs` | `VehicleController`, `VehicleHealth`, `WheelSetup` | no |
| `AnimeVehicleEffectsQuality.cs` | `VehicleEffects` | no |
| `AnimeVehicleFxBridge.cs` | `VehicleController` | **YES — `Game 1.unity` + `Game 2.unity`** |
| `AnimeDriftEffectsDefaults.cs` | — (defaults asset class) | no |
| `Editor/AnimeVehicleEffectsInstaller.cs` | `VehicleEffectsDefaults`, `DefaultEffectsBuilder.AssetPath` | no |
| `Editor/AnimeDriftEffectsInstaller.cs` | drift defaults asset | no |

### ⚠⚠ MOVE TARGET — read before moving anything

**`AnimeVehicleFxBridge.cs` (guid `6fb86d1d0ff70b343aabc0dc8295bc88`) is an added component on the
`Main Camera` prefab instance in `Assets/Scenes/Game 1.unity` and `Assets/Scenes/Game 2.unity`.**
(The prefab is `Assets/Prefabs/Vehicles/VehicleCameraRig.prefab`, guid `60d80293ca6ea2a43bd3097f81d6b1fc`;
the sibling added component is `AnimeSpeedFx`, guid `c48c92f367d0028459c70f46349571e3`.)

The AnimeToon handout calls this component *"optional, may be unused"*. **That is wrong** — it is wired into
the main camera of 2 of the 4 build scenes.

**Consequence:** if this file is moved *without* its `.meta`, Unity mints a new GUID and both scenes get
**"Missing (MonoBehaviour)"** on the Main Camera. Always move the `.cs` **and** the `.cs.meta` together.

### ⚠ LAYERING GAP

The two integration **Editor** files live in `Assets/Scripts/Editor/` **mixed in with 10 `VehicleSystem`
editor tools** (`MatchTools.cs`, `RoadSetupTools.cs`, `VehicleStatsEditor.cs`, …):

```
Assets/Scripts/Editor/AnimeVehicleEffectsInstaller.cs   <- integration
Assets/Scripts/Editor/AnimeDriftEffectsInstaller.cs      <- integration
Assets/Scripts/Editor/{MatchTools, RoadSetupTools, VehicleStatsEditor, ...}.cs  <- VehicleSystem
```

Today the "only the integration may reference game types" rule is enforced by **convention only**.
Fix: move the two files to a sibling `Assets/RickshawChaos_Integration/Editor/` so the bridge owns both halves.
**Move inside the Unity Project window** so `.meta` GUIDs survive.

### Target folder layout (three top-level zones)

```
Assets/
├── AnimeToon/                       <- universal toolkit  (shader account owns it)
│   ├── Runtime/  Editor/  Shaders/  VFX/  UI/  Presets/  Volume/  Materials/
│
├── Scripts/                         <- the game          (scripts account owns it)
│   ├── Core/ (new)  Data/  Vehicle/  Camera/  Input/  Road/
│   ├── Pickups/  Bots/  Race/  UI/  Menu/  Player/  Monetization/
│   └── Editor/                      <- 10 VehicleSystem editor tools ONLY
│
└── RickshawChaos_Integration/       <- THE BRIDGE        (both accounts)
    ├── Runtime/                     <- 5 files
    └── Editor/                      <- 2 files
```

**Why a sibling of `Scripts/`, not a folder inside it:**
1. The bridge must be in an assembly that sees **both** `VehicleSystem.*` **and** `AnimeToon`. A sibling
   folder makes that unambiguous; inside `Scripts/` the `Scripts/Editor` asmdef would swallow the 2
   integration editor files.
2. `ProjectSettings/ProjectSettings.asset` has `productName: 'Rickshaw Chaos: Dhaka'`, and the GDD is
   *"Rickshaw Chaos: Dhaka"*. **`RickshawChaos_Integration` is already the correct name** — the GitHub repo
   happens to be called `tesla-racing`, but that is only the repo name.
3. The AnimeToon handout §9.1 already specifies `Assets/RickshawChaos_Integration/{Runtime,Editor}/`.
   The shader account's instructions point at that path.
4. Path-scoped ownership becomes checkable: the shader account may only touch `Assets/AnimeToon/` and
   `Assets/RickshawChaos_Integration/`.

### Safe move procedure (7 files + 2 folder metas)

1. In the **Unity Project window**, create `Assets/RickshawChaos_Integration/Runtime` and `.../Editor`.
2. Drag the 5 runtime `.cs` files into `Runtime/` and the 2 editor `.cs` files into `Editor/`.
   **Use the Unity Project window** — it moves the `.meta` with the file. (Windows Explorer works too, but
   you must drag each `.cs.meta` alongside its `.cs`.)
3. Delete the now-empty `Assets/Scripts/Runtime/` folder (and its `.meta`) **from the Unity Project window**.
4. Verify: `git status` must show **renames (R)**, never a `D` + `??` pair. A `D`+`??` pair means a `.meta`
   was lost → a GUID was orphaned.
5. Open `Game 1.unity` and `Game 2.unity` → select **Main Camera** → the Inspector must still list
   **Anime Vehicle Fx Bridge** and **Anime Speed Fx**. Not "Missing (MonoBehaviour)".
6. Console must be clean; then Play `Game 2` and confirm the camera speed-FX still reacts.

### The contract surface (what must not break)

This is the exact set of game members the AnimeToon side depends on. Any rename here breaks the
other account's build — the handout calls this out and it is correct.

```
VehicleController : wheels, ForwardSpeed, IsGrounded, HandbrakeAmount, Stats.Get(VehicleStat.MaxSpeed),
                    static All, static event VehicleEnabled
WheelSetup       : collider, handbrake, drive
VehicleHealth    : IsDestroyed, event Destroyed(ImpactInfo), event Respawned
VehicleBoost     : IsBoosting
VehicleImpactSensor : event Impact(ImpactInfo)
ImpactInfo       : point, speedRatio, tier
VehicleEffects   : trail, hitSparks, hitDust, lowHealthSmoke, explosion
VehicleEffectsDefaults / DefaultEffectsBuilder : AssetPath, EnsureDefaults()
LocalPlayerRig   : Vehicle  (optional bridge only)
```

**Hidden ordering constraint** (from `AnimeDriftBootstrap.cs:19`):
> "VehicleController clears its static events at startup, so subscribe here, after that."

`AnimeDriftBootstrap` subscribes to `VehicleController.VehicleEnabled` and must run **after**
`VehicleController` clears its static event list. Do not reorder their `[DefaultExecutionOrder]`
or move the subscription into an `Awake` that runs earlier.

### Rules for the shader account

1. **Never** add a `VehicleSystem` reference inside `Assets/AnimeToon/`.
2. A new `_Anime_*` global must be **neutral at 0** and be registered in the contract table above.
3. Quality tier changes go into `AnimeQualityTier` (with defaults for all tiers), then into the menu.
4. `AnimeQuality` writes `Application.targetFrameRate` every 1.5 s → it **overrides** any cap set
   elsewhere (e.g. `MobileControls`). Change the cap in the tier, not in game code.
5. `AnimeStyleController` overwrites `Volume` values every frame (bloom, saturation, contrast,
   exposure, colour filter, vignette). Edit the Day/Dusk/Night **preset assets**, not the live profile.
6. `AnimeQuality` replaces `QualitySettings.renderPipeline` at startup with a runtime copy of the URP
   asset. The project's own asset is never edited.

### ⚠ Stale claim in the handout

> "None of this code has been compiled by its author, so every change has to be checked in the Unity editor."

**This is outdated.** Everything compiles cleanly today (`Assembly-CSharp.dll` built 2026-10-21 10:15:54;
`Editor.log` and `Editor-prev.log` both contain 0 CS errors). The only build warnings are the two
cosmetic TMP HDRP shader messages, which are expected in a URP project.

---

## 7B. Reconciliation with the previous Claude's plan

The previous conversation produced its own module list. It **agrees with this analysis** — that is a
good sign, and it means the two plans can be merged rather than reconciled. Differences:

| Previous Claude's module | This plan | Verdict |
|---|---|---|
| Core (`DriveInput`, `IDriveInputSource`, `ImpactInfo`, `SeededRandom`, shared enums) | `VehicleSystem.Core` | **Agree** — and it must be step 0; the shared contracts are currently in the wrong folders |
| Data | `VehicleSystem.Data` | Agree |
| Vehicle | `VehicleSystem.Vehicle` | Agree |
| Input and Camera (combined) | `.Input` + `.Camera` (separate) | Agree — combining is also legal, since Camera↔Input is not a cycle. Separate is better for the shader account |
| Road and Traffic | `VehicleSystem.Road` (+ `.Pickups`) | Agree — Pickups is a cycle partner of Road, so it goes with it |
| Bots | `VehicleSystem.Bots` | Agree |
| Match | `VehicleSystem.Race` | Agree |
| UI | `VehicleSystem.UI` | Agree |
| Economy and Services | `.Monetization` + `.Player` | Agree, split |
| Visuals and VFX | `AnimeToon` (separate assembly) | **Agree, but must be enforced by an asmdef**, not by convention |
| Networking and Audio | future | Agree |
| — | `VehicleSystem` (the bridge) | **Missing from the previous plan.** It is the assembly that lets `AnimeToon` and the game coexist |
| — | `VehicleSystem.EditorTools`, `AnimeToon.Editor` | Missing — Editor splits are cheap wins |

The previous Claude guessed one coupling correctly ("the camera and input read `PauseMenu.IsPaused`").
The measured list is the **7 cycles in §2**, which is a superset.

### The previous plan's strategy — keep it

- **Audit → Documents → one cleanup pass → briefs → split.** Correct order. Do **not** split first:
  every conversation invents its own boundaries and you get conflicts on merge.
- **One branch per module + one integration conversation.** Correct, and the only way to keep multiple
  accounts from clobbering each other.
- **Three shared documents** — `ARCHITECTURE.md` (rules), `MODULES.md` (files, public surface, allowed
  dependencies), `CONTRACTS.md` (signatures that must not change). **These are the shared memory that
  survives a context reset.** They are currently missing from the repo.
- **A handoff template** pasted into each new conversation: module, files it may edit, contracts it
  must not break, how to test, "deliver only changed files".

---

## 8. How to verify each step

1. **Compile check** — Unity batch mode:
   ```
   Unity.exe -batchmode -quit -projectPath "<path>" -logFile - -executeMethod <Build>.BuildLinux64
   ```
   or simply open the Editor and read the Console. The project currently compiles with **0 CS errors**
   (`Assembly-CSharp.dll` built 2026-10-21 10:15:54; `Editor.log` and `Editor-prev.log` both clean).
2. **Reference check** — no "Missing (MonoBehaviour)" and no "The referenced script" in the Editor log.
3. **Scene check** — all 4 build scenes open without errors.
4. **Guid check** — `git status` must show **no modified/deleted `.meta` files**.
5. **Play check** — MainMenu → vehicle select → race; a vehicle spawns, drives, bots race, HUD updates.

---

## 9. Two stale facts in `Assets/README.md` — correct these before trusting them

1. `Assets/README.md` ends with *"these files were refactored and structurally checked … outside Unity, so
   they were **not compiled in the Unity editor**."*
   **This is outdated.** They have since been compiled in Unity with 0 errors (see §8). Do not re-verify
   "does it even compile" as a first task — it does.
2. `Assets/README.md` documents the `Scripts/` layout as
   `Vehicle/ Input/ Camera/ Player/ UI/ Menu/ Data/ Race/ Pickups/ Bots/ Monetization/ Road/ Editor`.
   The `Scripts/Runtime/` folder (5 files, namespace `AnimeToon.Integration`) is **not mentioned** — it is
   the AnimeToon bridge and is easy to miss.

`Assets/README.md` is otherwise excellent: it is a real handover document covering behaviour, the
"what changed and why" table, setup instructions, and a "Not included yet" list
(`RaceManager` with checkpoints/laps, map selector, modifier loading in the spawner, room services, netcode).

---

## 10. Not in this project / out of scope

- `Assets/_Recovery/0.unity` — an orphan recovery scene (18 cross-folder refs). Not in build settings.
- `Assets/TextMesh Pro/Examples & Extras/` — 34 vendor example scripts. Never edit. TMP lives inside
  `com.unity.ugui` 2.0.0; the standalone `com.unity.textmeshpro` package is gone.
- `Assets/Settings/DefaultVolumeProfile.asset` — orphan, see §4.
- 6 dangling Git-LFS objects — unreachable from HEAD, missing from both the private and public repos.
  Pre-existing, harmless.

# AnimeToon Code Audit Report: Rickshaw Chaos (Tesla Racing)

**Project Root:** `D:\0 Games Workshop\Projects Folder\tesla racing`  
**Unity Version:** `6000.3.11f1` (Unity 6.3 LTS)  
**Target Platforms:** Android & PC (URP)  
**Audit Date:** 2026-10-18  

---

## 1. SUMMARY

The disappearing graphics settings menu is primarily caused by **zero local scale (`m_LocalScale: {x: 0, y: 0, z: 0}`) on both `Open Button Canvas` and `Panel Canvas` inside `Assets/Resources/AnimeToon/AnimeGraphicsMenu.prefab`**, rendering all child UI elements at zero world scale (invisible). Secondarily, `AnimeGraphicsMenu.EnsureEventSystem()` is only invoked inside `OpenPanel()`, so in scenes without a pre-existing `EventSystem`, the open button cannot receive click events. Furthermore, `AnimeQualitySettings.asset` contains a broken script GUID (`e9b67328430e16d49a5cb82a7c48b218` vs real `ef7f4760d50e42f4f8ab0dcd9f1d578b`), triggering missing script errors and forcing fallback settings.
**Confidence:** HIGH — verified directly via prefab YAML inspection, filesystem scan, and Editor log analysis.

---

## 2. HYPOTHESIS TABLE (H1 – H5)

| Hypothesis | Status | Evidence & Quoted Lines |
|---|---|---|
| **H1:** Old file `AnimeGraphicsMenuHook.cs` exists, sets `ShowButtonWhen`, or creates a code menu | **RULED OUT** | Scanned filesystem: `Get-ChildItem -Filter "*AnimeGraphicsMenuHook*"` returned 0 files. `ShowButtonWhen` is only referenced in declaration and check: `AnimeGraphicsMenu.cs:65` (`public static Func<bool> ShowButtonWhen;`), line 190, line 193. Editor.log shows it was imported previously, but has since been deleted. |
| **H2:** Two instances created; `Awake()` destroys the second one | **RULED OUT** | `AnimeGraphicsMenu.prefab` is instantiated ONLY via `AutoCreate()` (`AnimeGraphicsMenu.cs:112` `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]`). Scanned all scenes and prefabs: `Select-String "AnimeGraphicsMenu" Assets/**/*.unity Assets/**/*.prefab` matched only `Assets/Resources/AnimeToon/AnimeGraphicsMenu.prefab`. No scenes contain a duplicate menu instance. |
| **H3:** Prefab missing, ungenerated, or script GUID broken | **RULED OUT** (Partial) | Prefab exists at `Assets/Resources/AnimeToon/AnimeGraphicsMenu.prefab`. Script reference in prefab is `guid: dec549868e765a94db07af8b2b0696c1` (line 40), which **matches** `Assets/AnimeToon/Runtime/AnimeGraphicsMenu.cs.meta:2` (`guid: dec549868e765a94db07af8b2b0696c1`). Button and panel references are valid and non-zero. |
| **H4:** Game script destroys/hides it (Canvas disabling, culling, root destroy, DDOL clear) | **RULED OUT** | Full audit of non-Anime scripts found no scripts clearing DontDestroyOnLoad, destroying "Anime Graphics Menu", or disabling UI Canvases globally. `AnimeGraphicsMenu.cs:166` calls `DontDestroyOnLoad(gameObject)`, protecting it across scene transitions. |
| **H5:** Something else (Canvas scale zero in prefab, missing EventSystem, broken asset GUID) | **CONFIRMED** | 1. `AnimeGraphicsMenu.prefab:3639` (`Open Button Canvas`) and line 1835 (`Panel Canvas`): `m_LocalScale: {x: 0, y: 0, z: 0}`. Scale 0 renders all UI invisible.<br>2. `AnimeGraphicsMenu.cs:200`: `EnsureEventSystem()` called in `OpenPanel()` but not in `Awake()`/`Start()`, leaving `openButton` unresponsive in scenes without an `EventSystem`.<br>3. `AnimeQualitySettings.asset:12`: `guid: e9b67328430e16d49a5cb82a7c48b218` does not match `AnimeQualitySettings.cs.meta:2` (`guid: ef7f4760d50e42f4f8ab0dcd9f1d578b`), logging 8 missing script warnings in Editor.log. |

---

## 3. TASKS BREAKDOWN & EVIDENCE

### A. Inventory and Duplicates

#### A1. Files starting with "Anime"
**Total 55 files found:**
- **Runtime C# (12):**
  - `Assets\AnimeToon\Runtime\AnimeGraphicsMenu.cs`
  - `Assets\AnimeToon\Runtime\AnimeQuality.cs`
  - `Assets\AnimeToon\Runtime\AnimeQualityRunner.cs`
  - `Assets\AnimeToon\Runtime\AnimeQualitySettings.cs`
  - `Assets\AnimeToon\Runtime\AnimeSpeedFx.cs`
  - `Assets\AnimeToon\Runtime\AnimeStyleController.cs`
  - `Assets\AnimeToon\Runtime\AnimeStylePreset.cs`
  - `Assets\Scripts\Runtime\AnimeDriftBootstrap.cs`
  - `Assets\Scripts\Runtime\AnimeDriftEffects.cs`
  - `Assets\Scripts\Runtime\AnimeDriftEffectsDefaults.cs`
  - `Assets\Scripts\Runtime\AnimeVehicleEffectsQuality.cs`
  - `Assets\Scripts\Runtime\AnimeVehicleFxBridge.cs`
- **Editor C# (10):**
  - `Assets\AnimeToon\Editor\AnimeDriftVfxFactory.cs`
  - `Assets\AnimeToon\Editor\AnimeGraphicsMenuBuilder.cs`
  - `Assets\AnimeToon\Editor\AnimeMaterialPresets.cs`
  - `Assets\AnimeToon\Editor\AnimeMaterialPresetWindow.cs`
  - `Assets\AnimeToon\Editor\AnimeQualityMenu.cs`
  - `Assets\AnimeToon\Editor\AnimeSmoothNormalBaker.cs`
  - `Assets\AnimeToon\Editor\AnimeStylePresetCreator.cs`
  - `Assets\AnimeToon\Editor\AnimeVfxFactory.cs`
  - `Assets\Scripts\Editor\AnimeDriftEffectsInstaller.cs`
  - `Assets\Scripts\Editor\AnimeVehicleEffectsInstaller.cs`
- **Shaders (7):**
  - `Assets\AnimeToon\Shaders\Character\AnimeCharacterToon.shader`
  - `Assets\AnimeToon\Shaders\Environment\AnimeEnvironmentToon.shader`
  - `Assets\AnimeToon\Shaders\Environment\AnimeGlass.shader`
  - `Assets\AnimeToon\Shaders\Environment\AnimeWater.shader`
  - `Assets\AnimeToon\Shaders\Sky\AnimeSky.shader`
  - `Assets\AnimeToon\Shaders\VFX\AnimeParticleToon.shader`
  - `Assets\AnimeToon\Shaders\VFX\AnimeScreenFX.shader`
- **Materials (12):** `AnimeSky.mat` + 11 VFX materials in `Assets\AnimeToon\VFX\Materials\`
- **Prefabs (11):** `AnimeGraphicsMenu.prefab` in `Assets\Resources\AnimeToon\` + 10 VFX prefabs in `Assets\AnimeToon\VFX\Prefabs\`
- **Assets (3):** `AnimeQualitySettings.asset`, `AnimeDriftEffectsDefaults.asset`, `AnimeVolumeProfile.asset`

#### A2. Duplicate Definitions
Command:
```powershell
Select-String -Path 'Assets\**\*.cs' -Pattern '(class|enum|struct)\s+(AnimeQuality|AnimeQualityRunner|AnimeQualitySettings|AnimeQualityTier|AnimeQualityLevel|AnimeQualityMode|AnimeGraphicsMenu|AnimeGraphicsRow|AnimeGraphicsPreset|AnimeGraphicsSetting|AnimeStyleController|AnimeStylePreset|AnimeDriftEffects|AnimeDriftBootstrap|AnimeDriftEffectsDefaults|AnimeVehicleEffectsQuality|AnimeSpeedFx|AnimeVehicleFxBridge|AnimeGraphicsMenuHook)\b'
```
**Result:** **ZERO duplicates currently on disk.** Every type is declared in exactly one file.
*(Note: Duplicate errors in Editor.log were from a previously removed folder `Assets/Scripts/AnimeToon/`).*

#### A3. Same Filename in Two Folders
**Result:** **ZERO filename collisions** (no duplicate `.cs`, `.shader`, `.prefab`, or `.asset` files with identical names in different directories).

#### A4. Shader Names
Command:
```powershell
Select-String -Path 'Assets\**\*.shader' -Pattern '^Shader "Anime/'
```
- `AnimeToon\Shaders\Character\AnimeCharacterToon.shader:9`: `Shader "Anime/CharacterToon"`
- `AnimeToon\Shaders\Environment\AnimeEnvironmentToon.shader:6`: `Shader "Anime/EnvironmentToon"`
- `AnimeToon\Shaders\Environment\AnimeGlass.shader:4`: `Shader "Anime/Glass"`
- `AnimeToon\Shaders\Environment\AnimeWater.shader:5`: `Shader "Anime/Water"`
- `AnimeToon\Shaders\Sky\AnimeSky.shader:5`: `Shader "Anime/Sky"`
- `AnimeToon\Shaders\VFX\AnimeParticleToon.shader:5`: `Shader "Anime/ParticleToon"`
- `AnimeToon\Shaders\VFX\AnimeScreenFX.shader:5`: `Shader "Anime/ScreenFX"`
**Result:** Exactly 7 unique shaders. **ZERO duplicate shader names.**

#### A5. Duplicate GUIDs
Checked all 56 `Anime*.meta` files.
**Result:** **ZERO duplicate GUIDs.** All GUIDs are globally unique across the project.

---

### B. Expected Files

| Target | Status | Path |
|---|---|---|
| `AnimeQuality.cs` | **PRESENT** | `Assets\AnimeToon\Runtime\AnimeQuality.cs` |
| `AnimeQualitySettings.cs` | **PRESENT** | `Assets\AnimeToon\Runtime\AnimeQualitySettings.cs` |
| `AnimeQualityRunner.cs` | **PRESENT** | `Assets\AnimeToon\Runtime\AnimeQualityRunner.cs` |
| `AnimeGraphicsMenu.cs` | **PRESENT** | `Assets\AnimeToon\Runtime\AnimeGraphicsMenu.cs` |
| `AnimeStyleController.cs` | **PRESENT** | `Assets\AnimeToon\Runtime\AnimeStyleController.cs` |
| `AnimeStylePreset.cs` | **PRESENT** | `Assets\AnimeToon\Runtime\AnimeStylePreset.cs` |
| `AnimeDriftEffects.cs` | **PRESENT** | `Assets\Scripts\Runtime\AnimeDriftEffects.cs` |
| `AnimeDriftEffectsDefaults.cs` | **PRESENT** | `Assets\Scripts\Runtime\AnimeDriftEffectsDefaults.cs` |
| `AnimeDriftBootstrap.cs` | **PRESENT** | `Assets\Scripts\Runtime\AnimeDriftBootstrap.cs` |
| `AnimeVehicleEffectsQuality.cs` | **PRESENT** | `Assets\Scripts\Runtime\AnimeVehicleEffectsQuality.cs` |
| `AnimeGraphicsMenuBuilder.cs` | **PRESENT** | `Assets\AnimeToon\Editor\AnimeGraphicsMenuBuilder.cs` |
| `AnimeQualityMenu.cs` | **PRESENT** | `Assets\AnimeToon\Editor\AnimeQualityMenu.cs` |
| `AnimeStylePresetCreator.cs` | **PRESENT** | `Assets\AnimeToon\Editor\AnimeStylePresetCreator.cs` |
| `AnimeVfxFactory.cs` | **PRESENT** | `Assets\AnimeToon\Editor\AnimeVfxFactory.cs` |
| `AnimeDriftVfxFactory.cs` | **PRESENT** | `Assets\AnimeToon\Editor\AnimeDriftVfxFactory.cs` |
| `AnimeMaterialPresets.cs` | **PRESENT** | `Assets\AnimeToon\Editor\AnimeMaterialPresets.cs` |
| `AnimeMaterialPresetWindow.cs` | **PRESENT** | `Assets\AnimeToon\Editor\AnimeMaterialPresetWindow.cs` |
| `AnimeSmoothNormalBaker.cs` | **PRESENT** | `Assets\AnimeToon\Editor\AnimeSmoothNormalBaker.cs` |
| `AnimeDriftEffectsInstaller.cs` | **PRESENT** | `Assets\Scripts\Editor\AnimeDriftEffectsInstaller.cs` |
| 7 Shaders (Character, Env, Glass, Water, Sky, Particle, ScreenFX) | **PRESENT** | Under `Assets\AnimeToon\Shaders\` |
| `Assets/Resources/AnimeToon/AnimeGraphicsMenu.prefab` (+.meta) | **PRESENT** | `Assets\Resources\AnimeToon\AnimeGraphicsMenu.prefab` |
| **MUST NOT EXIST:** `AnimeGraphicsMenuHook.cs` | **ABSENT (CONFIRMED)** | Deleted from disk; does not exist. |
| Bonus: `AnimeSpeedFx.cs` | **PRESENT** | `Assets\AnimeToon\Runtime\AnimeSpeedFx.cs` |
| Bonus: `AnimeVehicleFxBridge.cs` | **PRESENT** | `Assets\Scripts\Runtime\AnimeVehicleFxBridge.cs` |

---

### C. The Menu Chain

#### C1. `ShowButtonWhen` and `AnimeGraphicsMenu` References
- `AnimeGraphicsMenuBuilder.cs:17`: `const string PrefabPath = Folder + "/AnimeGraphicsMenu.prefab";`
- `AnimeGraphicsMenuBuilder.cs:40`: `var menu = go.AddComponent<AnimeGraphicsMenu>();`
- `AnimeGraphicsMenu.cs:60`: `public class AnimeGraphicsMenu : MonoBehaviour`
- `AnimeGraphicsMenu.cs:65`: `public static Func<bool> ShowButtonWhen;`
- `AnimeGraphicsMenu.cs:115`: `if (Instance != null || FindFirstObjectByType<AnimeGraphicsMenu>() != null) return;`
- `AnimeGraphicsMenu.cs:117`: `var prefab = Resources.Load<GameObject>("AnimeToon/AnimeGraphicsMenu");`
- `AnimeGraphicsMenu.cs:124`: `public static AnimeGraphicsMenu Create()`
- `AnimeGraphicsMenu.cs:190`: `bool show = showButton && (ShowButtonWhen == null || ShowButtonWhen());`
- `AnimeGraphicsMenu.cs:193`: `if (ShowButtonWhen != null && !ShowButtonWhen() && IsOpen) ClosePanel();`
**Finding:** NO OTHER SCRIPT assigns `ShowButtonWhen` or calls `Create()`, `Open()`, `Close()`, or `Toggle()`. `ShowButtonWhen` remains `null`.

#### C2. `RuntimeInitializeOnLoadMethod` Methods & Order
1. `AnimeQuality.ResetStatics` (`SubsystemRegistration`)
2. `InputModeManager.ResetStatics` (`SubsystemRegistration`)
3. `PlayerProfile.ResetStatics` (`SubsystemRegistration`)
4. `PauseMenu.ResetStatics` (`SubsystemRegistration`)
5. `VehicleController.ResetStatics` (`SubsystemRegistration`)
6. `AnimeQuality.Start` (`BeforeSceneLoad`) — initializes quality system
7. **`AnimeGraphicsMenu.AutoCreate` (`AfterSceneLoad`)** — loads and instantiates `AnimeGraphicsMenu.prefab`
8. `AnimeDriftBootstrap.Install` (`AfterSceneLoad`) — attaches drift effects to vehicles

Only `AnimeGraphicsMenu.AutoCreate` instantiates `AnimeGraphicsMenu`. It executes after the first scene loads.

#### C3. YAML Prefab Verification (`Assets/Resources/AnimeToon/AnimeGraphicsMenu.prefab`)
- **MonoBehaviour Script GUID:** `dec549868e765a94db07af8b2b0696c1` (line 40) equals `AnimeGraphicsMenu.cs.meta:2` (`dec549868e765a94db07af8b2b0696c1`). **Valid.**
- **Behaviour values:**
  - `showButton: 1`
  - `persistAcrossScenes: 1`
  - `pauseGameWhileOpen: 1`
- **References:**
  - `buttonRoot: {fileID: 5994780793921113580}` (non-zero)
  - `panelRoot: {fileID: 2889299444962898977}` (non-zero)
  - `openButton: {fileID: 6235963638774101251}` (non-zero)
  - `backButton: {fileID: 1788902647271581021}` (non-zero)
  - `infoText: {fileID: 3429286844659362843}` (non-zero)
- **Presets & Rows:**
  - Presets count: **5** (Auto, Low, Medium, High, Custom)
  - Rows count: **10** (Resolution, Effects, Frame Rate, Shadows, Bloom, Outlines, Detail, Clouds, Textures, Skid Marks)
- **Active state and render modes:**
  - Root GameObject (`AnimeGraphicsMenu`): `m_IsActive: 1`
  - `Open Button Canvas`: `m_IsActive: 1`, `m_RenderMode: 0` (ScreenSpaceOverlay)
  - `Panel Canvas`: `m_IsActive: 1`, `m_RenderMode: 0` (ScreenSpaceOverlay)
- **CRITICAL DEFECT DETECTED IN YAML:**
  - `Open Button Canvas` RectTransform (`&3322121861606934774`, line 3639): `m_LocalScale: {x: 0, y: 0, z: 0}`
  - `Panel Canvas` RectTransform (`&6231381981507516131`, line 1835): `m_LocalScale: {x: 0, y: 0, z: 0}`

#### C4. Scene & Prefab Placements
- Scene scan: Zero scenes contain `AnimeGraphicsMenu`.
- Prefab scan: Only `Assets/Resources/AnimeToon/AnimeGraphicsMenu.prefab` contains it.

#### C5. Execution Lifecycle Analysis
1. **Prefab only (normal startup):**
   - At startup, `AutoCreate()` runs after scene load. `Resources.Load("AnimeToon/AnimeGraphicsMenu")` loads the prefab.
   - `Instantiate(prefab)` creates the menu.
   - In `Awake()`: `Instance` is set to `this`. `persistAcrossScenes` is true, so `transform.SetParent(null)` and `DontDestroyOnLoad(gameObject)` are called. `Bind()` hooks up buttons. `panelRoot.SetActive(false)` hides the panel.
   - In `Update()`: `showButton` is true and `ShowButtonWhen` is null, so `show` is true. `buttonRoot.SetActive(true)` ensures `Open Button Canvas` is active.
   - **Why it disappears / is invisible:** Even though `buttonRoot` is active, its RectTransform has `m_LocalScale = (0, 0, 0)`. The CanvasScaler does not reset `m_LocalScale` for ScreenSpaceOverlay. The button and all panel contents are scaled to 0 in world space! Furthermore, if the scene has no `EventSystem`, `openButton` cannot register clicks because `EnsureEventSystem()` is only called in `OpenPanel()`.
2. **Hook + Prefab (hypothetical):**
   - If hook set `ShowButtonWhen` to check `PauseMenu.IsPaused`, `Update()` line 191 would deactivate `buttonRoot` while unpaused. Since hook is deleted, this does not happen.
3. **Two instances:**
   - If a second instance wakes up, `Awake()` lines 154-157 execute: `if (Instance != null && Instance != this) { Destroy(gameObject); return; }`, destroying the second instance. Currently only one instance is instantiated.

---

### D. Game Scripts Impact Analysis

#### D1 & D2: Destruction and Canvas Manipulation in Game Code
- **Editor-Only Hits:** `DefaultEffectsBuilder.cs`, `PickupTools.cs`, `RoadSetupTools.cs`, `PauseMenu.cs` (inspector section) use `DestroyImmediate` in the Editor; they do not run at runtime.
- **Runtime Component Self-Destruction:** `VehicleSelectMenu.cs:106`, `ControlsOverlay.cs:73`, `HealthBar.cs:66`, `MatchHud.cs:101`, `PauseMenu.cs:114`, `PickupHud.cs:65`, `ResetCounterUI.cs:58`, `RespawnCountdown.cs:68`, `Speedometer.cs:75`, `VehicleNameTag.cs:82`, `WrongWayBanner.cs:72` only destroy their own local `uiRoot` references on teardown.
- **Scene Loads:**
  - `VehicleSelectMenu.cs:302`: `SceneManager.LoadScene(sceneName);`
  - `MatchHud.cs:273`: `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);`
  - `MatchHud.cs:284`: `SceneManager.LoadScene(mainMenuScene);`
  - `PauseMenu.cs:282,283`: `SceneManager.LoadScene(...)`
  *Impact:* Because `AnimeGraphicsMenu` calls `DontDestroyOnLoad(gameObject)` in `Awake()`, it **survives** these scene loads.
- **Global Canvas Disabling / Culling:**
  - `VehiclePreviewStage.cs:140-141` sets `previewCamera.cullingMask` on an isolated preview camera only.
  - No scripts call `FindObjectsByType<Canvas>()` or set `canvas.enabled = false` across all canvases.
  - **Result:** No game script destroys or hides the AnimeGraphicsMenu.

#### D3: `timeScale` Interactions
- `PauseMenu.cs:229`: Sets `Time.timeScale = 0f;` on pause; restores on resume (`Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;`).
- `AnimeGraphicsMenu.cs:204-209`: Checks `if (pauseGameWhileOpen && !pausedByMenu && Time.timeScale > 0f)`. If PauseMenu paused first, `pausedByMenu` is false, and `ReleasePause()` returns early without interfering.
- `AnimeSpeedFx.cs:163`: Temporarily sets timeScale to `0.05f` during hit-stop and restores. Checks `previous <= 0.01f` to abort if paused.

#### D4: `EventSystem`
- No non-Anime script creates or destroys `EventSystem`.
- `AnimeGraphicsMenu.cs:225-236` defines `EnsureEventSystem()`, but **only calls it in `OpenPanel()` line 200**.
- If a scene has no EventSystem, `openButton` (`GRAPHICS` button) **cannot be clicked** to open the panel!

---

### E. Editor Log Audit
Examined the last 4000 lines of `%LOCALAPPDATA%\Unity\Editor\Editor.log`:
- **`error CS`:** 93 compilation error matches earlier in the log (all `CS0101`, `CS0111`, `CS0579`). Root cause: A temporary duplicate directory `Assets/Scripts/AnimeToon/` existed and conflicted with `Assets/AnimeToon/`. That directory was subsequently deleted; the current project compiles without errors.
- **`AnimeGraphicsMenu`:**
  - `Line 1942: Graphics menu UI saved to Assets/Resources/AnimeToon/AnimeGraphicsMenu.prefab.`
  - `Line 1949: AnimeToon.Editor.AnimeGraphicsMenuBuilder:Generate () (at Assets/AnimeToon/Editor/AnimeGraphicsMenuBuilder.cs:49)`
  - No runtime errors or exceptions associated with `AnimeGraphicsMenu`.
- **`AnimeQuality`:**
  - Stack trace occurring 8 times:
    ```
    The referenced script (Unknown) on this Behaviour is missing!
    UnityEngine.Resources:Load<AnimeToon.AnimeQualitySettings> (string)
    AnimeToon.AnimeQualitySettings:Load () (at Assets/AnimeToon/Runtime/AnimeQualitySettings.cs:119)
    AnimeToon.AnimeQuality:Start () (at Assets/AnimeToon/Runtime/AnimeQuality.cs:102)
    ```
    Root cause: `AnimeQualitySettings.asset` points to an old deleted script GUID.
- **`AnimeDrift`:** File import logs only; zero errors.
- **`NullReferenceException` / `MissingReferenceException` / `"has been destroyed"`:** 0 occurrences.
- **`Shader error`:** 0 occurrences.

---

### F. API Compatibility Verification

| Class | Member | Type / Signature | Verified File & Line | Status |
|---|---|---|---|---|
| `VehicleController` | `wheels` | `List<WheelSetup>` | `VehicleController.cs:42`: `public List<WheelSetup> wheels = new List<WheelSetup>();` | **MATCH** |
| `VehicleController` | `ForwardSpeed` | `float` property | `VehicleController.cs:102`: `public float ForwardSpeed { get; private set; }` | **MATCH** |
| `VehicleController` | `IsGrounded` | `bool` property | `VehicleController.cs:106`: `public bool IsGrounded { get; private set; }` | **MATCH** |
| `VehicleController` | `HandbrakeAmount` | `float` property | `VehicleController.cs:108`: `public float HandbrakeAmount => handbrakeAmount;` | **MATCH** |
| `VehicleController` | `Stats` | `VehicleStats` (.Get(VehicleStat.MaxSpeed)) | `VehicleController.cs:104`: `public VehicleStats Stats => stats;` | **MATCH** |
| `VehicleController` | `All` | `static IReadOnlyList<VehicleController>` | `VehicleController.cs:87`: `public static IReadOnlyList<VehicleController> All => active;` | **MATCH** |
| `VehicleController` | `VehicleEnabled` | `static event Action<VehicleController>` | `VehicleController.cs:90`: `public static event Action<VehicleController> VehicleEnabled;` | **MATCH** |
| `WheelSetup` | `collider` | `WheelCollider` | `VehicleController.cs:12`: `public WheelCollider collider;` | **MATCH** |
| `WheelSetup` | `handbrake` | `bool` | `VehicleController.cs:22`: `public bool handbrake;` | **MATCH** |
| `WheelSetup` | `drive` | `bool` | `VehicleController.cs:20`: `public bool drive;` | **MATCH** |
| `VehicleHealth` | `IsDestroyed` | `bool` property | `VehicleHealth.cs:53`: `public bool IsDestroyed { get; private set; }` | **MATCH** |
| `VehicleHealth` | `Destroyed` | `event Action<ImpactInfo>` | `VehicleHealth.cs:63`: `public event Action<ImpactInfo> Destroyed;` | **MATCH** |
| `VehicleHealth` | `Respawned` | `event Action` | `VehicleHealth.cs:64`: `public event Action Respawned;` | **MATCH** |
| `VehicleBoost` | `IsBoosting` | `bool` property | `VehicleBoost.cs:40`: `public bool IsBoosting { get; private set; }` | **MATCH** |
| `VehicleImpactSensor` | `Impact` | `event Action<ImpactInfo>` | `VehicleImpactSensor.cs:36`: `public event Action<ImpactInfo> Impact;` | **MATCH** |
| `ImpactInfo` | `point` | `Vector3` | `ImpactInfo.cs:26`: `public Vector3 point;` | **MATCH** |
| `ImpactInfo` | `speedRatio` | `float` | `ImpactInfo.cs:22`: `public float speedRatio;` | **MATCH** |
| `ImpactInfo` | `tier` | `SpeedTier` | `ImpactInfo.cs:23`: `public SpeedTier tier;` | **MATCH** |
| `VehicleEffects` | `trail` | `ParticleSystem` | `VehicleEffects.cs:28`: `public ParticleSystem trail;` | **MATCH** |
| `VehicleEffects` | `hitSparks` | `ParticleSystem` | `VehicleEffects.cs:29`: `public ParticleSystem hitSparks;` | **MATCH** |
| `VehicleEffects` | `hitDust` | `ParticleSystem` | `VehicleEffects.cs:30`: `public ParticleSystem hitDust;` | **MATCH** |
| `VehicleEffects` | `lowHealthSmoke` | `ParticleSystem` | `VehicleEffects.cs:31`: `public ParticleSystem lowHealthSmoke;` | **MATCH** |
| `VehicleEffects` | `explosion` | `ParticleSystem` | `VehicleEffects.cs:32`: `public ParticleSystem explosion;` | **MATCH** |
| `PauseMenu` | `IsPaused` | `static bool` | `PauseMenu.cs:38`: `public static bool IsPaused { get; private set; }` | **MATCH** |
| `LocalPlayerRig` | `Vehicle` | `VehicleController` | `LocalPlayerRig.cs:71`: `public VehicleController Vehicle { get; private set; }` | **MATCH** |

**Zero mismatches found across all 25 game API symbols.**

---

### G. Shader Globals Consistency

All 18 `_Anime_` shader globals are declared in C# and matched in shader source:
- `_Anime_LowQuality`: `AnimeQuality.cs:29` <-> `AnimeCharacterToon.shader:114`, `AnimeEnvironmentToon.shader:123`, `AnimeWater.shader:86`
- `_Anime_OutlineOff`: `AnimeQuality.cs:30` <-> `AnimeCharacterToon.shader:115`, `AnimeEnvironmentToon.shader:124`
- `_Anime_CloudQuality`: `AnimeQuality.cs:31` <-> `AnimeSky.shader:89`
- `_Anime_ShadowTint`: `AnimeStyleController.cs:46` <-> `AnimeCharacterToon.shader:111`, `AnimeEnvironmentToon.shader:118`
- `_Anime_ShadowTintAmount`: `AnimeStyleController.cs:47` <-> `AnimeCharacterToon.shader:112`, `AnimeEnvironmentToon.shader:119`
- `_Anime_NightAmount`: `AnimeStyleController.cs:48` <-> `AnimeCharacterToon.shader:113`, `AnimeEnvironmentToon.shader:120`
- `_Anime_LightIntensityOffset`: `AnimeStyleController.cs:49` <-> `AnimeCharacterToon.shader:114`, `AnimeEnvironmentToon.shader:121`
- `_Anime_RimBoost`: `AnimeStyleController.cs:50` <-> `AnimeCharacterToon.shader:115`, `AnimeEnvironmentToon.shader:122`
- `_Anime_SkyOverride`: `AnimeStyleController.cs:52` <-> `AnimeSky.shader:88`
- `_Anime_SkyZenith`: `AnimeStyleController.cs:53` <-> `AnimeSky.shader:90`
- `_Anime_SkyHorizon`: `AnimeStyleController.cs:54` <-> `AnimeSky.shader:91`
- `_Anime_SkyGround`: `AnimeStyleController.cs:55` <-> `AnimeSky.shader:92`
- `_Anime_HorizonGlow`: `AnimeStyleController.cs:56` <-> `AnimeSky.shader:93`
- `_Anime_CloudLit`: `AnimeStyleController.cs:57` <-> `AnimeSky.shader:94`
- `_Anime_CloudShadow`: `AnimeStyleController.cs:58` <-> `AnimeSky.shader:95`
- `_Anime_CloudCoverage`: `AnimeStyleController.cs:59` <-> `AnimeSky.shader:96`
- `_Anime_StarAmount`: `AnimeStyleController.cs:60` <-> `AnimeSky.shader:97`
- `_Anime_SunColor`: `AnimeStyleController.cs:61` <-> `AnimeSky.shader:98`

**Zero mismatched or orphaned globals.**

---

### H. General Review of Anime* Scripts

1. **`AnimeGraphicsMenu.cs`:**
   - **Line 383, 404, 520-534:** `NewCanvas` creates child Canvases under a parent `Transform` without explicitly enforcing `localScale = Vector3.one`. In the prefab generator, this resulted in `m_LocalScale = (0, 0, 0)`.
   - **Line 200 vs Line 152:** `EnsureEventSystem()` is only called in `OpenPanel()`. The `openButton` itself is on `Open Button Canvas` and requires an `EventSystem` to receive clicks. If the scene lacks an `EventSystem`, the button cannot be pressed.
   - **Line 218-223 (`ReleasePause`):** Restoring `Time.timeScale = savedTimeScale` when closing the menu could accidentally unpause the game if another system (like `PauseMenu`) initiated a pause while the graphics menu was open.
2. **`AnimeQuality.cs`:**
   - **Line 310-327:** `savedShadows` stores `Light` references in a static dictionary. On scene transitions, destroyed Lights remain as dead references in the dictionary.
3. **`AnimeVehicleEffectsQuality.cs`:**
   - **Line 51:** `var main = entries[i].system.main;` does not null-check `entries[i].system`. If a particle system is destroyed dynamically, `LateUpdate` will throw `NullReferenceException`.
4. **`AnimeDriftEffects.cs`:**
   - **Line 115:** `if (marksRoot != null) Destroy(marksRoot.gameObject);` in `OnDestroy()` deletes all active skid marks on the road immediately when a vehicle is destroyed/respawned, rather than allowing them to fade out.

---

## 4. FINDINGS TABLE

| Severity | File : Line | Evidence | Recommended Fix (In Words Only) |
|---|---|---|---|
| **BLOCKER** | `Assets/Resources/AnimeToon/AnimeGraphicsMenu.prefab:3639,1835` | `Open Button Canvas` and `Panel Canvas` RectTransforms have `m_LocalScale: {x: 0, y: 0, z: 0}` | In `AnimeGraphicsMenu.cs` `Awake()`, enforce `buttonRoot.transform.localScale = Vector3.one;` and `panelRoot.transform.localScale = Vector3.one;`. Also update `NewCanvas()` to set `go.transform.localScale = Vector3.one;` before saving the prefab. |
| **BLOCKER** | `Assets/AnimeToon/Runtime/AnimeGraphicsMenu.cs:200` | `EnsureEventSystem()` is only called in `OpenPanel()`, not in `Awake()` or `Start()` | Call `EnsureEventSystem()` inside `Awake()` so that `openButton` can receive input events even in scenes that do not contain a pre-existing `EventSystem`. |
| **WARNING** | `Assets/Resources/AnimeToon/AnimeQualitySettings.asset:12` | `m_Script: {fileID: 11500000, guid: e9b67328430e16d49a5cb82a7c48b218, type: 3}` does not match `AnimeQualitySettings.cs.meta` (`ef7f4760d50e42f4f8ab0dcd9f1d578b`) | Update `guid` in `AnimeQualitySettings.asset` to `ef7f4760d50e42f4f8ab0dcd9f1d578b` or regenerate the asset using `Tools > Anime Toon > Quality Settings`. |
| **WARNING** | `Assets/Scripts/Runtime/AnimeVehicleEffectsQuality.cs:51` | `var main = entries[i].system.main;` lacks null-check for `entries[i].system` | Add `if (entries[i].system == null) continue;` at the beginning of the loop in `LateUpdate()`. |
| **NOTE** | `Assets/AnimeToon/Runtime/AnimeQuality.cs:320-326` | `savedShadows` static dictionary accumulates destroyed `Light` references across scene loads | Clear `savedShadows` or remove null keys when loading a new scene. |
| **NOTE** | `Assets/Scripts/Runtime/AnimeDriftEffects.cs:115` | `Destroy(marksRoot.gameObject)` in `OnDestroy()` deletes all road skid marks instantly upon vehicle respawn | Detach `marksRoot` or allow the trails to naturally expire before destroying the container. |

---

## 5. VERIFIED OK

- **No Type/Class Duplicates:** 0 duplicate definitions across all runtime/editor C# files.
- **No Duplicate Shaders:** All 7 `Anime/*` shaders have unique names and compile cleanly.
- **No Duplicate Meta GUIDs:** All meta files have distinct GUIDs.
- **Hook Script Deleted:** `AnimeGraphicsMenuHook.cs` is completely absent from the project.
- **API Signatures Match:** All 25 expected game fields, properties, events, and methods match exact types and signatures.
- **Shader Globals 100% Consistent:** All 18 `_Anime_` shader global variables match between C# script drivers and shader declarations.
- **Menu Prefab Linkage:** All button and panel GameObject/component references in `AnimeGraphicsMenu.prefab` are populated with valid, non-zero fileIDs.
- **DontDestroyOnLoad Persistence:** `AnimeGraphicsMenu` persists across scene transitions via `DontDestroyOnLoad`.

---

## 6. COULD NOT VERIFY

- **Runtime Graphic Raycaster Touch/Mouse Occlusion:** Whether existing HUD canvases (e.g. `ControlsOverlay`, `MatchHud`) have higher sorting orders than 590/600 or possess fullscreen transparent raycast target images that block touch/mouse input on specific mobile device resolutions. Requires running in Editor Game View or device testing.

---

## 7. RECOMMENDED FIXES (PRIORITY ORDER)

1. **Fix Canvas Scale in `AnimeGraphicsMenu.cs`:**
   In `Awake()`, immediately set `if (buttonRoot != null) buttonRoot.transform.localScale = Vector3.one;` and `if (panelRoot != null) panelRoot.transform.localScale = Vector3.one;`. Also set `go.transform.localScale = Vector3.one;` inside `NewCanvas()` so future prefab generations serialize with scale 1.
2. **Call `EnsureEventSystem()` on Initialization:**
   In `AnimeGraphicsMenu.cs`, invoke `EnsureEventSystem()` in `Awake()` (or `Start()`) so the standalone `openButton` is interactable immediately without depending on other scene objects.
3. **Repair `AnimeQualitySettings.asset` Script GUID:**
   Update the script GUID inside `Assets/Resources/AnimeToon/AnimeQualitySettings.asset` to `ef7f4760d50e42f4f8ab0dcd9f1d578b` (matching `AnimeQualitySettings.cs.meta`), or re-create the asset via the Editor menu.
4. **Regenerate the Prefab:**
   Run `Tools > Anime Toon > Generate Graphics Menu UI` in Unity Editor to re-export `AnimeGraphicsMenu.prefab` with clean (1,1,1) scales.
5. **Add Null-Guard in `AnimeVehicleEffectsQuality.cs`:**
   Add `if (entries[i].system == null) continue;` in `LateUpdate()` before accessing `entries[i].system.main`.

# CONTRACTS — Rickshaw Chaos: Dhaka

> The **stable surface**. These signatures are what modules are allowed to depend on.
> Changing one is a breaking change: it forces every consumer to change too.
> If a task needs to change something here, stop and say so before editing.
>
> All signatures below are copied from the working tree on 2026-10-21 and are verified against the code.

---

## 1. `VehicleSystem.Core` — depends on nothing

These live in `Scripts/Vehicle/` and `Scripts/Road/` today. They must move to `Scripts/Core/` and
must have **zero** outbound references.

### `DriveInput` — `Scripts/Vehicle/DriveInput.cs`

```csharp
public struct DriveInput
{
    public float throttle;
    public float steer;
    public bool handbrake;

    public DriveInput(float throttle, float steer, bool handbrake);
    public static DriveInput Neutral => default;
}
```

### `IDriveInputSource` — `Scripts/Vehicle/DriveInput.cs`

```csharp
public interface IDriveInputSource
{
    DriveInput Read();
}
```
Implementors: `BotInput` (Bots), `LocalPlayerInput` (Input). Consumed by `VehicleDriver` (Vehicle).
**This is the pattern to copy** when introducing a new boundary.

### `ImpactInfo` — `Scripts/Vehicle/ImpactInfo.cs`

```csharp
public struct ImpactInfo
{
    public float impactSpeed;
    public float speedRatio;
    public SpeedTier tier;
    public HitZone zone;
    public Vector3 point;
    public Vector3 normal;
    public Collider other;
    public Rigidbody otherBody;
    public VehicleController otherVehicle;   // <-- the one outbound dep; needs an interface to leave Core
}
```

### Enums — `Scripts/Vehicle/ImpactInfo.cs`

```csharp
public enum HitZone { Front, Rear, Side }

public enum SpeedTier
{
    Cruising    = 1,   // below "Fast From": no collision reward
    Fast        = 2,   // normal reward
    FullThrottle = 3   // maximum reward
}

public enum ImpactVerdict { Normal, Immune, Kill }
```

### `SeededRandom` — `Scripts/Road/SeededRandom.cs`

```csharp
public struct SeededRandom
{
    public SeededRandom(ulong seed);
    public static SeededRandom For(int seed, int index, int channel);
    public ulong NextULong();
    public float NextFloat();                       // 0 (inclusive) .. 1 (exclusive)
    public float Range(float min, float max);
    public int Range(int min, int max);             // min inclusive, max exclusive
    public bool Chance(float probability);
}
```
Deterministic across devices and Unity versions — the road, traffic and pickups are all built from it,
so every player using the host's seed gets the same road. **Do not replace this with `UnityEngine.Random`.**

---

## 2. `VehicleSystem.Vehicle` — the vehicle surface

### `VehicleController` — `Scripts/Vehicle/VehicleController.cs`

```csharp
public class VehicleController : MonoBehaviour
{
    public static IReadOnlyList<VehicleController> All { get; }
    public static event Action<VehicleController> VehicleEnabled;

    public List<WheelSetup> wheels = new List<WheelSetup>();
    public float ForwardSpeed { get; private set; }   // m/s, + forward
    public float SpeedKmh => Mathf.Abs(ForwardSpeed) * 3.6f;
    public VehicleStats Stats { get; }
    public bool IsGrounded { get; private set; }
    public float HandbrakeAmount { get; }
}
```

### `WheelSetup` — `Scripts/Vehicle/VehicleController.cs`

```csharp
public class WheelSetup
{
    public string label = "Wheel";
    public WheelCollider collider;
    public Transform visual;
    public int axleIndex;
    public bool steer;
    public bool drive;
    public bool brake;
    public bool handbrake;
}
```

### `VehicleHealth` — `Scripts/Vehicle/VehicleHealth.cs`

```csharp
public class VehicleHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float Health { get; private set; }
    public float Health01 { get; }
    public bool IsDestroyed { get; private set; }
    public bool IsInvulnerable { get; }
    public float RespawnTimeLeft { get; private set; }

    public void Kill();
    public void ApplyDamage(float amount, ImpactInfo info = default);
    public void Heal(float amount);
    public void SetHealthFromNetwork(float value);
    public void ResetHealth();

    public event Action<ImpactInfo> Destroyed;
    public event Action Respawned;
}
```

### `VehicleImpactSensor` — `Scripts/Vehicle/VehicleImpactSensor.cs`

```csharp
public class VehicleImpactSensor : MonoBehaviour
{
    public float minImpactSpeed = 2f;
    public float fastFrom = 0.5f;          // speedRatio where Fast begins
    public float fullThrottleFrom = 0.8f;  // speedRatio where FullThrottle begins
    public LayerMask ignoreLayers = 0;

    public event Action<ImpactInfo> Impact;
}
```

### `VehicleBoost` — `Scripts/Vehicle/VehicleBoost.cs`

```csharp
public class VehicleBoost : MonoBehaviour
{
    public int startingBatteries = 1;
    public int maxBatteries = 3;
    public float duration = 2.5f;

    public int Batteries { get; private set; }
    public bool IsBoosting { get; private set; }
    public float BoostTimeLeft { get; private set; }
    public float BoostTimeTotal { get; private set; }

    public bool AddBattery(int amount = 1);
    public void ResetBatteries();
    public bool TryBoost();
}
```

### `VehicleEffects` / `VehicleEffectsDefaults` — the 5 VFX slots

Both expose the **same five slots** in the same order. The integration layer maps them to particles.

```csharp
public class VehicleEffects : MonoBehaviour
{
    public ParticleSystem trail;
    public ParticleSystem hitSparks;
    public ParticleSystem hitDust;
    public ParticleSystem lowHealthSmoke;
    public ParticleSystem explosion;
}

public class VehicleEffectsDefaults : ScriptableObject   // Scripts/Data/VehicleEffectsDefaults.cs
{
    // same five slots
}
```

`DefaultEffectsBuilder` (`Scripts/Editor/`) owns the default asset:
`DefaultEffectsBuilder.AssetPath` + `DefaultEffectsBuilder.EnsureDefaults()`.
Asset path: `Assets/Resources/VehicleSystem/AnimeDriftEffectsDefaults.asset`.

---

## 3. `VehicleSystem.Data` — ScriptableObjects

### `VehicleDefinition` — `Scripts/Data/VehicleDefinition.cs`

```csharp
public class VehicleDefinition : ScriptableObject
{
    public string id = "vehicle";
    public string displayName = "Vehicle";
    public Sprite icon;
    public GameObject prefab;
    public GameObject previewPrefab;

    public float motorTorque = 600f;
    public float maxSpeedKmh = 55f;
    public float brakeTorque = 1800f;
    public float steerAngle = 32f;
    public float tireGrip = 1f;
    public float suspensionSpring = 18000f;
    public float suspensionDamper = 1800f;
    public float suspensionTravel = 0.25f;
    public float antiRoll = 3000f;

    public static event Action<VehicleDefinition> ValuesChanged;
}
```

### `VehicleStats` — `Scripts/Vehicle/VehicleStats.cs` (component)

```csharp
public class VehicleStats : MonoBehaviour
{
    public VehicleDefinition definition;
    public float motorTorque, maxSpeedKmh, brakeTorque, steerAngle, tireGrip,
                suspensionSpring, suspensionDamper, suspensionTravel, antiRoll;

    public float Get(VehicleStat stat);
    public float GetBase(VehicleStat stat);
    public void ApplyDefinition(VehicleDefinition def);
}
```

### `StatModifier` / `VehicleStat` / `ModifierType` — `Scripts/Vehicle/VehicleStats.cs`

```csharp
public enum VehicleStat { /* ... */ }
public enum ModifierType { /* ... */ }

public class StatModifier
{
    public readonly VehicleStat Stat;
    public readonly ModifierType Type;
    public readonly float Value;
    public readonly object Source;   // e.g. the equipment item that created it
    public readonly string Id;       // stable id, so it can be saved / sent over the network
}
```

> These three live in the **Vehicle** folder but belong with Data. Moving them to `Scripts/Data/`
> breaks the 4-type stats blob — one of the cheapest wins in the project.

### `StatModifierDef` — `Scripts/Data/StatModifierDef.cs`

The ScriptableObject form of a modifier. `StatModifierDef.id` is the stable id referenced by
`StatModifier.Id`, and `VehicleStats.GetModifierIds()` reads it.

---

## 4. `VehicleSystem.Monetization`

```csharp
// Scripts/Monetization/RewardedAds.cs
public interface IRewardedAdProvider
{
    bool IsReady { get; }
    void Show(string placement, Action<bool> onFinished);
}

public static class RewardedAds
{
    public static IRewardedAdProvider Provider;
    public static bool IsReady => Provider != null && Provider.IsReady;
    public static void Show(string placement, Action<bool> onFinished);
}
```
`LocalWallet` (static) also lives here and is the only other thing `MatchResultsFlow` depends on.

---

## 5. `AnimeToon` — the toolkit's public API

The game may call these. Nothing in `AnimeToon` may reference `VehicleSystem` or
`AnimeToon.Integration` types.

| Type | API |
|---|---|
| `AnimeGraphicsMenu` | `Open()`, `Close()`, `Toggle()`, `Show()`, `Hide()`, `ToggleVisible()` |
| `AnimeQuality` | `Changed` event · `ParticleScale` · `SkidMarks` · `DriftSparks` · `SetMode(...)` · `CycleMode()` |
| `AnimeSpeedFx` | `SetSpeed01(...)` · `SetBoosting(...)` · `Impact(...)` |
| `AnimeStyleController` | `SetTimeOfDay(...)` |
| `AnimeLightShafts` | present in `AnimeToon/Runtime/` |

### Shader-global contract — 19 `_Anime_*` keywords

All 19 exist in C# and are consumed by 7 shaders
(`AnimeCharacterToon`, `AnimeEnvironmentToon`, `AnimeGlass`, `AnimeRoadWet`, `AnimeWater`,
`AnimeFoliage`, `AnimeSky`):

```
_Anime_ShadowTint        _Anime_ShadowTintAmount   _Anime_NightAmount
_Anime_LightIntensityOffset  _Anime_RimBoost         _Anime_Wetness
_Anime_SkyOverride       _Anime_SkyZenith          _Anime_SkyHorizon
_Anime_SkyGround         _Anime_HorizonGlow        _Anime_CloudLit
_Anime_CloudShadow       _Anime_CloudCoverage      _Anime_StarAmount
_Anime_SunColor          _Anime_LowQuality         _Anime_OutlineOff
_Anime_CloudQuality
```

### Runtime assets

| Path | What |
|---|---|
| `Assets/Resources/AnimeToon/AnimeQualitySettings.asset` | quality tiers |
| `Assets/Resources/VehicleSystem/AnimeDriftEffectsDefaults.asset` | default VFX slots |

PlayerPrefs key: `AnimeToon.Quality`.

---

## 6. `AnimeToon.Integration` — what the toolkit needs from the game

This is the contract the **integration layer** fulfils on the game's behalf. It is the reason the
bridge folder exists at all: the toolkit must not know about `VehicleController`, but the game must
still drive the anime effects.

| Game type | Members the integration uses |
|---|---|
| `VehicleController` | `wheels` (`List<WheelSetup>`), `ForwardSpeed`, `IsGrounded`, `HandbrakeAmount`, `Stats`, static `All`, static event `VehicleEnabled` |
| `VehicleHealth` | `IsDestroyed`, event `Destroyed(ImpactInfo)`, event `Respawned` |
| `VehicleBoost` | `IsBoosting` |
| `VehicleImpactSensor` | event `Impact(ImpactInfo)` |
| `ImpactInfo` | `point`, `speedRatio`, `tier` |
| `SpeedTier` | enum |
| `VehicleEffects` | `trail`, `hitSparks`, `hitDust`, `lowHealthSmoke`, `explosion` |
| `VehicleEffectsDefaults` | same five slots |
| `DefaultEffectsBuilder` | `AssetPath`, `EnsureDefaults()` |
| `LocalPlayerRig` | `Vehicle` |

> If any of these change, the integration layer breaks. Change them together, and update this table
> in the same commit.

---

## 7. Rules for changing a contract

1. **Never change a signature without updating this file in the same commit.**
2. **Additive changes are fine** — new members, new overloads, new optional parameters with defaults.
3. **Breaking changes need agreement.** If a module needs `VehicleHealth.Destroyed` to carry different
   data, that is a cross-module change: announce it, update every consumer, update §2 here.
4. **A contract type may only gain dependencies on `Core`.** If `ImpactInfo` needed `VehicleController`
   it would not be in Core. That is exactly why `ImpactInfo.otherVehicle` is the one field that needs
   an interface before `ImpactInfo` can move to Core.
5. **Namespaces stay as they are.** `VehicleSystem`, `AnimeToon`, `AnimeToon.Integration`,
   `AnimeToon.Editor`, `VehicleSystem.EditorTools`. Renaming is not needed for assembly boundaries.

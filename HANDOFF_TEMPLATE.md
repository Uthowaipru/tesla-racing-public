# HANDOFF TEMPLATE

> Copy this into the **first message of every new conversation** that works on this project.
> Fill in the 6 fields. Keep it under one screen — the point is that a fresh Claude, on a fresh
> account, with no memory of previous conversations, can start correctly.

---

## Template

```markdown
**Project:** Rickshaw Chaos: Dhaka — Unity 6.3 LTS (6000.3.11f1), URP 17.3.0, Android.
Repo: https://github.com/Uthowaipru/tesla-racing-public  (public mirror; clone with
`git -c credential.helper= clone --depth 1` and `git config --global clone.protectHooks false`
if git-lfs complains about a post-checkout hook — the checkout actually succeeds).

**Read these first, in this order, before writing any code:**
1. `ARCHITECTURE.md`   — the 3 layers, the 5 rules, the Unity landmines, what is stale
2. `MODULES.md`        — the 17 target assemblies, the measured dependency graph, the 21 edges to break
3. `CONTRACTS.md`      — the stable signatures; do not change them without saying so
4. `Assets/README.md`  — what the game does and what has already been refactored
5. `Assets/Rickshaw Chaos_ Dhaka GDD v0.3.md` — the game design (rules, economy, multiplayer plan)

**Task:** <ONE boundary / ONE edge / ONE module. Not "decouple the project".>

**You may edit:**
- <files or folders>

**You must not break:**
- the signatures in `CONTRACTS.md`
- the `.meta` GUID of any moved file (move `.cs` + `.meta` together, never regenerate a GUID)
- `Assets/Scripts/Editor/` ↔ `Assets/RickshawChaos_Integration/Editor/` separation
- `AnimeToon/` must keep referencing zero `VehicleSystem` types

**How to verify:**
1. Unity compiles with 0 errors — `Assembly-CSharp.dll` rebuilt
2. Console has no `Missing (MonoBehaviour)` and no `The referenced script`
3. All 4 scenes open: `Assets/Scenes/MainMenu.unity`, `Game.unity`, `Game 1.unity`, `Game 2.unity`
4. `git status` shows **no modified or deleted `.meta`**
5. Game boots MainMenu → vehicle select → race, vehicle spawns and drives, bots race, HUD updates

**Deliver:** only the changed files, plus a short list of what changed, what you verified, and any
contract you had to touch. If you could not verify something, say so explicitly — do not guess.
```

---

## Worked example — Step 2 (extract the `AnimeToon` assembly)

```markdown
**Task:** Add an assembly definition for the AnimeToon toolkit only.

**You may edit:**
- create `Assets/AnimeToon/Runtime/AnimeToon.asmdef`
- create `Assets/AnimeToon/Editor/AnimeToon.Editor.asmdef`

**Do NOT touch** any file in `Assets/Scripts/` or `Assets/RickshawChaos_Integration/`.
`Assets/AnimeToon/` must keep referencing zero `VehicleSystem` types — verify that after the change.

**How to verify:**
1. Unity compiles with 0 errors and `AnimeToon.dll` + `AnimeToon.Editor.dll` appear in
   `Library/ScriptAssemblies/`
2. `Assets/Scripts/**` still compiles into `Assembly-CSharp` and now references `AnimeToon`
3. Console has no `Missing (MonoBehaviour)`
4. `git status` shows only 2 new `.asmdef` files + their `.meta` files — no other changes
5. All 4 scenes open and play

**Deliver:** the two `.asmdef` files and a one-line summary.
```

---

## Multi-conversation sequencing

**One conversation = one boundary.** Do not attempt the whole decouple in one session — the context
will blow out and the compile errors will pile up.

| # | Conversation | Risk | Depends on |
|---:|---|---|---|
| 1 | `AnimeToon` + `AnimeToon.Editor` asmdefs | low | — |
| 2 | `RickshawChaos.Integration` + `.Editor` asmdefs | low | 1 |
| 3 | `VehicleSystem.EditorTools` asmdef | low | 1 |
| 4 | Move `DriveInput.cs`, `ImpactInfo.cs`, `SeededRandom.cs` → `Assets/Scripts/Core/` + `VehicleSystem.Core` asmdef | low | 3 |
| 5 | Break the stats blob (edges 20–21) → `VehicleSystem.Data` asmdef | medium | 4 |
| 6 | Break the 11 `LocalPlayerRig` edges | **high** | 5 |
| 7 | Break the Vehicle edges (2, 19) | medium | 6 |
| 8 | Break the Road edges (5, 8, 12, 14, 18) | medium | 7 |
| 9 | Break the Input/UI/Monetization edges (4, 20) | medium | 8 |
| 10 | Create the remaining asmdefs | medium | 9 |

### Branch strategy

One branch per step, merged by one integration conversation at the end:

```
git switch -c refactor/step-2-anime-toon-asmdef
# ... work ...
git commit -m "refactor: extract AnimeToon into its own assembly"
git push -u origin refactor/step-2-anime-toon-asmdef
```

Merge to `main` only after the verification checklist passes. If a step goes wrong, `git revert`
the merge — do not force-push over `main`.

### Context hygiene

- `/clear` at the **start** of a new step, not mid-step.
- `/compact` only if the conversation is still useful; otherwise start fresh with the template.
- Put long reference material in a file, not in the chat. That is what `ARCHITECTURE.md`,
  `MODULES.md` and `CONTRACTS.md` are for.
- Ask Claude to read a file rather than pasting it. Reading is cheaper than the transcript and
  survives a context reset.

---

## Anti-patterns — do not do these

| Don't | Why |
|---|---|
| "Decouple the whole project" in one prompt | 43 types are mutually entangled. You will get a half-finished refactor that does not compile. |
| Moving a `.cs` without its `.meta` | Scenes reference GUIDs. The component silently becomes `Missing (MonoBehaviour)`. |
| Regenerating a `.meta` GUID | Same problem, plus it breaks `VehicleInput` → `LocalPlayerInput` continuity. |
| Using `UnityEngine.Random` for road/traffic/pickups | The GDD requires all players to see the same road from the host's seed. Use `SeededRandom`. |
| Adding new `UnityEngine.Input` calls | The project is Input System only. Legacy calls are compiled out. |
| Renaming namespaces while splitting | Namespaces are already clean. Renaming adds a huge diff for zero benefit. |
| Moving `AnimeVehicleFxBridge.cs` without its `.meta` | It is on the Main Camera in `Game 1.unity` and `Game 2.unity`. |
| Editing `Assets/TextMesh Pro/Examples & Extras/` | 34 vendor scripts. TMP now lives inside `com.unity.ugui` 2.0.0. |
| "Fixing" the 10 broken script GUIDs | All vendor/sample content. See `DECOUPLING_PLAN.md` §4. They are out of scope. |
| Assuming the README's "not compiled in Unity" line is true | It is stale. The project compiles clean. |

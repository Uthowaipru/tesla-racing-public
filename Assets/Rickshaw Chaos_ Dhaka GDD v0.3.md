# Rickshaw Chaos: Dhaka

**Game Design Document v0.3** · Android · Unity 6.3 LTS · Art: 3D stylized, Dhaka-inspired

> *(proposed)* marks a suggestion you have not confirmed. **Placeholder** values are there to be replaced after playtests. Everything else was decided. Section 17 lists what is still open.

## 1. Overview

A short-match, score-attack chaos game on a procedurally generated Dhaka road. You drive a battery rickshaw (later auto/CNG) through dense traffic, ram traffic at speed for points, and survive until the timer ends. Played offline, over local Wi-Fi/hotspot, and later online.

**Pillars:** readable chaos · one-hand touch controls with auto-acceleration · short matches (3 to 12 min) · fair, not pay-to-win · all ages, family-friendly.

## 2. Match rules

- **No finish line.** The match ends when the timer ends (3, 6, 10 or 12 min). Ranking is the live total of points.
- **Points**

| Event | Points |
| --- | --- |
| Every 100 m travelled | +10 |
| High-speed NPC side bump | +20 |
| Very high-speed NPC side bump | +30 |
| Successful rear ram on a rival | +20 |
| Successful side ram on a rival | +25 |
| Overtake a rival | +10 |
| Player destroyed | -50 |

- **Speed tiers** use each vehicle's own tuned maximum speed: **Tier 1 Cruising** (below 50%): no collision bonus · **Tier 2 Fast** (50 to 79%): normal reward · **Tier 3 Full throttle** (80 to 100%): maximum reward and stronger knockback. Slow, repeated bumping earns nothing.
- **Collision angle matters.** A glancing side hit scores. A **head-on crash into an NPC** depends on speed: **Tier 3 (high speed) = destroyed** (-50 and a respawn); **Tier 2 and Tier 1** only damage health, scaled by speed *(placeholder: about 40% and 10% of max health)*. Head-on crashes never earn a bonus.
- **Chain bonus** for hits on distinct traffic vehicles within 4 seconds:

| Chain | Extra |
| --- | --- |
| 2 hits | +5 |
| 3 hits | +10 |
| 4 hits | +20 |
| 5 or more | +30 per qualifying hit |

- **Anti-farming** *(proposed)*: each NPC rewards once; per-pair cooldown and minimum speed for rival hits; cooldown on overtakes.

## 3. Game modes and menu

Main menu: **Play** (offline levels) · **Garage** · **Create Room** (Local / Online) · **Multiplayer** (future: Classic / Ranked) · **Settings**.

**Room settings (host):** match time · **rule: Classic** (upgrades allowed, each vehicle uses its own stats) **or Equal tuning** (same stats for all within a class, upgrades off, ranked-style) · bots to fill empty slots. The host's road seed is used by every player.

**Offline levels:** a data asset per level with a **fixed seed**, time limit, objectives, star thresholds and difficulty settings.

- Objectives: reach a score · travel a distance (a finish chunk at that distance) · collect N batteries · finish without being destroyed · make N high-speed hits.
- Stars *(proposed)*: 1 = complete, 2 = target score, 3 = stretch goal.
- Difficulty knobs: traffic density, NPC speed variety, hazards, battery frequency, time pressure, target score, vehicle restriction.
- Pacing: levels 1 to 3 teach one mechanic each, then a gradual ramp with breather levels and a special level about every 5. Number of levels: **30 as a placeholder** (3 chapters of 10); the real count and level design come later.

**Future:** ranked and queue play, and a separate racing mode with a finish line.

## 4. Vehicles

- **Playable:** battery rickshaw, auto/tuktuk, CNG (three-wheelers). Buses and trucks are NPC traffic only.
- **Classes:** **Light** and **Heavy** (small and big hitbox). A class asset holds class stats, mass and hitbox.
- **Class stats (placeholder, Light = 100%):** Heavy has about 150% health, 90% top speed, 85% acceleration, 85% handling, 160% mass and a bigger hitbox. Final values after playtests.
- **Rarity:** Rare · Epic · Legendary. **Rarity changes looks only.** Base stats come from the vehicle's class, so a Legendary is never stronger than a Rare of the same class.
- **Ownership:** free starter · bought with **Cash** · ad rental (next 3 matches, or a set number). Vehicles are never bought with coins. Prices are placeholders (section 12).
- **Upgrades:** paid with **coins** only; apply in Classic and offline play only. Placeholder list: engine power, top speed, brakes, handling/grip, suspension, health (armor) and **battery boost efficiency**; 5 levels each, placeholder price per level 100, 250, 500, 1,000 and 2,000 coins.
- **Equal tuning / ranked:** identical stats, mass and hitbox inside a class.

## 5. Controls and feel

- Auto-acceleration stays. Brake brakes then reverses. Steering, **eased handbrake** (not instant), **BOOST** button, **RESET** (repair) button.
- Touch, keyboard and gamepad.
- Smoothness (original implementation): asymmetric throttle rise/fall, steering ramp with slower recentering at speed, speed-based steering angle, optional sideways-grip assist, tuned by feel.

## 6. Health, destruction and recovery

- **Health bar only**, no mesh deformation (maybe a later update). Damage comes from collision impact; light touches are ignored.
- **Destroyed** (health reaches zero, or a head-on NPC crash): -50 points, respawn after a few seconds at the last safe place, brief invulnerability.
- **Auto recovery** stays (reverse out / go to a safe place when stuck). Remove the occasional hop/lift; place the car on the ground instead.
- **Manual recovery** (R / RESET) costs one repair kit: teleport to the **last safe place** and **restore 35% health**. No lift, no move backward. No free repair without kits. Destruction still triggers the normal respawn penalty.

## 7. Pickups (in match, reset every match)

|  | Battery | Repair kit |
| --- | --- | --- |
| Start with | 1 | 1 |
| Carry limit | 3 | 3 |
| Effect | **BOOST** for 2.5 s: more acceleration, top speed and collision power | Teleport to last safe place, +35% health |
| Notes | Consumed on use, no recharge. Helps catch up with others | No kit, no repair |

- Pickups are per-player copies generated from the seed, so nothing needs syncing.
- Nothing from pickups is saved to the profile. Upgrades can improve boost efficiency.

## 8. Road and traffic

- Road built from a few preset chunks by a **seeded, deterministic generator** (own random generator). **All players share the host's seed.** Game versions must match to join.
- NPC traffic: cars, CNG, buses, trucks. Each device runs its own traffic from the shared seed (no NPC networking).
- No teleport or leash rule; catch-up is done with batteries.

## 9. Multiplayer

- **Rooms:** local max 8, online max 4 (more later). Minimum 2 (host and client) or fill with bots. *Test 4 on a hotspot first; 8 is a stretch for the host phone.*
- **Local:** one phone hosts; others join on the same Wi-Fi or the host's hotspot by QR, code or room list.
- **Online:** the host phone still runs the match; players join by invite code through a relay (Unity Relay, no game server).
- Cars are simulated by their owner phone and interpolated on others; the host owns match state (timer, scores).
- Netcode for GameObjects, built in host-only mode first so offline uses the same code. Bots follow the road centre line with the same input-source design as players.
- **Future ranked/queue:** class-based, identical stats; queue service decided later.

## 10. Art and visual effects

- **Art:** 3D stylized, Dhaka-inspired (invented signs and brands, no real company logos).
- **Particle trail behind the player vehicle** (dust/exhaust, stronger while boosting).
- **Hit particles** on collisions with other vehicles (sparks, dust, debris) scaled by impact size.
- Smoke at low health, a puff when destroyed, pickup effects, score pop-ups, small screen shake.
- Mobile budget: pooled particle systems, low counts.

## 11. HUD and screens

- **HUD:** speedometer · timer · total points · live position · health bar · battery count + BOOST · repair count + RESET · marker toward the nearest player · pause.
- **Screens:** main menu · Garage (3D rotating vehicle selector) · create/join room (QR, code, list) · level select · results (score, coins, double-reward ad) · settings.

## 12. Progression and economy

Two currencies:

|  | Coins | Cash |
| --- | --- | --- |
| Used for | Upgrades and tuning only | Buying vehicles |
| Earned | Match rewards (10 points = 1 coin), level bonuses, ad doubling | Match rewards at a lower rate (40 points = 1 Cash, placeholder) |
| Real money | **Never** | **Yes** (in-app purchase) |

| Match score | Coins | Cash |
| --- | --- | --- |
| 500 | 50 | 12 |
| 1,000 | 100 | 25 |
| 2,500 | 250 | 62 |
| 5,000 | 500 | 125 |

- **Level bonuses (coins, once per level):** first completion +100, 2 stars +50, 3 stars +100 *(set by me, tunable)*. A first perfect clear gives up to 250 coins.
- **Rewarded ad:** doubles the match's coin and Cash reward, **up to 5 per day** *(set by me, tunable)*. Vehicle-rental ads have their own limit, to decide.
- **Vehicle prices (placeholder, in Cash):** Rare 500 · Epic 1,500 · Legendary 4,000. Free players can still earn Cash by playing, only slowly.
- Cash packs are the only real-money purchase. Consider a parental gate before purchases *(proposed)*.
- No currency pickups in matches.

## 13. Monetization and ads

- **Rewarded ads only** (double reward, vehicle rental). **No banner ads.** Rarity is looks only, so purchases never buy power.
- **Target audience:** all ages, with a fallback to **13+** if the Families requirements prove too heavy (a game that clearly appeals to children may still be treated as child-directed, so 13+ is not a guaranteed shortcut). **Neutral age entry** on first launch (no hint which answer unlocks what; no instant retry); store only the bracket, on the device.
- **Ad treatment follows the age bracket:** child-safe ads for children **and unknown-age users**.
- **Privacy:** no advertising identifiers sent for children or unknown-age users.
- **Fallback:** if age screening fails, default to the safer child-appropriate treatment.
- Rewarded ads must be closable after 5 seconds in child treatment (closing early forfeits the reward). Review Google Play Families and child-privacy rules, including Play Games sign-in and the backend, before release (not legal advice).

## 14. Profile and backend

- Google Play Games sign-in when online. **Offline-first:** the phone holds the real profile and syncs when connected.
- Backend: Unity Gaming Services free tier (Authentication, Cloud Save, Cloud Code, Cloud Content Delivery), behind an `IProfileService` interface so it can be swapped.
- **Stored only:** player ID, coins, Cash, owned vehicles and upgrades, purchases, settings, shared game data (prices, vehicle lists). Nothing else is collected. Purchases are validated against Google Play.

## 15. Technical overview

**Built:** vehicle controller and stats, `DriveInput` / `VehicleDriver` / `LocalPlayerRig`, `VehicleDefinition` / `StatModifierDef` / `VehicleCatalog`, vehicle selector with 3D preview, `VehicleSpawner`, speedometer, recovery, pause menu, mobile controls.

**To build:** `MatchManager` + score system · road generator · traffic manager · pickups · damage and respawn · bots · `LevelDefinition` · `VehicleClass` · room rule settings · profile service · netcode and room services · ads/IAP/age screen · VFX.

## 16. Roadmap

1. Controller feel, health and destruction, new manual recovery.
2. Procedural road and NPC traffic (single player).
3. `MatchManager`, scoring, HUD, bots, offline levels.
4. Menus, garage, profile backend, economy.
5. Netcode: host-only, then local rooms, then online with relay.
6. Ads, in-app purchases, age screen.
7. Polish, VFX, soft launch.

**Later:** ranked and queue, racing mode with a finish line, and a separate open-world Dhaka game after this project.

## 17. Still open

- Placeholders to finalize after playtests: number of levels, vehicle prices, class stats, upgrade list and prices, the Cash earn rate, head-on damage values.
- Size and price of Cash packs, and whether to add a parental gate for purchases.
- Final target-audience setting in the Google Play Console (all ages, fallback 13+).
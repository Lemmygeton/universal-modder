# Mithrix, King of Nothing (Reclaimed): final survivor spec

Status: **final design, implementation-ready** (2026-10-11). Supersedes the three candidate passes
(`kb/design_mithrix_lore.md`, `kb/design_mithrix_mechanics.md`, `kb/design_mithrix_synergy.md`). The winner was the
item-synergy pass; this document grafts the best ideas from the other two (appendix A explains the scoring and every
graft and cut).

Sources: `kb/boss_mithrix.md` (cited as **D§n**), `docs/ARCHITECTURE.md`, `docs/CORE_API.md`, RoR2-ref 1.3.1 (API names),
MMHOOK_RoR2 2026.10.8 (live signatures). Tags: **VERIFIED** = name confirmed from UmbralMithrix/RiskyMithrix source
(D§6); **CANDIDATE** = inferred from a clip or hash name, always paired with a fallback; **LAB** = a vanilla behaviour this
spec assumes and the lab must confirm (checklist §16). Every number below is a config default (§14), not a constant.

---------------------------------------------------------------------------------------------------------------------------

## 0. Summary card

| | |
|---|---|
| Name / subtitle | **Mithrix**, *King of Nothing* |
| Body / tokens / plugin | `ReclaimedMithrixBody`, `RECLAIMED_MITHRIX_*`, `com.reclaimed.mithrix`. No "Brother" substring anywhere in our body, prefab or projectile names (§13.1) |
| Role | **Sovereign bruiser / zone controller.** Planted, heavy, telegraphed blows that own the ground he stands on. Team value from Cripple, stuns, a pull, projectile denial and (alt) a team attack-speed aura |
| Pitch | *Every blow he endures perfects the next one he lands.* |
| Passive | **Adaptive Superiority**: heavy hits taken grow Adaptive Armor; all bonus armor raises his damage on a capped curve. Keyword **Heavy**: hammer skills stop speeding up at +150% attack speed, the rest becomes damage |
| Primary | **Sunder** (sweet-spot hammer smash; while sprinting it throws homing **Lunar Needles** instead, as the boss does) |
| Secondary | **Rebuke** (lunging swing: stun, deletes projectiles, 2 stocks) |
| Utility | **Lunar Slide** (4-way dash, cleanse, crystal spikes, 2 charges) · alt **Cast Down** (gravity well) |
| Special | **Shock Wave** (invulnerable leap, ring of waves, *Conquest* refunds) · alt **Forbidden Decree** (rotating flame walls, then the **Ascendant** form) |
| Resource / form | No resource bar. One timed form (**Ascendant**, 10 s) reachable only through the alt Special (§9) |
| Unlock | ***...Next Time***: "Defeat Mithrix while carrying Purity." Server-side; no Halcyon Seed, Aurelionite, Lunar Ruin, kill credit or Karma, so it cannot collide with False Son: Family Bonding (§10) |
| Alts | exactly 2: Utility (Cast Down) and Special (Forbidden Decree) |

**The loop:** sprint in throwing needles → Rebuke to close (stun, projectiles swatted) → plant Sunder on the stunned target's
core → Lunar Slide out of the answer, leaving spikes → Shock Wave into the next pack → its kills refund the leap → repeat.

**The one rule players learn:** *wind-ups are commitments only Utility and Special can break; recoveries can always be
cancelled by another skill.* (The boss's own rule: his dash cancels Smash and Swing end lag, D§2.)

---------------------------------------------------------------------------------------------------------------------------

## 1. Identity and pillars

**Fantasy.** The King of Nothing, *next time*. Same hammer, same leap, same lunar fire. What changed: he no longer despises
the "baubles and trinkets" he mocked ("WEAK, WITHOUT YOUR BAUBLES AND TRINKETS."), and the phase that once tried to kill
everyone now quickens the people beside him. He is still regal, precise and contemptuous of waste; the redemption is
in what the design is *for*.

| Theme (brief) | Where it lives in the kit |
|---|---|
| Perfect design / precision | Sunder's sweet-spot core; "Next Time" whiff mercy; the commit-then-cancel rule |
| Lunar energy | Needles, waves, crystal spikes, gravity well, flame walls: every effect is a boss asset, recoloured |
| Control | Cripple on waves, spikes, well and walls; Rebuke stun and projectile deletion; the pull |
| Adaptive superiority | Adaptive Armor (the boss's P1/P3 adaptive armor) → Superiority damage |
| Conquest | Shock Wave's kill refunds |
| Bitterness toward imperfect life | Voice: tooltips, logbook, boss lines. Mechanically he never heals (no regen beyond base, no lifesteal) |
| Redemption seed ("NEXT TIME...") | The unlock, the Sunder "Next Time" mercy, the Ascendant team aura, the logbook and recognition lines |

**Strengths:** six hits ≥400% (Bands, Totem, Runic Lens); team control and armor shred; reactive tankiness that peaks
against spikes; a 1 s invulnerable leap; graceful at 100+ items (§11).
**Weaknesses:** committed wind-ups; large hurtbox; mobility only on cooldowns; ground-bound AoE (flyers need needles,
aimed Rebuke or Cast Down); no healing in the kit.

**Pillars (the contract every number obeys)**
1. **Big hits are the identity.** Hit frequency is bounded (Heavy), damage keeps scaling.
2. **Every scaling axis has one ceiling, and the ceiling converts instead of wasting** (attack speed → damage, armor →
   damage on a hyperbola, kill refunds ≤ half the cooldown).
3. **Discrete hits only.** No ticking zones, no fire pillars. Each damaging object hits a given enemy a bounded number of
   times.
4. **Correct `DamageSource` on everything**, child projectiles included.
5. **Boss-faithful verbs, player-tuned numbers.** None of the boss's tuning, statics, states, families or prefabs is
   touched (ARCHITECTURE rule 1).

---------------------------------------------------------------------------------------------------------------------------

## 2. Base stats and growth

| Stat | **Mithrix** | Loader | Acrid | False Son | Commando |
|---|---|---|---|---|---|
| Health (+/lvl) | **160 (+48)** | 160 (+48) | 160 (+48) | 180 (+54) | 110 (+33) |
| Regen (+/lvl) | **2.5 (+0.5)** | 2.5 (+0.5) | 2.5 (+0.5) | 1 (+0.2) | 1 (+0.2) |
| Damage (+/lvl) | **12 (+2.4)** | 12 (+2.4) | 15 (+3) | 12 (+2.4) | 12 (+2.4) |
| Armor | **20** | 20 | 20 | 0 | 0 |
| Move speed / sprint | **7 / ×1.45** | 7 | 7 | 7 | 7 |
| Acceleration | **60** | 80 | 80 | 50 | 80 |
| Jump power / jumps | **15 / 1** | 15 / 1 | 15 / 1 | 15 / 1 | 15 / 2 |
| Mass | **300** | 150 | 300 | 100 | 100 |
| Crit | **1%** | 1% | 1% | 1% | 1% |

**Why.**
- *Loader-class bulk without Loader's barrier.* He trades Scrap Barrier (proactive) for Adaptive Armor (reactive, strongest
  exactly when a hit is dangerous). Level-1 EHP 160 × 1.2 = 192 vs False Son's 180; False Son's Growth turns bonus *health*
  into stocks, Mithrix's Superiority turns bonus *armor* into damage — the two lunar bruisers scale on different stats.
- *Damage 12, not 15.* His weight lives in coefficients (480–700%). A higher base would inflate every big-hit item twice.
- *Regen 2.5.* The kit has no heal at all; 2.5 is the melee quality-of-life floor (Loader, Acrid). First balance lever if
  he underperforms (§12).
- *Acceleration 60.* Regal, not twitchy (between False Son 50 and the default 80). *Mass 300* (Acrid): a planted king is not
  juggled, which protects his wind-ups.
- Boss speed 15 is tuned for moon gravity (D§7); survivor 7.
- Growth is the vanilla default (+30% health, +20% damage, +20% regen per level). Nothing in the kit scales per level.

**Size.** Target capsule **height 2.5 m, radius 0.8 m** (≈1.35× Commando, Acrid-wide). At load, before
`SurvivorBuilder.Build`, read the boss capsule (`CapsuleCollider` on `BrotherBody`) and set
`ModelScale = Body.TargetHeight / bossCapsuleHeight` (config, §14). Camera: clone `GameKeys.CameraStandardTall`,
`IdealLocalCameraPos = (0, 1.0, -13)`, pivot from capsule, so the overhead hammer and the Sunder ring are in frame.

---------------------------------------------------------------------------------------------------------------------------

## 3. Passive: Adaptive Superiority

> **Adaptive Superiority.** Taking a heavy hit grants **Adaptive Armor**. Every point of **bonus armor** increases your
> damage, up to **+30%**.
> Keyword **Heavy**: *gets faster with attack speed up to +150%; attack speed beyond that increases its damage instead.*

Lore: the boss has adaptive armor in phases 1 and 3 (D§2). The reclaimed king keeps it, and his "superiority" — once
contempt — becomes the measured confidence of someone who has been hit by everything and stayed standing.

### 3.1 Adaptive Armor (Core `AdaptiveArmor`, server-authoritative, manual serialization)

Component fields set by the Mithrix plugin (Core defaults overridden):

| Field | Value | Config key |
|---|---|---|
| `armorPerHealthFraction` | **250** (armor per 1.0 of full combined health lost in one hit) | `Passive.AdaptivePerFraction` |
| `minimumHealthFraction` | **0.02** (hits below 2% of full combined health grant nothing) | `Passive.AdaptiveMinFraction` |
| `maxArmor` | **100** | `Passive.AdaptiveMax` |
| `decayDelay` | **2.5 s** after the last qualifying hit | `Passive.AdaptiveDecayDelay` |
| `decayPerSecond` | **30** | `Passive.AdaptiveDecayRate` |
| Excluded hits | DoT ticks (`DamageType.DoT` or `DamageSource.DOT`), `DamageSource.Hazard`, `DamageType.FallDamage`, attacker == victim | Core request C1 (§13.5) |

```
on server damage dealt to Mithrix (after mitigation):
    if excluded(report) or report.damageDealt <= 0: return
    f = report.damageDealt / max(1, healthComponent.fullCombinedHealth)
    if f < 0.02: return
    A = min(100, A + 250 * f);  lastHit = now
every FixedUpdate (server): if now - lastHit >= 2.5: A = max(0, A - 30 * dt)
armor stat += A          (RecalculateStatsAPI armorAdd; stats dirtied in 2-armor steps, existing Core behaviour)
```

| Situation (level 1, 160 HP) | Hit | f | Armor gained |
|---|---|---|---|
| Lemurian bite | 12 | 7.5% | +18.75 |
| Beetle Guard slam | 40 | 25% | +62.5 |
| Late run (3,000 HP), elite hit | 150 | 5% | +12.5 |
| Late run, boss crush | 900 | 30% | +75 |

Self-stabilising: gains are proportional to the *fraction* of health lost, so the passive is strongest early and against
spikes, and fades into a spike guard late. It never needs a late-game nerf.

**Telegraph (all peers):** a `TemporaryOverlay` with a clone of `matBrotherShield_mat 9508dece7208a9c4cb617dadcd00b3f9`
(Soul tint) whose alpha = `AdaptiveArmor.Ratio × 0.6`. A visible buff `bdReclaimedMithrixAdaptive` (icon generated,
stackable) shows `floor(A)` as stacks; the server updates stacks only when they change by ≥5 (or reach 0).

### 3.2 Superiority (armor becomes damage)

```
B          = max(0, body.armor - 20)                      // all armor above his base 20, from ANY source
multiplier = 1 + 0.30 * B / (B + 100)
```

| Bonus armor B | 0 | 25 | 50 | 100 | 200 | 400 | 1000 | ∞ |
|---|---|---|---|---|---|---|---|---|
| Damage bonus | 0 | +6.0% | +10.0% | +15.0% | +20.0% | +24.0% | +27.3% | +30% |

- **Implementation:** `On.RoR2.CharacterBody.RecalculateStats` postfix, filtered to our `BodyIndex`: after `orig(self)`,
  `self.armor` is final; set `self.damage *= multiplier` (the `damage` setter is public in the reference assembly).
  `RecalculateStatsAPI.GetStatCoefficients` runs before armor is final, so it cannot be used. All inputs (items, buffs,
  the replicated Adaptive Armor value) are identical on every peer → deterministic.
  *Fallback* if the setter is not accessible in the live build (apicheck): multiply `damageInfo.damage` in a server
  `HealthComponent.TakeDamage` prefix when `attacker` is a Mithrix body.
- Cripple *on Mithrix* (−20 armor) lowers B — the boss's own debuff remains a counter.
- **Readout:** visible buff `bdReclaimedMithrixSuperiority`, stacks = `round(bonus %)`, set by the server via
  `ReclaimedBuffs.SetStacks` only when the rounded value changes.
- Hyperbola for the same reason armor uses one: the first 50 armor is a noticeable +10%, then it flattens, so stacking ten
  Opals cannot turn armor into a damage engine. Levers: `Passive.SuperiorityMax`, `Passive.SuperiorityHalfPoint`.

### 3.3 Heavy (the attack-speed ceiling)

For any **Heavy** skill with base duration `D`, coefficient `C`, attack speed `A` (1.0 = base), cap `K`, efficiency `e`:

```
animSpeed  = min(A, K)                // K = 2.5  (i.e. +150% attack speed)
duration   = D / animSpeed
damageMult = 1 + e * max(0, A / K - 1)   // e = 1.0
coefficient = C * damageMult
```

| Attack speed | +0% | +50% | +150% (cap) | +300% | +500% |
|---|---|---|---|---|---|
| Sunder duration | 1.00 s | 0.67 s | 0.40 s | 0.40 s | 0.40 s |
| Sunder core coefficient | 480% | 480% | 480% | 768% | 1152% |
| Sunder core DPS | 480%/s | 720%/s | 1200%/s | 1920%/s | 2880%/s |

- DPS stays linear in attack speed (e = 1): Soldier's Syringe is never dead.
- Hit frequency (procs, Behemoth explosions, network traffic) stops at 2.5× base: the most important 100-item safeguard,
  and it keeps the boss animations readable.
- **Heavy skills:** Sunder, Rebuke, Shock Wave (crouch only), Forbidden Decree (enter, channel, exit). Lunar Needles use
  the same formula with **K = 3** (`Heavy.NeedleCap`). Lunar Slide and Cast Down do not scale with attack speed.
- Implementation: a shared helper `HeavyScaling { float AnimSpeed(float A); float DamageMult(float A); }` and Core
  request C2 (`ReclaimedSkillState.AttackSpeedForDuration` virtual, §13.5) so `ScaledDuration` and
  `ReclaimedMeleeState` use `min(A, K)`.

---------------------------------------------------------------------------------------------------------------------------

## 4. Shared skill conventions

- **State machines:** `Body` (spawn `LunarDescent`, `GenericCharacterMain`, death) and `Weapon` (every skill state). One
  machine for all skills makes the cancel rule a single interrupt-priority system. Both are listed in
  `NetworkStateMachine` (Core does this). Stun and freeze idle `Weapon` through `SetStateOnHurt` as on vanilla survivors.
- **Attacks:** `BlastAttack`, `OverlapAttack` and `ProjectileManager.FireProjectile` from the **authority** through the
  `ReclaimedSkillState` helpers (`FireBlastAuthority`, `FireOverlapAuthority`, `FireProjectileAuthority`); every hit is
  tagged with the slot's `DamageSource` (`Tag`/`TaggedDamage`). Server-only side effects (cleanse, projectile deletion,
  buffs, the well pull, Conquest, the Decree hit limiter) run in the **server's copy** of the state
  (`if (NetworkServer.active)` in `OnEnter`/`FixedUpdate`) or in server components. The state is replicated to every peer
  by `NetworkStateMachine`, so animations and cosmetics are played in `OnEnter` on **all** peers (never inside
  `isAuthority`), which avoids UmbralMithrix's "animation doesn't play in MP" bug class (D§7).
- **Gameplay timing is fixed fractions in code** (deterministic, latency- and animation-independent). Animations are
  *fitted* to those timings with the segment player below. The VERIFIED float curves `weapon.hitBoxActive`,
  `blast.hitBoxActive`, `fist.hitBoxActive` (D§6) are used only to **measure** clip impact times in the lab.
- **Segment player** (Mithrix-local helper `ClipFitter`):
  ```
  inputs: layer, state, rateParam, clipLength L, clipImpact ti (clip seconds at rate 1), windup w, recovery r, Rmax
  rate1 = ti / w;  start = 0
  if rate1 > Rmax: start = (ti - Rmax * w) / L;  rate1 = Rmax          // skip the front of the wind-up instead of blurring it
  AnimationResolver.TryResolve(...) → animator.Play(stateHash, layer, start); animator.SetFloat(rateParam, rate1)
  at impact: animator.SetFloat(rateParam, min(Rmax, (L - ti) / r))
  at state exit: CrossFadeInFixedTime("BufferEmpty", 0.1) on "FullBody Override" (VERIFIED) for FullBody states
  ```
  `Rmax = Anim.MaxPlaybackRate = 6`. `L` and `ti` per clip come from config (`Anim.<Clip>Length`, `Anim.<Clip>Impact`),
  default −1 = "use 0.40 × L", filled from the lab tool `reclaimed_mithrix_measure <layer> <state> <curve>` (registered via
  `ReclaimedCommands.Register`, cheat=false): plays the state at rate 1 on the local body and logs clip length and the first
  time the curve exceeds 0.5.
- **Assets:** every effect/projectile is a clone (`EffectFactory.CloneEffect/CloneProjectile`, palette **Soul**: warm
  gold/ivory) named `ReclaimedMithrix<Thing>`. Gold = friendly, blue = boss, in Commencement. Where a field is "from ESC",
  read the vanilla EntityStateConfiguration's `serializedFieldsCollection` at load (D§5.1); the GUID listed is the fallback.
- **Sounds** are the Wwise strings from D§4.0 (already in the base soundbank).
- **Keywords:** `KEYWORD_AGILE` (vanilla), `KEYWORD_STUNNING` (vanilla), and ours `KEYWORD_RECLAIMED_MITHRIX_HEAVY`,
  `KEYWORD_RECLAIMED_MITHRIX_CRIPPLING`, `KEYWORD_RECLAIMED_MITHRIX_ASCENDANT` (text in §15).
- **Cripple** = vanilla `bdCripple_asset a6a4458bb70c46d4aa7c9a7c6666a578` via `DamageType.CrippleOnHit` (−20 armor,
  −50% move speed, 3 s, non-stacking).

### 4.1 Interrupt priorities and cancel matrix

| Skill | SkillDef priority | State: commit phase | State: free phase |
|---|---|---|---|
| Sunder | Any | 0 → impact (0.40 D): `PrioritySkill` | impact → end: `Skill` (cannot self-cancel) |
| Lunar Needle | Any | — | `Any` |
| Rebuke | Skill | 0 → 0.425 D: `PrioritySkill` | 0.425 D → end: `Any` |
| Lunar Slide | PrioritySkill | whole 0.30 s: `PrioritySkill` | — |
| Cast Down | PrioritySkill | 0 → 0.20 s: `PrioritySkill` | 0.20 → 0.35 s: `Any` |
| Shock Wave | PrioritySkill | Crouch, Leap, Drop: `Pain` | Land 0 → 0.2 s `PrioritySkill`, then `Any` |
| Forbidden Decree | PrioritySkill | Enter, Channel: `Pain` | Exit: `Any` |

`CanInterruptState(p)` is `currentMinimum <= p`, so: Utility/Special (PrioritySkill) break Sunder's and Rebuke's wind-ups;
Rebuke (Skill) breaks Sunder's recovery; Sunder (Any) follows Rebuke, Cast Down, Shock Wave and Decree recoveries;
nothing of his own breaks Shock Wave's air phase or the Decree channel. Stun/freeze always cancel (vanilla
`SetStateOnHurt`).

---------------------------------------------------------------------------------------------------------------------------

## 5. Primary: Sunder (with sprint-switched Lunar Needles)

One custom SkillDef **`MithrixPrimarySkillDef : SkillDef`**: `InstantiateNextState` returns `LunarNeedle` when
`characterBody.isSprinting` (and `Primary.SprintNeedles` is true), else `SunderSmash`. `GetCurrentIcon` /
`GetCurrentNameToken` / `GetCurrentDescriptionToken` return the needle variants while sprinting (all three are virtual
in the reference; apicheck). Fields: `activationStateMachineName = "Weapon"`, `interruptPriority = Any`,
`cancelSprintingOnActivation = false`, `canceledFromSprinting = false`, `mustKeyPress = false`, `isCombatSkill = true`,
`autoHandleLuminousShot = true`. The Sunder state ends sprint itself on enter (`characterBody.isSprinting = false` on the
authority); the needle state keeps it. This is the boss's own conditional primary ("Sprint and FireLunarShards", D§2) and
it covers the default kit's flyer weakness without an extra skill.

### 5.1 SunderSmash (not sprinting)

*"The hammer falls precisely where he intends. Everything near it learns the difference."*

| Field | Value |
|---|---|
| Fantasy | "Break beneath me." A planted overhead verdict with one readable sweet spot |
| State flow | `SunderSmash` (single state, Weapon machine). Base duration **D = 1.00 s**, **Heavy** |
| Timing | wind-up 0 → **0.40 D**; impact at **0.40 D**; recovery 0.40 D → 1.00 D |
| Movement | `walkSpeedPenaltyCoefficient` 0.3 during wind-up, 0 for 0.1 s after impact, 0.6 during recovery. Usable in the air |
| Hit | one `BlastAttack` at the **impact point** = `footPosition + forward × Sunder.ForwardOffset (3.0 m)`, ground-snapped (raycast down 4 m on World layer; if no ground, the point as is). Radius **6 m**, `FalloffModel.SweetSpot` → **core** (inner half, 3 m) full damage, **rim** 25% (LAB L1) |
| Damage | **480%** core / **120%** rim (`Sunder.Damage = 4.8`), × Heavy damageMult |
| Proc | **1.0** |
| Force | 0 (no knockback: never scatters a teammate's target) |
| DamageSource | Primary |
| Interrupt | §4.1 |
| "Next Time" | If the blast result has **no hit point within the core** (`hitPoints[i].distanceSqr ≤ (r/2)²` for none), arm *Next Time* for **4 s**: the next Sunder uses radius **8 m** (core 4 m). Consumed by the next Sunder. Authority-local (`NextTimeTracker` component); a gold glint (`OmniImpactVFXBrotherLunarShardExplosion` clone, scale 0.3, local only) plays on `MuzzleSprintBash` while armed. The death line made into mercy: *perhaps we will get it right next time* |
| Ascendant rider | While `bdReclaimedMithrixAscendant` is active: at impact, fire **3 Sunder ripples** from the impact point + 1 m forward, yaw −15°/0°/+15°: projectile `ReclaimedMithrixSunderRipple`, **100%** each, proc **0.33**, Cripple, 40 m/s for 0.5 s (20 m), each ripple hits each enemy once. DamageSource Primary |
| Animation | `FullBody Override` / **`WeaponSlam`**, param `WeaponSlam.playbackRate` — **VERIFIED**. Fitted with `ClipFitter` (w = 0.40 D, r = 0.60 D). Exit crossfade to `FullBody Override/BufferEmpty` (VERIFIED) |
| Effects | Impact `BrotherSlamImpact_prefab 1f0d11e3dee8bd84d91e9a58ed4588ba` → `ReclaimedMithrixSlamImpact` (Soul), scale **0.5** (0.67 with Next Time), at the impact point, `transmit: true`. Hit effect `OmniImpactVFXBrotherLunarShardExplosion_prefab d69708703be36df4b80479bf25ecc56c` → `ReclaimedMithrixHitSpark`. Slam effect refs (`slamImpactEffect`, `weaponHitEffectPrefab`, `weaponImpactSound`) read from ESC `EntityStates_BrotherMonster_WeaponSlam_asset ee9a85d8393219c40bfa6772bc0fa455` |
| Ripples | clone of `BrotherSunderWave_prefab 2bf848c66467fdf49a7b43a330e8dd80` (ghost `BrotherSunderWaveGhost_prefab 65c201469fbfddf449b7118d6895ac3a`), `ProjectileSimple.desiredForwardSpeed = 40`, `lifetime = 0.5`, `ProjectileOverlapAttack.resetInterval = -1` (LAB L4), scale 0.5 |
| Sound | wind-up `Play_moonBrother_swing_vertical` (attack-speed sound); impact: ESC `weaponImpactSound` (NetworkSoundEventDef) via `EffectManager.SimpleSoundEffect`, fallback `Play_moonBrother_phaseJump_land_impact` |
| Telegraph | Hammer rises for 0.40 D (unmistakable to allies). **Local player only:** a 3 m (4 m with Next Time) core ring at the predicted impact point during the wind-up: a flat quad with a clone of `matBrotherCrippleWardRangeIndicator_mat ea7258fb9c75c1c4e840b3044d88e883` (Soul tint), config `Sunder.ShowCoreRing = true` |
| Item synergy | Core ≥400% → Kjaro's/Runald's/Singularity Band, Totem of Avarice, Runic Lens (+meteor per 100%); rim (120%) never triggers Bands, so cooldown items fire on the *precise* target. Luminous Shot is consumed by the first Sunder hit after a Secondary. Shuriken: one per activation (≈1/s base, 2.5/s at cap). Brilliant Behemoth: one explosion per enemy hit, bounded by Heavy. Breaching Fin launches enemies out of the core: documented anti-synergy |
| Multiplayer | No knockback, no ally displacement; the ring is local-only (no clutter) |

DPS reference (single target, base): core 480%/s; rim 120%/s (AoE). Weaving Rebuke into the recovery saves 0.6 D per
weave.

### 5.2 LunarNeedle (while sprinting)

*"In the fight he only fired these while running. He still prefers to."*

| Field | Value |
|---|---|
| State flow | `LunarNeedle` (Weapon machine), one needle per state at state entry. Duration **0.25 s / min(A, 3)**. Keeps sprint (**Agile**) |
| Projectile | `ReclaimedMithrixNeedle`: clone of `LunarShardProjectile_prefab 7fda9d7db9976544fa4061679fe7604a` (ghost `LunarShardGhost_prefab c35e2bc3a972d434991543f8604d699b`). `ProjectileSimple.desiredForwardSpeed 120`, `lifetime 1.0`; `ProjectileDirectionalTargetFinder` lookRange 60, lookCone 20°, allowTargetLoss true; `ProjectileSteerTowardTarget.rotationSpeed 90`; `ProjectileImpactExplosion` blastRadius **2 m**, `blastProcCoefficient 0.7`, falloff None. Add any of these components the clone lacks; team-filtered |
| Damage | **70%** per needle × needle Heavy damageMult (K = 3) |
| Proc | **0.7** |
| Rate | 4/s base → 12/s cap (overflow → damage) |
| DamageSource | Primary |
| Interrupt | `Any` |
| Ascendant rider | fire `ReclaimedMithrixNeedleEnergized` instead: **+40% damage (98%)**, blast **3 m**, `CrippleOnHit` |
| Animation | `Gesture, Additive` **and** `Gesture, Override` / **`FireLunarShards`**, played together — **VERIFIED**. Legs keep the sprint cycle |
| Effects | muzzle `MuzzleflashLunarShard_prefab 6fe75f982b06bdd42ad9506e3eaf0343` at **`MuzzleLeft`** (VERIFIED); impact `OmniImpactVFXBrotherLunarShardExplosion` clone. Projectile/muzzle/sound fields from ESC `EntityStates_BrotherMonster_Weapon_FireLunarShards_asset b87e0703805f05c46a83c9ff31e17881` |
| Sound | ESC `fireSound`; fallback `Play_moonBrother_m1_laser_shoot`. At most one play per 0.1 s |
| Telegraph | Left-hand flash and the needle trail players already know from the moon |
| Item synergy | 2.8 proc-units/s at base: Ukulele, AtG, Tri-Tip, Brittle Crown, Sentient Meat Hook. Rose Buckler (+30 armor while sprinting) → +6.9% Superiority *while throwing*. Energy Drink/Goat Hoof make it the chase tool |
| Multiplayer | Homing needles clear flyers for the team |

DPS: 280%/s, safer and ranged, deliberately below Sunder's 480%/s core.

---------------------------------------------------------------------------------------------------------------------------

## 6. Secondary: Rebuke

*"He sweeps aside what was thrown at him, and whoever threw it."* ("Mistake.")

| Field | Value |
|---|---|
| State flow | `Rebuke` (Weapon machine; derive from Core `ReclaimedMeleeState`, `HitboxGroupName = "Weapon"`). Base **D = 0.80 s**, **Heavy** |
| Mechanics | Lunge **7 m** over 0 → 0.30 s along the aim direction with pitch clamped to **±30°** (reaches ledges and low flyers; usable in the air). Speed curve: copy `forwardVelocityCurve` from ESC `EntityStates_BrotherMonster_SprintBash_asset 5ee121172ad5c294eb2caee67f41a2bf` if present, else ease-out (v₀ = 2 × 7 / 0.30). Active frames **0.225 D → 0.425 D** (`AttackStartFraction 0.225`, `AttackEndFraction 0.425`, `EarlyExitFraction 0.425`), hit-stop 0.06 s. Lunge distance does not scale with attack speed |
| Damage | **450%**, `DamageType.Stun1s` |
| Proc | **1.0** |
| Force | 1000 **sideways** (perpendicular to the swing, toward the swing's sweep side), never away from the team |
| Projectile deletion | **Server copy only**, during active frames, every FixedUpdate: every `ProjectileController` whose `teamFilter.teamIndex` is an enemy of Mithrix's team, within **8 m** of `corePosition` and within **60°** of the aim direction, is destroyed (`Object.Destroy` on the server; networked) with `ReclaimedMithrixShatter` (clone of `OmniImpactVFXBrotherLunarShardExplosion`, scale 0.4) — **unless** `cannotBeDeleted == true` or the prefab name contains any of: `BrotherSunderWave`, `BrotherUltLine`, `BrotherFirePillar`, `BrotherSkyLeapBomb`, `BrotherGravityWell`, `BrotherMeteorStorm`, `BrotherBlockingWall`, `KarmaSpike`, `LunarCrawler`, `KarmaBomb` (config `Rebuke.DeleteDenylist`). The boss's `LunarShardProjectile` is **allowed**: the reclaimed king may swat his old self's needles |
| Cooldown / stocks | **5 s, 2 stocks**, `rechargeStock 1` |
| SkillDef | `interruptPriority Skill`, `cancelSprintingOnActivation true`, `mustKeyPress true`, `isCombatSkill true` |
| DamageSource | Secondary |
| Interrupt | §4.1 (commit until 0.425 D, then Any so Sunder follows instantly) |
| Animation | `FullBody Override` / **`SprintBash`**, param `SprintBash.playbackRate`, crossfade 0.05 — **VERIFIED**. Fitted with `ClipFitter` (impact = 0.225 D). On exit set ChildLocator **`SpinnyFX`** inactive (VERIFIED child; the boss does this) |
| Effects | swing `BrotherSwing1__Kickup_prefab ae3eb43aa425e0d48b842f9fb20c2610` at **`MuzzleSprintBash`** (VERIFIED) → `ReclaimedMithrixSwing`; hit spark `BrotherSwing1__Claw_prefab 404ea959e3a86cb41b9055164d973623` → `ReclaimedMithrixClaw` |
| Sound | `Play_moonBrother_swing_horizontal` |
| Telegraph | Hammer drawn across the body (0.18 s), then a gold arc; shatter puffs show deleted projectiles |
| Item synergy | Luminous Shot charges on every activation (2 stocks feed it fast); Stained Scimitar; Backup Magazine (+1 stock); Vile Censer (stunned target + follow-up skill hits); Death Mark (Stun + Cripple + item debuffs); Bands/Totem (450%); Electric Boomerang stun extends the window |
| Multiplayer | Deletion protects allies behind him; stun interrupts attacks aimed at teammates; sideways push only |

---------------------------------------------------------------------------------------------------------------------------

## 7. Utility

### 7.1 Lunar Slide (default)

*"Two charges, four directions, no hesitation. He leaves the floor sharper than he found it."*

| Field | Value |
|---|---|
| State flow | `LunarSlide` (Weapon machine; derive from Core `ReclaimedDashState`). **0.30 s fixed** |
| Mechanics | Direction = movement input relative to aim, snapped to the nearest of forward/back/left/right (no input → forward). `SpeedCoefficient 8.8` × `moveSpeed`, `EndSpeedFraction 0.3` → **≈12 m at base walk speed** (more while sprinting or with speed items, as vanilla dashes). Horizontal only; usable in the air; **Agile** (`cancelSprintingOnActivation false`). Speed curve: ESC `EntityStates_BrotherMonster_BaseSlideState_asset 4f5b2d1cdd4851a41bcbac75b3b0080e` `speedCoefficientCurve` if present (values copied), else Core ease-out. No i-frames (boss parity) |
| Cleanse | **Server copy, `OnEnter`:** remove every timed buff with `isDebuff && !isCooldown` that is **not** the associated buff of a DoT (`DotController.dotDefs[*].associatedBuff`: burn, bleed, poison, blight… stay). Cripple, slows, root, Weak, Pulverized, Lunar Ruin are cleansed. The boss's dash cleanse, D§2 |
| Crystal spikes | At t = 0, 0.10, 0.20 s, the authority fires `ReclaimedMithrixSpike` at the ground-snapped foot position: a stationary projectile that erupts after **0.5 s** — blast **2.5 m**, **150%**, proc **0.5**, `CrippleOnHit`, each spike hits each enemy once. Boss Karma-2 dash spikes (D§2) with **Lunar Ruin removed** (False Son's identity) |
| Spike prefab | Built at load: `PrefabAPI.CreateEmptyPrefab("ReclaimedMithrixSpike")` + `NetworkIdentity`, `ProjectileController` (ghost = clone of `PendingKarmicSpikeGhost_prefab 5c27c5495d595c4449ec1392f2f8f9a7`, Soul), `TeamFilter`, `ProjectileDamage`, kinematic `Rigidbody`, `ProjectileSimple` (speed 0, lifetime 0.6), `ProjectileImpactExplosion` (`lifetime 0.5`, `destroyOnEnemy false`, `destroyOnWorld false`, `impactOnWorld false`, `blastRadius 2.5`, `blastProcCoefficient 0.5`, falloff None, `explosionEffect` = clone of `KarmaSpikeBreakEffect_prefab 248c688b92eae674eaa0976412c0e54d`). Registered with `ReclaimedContent.AddProjectile`. (Cloning the Karma spike projectiles themselves is rejected: they may chain child projectiles with Lunar Ruin, UNVERIFIED) |
| Cooldown / stocks | **5 s, 2 charges**, `rechargeStock 1` |
| DamageSource | Utility (spikes) |
| Interrupt | SkillDef `PrioritySkill`; state `PrioritySkill` for its 0.30 s |
| Animation | **CANDIDATE** `DashForward` / `DashBackward` / `DashLeft` / `DashRight` (clip names, layer unknown). Resolver order: `Body/Dash<Dir>`, `FullBody Override/Dash<Dir>`, `Gesture, Override/Dash<Dir>`. **Fallback:** no override (locomotion keeps playing) — the dash effect hides most of the body |
| Effects | `BrotherDashEffect_prefab 86bf93d2f5c08724bad23aeb3b5a3e49` → `ReclaimedMithrixDash` at ESC `slideEffectMuzzlestring` (fallback root) |
| Sound | ESC BaseSlideState `soundString` (fallback none) |
| Telegraph | Body streak; spikes show their gold warning ghost for the full 0.5 s before erupting (visible to all) |
| Item synergy | Hardlight Afterburner (+2 charges, −33% cd → 4 dashes, 12 spikes per cycle). Spikes are skill hits for Infernal Mark, Nightmare Demon, Vile Censer. Cripple counts for Death Mark, Noxious Thorn spreads it. Blast Shower keeps its niche (DoTs). Strides of Heresy replaces the slot (loses spikes; fair) |
| Multiplayer | Cripple's −20 armor amplifies every ally's damage; the slow pins enemies for melee allies |

### 7.2 Cast Down (alt; unlock "Thorp!")

*"Mass, the first compound. He points, and the world moves to him."* Lore: two children throwing things into gravity
wells — *Thorp!* (Mithrix logbook, D§1); the Karma-2 `BrotherGravityWell` (D§2).

| Field | Value |
|---|---|
| State flow | `CastDown` (Weapon machine), **0.35 s fixed**, **Agile** (`cancelSprintingOnActivation false`). At 0.15 s the authority places the well |
| Placement | Aim raycast (World layer) up to **35 m**; if nothing is hit, the point at 35 m dropped to the ground (raycast down 30 m); if still nothing, cast fails softly (no cooldown spent: refund the stock) |
| Well | Projectile `ReclaimedMithrixWell` (clone of `BrotherGravityWell_prefab 3052676c7639d4a409599cc22b8511c4`, ghost `BrotherGravityWellGhost_prefab 919483185c864eb49b614751acf97bff`, Soul, scaled to read 12 m) spawned at the point with speed 0. Strip every vanilla component whose type name contains `Force`, `Pull`, `Tether` or `Damage` except `ProjectileDamage`/`ProjectileController`/`TeamFilter`; add `MithrixWellController` (server) |
| Pull (server) | For **2.5 s**, every FixedUpdate: for each enemy `CharacterBody` within **12 m** that is **not** `isChampion`, not `isBoss`, not a player, not on Mithrix's team: desired inward speed 10 m/s, accel 50 m/s²: `Δv = clamp(10 − v_inward, 0, 50·dt)` toward the centre (horizontal only if grounded, full 3D for flyers), scaled by `min(1, 500 / mass)`; applied with `CharacterMotor.ApplyForce(Δv·mass, alwaysApply: true)` or `Rigidbody.AddForce(Δv, VelocityChange)`. Every 0.5 s: `AddTimedBuff(bdCripple, 1.0 s)` to those bodies |
| Collapse | At 2.5 s: `BlastAttack` **8 m**, **600%**, proc **1.0**, falloff None, `CrippleOnHit`, DamageSource Utility, attacker = Mithrix. **Early collapse:** the server copies of `SunderSmash` (at impact) and `ShockWaveLand` (on enter) call `MithrixWellController.TryCollapseNear(owner, point, 4 m)`: strike the well to collapse it immediately (same damage). One well per Mithrix: a new cast collapses the old one at once |
| Cooldown / stocks | **9 s, 1 stock** |
| DamageSource | Utility |
| Interrupt | SkillDef `PrioritySkill`; state `PrioritySkill` to 0.20 s, then `Any` |
| Animation | `Gesture, Additive` + `Gesture, Override` / **`FireLunarShards`** — **VERIFIED** (left-hand point; legs free) |
| Effects | range ring: flat quad with clone of `matKarmaSlowZone_mat cf8cb22c17c63714181c682d52186a1f` (Soul), radius 12 m, child of the well (all peers); collapse: `KarmaBombExplosion_prefab fa6598e4590190b46aa66a9d2c2255e0` → `ReclaimedMithrixWellCollapse`, scale to 8 m |
| Sound | cast `Play_moonBrother_orb_slam_pre`; collapse `Play_moonBrother_phaseJump_land_impact` |
| Telegraph | The ring is visible to everyone for the whole 2.5 s and the ghost shrinks in the last 0.3 s |
| Choice vs Slide | Gives up escape and cleanse for the best grouping tool in the roster. Mobility then comes only from Rebuke and Shock Wave |
| Item synergy | Grouped packs feed Behemoth, Gasoline, Will-o'-the-wisp, Frost Relic, Ceremonial Dagger; the 600% collapse triggers Bands; Singularity Band adds a second hole; Hardlight +2 stocks (still one well at a time — each cast collapses the previous) |
| Multiplayer | Never moves allies, champions or bosses (cannot drag a boss off a teammate). Strongest team setup tool |

---------------------------------------------------------------------------------------------------------------------------

## 8. Special: Shock Wave (default)

*"The leap the Commencement arena taught everyone to fear, now landing where he chooses."*

**State flow (Weapon machine):** `ShockWaveCrouch` → `ShockWaveLeap` → `ShockWaveDrop` → `ShockWaveLand`.

| State | Duration | What happens |
|---|---|---|
| `ShockWaveCrouch` | **0.50 s**, Heavy | Rooted (`walkSpeedPenaltyCoefficient 0`). `Body/EnterSkyLeap`, param `SkyLeap.playbackRate` (**VERIFIED**) + `FullBody Override/BufferEmpty` (VERIFIED pattern). Sound: ESC `EntityStates_BrotherMonster_EnterSkyLeap_asset ec886cd6be7eeaa4088d0bbe746596c8` `soundString`. Min priority `Pain` |
| `ShockWaveLeap` | ≤ **1.00 s** (fixed) | Authority: launch up at 40 m/s for 0.25 s, then hold altitude (gravity cancelled by setting `characterMotor.velocity = 0` each tick). **Hidden** (all peers, `OnEnter`): `characterModel.invisibilityCount++`, `hurtBoxGroup.hurtBoxesDeactivatorCounter++`; server: `AddTimedBuff(bdHiddenInvincibility_asset c0de820bf082c874aa35fbf50021ad5d, 1.2 s)` (removed on exit). **Invulnerability limit:** hidden only if the previous hidden phase ended ≥ **3 s** ago (`MithrixLeapLimiter` on the body, timestamps on all peers from state entries); otherwise the leap is **visible** with server `bdArmorBoost_asset 87fa5068b6724ef41a470a1943f6f2a9` (+200 armor) instead. Steering: local-only reticle (clone of `BrotherMeteorStormPredictionEffect_prefab 415a9b625a588574a8fcc200891cc91a`, Soul, non-networked instance) at the aim raycast point (World layer, ≤ 60 m), horizontal distance clamped to **30 m** from takeoff, ground-snapped (raycast down 200 m; no ground → takeoff point). Press Special or Primary after 0.30 s to drop early. Fall damage ignored: `bodyFlags |= IgnoreFallDamage` on all peers from Leap enter to Land exit (restored). Animation: hidden; if visible, let locomotion play (Body layer `isGrounded` false) — holding the last EnterSkyLeap frame via `SkyLeap.playbackRate = 0` is CANDIDATE |
| `ShockWaveDrop` | 0.25 s hang + fall ≤ 0.5 s | Authority: teleport to reticle + 12 m up (`TeleportHelper.TeleportBody`), spawn a **transmitted** prediction marker (same clone, 0.6 s lifetime) at the landing point so **allies see where he lands ~0.4 s early**; un-hide (counters released; hidden-phase end time recorded); hang 0.25 s; then descend at **90 m/s** until grounded. Sound `Play_moonBrother_phaseJump_land_preWhoosh` at fall start |
| `ShockWaveLand` | **0.40 s** | Authority: `BlastAttack` **8 m**, **700%**, proc **1.0**, falloff None, at `footPosition`. Then a **ring of 12 waves** at 30° spacing (aim yaw + k·30°) spawned on the blast edge (8 m out) so enemies inside the blast are not also hit by overlapping waves: `ReclaimedMithrixShockWave`, **250%** each, proc **0.5**, `CrippleOnHit`, 40 m/s, lifetime 0.75 s (30 m), ground-hugging (vanilla wave behaviour), each wave hits each enemy once. `Body/ExitSkyLeap` (**VERIFIED**). Effects: `ReclaimedMithrixSlamImpact` scale 0.8 + `BrotherRing_prefab 729eaf2d6a477b342adf936502942d0f` → `ReclaimedMithrixRing`. Sound `Play_moonBrother_phaseJump_land_impact`. Server copy: opens the **Conquest** window and calls `TryCollapseNear` for Cast Down. Min priority `PrioritySkill` to 0.2 s, then `Any` |

| Field | Value |
|---|---|
| Cooldown / stocks | **12 s, 1 stock** |
| SkillDef | `interruptPriority PrioritySkill`, `mustKeyPress true`, `cancelSprintingOnActivation true` |
| DamageSource | Special (landing and waves) |
| Cancelled early | Stun/freeze before `ShockWaveLand` (boss parity: "freezing cancels the jump") → `OnExit` restores visibility/hurtboxes/flags and the authority **refunds the stock** (`skillLocator.special.AddOneStock()`) |
| **Conquest** | Server component `MithrixConquestTracker`: window = 3 s from Land enter. On `GlobalEventManager.onCharacterDeathGlobal`, if `attackerBody == mithrix` or `attackerMaster.minionOwnership.ownerMaster == mithrix.master`, and inside the window: refund **1 s** (elite 2 s, champion 4 s), total per cast clamped to **6 s** (50% of base cd). Delivery: R2API `INetMessage MithrixConquestRefund { NetworkInstanceId body; float seconds; }` sent to all clients; the peer with `hasEffectiveAuthority` calls `skillLocator.special.RunRecharge(seconds)` (host applies directly). Applies only if the Special slot's current SkillDef is Shock Wave |
| Waves prefab | clone of `BrotherSunderWave_prefab 2bf848c66467fdf49a7b43a330e8dd80` (ghost `65c201469fbfddf449b7118d6895ac3a`), `ProjectileOverlapAttack.resetInterval = -1` (LAB L4); flight loop `lsdBrotherShockwave_asset d50cb76ff9b76f74eb26339b38475fa0`; wave count/force read from ESC `EntityStates_BrotherMonster_ExitSkyLeap_asset fa7a58dee9fb6c747877c5089ff6ec30` for reference only, never written |
| Telegraph | 0.5 s crouch; the transmitted gold landing marker ~0.4 s before impact; slow bright waves that can be jumped |
| Item synergy | Lysate Cell (2 leaps); Alien Head/Purity/Brainstalks (cast ≈2.2 s self-limits); Bandolier and on-kill items feed Conquest; Eclipse Lite (12 s base cd → big barrier); Bands/Totem/Lens on the 700% landing; Superiority: visible-leap ArmorBoost (+200 armor) gives +20% damage on that landing (bounded, intended) |
| Multiplayer | Leap into a downed ally's attackers; allies always see the landing; waves cripple without knockback |

Invulnerable uptime ceiling: hidden ≤ 1.0 s per ≥ 4.0 s → **≤ 25%**, regardless of cooldown items.

---------------------------------------------------------------------------------------------------------------------------

## 9. Alt Special: Forbidden Decree → Ascendant (unlock "I Know Your Design")

*"Phase three: the hammer remembers its fire. Nine walls no longer — he needs only six. And this time, the ones beside
him are quickened, not burned."* (Big Spinny / "Forbidden Pizza", D§2; `UltEnter/UltChannel/UltExit`, D§6.)

**State flow (Weapon machine):** `DecreeEnter` (0.40 s) → `DecreeChannel` (1.60 s) → `DecreeExit` (0.40 s); all three
**Heavy**. Total 2.4 s at base.

| State | What happens |
|---|---|
| `DecreeEnter` | Rooted (penalty 0, horizontal velocity 0), can turn. `Body/UltEnter`, param `Ult.playbackRate` (**VERIFIED**). Flash `ItemStealEndMuzzleflash_prefab a7b49ef89e1f56d46b0d1b9bbff40e9d` → `ReclaimedMithrixDecreeFlash` at **`MuzzleUlt`** (VERIFIED). Sound: ESC `EntityStates_BrotherMonster_UltEnterState_asset 92f7945ec8ad9cb47be836172270f00f` `soundString`. Server: `bdArmorBoost` (+200 armor) until channel end. Server: reset the per-cast hit limiter. Min priority `Pain` |
| `DecreeChannel` | Rooted. **5 pulses** at channel time 0.40, 0.70, 1.00, 1.30, 1.60 × (D_channel / 1.6). Each pulse: **6 wall segments** at yaw θₖ = aimYaw + k·60° + p·6°·s (p = pulse index 0–4, s = +1 or −1 alternating per cast), prefab `ReclaimedMithrixDecreeLineL` when s = +1, `…LineR` when s = −1. Each segment: **500%**, proc **1.0**, `CrippleOnHit`, speed 30 m/s, lifetime 1.33 s (**40 m**), rotation from the vanilla RotateLeft/Right component, `ProjectileOverlapAttack.resetInterval = -1`. A 0.3 s gold ground indicator (`matBrotherUltIndicator_mat 3c1e3f7da5d063142895313765fb82aa` clone on a thin quad) is spawned (transmitted effect) along each of the 6 directions 0.3 s before every pulse. **Hit limiter (server):** each segment carries `ModdedDamageType MithrixDecree`; a server `HealthComponent.TakeDamage` prefix counts hits per (attacker, victim) since the cast's limiter reset and sets `damageInfo.rejected = true` beyond **2** → ≤ **1000%** per enemy per cast. Animation: `Body/UltChannel` is **CANDIDATE** (clip `BrotherArmature|UltChannel`); fallback hold the last frame of `UltEnter` (`Ult.playbackRate = 0`). Sounds: `Play_moonBrother_blueWall_slam_start` per pulse; `Play_moonBrother_blueWall_active_loop` on enter, `Stop_moonBrother_blueWall_active_loop` on exit (posted on our GameObject only; audition in lab) |
| `DecreeExit` | `Body/UltExit` (**VERIFIED**), `ChannelingExitEffect_prefab 8c6f8135aa1735e4b9bf48be263aa04d` → `ReclaimedMithrixDecreeEnd`. **Server: apply `bdReclaimedMithrixAscendant` for 10 s** (refresh, not stack). Min priority `Any` |

Prefabs: `ReclaimedMithrixDecreeLineL` = clone of `BrotherUltLineProjectileRotateLeft_prefab 8c4c1936ea6276842a4b94a5e5d040e0`,
`…LineR` = clone of `BrotherUltLineProjectileRotateRight_prefab f714304255ce4914c973bd0c47c49351`, ghost
`BrotherUltLineGhost_prefab 0b655a4f3d9438945b80a016e17fe93d` (Soul). The data-driven `BrotherUltUtils.EvaluateInSequence`
(D§5.4) is **not** used (API shape UNVERIFIED); angles are computed as above.

### 9.1 Ascendant (the form)

Buff `bdReclaimedMithrixAscendant` (visible, non-stacking, timed 10 s, `ReclaimedBuffs.Create`). Every peer drives the
cosmetics from the buff count; the server drives the aura.

| Effect | Spec |
|---|---|
| Sunder rider | 3 ripples per smash (§5.1) |
| Needle rider | Energized needles (§5.2) |
| **Royal Tempo** (team aura) | Server, every 0.5 s: every `CharacterBody` on Mithrix's team within **25 m** (including himself, players, drones, minions) gets `bdReclaimedMithrixRoyalTempo` for **1.0 s**: **+20% attack speed** (`RecalculateStatsAPI` `attackSpeedMultAdd += 0.20`), non-stacking — several Mithrix players do not stack it |
| Cosmetic | Hammer glow: enable the model's phase-3 glow objects (the former `EnableWithPhase.gameObjectsToEnable` targets, captured at build time before stripping the component; fallback: hammer material `_EmPower × 3`); eye trail: clone of `matBrotherEyeTrail_mat 0c4e580d76291bd45aae49b08cfd849e` (Soul) on a `TrailRenderer` at the eye bone (fallback none); one gold ring pulse (`ReclaimedMithrixRing`, scale to 25 m) at form start |

| Field | Value |
|---|---|
| Cooldown / stocks | **30 s from activation, 1 stock** (≈18.4 s of downtime after the 11.6 s cast + form at base). Recasting during Ascendant refreshes it to 10 s |
| SkillDef | `interruptPriority PrioritySkill`, `mustKeyPress true`, `beginSkillCooldownOnSkillEnd false` |
| DamageSource | Special (walls) |
| Uptime math | `U = min(1, 10 / C_eff)` with `C_eff = max(0.5, (30 − flat) × Π mult)` |

| Cooldown setup | C_eff | Ascendant uptime |
|---|---|---|
| Base | 30 s | 33% |
| 1 Alien Head (×0.75) | 22.5 s | 44% |
| Purity ×1 (−2 s) + 1 Alien Head | 21.0 s | 48% |
| Light Flux (×0.5) + 1 Alien Head | 11.25 s | 89% |
| Brainstalks / extreme CDR | 0.5 s | 100% (and Decree every ≈2.4 s cast — the same Brainstalks spam every Special allows) |

Value at base, single target: ripples +300% per smash (+62% Sunder DPS while active), +20% attack speed (also allies),
one Decree (≤1000% per enemy per cast). Averaged over a 30 s cycle ≈ +25–30% DPS; at full uptime ≈ +75% plus the team aura.
**Choice vs Shock Wave:** Shock Wave = mobility, i-frames, burst, Conquest chains. Decree = a rooted 2.4 s commitment for
arena control, a team buff and a cooldown-scaling form (the "Ascendant" CDR build).

---------------------------------------------------------------------------------------------------------------------------

## 10. Unlock: ***...Next Time***

> **Challenge:** *...Next Time* — "Defeat Mithrix while carrying Purity."
> Achievement `ReclaimedMithrixPurified` → unlockable `Characters.ReclaimedMithrix`. Lunar coin reward 5.

### 10.1 Why it is earned
- His last words: **"BROTHER... PERHAPS... WE WILL GET IT RIGHT... NEXT TIME..."** The only log in which he remembers his
  brother without bitterness is **Purity's**: *"It sounds like two brothers, chasing glass frogs in the sun."* You come
  back "next time" and bring him that memory.
- Purity is unlocked by *The Calm* (beat the game on Monsoon). Like Captain after *Washed Away*, the playable final boss
  arrives only after the player has properly beaten him.
- Purity is stealable (no `BrotherBlacklist`/`CannotSteal` tag): in phase 4 the King of Nothing may literally hold the memory
  of his brother, and striking him returns it — the player *reclaims* it. The condition does not depend on the steal roll.
- It teaches the kit: cooldowns are one of his scaling axes (Rebuke stocks, Shock Wave, Decree uptime).

### 10.2 Exact server facts (`PurificationSpec`)
```csharp
var spec = new PurificationSpec("ReclaimedMithrix", "Mithrix", "BrotherHurtBody")
{
    FightStartBodyNames  = new[] { "BrotherBody" },
    AchievementName      = "...Next Time",
    AchievementDescription = "Defeat Mithrix while carrying Purity.",
    RequireParticipation = true,      // Core: dealt damage to the boss OR alive at the defeat
}
.AddCondition("fight on Commencement", ctx => ctx.SceneName == "moon2" || ctx.SceneName == "moon")
.AddCondition("reach his final phase",  ctx => Flag(ctx, "Mithrix.P4"))
.AddCondition("carry Purity",           ctx => Flag(ctx, "Mithrix.PurityP3") || Flag(ctx, "Mithrix.PurityP4")
                                               || ctx.ItemCount("LunarBadLuck") > 0);
// Flag(ctx,k) := Equals(ctx.GetRunFlag(k), true)
```
Run-flag trackers (server, Mithrix assembly, observe-only hooks; the boss is unchanged):
| Hook | Action (for every `PlayerCharacterMasterController` master m) |
|---|---|
| `On.EntityStates.Missions.BrotherEncounter.Phase1.OnEnter` | `SetRunFlag(m, "Mithrix.P4", false)`, `"Mithrix.PurityP3", false`, `"Mithrix.PurityP4", false` (new encounter) |
| `…Phase3.OnEnter` | `SetRunFlag(m, "Mithrix.PurityP3", HasPurity(m))` |
| `…Phase4.OnEnter` (ESC `abc3df4025df7034299e17698e8b4fb7`) | `SetRunFlag(m, "Mithrix.P4", true)`; `SetRunFlag(m, "Mithrix.PurityP4", HasPurity(m))` — taken before the item steal starts |
`HasPurity(m)` = `m.inventory.GetItemCount(RoR2Content.Items.LunarBadLuck) > 0` (resolve through the catalog, not a GUID:
the folder has two keys, `Items_LunarBadLuck_asset b0bfb8bceb0b7514bbe1409318d57f28` and `LunarBadLuck_asset
f9a779bd46c2b86419c0688b79b75d22`).

Facts this encodes: victim body is the vanilla `BrotherHurtBody` (never our clone); the scene is Commencement (excludes
Simulacrum's `ITBrotherBody` and modded spawns); Purity held at phase 3 or 4 entry or at the kill (independent of the steal
and of `ReturnStolenItemsOnGettingHit` ordering); the player participated (dealt damage or alive — a teammate who carried
Purity and died in phase 4 after damaging him still qualifies). Any survivor, difficulty, Karma level or artifact set;
escaping is not required (purification happens at the defeat, as Purified Freedom). Artifact of Swarms: two
`BrotherHurtBody` deaths evaluate twice; granting is idempotent.

### 10.3 No collision with False Son
| Achievement | Item | Ally / kill credit | Debuff | Survivor | Boss |
|---|---|---|---|---|---|
| Purified Freedom (False Son) | Halcyon Seed | — | — | any | False Son |
| Family Bonding (Meridian's Will) | — | Aurelionite gets the kill | Lunar Ruin on Mithrix | False Son | Mithrix |
| Commando: Retribution | — | Commando last hit | — | Commando | Mithrix at 8 Karma |
| ***...Next Time*** | **Purity** | **none** | **none** | **any** | Mithrix |
Nothing overlaps or excludes: a False Son player carrying Purity can earn Family Bonding and ...Next Time in one fight —
different stories, same fight. The kit also has no Lunar Ruin anywhere (§7.1 strips it).

### 10.4 Fallback and debug
`Unlock.Condition = Purity | GlassFrog | Either` (default `Purity`). `GlassFrog`: the player petted Commencement's glass
frog at least once this run (`On.RoR2.FrogController.Pet` on the server, credited to the interactor's master as run flag
`"Mithrix.FrogPetted"`; prefab `FrogInteractable_prefab d46096aadce023e4d8743d60e500508d`) — the other half of the same
memory, for players without *The Calm*. `reclaimed_grant_unlock ReclaimedMithrix` stays behind Core's cheat flag.

**Unlock moment:** 2.5 s after the vanilla death line, a system chat line to everyone:
`<style=cIsLunar>...and something on the moon was reclaimed.</style>` (token `RECLAIMED_MITHRIX_UNLOCK_CHAT`).

### 10.5 Skill and skin unlocks (all prerequisite `ReclaimedMithrixPurified`)

| Achievement id → unlockable | Name | Condition (server facts) |
|---|---|---|
| `ReclaimedMithrixThorp` → `Skills.ReclaimedMithrix.CastDown` | **Mithrix: Thorp!** | `PurificationSpec("ReclaimedMithrixThorp", "Worm", "MagmaWormBody", "ElectricWormBody")` + `ctx.PlayingBody("ReclaimedMithrixBody")` (participation as Core). Lore: "his brother loved worms" |
| `ReclaimedMithrixKnowDesign` → `Skills.ReclaimedMithrix.ForbiddenDecree` | **Mithrix: I Know Your Design** | `PurificationSpec("ReclaimedMithrixKnowDesign", "Mithrix", "BrotherBody")` (P3 BrotherBody "dies" into phase 4, LAB L7) + scene moon2 + `PlayingBody` + `IsAlive` + flag `"Mithrix.UltSeen"` + NOT flag `"Mithrix.HitByUlt"`. Trackers: `Phase1/Phase3.OnEnter` clear both flags; `On.EntityStates.BrotherMonster.UltChannelState.OnEnter` (vanilla body only, observe) sets `UltSeen` on all player masters; `GlobalEventManager.onServerDamageDealt` with `inflictor` name containing `BrotherUltLine` and a `ReclaimedMithrixBody` victim sets `HitByUlt` on that master. Fallback if L7 fails: trigger on `Phase4.OnEnter` through an own `BaseServerAchievement` |
| `ReclaimedMithrixMirror` → `Skins.ReclaimedMithrix.Phantom` | **Mithrix: The Mirror** | `PurificationSpec("ReclaimedMithrixMirror", "Mithrix", "BrotherHurtBody")` + scene moon2 + `PlayingBody("ReclaimedMithrixBody")` |
| `ReclaimedMithrixClearGameMonsoon` → `Skins.ReclaimedMithrix.Mastery` | **Mithrix: Mastery** | `class MithrixMastery : BasePerSurvivorClearGameMonsoonAchievement`, `LookUpRequiredBodyIndex() => BodyCatalog.FindBodyIndex("ReclaimedMithrixBody")`, `[RegisterAchievement("ReclaimedMithrixClearGameMonsoon", "Skins.ReclaimedMithrix.Mastery", "ReclaimedMithrixPurified", 10, null)]` |

For the three spec-based ones set `AchievementId`, `UnlockableName`, `AchievementName`, `AchievementDescription` explicitly
(defaults would be `<Id>Purified` / `Characters.<Id>`). Unlockables via `UnlockableFactory.Create(cachedName, nameToken,
icon, achievementId)`; attach with `SkillFamilyBuilder.AddVariant(slot, def, unlockable)` and `SkinFactory` (§13.4).

---------------------------------------------------------------------------------------------------------------------------

## 11. Item synergy and 100+ item stability

### 11.1 DamageSource and proc map (every damaging object)

| Object | Slot / DamageSource | Coefficient | Proc | Hits per enemy | ≥400% | Damage types |
|---|---|---|---|---|---|---|
| Sunder core / rim | Primary | 480% / 120% | 1.0 | 1 per smash | core ✓ | — |
| Sunder ripple (Ascendant) | Primary | 100% | 0.33 | ≤3 per smash | ✗ | CrippleOnHit |
| Lunar Needle (Energized) | Primary | 70% (98%) | 0.7 | 1 | ✗ | (Energized: CrippleOnHit) |
| Rebuke | Secondary | 450% | 1.0 | 1 | ✓ | Stun1s |
| Slide spike | Utility | 150% | 0.5 | 1 per spike | ✗ | CrippleOnHit |
| Cast Down collapse | Utility | 600% | 1.0 | 1 | ✓ | CrippleOnHit |
| Shock Wave landing | Special | 700% | 1.0 | 1 | ✓ | — |
| Shock Wave wave | Special | 250% | 0.5 | 1 per wave | ✗ | CrippleOnHit |
| Decree wall | Special | 500% | 1.0 | ≤2 per cast | ✓ | CrippleOnHit |

### 11.2 Axes and their bounds

| Axis | Kit mechanism | Bound |
|---|---|---|
| Proc chance (Tri-Tip, Ukulele, AtG, Meat Hook) | discrete hits; child projectiles ≤0.5 proc (ripples 0.33) | Heavy cap 2.5× (needles 3×); no ticking zones |
| Big-hit items (Bands, Totem, Runic Lens, Laser Scope) | six ≥400% sources | the items' own cooldowns / chance caps; Heavy efficiency `e` lever |
| Attack speed | Heavy: speed to 2.5×, then damage | hit frequency fixed after the cap |
| Cooldown (Alien Head, Purity, Light Flux, Brainstalks) | Rebuke/Slide stocks, Shock Wave, Ascendant uptime | cast times (Shock Wave ≈2.2 s, Decree 2.4 s); invulnerable ≤25% |
| Charges (Backup Mag, Hardlight, Lysate) | Rebuke, Slide/Cast Down, Specials | one well at a time |
| On-kill (Gasoline, Wisp, Dagger, Frost Relic, Bandolier) | AoE kills, Conquest | Conquest ≤6 s per cast |
| Defence (armor, health) | Adaptive Armor + Superiority | Adaptive ≤+100, fraction-based; Superiority ≤+30% |
| DamageSource items (Luminous Shot, Shuriken, Stained Scimitar, Infernal Mark, Vile Censer, Nightmare Demon, Breaching Fin, Eclipse Lite, Hardlight, Lysate) | correct tags on every object (§11.1) | per item; nothing re-tags damage |

### 11.3 Notable interactions (S = build-defining, A = great, C = weak/anti)
- **S:** Runic Lens (per-100% scaling on 480–700% hits); Brilliant Behemoth (bounded by Heavy); Alien Head (stocks, Shock
  Wave, Ascendant uptime); Shaped Glass (doubles damage; halved HP makes hits bigger fractions → more Adaptive Armor).
- **A:** Bands/Totem; Syringe/Mocha (never wasted); Luminous Shot (Rebuke charges, Sunder spends); Vile Censer + Rebuke stun;
  Death Mark; Prison Matrix/Opal/Kinetic Dampener/Repulsion Plate (Superiority); Hardlight; Backup Mag; Lysate; Purity
  (−2 s on 5 s cooldowns, bad luck hurts proc builds: a real trade); Bandolier/Gasoline/Wisp/Frost Relic; Rose Buckler with
  needles; Hunter's Harpoon/Energy Drink/Goat Hoof (mobility gap); Light Flux for the Decree build.
- **C (documented, not fixed):** Breaching Fin (launches enemies out of Sunder's core); Warped Echo (splits hits under the
  2% Adaptive gate; still reduces damage); Shattering Justice/Infernal Mark on Sunder-only play (hit-count items).
- **Heresy:** Visions replaces the whole contextual primary; Hooks/Strides/Essence replace their slots. Passive and form
  never require a slot to deal damage, so nothing breaks.
- **Tooltips name the bounds** (Heavy keyword, "Hold up to 2", "each enemy at most twice").

### 11.4 Hard budgets (per Mithrix, any item count)
| Budget | Value | Enforced by |
|---|---|---|
| Hammer hits per target per second | ≤2.5 (Sunder) | Heavy |
| Needles per second | ≤12 | needle Heavy (K = 3) |
| Projectiles alive, worst case | ≈ 12 needles + 3 ripples + 12 waves + 6 spikes + 1 well + 30 Decree segments ≈ 64 | per-skill caps |
| Child-projectile proc | ≤0.5 | spec |
| Ticking damage objects | **0** | spec (no fire pillars) |
| Kill refund | ≤6 s per Shock Wave cast | server clamp |
| Superiority / Adaptive | ≤+30% / ≤+100 armor | formulas |
| Invulnerable uptime | ≤25% | 3 s limiter |
| Decree damage per enemy per cast | ≤2 walls (1000% base) | server limiter |

---------------------------------------------------------------------------------------------------------------------------

## 12. Balance risks and counter-measures

| # | Risk | Counter-measure / lever (config) |
|---|---|---|
| 1 | Heavy overflow inflates big-hit items late (Lens, Bands at 1000%+) | `Heavy.OverflowEfficiency` 1.0 → 0.75; items keep their own cooldowns/chance caps |
| 2 | Superiority + armor stacking becomes a damage engine | hyperbola ≤30%; `Passive.SuperiorityMax`, `Passive.SuperiorityHalfPoint` |
| 3 | Adaptive Armor trivialises early game | 2% gate, 100 cap, 2.5 s hold, 30/s decay; `Passive.AdaptivePerFraction` 250 → 200 |
| 4 | Shock Wave i-frame chaining with CDR | 3 s hidden-phase limiter (≤25% uptime); visible leaps get ArmorBoost only |
| 5 | Conquest makes Shock Wave permanent in swarms | ≤6 s refund per cast; cast itself ≈2.2 s |
| 6 | Rebuke trivialises boss patterns | `cannotBeDeleted` + boss-pattern denylist; 8 m/60° cone; active frames only; 5 s cd |
| 7 | Cast Down griefs (pulls bosses/allies, pit kills) | champions, bosses, players and allies never pulled; 1 well; `min(1, 500/mass)` |
| 8 | Slide cleanse trivialises debuff content | timed non-DoT debuffs only; `Slide.Cleanses` toggle |
| 9 | Permanent Ascendant + Royal Tempo at extreme CDR | riders bounded; aura non-stacking; Decree ≤2 hits/enemy; `Decree.Cooldown`, `Ascendant.Duration` |
| 10 | Sprint-switch confuses auto-sprint players | `Primary.SprintNeedles` toggle (false = always Sunder); icon swaps while sprinting |
| 11 | Big hurtbox + wind-ups feel bad on Eclipse | regen 2.5 and mass 300; tune `Sunder.WindupFraction` 0.40 → 0.35 before touching damage |
| 12 | Boss-blue VFX confuse players in Commencement | Soul gold palette for every skin; never boss blue |
| 13 | Vanilla Mithrix logic treats our body as the boss | no "Brother" in any of our names; separate BodyIndex; strip boss components (§13.1); LAB L6 |
| 14 | Network load (4 Mithrix players, needles) | needle cap; Superiority/Adaptive stacks only sent on change; server-side limiter is O(hits) |
| 15 | Heavy full-body animations at high attack speed look broken | `ClipFitter` skips wind-up frames instead of exceeding 6× playback |

**Tuning order** (cheapest, most readable first): wind-up fractions → cooldowns → coefficients → caps → passive constants.
**Targets to measure in the lab:** Sunder core DPS 450–500%/s at base; Shock Wave cast ≈2.2 s; Adaptive bar empties in
≈3.3 s from cap after the hold; Decree average 1.5–2 wall hits per enemy in a pack.

---------------------------------------------------------------------------------------------------------------------------

## 13. Implementation map

### 13.1 Body (`SurvivorSpec`)
```csharp
new SurvivorSpec {
  Name = "Mithrix", BodyName = "ReclaimedMithrixBody", TokenPrefix = "RECLAIMED_MITHRIX",
  SourceBodyGuid = MithrixKeys.BrotherBody,                 // 428a5d7ff6fb5074fb061b82198e65fa
  Stats = { MaxHealth=160, LevelMaxHealth=48, HealthRegen=2.5f, LevelHealthRegen=0.5f, Damage=12, LevelDamage=2.4f,
            Armor=20, MoveSpeed=7, Acceleration=60, JumpPower=15, JumpCount=1, Crit=1 },
  ModelScale = Body.TargetHeight / bossCapsuleHeight,  CapsuleHeight = 2.5f, CapsuleRadius = 0.8f,
  Camera = { BaseParamsGuid = GameKeys.CameraStandardTall, IdealLocalCameraPos = new Vector3(0, 1.0f, -13f) },
  PodGuid = null,                                           // too large for a pod: LunarDescent instead
  SpawnStateType = typeof(LunarDescent), DeathStateType = typeof(MithrixDeath),
  Hull = HullClassification.Golem, BodyColor = new Color(0.95f, 0.82f, 0.50f), SortPosition = 121f,
  RequiredExpansion = "",                                   // base game: Mithrix needs no DLC
  RemoveComponents = { "EnableWithPhase", "KarmaBodyBehaviour", "BrotherSuperArmorController", "BrotherSpeechDriver",
                       "CharacterSpeechController", "ItemStealController", "ReturnStolenItemsOnGettingHit",
                       "BrotherSkyLeapHelper" },
  DisplayAnimationLayers = { "Body" }, DisplayAnimationStates = { "ExitSkyLeap", "Idle", "IdleReady" }, // ExitSkyLeap VERIFIED; idles CANDIDATE
  Unlockable = nextTimeUnlock, CustomizeBody = MithrixBodyCustomizer.Apply,
}
```
- `CustomizeBody` captures `EnableWithPhase.gameObjectsToEnable` (for the Ascendant glow) **before** the scrubber removes
  it (or read it from the boss prefab at load), adds `AdaptiveArmor` (fields §3.1), `NextTimeTracker`, `MithrixLeapLimiter`,
  `MithrixConquestTracker`, `MithrixDecreeLimiter`, `MithrixCosmetics` (overlay, Ascendant glow), footstep effect
  `BrotherFootstepDust_prefab 4005e78f24bb4dc4a9c6e3fff0198b53` (Soul, scale 0.5), and hides ChildLocator `SpinnyFX`.
- ItemDisplayRuleSet seeded from `idrsBrother_asset 8464d010b0eb5874087422831a1044e2` (cloned), then filled in the lab.
- Name safety: `BrotherKarmaUtils.IsBrother*` must return false for our body (LAB L6). None of our prefab/projectile clones
  contain "Brother", so Rebuke's denylist and community mods keyed on names never match us.
- **AI drivers** (`AIDrivers`): `ShockWave` Special 0–30 m require ready; `Rebuke` Secondary 0–10 m; `Slide` Utility when
  health < 50% (flee); `Sunder` Primary 0–5 m; `Needles` Primary 20–60 m with `ShouldSprint = true`; chase 0–60 m.

### 13.2 States (`Reclaimed.Mithrix.States`)
`LunarDescent` (Body; 1.2 s: model shown at 0.1 s, `Body/ExitSkyLeap` VERIFIED, `ReclaimedMithrixSlamImpact` scale 0.4,
`Play_moonBrother_phaseJump_land_impact`, no damage), `MithrixDeath : GenericCharacterDeath` (animator speed 0, spawn
`BrotherHelmetBreak_prefab 09636c4c0efc1d640b82b96a28bc31c7` at the head and `BrotherDeathEffect_prefab
954832bc10119384d8fc03e9243f0958` → Soul clones, hide the model at 0.6 s; never `SkyLeapDeathState`/`TrueDeathState`,
which drive the encounter), `SunderSmash`, `LunarNeedle`, `Rebuke`, `LunarSlide`, `CastDown`, `ShockWaveCrouch`,
`ShockWaveLeap`, `ShockWaveDrop`, `ShockWaveLand`, `DecreeEnter`, `DecreeChannel`, `DecreeExit`.

### 13.3 Skills
| SkillDef (asset name) | Family | State | Notes |
|---|---|---|---|
| `ReclaimedMithrixSunder` (`MithrixPrimarySkillDef`) | Primary | Sunder/LunarNeedle | §5 |
| `ReclaimedMithrixRebuke` | Secondary | `Rebuke` | cd 5, stocks 2 |
| `ReclaimedMithrixSlide` | Utility | `LunarSlide` | cd 5, stocks 2, Agile |
| `ReclaimedMithrixCastDown` | Utility (alt, unlockable) | `CastDown` | cd 9, Agile |
| `ReclaimedMithrixShockWave` | Special | `ShockWaveCrouch` | cd 12 |
| `ReclaimedMithrixDecree` | Special (alt, unlockable) | `DecreeEnter` | cd 30 |
Passive via `SkillFamilyBuilder.SetPassive` (§15 text). Icons: `IconFactory` placeholders until generated art exists
(Sunder Ring, Rebuke Chevron, Slide Bars, Cast Down Circle, Shock Wave Star, Decree Cross; colour (0.95, 0.82, 0.50)).

### 13.4 Skins (recolours of existing materials only)
Core `SkinFactory.AddRecolor` applies one tint to every material; Mithrix needs per-material values, so it uses Core
request C4 (`SkinFactory.AddSkin(BuiltSurvivor, SkinSpec)` with per-renderer material overrides and
`gameObjectActivations`). Material edits are on clones; `Shift(color, target, s)` = `EffectFactory.Shift`. Shader
properties (hopoo standard): `_Color`, `_EmColor`, `_EmPower`. VFX palette is **Soul** for every skin.

| Skin | Unlock | Renderer materials (clone) → values |
|---|---|---|
| **Reclaimed** (default) | — | Source infos `skinBrotherBodyDefault_asset 9f70f6d6114130041967d933f25db793`. `matBrother f47fb45fdc20b9c4c841039195a72023`: `_Color` Shift → (0.92, 0.88, 0.80), s 0.25. `matBrotherCloth a4cb334d615d6d84eb69fd35f30adb61`: Shift → (0.95, 0.90, 0.78), s 0.60. `matBrotherArmor 31a8c6a3c05118743b8e18a20b042637`: Shift → (0.85, 0.78, 0.62), s 0.35. `matBrotherHammer 0bb2107d4b60946498a890c7f7acd7bc`, `matBrotherWeapon 8741d5403bf9b2a4bb66cb45407df0b0`, `matBrotherStib a73788b69725dd54da8b20c35052d79c`: Shift → (1.00, 0.85, 0.50), s 0.80; `_EmColor` = (1.00, 0.78, 0.38). `matBrotherEye 3fcd0cb1c0e429b418d926d56708fd9d`: `_EmColor` = (1.00, 0.86, 0.55), Shift s 1.0. Must read "not the boss" at a glance |
| **Apocalypse Aspirant** (mastery) | Mithrix: Mastery | Source infos `skinBrotherBodyKarma_asset 2fd241dd9b1c8bb4f9b8c20b5ed7874e`. `matBrotherKarma 782217e749bf3164db4de98cc99fdb62`: Shift → (0.30, 0.24, 0.40), s 0.50; `_EmColor` = (0.78, 0.50, 1.00). `matBrotherEyeKarma 3ff2f7703ded4de449bafac32d43689f`: `_EmColor` = (0.85, 0.60, 1.00). Regalia (static, `gameObjectActivations`): `BrotherKarmaShoulderArmor 7fd6ebc2695ebff43887ede3e22b386e`, `BrotherKarmaNeckPiece 2573f5333ac9b6d46a0a877f4a02b811`, `BrotherKarmaBelt 5d69f046424ca1649b6d3d03edd15679`, `BrotherKarmaWaistCape 64276a3c265ee37459c5f2894b610e82`, `BrotherKarmaMedallion 8bc52584b9363234a8057c0013fb84ab`, `BrotherKarmaLegArmor d59f66b7fa9742c4bb8c30fdf955dd8d` instantiated under the parents `KarmaBodyBehaviour` uses (read from the boss prefab's component before stripping, UNVERIFIED field names), disabled by default, material = the tinted Karma clone. Fallback: material recolour only |
| **Phantom of Nothing** | Mithrix: The Mirror | `matPhantomBrother 8f14d5fea7fe5c649b083ec3a2c3b441`, `matPhantomBrotherArmor cdd8d892e89a3be4b92db872478d342f`, `matPhantomBrotherCloth 6aa51060140b9634c99975bca0e65864`, `matPhantomBrotherEye 06da12d5f91554f499e0a22a60c47f55` replace the default renderers' materials by role: Shift → (0.82, 0.86, 0.92), s 0.50; eye `_EmColor` = (0.90, 0.92, 1.00). The ghost of the self he left behind (the unused Karma phantoms) |

### 13.5 Core requests (small, generic)
- **C1** `AdaptiveArmor`: instance flags `excludeDots`, `excludeFallDamage`, `excludeSelfDamage`, `excludeHazard` (all
  default false; Mithrix sets true).
- **C2** `ReclaimedSkillState`: `protected virtual float AttackSpeedForDuration => attackSpeedStat;` used by
  `ScaledDuration`, `ReclaimedMeleeState` and `PlayAttackSpeedSound` (Mithrix overrides with `min(A, K)`).
- **C3** `ReclaimedBuffs.SetStacksIfChanged(body, buff, stacks, minDelta)`.
- **C4** `SkinFactory.AddSkin(BuiltSurvivor, SkinSpec)` with per-renderer materials and `gameObjectActivations`.

### 13.6 Boss recognition (Mithrix assembly `MithrixRecognition`, server; the boss itself is unchanged)
Send with `Chat.SendBroadcastChat(new Chat.NpcChatMessage { sender = bossBody.gameObject, baseToken = token,
formatStringToken = fmt, sound = null })`, where `fmt` = `CharacterSpeechController.chatFormatToken` read at load from
`BrotherSpeechController_prefab b55bca296aece0d43a4146d117035790` (phase 4: `BrotherHurtSpeechController_prefab
ea76bb74159734e44bb0f14f3f541c8c`), fallback our `RECLAIMED_MITHRIX_BOSS_DIALOGUE_FORMAT`. Conditions: a living
`ReclaimedMithrixBody` player in the scene; each trigger at most once per encounter; ≥4 s between our lines; each line
delayed 2.5 s after its trigger so it follows his vanilla speech. Config `Flavor.BossRecognition`. Lines in §15.6.

| Trigger (server facts) | Hook |
|---|---|
| P1 start | `BrotherEncounter.Phase1.OnEnter` + 3 s |
| P1 start, a `FalseSonBody` player also present | same (replaces the plain P1 line) |
| P1 start, any player holds Purity | same (replaces; priority False Son > Purity > plain) |
| P2 throne | `Phase2.OnEnter` |
| P3 start | `Phase3.OnEnter` |
| Reclaimed Shock Wave lands within 30 m of the boss (first per phase) | server copy of `ShockWaveLand` |
| Boss kills the reclaimed player (P1/P3 vs P4 pools) | `onCharacterDeathGlobal`, attacker body name `BrotherBody` / `BrotherHurtBody`, victim ours |
| P4 start | `Phase4.OnEnter` |
| P4, a player who had Purity at P4 entry now has 0 (stolen) | server poll every 1 s during P4 |
| P4, reclaimed player holds a Heresy item (`CannotSteal`) | `Phase4.OnEnter` + 8 s |
| Boss death with the reclaimed player alive | `onCharacterDeathGlobal` victim `BrotherHurtBody` + 2.5 s; optional player reply +3 s (`Flavor.PlayerChatLines`) |

---------------------------------------------------------------------------------------------------------------------------

## 14. Config keys (BepInEx + Risk of Options via `ReclaimedConfig.Bind`, file `com.reclaimed.mithrix.cfg`)

```
[Stats]     BaseHealth=160 HealthGrowth=48 BaseRegen=2.5 RegenGrowth=0.5 BaseDamage=12 DamageGrowth=2.4
            BaseArmor=20 MoveSpeed=7 Acceleration=60 Mass=300
[Body]      TargetHeight=2.5 CapsuleRadius=0.8                                        (restart)
[Passive]   AdaptivePerFraction=250 AdaptiveMinFraction=0.02 AdaptiveMax=100 AdaptiveDecayDelay=2.5
            AdaptiveDecayRate=30 SuperiorityMax=0.30 SuperiorityHalfPoint=100
[Heavy]     Cap=2.5 OverflowEfficiency=1.0 NeedleCap=3.0
[Primary]   SprintNeedles=true
[Sunder]    Damage=4.8 Radius=6 ForwardOffset=3.0 Duration=1.0 WindupFraction=0.40 NextTimeRadius=8
            NextTimeWindow=4 ShowCoreRing=true RippleDamage=1.0 RippleProc=0.33 RippleCount=3
[Needles]   Damage=0.7 Proc=0.7 Interval=0.25 Radius=2 Speed=120 EnergizedBonus=0.4 EnergizedRadius=3
[Rebuke]    Damage=4.5 Cooldown=5 Stocks=2 Duration=0.8 LungeDistance=7 DeleteProjectiles=true DeleteRange=8
            DeleteHalfAngle=60 DeleteDenylist=BrotherSunderWave,BrotherUltLine,BrotherFirePillar,BrotherSkyLeapBomb,BrotherGravityWell,BrotherMeteorStorm,BrotherBlockingWall,KarmaSpike,LunarCrawler,KarmaBomb
[Slide]     Cooldown=5 Charges=2 Duration=0.3 SpeedCoefficient=8.8 Cleanses=true SpikeDamage=1.5 SpikeDelay=0.5
            SpikeRadius=2.5
[CastDown]  Damage=6.0 Cooldown=9 Range=35 PullRadius=12 PullDuration=2.5 PullSpeed=10 CollapseRadius=8
            StrikeCollapseRadius=4
[ShockWave] Damage=7.0 Radius=8 WaveDamage=2.5 WaveCount=12 Cooldown=12 CrouchDuration=0.5 MaxAirTime=1.0
            MaxRange=30 InvulnLimiter=3 ConquestPerKill=1 ConquestElite=2 ConquestChampion=4 ConquestCapFraction=0.5
            ConquestWindow=3
[Decree]    WallDamage=5.0 WallsPerPulse=6 Pulses=5 MaxHitsPerEnemy=2 WallRange=40 Cooldown=30
[Ascendant] Duration=10 TempoBonus=0.2 TempoRadius=25
[Anim]      MaxPlaybackRate=6 WeaponSlamLength=-1 WeaponSlamImpact=-1 SprintBashLength=-1 SprintBashImpact=-1
            EnterSkyLeapLength=-1 UltEnterLength=-1                                    (-1 = use fractions)
[Unlock]    Condition=Purity                                                           (Purity | GlassFrog | Either)
[Flavor]    BossRecognition=true PlayerChatLines=false
```

---------------------------------------------------------------------------------------------------------------------------

## 15. Tokens (English)

### 15.1 Survivor
| Token | Text |
|---|---|
| `RECLAIMED_MITHRIX_NAME` | Mithrix |
| `RECLAIMED_MITHRIX_SUBTITLE` | King of Nothing |
| `RECLAIMED_MITHRIX_OUTRO_FLAVOR` | ..and so he left, King of Nothing no longer, carrying something at last. |
| `RECLAIMED_MITHRIX_OUTRO_FAILURE` | ..and so he vanished, King of Nothing once more. |
| `RECLAIMED_MITHRIX_DEFAULT_SKIN_NAME` | Reclaimed |
| `RECLAIMED_MITHRIX_SKIN_MASTERY_NAME` | Apocalypse Aspirant |
| `RECLAIMED_MITHRIX_SKIN_PHANTOM_NAME` | Phantom of Nothing |
| `RECLAIMED_MITHRIX_UNLOCK_CHAT` | `<style=cIsLunar>...and something on the moon was reclaimed.</style>` |
| `RECLAIMED_MITHRIX_BOSS_DIALOGUE_FORMAT` | `<color=#c6d5ff><size=120%>Mithrix: {0}</color></size>` |

`RECLAIMED_MITHRIX_DESCRIPTION`:
```
Mithrix is a deliberate, oppressive bruiser who turns every blow he endures into the next one he lands.<style=cSub>

< ! > Sunder deals full damage only in its core. If you miss, your next Sunder is wider - perhaps you will get it right next time.

< ! > Rebuke stuns and swats projectiles out of the air. Use it to open a window for Sunder.

< ! > Wind-ups are commitments that only your Utility and Special can break, but every recovery can be cancelled.

< ! > Armor of every kind makes you hit harder. Let them strike you; then strike back.

< ! > Kills just after Shock Wave lands refund its cooldown. Leap from conquest to conquest.</style>
```

### 15.2 Passive and keywords
| Token | Text |
|---|---|
| `RECLAIMED_MITHRIX_PASSIVE_NAME` | Adaptive Superiority |
| `RECLAIMED_MITHRIX_PASSIVE_DESCRIPTION` | Taking heavy damage grants up to <style=cIsHealing>100 Adaptive Armor</style>, which fades when you are no longer struck. Every point of <style=cIsUtility>bonus armor</style> increases your damage, up to <style=cIsDamage>30%</style>. |
| `KEYWORD_RECLAIMED_MITHRIX_HEAVY` | `<style=cKeywordName>Heavy</style><style=cSub>Gets faster with attack speed up to <style=cIsDamage>+150%</style>. Attack speed beyond that increases its <style=cIsDamage>damage</style> instead.</style>` |
| `KEYWORD_RECLAIMED_MITHRIX_CRIPPLING` | `<style=cKeywordName>Crippling</style><style=cSub>Reduces armor by <style=cIsDamage>20</style> and movement speed by <style=cIsDamage>50%</style>.</style>` |
| `KEYWORD_RECLAIMED_MITHRIX_ASCENDANT` | `<style=cKeywordName>Ascendant</style><style=cSub>For 10 seconds, Sunder releases <style=cIsDamage>3 shockwaves</style>, Lunar Needles become <style=cIsDamage>Crippling</style>, and you and allies within 25m gain <style=cIsDamage>+20% attack speed</style>.</style>` |

### 15.3 Skills
| Token (`RECLAIMED_MITHRIX_<SLOT>_<KEY>_NAME/_DESCRIPTION`) | Name | Description |
|---|---|---|
| `PRIMARY_SUNDER` | Sunder | `<style=cKeywordName>Heavy</style>. Smash the ground for <style=cIsDamage>480% damage</style> at the hammer's core and <style=cIsDamage>120%</style> at its edge. While <style=cIsUtility>sprinting</style>, throw <style=cIsDamage>homing Lunar Needles</style> for <style=cIsDamage>70% damage</style> instead.` |
| `PRIMARY_NEEDLE` (sprint variant) | Lunar Needles | `<style=cIsUtility>Agile</style>. Throw a homing Lunar Needle for <style=cIsDamage>70% damage</style>.` |
| `SECONDARY_REBUKE` | Rebuke | `<style=cKeywordName>Heavy</style>. <style=cIsDamage>Stunning</style>. Lunge and swing for <style=cIsDamage>450% damage</style>, <style=cIsUtility>destroying enemy projectiles</style> before you. Hold up to 2.` |
| `UTILITY_SLIDE` | Lunar Slide | `<style=cIsUtility>Agile</style>. Dash in any direction, <style=cIsUtility>cleansing debuffs</style> and leaving 3 lunar crystals that erupt for <style=cIsDamage>150% damage</style> and are <style=cIsDamage>Crippling</style>. Hold up to 2.` |
| `UTILITY_CASTDOWN` | Cast Down | `<style=cIsUtility>Agile</style>. Open a gravity well that <style=cIsUtility>pulls</style> and <style=cIsDamage>Cripples</style> enemies for 2.5 seconds, then collapses for <style=cIsDamage>600% damage</style>. Strike it with Sunder or Shock Wave to collapse it early. <i>Thorp!</i>` |
| `SPECIAL_SHOCKWAVE` | Shock Wave | `Leap out of sight, <style=cIsUtility>invulnerable</style>, then crash down where you choose for <style=cIsDamage>700% damage</style>, releasing 12 <style=cIsDamage>Crippling</style> shockwaves for <style=cIsDamage>250%</style> each. Kills soon after landing <style=cIsUtility>reduce this cooldown</style>.` |
| `SPECIAL_DECREE` | Forbidden Decree | `<style=cKeywordName>Heavy</style>. Plant your hammer and release <style=cIsDamage>Crippling</style> lunar flame walls for <style=cIsDamage>500% damage</style> each (each enemy at most twice), then become <style=cIsUtility>Ascendant</style>. <style=cIsHealth>You cannot move while channeling.</style>` |
Keyword lists: Sunder [HEAVY, AGILE]; Rebuke [HEAVY, KEYWORD_STUNNING]; Slide [AGILE, CRIPPLING]; Cast Down [AGILE,
CRIPPLING]; Shock Wave [CRIPPLING]; Decree [HEAVY, CRIPPLING, ASCENDANT].

### 15.4 Achievements
| Token | Text |
|---|---|
| `ACHIEVEMENT_RECLAIMEDMITHRIXPURIFIED_NAME` / `_DESCRIPTION` | ...Next Time / Defeat Mithrix while carrying Purity. |
| `ACHIEVEMENT_RECLAIMEDMITHRIXTHORP_NAME` / `_DESCRIPTION` | Mithrix: Thorp! / As Mithrix, defeat a Magma Worm or an Overloading Worm. |
| `ACHIEVEMENT_RECLAIMEDMITHRIXKNOWDESIGN_NAME` / `_DESCRIPTION` | Mithrix: I Know Your Design / As Mithrix, survive the third phase of the Mithrix fight without being struck by his lunar flame walls. |
| `ACHIEVEMENT_RECLAIMEDMITHRIXMIRROR_NAME` / `_DESCRIPTION` | Mithrix: The Mirror / As Mithrix, defeat Mithrix. |
| `ACHIEVEMENT_RECLAIMEDMITHRIXCLEARGAMEMONSOON_NAME` / `_DESCRIPTION` | Mithrix: Mastery / As Mithrix, beat the game or obliterate on Monsoon. |

### 15.5 Logbook lore (`RECLAIMED_MITHRIX_LORE`, his voice, reclaimed)
```
Brother.

They came up the arm of my arena again. Small things. Soft things. Carrying their baubles up my stairs, as if a pocket full of trinkets were a reason to live.

I told them they were weak without them. I was right. I am always right about design.

I broke them, and they stood back up. I took their trinkets one by one, and still they struck. Every blow taught me something, and I hardened against it, as I always have. And when the last blow fell, I was the one who could not stand.

Afterward, in the dark, I sorted what I had taken. Bands. Bombs. Bloodied glass. And at the bottom, a small white seed, carried all that way by one of them. I knew it. I made it. It smelled of hot stone and tall grass. It was the shape of an afternoon - two brothers, and glass frogs, and the sun.

They had brought it back to me.

I do not ask to be forgiven. Forgiveness was always your compound, not mine. I was right about mass. I was right about design. I was wrong about what they were for.

So. Let the design be perfect. But let it be for them. Let the hammer fall where I mean it to, and let the small things behind me still be standing when it does.

I called for you, at the end. I said: perhaps we will get it right next time.

This is next time, brother. I am not good at it yet.

But I am learning.
```

### 15.6 Boss recognition lines (`RECLAIMED_MITHRIX_BOSS_<KEY>_<n>`)
| Key | Lines |
|---|---|
| `P1` | "You wear my shape. Badly." · "So. Even my design can be stolen." · "A king who kneels to vermin is no king." |
| `P1_FALSESON` | "The idol... and my own reflection. Is this your jest, brother?" |
| `P1_PURITY` | "That scent... hot stone, tall grass... No. Die." |
| `P2` | "Look at them, following you. They followed HIM like that." |
| `P3` | "Your design is flawed. You left room for them." · "Show me what you have become, then." |
| `LEAP` | "I taught you that leap." · "My own fall, aimed at me?" |
| `KILL` | "Return to nothing." · "You were never me." · "Soft. You have grown soft." |
| `P4` | "STOP LOOKING AT ME LIKE HE DID." · "WHAT... ARE YOU...?" |
| `P4_KILL` | "AS I THOUGHT... NOTHING." |
| `P4_PURITY` | "HOT STONE... TALL GRASS... WHY DO YOU CARRY THIS...?" · "THAT... IS NOT YOURS TO CARRY." |
| `P4_HERESY` | "MY DESIGNS... AND I CANNOT EVEN TAKE THEM BACK..." |
| `DEATH` | "...THEN YOU... ARE THE NEXT TIME..." · "BROTHER... DO YOU SEE...? WE GOT IT RIGHT..." |
| `REPLY` (player, optional) | "Rest. Both of us." |

### 15.7 Optional player chat (`Flavor.PlayerChatLines`, default off; chat only, never voiced)
Spawn: "Again." / "Stand behind me." / "Next time is now." · First Ascendant of a run: "Perfect." · Petting the glass frog:
"...Thorp." · Death: "Not... yet..."

---------------------------------------------------------------------------------------------------------------------------

## 16. Lab assumptions and in-game test checklist

**LAB assumptions (verify first; each has a fallback in the text)**
- **L1** `FalloffModel.SweetSpot` = 100% inside r/2, 25% outside. Fallback: core blast (r/2, `None`, **400%**) + rim blast
  (r, `None`, **100%**), core targets take both (500%).
- **L2** VERIFIED states play on a **remote client** body: WeaponSlam, SprintBash, FireLunarShards (both gesture layers),
  EnterSkyLeap, ExitSkyLeap, UltEnter, UltExit; their rate params respond. Resolve CANDIDATES: `UltChannel`, `Dash*`, display
  idles; record `AnimationResolver`'s picks.
- **L3** `reclaimed_mithrix_measure` yields clip length and impact for WeaponSlam (`blast.hitBoxActive`) and SprintBash
  (`weapon.hitBoxActive`); write them into `[Anim]`.
- **L4** Cloned `BrotherSunderWave` / `BrotherUltLine*` use `ProjectileOverlapAttack`; `resetInterval = -1` makes each hit
  once. Fallback: per-projectile `HashSet<HealthComponent>` filter component.
- **L5** Cloned `BrotherGravityWell` has no force/damage component left after stripping and does not pull players.
- **L6** `BrotherKarmaUtils.IsBrother*` returns false for `ReclaimedMithrixBody`.
- **L7** The P3 `BrotherBody` raises `onCharacterDeathGlobal` when it leaves for phase 4.
- **L8** Luminous Shot, Infernal Mark, Vile Censer, Nightmare Demon, Breaching Fin read our DamageSource tags.

**Checklist (each line: setup → expected)**
1. Lobby, Mithrix locked → CSS shows the lock with "...Next Time" how-to; `reclaimed_grant_unlock ReclaimedMithrix` (cheats on)
   unlocks it.
2. Spawn on stage 1 → `LunarDescent` lands with gold slam, no pod, no damage; camera frames hammer and ring.
3. Component dump (`reclaimed_dump_animator`, component list) → no `EnableWithPhase`, `KarmaBodyBehaviour`,
   `BrotherSuperArmorController`, speech, item-steal components; ChildLocator has `MuzzleLeft`, `MuzzleRight`, `MuzzleFloor`,
   `SlamZone`, `MuzzleSprintBash`, `MuzzleUlt`, `SpinnyFX`; HitBoxGroups `Weapon`, `WeaponBig`.
4. Sunder on a Beetle at the core → 480%; at the edge → 120% (damage numbers); Kjaro's Band procs only on core hits.
5. Whiff Sunder → glint; next Sunder within 4 s shows a 4 m ring and an 8 m impact.
6. Hold M1 while sprinting → needles every 0.25 s, sprint not broken, icon swaps; release sprint → Sunder.
7. Attack speed +150% / +400% (`give_item Syringe`) → Sunder duration pinned at 0.40 s; damage ×1.0 / ×2.0.
8. Sunder wind-up + press Rebuke → no cancel; press Slide → cancels. Sunder recovery + Rebuke → cancels.
9. Rebuke vs Lemurian fireballs and Mithrix needles → projectiles deleted with shatter; vs Sunder waves / ult lines → not
   deleted; enemies stunned 1 s; 2 stocks shown.
10. Slide while Crippled and burning → Cripple removed, burn stays; 3 spikes erupt 0.5 s later, Cripple enemies.
11. Shock Wave → invulnerable and hidden ≤1 s; local reticle; allies (second client) see the landing marker; landing 700% +
    12 waves; second leap within 3 s is visible with ArmorBoost; freeze during crouch refunds the stock.
12. Shock Wave into 10 Lemurians → cooldown drops by ≤6 s total.
13. Cast Down (unlocked via cheat) → enemies pulled, Cripple ticks, collapse 600% at 2.5 s; Sunder into the well → early
    collapse; boss and players never moved.
14. Forbidden Decree → 5 pulses of 6 gold walls with 0.3 s indicators; one enemy takes at most 2 wall hits; then Ascendant:
    ripples on Sunder, Crippling needles, an ally within 25 m shows Royal Tempo; recast refreshes 10 s; cooldown 30 s.
15. Take a 30% hit → Adaptive Armor +75 (buff stacks), overlay visible, decays after 2.5 s at 30/s; Superiority buff shows
    the matching %; fall damage and burn ticks grant nothing.
16. Prison Matrix ×3 + Opal ×5 → Superiority ≤30 stacks; damage stat matches the formula.
17. Two-player session (host + client playing Mithrix) → every skill animates on both screens; needles home on the client;
    Conquest refund reaches the client; Ascendant cosmetics show on both.
18. Commencement, Mithrix as any survivor holding Purity → defeat → "...Next Time" unlock + chat line for each participating
    player (including one who died in phase 4 after damaging him); without Purity → log names the failed condition.
19. Commencement as Mithrix → recognition lines at P1, P2, P3, P4 and death; no overlap with vanilla speech; the vanilla
    fight (phases, Karma, item steal, stagger) behaves exactly as without the mod.
20. False Son + Mithrix player, Purity, Aurelionite kill with Lunar Ruin → both Family Bonding and ...Next Time granted.
21. 100-item stress (Simulacrum or debug spawn: 10 Ukulele, 5 Behemoth, 3 Meat Hook, Syringes) vs 30 Lemurians, 4 players
    → frame time stable, needle rate ≤12/s each, no lingering projectiles.
22. Skins: Reclaimed reads gold/ivory (not boss blue); Apocalypse Aspirant shows the Karma regalia (or recolour fallback);
    Phantom of Nothing translucent silver; all VFX gold.
23. apicheck reports 0 broken.

---------------------------------------------------------------------------------------------------------------------------

## 17. Asset key table (`MithrixKeys`, all from `kb/asset_keys.tsv`)

| Constant | GUID | Use |
|---|---|---|
| `BrotherBody` | 428a5d7ff6fb5074fb061b82198e65fa | body source |
| `BrotherSpeechController` / `BrotherHurtSpeechController` | b55bca296aece0d43a4146d117035790 / ea76bb74159734e44bb0f14f3f541c8c | chat format token |
| `EscWeaponSlam` / `EscSprintBash` / `EscFireLunarShards` | ee9a85d8393219c40bfa6772bc0fa455 / 5ee121172ad5c294eb2caee67f41a2bf / b87e0703805f05c46a83c9ff31e17881 | ESC reads |
| `EscBaseSlide` / `EscEnterSkyLeap` / `EscExitSkyLeap` / `EscUltEnter` | 4f5b2d1cdd4851a41bcbac75b3b0080e / ec886cd6be7eeaa4088d0bbe746596c8 / fa7a58dee9fb6c747877c5089ff6ec30 / 92f7945ec8ad9cb47be836172270f00f | ESC reads |
| `BrotherSlamImpact` | 1f0d11e3dee8bd84d91e9a58ed4588ba | Sunder, Shock Wave, descent |
| `OmniImpactLunarShard` | d69708703be36df4b80479bf25ecc56c | hit spark, glint, shatter |
| `BrotherSwingKickup` / `BrotherSwingClaw` | ae3eb43aa425e0d48b842f9fb20c2610 / 404ea959e3a86cb41b9055164d973623 | Rebuke |
| `LunarShardProjectile` / `LunarShardGhost` / `MuzzleflashLunarShard` | 7fda9d7db9976544fa4061679fe7604a / c35e2bc3a972d434991543f8604d699b / 6fe75f982b06bdd42ad9506e3eaf0343 | needles |
| `BrotherSunderWave` / ghost | 2bf848c66467fdf49a7b43a330e8dd80 / 65c201469fbfddf449b7118d6895ac3a | ripples, Shock Wave waves |
| `LsdBrotherShockwave` | d50cb76ff9b76f74eb26339b38475fa0 | wave loop |
| `BrotherDashEffect` | 86bf93d2f5c08724bad23aeb3b5a3e49 | Slide |
| `PendingKarmicSpikeGhost` / `KarmaSpikeBreakEffect` | 5c27c5495d595c4449ec1392f2f8f9a7 / 248c688b92eae674eaa0976412c0e54d | Slide spikes |
| `BrotherGravityWell` / ghost | 3052676c7639d4a409599cc22b8511c4 / 919483185c864eb49b614751acf97bff | Cast Down |
| `KarmaBombExplosion` / `MatKarmaSlowZone` | fa6598e4590190b46aa66a9d2c2255e0 / cf8cb22c17c63714181c682d52186a1f | Cast Down |
| `BrotherMeteorStormPrediction` / `BrotherRing` | 415a9b625a588574a8fcc200891cc91a / 729eaf2d6a477b342adf936502942d0f | Shock Wave |
| `BdHiddenInvincibility` / `BdArmorBoost` / `BdCripple` | c0de820bf082c874aa35fbf50021ad5d / 87fa5068b6724ef41a470a1943f6f2a9 / a6a4458bb70c46d4aa7c9a7c6666a578 | buffs |
| `UltLineRotateLeft` / `UltLineRotateRight` / `UltLineGhost` | 8c4c1936ea6276842a4b94a5e5d040e0 / f714304255ce4914c973bd0c47c49351 / 0b655a4f3d9438945b80a016e17fe93d | Decree |
| `ItemStealEndMuzzleflash` / `ChannelingExitEffect` / `MatBrotherUltIndicator` | a7b49ef89e1f56d46b0d1b9bbff40e9d / 8c6f8135aa1735e4b9bf48be263aa04d / 3c1e3f7da5d063142895313765fb82aa | Decree |
| `MatBrotherShield` / `MatBrotherEyeTrail` / `MatCrippleWardRange` | 9508dece7208a9c4cb617dadcd00b3f9 / 0c4e580d76291bd45aae49b08cfd849e / ea7258fb9c75c1c4e840b3044d88e883 | cosmetics, rings |
| `BrotherHelmetBreak` / `BrotherDeathEffect` / `BrotherFootstepDust` | 09636c4c0efc1d640b82b96a28bc31c7 / 954832bc10119384d8fc03e9243f0958 / 4005e78f24bb4dc4a9c6e3fff0198b53 | death, steps |
| `IdrsBrother` | 8464d010b0eb5874087422831a1044e2 | item displays seed |
| `PhaseEncounter4` | abc3df4025df7034299e17698e8b4fb7 | unlock tracker reference |
| `FrogInteractable` | d46096aadce023e4d8743d60e500508d | GlassFrog fallback |
| skins / materials | see §13.4 | |

Animator controller: `animBrother_controller 6365ab5d2660de94384961d8f70015a2` (comes with the cloned model).

---------------------------------------------------------------------------------------------------------------------------

## Appendix A. Scores & synthesis rationale

Scores 1–10 (higher is better; for balance risk, higher = safer).

| Criterion | Lore pass | Mechanics pass | Synergy pass |
|---|---|---|---|
| Lore loyalty & recognizability | **9** | 8 | 7 |
| False-Son-model fidelity (reclaimed, not copied) | 8 | 8 | 8 |
| Fun / skill expression | 6 | **10** | 7 |
| Readability / telegraphs | 8 | 6 | 8 |
| Item synergy & late-game stability | 5 | 6 | **10** |
| Multiplayer value | 8 | 7 | **9** |
| Technical feasibility (VERIFIED states, assets) | 5 | 5 | **7** |
| Balance risk | 5 | 6 | **8** |
| **Total** | 54 | 56 | **64** |

**Winner: the synergy pass.** Its passive (Adaptive Superiority) is one rule that *is* the brief's "adaptive superiority",
Heavy is the single most important 100-item safeguard, every scaling axis has a documented ceiling, it has no ticking
zones, and its kit leans on VERIFIED states (WeaponSlam, SprintBash, FireLunarShards, Enter/ExitSkyLeap, UltEnter/Exit).

**Why not the others as the base.**
- *Lore pass* — the four-compound arc (Mass → Design → Blood → Soul) is the most beautiful idea of the three, but it makes
  power depend on Teleporter-boss kills (weak stage 1, snowball later), uses **Lunar Ruin** (False Son's identity and part
  of Family Bonding's condition), adds a ticking fire pillar, and attaches regalia to UNVERIFIED `KarmaBodyBehaviour`
  parents.
- *Mechanics pass* — the best skill expression (Precise, Perfect Dodge, counter-hits, Plunge, Flawless/Perfected), but seven
  interlocking systems, a latency-sensitive dodge window, a heuristic counter-hit detector, three primary modes and a
  hit-fed meter that grows with attack speed × AoE. Too much for "elegant, readable".

**Grafts into the synthesis.**
- From the mechanics pass: the **"Next Time"** whiff mercy on Sunder (the death line as a mechanic); the **3 s invulnerability
  limiter** on Shock Wave; server-copy **projectile deletion** with the boss-pattern denylist (and allowing his old needles);
  **all skills on one `Weapon` machine**; **fixed-fraction gameplay timing with animations fitted** to it; the
  **Cast Down** name and "strike the well to collapse it" (kept, without the Precise bonus); the GlassFrog fallback.
- From the lore pass: **sprint-switched needles** as the boss's own conditional primary (instead of a separate alt);
  the participation rule that credits a teammate who died in phase 4; the **Thorp!** and **I Know Your Design** unlocks;
  the Adaptive-Armor shield overlay telegraph; the *Phantom* skin; the unlock chat line; recognition lines and lore phrases
  ("hot stone and tall grass", "Forgiveness was always your compound").
- From the synergy pass (kept): passive, Heavy, Sunder's sweet-spot core, Rebuke, Lunar Slide's de-Ruined crystal spikes,
  Shock Wave + Conquest, Royal Tempo, Purity snapshot logic, budgets, item ratings, mastery skin source.

**Changed during synthesis.**
- *Ascend → Forbidden Decree*: the synergy pass's two-step form (Ascend, then a one-shot recast) needed a skill override or
  a paused-recharge SkillDef, both of which interact badly with `GenericSkill` stock on override removal. The final design
  is one press: channel the Decree, **then** become Ascendant; the cooldown (30 s) runs from activation. Same fantasy, same
  uptime math shape, no custom recharge code.
- Decree walls became 5 pulses of 6 travelling segments (how the vanilla ult-line projectiles actually behave) with a
  **server hit limiter** (≤2 per enemy per cast) instead of 6 static rotating walls.
- Shock Wave waves spawn at the blast edge so a target at ground zero is not hit by 12 overlapping waves.
- Slide spikes 200% → **150%** (free damage on a 2-charge utility was the largest single-target outlier).
- Needles 110% at 3.33/s (alt) → **70% at 4/s** in the sprint mode (280%/s, below Sunder's 480%/s core; Huntress-like).
- Phase III renamed **Ascendant** for players (the boss's "phase" vocabulary reads oddly on a survivor).

**Cut (and where the theme went instead).** Compound progression (theme kept in the logbook lore and as the Karma regalia
on the mastery skin); Design meter, Perfect Dodge, counter-hit, Plunge (skill expression kept in sweet-spotting, weaving, Rebuke
timing, Shock Wave routing, Decree placement); Blood Tithe (phase 4 lives in the unlock and the recognition lines);
fire pillars (no ticking zones); Lunar Ruin (False Son's); Soul barrier (team value comes from Royal Tempo, Cripple,
deletion and the pull); Needle Barrage as an alt (folded into the primary); item stealing (never: duplication risk, 100-item
breakage).

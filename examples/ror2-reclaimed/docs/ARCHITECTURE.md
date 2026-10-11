# Reclaimed — architecture

Reclaimed makes six Risk of Rain 2 bosses playable the way Seekers of the Storm made the False Son playable:

| | Boss (untouched, still fought) | Survivor (new body) |
|---|---|---|
| False Son (vanilla reference) | `FalseSonBossBody` on Prime Meridian | `FalseSonBody`, unlocked by "purifying" him |
| Mithrix, King of Nothing | `BrotherBody` / `BrotherHurtBody` on Commencement | `ReclaimedMithrixBody` |
| Voidling, Diviner of the Deep | `MiniVoidRaidCrabBodyPhase1-3` in the Planetarium | `ReclaimedVoidlingBody` |
| Echo of Providence | Providence encounter in Terminal Lucidity | `ReclaimedEchoBody` |
| Anima Harvester, Avatar of N'kuhana | `ReaperBossBody` in Soulbound Threshold | `ReclaimedHarvesterBody` |
| Solus Wing | `SolusWingBody` in Solutional Haunt | `ReclaimedSolusWingBody` |
| Solus Heart, Virtual Super Ego | `SolusHeartBody` (Alloyed Collective finale) | `ReclaimedSolusHeartBody` |

Three rules follow from the False Son model and drive every technical decision:

1. **The boss is never modified.** Survivors are separate prefabs cloned at load time
   (`PrefabAPI.InstantiateClone`). Vanilla EntityStates, static config fields, masters, spawn cards and
   director entries are left alone. Static fields on vanilla states are shared with the boss, so survivor
   skills are our own EntityStates that reuse the boss's *assets* (models, animations, effects,
   projectiles, sounds) but never its tuning.
2. **The survivor is the boss reclaimed, not shrunk.** Each kit is a reinterpretation of the fight
   (the same verbs, readable at player scale), a passive that carries the boss's identity, and a power
   curve that rewards items the way survivors do.
3. **The unlock is earned from the boss.** Each survivor is locked behind a "purification" achievement:
   defeat that boss under a thematic condition. The condition check runs on the server and the unlock is
   granted to each qualifying player.

## Layout

```
examples/ror2-reclaimed/
  Reclaimed.sln
  Directory.Build.props     shared build settings; RefsMode=Local (refs/) or Packages (NuGet)
  NuGet.config              nuget.org + nuget.bepinex.dev
  tools/fetch_refs.py       fills refs/ from nuget.org (or from the user's game + r2modman profile)
  tools/RefTools/           `shim` (GameLibs 1.3.1 -> current DamageSource API) and `apicheck`
  tools/package.py          builds the Thunderstore zip
  src/Reclaimed.Core/       shared frameworks (plugin com.reclaimed.core)
  src/Reclaimed.<Name>/     one plugin per survivor (com.reclaimed.<name>), depends on Core
  docs/                     this file, docs/survivors/<name>.md design docs, TESTING.md
  thunderstore/             manifest.json, icon.png, README/CHANGELOG for the package
  MODLOG.md                 the working journal
```

Each survivor is its own assembly so it can be balanced, disabled or shipped independently; a survivor
plugin only talks to Core through the public API below and never to another survivor.

## Build and verification

- `python3 tools/fetch_refs.py` then `dotnet build -c Release`. In environments with
  `nuget.bepinex.dev`, `dotnet build -p:RefsMode=Packages` restores the standard community packages
  (BepInEx.Core 5.4.21, RiskOfRain2.GameLibs 1.4.1-r.0) instead. Release builds should use Packages mode
  or `fetch_refs.py --game ... --bepinex ...` (the user's own install).
- `dotnet tools/RefTools/bin/Release/net8.0/RefTools.dll apicheck refs/mmhook/MMHOOK_RoR2.dll refs src/*/bin/Release/*.dll`
  checks every method/ctor/property we call on a RoR2 type against the live game's signatures
  (MMHOOK_RoR2 is regenerated for each patch), and fields against field references of R2API /
  RoR2BepInExPack builds for the live game. It must report 0 broken.
- Code that touches DLC3/DLC4-only types does it by Addressables GUID and by component/type *name*
  at runtime (the compile references predate those DLCs). That keeps one binary working whether or not
  the player owns a DLC; a survivor whose DLC is not owned is hidden (`SurvivorDef.requiredExpansion`).

## Reclaimed.Core

| Namespace | Responsibility |
|---|---|
| `Reclaimed.Core` | `ReclaimedCorePlugin` (BepInEx entry), `Log`, `ReclaimedConfig`, `RiskOfOptionsBridge` (reflection, no hard dependency) |
| `Reclaimed.Core.Assets` | `GameAssets.Load<T>(guid)` (cached Addressables), `GameAssets.BodyFromMaster(masterGuid)`, `GameKeys` (GUIDs shared by several survivors) |
| `Reclaimed.Core.Content` | content registration wrappers over `R2API.ContentAddition`; registration happens in plugin `Awake` before the content pack is built |
| `Reclaimed.Core.Survivors` | `SurvivorSpec` + `SurvivorBuilder`: boss body -> player body, display prefab, AI master, `SurvivorDef`, expansion gating; `ModelScaler`; `BodyComponentScrubber` (removes boss-only components) |
| `Reclaimed.Core.Skills` | `SkillSpec` / `SkillFactory` (SkillDef, SteppedSkillDef, families, passive slot, alt skills, unlockable variants) |
| `Reclaimed.Core.States` | `ReclaimedSkillState` base: animation via `AnimationResolver`, `DamageSource` tagging, server/authority-correct attack helpers, attack-speed scaling, aim mode |
| `Reclaimed.Core.Animation` | `AnimationResolver`: plays the first animator state that exists from a list of candidates (`Animator.HasState`), caches per controller, logs what it chose once, config overrides; `reclaimed_dump_animator` command |
| `Reclaimed.Core.Resources` | `ResourceMeter` (networked, manual `OnSerialize`), `ResourceMeterHud` (bar under the health bar for any local body that has a meter) |
| `Reclaimed.Core.Combat` | `AdaptiveArmor` (networked damage-adaptive armor), buffs/DoTs/damage types shared by kits, `DamageSourceUtil` |
| `Reclaimed.Core.Effects` | clone + recolor vanilla effects and projectiles (`EffectFactory`, palettes: Lunar, Void, Digital, Soul, Memory) |
| `Reclaimed.Core.Unlocks` | the purification pipeline (below) |
| `Reclaimed.Core.Bosses` | `BossRecognition`: when a boss meets its reclaimed counterpart in a run, it speaks (chat lines) — the boss itself is unchanged |
| `Reclaimed.Core.Lore` | token helpers over `R2API.LanguageAPI`, logbook entries |
| `Reclaimed.Core.Debug` | console commands (`reclaimed_dump_animator`, `reclaimed_grant_unlock` behind a config flag) |

### Purification unlock pipeline

```
boss dies (server) ──> BossDefeatTracker ──> for each PurificationSpec matching the victim body name
                         │                       evaluate conditions per player (PurificationContext)
                         │                       └─ items held, difficulty, stage, fight damage taken,
                         │                          participation, run flags set by kit-specific trackers
                         └─> qualifying players ──> PurificationServerAchievement.Grant() ──> client achievement
                                                                                             └─> UnlockableDef
```

- A survivor registers a `PurificationSpec { Id, BossBodyNames, Conditions, Description }` with
  `PurificationRegistry`.
- Each spec has a tiny achievement class in the survivor assembly:
  `[RegisterAchievement("Reclaimed<Name>Purified", "Characters.Reclaimed<Name>", null, 5, typeof(<Name>ServerTracker))]`
  deriving from `PurificationAchievement` / `PurificationServerAchievement` in Core. The assembly carries
  `[assembly: HG.Reflection.SearchableAttribute.OptIn]` so the achievement manager finds it.
- `BossFightRecorder` records per-player facts during a boss fight (damage taken, deaths, items picked up
  while the boss lived, whether they used a given equipment) so conditions stay simple predicates.

## Shared survivor conventions

- Body names `Reclaimed<Name>Body`, tokens `RECLAIMED_<NAME>_...`, plugin GUIDs `com.reclaimed.<name>`.
- Every skill tags its damage with the correct `DamageSource` (Primary/Secondary/Utility/Special) so
  Seekers-era items (Luminous Shot, Electric Boomerang, etc.) behave as on vanilla survivors.
- Attacks: `BlastAttack`/`ProjectileManager` on the server path, `OverlapAttack`/`BulletAttack` on the
  authority path, the standard RoR2 split. All custom per-body state lives in components that
  serialize manually (no UNet weaver in mods).
- Proc coefficients, attack-speed scaling, sprint cancellation and interrupt priorities follow vanilla
  survivors so the kits work with items as players expect.
- Config: every number that tunes balance sits in BepInEx config (and Risk of Options when installed).

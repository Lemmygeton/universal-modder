# MODLOG — Reclaimed (Risk of Rain 2)

## 2026-10-11 — setup
- Goal: six bosses as playable survivors, False Son model (boss untouched + purified survivor + boss-earned unlock).
- Route: BepInEx 5 + R2API 5.x plugin (C#, netstandard2.1). No game install in this environment, so no in-game test yet.
- Environment: nuget.org reachable; nuget.bepinex.dev, thunderstore.io, github.com (except raw) blocked.
  - RiskOfRain2.GameLibs on nuget.org is only 1.3.1.275 (has DamageTypeCombo but not DamageSource).
  - tools/RefTools `shim` adds DamageSource to it for compile checks; `apicheck` validates our DLLs against
    MMHOOK_RoR2 2026.10.8 (generated from the live game) — method signatures exact, fields partially.
  - RoR2BepInExPack 1.45.0 (2026-10-10) ships GameAssetPathsBetter: GUIDs for every Addressables asset in the
    live game, DLC3 + DLC4 included. That is our asset source of truth.
- Boss internal names: Mithrix=BrotherBody, Voidling=MiniVoidRaidCrabBodyPhase1-3/VoidRaidCrabBody,
  Solus Wing=SolusWingBody (DLC3), Solus Heart=SolusHeartBody (DLC3), Anima Harvester=ReaperBossBody (DLC4),
  Echo of Providence=ProvidenceMaster (DLC4 root folder; body prefab only reachable through the master).

## 2026-10-11 — Phase 1, ULTRACODE PASS (scaffolding)
- `src/Reclaimed.{Mithrix,Voidling,Echo,Harvester,SolusWing,SolusHeart}`: csproj (ProjectReference to Core,
  `Private=false`), plugin `com.reclaimed.<name>` depending on `com.reclaimed.core` + R2API Prefab/Language,
  `[assembly: HG.Reflection.SearchableAttribute.OptIn]`, Awake only logs ("vertical slice pending"). All in the sln.
- `thunderstore/manifest.json` (Reclaimed 0.1.0; BepInExPack 5.4.2121, RoR2BepInExPack 1.45.0 and the R2API modules
  Core needs, at the Directory.Build.props versions: Core 5.3.0, Prefab 1.1.1, Language 1.1.0, ContentManagement
  1.0.11, Networking 1.0.4, Skins 1.4.10, RecalculateStats 1.6.6, DamageType 1.1.7). Survivors that later use
  Sound/Dot/Orb/Deployable/Director add those packages then. README/CHANGELOG placeholders.
- `tools/package.py`: validates manifest + 256x256 icon (fails naming the missing file), builds Release, zips
  `manifest.json icon.png README.md CHANGELOG.md plugins/Reclaimed/Reclaimed.*.dll` into
  `build/Reclaimed-<version>.zip`; refuses game/loader/R2API/MMHOOK DLLs. Tested: 11 entries, 7 plugin DLLs.

## 2026-10-11 — Phase 1, MAX PASS (Reclaimed.Core)
Public API documented in `docs/CORE_API.md` (the contract for the survivor agents). Decisions:
- **Content**: `ReclaimedContent` wraps `ContentAddition`; UnlockableDefs go through
  `ContentAddition.AddUnlockableDef` (R2API.ContentManagement 1.0.11 has it), so no custom IContentPackProvider.
  R2API attributes everything to the Core content pack (its caller detection stops at the first non-R2API
  frame); harmless, the summary is logged on `RoR2Application.onLoad`.
- **Unlocks**: UnlockableAPI is obsolete since 1.2.3; we use the game's `[RegisterAchievement]` +
  `BaseAchievement`/`BaseServerAchievement`. RoR2BepInExPack's SaferAchievementManager scans every assembly that
  references RoR2 (OptIn not needed but present), derives `ACHIEVEMENT_<ID>_NAME/_DESCRIPTION`, takes the icon from
  `UnlockableDef.achievementIcon` and overwrites the unlockable's how-to strings. Survivors write two empty classes;
  the server half finds its spec from the achievement id. Server pipeline: `BossFightRecorder` (fight facts per
  player) -> `BossDefeatTracker` (per-player evaluation, failed conditions logged by description, optional
  win-the-run gate) -> `PurificationRegistry.OnPurifiedServer` -> achievement `Grant()`.
- **Survivors**: clone with `InstantiateClone(registerNetwork: true)`; scrub by type *name* (no DLC3/4 compile refs);
  always a CharacterMotor (+KCC capsule) for players; the boss's GenericSkills and extra state machines are dropped
  (their vanilla families/states are shared with the boss and never edited); boss death state replaced by
  GenericCharacterDeath (vanilla boss death states drive encounters). Display = model clone with gameplay
  components stripped (whitelist), AI master = clone of CommandoMonsterMaster with spec drivers.
- **Flight**: MMHOOK confirms `CharacterFlightParameters.CheckShouldUseFlight` and
  `CharacterGravityParameters.CheckShouldUseGravity` in the live game; `ReclaimedFlightController` adds a flight and an
  anti-gravity granter through the motor's provider interfaces (isolated in a NoInlining method so a changed API is
  caught), and `ReclaimedFlyingMain` (overrides the live `HandleMovements`/`ProcessJump`) steers in 3D. Fallback when
  the API is missing: the main state drives `CharacterMotor.velocity` directly (documented, untested in game).
- **Skins**: the live game uses `SkinDefParams` (MMHOOK `On.RoR2.SkinDefParams`), absent from the 1.3.1 refs. We pass
  R2API's `SkinDefInfo` to `Skins.CreateNewSkinDef`, which builds SkinDef + SkinDefParams inside R2API (built for the
  live game). Body and display share the skin array (R2API's display-skin fixup then skips them).
- **Networking**: no weaver: `ResourceMeter`/`AdaptiveArmor` serialize manually with dirty bits; authority requests
  use an R2API.Networking `INetMessage` registered by Core.
- **Console**: `[ConCommand]` scanning can precede plugin load and a re-scan would throw on duplicates, so commands
  are injected into `Console.concommandCatalog` (found by reflection) on `onLoad` and on first use via the
  `Console.RunCmd` hook (signature confirmed in MMHOOK).
- **Animation**: never `EntityState.PlayAnimation(string,string,string,float)`: the live game only has the 5-arg
  overload (MMHOOK). `AnimationResolver` drives the Animator directly (HasState/CrossFadeInFixedTime/Play + rate param).
- **DamageSource**: built with the live `DamageTypeCombo(DamageTypeCombo, DamageTypeExtended, DamageSource)` ctor
  (MMHOOK; the 2-arg ctor is gone), then `damageType` set; every attack helper tags the activating slot's source.

### Verification
- `dotnet build Reclaimed.sln -c Release`: 0 errors, 0 warnings.
- `tools/RefTools` apicheck extended: methods MMHOOK cannot hook (property accessors, events, interface members) are
  now also verified against method references in R2API/RoR2BepInExPack (built for the live game).
  Result: `113 verified (12 via current-game references), 0 broken, 309 not verifiable from MMHOOK`.
- Not-verifiable entries reviewed by hand:
  - Checked in MMHOOK: `RegisterAchievementAttribute` ctor (identifier, unlockable, prerequisite, uint coins,
    Type serverTracker); `BaseAchievement.OnInstall/OnUninstall/SetServerTracked/Grant`; `BaseServerAchievement`
    (`OnInstall/OnUninstall/Grant`); `HUD.Awake`; `Console.RunCmd(CmdSender, string, List<string>)`;
    `Console.ConCommand` ctor; `GenericCharacterMain.HandleMovements/ProcessJump`; `CharacterFlightParameters`,
    `CharacterGravityParameters`, `SkinDefParams`, `ExpansionRequirementComponent`, `UI.HealthBar`,
    `ViewablesCatalog.Node`, `SkillFamily` all exist.
  - Hardened with reflection (degrade with one log line instead of a MissingFieldException):
    `CharacterBody._defaultCrosshairPrefab`, `Console.concommandCatalog`, `HUD.healthBar` (falls back to the
    non-ally HealthBar under the HUD), SkillDef `attackSpeedBuffsRestockSpeed(_Multiplier)`,
    `autoHandleLuminousShot`, `hideStockCount`; flight provider calls isolated + caught.
  - Accepted (serialized fields every content mod and the R2API survivor templates write; no live-game evidence
    either way in this environment): CharacterBody stats/tokens/flags/pod/preferredInitialStateType, SkillDef core
    fields, SkillLocator slots + passive, EntityStateMachine, NetworkStateMachine, CharacterDeathBehavior,
    SetStateOnHurt, AISkillDriver, BlastAttack/BulletAttack/OverlapAttack/FireProjectileInfo, HurtBox(Group),
    CameraTargetParams/CharacterCameraParamsData, ModelLocator, SurvivorDef, BuffDef, DamageReport,
    `DamageTypeCombo.damageSource` (the shimmed field; the live ctor takes a DamageSource).
  - Property accessors/events not covered by references (e.g. `CharacterBody.onBodyStartGlobal`,
    `GlobalEventManager.onServerDamageDealt/onCharacterDeathGlobal`, `Run.onServerGameOver`,
    `Inventory.onServerItemGiven`, `EquipmentSlot.onServerEquipmentActivated`, `HUD.targetBodyObject`): long-standing
    public API, unverifiable here.

### Open risks (need an in-game test)
- No game install here: nothing has run. First in-game check: survivor appears in character select, spawns from a
  pod, moves, takes damage, dies; `reclaimed_dump_animator`; HUD bar; `reclaimed_grant_unlock`.
- Boss rigs: state names for player-scale animation are unknown until `reclaimed_dump_animator` runs per boss;
  `AnimationResolver` overrides exist for that.
- Bosses without CharacterMotor/capsule (Voidling, Solus Wing, Solus Heart, likely Echo/Harvester): the added KCC
  capsule and generated hurtbox are first guesses; root offsets of the model vs. a new capsule need tuning per boss.
- Flight fallback path and Hover height control are untested; `ICharacterFlightParameterProvider` on CharacterMotor is
  from the 1.3.1 refs.
- Display/body share SkinDefs; if the live ModelSkinController requires `rootObject` to be its own model, the display
  needs its own SkinDef (would show as the boss's default materials in character select).
- Field-level API drift beyond the hardened members cannot be checked without the live Assembly-CSharp; switch to
  `RefsMode=Packages` (GameLibs 1.4.1) or `fetch_refs.py --game` when available and rerun apicheck.

## 2026-10-11 — Review (Phase 1), Reclaimed.Core
Twelve findings, each re-checked against the code and the decompiled R2API / MMHOOK before fixing. All were real;
none rejected. Two (the `[DisallowMultipleComponent]` ones) were the same defect reported twice and share one fix.
- **ResourceMeter prediction (major)**: client drift, stale updates refunding predictions, and silent server refusals
  confirmed. Spends now carry a sequence number; the server echoes the last processed one with the value plus its
  time since the last gain/spend, so clients re-apply unacknowledged spends on every update and run the same
  regen/decay timers. A refused spend is undone and raises `OnSpendRejected` on the authority. `OnFull`/`OnEmpty`
  fire only on server-confirmed values; `OnChanged` stays on the displayed value (UI). Predicted casts still fire before
  the server answers: documented that a hard resource gate needs a server-side check.
- **ResourceMeter sender validation (minor)**: confirmed R2API's `HandleMessageServer` drops `msg.conn`. Replaced the
  `INetMessage` with raw UNet messages (`ResourceMeterNetwork`, ids 21059/21060, handlers on
  `NetworkManagerSystem.onStartServerGlobal/onStartClientGlobal`); the server applies a spend only when the sender is
  the body's `clientAuthorityOwner`. Clients can no longer Gain/Set: `GainAuthority`/`SetAuthority`/`Op` removed,
  gains are server-only. R2API.Networking stays a dependency for kit messages.
- **Multiple meters (minor, reported twice)**: removed `[DisallowMultipleComponent]`; `BuiltSurvivor.AddMeter()` always
  adds a meter (index = order added). `AddBodyComponent<T>` keeps get-or-add for single components (AdaptiveArmor).
- **BossDefeatTracker first copy ends the fight (major)**: confirmed (`End` then `StartOrGet` makes an empty record).
  A defeat now removes the body from `BossFight.BossBodies` and evaluates only when no other hostile defeat body is
  alive; `Evaluate` never creates a fight (no active fight = warning, no grant). Only defeat bodies block: fight-start
  bodies leave or despawn between phases (Mithrix phase 2 has no boss body at all), so waiting on them could block
  the evaluation forever.
- **Allied boss copies (major)**: confirmed no team check anywhere. `BossFightRecorder.IsHostileBoss` (not
  player-controlled, not `TeamIndex.Player`) now gates fight start, `BossFight.IsBoss`, the defeat tracker (plus
  `report.victimTeamIndex`) and `BossRecognition`.
- **Placeholder SkillDef registered in Start (major)**: confirmed in R2API.ContentManagement 1.0.11
  (`CreateContentPacks` snapshots via the cached `GetOrCreateContentPack`; `AddSkillDef` checks only catalog
  availability). The placeholder SkillDef/EntityState are created in Core `Awake`; `Start` only appends the variant.
  `ReclaimedContent` summary corrected.
- **Capsule from inactive renderer bounds (major)**: confirmed (`ModdedPrefabs` is inactive). `ModelScaler.RendererBounds`
  replaced by `TryModelBounds`, built from shared-mesh bounds and transforms in the body root's frame (skinned meshes in
  bind pose), with a fallback and a warning when there are no meshes; the resulting capsule is logged per survivor.
- **Unguarded installs (minor)**: every Core subsystem installs in its own try/catch; the item and equipment
  subscriptions are separate NoInlining methods, so a missing event accessor only loses that fact.
- **ProcessJump(bool) (minor)**: confirmed in MMHOOK. `ReclaimedFlyingMain` clears `jumpInputReceived` before
  `base.HandleMovements()` (isolated NoInlining write; if the field is gone it warns once and falls back to the
  `ProcessJump()` override).
- **Display root networked (minor)**: confirmed (`CreateEmptyPrefab` ignores `registerNetwork`). The display root is a
  plain GameObject under Core's own inactive, DontDestroyOnLoad holder.
- **Display pose lost on async skin apply (minor, unconfirmed in game)**: MMHOOK shows the async skin path and Animator
  activation helpers; fixed defensively: `ReclaimedDisplayAnimator` plays the pose each time the Animator becomes active
  with a controller (LateUpdate edge check) instead of once in OnEnable.
- New members not verifiable from MMHOOK (accepted, long-standing public API): `GenericCharacterMain.jumpInputReceived`
  (guarded), `DamageReport.victimTeamIndex`, `CharacterBody.teamComponent`, `TeamComponent.teamIndex`,
  `QosChannelIndex.defaultReliable` (R2API.Core, built for the live game, uses `QosChannelIndex.chat`).
- Verification: build 0 errors / 0 warnings; apicheck 0 broken.

## 2026-10-11 — switched compile references to the live game (GameLibs 1.5.0-r.1)
- nuget.bepinex.dev became reachable. `fetch_refs.py` now pulls RiskOfRain2.GameLibs 1.5.0-r.1 (live DLC4 build,
  2026-10-09) and BepInEx.BaseLib 5.4.21 from it; the 1.3.1 + shim path is `--offline-fallback` only.
- Why it matters: enum constants are compiled into our DLL. Against 1.3.1 they were stale: InterruptPriority gained
  Taunt(4)/Immobilize(6) (Stun/Frozen/Vehicle/Death shifted), DamageSource is Hazard 0x10 / DOT 0x20 / Equipment 0x40
  (the shim guessed differently), BodyFlags 0x4000 is IgnoreKnockup. Rebuilt everything against 1.5.0-r.1: 0 errors.
- The Mithrix implementation run that had just started against the old refs was stopped and restarted.

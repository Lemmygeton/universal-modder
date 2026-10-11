# Reclaimed.Core API

The public surface survivor plugins code against. Everything here is in `Reclaimed.Core.dll`
(plugin `com.reclaimed.core`). A survivor plugin depends on it with `[BepInDependency(ReclaimedCorePlugin.Guid)]`,
references the project with `<Private>false</Private>`, and talks to no other survivor.

Contents: [Survivor plugin skeleton](#survivor-plugin-skeleton) ·
[Core, Log, config](#reclaimedcore) · [Assets](#reclaimedcoreassets) · [Content](#reclaimedcorecontent) ·
[Survivors](#reclaimedcoresurvivors) · [Skills](#reclaimedcoreskills) · [States](#reclaimedcorestates) ·
[Animation](#reclaimedcoreanimation) · [Resources](#reclaimedcoreresources) · [Combat](#reclaimedcorecombat) ·
[Effects](#reclaimedcoreeffects) · [Unlocks](#reclaimedcoreunlocks) · [Bosses](#reclaimedcorebosses) ·
[Lore](#reclaimedcorelore) · [Debug](#reclaimedcoredebug) · [Rules of thumb](#rules-of-thumb)

## Survivor plugin skeleton

Everything a survivor registers happens in its plugin `Awake` (content packs are built after all plugins load).

```csharp
using BepInEx;
using R2API;
using Reclaimed.Core;
using Reclaimed.Core.Skills;
using Reclaimed.Core.Survivors;
using Reclaimed.Core.Unlocks;
using RoR2;
using UnityEngine;

[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace Reclaimed.Mithrix
{
    [BepInPlugin("com.reclaimed.mithrix", "Reclaimed Mithrix", "0.1.0")]
    [BepInDependency(ReclaimedCorePlugin.Guid)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    public sealed class MithrixPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            var cfg = new ReclaimedConfig(this);

            // 1. Unlock: spec + unlockable (+ the two tiny achievement classes below).
            PurificationRegistry.Register(new PurificationSpec("ReclaimedMithrix", "Mithrix", "BrotherHurtBody")
                {
                    FightStartBodyNames = new[] { "BrotherBody" },
                    AchievementName = "Mithrix: Reclaimed",
                }
                .AddCondition("carry no lunar items", ctx => ctx.LunarItemCount() == 0));
            UnlockableDef unlock = UnlockableFactory.CreateSurvivorUnlock("ReclaimedMithrix", "RECLAIMED_MITHRIX_NAME", null);

            // 2. Body, display, AI master, SurvivorDef.
            BuiltSurvivor s = SurvivorBuilder.Build(new SurvivorSpec
            {
                Name = "Mithrix", BodyName = "ReclaimedMithrixBody", TokenPrefix = "RECLAIMED_MITHRIX",
                SourceBodyGuid = MithrixKeys.BrotherBody,
                DisplayName = "Mithrix", Subtitle = "King of Nothing", Description = "...", Lore = "...",
                OutroFlavor = "..and so he left, crowned in nothing.", FailureFlavor = "..and so he returned to the void.",
                ModelScale = 0.5f, BodyColor = new Color(0.62f, 0.8f, 1f), SortPosition = 120f, Unlockable = unlock,
            });
            if (!s.Ok) return;

            // 3. Skills.
            SkillFamilyBuilder.AddVariant(s.Primary, SkillFactory.Create(SkillSpec.Primary("ReclaimedMithrixSwing", typeof(HammerSwing))));
            SkillFamilyBuilder.SetPassive(s.SkillLocator, new PassiveSpec { NameToken = "RECLAIMED_MITHRIX_PASSIVE_NAME", NameText = "..." });
        }
    }

    [RegisterAchievement("ReclaimedMithrixPurified", "Characters.ReclaimedMithrix", null, 5, typeof(MithrixPurifiedServer))]
    public class MithrixPurified : PurificationAchievement { }
    public class MithrixPurifiedServer : PurificationServerAchievement { }
}
```

Conventions: body `Reclaimed<Name>Body`, tokens `RECLAIMED_<NAME>_...`, GUID `com.reclaimed.<name>`, achievement
`Reclaimed<Name>Purified`, unlockable `Characters.Reclaimed<Name>`.

---

## Reclaimed.Core

### `ReclaimedCorePlugin : BaseUnityPlugin`
```csharp
public const string Guid = "com.reclaimed.core";
public const string PluginName = "Reclaimed Core";
public const string Version = "0.1.0";
public static ReclaimedCorePlugin Instance { get; }
public static ReclaimedConfig CoreConfig { get; }               // com.reclaimed.core.cfg
public static ConfigEntry<bool> AllowCheatCommands { get; }     // [Debug] AllowCheatCommands, default false
public static ConfigEntry<bool> ShowResourceBars { get; }       // [HUD] ShowResourceBars, default true (restart)
```
`Awake` installs: Risk of Options description, AnimationResolver overrides, the ResourceMeter network handlers,
the HUD bars, AdaptiveArmor stats, BossFightRecorder, BossDefeatTracker, BossRecognition, console commands and the
placeholder skill. Each install is guarded on its own: a subsystem whose game API changed logs an error and is
disabled without stopping the others. `Start` (after every survivor's Awake) appends the already registered
placeholder skill to any empty skill family (and logs an error naming it). On `RoR2Application.onLoad` it resolves
expansion requirements by name and logs the content summary.

### `Log` (static)
```csharp
void Debug(string) / Info(string) / Warn(string) / Error(string)
void Exception(Exception e, string context)
void WarnOnce(string key, string message)    // first call per key only
void InfoOnce(string key, string message)
```
Source name `Reclaimed Core`. Use it from survivors too: `Log.Info("[Mithrix] ...")`.

### `ReclaimedConfig`
```csharp
new ReclaimedConfig(BaseUnityPlugin plugin)
new ReclaimedConfig(ConfigFile file, string modGuid, string modName)
ConfigFile File; string ModGuid; string ModName;
ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description,
                       float min = float.NaN, float max = float.NaN, bool restartRequired = false)
ConfigEntry<T> BindHidden<T>(string section, string key, T defaultValue, string description)   // file only
void SetModDescription(string description)
void SetModIcon(Sprite icon)
```
`Bind` writes the BepInEx entry and adds a Risk of Options widget on the plugin's own page: float/int with
`min`+`max` → clamped slider; float/int without → numeric field; bool → checkbox; `KeyboardShortcut` → keybind;
string → text field.
```csharp
var cfg = new ReclaimedConfig(this);
ConfigEntry<float> dmg = cfg.Bind("Primary", "Damage", 2.8f, "Damage coefficient per swing.", 0.5f, 10f);
ConfigEntry<KeyboardShortcut> key = cfg.Bind("Controls", "ToggleStance", new KeyboardShortcut(KeyCode.G), "Stance key.");
```

### `RiskOfOptionsBridge` (static, pure reflection)
```csharp
const string Guid = "com.rune580.riskofoptions";
bool IsAvailable { get; }
void AddBool(ConfigEntry<bool>, string modGuid, string modName, bool restartRequired = false)
void AddFloatSlider(ConfigEntry<float>, float min, float max, string modGuid, string modName, string format = "{0:0.##}", bool restartRequired = false)
void AddIntSlider(ConfigEntry<int>, int min, int max, string modGuid, string modName, bool restartRequired = false)
void AddFloatField(ConfigEntry<float>, string modGuid, string modName, bool restartRequired = false)
void AddIntField(ConfigEntry<int>, string modGuid, string modName, bool restartRequired = false)
void AddKeyBind(ConfigEntry<KeyboardShortcut>, string modGuid, string modName)
void AddString(ConfigEntry<string>, string modGuid, string modName, bool restartRequired = false)
void SetModDescription(string description, string modGuid, string modName)
void SetModIcon(Sprite icon, string modGuid, string modName)
```
No compile reference to Risk of Options; every call silently no-ops when it is absent, and logs one warning if
it is present but its API differs. Normally used through `ReclaimedConfig`.

---

## Reclaimed.Core.Assets

### `GameAssets` (static)
```csharp
T Load<T>(string guid, string context = null) where T : UnityEngine.Object      // null + clear error on failure
bool TryLoad<T>(string guid, out T asset) where T : UnityEngine.Object           // silent
GameObject BodyFromMaster(string masterGuid, string context = null)              // master.bodyPrefab
T LoadComponent<T>(string prefabGuid, string context = null) where T : Component
```
Synchronous Addressables loads by GUID, cached per (type, GUID); failures are cached too. Call at load time.
GUIDs come from the knowledge base's `asset_keys.tsv` (RoR2BepInExPack 1.45.0, the live game incl. DLC3/DLC4).
```csharp
GameObject brother = GameAssets.Load<GameObject>("…guid…", "Mithrix body");
GameObject echo = GameAssets.BodyFromMaster(EchoKeys.ProvidenceMaster, "Echo body");   // body only reachable via master
```

### `GameKeys` (static, `const string` GUIDs shared by survivors; each verified against asset_keys.tsv)
- Expansions: `ExpansionDLC1`, `ExpansionDLC2`, `ExpansionDLC3`, `ExpansionDLC4`
- Crosshairs: `StandardCrosshair`, `SimpleDotCrosshair`, `StraightBracketCrosshair`, `TiltedBracketCrosshair`, `WideBracketCrosshair`, `SMGCrosshair`
- Pods: `SurvivorPod`, `RoboCratePod`, `VoidSurvivorPod` (DLC1)
- Camera params: `CameraStandard`, `CameraStandardMelee`, `CameraStandardTall`, `CameraStandardGiant`, `CameraStandardHuge`, `CameraStandardRaidboss`
- AI masters: `CommandoMonsterMaster`, `MercMonsterMaster`
- VFX: `OmniImpactVFX`, `OmniImpactVFXMedium`, `OmniImpactVFXLarge`, `OmniImpactVFXSlash`, `OmniImpactVFXSlashMerc`, `OmniImpactVFXLightning`, `OmniExplosionVFX`, `OmniExplosionVFXQuick`, `Hitspark1`, `Muzzleflash1`, `MuzzleflashBarrage`, `TeleportOutBoom`, `HuntressBlinkEffect`, `MercSwordSlash`, `MercSwordFinisherSlash`, `LoaderGroundSlam`, `ImpactLoaderFistSmall`, `TracerCommandoDefault`, `ExplosionLunarSun` (DLC1), `VoidSurvivorMegaBlasterExplosion` (DLC1)
- Icons: `TexMysteryIcon` (Texture2D)

Boss-specific keys belong in the survivor assembly (e.g. `MithrixKeys`).

---

## Reclaimed.Core.Content

### `ReclaimedContent` (static) — logging wrappers over `R2API.ContentAddition`
```csharp
bool AddBody(GameObject bodyPrefab)
bool AddMaster(GameObject masterPrefab)
bool AddSurvivor(SurvivorDef survivorDef)
bool AddSkill(SkillDef skillDef)
bool AddSkillFamily(SkillFamily family)
SerializableEntityStateType AddEntityState(Type stateType)      // idempotent per type
SerializableEntityStateType AddEntityState<T>() where T : EntityState
bool AddEffect(GameObject effectPrefab, bool applyScale = false, string soundName = null)
bool AddProjectile(GameObject projectilePrefab)
bool AddNetworkedObject(GameObject prefab)
bool AddBuff(BuffDef buffDef)
bool AddUnlockable(UnlockableDef unlockableDef)                 // ContentAddition.AddUnlockableDef
bool AddNetworkSoundEvent(NetworkSoundEventDef def)
string Summary()                                                // "Reclaimed content registered: 6 bodies, ..."
```
`AddEffect` adds a missing `EffectComponent` (warns) and `VFXAttributes` (Medium/Medium). Every rejection is
logged with the asset name. Builders below register their own output; call these for your extra assets.
Register everything in plugin `Awake`. R2API snapshots each mod's content when it builds the content packs; an
addition made after that but before the matching catalog initializes returns true, logs nothing and is lost.
Plugin `Start` has no guaranteed order relative to that build.

---

## Reclaimed.Core.Survivors

### `SurvivorSpec`
| Field | Default | Meaning |
|---|---|---|
| `Name` | — (required) | short key, e.g. `"Mithrix"`; logs, config and animation-override keys |
| `BodyName` | — (required) | `"ReclaimedMithrixBody"` |
| `TokenPrefix` | — (required) | `"RECLAIMED_MITHRIX"` |
| `DisplayName, Subtitle, Description, Lore, OutroFlavor, FailureFlavor` | null | English text for the standard tokens |
| `SourceBodyGuid` / `SourceMasterGuid` | null | boss body to clone, or a master whose `bodyPrefab` is cloned |
| `Stats` | `new SurvivorStats()` | see below |
| `BodyFlags` | None | `CharacterBody.BodyFlags`; Flying/Hover add `IgnoreFallDamage` |
| `BodyColor`, `PrimaryColor` | white, null (= BodyColor) | body color; character-select color |
| `SortPosition` | 100 | `SurvivorDef.desiredSortPosition` |
| `PortraitIcon` | null (keep boss's) | |
| `Hull` | Human | `HullClassification` (AI navigation) |
| `ModelScale` | 1 | scales model, hurtboxes, hitboxes, child locators, particles, capsule, root anchors |
| `CapsuleRadius`, `CapsuleHeight` | 0 (derive) | world units after scaling |
| `Motor` | Ground | `MotorKind.Ground / Flying / Hover` |
| `HoverHeight`, `VerticalSpeedCoefficient` | 3, 0.8 | Flying/Hover tuning |
| `Camera` | `new CameraSpec()` | see below |
| `CrosshairGuid` / `CrosshairPrefab` | `GameKeys.StandardCrosshair` / null | |
| `PodGuid` | `GameKeys.SurvivorPod` | null/empty = no pod |
| `MainStateType` | null | null → `GenericCharacterMain` (Ground) or `ReclaimedFlyingMain` |
| `SpawnStateType` | `SpawnTeleporterState` | Body initial state and `preferredInitialStateType` |
| `DeathStateType` | `GenericCharacterDeath` | never the boss's death state (it drives encounters) |
| `ExtraStateMachines` | empty | `List<StateMachineSpec>` |
| `Hurt` | `new HurtSpec()` | SetStateOnHurt flags |
| `RequiredExpansion` | null | `"DLC1".."DLC4"`, `""` = base game, null = inherit the boss's requirement |
| `Unlockable` | null | `UnlockableDef` that locks the survivor |
| `RemoveComponents`, `DisableComponents`, `KeepComponents` | empty | type-name lists for the scrubber |
| `DisplayScale`, `DisplayOffset`, `DisplayRotation` | 1, 0, 0 | character-select model placement |
| `DisplayAnimationLayers`, `DisplayAnimationStates` | Body/FullBody/Base; Spawn/SpawnToIdle/Idle/IdleIn | pose candidates |
| `AIMasterGuid` | `GameKeys.CommandoMonsterMaster` | master cloned for the survivor's AI |
| `AIDrivers` | empty (= generic set) | `List<AIDriverSpec>` |
| `DefaultSkinNameToken`, `DefaultSkinIcon` | `<PREFIX>_DEFAULT_SKIN_NAME` ("Default"), generated | |
| `CustomizeBody`, `CustomizeDisplay` | null | `Action<GameObject>` hooks before registration |
| `CachedName` (get) | | `BodyName` without the `Body` suffix |

`SurvivorStats`: `MaxHealth 160, HealthRegen 2.5, Damage 12, AttackSpeed 1, Crit 1, Armor 20, MoveSpeed 7,
JumpPower 15, JumpCount 1, Acceleration 80, MaxShield 0`; per level `LevelMaxHealth / LevelHealthRegen /
LevelDamage` (NaN = vanilla 30% / 20% / 20% of base), `LevelAttackSpeed, LevelCrit, LevelArmor, LevelMoveSpeed,
LevelJumpPower, LevelMaxShield` (0).

`CameraSpec`: `BaseParamsGuid` (default `GameKeys.CameraStandard`, cloned, never edited),
`Vector3? IdealLocalCameraPos`, `float? PivotVerticalOffset, MinPitch, MaxPitch, WallCushion`,
`float PivotHeight` (NaN = from capsule; unscaled units, multiplied by `ModelScale`).

`HurtSpec`: `CanBeHitStunned false, CanBeStunned true, CanBeFrozen true, HitThreshold 0.15, HurtStateType = HurtState`.

`StateMachineSpec`: `Name, InitialState = Idle, MainState = Idle, IdleOnDeath = true` (also idled on stun).

`AIDriverSpec`: `Name, Slot (SkillSlot.None = movement only), MinDistance, MaxDistance, RequireSkillReady,
SelectionRequiresTargetLoS, ActivationRequiresAimConfirmation, Movement, Aim, Press, MinUserHealthFraction,
MaxUserHealthFraction, ShouldSprint, DriverUpdateTimerOverride`.

### `SurvivorBuilder` (static)
```csharp
BuiltSurvivor Build(SurvivorSpec spec)
IList<BuiltSurvivor> All { get; }
BuiltSurvivor Find(string bodyName)
string KeyForBody(CharacterBody body)        // spec Name of a live reclaimed body, else null
```
`Build` steps (each guarded; a failure logs `[Name] step 'x' failed` and continues where safe; locate model,
scrub, motor, state machines and skill slots are required and abort the build):
1. Clone the source with `PrefabAPI.InstantiateClone(name, registerNetwork: true)` — the boss is never touched.
2. Scrub: `BodyComponentScrubber.DefaultRemove` + spec lists + non-character motors; drop the boss's
   GenericSkills and every state machine except Body/Weapon/extras (vanilla families/states are untouched).
3. CharacterBody: tokens, stats + level growth, flags, colors, crosshair, pod, `isChampion = false`,
   `NetworkIdentity.localPlayerAuthority = true`.
4. `ModelScaler.Scale` with `ModelScale`.
5. Motor: kinematic Rigidbody + CapsuleCollider + KinematicCharacterMotor + CharacterMotor + CharacterDirection;
   capsule from spec or scaled boss capsule or model mesh bounds (logged per survivor so it can be tuned);
   `ReclaimedFlightController` for Flying/Hover.
6. State machines Body (spawn → main), Weapon (Idle), extras; NetworkStateMachine with all of them;
   CharacterDeathBehavior; SetStateOnHurt. Our own state types are registered automatically.
7. SkillLocator with four `GenericSkill`s on new, empty, registered families; passive disabled.
8. InputBankTest, CharacterNetworkTransform, EquipmentSlot, Interactor (+capsule radius), InteractionDriver,
   SfxLocator (land/fall sounds), TeamComponent, HealthComponent, AimAnimator wiring.
9. CharacterModel (`body`, renderer infos if empty, main skinned mesh), HurtBoxGroup (all hurtboxes wired, a main
   bullseye; one is created from the capsule if the boss has none), CameraTargetParams (cloned params + pivot),
   aim origin.
10. Expansion requirement (`ExpansionRequirementComponent`, also copied to the unlockable).
11. Tokens; `CustomizeBody`; display prefab; default skin; AI master; `SurvivorDef` registration.

### `BuiltSurvivor`
```csharp
SurvivorSpec Spec; bool Ok; SurvivorTokens Tokens;
GameObject BodyPrefab, DisplayPrefab, MasterPrefab; SurvivorDef SurvivorDef;
CharacterBody Body; Transform Model; CharacterModel CharacterModel; ChildLocator ChildLocator; Animator Animator;
SkinDef DefaultSkin; SkillLocator SkillLocator; GenericSkill Primary, Secondary, Utility, Special;
EntityStateMachine BodyMachine, WeaponMachine; Dictionary<string, EntityStateMachine> Machines;
EntityStateMachine Machine(string name)
T AddBodyComponent<T>() where T : Component       // returns existing if present
ResourceMeter AddMeter()                           // always a new meter; meters are indexed in the order added
```
```csharp
ResourceMeter rage = s.AddMeter();
ResourceMeter souls = s.AddMeter();               // second bar under the first
var armor = s.AddBodyComponent<AdaptiveArmor>();
```

### `SkinFactory` (static) — R2API.Skins, live SkinDefParams model
```csharp
SkinDef CreateDefault(BuiltSurvivor survivor, string nameToken, Sprite icon)        // called by Build
SkinDef AddRecolor(BuiltSurvivor survivor, string skinName, string nameToken, string nameText, Color tint,
                   Sprite icon = null, UnlockableDef unlockable = null, float strength = 1f)
```
The live game stores skin data in `SkinDefParams`, which the compile references predate. R2API.Skins (built for the
live game) converts our legacy-shaped `SkinDefInfo` into a `SkinDef` + `SkinDefParams`, so Core never names the new
type. Body and display share the SkinDef array (identical hierarchies).
```csharp
SkinFactory.AddRecolor(s, "skinReclaimedMithrixUmbral", "RECLAIMED_MITHRIX_SKIN_UMBRAL_NAME", "Umbral", new Color(0.4f, 0.2f, 0.7f),
    unlockable: masteryUnlock);
```

### `BodyComponentScrubber` (static)
```csharp
static readonly string[] DefaultRemove        // DeathRewards, BaseAI, AISkillDriver, *EncounterController, *BossController,
                                              // *HealthBarOverlayProvider, *AISkillDriverController
static readonly string[] NonCharacterMotors   // RigidbodyMotor, RigidbodyDirection, QuaternionPID, VectorPID, RailMotor
int Remove(GameObject root, IEnumerable<string> names, ICollection<string> keep, string context)
int Disable(GameObject root, IEnumerable<string> names, string context)
bool Matches(Type type, IEnumerable<string> patterns)       // simple/full name or "*Suffix", base types too
int RemoveAll<T>(GameObject root) where T : Component
```
Names, not types, so DLC3/DLC4 components need no compile reference: `spec.RemoveComponents.Add("SolusWingController")`.

### `ModelScaler` (static)
```csharp
void Scale(GameObject body, float scale, string context)
void SetCapsule(GameObject body, float radius, float height, float yOffset)   // collider + KCC
bool TryModelBounds(Transform model, Transform space, out Bounds bounds)   // mesh bounds in space's local frame
```

### Flight: `MotorKind`, `ReclaimedFlightController`, `ReclaimedFlyingMain`
- `ReclaimedFlightController : MonoBehaviour` — `mode, hoverHeight, hoverRiseHeight (6), verticalSpeedCoefficient,
  pitchInfluence (1)`; `bool UsingNativeFlight`; `Vector3 ComputeMoveDirection(InputBankTest)`. On start it adds a
  flight granter and an anti-gravity granter through the motor's `ICharacterFlightParameterProvider` /
  `ICharacterGravityParameterProvider` (CharacterFlightParameters / CharacterGravityParameters exist in the live game
  per MMHOOK). If that API is missing it logs once and the main state drives velocity directly.
- `ReclaimedFlyingMain : GenericCharacterMain` — forward input follows camera pitch, jump climbs (no jumping: it clears
  `jumpInputReceived` before `base.HandleMovements()`, since the live `ProcessJump(bool)` overload cannot be overridden
  against the compile refs; the `ProcessJump()` override stays as a second guard);
  Hover holds `hoverHeight` above the ground (raycast on the world layer), jump rises up to `hoverRiseHeight` more.
  Subclass it for a custom flying main state (`spec.MainStateType`).

### `ExpansionResolver` (static)
```csharp
ExpansionDef Resolve(string name)                     // GUID for DLC1..DLC4, else ExpansionCatalog by name
void Track(ExpansionRequirementComponent component, string name)   // re-resolved by name on RoR2Application.onLoad
ExpansionDef FindInCatalog(string name)
```

### `ReclaimedDisplayAnimator : MonoBehaviour`
On the display model: `layers, states, overrideKey`; plays the first existing pose (override key `<Name>.Display`)
each time the model's Animator becomes active with a controller, because the live ModelSkinController applies skins
asynchronously and toggles the Animator, which restarts it from its entry state. The display root is a plain
local object (no NetworkIdentity, not a networked prefab).

---

## Reclaimed.Core.Skills

### `SkillSpec`
| Field | Default |
|---|---|
| `SkillName` | — unique internal/asset name, e.g. `"ReclaimedMithrixSlam"` |
| `NameToken, DescriptionToken, NameText, DescriptionText` | text fields register the tokens |
| `KeywordTokens` | `new string[0]` |
| `Icon` / `IconColor` / `IconGlyph` | null → generated placeholder / (0.6,0.7,0.9) / Auto |
| `StateType` | — EntityState type (registered automatically) |
| `MachineName` | `"Weapon"` |
| `InterruptPriority` | Any |
| `Cooldown, Stocks, RechargeStock, RequiredStock, StockToConsume` | 0, 1, 1, 1, 1 |
| `IsCombatSkill` | true |
| `CanceledFromSprinting` | false |
| `CancelSprintingOnActivation` | true |
| `ForceSprintDuringState, MustKeyPress, BeginSkillCooldownOnSkillEnd` | false |
| `FullRestockOnAssign` | true |
| `ResetCooldownTimerOnUse, DontAllowPastMaxStocks, HideStockCount` | false |
| `AttackSpeedBuffsRestockSpeed`, `AttackSpeedBuffsRestockSpeedMultiplier` | false, 1 |
| `AutoHandleLuminousShot` | true |

Constructors/presets: `new SkillSpec(string skillName, Type stateType)`, `SkillSpec.Primary(name, type)`,
`SkillSpec.Cooldowned(name, type, cooldown)` (Skill priority, must key press, no Luminous Shot handling).
Fields newer than the compile references (`attackSpeedBuffsRestockSpeed*`, `autoHandleLuminousShot`,
`hideStockCount`) are set by reflection and skipped with one log line if the game lacks them.

### `SkillFactory` (static)
```csharp
SkillDef Create(SkillSpec spec)
SteppedSkillDef CreateStepped(SkillSpec spec, int stepCount, float stepGraceDuration = 0.1f)
T Create<T>(SkillSpec spec) where T : SkillDef          // custom SkillDef subclasses
void Fill(SkillDef def, SkillSpec spec)                   // copy onto an existing instance, no registration
```
```csharp
SkillSpec leap = SkillSpec.Cooldowned("ReclaimedMithrixLeap", typeof(SkyLeap), 6f);
leap.NameToken = "RECLAIMED_MITHRIX_UTILITY_LEAP_NAME";
leap.NameText = "Skyward Leap";
leap.DescriptionToken = "RECLAIMED_MITHRIX_UTILITY_LEAP_DESCRIPTION";
leap.DescriptionText = "Leap into the air and crash down for <style=cIsDamage>600% damage</style>.";
SkillFamilyBuilder.AddVariant(s.Utility, SkillFactory.Create(leap));
```

### `SkillFamilyBuilder` (static)
```csharp
SkillFamily CreateFamily(string name)                               // registered, empty
GenericSkill AddSlot(GameObject bodyPrefab, string familyName, string skillName, bool hideInCharacterSelect = false)
void AddVariant(GenericSkill slot, SkillDef skillDef, UnlockableDef unlockableDef = null)
void AddVariant(SkillFamily family, SkillDef skillDef, UnlockableDef unlockableDef = null)
void SetDefault(SkillFamily family, SkillDef skillDef)
void SetPassive(SkillLocator locator, PassiveSpec spec)
```
The first variant is the default. Families left empty get the placeholder skill (registered in Core `Awake`)
appended at Core `Start`, with an error.

### `PassiveSpec`
`NameToken, DescriptionToken, NameText, DescriptionText, KeywordToken, Icon, IconColor`.

### `IconFactory` (static)
```csharp
enum Glyph { Auto, Circle, Ring, Diamond, Triangle, Cross, Bars, Chevron, Star }
const int Size = 128;
Sprite Create(string key, Color color, Glyph glyph = Glyph.Auto)     // cached, deterministic
```

`PlaceholderSkillState : BaseSkillState` — returns to main immediately (used for unfilled families).

---

## Reclaimed.Core.States

### `ReclaimedSkillState : BaseSkillState` (abstract)
Overridable:
```csharp
protected virtual string SurvivorKey { get; }            // default SurvivorBuilder.KeyForBody(characterBody), e.g. "Mithrix"
protected virtual string SkillKey { get; }               // default GetType().Name
protected virtual DamageSource FallbackDamageSource { get; }   // Primary
protected virtual InterruptPriority MinimumInterruptPriority { get; }   // Skill
```
Helpers:
```csharp
protected DamageSource SkillDamageSource { get; }        // from activatorSkillSlot's slot, else fallback
public void SetDamageSource(DamageSource source)         // chained states inherit the parent's source
protected float ScaledDuration(float baseDuration)       // / attackSpeedStat
protected string OverrideKey { get; }                    // "<SurvivorKey>.<SkillKey>"
protected bool PlayResolved(string[] layers, string[] states, string rateParam, float duration, float crossfade = 0.05f)
protected bool PlayResolved(string layer, string[] states, string rateParam, float duration, float crossfade = 0.05f)
protected DamageTypeCombo TaggedDamage(DamageType damageType = DamageType.Generic)
protected void Tag(ref DamageTypeCombo combo) / Tag(BlastAttack) / Tag(OverlapAttack) / Tag(BulletAttack) / Tag(ref FireProjectileInfo)
protected BlastAttack CreateBlast(Vector3 position, float radius, float damageCoefficient, float procCoefficient = 1f, float force = 0f,
    BlastAttack.FalloffModel falloff = None, GameObject impactEffect = null, DamageType damageType = Generic, bool? crit = null)
protected BlastAttack.Result FireBlastAuthority(BlastAttack blast)
protected BlastAttack.Result FireBlastServer(Vector3 position, float radius, float damageCoefficient, ...same optional args)
protected OverlapAttack CreateOverlap(string hitboxGroupName, float damageCoefficient, float procCoefficient = 1f, GameObject hitEffect = null,
    Vector3 force = default, float pushAwayForce = 0f, DamageType damageType = Generic, bool? crit = null)
protected bool FireOverlapAuthority(OverlapAttack attack, List<HurtBox> hits = null)
protected BulletAttack CreateBullet(Ray aimRay, float damageCoefficient, float procCoefficient = 1f, float force = 0f, float maxDistance = 200f,
    string muzzleName = null, GameObject hitEffect = null, GameObject tracerEffect = null, DamageType damageType = Generic, bool? crit = null)
protected void FireBulletAuthority(BulletAttack bullet)
protected void FireBulletAuthority(Ray aimRay, float damageCoefficient, float procCoefficient = 1f, float force = 0f, float maxDistance = 200f,
    string muzzleName = null, GameObject hitEffect = null, GameObject tracerEffect = null)
protected bool FireProjectileAuthority(GameObject prefab, Ray aimRay, float damageCoefficient, float force = 0f, float speedOverride = -1f,
    GameObject target = null, DamageType damageType = Generic, bool? crit = null)
protected void MuzzleFlash(GameObject effectPrefab, string muzzleName)
protected void SpawnEffect(GameObject effectPrefab, Vector3 position, Quaternion rotation, float scale = 1f, bool transmit = false)
protected void PlaySound(string soundName) / PlayAttackSpeedSound(string soundName)
```
Network split (vanilla): skills fire overlap, bullet, projectile and blast on the **authority** (the game forwards
hits/projectiles to the server). `FireBlastServer` is for server-driven pulses (auras, components). `crit` defaults
to `RollCrit()` per attack object; damage is `damageStat * coefficient`; every attack is tagged with
`SkillDamageSource`.
```csharp
public class Shockwave : ReclaimedSkillState
{
    public override void OnEnter()
    {
        base.OnEnter();
        PlayResolved(new[] { "FullBody, Override" }, new[] { "SmashFullBody", "Smash" }, "Smash.playbackRate", ScaledDuration(0.8f));
        if (isAuthority) FireBlastAuthority(CreateBlast(characterBody.footPosition, 12f, 4f, 1f, 2000f, BlastAttack.FalloffModel.SweetSpot));
    }
}
```

### `ReclaimedMeleeState : ReclaimedSkillState` (abstract)
Override: `HitboxGroupName` (abstract), `BaseDuration 1, DamageCoefficient 1, ProcCoefficient 1, AttackStartFraction 0.3,
AttackEndFraction 0.55, EarlyExitFraction 0.85, HitStopDuration 0.06, PushAwayForce 0, BonusForce 0,
LungeSpeedCoefficient 0, DamageTypeFlags Generic, HitEffectPrefab, SwingEffectPrefab, SwingEffectMuzzle, SwingSound,
HitSound, AnimationLayers, AnimationStates, AnimationRateParam ("Slash.playbackRate")`.
Hooks: `OnSwing()` (all peers), `OnHitAuthority(List<HurtBox>)`, `CreateNextState()` (combo chaining),
`ShouldExitEarly()` (default: key held). Fields: `duration, stopwatch, attack, hasSwung, hasHit`.
Hitstop freezes the animator and motor on the authority. Interrupt priority drops to Any after `EarlyExitFraction`.

### `ReclaimedChargeState : ReclaimedSkillState` (abstract)
Override: `BaseChargeDuration 1, MinimumChargeFraction 0, FireOnFullCharge false, ScaleWithAttackSpeed true,
AnimationLayers, AnimationStates, AnimationRateParam, ChargeSound`; abstract `EntityState CreateFireState(float chargeFraction)`;
hook `OnChargeUpdate(float)`. `ChargeFraction` property. Charge time is serialized for remote peers.

### `ReclaimedDashState : ReclaimedSkillState` (abstract)
Override: `BaseDuration 0.3, SpeedCoefficient 7 (× move speed), EndSpeedFraction 0.3, UseAimDirection false,
AllowVertical false, Intangible false, DashBuff null, DashSound, StartEffectPrefab, AnimationLayers, AnimationStates,
AnimationRateParam`; `virtual Vector3 ChooseDirection()`. Interrupt priority PrioritySkill, fallback source Utility.
Direction is serialized; intangibility uses `HurtBoxGroup.hurtBoxesDeactivatorCounter`.

---

## Reclaimed.Core.Animation

### `AnimationResolver` (static)
```csharp
const string OverrideSection = "AnimationOverrides";
bool TryPlay(Animator animator, string[] layers, string[] states, string rateParam, float duration, float crossfade = 0f, string overrideKey = null)
bool TryResolve(Animator animator, string[] layers, string[] states, out int layer, out int stateHash, string overrideKey = null)
void Play(Animator animator, int layer, int stateHash, string rateParam, float duration, float crossfade)
bool HasParameter(Animator animator, string name)
string Dump(Animator animator)      // layers, parameters (types), clip names
void ClearCache()
```
States are tried in order, each on every candidate layer (`Animator.HasState`). The choice is cached per
controller+request and logged once; a total miss logs the controller's layers and clip names. `rateParam` (if the
animator has it) is scaled so the clip lasts `duration`, like vanilla `PlayAnimation`. With an `overrideKey`
(`"Mithrix.HammerSwing"`) a config entry `[AnimationOverrides] Mithrix.HammerSwing = Layer/State` in
`com.reclaimed.core.cfg` is tried first (empty by default; created on first use).

---

## Reclaimed.Core.Resources

### `ResourceMeter : NetworkBehaviour`
Inspector-style fields: `displayNameToken, color, segments, showOnHud (true), maxValue (100), startValue,
regenPerSecond, regenDelay, decayPerSecond, decayDelay`.
```csharp
enum Op : byte { Gain, Spend, Set, SetMax }
event Action<ResourceMeter, float, float> OnChanged;   // (meter, old, new), every peer
event Action<ResourceMeter> OnFull, OnEmpty;
float Current, Max, Fraction; bool IsFull; CharacterBody Body; int MeterIndex;
// server
float Gain(float amount); bool Spend(float amount); float Drain(float amount); void SetValue(float value); void SetMax(float newMax, bool keepFraction = false);
// authority (also fine on the host)
bool TrySpendAuthority(float amount); void GainAuthority(float amount); void SetAuthority(float value);
```
Server-authoritative; replicated through manual `OnSerialize`/`OnDeserialize` with dirty bits (send interval 0.1 s).
`TrySpendAuthority` on the server (host, AI bodies) spends for real. On a client it predicts the spend, gives it a
sequence number and sends it to the server over a raw UNet message; the server accepts it only from the connection
that owns the body (`NetworkIdentity.clientAuthorityOwner`) and echoes the last sequence it processed with the value.
Clients keep unacknowledged spends applied on top of every server value (stale updates cannot refund them), and
replicate the server's time since the last gain/spend so their regen/decay matches. A spend the server refuses is
undone and raises `OnSpendRejected(meter, amount)` on the authority. Clients cannot gain or set a meter: do gains on
the server (on hit, on kill). `OnChanged` follows the displayed value (predictions included, for UI);
`OnFull`/`OnEmpty` fire only on server-confirmed values. A predicted cast has already fired its attack when a
rejection arrives, so a cast that must never happen unpaid also needs a server-side check.
```csharp
var meter = s.AddMeter();
meter.displayNameToken = "RECLAIMED_MITHRIX_METER"; meter.color = new Color(0.6f, 0.8f, 1f); meter.segments = 4;
meter.maxValue = 100f; meter.decayPerSecond = 5f; meter.decayDelay = 4f;
// in a state:
ResourceMeter m = GetComponent<ResourceMeter>();
if (isAuthority && !m.TrySpendAuthority(25f)) { outer.SetNextStateToMain(); return; }
m.OnSpendRejected += (meter, amount) => { /* authority: e.g. cut a channel short */ };
// server, e.g. on hit:
if (NetworkServer.active) m.Gain(5f);
```

### `ResourceMeterHud : MonoBehaviour`
Installed by Core on `HUD.Awake` (MMHOOK). One bar per `showOnHud` meter of the HUD's target body, under the health
bar (joins its layout group if it has one), with segments and a "NAME current / max" label. Rebuilds on target
change. Pure UnityEngine.UI (label reuses the health bar's TMP font). Disabled by `[HUD] ShowResourceBars`.

`ResourceMeterNetwork` (internal) carries the spend request and the rejection; its handlers are registered on
`NetworkManagerSystem.onStartServerGlobal` / `onStartClientGlobal` (message ids 21059/21060).

---

## Reclaimed.Core.Combat

### `AdaptiveArmor : NetworkBehaviour`
Fields: `armorPerHealthFraction (300), maxArmor (150), minimumHealthFraction (0.01), decayDelay (2), decayPerSecond (40)`.
```csharp
float CurrentArmor { get; }
float Ratio { get; }        // 0..1 of maxArmor
void ResetArmor()           // server
```
Server gains armor from `GlobalEventManager.onServerDamageDealt` (fraction of full combined health lost × rate,
capped); decays after the delay; replicated manually; added to stats via `RecalculateStatsAPI` (`armorAdd`), with
stat refreshes in 2-armor steps.

### `DamageSourceUtil` (static)
```csharp
DamageSource FromSlot(SkillSlot slot)
SkillSlot SlotOf(SkillLocator locator, GenericSkill skill)
DamageSource FromSkill(SkillLocator locator, GenericSkill skill, DamageSource fallback = NoneSpecified)
DamageTypeCombo Combo(DamageType damageType, DamageSource source)
DamageTypeCombo Combo(DamageType damageType, DamageTypeExtended extended, DamageSource source)
void Tag(ref DamageTypeCombo combo, DamageSource source)
```
Uses the live game's `DamageTypeCombo(DamageTypeCombo, DamageTypeExtended, DamageSource)` constructor (verified in MMHOOK).

### `ReclaimedBuffs` (static)
```csharp
BuffDef Create(string name, Sprite icon, Color color, bool canStack = false, bool isDebuff = false, bool isCooldown = false, bool isHidden = false)
void SetStacks(CharacterBody body, BuffDef buff, int stacks)     // server
```

### `ReclaimedDamageTypes` (static) — R2API.DamageType
```csharp
DamageAPI.ModdedDamageType Reserve(string debugName)
void Add(ref DamageTypeCombo combo, DamageAPI.ModdedDamageType type)
bool Has(DamageInfo damageInfo, DamageAPI.ModdedDamageType type)
```

---

## Reclaimed.Core.Effects

### `Palette` enum: `None, Lunar, Void, Digital, Soul, Memory`

### `EffectFactory` (static)
```csharp
Color ColorOf(Palette palette)
GameObject CloneEffect(string guid, string newName, Palette palette = None, bool register = true)
GameObject CloneEffect(GameObject source, string newName, Palette palette = None, bool register = true)
GameObject CloneProjectile(string guid, string newName, Palette palette = None, bool cloneGhost = true, bool register = true)
GameObject CloneGhost(string guid, string newName, Palette palette = None)
GameObject CloneGhost(GameObject source, string newName, Palette palette = None)
void Tint(GameObject root, Color color, float strength = 1f)
Color Shift(Color original, Color target, float strength = 1f)     // hue/saturation toward target, keeps value (HDR) and alpha
```
Clones by GUID under a new name; tinting copies materials (cached per source material and color) and recolors
particle start/over-lifetime colors, trails, lines and lights. The vanilla asset is never modified.
```csharp
GameObject wave = EffectFactory.CloneProjectile(MithrixKeys.BrotherSunderWave, "ReclaimedMithrixSunderWave", Palette.Lunar);
```

---

## Reclaimed.Core.Unlocks

### `PurificationSpec`
```csharp
string Id; string BossDisplayName; string[] BossBodyNames; string[] FightStartBodyNames;
List<PurificationCondition> Conditions; bool RequiresWinningRun; bool RequireParticipation = true;
string AchievementId;       // default Id + "Purified"
string UnlockableName;      // default "Characters." + Id
string AchievementName;     // default "<Boss>: Purified"
string AchievementDescription;   // default generated from conditions
new PurificationSpec(string id, string bossDisplayName, params string[] bossBodyNames)
PurificationSpec AddCondition(string description, Func<PurificationContext, bool> predicate)
string ResolvedAchievementId, ResolvedUnlockableName; string[] ResolvedFightStartBodyNames;
bool IsDefeatBody(string bodyName); bool IsFightBody(string bodyName); string DescribeConditions();
```
`PurificationCondition`: `string Description; Func<PurificationContext, bool> Predicate`.

### `PurificationContext` (one player, at the defeat)
```csharp
PurificationSpec Spec; BossFight Fight; PlayerFightRecord Record (never null); CharacterBody BossBody; DamageReport KillReport;
NetworkUser User; CharacterMaster Master; CharacterBody PlayerBody (null if dead); bool IsAlive; bool IsKiller;
DifficultyIndex Difficulty; DifficultyDef DifficultyDef; string SceneName; float RunTime; float FightDuration; int StageClearCount;
Inventory Inventory { get; }
int ItemCount(string itemName); bool HasItem(string itemName); int ItemCountOfTier(ItemTier tier); int LunarItemCount();
string EquipmentName { get; }; bool HasEquipment(string name); bool UsedEquipment(string name);
bool PlayingBody(string bodyName); bool DifficultyAtLeast(float scalingValue);
bool HasRunFlag(string key); object GetRunFlag(string key);
```

### `PlayerFightRecord`
`Master, User, DamageTaken, HitsTaken, Deaths, DamageDealtToBoss, HitsOnBoss, LowestHealthFraction,
HashSet<EquipmentIndex> EquipmentUsed, Dictionary<ItemIndex,int> ItemsGained, Dictionary<string,float> DamageTakenBySource
(attacker body name → damage), bool DealtDamage`.

### `BossFight`
`Spec, StartRunTime, SceneName, HashSet<CharacterBody> BossBodies, Dictionary<CharacterMaster, PlayerFightRecord> Players,
PlayerFightRecord RecordFor(CharacterMaster), bool IsBoss(CharacterBody), bool AnyDefeatBodyAlive(CharacterBody except)`.

### `BossFightRecorder` (static, server)
`IEnumerable<BossFight> ActiveFights; BossFight GetActiveFight(string specId); bool IsHostileBoss(CharacterBody)`.
A fight starts when a hostile body (`IsHostileBoss`: not player-controlled and not on `TeamIndex.Player`, so Happiest
Mask ghosts never count) named in a spec's fight/defeat names starts; it records player damage taken (total, per
source, lowest health), deaths, damage dealt to the boss, equipment activations and items gained; it ends when the
last defeat body dies, at stage start or at run end. The equipment and item facts are subscribed separately and
degrade to "not recorded" (one warning) if the live game's events changed.

### `BossDefeatTracker` (static, server)
On `onCharacterDeathGlobal` for a hostile defeat body: once no other hostile copy of a defeat body is alive (two
Mithrixes under Artifact of Swarms are one fight), evaluates the spec against the recorded fight for every player
(`RequireParticipation`: dealt damage or alive), logs each failed condition by description, and raises
`PurificationRegistry.OnPurifiedServer` — at once, or on `Run.onServerGameOver` with a winning ending when
`RequiresWinningRun`. A defeat with no recorded fight is logged and never evaluated.

### `PurificationRegistry` (static)
```csharp
event Action<PurificationSpec, NetworkUser> OnPurifiedServer;
IEnumerable<PurificationSpec> Specs;
void Register(PurificationSpec spec)                 // also registers ACHIEVEMENT_<ID>_NAME/_DESCRIPTION tokens
PurificationSpec Get(string id); PurificationSpec Find(string idOrAchievementOrUnlockable); PurificationSpec FindByAchievement(string achievementId);
void SetRunFlag(CharacterMaster master, string key, object value = null)    // server, cleared each run
object GetRunFlag(CharacterMaster master, string key)
```
Kit-specific trackers record facts with run flags; conditions read them:
```csharp
// in the survivor plugin
GlobalEventManager.onServerDamageDealt += r => { if (IsHammerHit(r)) PurificationRegistry.SetRunFlag(r.victimMaster, "MithrixHammerHit"); };
spec.AddCondition("are never struck by his hammer", ctx => !ctx.HasRunFlag("MithrixHammerHit"));
```

### `PurificationAchievement : BaseAchievement` / `PurificationServerAchievement : BaseServerAchievement` (abstract)
Client half requests server tracking; server half grants when `OnPurifiedServer` fires for its own NetworkUser and
the spec matching its achievement id (`protected virtual string PurificationId`). The survivor writes only:
```csharp
[RegisterAchievement("ReclaimedMithrixPurified", "Characters.ReclaimedMithrix", null, 5, typeof(MithrixPurifiedServer))]
public class MithrixPurified : PurificationAchievement { }
public class MithrixPurifiedServer : PurificationServerAchievement { }
```
`RegisterAchievementAttribute(string identifier, string unlockableRewardIdentifier, string prerequisiteAchievementIdentifier,
uint lunarCoinReward, Type serverTrackerType)` is unchanged in the live game (MMHOOK). RoR2BepInExPack's
SaferAchievementManager finds the class in any assembly referencing RoR2, builds `ACHIEVEMENT_<ID>_NAME/_DESCRIPTION`,
takes the icon from the unlockable's `achievementIcon`, and rewrites the unlockable's how-to-unlock strings.

### `UnlockableFactory` (static)
```csharp
UnlockableDef CreateSurvivorUnlock(string id, string nameToken, Sprite icon, string achievementId = null)   // "Characters.<id>"
UnlockableDef Create(string cachedName, string nameToken, Sprite icon, string achievementId, int sortScore = 200)
```
Registers through `ContentAddition.AddUnlockableDef`. A null icon gets a generated placeholder.

---

## Reclaimed.Core.Bosses

### `BossRecognition` (static)
```csharp
static float SpeakDelay = 2.5f;
void Register(string bossBodyName, string survivorBodyName, string speaker, Color color, params string[] lines)
```
Server: when the boss body starts and a player is playing `survivorBodyName`, one random line is broadcast
(`Chat.SimpleChatMessage`, colored, as a language token) after `SpeakDelay`, once per stage per pair. The boss is
otherwise untouched.
```csharp
BossRecognition.Register("BrotherBody", "ReclaimedMithrixBody", "Mithrix", new Color(0.62f, 0.8f, 1f),
    "You wear my shape like stolen glass.", "Even my echo kneels to no one.");
```

---

## Reclaimed.Core.Lore

### `Tokens` (static)
```csharp
void Add(string token, string text)                     // skips null text
void Add(string token, string text, string language)
string Get(string token)                                // English text we registered
SurvivorTokens AddSurvivor(string prefix, string name, string subtitle, string description, string lore, string outro, string failure)
void AddAchievement(string achievementId, string name, string description)   // ACHIEVEMENT_<ID>_NAME/_DESCRIPTION
string AddKeyword(string token, string title, string body)                   // keyword box formatting
```
### `SurvivorTokens`
`Prefix, Name (_NAME), Subtitle (_SUBTITLE), Description (_DESCRIPTION), Lore (_LORE), Outro (_OUTRO_FLAVOR),
Failure (_OUTRO_FAILURE)`; `string Skill(string slot, string key, string part)` → `<PREFIX>_<SLOT>_<KEY>_<PART>`.
The logbook finds a survivor's lore as its name token with `_NAME` replaced by `_LORE`, which this scheme follows.

---

## Reclaimed.Core.Debug

### `ReclaimedCommands` (static)
```csharp
void Register(string name, string help, Action<ConCommandArgs> action, bool cheat = false)
```
Built in: `reclaimed_dump_animator` (your body's animator: layers, parameters, clips), `reclaimed_list_purifications`,
`reclaimed_grant_unlock <id>` (host only; requires `[Debug] AllowCheatCommands = true`; grants through the normal
achievement path). Commands are injected into `RoR2.Console`'s command table on `RoR2Application.onLoad` (and on
first use), not via `[ConCommand]`, because the game's attribute scan may run before plugins load.

---

## Rules of thumb
- Never modify a vanilla prefab, state, static field, family or ScriptableObject. Clone (`InstantiateClone`,
  `EffectFactory`, `Object.Instantiate` for ScriptableObjects) and change the clone.
- DLC3/DLC4 types do not exist at compile time: reach them by Addressables GUID and by component/type **name**
  (`spec.RemoveComponents`, `GetComponent(string)`), never by type.
- No `[SyncVar]`, `[Command]`, `[ClientRpc]`: there is no weaver. Use manual `OnSerialize`/`OnDeserialize` + `SetDirtyBit`
  (see `ResourceMeter`, `AdaptiveArmor`) or an R2API.Networking `INetMessage`. R2API's message handler does not
  pass the sender's connection: a client request that must be checked against the body's owner needs a raw
  handler (see `ResourceMeterNetwork`).
- Tag every hit with the slot's `DamageSource` (use the `ReclaimedSkillState` helpers).
- Put balance numbers in config (`ReclaimedConfig.Bind`), keyed by skill.
- Build, then run apicheck (must report 0 broken) after every change that calls a new game member.

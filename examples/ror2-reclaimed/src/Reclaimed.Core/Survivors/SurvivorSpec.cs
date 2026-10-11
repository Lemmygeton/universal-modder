using System;
using System.Collections.Generic;
using EntityStates;
using RoR2;
using RoR2.CharacterAI;
using UnityEngine;

namespace Reclaimed.Core.Survivors
{
    /// <summary>How the survivor moves. Bosses use many motor types; players always get a CharacterMotor.</summary>
    public enum MotorKind
    {
        /// <summary>Normal walking and jumping.</summary>
        Ground,
        /// <summary>Free 3D flight: no gravity, forward follows the camera pitch, jump ascends.</summary>
        Flying,
        /// <summary>Floats at <see cref="SurvivorSpec.HoverHeight"/> above the ground; jump rises, release settles.</summary>
        Hover,
    }

    /// <summary>Base stats and per-level growth. Defaults are a sturdy vanilla-like melee survivor.</summary>
    public sealed class SurvivorStats
    {
        public float MaxHealth = 160f;
        public float HealthRegen = 2.5f;
        public float Damage = 12f;
        public float AttackSpeed = 1f;
        public float Crit = 1f;
        public float Armor = 20f;
        public float MoveSpeed = 7f;
        public float JumpPower = 15f;
        public int JumpCount = 1;
        public float Acceleration = 80f;
        public float MaxShield;

        /// <summary>Per-level growth; NaN = vanilla rule (health 30%, regen 20%, damage 20% of base; others 0).</summary>
        public float LevelMaxHealth = float.NaN;
        public float LevelHealthRegen = float.NaN;
        public float LevelDamage = float.NaN;
        public float LevelAttackSpeed;
        public float LevelCrit;
        public float LevelArmor;
        public float LevelMoveSpeed;
        public float LevelJumpPower;
        public float LevelMaxShield;
    }

    /// <summary>Camera placement. Defaults derive from the standard survivor camera.</summary>
    public sealed class CameraSpec
    {
        /// <summary>CharacterCameraParams asset to start from (see GameKeys.Camera*).</summary>
        public string BaseParamsGuid = Assets.GameKeys.CameraStandard;
        /// <summary>Camera position relative to the pivot; null keeps the base asset's.</summary>
        public Vector3? IdealLocalCameraPos;
        public float? PivotVerticalOffset;
        public float? MinPitch;
        public float? MaxPitch;
        public float? WallCushion;
        /// <summary>Height of the camera pivot above the body origin, in unscaled body units; NaN derives it from the capsule.</summary>
        public float PivotHeight = float.NaN;
    }

    /// <summary>SetStateOnHurt tuning. Players are normally stunnable and freezable but not hit-stunned.</summary>
    public sealed class HurtSpec
    {
        public bool CanBeHitStunned;
        public bool CanBeStunned = true;
        public bool CanBeFrozen = true;
        public float HitThreshold = 0.15f;
        public Type HurtStateType = typeof(HurtState);
    }

    /// <summary>One AISkillDriver for the survivor's AI master (Umbra/Vengeance doppelgangers, Goobo, etc.).</summary>
    public sealed class AIDriverSpec
    {
        public string Name = "Driver";
        public SkillSlot Slot = SkillSlot.None;
        public float MinDistance;
        public float MaxDistance = float.PositiveInfinity;
        public bool RequireSkillReady = true;
        public bool SelectionRequiresTargetLoS = true;
        public bool ActivationRequiresAimConfirmation;
        public AISkillDriver.MovementType Movement = AISkillDriver.MovementType.ChaseMoveTarget;
        public AISkillDriver.AimType Aim = AISkillDriver.AimType.AtCurrentEnemy;
        public AISkillDriver.ButtonPressType Press = AISkillDriver.ButtonPressType.Hold;
        public float MinUserHealthFraction = float.NegativeInfinity;
        public float MaxUserHealthFraction = float.PositiveInfinity;
        public bool ShouldSprint;
        public float DriverUpdateTimerOverride = -1f;
    }

    /// <summary>An extra EntityStateMachine (e.g. "Weapon2", "Wings") added next to Body and Weapon.</summary>
    public sealed class StateMachineSpec
    {
        public string Name;
        public Type InitialState = typeof(Idle);
        public Type MainState = typeof(Idle);
        /// <summary>Set to Idle on death and stun (like Weapon).</summary>
        public bool IdleOnDeath = true;
    }

    /// <summary>
    /// All knobs for turning a boss body into a survivor. Required: <see cref="Name"/>, <see cref="BodyName"/>,
    /// <see cref="TokenPrefix"/>, and one of <see cref="SourceBodyGuid"/> / <see cref="SourceMasterGuid"/>.
    /// </summary>
    public sealed class SurvivorSpec
    {
        // ---- identity ----
        /// <summary>Short key used in logs and config/override keys, e.g. "Mithrix".</summary>
        public string Name;
        /// <summary>Body prefab name, e.g. "ReclaimedMithrixBody".</summary>
        public string BodyName;
        /// <summary>Token prefix, e.g. "RECLAIMED_MITHRIX" (tokens become RECLAIMED_MITHRIX_NAME, ...).</summary>
        public string TokenPrefix;
        public string DisplayName;
        public string Subtitle;
        public string Description;
        public string Lore;
        public string OutroFlavor;
        public string FailureFlavor;

        // ---- source ----
        /// <summary>Addressables GUID of the boss body prefab to clone.</summary>
        public string SourceBodyGuid;
        /// <summary>Alternative: a master GUID whose bodyPrefab is cloned (bodies only reachable through masters).</summary>
        public string SourceMasterGuid;

        // ---- stats and presentation ----
        public SurvivorStats Stats = new SurvivorStats();
        public CharacterBody.BodyFlags BodyFlags = CharacterBody.BodyFlags.None;
        public Color BodyColor = Color.white;
        /// <summary>Character select / logbook color; defaults to <see cref="BodyColor"/>.</summary>
        public Color? PrimaryColor;
        public float SortPosition = 100f;
        public Texture PortraitIcon;
        public HullClassification Hull = HullClassification.Human;

        // ---- size and movement ----
        /// <summary>Multiplier on the boss model's scale (hurtboxes, hitboxes and child locators follow).</summary>
        public float ModelScale = 1f;
        /// <summary>Capsule radius/height in world units after scaling; 0 = derive from the scaled boss capsule.</summary>
        public float CapsuleRadius;
        public float CapsuleHeight;
        public MotorKind Motor = MotorKind.Ground;
        public float HoverHeight = 3f;
        /// <summary>Vertical speed for Flying/Hover as a multiple of move speed.</summary>
        public float VerticalSpeedCoefficient = 0.8f;
        public CameraSpec Camera = new CameraSpec();

        // ---- gameplay components ----
        public string CrosshairGuid = Assets.GameKeys.StandardCrosshair;
        public GameObject CrosshairPrefab;
        /// <summary>Drop pod (GameKeys.SurvivorPod, RoboCratePod, VoidSurvivorPod) or null for none (spawns standing).</summary>
        public string PodGuid = Assets.GameKeys.SurvivorPod;
        /// <summary>Body main state; null picks GenericCharacterMain or the flight/hover main for <see cref="Motor"/>.</summary>
        public Type MainStateType;
        /// <summary>Initial Body state when not spawning from a pod; default SpawnTeleporterState.</summary>
        public Type SpawnStateType = typeof(SpawnTeleporterState);
        public Type DeathStateType = typeof(GenericCharacterDeath);
        public List<StateMachineSpec> ExtraStateMachines = new List<StateMachineSpec>();
        public HurtSpec Hurt = new HurtSpec();
        /// <summary>"DLC1".."DLC4" (ExpansionDef name), "" for base game, null to inherit the boss's requirement.</summary>
        public string RequiredExpansion;
        public UnlockableDef Unlockable;

        // ---- scrubbing ----
        /// <summary>Component type names (simple or full) removed from the clone, on top of the built-in list.</summary>
        public List<string> RemoveComponents = new List<string>();
        /// <summary>Component type names disabled (kept for references) instead of removed.</summary>
        public List<string> DisableComponents = new List<string>();
        /// <summary>Type names never removed even if they match a built-in rule.</summary>
        public List<string> KeepComponents = new List<string>();

        // ---- display ----
        public float DisplayScale = 1f;
        public Vector3 DisplayOffset = Vector3.zero;
        public Vector3 DisplayRotation = Vector3.zero;
        public string[] DisplayAnimationLayers = { "Body", "FullBody, Override", "Base Layer" };
        /// <summary>States tried for the character-select pose (first existing wins).</summary>
        public string[] DisplayAnimationStates = { "Spawn", "SpawnToIdle", "Idle", "IdleIn" };

        // ---- AI ----
        public string AIMasterGuid = Assets.GameKeys.CommandoMonsterMaster;
        /// <summary>Empty = a generic set (special, secondary, utility at range, primary close, chase).</summary>
        public List<AIDriverSpec> AIDrivers = new List<AIDriverSpec>();

        // ---- skins ----
        public string DefaultSkinNameToken;
        public Sprite DefaultSkinIcon;

        /// <summary>Last chance to adjust the body prefab before registration.</summary>
        public Action<GameObject> CustomizeBody;
        /// <summary>Adjust the display prefab before registration.</summary>
        public Action<GameObject> CustomizeDisplay;

        public string CachedName { get { return BodyName != null && BodyName.EndsWith("Body") ? BodyName.Substring(0, BodyName.Length - 4) : BodyName; } }
    }
}

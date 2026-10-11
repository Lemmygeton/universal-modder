using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EntityStates;
using KinematicCharacterController;
using R2API;
using Reclaimed.Core.Animation;
using Reclaimed.Core.Assets;
using Reclaimed.Core.Content;
using Reclaimed.Core.Lore;
using Reclaimed.Core.Skills;
using RoR2;
using RoR2.CharacterAI;
using RoR2.ExpansionManagement;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Reclaimed.Core.Survivors
{
    /// <summary>
    /// Turns a boss body into a playable survivor, False-Son style: the boss prefab is cloned
    /// (<see cref="PrefabAPI.InstantiateClone(GameObject, string, bool)"/>) and only the clone is changed.
    /// Builds the player body, character-select display, AI master and SurvivorDef, with four empty skill
    /// slots for the survivor to fill. Every step is guarded: a failure is logged with the survivor and step
    /// name and the build continues where it safely can.
    /// </summary>
    /// <example>
    /// <code>
    /// BuiltSurvivor mithrix = SurvivorBuilder.Build(new SurvivorSpec
    /// {
    ///     Name = "Mithrix", BodyName = "ReclaimedMithrixBody", TokenPrefix = "RECLAIMED_MITHRIX",
    ///     SourceBodyGuid = MithrixKeys.BrotherBody, DisplayName = "Mithrix", Subtitle = "King of Nothing",
    ///     ModelScale = 0.55f, BodyColor = new Color(0.62f, 0.8f, 1f), SortPosition = 120f,
    ///     Unlockable = UnlockableFactory.CreateSurvivorUnlock("ReclaimedMithrix", "RECLAIMED_MITHRIX_NAME", null),
    /// });
    /// if (mithrix.Ok) SkillFamilyBuilder.AddVariant(mithrix.Primary, SkillFactory.Create(primarySpec));
    /// </code>
    /// </example>
    public static class SurvivorBuilder
    {
        private static readonly List<BuiltSurvivor> built = new List<BuiltSurvivor>();

        public static IList<BuiltSurvivor> All { get { return built.AsReadOnly(); } }

        public static BuiltSurvivor Find(string bodyName)
        {
            return built.FirstOrDefault(s => s.Spec.BodyName == bodyName);
        }

        private static readonly Dictionary<BodyIndex, string> keysByBody = new Dictionary<BodyIndex, string>();

        /// <summary>The <see cref="SurvivorSpec.Name"/> of a live body built here (e.g. "Mithrix"), or null.</summary>
        public static string KeyForBody(CharacterBody body)
        {
            if (body == null || body.bodyIndex == BodyIndex.None) return null;
            string key;
            if (!keysByBody.TryGetValue(body.bodyIndex, out key))
            {
                BuiltSurvivor s = Find(BodyCatalog.GetBodyName(body.bodyIndex));
                key = s != null ? s.Spec.Name : null;
                keysByBody[body.bodyIndex] = key;
            }
            return key;
        }

        public static BuiltSurvivor Build(SurvivorSpec spec)
        {
            var s = new BuiltSurvivor { Spec = spec };
            if (spec == null || string.IsNullOrEmpty(spec.Name) || string.IsNullOrEmpty(spec.BodyName) || string.IsNullOrEmpty(spec.TokenPrefix))
            {
                Log.Error("SurvivorBuilder: spec needs Name, BodyName and TokenPrefix.");
                return s;
            }
            string ctx = "[" + spec.Name + "]";
            GameObject source = LoadSource(spec, ctx);
            if (source == null) return s;

            s.BodyPrefab = source.InstantiateClone(spec.BodyName, true);
            s.Body = s.BodyPrefab.GetComponent<CharacterBody>();
            if (s.Body == null)
            {
                Log.Error(ctx + " source '" + source.name + "' has no CharacterBody; aborting.");
                return s;
            }
            s.Tokens = new SurvivorTokens(spec.TokenPrefix);

            // Required steps: without them the body cannot be controlled, so stop at the first failure.
            bool ok = Step(ctx, "locate model", () => LocateModel(s), true)
                      && Step(ctx, "scrub boss components", () => Scrub(s), true);
            if (ok)
            {
                Step(ctx, "character body", () => ConfigureBody(s));
                Step(ctx, "scale", () => ModelScaler.Scale(s.BodyPrefab, spec.ModelScale, ctx));
                ok = Step(ctx, "motor", () => ConfigureMotor(s), true)
                     && Step(ctx, "state machines", () => ConfigureStateMachines(s), true)
                     && Step(ctx, "skill slots", () => ConfigureSkills(s), true);
            }
            if (!ok)
            {
                Log.Error(ctx + " a required step failed; the survivor is not registered.");
                return s;
            }
            Step(ctx, "player components", () => ConfigurePlayerComponents(s));
            Step(ctx, "character model", () => ConfigureCharacterModel(s));
            Step(ctx, "hurtboxes", () => ConfigureHurtBoxes(s));
            Step(ctx, "camera", () => ConfigureCamera(s));
            Step(ctx, "expansion", () => ConfigureExpansion(s));
            Step(ctx, "tokens", () => RegisterTokens(s));
            if (spec.CustomizeBody != null) Step(ctx, "CustomizeBody", () => spec.CustomizeBody(s.BodyPrefab));
            Step(ctx, "display prefab", () => BuildDisplay(s));
            Step(ctx, "skins", () =>
            {
                string token = spec.DefaultSkinNameToken ?? spec.TokenPrefix + "_DEFAULT_SKIN_NAME";
                if (spec.DefaultSkinNameToken == null) Tokens.Add(token, "Default");
                s.DefaultSkin = SkinFactory.CreateDefault(s, token, spec.DefaultSkinIcon);
            });
            Step(ctx, "AI master", () => BuildMaster(s));
            Step(ctx, "register", () => Register(s));
            s.Ok = s.SurvivorDef != null;
            built.Add(s);
            Log.Info(ctx + " built " + spec.BodyName + " from " + source.name + (s.Ok ? "" : " (registration failed)") + ".");
            return s;
        }

        private static bool Step(string ctx, string name, Action action, bool required = false)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                Log.Error(ctx + " step '" + name + "' failed" + (required ? "" : " (continuing)") + ": " + e);
                return false;
            }
        }

        private static GameObject LoadSource(SurvivorSpec spec, string ctx)
        {
            if (!string.IsNullOrEmpty(spec.SourceBodyGuid))
            {
                GameObject body = GameAssets.Load<GameObject>(spec.SourceBodyGuid, ctx + " source body");
                if (body != null) return body;
            }
            if (!string.IsNullOrEmpty(spec.SourceMasterGuid)) return GameAssets.BodyFromMaster(spec.SourceMasterGuid, ctx + " source master");
            Log.Error(ctx + " no loadable SourceBodyGuid/SourceMasterGuid; survivor skipped.");
            return null;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static SerializableEntityStateType StateType(Type type)
        {
            if (type == null) return default(SerializableEntityStateType);
            // Vanilla states are already in the catalog; ours must be registered.
            if (type.Assembly != typeof(EntityState).Assembly) return ReclaimedContent.AddEntityState(type);
            return new SerializableEntityStateType(type);
        }

        // ---- steps ----

        private static void LocateModel(BuiltSurvivor s)
        {
            ModelLocator locator = s.BodyPrefab.GetComponent<ModelLocator>();
            Transform model = locator != null ? locator.modelTransform : null;
            if (model == null)
            {
                Animator animator = s.BodyPrefab.GetComponentInChildren<Animator>(true);
                model = animator != null ? animator.transform : null;
                if (model == null) throw new InvalidOperationException("the boss body has no model");
                locator = GetOrAdd<ModelLocator>(s.BodyPrefab);
                locator.modelTransform = model;
            }
            if (locator.modelBaseTransform == null && model.parent != s.BodyPrefab.transform) locator.modelBaseTransform = model.parent;
            locator.autoUpdateModelTransform = true;
            locator.dontDetatchFromParent = false;
            locator.noCorpse = false;
            locator.normalizeToFloor = false;
            s.Model = model;
            s.Animator = model.GetComponent<Animator>();
            s.ChildLocator = model.GetComponent<ChildLocator>();
        }

        private static void Scrub(BuiltSurvivor s)
        {
            SurvivorSpec spec = s.Spec;
            string ctx = "[" + spec.Name + "]";
            var remove = new List<string>(BodyComponentScrubber.DefaultRemove);
            remove.AddRange(spec.RemoveComponents);
            remove.AddRange(BodyComponentScrubber.NonCharacterMotors);
            if (spec.RequiredExpansion == "") remove.Add("ExpansionRequirementComponent");
            int removed = BodyComponentScrubber.Remove(s.BodyPrefab, remove, spec.KeepComponents, ctx);
            int disabled = BodyComponentScrubber.Disable(s.BodyPrefab, spec.DisableComponents, ctx);

            // Boss skills and their (vanilla, shared) families are dropped, never edited.
            int skills = BodyComponentScrubber.RemoveAll<GenericSkill>(s.BodyPrefab);
            var keepMachines = new HashSet<string> { "Body", "Weapon" };
            foreach (StateMachineSpec m in spec.ExtraStateMachines) keepMachines.Add(m.Name);
            int machines = 0;
            foreach (EntityStateMachine esm in s.BodyPrefab.GetComponents<EntityStateMachine>())
            {
                if (keepMachines.Contains(esm.customName)) continue;
                Object.DestroyImmediate(esm);
                machines++;
            }
            Log.Debug(ctx + " scrubbed: " + removed + " components removed, " + disabled + " disabled, " + skills + " boss skills and " +
                      machines + " boss state machines dropped.");
        }

        private static void ConfigureBody(BuiltSurvivor s)
        {
            SurvivorSpec spec = s.Spec;
            SurvivorStats st = spec.Stats;
            CharacterBody b = s.Body;
            b.baseNameToken = s.Tokens.Name;
            b.subtitleNameToken = s.Tokens.Subtitle;
            b.bodyColor = spec.BodyColor;
            b.bodyFlags = spec.BodyFlags | (spec.Motor != MotorKind.Ground ? CharacterBody.BodyFlags.IgnoreFallDamage : 0);
            b.isChampion = false;
            b.hullClassification = spec.Hull;
            if (spec.PortraitIcon != null) b.portraitIcon = spec.PortraitIcon;
            b.hideCrosshair = false;
            b.rootMotionInMainState = false;
            b.sprintingSpeedMultiplier = 1.45f;
            b.baseVisionDistance = float.PositiveInfinity;

            b.baseMaxHealth = st.MaxHealth;
            b.baseRegen = st.HealthRegen;
            b.baseMaxShield = st.MaxShield;
            b.baseDamage = st.Damage;
            b.baseAttackSpeed = st.AttackSpeed;
            b.baseCrit = st.Crit;
            b.baseArmor = st.Armor;
            b.baseMoveSpeed = st.MoveSpeed;
            b.baseJumpPower = st.JumpPower;
            b.baseJumpCount = st.JumpCount;
            b.baseAcceleration = st.Acceleration;
            b.autoCalculateLevelStats = false;
            b.levelMaxHealth = float.IsNaN(st.LevelMaxHealth) ? st.MaxHealth * 0.3f : st.LevelMaxHealth;
            b.levelRegen = float.IsNaN(st.LevelHealthRegen) ? st.HealthRegen * 0.2f : st.LevelHealthRegen;
            b.levelDamage = float.IsNaN(st.LevelDamage) ? st.Damage * 0.2f : st.LevelDamage;
            b.levelAttackSpeed = st.LevelAttackSpeed;
            b.levelCrit = st.LevelCrit;
            b.levelArmor = st.LevelArmor;
            b.levelMoveSpeed = st.LevelMoveSpeed;
            b.levelJumpPower = st.LevelJumpPower;
            b.levelMaxShield = st.LevelMaxShield;

            GameObject crosshair = spec.CrosshairPrefab != null ? spec.CrosshairPrefab : GameAssets.Load<GameObject>(spec.CrosshairGuid, spec.Name + " crosshair");
            if (crosshair != null && !Reflect.TrySetField(b, crosshair, "_defaultCrosshairPrefab", "defaultCrosshairPrefab"))
                Log.Warn("[" + spec.Name + "] CharacterBody has no crosshair field in this game version; keeping the boss default.");
            b.preferredPodPrefab = string.IsNullOrEmpty(spec.PodGuid) ? null : GameAssets.Load<GameObject>(spec.PodGuid, spec.Name + " pod");

            NetworkIdentity identity = s.BodyPrefab.GetComponent<NetworkIdentity>();
            if (identity != null) identity.localPlayerAuthority = true;
        }

        private static void ConfigureMotor(BuiltSurvivor s)
        {
            SurvivorSpec spec = s.Spec;
            GameObject go = s.BodyPrefab;

            Rigidbody rb = GetOrAdd<Rigidbody>(go);
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;

            bool hadCapsule = go.GetComponent<CapsuleCollider>() != null;
            if (!hadCapsule)
            {
                // Rigidbody-motor bosses collide with a sphere/box on the root; the KCC capsule replaces it.
                foreach (Collider other in go.GetComponents<Collider>()) Object.DestroyImmediate(other);
            }
            CapsuleCollider capsule = GetOrAdd<CapsuleCollider>(go);
            KinematicCharacterMotor kcm = GetOrAdd<KinematicCharacterMotor>(go);
            CharacterMotor motor = go.GetComponent<CharacterMotor>();
            if (motor == null)
            {
                motor = go.AddComponent<CharacterMotor>();
                motor.mass = 200f;
            }
            motor.airControl = Mathf.Max(motor.airControl, spec.Motor == MotorKind.Ground ? 0.25f : 1f);
            motor.muteWalkMotion = false;

            float radius = spec.CapsuleRadius > 0f ? spec.CapsuleRadius : capsule.radius;
            float height = spec.CapsuleHeight > 0f ? spec.CapsuleHeight : capsule.height;
            if (!hadCapsule && (spec.CapsuleRadius <= 0f || spec.CapsuleHeight <= 0f))
            {
                // No boss capsule: size it from the (already scaled) model so the player is not a point.
                Bounds bounds;
                if (ModelScaler.TryModelBounds(s.Model, go.transform, out bounds))
                {
                    if (spec.CapsuleRadius <= 0f) radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.6f, 0.5f, 4f);
                    if (spec.CapsuleHeight <= 0f) height = Mathf.Clamp(bounds.size.y * 0.8f, 1.5f, 12f);
                }
                else
                {
                    if (spec.CapsuleRadius <= 0f) radius = 1f;
                    if (spec.CapsuleHeight <= 0f) height = 3f;
                    Log.Warn("[" + spec.Name + "] model has no meshes to measure; set CapsuleRadius/CapsuleHeight in the spec.");
                }
            }
            float yOffset = hadCapsule && spec.CapsuleHeight <= 0f ? capsule.center.y : 0f;
            ModelScaler.SetCapsule(go, radius, height, yOffset);
            kcm.Capsule = capsule;
            Log.Info("[" + spec.Name + "] movement capsule: radius " + capsule.radius.ToString("0.00") + ", height " +
                     capsule.height.ToString("0.00") + ", y offset " + yOffset.ToString("0.00") +
                     (spec.CapsuleRadius > 0f && spec.CapsuleHeight > 0f ? " (from the spec)"
                         : hadCapsule ? " (from the boss)" : " (from the model; tune with CapsuleRadius/CapsuleHeight)") + ".");

            CharacterDirection direction = GetOrAdd<CharacterDirection>(go);
            ModelLocator locator = go.GetComponent<ModelLocator>();
            direction.targetTransform = locator.modelBaseTransform != null ? locator.modelBaseTransform : s.Model;
            direction.modelAnimator = s.Animator;
            if (direction.turnSpeed <= 0f) direction.turnSpeed = 720f;
            motor.characterDirection = direction;

            if (spec.Motor != MotorKind.Ground)
            {
                ReclaimedFlightController flight = GetOrAdd<ReclaimedFlightController>(go);
                flight.mode = spec.Motor;
                flight.hoverHeight = spec.HoverHeight;
                flight.verticalSpeedCoefficient = spec.VerticalSpeedCoefficient;
            }
        }

        private static void ConfigureStateMachines(BuiltSurvivor s)
        {
            SurvivorSpec spec = s.Spec;
            GameObject go = s.BodyPrefab;
            Type mainType = spec.MainStateType ?? (spec.Motor == MotorKind.Ground ? typeof(GenericCharacterMain) : typeof(ReclaimedFlyingMain));
            Type spawnType = spec.SpawnStateType ?? mainType;

            EntityStateMachine body = FindMachine(go, "Body") ?? go.AddComponent<EntityStateMachine>();
            body.customName = "Body";
            body.initialStateType = StateType(spawnType);
            body.mainStateType = StateType(mainType);
            s.Body.preferredInitialStateType = StateType(spawnType);

            EntityStateMachine weapon = FindMachine(go, "Weapon") ?? go.AddComponent<EntityStateMachine>();
            weapon.customName = "Weapon";
            weapon.initialStateType = StateType(typeof(Idle));
            weapon.mainStateType = StateType(typeof(Idle));

            s.BodyMachine = body;
            s.WeaponMachine = weapon;
            s.Machines["Body"] = body;
            s.Machines["Weapon"] = weapon;
            var idleOnDeath = new List<EntityStateMachine> { weapon };
            foreach (StateMachineSpec m in spec.ExtraStateMachines)
            {
                if (string.IsNullOrEmpty(m.Name) || s.Machines.ContainsKey(m.Name)) continue;
                EntityStateMachine esm = FindMachine(go, m.Name) ?? go.AddComponent<EntityStateMachine>();
                esm.customName = m.Name;
                esm.initialStateType = StateType(m.InitialState ?? typeof(Idle));
                esm.mainStateType = StateType(m.MainState ?? typeof(Idle));
                s.Machines[m.Name] = esm;
                if (m.IdleOnDeath) idleOnDeath.Add(esm);
            }

            NetworkStateMachine network = GetOrAdd<NetworkStateMachine>(go);
            network.stateMachines = s.Machines.Values.ToArray();

            CharacterDeathBehavior death = GetOrAdd<CharacterDeathBehavior>(go);
            death.deathStateMachine = body;
            death.deathState = StateType(spec.DeathStateType ?? typeof(GenericCharacterDeath));
            death.idleStateMachine = idleOnDeath.ToArray();

            SetStateOnHurt hurt = GetOrAdd<SetStateOnHurt>(go);
            hurt.targetStateMachine = body;
            hurt.idleStateMachine = idleOnDeath.ToArray();
            hurt.hurtState = StateType(spec.Hurt.HurtStateType ?? typeof(HurtState));
            hurt.canBeHitStunned = spec.Hurt.CanBeHitStunned;
            hurt.canBeStunned = spec.Hurt.CanBeStunned;
            hurt.canBeFrozen = spec.Hurt.CanBeFrozen;
            hurt.hitThreshold = spec.Hurt.HitThreshold;
        }

        private static EntityStateMachine FindMachine(GameObject go, string name)
        {
            return go.GetComponents<EntityStateMachine>().FirstOrDefault(m => m.customName == name);
        }

        private static void ConfigureSkills(BuiltSurvivor s)
        {
            string n = s.Spec.CachedName;
            SkillLocator locator = GetOrAdd<SkillLocator>(s.BodyPrefab);
            s.SkillLocator = locator;
            s.Primary = SkillFamilyBuilder.AddSlot(s.BodyPrefab, n + "PrimaryFamily", "Primary");
            s.Secondary = SkillFamilyBuilder.AddSlot(s.BodyPrefab, n + "SecondaryFamily", "Secondary");
            s.Utility = SkillFamilyBuilder.AddSlot(s.BodyPrefab, n + "UtilityFamily", "Utility");
            s.Special = SkillFamilyBuilder.AddSlot(s.BodyPrefab, n + "SpecialFamily", "Special");
            locator.primary = s.Primary;
            locator.secondary = s.Secondary;
            locator.utility = s.Utility;
            locator.special = s.Special;
            locator.passiveSkill.enabled = false;
            locator.primaryBonusStockOverrideSkill = null;
            locator.secondaryBonusStockOverrideSkill = null;
            locator.utilityBonusStockOverrideSkill = null;
            locator.specialBonusStockOverrideSkill = null;
        }

        private static void ConfigurePlayerComponents(BuiltSurvivor s)
        {
            GameObject go = s.BodyPrefab;
            GetOrAdd<InputBankTest>(go);
            GetOrAdd<CharacterNetworkTransform>(go);
            GetOrAdd<EquipmentSlot>(go);

            CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
            Interactor interactor = GetOrAdd<Interactor>(go);
            interactor.maxInteractionDistance = 3f + (capsule != null ? capsule.radius : 0f);
            InteractionDriver driver = GetOrAdd<InteractionDriver>(go);
            driver.highlightInteractor = true;

            SfxLocator sfx = GetOrAdd<SfxLocator>(go);
            if (string.IsNullOrEmpty(sfx.landingSound)) sfx.landingSound = "Play_char_land";
            if (string.IsNullOrEmpty(sfx.fallDamageSound)) sfx.fallDamageSound = "Play_char_land_fall_damage";

            TeamComponent team = GetOrAdd<TeamComponent>(go);
            team.hideAllyCardDisplay = false;

            HealthComponent health = GetOrAdd<HealthComponent>(go);
            health.dontShowHealthbar = false;
            health.globalDeathEventChanceCoefficient = 1f;

            AimAnimator aim = s.Model.GetComponent<AimAnimator>();
            if (aim != null)
            {
                aim.inputBank = go.GetComponent<InputBankTest>();
                aim.directionComponent = go.GetComponent<CharacterDirection>();
            }
        }

        private static void ConfigureCharacterModel(BuiltSurvivor s)
        {
            CharacterModel model = GetOrAdd<CharacterModel>(s.Model.gameObject);
            s.CharacterModel = model;
            model.body = s.Body;
            model.autoPopulateLightInfos = true;
            if (model.baseRendererInfos == null || model.baseRendererInfos.Length == 0)
            {
                model.baseRendererInfos = s.Model.GetComponentsInChildren<Renderer>(true)
                    .Where(r => !(r is ParticleSystemRenderer) && r.sharedMaterial != null)
                    .Select(r => new CharacterModel.RendererInfo
                    {
                        renderer = r,
                        defaultMaterial = r.sharedMaterial,
                        defaultShadowCastingMode = r.shadowCastingMode,
                        ignoreOverlays = r is TrailRenderer || r is LineRenderer,
                    }).ToArray();
                Log.Debug("[" + s.Spec.Name + "] populated " + model.baseRendererInfos.Length + " renderer infos.");
            }
            if (model.mainSkinnedMeshRenderer == null)
            {
                model.mainSkinnedMeshRenderer = s.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .OrderByDescending(r => r.sharedMesh != null ? r.sharedMesh.vertexCount : 0).FirstOrDefault();
            }
        }

        private static void ConfigureHurtBoxes(BuiltSurvivor s)
        {
            HealthComponent health = s.BodyPrefab.GetComponent<HealthComponent>();
            HurtBoxGroup group = GetOrAdd<HurtBoxGroup>(s.Model.gameObject);
            List<HurtBox> boxes = s.Model.GetComponentsInChildren<HurtBox>(true).ToList();
            if (boxes.Count == 0)
            {
                boxes.Add(CreateMainHurtBox(s));
                Log.Warn("[" + s.Spec.Name + "] boss model had no hurtboxes; created one from the capsule.");
            }
            HurtBox main = group.mainHurtBox != null && boxes.Contains(group.mainHurtBox) ? group.mainHurtBox : boxes[0];
            int bullseyes = 0;
            for (int i = 0; i < boxes.Count; i++)
            {
                HurtBox box = boxes[i];
                box.healthComponent = health;
                box.hurtBoxGroup = group;
                box.indexInGroup = (short)i;
                if (box == main) box.isBullseye = true;
                if (box.isBullseye) bullseyes++;
            }
            group.hurtBoxes = boxes.ToArray();
            group.mainHurtBox = main;
            group.bullseyeCount = bullseyes;
        }

        private static HurtBox CreateMainHurtBox(BuiltSurvivor s)
        {
            CapsuleCollider body = s.BodyPrefab.GetComponent<CapsuleCollider>();
            var go = new GameObject("ReclaimedMainHurtbox");
            go.layer = LayerIndex.entityPrecise.intVal;
            go.transform.SetParent(s.Model, false);
            Vector3 lossy = s.Model.lossyScale;
            float inv = 1f / Mathf.Max(0.0001f, Mathf.Max(lossy.x, Mathf.Max(lossy.y, lossy.z)));
            Vector3 center = s.BodyPrefab.transform.TransformPoint(body != null ? body.center : Vector3.up);
            go.transform.position = center;
            CapsuleCollider c = go.AddComponent<CapsuleCollider>();
            c.isTrigger = false;
            c.radius = (body != null ? body.radius : 1f) * inv;
            c.height = (body != null ? body.height : 2f) * inv;
            HurtBox box = go.AddComponent<HurtBox>();
            box.isBullseye = true;
            return box;
        }

        private static void ConfigureCamera(BuiltSurvivor s)
        {
            CameraSpec cam = s.Spec.Camera ?? new CameraSpec();
            CameraTargetParams target = GetOrAdd<CameraTargetParams>(s.BodyPrefab);
            CharacterCameraParams baseParams = GameAssets.Load<CharacterCameraParams>(cam.BaseParamsGuid, s.Spec.Name + " camera");
            CharacterCameraParams ccp = baseParams != null ? Object.Instantiate(baseParams) : ScriptableObject.CreateInstance<CharacterCameraParams>();
            ccp.name = "ccp" + s.Spec.CachedName;
            if (cam.IdealLocalCameraPos.HasValue) ccp.data.idealLocalCameraPos = new HG.BlendableTypes.BlendableVector3 { value = cam.IdealLocalCameraPos.Value, alpha = 1f };
            if (cam.PivotVerticalOffset.HasValue) ccp.data.pivotVerticalOffset = new HG.BlendableTypes.BlendableFloat { value = cam.PivotVerticalOffset.Value, alpha = 1f };
            if (cam.MinPitch.HasValue) ccp.data.minPitch = new HG.BlendableTypes.BlendableFloat { value = cam.MinPitch.Value, alpha = 1f };
            if (cam.MaxPitch.HasValue) ccp.data.maxPitch = new HG.BlendableTypes.BlendableFloat { value = cam.MaxPitch.Value, alpha = 1f };
            if (cam.WallCushion.HasValue) ccp.data.wallCushion = new HG.BlendableTypes.BlendableFloat { value = cam.WallCushion.Value, alpha = 1f };
            target.cameraParams = ccp;

            CapsuleCollider capsule = s.BodyPrefab.GetComponent<CapsuleCollider>();
            float pivotHeight = !float.IsNaN(cam.PivotHeight) ? cam.PivotHeight * s.Spec.ModelScale
                : (capsule != null ? capsule.center.y + capsule.height * 0.35f : 1.5f);
            Transform root = s.BodyPrefab.transform;
            Transform pivot = target.cameraPivotTransform != null ? target.cameraPivotTransform : root.Find("CameraPivot");
            if (pivot == null)
            {
                pivot = new GameObject("CameraPivot").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(0f, pivotHeight, 0f);
            }
            else if (!float.IsNaN(cam.PivotHeight)) pivot.localPosition = new Vector3(0f, pivotHeight, 0f);
            target.cameraPivotTransform = pivot;

            if (s.Body.aimOriginTransform == null)
            {
                Transform aim = new GameObject("AimOrigin").transform;
                aim.SetParent(root, false);
                aim.localPosition = new Vector3(0f, pivotHeight, 0f);
                s.Body.aimOriginTransform = aim;
            }
        }

        private static void ConfigureExpansion(BuiltSurvivor s)
        {
            string name = s.Spec.RequiredExpansion;
            if (name == null)
            {
                ExpansionRequirementComponent inherited = s.BodyPrefab.GetComponent<ExpansionRequirementComponent>();
                if (inherited != null && inherited.requiredExpansion != null && s.Spec.Unlockable != null)
                    s.Spec.Unlockable.requiredExpansion = inherited.requiredExpansion;
                return;
            }
            if (name == "") return;
            ExpansionRequirementComponent component = GetOrAdd<ExpansionRequirementComponent>(s.BodyPrefab);
            component.requiredExpansion = ExpansionResolver.Resolve(name);
            ExpansionResolver.Track(component, name);
            if (s.Spec.Unlockable != null && component.requiredExpansion != null) s.Spec.Unlockable.requiredExpansion = component.requiredExpansion;
        }

        private static void RegisterTokens(BuiltSurvivor s)
        {
            SurvivorSpec spec = s.Spec;
            Tokens.AddSurvivor(spec.TokenPrefix, spec.DisplayName ?? spec.Name, spec.Subtitle, spec.Description, spec.Lore,
                spec.OutroFlavor, spec.FailureFlavor);
        }

        // ---- display ----

        private static readonly string[] displayKeep =
        {
            "CharacterModel", "ModelSkinController", "ChildLocator", "AnimationEvents", "FootstepHandler",
            "DynamicBone", "DynamicBoneCollider", "PrintController", "ReclaimedDisplayAnimator",
        };

        private static Transform displayHolder;

        // Displays are local-only: not under PrefabAPI (CreateEmptyPrefab always adds a NetworkIdentity and registers
        // a networked prefab), but under our own inactive holder so they never run as scene objects.
        private static Transform DisplayHolder()
        {
            if (displayHolder == null)
            {
                var holder = new GameObject("ReclaimedDisplayPrefabs");
                Object.DontDestroyOnLoad(holder);
                holder.hideFlags = HideFlags.HideAndDontSave;
                holder.SetActive(false);
                displayHolder = holder.transform;
            }
            return displayHolder;
        }

        private static void BuildDisplay(BuiltSurvivor s)
        {
            SurvivorSpec spec = s.Spec;
            var root = new GameObject(spec.CachedName + "Display");
            root.transform.SetParent(DisplayHolder(), false);
            GameObject model = Object.Instantiate(s.Model.gameObject, root.transform);
            model.name = s.Model.name;
            model.transform.localPosition = spec.DisplayOffset;
            model.transform.localRotation = Quaternion.Euler(spec.DisplayRotation);
            model.transform.localScale = s.Model.localScale * spec.DisplayScale;

            Assembly gameAssembly = typeof(CharacterBody).Assembly;
            int removed = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                bool progress = false;
                foreach (Component c in model.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform) continue;
                    if (!ShouldStripFromDisplay(c, gameAssembly)) continue;
                    Object.DestroyImmediate(c);
                    if (c == null)
                    {
                        removed++;
                        progress = true;
                    }
                }
                if (!progress) break;
            }

            CharacterModel characterModel = model.GetComponent<CharacterModel>();
            if (characterModel != null) characterModel.body = null;
            ReclaimedDisplayAnimator anim = model.AddComponent<ReclaimedDisplayAnimator>();
            anim.layers = spec.DisplayAnimationLayers;
            anim.states = spec.DisplayAnimationStates;
            anim.overrideKey = spec.Name + ".Display";

            if (spec.CustomizeDisplay != null) spec.CustomizeDisplay(root);
            s.DisplayPrefab = root;
            Log.Debug("[" + spec.Name + "] display built (" + removed + " gameplay components stripped).");
        }

        private static bool ShouldStripFromDisplay(Component c, Assembly gameAssembly)
        {
            if (c is Collider || c is Rigidbody || c is Joint || c is NetworkIdentity || c is NetworkBehaviour) return true;
            Type type = c.GetType();
            if (BodyComponentScrubber.Matches(type, displayKeep)) return false;
            // Game gameplay components (hurtboxes, hitboxes, aim animators, controllers) have no place in a lobby display.
            return type.Assembly == gameAssembly;
        }

        // ---- AI master ----

        private static void BuildMaster(BuiltSurvivor s)
        {
            SurvivorSpec spec = s.Spec;
            GameObject source = GameAssets.Load<GameObject>(spec.AIMasterGuid, spec.Name + " AI master");
            if (source == null) return;
            GameObject master = source.InstantiateClone(spec.CachedName + "MonsterMaster", true);
            CharacterMaster cm = master.GetComponent<CharacterMaster>();
            if (cm == null) throw new InvalidOperationException("AI master source has no CharacterMaster");
            cm.bodyPrefab = s.BodyPrefab;
            BodyComponentScrubber.RemoveAll<AISkillDriver>(master);
            List<AIDriverSpec> drivers = spec.AIDrivers != null && spec.AIDrivers.Count > 0 ? spec.AIDrivers : DefaultDrivers();
            foreach (AIDriverSpec d in drivers) AddDriver(master, d);
            s.MasterPrefab = master;
        }

        private static List<AIDriverSpec> DefaultDrivers()
        {
            return new List<AIDriverSpec>
            {
                new AIDriverSpec { Name = "Special", Slot = SkillSlot.Special, MaxDistance = 30f },
                new AIDriverSpec { Name = "Secondary", Slot = SkillSlot.Secondary, MaxDistance = 40f },
                new AIDriverSpec { Name = "Utility", Slot = SkillSlot.Utility, MinDistance = 15f, MaxDistance = 50f, ShouldSprint = true },
                new AIDriverSpec { Name = "Primary", Slot = SkillSlot.Primary, MaxDistance = 25f, Movement = AISkillDriver.MovementType.StrafeMovetarget },
                new AIDriverSpec { Name = "Chase", Slot = SkillSlot.None, RequireSkillReady = false, SelectionRequiresTargetLoS = false, ShouldSprint = true },
            };
        }

        private static void AddDriver(GameObject master, AIDriverSpec d)
        {
            AISkillDriver driver = master.AddComponent<AISkillDriver>();
            driver.customName = d.Name;
            driver.skillSlot = d.Slot;
            driver.requireSkillReady = d.Slot != SkillSlot.None && d.RequireSkillReady;
            driver.minDistance = d.MinDistance;
            driver.maxDistance = d.MaxDistance;
            driver.minUserHealthFraction = d.MinUserHealthFraction;
            driver.maxUserHealthFraction = d.MaxUserHealthFraction;
            driver.minTargetHealthFraction = float.NegativeInfinity;
            driver.maxTargetHealthFraction = float.PositiveInfinity;
            driver.selectionRequiresTargetLoS = d.SelectionRequiresTargetLoS;
            driver.activationRequiresAimConfirmation = d.ActivationRequiresAimConfirmation;
            driver.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            driver.movementType = d.Movement;
            driver.aimType = d.Aim;
            driver.buttonPressType = d.Press;
            driver.moveInputScale = 1f;
            driver.shouldSprint = d.ShouldSprint;
            driver.maxTimesSelected = -1;
            driver.driverUpdateTimerOverride = d.DriverUpdateTimerOverride;
        }

        // ---- registration ----

        private static void Register(BuiltSurvivor s)
        {
            SurvivorSpec spec = s.Spec;
            SurvivorDef def = ScriptableObject.CreateInstance<SurvivorDef>();
            def.cachedName = spec.CachedName;
            ((ScriptableObject)def).name = spec.CachedName;
            def.bodyPrefab = s.BodyPrefab;
            def.displayPrefab = s.DisplayPrefab;
            def.displayNameToken = s.Tokens.Name;
            def.descriptionToken = s.Tokens.Description;
            def.outroFlavorToken = s.Tokens.Outro;
            def.mainEndingEscapeFailureFlavorToken = s.Tokens.Failure;
            def.primaryColor = spec.PrimaryColor ?? spec.BodyColor;
            def.desiredSortPosition = spec.SortPosition;
            def.unlockableDef = spec.Unlockable;
            def.hidden = false;

            bool ok = ReclaimedContent.AddBody(s.BodyPrefab);
            if (s.MasterPrefab != null) ReclaimedContent.AddMaster(s.MasterPrefab);
            ok &= ReclaimedContent.AddSurvivor(def);
            if (ok) s.SurvivorDef = def;
        }
    }

    /// <summary>
    /// Plays the character-select pose on the display model (first existing candidate state). The live
    /// ModelSkinController applies skins asynchronously and toggles the Animator around that load, and a
    /// re-enabled Animator restarts from its entry state, so the pose is played whenever the Animator becomes
    /// active with a controller, not once in OnEnable.
    /// </summary>
    public sealed class ReclaimedDisplayAnimator : MonoBehaviour
    {
        public string[] layers;
        public string[] states;
        public string overrideKey;

        private Animator animator;
        private bool wasPlayable;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null || states == null || states.Length == 0) enabled = false;
        }

        private void OnEnable()
        {
            wasPlayable = false;
        }

        private void LateUpdate()
        {
            bool playable = animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
            if (playable && !wasPlayable) AnimationResolver.TryPlay(animator, layers, states, null, 0f, 0f, overrideKey);
            wasPlayable = playable;
        }
    }
}

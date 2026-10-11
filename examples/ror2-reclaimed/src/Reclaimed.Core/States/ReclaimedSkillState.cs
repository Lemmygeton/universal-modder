using System.Collections.Generic;
using EntityStates;
using Reclaimed.Core.Animation;
using Reclaimed.Core.Combat;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace Reclaimed.Core.States
{
    /// <summary>
    /// Base for every Reclaimed skill state. Adds candidate-based animation, DamageSource tagging derived
    /// from the slot that activated the skill, and attack helpers that follow the vanilla split:
    /// overlap/bullet/projectile/blast on the body's authority (the game forwards hits to the server),
    /// <see cref="FireBlastServer"/> for server-driven pulses.
    /// </summary>
    /// <remarks>
    /// Vanilla boss states keep tuning in static fields shared with the boss, so a survivor never reuses or
    /// edits them; it reads the boss's assets (effects, projectiles, sounds) and keeps its numbers here.
    /// </remarks>
    public abstract class ReclaimedSkillState : BaseSkillState
    {
        private bool sourceResolved;
        private DamageSource damageSource;

        /// <summary>
        /// Prefix for animation override keys; defaults to the survivor's <see cref="Survivors.SurvivorSpec.Name"/>
        /// (e.g. "Mithrix"). Null disables config overrides.
        /// </summary>
        protected virtual string SurvivorKey { get { return Survivors.SurvivorBuilder.KeyForBody(characterBody); } }

        /// <summary>Second half of the override key; defaults to the state's class name.</summary>
        protected virtual string SkillKey { get { return GetType().Name; } }

        /// <summary>Used when the state was not entered from a skill slot (AI, chained states, other machines).</summary>
        protected virtual DamageSource FallbackDamageSource { get { return DamageSource.Primary; } }

        protected virtual InterruptPriority MinimumInterruptPriority { get { return InterruptPriority.Skill; } }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return MinimumInterruptPriority;
        }

        /// <summary>The DamageSource for this skill: the activating slot (Primary..Special) or the fallback.</summary>
        protected DamageSource SkillDamageSource
        {
            get
            {
                if (!sourceResolved)
                {
                    sourceResolved = true;
                    damageSource = DamageSourceUtil.FromSkill(skillLocator, activatorSkillSlot, FallbackDamageSource);
                }
                return damageSource;
            }
        }

        /// <summary>Lets a chained state inherit its parent's source (call before attacking).</summary>
        public void SetDamageSource(DamageSource source)
        {
            damageSource = source;
            sourceResolved = true;
        }

        protected float ScaledDuration(float baseDuration)
        {
            return baseDuration / Mathf.Max(0.01f, attackSpeedStat);
        }

        // ---- animation ----

        protected string OverrideKey
        {
            get { return SurvivorKey == null ? null : SurvivorKey + "." + SkillKey; }
        }

        /// <summary>Plays the first existing (layer, state) candidate; see <see cref="AnimationResolver"/>.</summary>
        protected bool PlayResolved(string[] layers, string[] states, string rateParam, float duration, float crossfade = 0.05f)
        {
            return AnimationResolver.TryPlay(GetModelAnimator(), layers, states, rateParam, duration, crossfade, OverrideKey);
        }

        protected bool PlayResolved(string layer, string[] states, string rateParam, float duration, float crossfade = 0.05f)
        {
            return PlayResolved(new[] { layer }, states, rateParam, duration, crossfade);
        }

        // ---- damage tagging ----

        protected DamageTypeCombo TaggedDamage(DamageType damageType = DamageType.Generic)
        {
            return DamageSourceUtil.Combo(damageType, SkillDamageSource);
        }

        protected void Tag(ref DamageTypeCombo combo)
        {
            combo.damageSource = SkillDamageSource;
        }

        protected void Tag(BlastAttack attack)
        {
            if (attack != null) attack.damageType.damageSource = SkillDamageSource;
        }

        protected void Tag(OverlapAttack attack)
        {
            if (attack != null) attack.damageType.damageSource = SkillDamageSource;
        }

        protected void Tag(BulletAttack attack)
        {
            if (attack != null) attack.damageType.damageSource = SkillDamageSource;
        }

        protected void Tag(ref FireProjectileInfo info)
        {
            DamageTypeCombo combo = info.damageTypeOverride.HasValue ? info.damageTypeOverride.Value : DamageTypeCombo.Generic;
            combo.damageSource = SkillDamageSource;
            info.damageTypeOverride = combo;
        }

        // ---- attacks ----

        /// <summary>
        /// A tagged BlastAttack from this body. Fire it on the authority for skills (<see cref="FireBlastAuthority"/>)
        /// or on the server for server-driven effects (<see cref="FireBlastServer"/>).
        /// </summary>
        protected BlastAttack CreateBlast(Vector3 position, float radius, float damageCoefficient, float procCoefficient = 1f,
            float force = 0f, BlastAttack.FalloffModel falloff = BlastAttack.FalloffModel.None, GameObject impactEffect = null,
            DamageType damageType = DamageType.Generic, bool? crit = null)
        {
            var blast = new BlastAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = GetTeam(),
                position = position,
                radius = radius,
                baseDamage = damageStat * damageCoefficient,
                baseForce = force,
                bonusForce = Vector3.zero,
                crit = crit.HasValue ? crit.Value : RollCrit(),
                procCoefficient = procCoefficient,
                falloffModel = falloff,
                damageColorIndex = DamageColorIndex.Default,
                attackerFiltering = AttackerFiltering.NeverHitSelf,
                damageType = TaggedDamage(damageType),
            };
            if (impactEffect != null) blast.impactEffect = EffectCatalog.FindEffectIndexFromPrefab(impactEffect);
            return blast;
        }

        protected BlastAttack.Result FireBlastAuthority(BlastAttack blast)
        {
            if (!isAuthority || blast == null) return default(BlastAttack.Result);
            return blast.Fire();
        }

        /// <summary>Server only (returns an empty result elsewhere).</summary>
        protected BlastAttack.Result FireBlastServer(Vector3 position, float radius, float damageCoefficient, float procCoefficient = 1f,
            float force = 0f, BlastAttack.FalloffModel falloff = BlastAttack.FalloffModel.None, GameObject impactEffect = null,
            DamageType damageType = DamageType.Generic, bool? crit = null)
        {
            if (!NetworkServer.active) return default(BlastAttack.Result);
            return CreateBlast(position, radius, damageCoefficient, procCoefficient, force, falloff, impactEffect, damageType, crit).Fire();
        }

        /// <summary>A tagged OverlapAttack on the model's HitBoxGroup <paramref name="hitboxGroupName"/>.</summary>
        protected OverlapAttack CreateOverlap(string hitboxGroupName, float damageCoefficient, float procCoefficient = 1f,
            GameObject hitEffect = null, Vector3 force = default(Vector3), float pushAwayForce = 0f,
            DamageType damageType = DamageType.Generic, bool? crit = null)
        {
            HitBoxGroup group = FindHitBoxGroup(hitboxGroupName);
            if (group == null)
                Log.WarnOnce("hitbox." + GetType().FullName + "." + hitboxGroupName,
                    GetType().Name + ": no HitBoxGroup named '" + hitboxGroupName + "' on " + (characterBody ? characterBody.name : "?") + "; melee will not hit.");
            return new OverlapAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = GetTeam(),
                damage = damageStat * damageCoefficient,
                procCoefficient = procCoefficient,
                hitEffectPrefab = hitEffect,
                forceVector = force,
                pushAwayForce = pushAwayForce,
                isCrit = crit.HasValue ? crit.Value : RollCrit(),
                hitBoxGroup = group,
                damageColorIndex = DamageColorIndex.Default,
                damageType = TaggedDamage(damageType),
            };
        }

        /// <summary>Fires on the authority; true if anything was hit. <paramref name="hits"/> receives the hurtboxes.</summary>
        protected bool FireOverlapAuthority(OverlapAttack attack, List<HurtBox> hits = null)
        {
            if (!isAuthority || attack == null || attack.hitBoxGroup == null) return false;
            return attack.Fire(hits);
        }

        /// <summary>A tagged BulletAttack along <paramref name="aimRay"/>; adjust the returned object before firing.</summary>
        protected BulletAttack CreateBullet(Ray aimRay, float damageCoefficient, float procCoefficient = 1f, float force = 0f,
            float maxDistance = 200f, string muzzleName = null, GameObject hitEffect = null, GameObject tracerEffect = null,
            DamageType damageType = DamageType.Generic, bool? crit = null)
        {
            return new BulletAttack
            {
                owner = gameObject,
                weapon = gameObject,
                origin = aimRay.origin,
                aimVector = aimRay.direction,
                minSpread = 0f,
                maxSpread = characterBody ? characterBody.spreadBloomAngle : 0f,
                bulletCount = 1u,
                damage = damageStat * damageCoefficient,
                procCoefficient = procCoefficient,
                force = force,
                maxDistance = maxDistance,
                muzzleName = muzzleName,
                hitEffectPrefab = hitEffect,
                tracerEffectPrefab = tracerEffect,
                isCrit = crit.HasValue ? crit.Value : RollCrit(),
                falloffModel = BulletAttack.FalloffModel.DefaultBullet,
                smartCollision = true,
                damageColorIndex = DamageColorIndex.Default,
                damageType = TaggedDamage(damageType),
            };
        }

        protected void FireBulletAuthority(BulletAttack bullet)
        {
            if (isAuthority && bullet != null) bullet.Fire();
        }

        /// <summary>Creates and fires a tagged bullet in one call (authority only).</summary>
        protected void FireBulletAuthority(Ray aimRay, float damageCoefficient, float procCoefficient = 1f, float force = 0f,
            float maxDistance = 200f, string muzzleName = null, GameObject hitEffect = null, GameObject tracerEffect = null)
        {
            if (isAuthority) CreateBullet(aimRay, damageCoefficient, procCoefficient, force, maxDistance, muzzleName, hitEffect, tracerEffect).Fire();
        }

        /// <summary>Fires a projectile from the authority (the game relays it to the server). False if not authority.</summary>
        protected bool FireProjectileAuthority(GameObject prefab, Ray aimRay, float damageCoefficient, float force = 0f,
            float speedOverride = -1f, GameObject target = null, DamageType damageType = DamageType.Generic, bool? crit = null)
        {
            if (!isAuthority || prefab == null) return false;
            var info = new FireProjectileInfo
            {
                projectilePrefab = prefab,
                position = aimRay.origin,
                rotation = Util.QuaternionSafeLookRotation(aimRay.direction),
                owner = gameObject,
                target = target,
                damage = damageStat * damageCoefficient,
                force = force,
                crit = crit.HasValue ? crit.Value : RollCrit(),
                damageColorIndex = DamageColorIndex.Default,
                speedOverride = speedOverride,
                damageTypeOverride = TaggedDamage(damageType),
            };
            ProjectileManager.instance.FireProjectile(info);
            return true;
        }

        // ---- effects ----

        protected void MuzzleFlash(GameObject effectPrefab, string muzzleName)
        {
            if (effectPrefab != null) EffectManager.SimpleMuzzleFlash(effectPrefab, gameObject, muzzleName, false);
        }

        /// <summary>Spawns an effect locally, or for everyone when <paramref name="transmit"/> (server or authority).</summary>
        protected void SpawnEffect(GameObject effectPrefab, Vector3 position, Quaternion rotation, float scale = 1f, bool transmit = false)
        {
            if (effectPrefab == null) return;
            EffectManager.SpawnEffect(effectPrefab, new EffectData { origin = position, rotation = rotation, scale = scale }, transmit);
        }

        protected void PlaySound(string soundName)
        {
            if (!string.IsNullOrEmpty(soundName)) Util.PlaySound(soundName, gameObject);
        }

        protected void PlayAttackSpeedSound(string soundName)
        {
            if (!string.IsNullOrEmpty(soundName)) Util.PlayAttackSpeedSound(soundName, gameObject, attackSpeedStat);
        }
    }
}

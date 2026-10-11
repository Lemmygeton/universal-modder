using System.Collections.Generic;
using EntityStates;
using RoR2;
using UnityEngine;

namespace Reclaimed.Core.States
{
    /// <summary>
    /// Melee swing with an OverlapAttack on a model HitBoxGroup: attack window, hitstop on hit, swing/hit
    /// effects and sounds, optional forward lunge. Override the properties; everything is attack-speed scaled.
    /// </summary>
    /// <example>
    /// <code>
    /// public class HammerSwing : ReclaimedMeleeState
    /// {
    ///     protected override string HitboxGroupName { get { return "Hammer"; } }
    ///     protected override float BaseDuration { get { return 1.1f; } }
    ///     protected override float DamageCoefficient { get { return 2.8f; } }
    ///     protected override string[] AnimationStates { get { return new[] { "SmashForward", "Slam1" }; } }
    /// }
    /// </code>
    /// </example>
    public abstract class ReclaimedMeleeState : ReclaimedSkillState
    {
        protected abstract string HitboxGroupName { get; }
        protected virtual float BaseDuration { get { return 1f; } }
        protected virtual float DamageCoefficient { get { return 1f; } }
        protected virtual float ProcCoefficient { get { return 1f; } }
        protected virtual float AttackStartFraction { get { return 0.3f; } }
        protected virtual float AttackEndFraction { get { return 0.55f; } }
        /// <summary>After this fraction the authority may leave early if the key is held (combo flow).</summary>
        protected virtual float EarlyExitFraction { get { return 0.85f; } }
        protected virtual float HitStopDuration { get { return 0.06f; } }
        protected virtual float PushAwayForce { get { return 0f; } }
        protected virtual Vector3 BonusForce { get { return Vector3.zero; } }
        /// <summary>Forward speed (in move-speed units) during the attack window; 0 for none.</summary>
        protected virtual float LungeSpeedCoefficient { get { return 0f; } }
        protected virtual DamageType DamageTypeFlags { get { return DamageType.Generic; } }

        protected virtual GameObject HitEffectPrefab { get { return null; } }
        protected virtual GameObject SwingEffectPrefab { get { return null; } }
        protected virtual string SwingEffectMuzzle { get { return null; } }
        protected virtual string SwingSound { get { return null; } }
        protected virtual string HitSound { get { return null; } }

        protected virtual string[] AnimationLayers { get { return new[] { "Gesture, Override", "FullBody, Override", "Body" }; } }
        protected virtual string[] AnimationStates { get { return new string[0]; } }
        protected virtual string AnimationRateParam { get { return "Slash.playbackRate"; } }

        protected float duration;
        protected float stopwatch;
        protected OverlapAttack attack;
        protected bool hasSwung;
        protected bool hasHit;
        private float hitStopTimer;
        private Vector3 storedVelocity;
        private Animator animator;
        private readonly List<HurtBox> hitResults = new List<HurtBox>();

        public override void OnEnter()
        {
            base.OnEnter();
            duration = ScaledDuration(BaseDuration);
            animator = GetModelAnimator();
            attack = CreateOverlap(HitboxGroupName, DamageCoefficient, ProcCoefficient, HitEffectPrefab,
                BonusForce, PushAwayForce, DamageTypeFlags);
            StartAimMode(duration + 0.5f, false);
            if (AnimationStates.Length > 0) PlayResolved(AnimationLayers, AnimationStates, AnimationRateParam, duration);
            PlayAttackSpeedSound(SwingSound);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (hitStopTimer > 0f)
            {
                hitStopTimer -= Time.fixedDeltaTime;
                if (isAuthority && characterMotor) characterMotor.velocity = Vector3.zero;
                if (hitStopTimer <= 0f) EndHitStop();
                return;
            }

            stopwatch += Time.fixedDeltaTime;
            bool inWindow = stopwatch >= duration * AttackStartFraction && stopwatch <= duration * AttackEndFraction;
            if (inWindow)
            {
                if (!hasSwung)
                {
                    hasSwung = true;
                    MuzzleFlash(SwingEffectPrefab, SwingEffectMuzzle);
                    OnSwing();
                }
                if (isAuthority)
                {
                    if (LungeSpeedCoefficient > 0f && characterMotor && characterDirection)
                        characterMotor.rootMotion += characterDirection.forward * (LungeSpeedCoefficient * moveSpeedStat * Time.fixedDeltaTime);
                    hitResults.Clear();
                    if (FireOverlapAuthority(attack, hitResults))
                    {
                        if (!hasHit) PlaySound(HitSound);
                        hasHit = true;
                        OnHitAuthority(hitResults);
                        BeginHitStop();
                    }
                }
            }

            if (!isAuthority) return;
            if (stopwatch >= duration || (stopwatch >= duration * EarlyExitFraction && hasSwung && ShouldExitEarly()))
            {
                EntityState next = CreateNextState();
                if (next != null) outer.SetNextState(next);
                else outer.SetNextStateToMain();
            }
        }

        /// <summary>Called once on every peer when the attack window opens.</summary>
        protected virtual void OnSwing()
        {
        }

        /// <summary>Called on the authority each tick the overlap hits something new.</summary>
        protected virtual void OnHitAuthority(List<HurtBox> hits)
        {
        }

        /// <summary>The state to chain into at the end (null returns to main).</summary>
        protected virtual EntityState CreateNextState()
        {
            return null;
        }

        protected virtual bool ShouldExitEarly()
        {
            return IsKeyDownAuthority();
        }

        private void BeginHitStop()
        {
            if (HitStopDuration <= 0f || hitStopTimer > 0f) return;
            hitStopTimer = HitStopDuration;
            if (animator) animator.speed = 0f;
            if (characterMotor)
            {
                storedVelocity = characterMotor.velocity;
                characterMotor.velocity = Vector3.zero;
            }
        }

        private void EndHitStop()
        {
            hitStopTimer = 0f;
            if (animator) animator.speed = 1f;
            if (isAuthority && characterMotor) characterMotor.velocity = storedVelocity;
        }

        public override void OnExit()
        {
            if (hitStopTimer > 0f) EndHitStop();
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return stopwatch >= duration * EarlyExitFraction ? InterruptPriority.Any : MinimumInterruptPriority;
        }
    }
}

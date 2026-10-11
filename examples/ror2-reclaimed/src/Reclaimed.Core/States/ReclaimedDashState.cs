using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Reclaimed.Core.States
{
    /// <summary>
    /// Short directional dash driven through CharacterMotor root motion (authority). The direction comes from
    /// movement input (aim forward when idle) and is serialized so every peer plays the same dash. Optional
    /// intangibility and a buff for its duration.
    /// </summary>
    public abstract class ReclaimedDashState : ReclaimedSkillState
    {
        protected virtual float BaseDuration { get { return 0.3f; } }
        /// <summary>Dash speed as a multiple of the body's move speed.</summary>
        protected virtual float SpeedCoefficient { get { return 7f; } }
        /// <summary>Speed multiplier at the end of the dash (linear ease-out from 1).</summary>
        protected virtual float EndSpeedFraction { get { return 0.3f; } }
        protected virtual bool UseAimDirection { get { return false; } }
        protected virtual bool AllowVertical { get { return false; } }
        protected virtual bool Intangible { get { return false; } }
        protected virtual BuffDef DashBuff { get { return null; } }
        protected virtual string DashSound { get { return null; } }
        protected virtual GameObject StartEffectPrefab { get { return null; } }

        protected virtual string[] AnimationLayers { get { return new[] { "FullBody, Override", "Body" }; } }
        protected virtual string[] AnimationStates { get { return new string[0]; } }
        protected virtual string AnimationRateParam { get { return null; } }

        protected override InterruptPriority MinimumInterruptPriority { get { return InterruptPriority.PrioritySkill; } }
        protected override DamageSource FallbackDamageSource { get { return DamageSource.Utility; } }

        protected Vector3 dashDirection;
        protected float stopwatch;
        private bool intangibleApplied;
        private HurtBoxGroup hurtBoxGroup;

        public override void OnEnter()
        {
            base.OnEnter();
            if (isAuthority) dashDirection = ChooseDirection();
            if (characterDirection && dashDirection != Vector3.zero)
                characterDirection.forward = new Vector3(dashDirection.x, 0f, dashDirection.z);

            if (AnimationStates.Length > 0) PlayResolved(AnimationLayers, AnimationStates, AnimationRateParam, BaseDuration);
            PlaySound(DashSound);
            if (StartEffectPrefab != null)
                SpawnEffect(StartEffectPrefab, characterBody ? characterBody.corePosition : transform.position,
                    Util.QuaternionSafeLookRotation(dashDirection));

            if (Intangible)
            {
                Transform model = GetModelTransform();
                hurtBoxGroup = model ? model.GetComponent<HurtBoxGroup>() : null;
                if (hurtBoxGroup)
                {
                    hurtBoxGroup.hurtBoxesDeactivatorCounter++;
                    intangibleApplied = true;
                }
            }
            if (NetworkServer.active && DashBuff != null && characterBody) characterBody.AddBuff(DashBuff);
        }

        protected virtual Vector3 ChooseDirection()
        {
            Vector3 dir = Vector3.zero;
            if (!UseAimDirection && inputBank) dir = inputBank.moveVector;
            if (dir.sqrMagnitude < 0.01f && inputBank) dir = inputBank.aimDirection;
            if (!AllowVertical) dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            return dir.normalized;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            stopwatch += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(stopwatch / BaseDuration);
            if (isAuthority && characterMotor)
            {
                float speed = moveSpeedStat * SpeedCoefficient * Mathf.Lerp(1f, EndSpeedFraction, t);
                characterMotor.rootMotion += dashDirection * (speed * Time.fixedDeltaTime);
                if (!AllowVertical && characterMotor.velocity.y < 0f) characterMotor.velocity.y = 0f;
            }
            if (isAuthority && stopwatch >= BaseDuration) outer.SetNextStateToMain();
        }

        public override void OnExit()
        {
            if (intangibleApplied && hurtBoxGroup) hurtBoxGroup.hurtBoxesDeactivatorCounter--;
            intangibleApplied = false;
            if (NetworkServer.active && DashBuff != null && characterBody) characterBody.RemoveBuff(DashBuff);
            base.OnExit();
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(dashDirection);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            dashDirection = reader.ReadVector3();
        }
    }
}

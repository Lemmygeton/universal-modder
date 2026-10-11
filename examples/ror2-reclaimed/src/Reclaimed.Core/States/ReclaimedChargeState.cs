using EntityStates;
using UnityEngine;
using UnityEngine.Networking;

namespace Reclaimed.Core.States
{
    /// <summary>
    /// Hold-to-charge: charges while the skill key is held (authority) and hands the 0..1 charge to the state
    /// from <see cref="CreateFireState"/> on release, or at full charge when <see cref="FireOnFullCharge"/>.
    /// The fire state should serialize the charge itself if remote peers need it.
    /// </summary>
    public abstract class ReclaimedChargeState : ReclaimedSkillState
    {
        protected virtual float BaseChargeDuration { get { return 1f; } }
        /// <summary>Releasing before this fraction keeps charging until it is reached.</summary>
        protected virtual float MinimumChargeFraction { get { return 0f; } }
        protected virtual bool FireOnFullCharge { get { return false; } }
        protected virtual bool ScaleWithAttackSpeed { get { return true; } }

        protected virtual string[] AnimationLayers { get { return new[] { "Gesture, Override", "FullBody, Override", "Body" }; } }
        protected virtual string[] AnimationStates { get { return new string[0]; } }
        protected virtual string AnimationRateParam { get { return null; } }
        protected virtual string ChargeSound { get { return null; } }

        protected float chargeDuration;
        protected float stopwatch;

        /// <summary>0..1, attack-speed scaled.</summary>
        protected float ChargeFraction { get { return chargeDuration > 0f ? Mathf.Clamp01(stopwatch / chargeDuration) : 1f; } }

        protected abstract EntityState CreateFireState(float chargeFraction);

        /// <summary>Every tick on every peer while charging (visuals, slowdowns).</summary>
        protected virtual void OnChargeUpdate(float chargeFraction)
        {
        }

        public override void OnEnter()
        {
            base.OnEnter();
            chargeDuration = ScaleWithAttackSpeed ? ScaledDuration(BaseChargeDuration) : BaseChargeDuration;
            if (AnimationStates.Length > 0) PlayResolved(AnimationLayers, AnimationStates, AnimationRateParam, chargeDuration);
            PlayAttackSpeedSound(ChargeSound);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            stopwatch += Time.fixedDeltaTime;
            float fraction = ChargeFraction;
            OnChargeUpdate(fraction);
            if (characterBody) characterBody.SetAimTimer(1f);
            if (!isAuthority) return;
            bool released = !IsKeyDownAuthority() && fraction >= MinimumChargeFraction;
            bool full = FireOnFullCharge && fraction >= 1f;
            if (released || full) outer.SetNextState(CreateFireState(fraction));
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(stopwatch);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            stopwatch = reader.ReadSingle();
        }
    }
}

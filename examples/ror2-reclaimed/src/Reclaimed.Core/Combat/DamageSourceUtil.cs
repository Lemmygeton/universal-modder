using RoR2;

namespace Reclaimed.Core.Combat
{
    /// <summary>
    /// DamageSource tagging. Seekers-era items (Luminous Shot, Electric Boomerang, Chronic Expansion, ...)
    /// read <see cref="DamageTypeCombo.damageSource"/>, so every survivor hit must carry the slot it came from.
    /// </summary>
    public static class DamageSourceUtil
    {
        public static DamageSource FromSlot(SkillSlot slot)
        {
            switch (slot)
            {
                case SkillSlot.Primary: return DamageSource.Primary;
                case SkillSlot.Secondary: return DamageSource.Secondary;
                case SkillSlot.Utility: return DamageSource.Utility;
                case SkillSlot.Special: return DamageSource.Special;
                default: return DamageSource.NoneSpecified;
            }
        }

        /// <summary>The slot a GenericSkill occupies on its body, by reference comparison with the SkillLocator.</summary>
        public static SkillSlot SlotOf(SkillLocator locator, GenericSkill skill)
        {
            if (locator == null || skill == null) return SkillSlot.None;
            if (skill == locator.primary) return SkillSlot.Primary;
            if (skill == locator.secondary) return SkillSlot.Secondary;
            if (skill == locator.utility) return SkillSlot.Utility;
            if (skill == locator.special) return SkillSlot.Special;
            return SkillSlot.None;
        }

        public static DamageSource FromSkill(SkillLocator locator, GenericSkill skill, DamageSource fallback = DamageSource.NoneSpecified)
        {
            DamageSource source = FromSlot(SlotOf(locator, skill));
            return source == DamageSource.NoneSpecified ? fallback : source;
        }

        /// <summary>A combo with the given damage type bits and source.</summary>
        public static DamageTypeCombo Combo(DamageType damageType, DamageSource source)
        {
            DamageTypeCombo combo = new DamageTypeCombo(DamageTypeCombo.Generic, DamageTypeExtended.Generic, source);
            combo.damageType = damageType;
            return combo;
        }

        public static DamageTypeCombo Combo(DamageType damageType, DamageTypeExtended extended, DamageSource source)
        {
            DamageTypeCombo combo = new DamageTypeCombo(DamageTypeCombo.Generic, extended, source);
            combo.damageType = damageType;
            return combo;
        }

        /// <summary>Sets the source on an existing combo (keeps its damage type bits).</summary>
        public static void Tag(ref DamageTypeCombo combo, DamageSource source)
        {
            combo.damageSource = source;
        }
    }
}

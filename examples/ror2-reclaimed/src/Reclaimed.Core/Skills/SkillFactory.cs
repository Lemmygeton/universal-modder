using System;
using EntityStates;
using Reclaimed.Core.Content;
using Reclaimed.Core.Lore;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace Reclaimed.Core.Skills
{
    /// <summary>Builds and registers SkillDefs from <see cref="SkillSpec"/>s.</summary>
    /// <example>
    /// <code>
    /// SkillDef slam = SkillFactory.Create(new SkillSpec("ReclaimedMithrixSlam", typeof(HammerSlam))
    /// {
    ///     NameToken = "RECLAIMED_MITHRIX_PRIMARY_SLAM_NAME", NameText = "Shattering Swing",
    ///     DescriptionToken = "RECLAIMED_MITHRIX_PRIMARY_SLAM_DESCRIPTION", DescriptionText = "Swing for 280% damage.",
    /// });
    /// SteppedSkillDef combo = SkillFactory.CreateStepped(spec, 3, 0.1f);
    /// </code>
    /// </example>
    public static class SkillFactory
    {
        /// <summary>Creates a plain SkillDef.</summary>
        public static SkillDef Create(SkillSpec spec)
        {
            return Create<SkillDef>(spec);
        }

        /// <summary>Creates a SteppedSkillDef (combo strings; the state reads its step from ISkillState/SteppedSkillDef).</summary>
        public static SteppedSkillDef CreateStepped(SkillSpec spec, int stepCount, float stepGraceDuration = 0.1f)
        {
            SteppedSkillDef def = Create<SteppedSkillDef>(spec);
            if (def != null)
            {
                def.stepCount = Mathf.Max(1, stepCount);
                def.stepGraceDuration = stepGraceDuration;
            }
            return def;
        }

        /// <summary>Creates any SkillDef subclass (e.g. a custom tracking skill def) from a spec.</summary>
        public static T Create<T>(SkillSpec spec) where T : SkillDef
        {
            if (spec == null) throw new ArgumentNullException("spec");
            if (spec.StateType == null || !typeof(EntityState).IsAssignableFrom(spec.StateType))
            {
                Log.Error("Skill '" + spec.SkillName + "' has no valid EntityState type; not created.");
                return null;
            }
            T def = ScriptableObject.CreateInstance<T>();
            Fill(def, spec);
            ReclaimedContent.AddSkill(def);
            return def;
        }

        /// <summary>Copies the spec onto an existing SkillDef instance (does not register it).</summary>
        public static void Fill(SkillDef def, SkillSpec spec)
        {
            string name = string.IsNullOrEmpty(spec.SkillName) ? spec.StateType.Name : spec.SkillName;
            ((ScriptableObject)def).name = name;
            def.skillName = name;
            def.skillNameToken = spec.NameToken;
            def.skillDescriptionToken = spec.DescriptionToken;
            Tokens.Add(spec.NameToken, spec.NameText);
            Tokens.Add(spec.DescriptionToken, spec.DescriptionText);
            def.keywordTokens = spec.KeywordTokens ?? new string[0];
            def.icon = spec.Icon != null ? spec.Icon : IconFactory.Create(name, spec.IconColor, spec.IconGlyph);

            def.activationState = ReclaimedContent.AddEntityState(spec.StateType);
            def.activationStateMachineName = string.IsNullOrEmpty(spec.MachineName) ? "Weapon" : spec.MachineName;
            def.interruptPriority = spec.InterruptPriority;

            def.baseRechargeInterval = Mathf.Max(0f, spec.Cooldown);
            def.baseMaxStock = Mathf.Max(1, spec.Stocks);
            def.rechargeStock = spec.RechargeStock;
            def.requiredStock = spec.RequiredStock;
            def.stockToConsume = spec.StockToConsume;

            def.isCombatSkill = spec.IsCombatSkill;
            def.canceledFromSprinting = spec.CanceledFromSprinting;
            def.cancelSprintingOnActivation = spec.CancelSprintingOnActivation;
            def.forceSprintDuringState = spec.ForceSprintDuringState;
            def.mustKeyPress = spec.MustKeyPress;
            def.beginSkillCooldownOnSkillEnd = spec.BeginSkillCooldownOnSkillEnd;
            def.fullRestockOnAssign = spec.FullRestockOnAssign;
            def.resetCooldownTimerOnUse = spec.ResetCooldownTimerOnUse;
            def.dontAllowPastMaxStocks = spec.DontAllowPastMaxStocks;

            // Fields added in later patches are set by reflection so an older/newer game cannot fail to JIT this method.
            SetOptional(def, "attackSpeedBuffsRestockSpeed", spec.AttackSpeedBuffsRestockSpeed);
            SetOptional(def, "attackSpeedBuffsRestockSpeed_Multiplier", spec.AttackSpeedBuffsRestockSpeedMultiplier);
            SetOptional(def, "autoHandleLuminousShot", spec.AutoHandleLuminousShot);
            SetOptional(def, "hideStockCount", spec.HideStockCount);
        }

        private static void SetOptional(SkillDef def, string fieldName, object value)
        {
            if (!Reflect.TrySetField(def, value, fieldName))
                Log.InfoOnce("skilldef." + fieldName, "SkillDef." + fieldName + " does not exist in this game version; skipped.");
        }
    }
}

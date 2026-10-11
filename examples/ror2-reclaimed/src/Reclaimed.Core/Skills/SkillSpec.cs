using System;
using EntityStates;
using UnityEngine;

namespace Reclaimed.Core.Skills
{
    /// <summary>
    /// Everything that defines a SkillDef. Defaults match a typical vanilla survivor primary; set what differs.
    /// Text fields are optional: when set, the tokens are registered with that English text.
    /// </summary>
    public sealed class SkillSpec
    {
        /// <summary>Unique internal name, e.g. "ReclaimedMithrixPrimarySlam". Also the asset name.</summary>
        public string SkillName;
        public string NameToken;
        public string DescriptionToken;
        public string NameText;
        public string DescriptionText;
        public string[] KeywordTokens = new string[0];

        /// <summary>Null generates a placeholder from <see cref="IconColor"/>/<see cref="IconGlyph"/>.</summary>
        public Sprite Icon;
        public Color IconColor = new Color(0.6f, 0.7f, 0.9f);
        public IconFactory.Glyph IconGlyph = IconFactory.Glyph.Auto;

        /// <summary>The EntityState entered on activation (registered automatically).</summary>
        public Type StateType;
        public string MachineName = "Weapon";
        public InterruptPriority InterruptPriority = InterruptPriority.Any;

        public float Cooldown;
        public int Stocks = 1;
        public int RechargeStock = 1;
        public int RequiredStock = 1;
        public int StockToConsume = 1;

        public bool IsCombatSkill = true;
        public bool CanceledFromSprinting;
        public bool CancelSprintingOnActivation = true;
        public bool ForceSprintDuringState;
        public bool MustKeyPress;
        public bool BeginSkillCooldownOnSkillEnd;
        public bool FullRestockOnAssign = true;
        public bool ResetCooldownTimerOnUse;
        public bool DontAllowPastMaxStocks;
        public bool HideStockCount;
        /// <summary>Attack speed shortens the recharge (like Railgunner/CHEF). Ignored if the game lacks the field.</summary>
        public bool AttackSpeedBuffsRestockSpeed;
        public float AttackSpeedBuffsRestockSpeedMultiplier = 1f;
        /// <summary>Lets the game consume Luminous Shot stacks on this skill (primaries and secondaries).</summary>
        public bool AutoHandleLuminousShot = true;

        public SkillSpec()
        {
        }

        public SkillSpec(string skillName, Type stateType)
        {
            SkillName = skillName;
            StateType = stateType;
        }

        /// <summary>Spec for a primary: no cooldown, cancels sprint, sprint does not cancel it.</summary>
        public static SkillSpec Primary(string skillName, Type stateType)
        {
            return new SkillSpec(skillName, stateType) { Cooldown = 0f, InterruptPriority = InterruptPriority.Any };
        }

        /// <summary>Spec for a cooldown skill with skill-level interrupt priority.</summary>
        public static SkillSpec Cooldowned(string skillName, Type stateType, float cooldown)
        {
            return new SkillSpec(skillName, stateType)
            {
                Cooldown = cooldown,
                InterruptPriority = InterruptPriority.Skill,
                MustKeyPress = true,
                AutoHandleLuminousShot = false,
            };
        }
    }

    /// <summary>The SkillLocator passive (shown in character select and the loadout, not a GenericSkill).</summary>
    public sealed class PassiveSpec
    {
        public string NameToken;
        public string DescriptionToken;
        public string NameText;
        public string DescriptionText;
        public string KeywordToken;
        public Sprite Icon;
        public Color IconColor = new Color(0.9f, 0.85f, 0.6f);
    }
}

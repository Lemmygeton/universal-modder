using System.Collections.Generic;
using EntityStates;
using Reclaimed.Core.Content;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace Reclaimed.Core.Skills
{
    /// <summary>
    /// Skill families and their variants. <see cref="Survivors.SurvivorBuilder"/> creates the four slots with
    /// empty families; survivors fill them with <see cref="AddVariant(GenericSkill, SkillDef, UnlockableDef)"/>.
    /// The first variant added is the default.
    /// </summary>
    /// <example>
    /// <code>
    /// SkillFamilyBuilder.AddVariant(survivor.Primary, slamDef);
    /// SkillFamilyBuilder.AddVariant(survivor.Primary, altDef, masteryUnlockable);
    /// SkillFamilyBuilder.SetPassive(survivor.SkillLocator, new PassiveSpec { NameToken = ..., DescriptionToken = ... });
    /// </code>
    /// </example>
    public static class SkillFamilyBuilder
    {
        private static readonly List<SkillFamily> families = new List<SkillFamily>();
        private static SkillDef placeholder;

        public static SkillFamily CreateFamily(string name)
        {
            SkillFamily family = ScriptableObject.CreateInstance<SkillFamily>();
            ((ScriptableObject)family).name = name;
            family.variants = new SkillFamily.Variant[0];
            ReclaimedContent.AddSkillFamily(family);
            families.Add(family);
            return family;
        }

        /// <summary>Adds a GenericSkill with a new, empty, registered family to a body prefab.</summary>
        public static GenericSkill AddSlot(GameObject bodyPrefab, string familyName, string skillName, bool hideInCharacterSelect = false)
        {
            GenericSkill slot = bodyPrefab.AddComponent<GenericSkill>();
            slot.skillName = skillName;
            slot.hideInCharacterSelect = hideInCharacterSelect;
            slot._skillFamily = CreateFamily(familyName);
            return slot;
        }

        public static void AddVariant(GenericSkill slot, SkillDef skillDef, UnlockableDef unlockableDef = null)
        {
            if (slot == null)
            {
                Log.Error("AddVariant: slot is null (skill " + (skillDef != null ? skillDef.skillName : "null") + ").");
                return;
            }
            AddVariant(slot.skillFamily, skillDef, unlockableDef);
        }

        public static void AddVariant(SkillFamily family, SkillDef skillDef, UnlockableDef unlockableDef = null)
        {
            if (family == null || skillDef == null)
            {
                Log.Error("AddVariant: family or skill is null.");
                return;
            }
            var variant = new SkillFamily.Variant
            {
                skillDef = skillDef,
                unlockableDef = unlockableDef,
                viewableNode = new ViewablesCatalog.Node(skillDef.skillNameToken ?? skillDef.skillName, false, null),
            };
            HG.ArrayUtils.ArrayAppend(ref family.variants, in variant);
        }

        /// <summary>Makes an already-added variant the default.</summary>
        public static void SetDefault(SkillFamily family, SkillDef skillDef)
        {
            for (int i = 0; i < family.variants.Length; i++)
            {
                if (family.variants[i].skillDef != skillDef) continue;
                family.defaultVariantIndex = (uint)i;
                return;
            }
            Log.Warn("SetDefault: " + skillDef.skillName + " is not a variant of " + ((ScriptableObject)family).name + ".");
        }

        /// <summary>Fills the SkillLocator's passive (character select and loadout; not a GenericSkill).</summary>
        public static void SetPassive(SkillLocator locator, PassiveSpec spec)
        {
            if (locator == null || spec == null) return;
            Lore.Tokens.Add(spec.NameToken, spec.NameText);
            Lore.Tokens.Add(spec.DescriptionToken, spec.DescriptionText);
            locator.passiveSkill.enabled = true;
            locator.passiveSkill.skillNameToken = spec.NameToken;
            locator.passiveSkill.skillDescriptionToken = spec.DescriptionToken;
            locator.passiveSkill.keywordToken = spec.KeywordToken;
            locator.passiveSkill.icon = spec.Icon != null ? spec.Icon : IconFactory.Create(spec.NameToken ?? "passive", spec.IconColor, IconFactory.Glyph.Ring);
        }

        /// <summary>
        /// Called by Core in Awake: creates and registers the inert skill that unfilled families get. It has to be
        /// registered while R2API still accepts content; additions after the content packs are built are accepted
        /// without error and then silently lost.
        /// </summary>
        internal static void CreatePlaceholder()
        {
            if (placeholder != null) return;
            placeholder = SkillFactory.Create(new SkillSpec("ReclaimedPlaceholderSkill", typeof(PlaceholderSkillState))
            {
                NameToken = "RECLAIMED_PLACEHOLDER_SKILL_NAME",
                NameText = "Unfinished",
                DescriptionToken = "RECLAIMED_PLACEHOLDER_SKILL_DESCRIPTION",
                DescriptionText = "This skill has not been built yet.",
                IconColor = Color.gray,
                IsCombatSkill = false,
            });
        }

        /// <summary>
        /// Called by Core in Start, after every survivor's Awake: a family with no variants breaks character
        /// select, so each one gets the placeholder skill and an error naming it. Only appends a variant to an
        /// already registered family, which is safe at this point.
        /// </summary>
        internal static void FillEmptyFamilies()
        {
            foreach (SkillFamily family in families)
            {
                if (family == null || (family.variants != null && family.variants.Length > 0)) continue;
                if (placeholder == null)
                {
                    Log.Error("Skill family '" + ((ScriptableObject)family).name + "' has no variants and the placeholder skill is missing.");
                    continue;
                }
                Log.Error("Skill family '" + ((ScriptableObject)family).name + "' has no variants; adding a placeholder skill.");
                AddVariant(family, placeholder);
            }
        }
    }

    /// <summary>Does nothing and returns to the main state; used only for unfilled skill families.</summary>
    public sealed class PlaceholderSkillState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (isAuthority) outer.SetNextStateToMain();
        }
    }
}

using Reclaimed.Core.Content;
using Reclaimed.Core.Skills;
using RoR2;
using UnityEngine;

namespace Reclaimed.Core.Unlocks
{
    /// <summary>
    /// Creates and registers UnlockableDefs. The how-to/unlocked strings are set here for safety; when an
    /// achievement rewards the unlockable, the game's achievement manager replaces them with the achievement's
    /// name and description.
    /// </summary>
    public static class UnlockableFactory
    {
        /// <summary>
        /// "Characters.{id}" unlockable for a survivor, rewarded by achievement "{id}Purified" unless
        /// <paramref name="achievementId"/> says otherwise.
        /// </summary>
        public static UnlockableDef CreateSurvivorUnlock(string id, string nameToken, Sprite icon, string achievementId = null)
        {
            return Create("Characters." + id, nameToken, icon, achievementId ?? id + "Purified", 200);
        }

        /// <summary>Unlockable for a skill variant ("Skills.{id}") or skin ("Skins.{id}") etc.</summary>
        public static UnlockableDef Create(string cachedName, string nameToken, Sprite icon, string achievementId, int sortScore = 200)
        {
            UnlockableDef def = ScriptableObject.CreateInstance<UnlockableDef>();
            def.cachedName = cachedName;
            def.nameToken = nameToken;
            def.achievementIcon = icon != null ? icon : IconFactory.Create(cachedName, new Color(0.85f, 0.85f, 1f), IconFactory.Glyph.Star);
            def.sortScore = sortScore;
            string id = (achievementId ?? cachedName).ToUpperInvariant();
            string achName = "ACHIEVEMENT_" + id + "_NAME";
            string achDesc = "ACHIEVEMENT_" + id + "_DESCRIPTION";
            def.getHowToUnlockString = () => Language.GetStringFormatted("UNLOCK_VIA_ACHIEVEMENT_FORMAT", Language.GetString(achName), Language.GetString(achDesc));
            def.getUnlockedString = () => Language.GetStringFormatted("UNLOCKED_FORMAT", Language.GetString(achName), Language.GetString(achDesc));
            ReclaimedContent.AddUnlockable(def);
            return def;
        }
    }
}

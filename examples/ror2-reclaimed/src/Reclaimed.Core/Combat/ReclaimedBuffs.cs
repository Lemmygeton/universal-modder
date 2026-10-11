using Reclaimed.Core.Content;
using Reclaimed.Core.Skills;
using RoR2;
using UnityEngine;

namespace Reclaimed.Core.Combat
{
    /// <summary>BuffDef creation + registration in one call.</summary>
    public static class ReclaimedBuffs
    {
        /// <summary>
        /// Creates and registers a BuffDef named <paramref name="name"/> (use a unique "Reclaimed..." name).
        /// A null icon gets a generated placeholder so the buff bar never shows a blank square.
        /// </summary>
        public static BuffDef Create(string name, Sprite icon, Color color, bool canStack = false, bool isDebuff = false,
            bool isCooldown = false, bool isHidden = false)
        {
            BuffDef buff = ScriptableObject.CreateInstance<BuffDef>();
            ((ScriptableObject)buff).name = name;
            buff.iconSprite = icon != null ? icon : IconFactory.Create(name, color, IconFactory.Glyph.Diamond);
            buff.buffColor = color;
            buff.canStack = canStack;
            buff.isDebuff = isDebuff;
            buff.isCooldown = isCooldown;
            buff.isHidden = isHidden;
            ReclaimedContent.AddBuff(buff);
            return buff;
        }

        /// <summary>
        /// Server-side helper: sets a buff to an exact stack count (adds/removes as needed). Ignores null
        /// bodies/buffs and unregistered buffs.
        /// </summary>
        public static void SetStacks(CharacterBody body, BuffDef buff, int stacks)
        {
            if (body == null || buff == null || buff.buffIndex == BuffIndex.None) return;
            body.SetBuffCount(buff.buffIndex, Mathf.Max(0, stacks));
        }
    }
}

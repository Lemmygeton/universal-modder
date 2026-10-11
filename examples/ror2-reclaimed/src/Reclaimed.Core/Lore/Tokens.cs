using System.Collections.Generic;
using R2API;

namespace Reclaimed.Core.Lore
{
    /// <summary>Language token helpers over <see cref="LanguageAPI"/> (English unless a language is given).</summary>
    public static class Tokens
    {
        private static readonly Dictionary<string, string> added = new Dictionary<string, string>();

        /// <summary>Adds or replaces a token. Empty text is skipped so optional spec fields stay unset.</summary>
        public static void Add(string token, string text)
        {
            if (string.IsNullOrEmpty(token) || text == null) return;
            LanguageAPI.Add(token, text);
            added[token] = text;
        }

        public static void Add(string token, string text, string language)
        {
            if (string.IsNullOrEmpty(token) || text == null) return;
            LanguageAPI.Add(token, text, language);
        }

        /// <summary>The English text we registered for a token, or null (useful for logs and tests).</summary>
        public static string Get(string token)
        {
            string text;
            return token != null && added.TryGetValue(token, out text) ? text : null;
        }

        /// <summary>
        /// Registers the standard survivor token set under <paramref name="prefix"/> (e.g. RECLAIMED_MITHRIX):
        /// _NAME, _SUBTITLE, _DESCRIPTION, _LORE, _OUTRO_FLAVOR, _OUTRO_FAILURE. Returns the token names.
        /// </summary>
        public static SurvivorTokens AddSurvivor(string prefix, string name, string subtitle, string description, string lore,
            string outro, string failure)
        {
            var t = new SurvivorTokens(prefix);
            Add(t.Name, name);
            Add(t.Subtitle, subtitle);
            Add(t.Description, description);
            Add(t.Lore, lore);
            Add(t.Outro, outro);
            Add(t.Failure, failure);
            return t;
        }

        /// <summary>Achievement tokens use the game's naming: ACHIEVEMENT_{ID}_NAME / _DESCRIPTION.</summary>
        public static void AddAchievement(string achievementId, string name, string description)
        {
            string id = achievementId.ToUpperInvariant();
            Add("ACHIEVEMENT_" + id + "_NAME", name);
            Add("ACHIEVEMENT_" + id + "_DESCRIPTION", description);
        }

        /// <summary>Common keyword token (shown under skill descriptions), e.g. KEYWORD_RECLAIMED_ADAPTIVE.</summary>
        public static string AddKeyword(string token, string title, string body)
        {
            Add(token, "<style=cKeywordName>" + title + "</style><style=cSub>" + body + "</style>");
            return token;
        }
    }

    /// <summary>Token names for one survivor, all derived from a prefix.</summary>
    public sealed class SurvivorTokens
    {
        public readonly string Prefix;
        public readonly string Name;
        public readonly string Subtitle;
        public readonly string Description;
        public readonly string Lore;
        public readonly string Outro;
        public readonly string Failure;

        public SurvivorTokens(string prefix)
        {
            Prefix = prefix;
            Name = prefix + "_NAME";
            Subtitle = prefix + "_SUBTITLE";
            Description = prefix + "_DESCRIPTION";
            Lore = prefix + "_LORE";
            Outro = prefix + "_OUTRO_FLAVOR";
            Failure = prefix + "_OUTRO_FAILURE";
        }

        /// <summary>Token for a skill: {prefix}_{slot}_{key}_NAME / _DESCRIPTION.</summary>
        public string Skill(string slot, string key, string part)
        {
            return Prefix + "_" + slot.ToUpperInvariant() + "_" + key.ToUpperInvariant() + "_" + part;
        }
    }
}

using System;
using System.Collections.Generic;
using Reclaimed.Core.Lore;
using RoR2;

namespace Reclaimed.Core.Unlocks
{
    /// <summary>
    /// The registered purification specs, per-run flags for kit-specific trackers, and the server event that
    /// achievement trackers listen to.
    /// </summary>
    public static class PurificationRegistry
    {
        private static readonly Dictionary<string, PurificationSpec> specs = new Dictionary<string, PurificationSpec>();
        private static readonly Dictionary<CharacterMaster, Dictionary<string, object>> runFlags = new Dictionary<CharacterMaster, Dictionary<string, object>>();

        /// <summary>Server: a player qualified for a purification (after any win-the-run requirement).</summary>
        public static event Action<PurificationSpec, NetworkUser> OnPurifiedServer;

        public static IEnumerable<PurificationSpec> Specs { get { return specs.Values; } }

        /// <summary>Registers a spec and its achievement tokens. Call from the survivor plugin's Awake.</summary>
        public static void Register(PurificationSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id))
            {
                Log.Error("PurificationRegistry.Register: spec or spec.Id is null.");
                return;
            }
            if (spec.BossBodyNames == null || spec.BossBodyNames.Length == 0)
                Log.Warn("Purification '" + spec.Id + "' has no boss body names; it can never trigger.");
            if (specs.ContainsKey(spec.Id)) Log.Warn("Purification '" + spec.Id + "' registered twice; the later one wins.");
            specs[spec.Id] = spec;
            Tokens.AddAchievement(spec.ResolvedAchievementId, spec.AchievementName ?? (spec.BossDisplayName ?? spec.Id) + ": Purified",
                spec.AchievementDescription ?? spec.DescribeConditions());
            Log.Info("Purification '" + spec.Id + "': " + spec.DescribeConditions());
        }

        public static PurificationSpec Get(string id)
        {
            PurificationSpec spec;
            return id != null && specs.TryGetValue(id, out spec) ? spec : null;
        }

        /// <summary>Finds a spec by id, achievement id or unlockable name (case-insensitive).</summary>
        public static PurificationSpec Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (PurificationSpec spec in specs.Values)
            {
                if (string.Equals(spec.Id, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(spec.ResolvedAchievementId, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(spec.ResolvedUnlockableName, key, StringComparison.OrdinalIgnoreCase)) return spec;
            }
            return null;
        }

        public static PurificationSpec FindByAchievement(string achievementId)
        {
            foreach (PurificationSpec spec in specs.Values)
                if (spec.ResolvedAchievementId == achievementId) return spec;
            return null;
        }

        /// <summary>Server: marks a fact for a player for the rest of the run (cleared on run start/end).</summary>
        public static void SetRunFlag(CharacterMaster master, string key, object value = null)
        {
            if (master == null || key == null) return;
            Dictionary<string, object> flags;
            if (!runFlags.TryGetValue(master, out flags))
            {
                flags = new Dictionary<string, object>();
                runFlags[master] = flags;
            }
            flags[key] = value ?? true;
        }

        public static object GetRunFlag(CharacterMaster master, string key)
        {
            Dictionary<string, object> flags;
            object value;
            if (master == null || key == null || !runFlags.TryGetValue(master, out flags)) return null;
            return flags.TryGetValue(key, out value) ? value : null;
        }

        internal static void ClearRunFlags()
        {
            runFlags.Clear();
        }

        internal static void RaisePurified(PurificationSpec spec, NetworkUser user)
        {
            Log.Info("Purification '" + spec.Id + "' granted to " + (user != null ? user.userName : "?") + ".");
            Action<PurificationSpec, NetworkUser> handler = OnPurifiedServer;
            if (handler == null)
            {
                Log.Warn("No achievement is listening for '" + spec.Id + "'. Is the survivor's achievement class registered?");
                return;
            }
            handler(spec, user);
        }
    }
}

using System;
using System.Collections.Generic;
using Reclaimed.Core.Assets;
using RoR2.ExpansionManagement;
using UnityEngine;

namespace Reclaimed.Core.Survivors
{
    /// <summary>
    /// Resolves expansion names ("DLC1".."DLC4") to ExpansionDefs without compile-time references to newer
    /// DLC content: first through known Addressables GUIDs at load time, then by name in ExpansionCatalog once
    /// the game has loaded (which also covers a GUID that moved in a patch).
    /// </summary>
    public static class ExpansionResolver
    {
        private static readonly Dictionary<string, string> knownGuids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "DLC1", GameKeys.ExpansionDLC1 },
            { "DLC2", GameKeys.ExpansionDLC2 },
            { "DLC3", GameKeys.ExpansionDLC3 },
            { "DLC4", GameKeys.ExpansionDLC4 },
        };

        private static readonly List<KeyValuePair<ExpansionRequirementComponent, string>> tracked =
            new List<KeyValuePair<ExpansionRequirementComponent, string>>();

        /// <summary>The ExpansionDef for a name, or null (logged) if it cannot be found yet.</summary>
        public static ExpansionDef Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string guid;
            ExpansionDef def;
            if (knownGuids.TryGetValue(name, out guid) && GameAssets.TryLoad(guid, out def)) return def;
            def = FindInCatalog(name);
            if (def == null) Log.Debug("Expansion '" + name + "' not resolvable yet; will retry when the catalog loads.");
            return def;
        }

        /// <summary>Re-checks this component's requirement by name when the expansion catalog is ready.</summary>
        public static void Track(ExpansionRequirementComponent component, string name)
        {
            if (component != null && !string.IsNullOrEmpty(name)) tracked.Add(new KeyValuePair<ExpansionRequirementComponent, string>(component, name));
        }

        public static ExpansionDef FindInCatalog(string name)
        {
            try
            {
                foreach (ExpansionDef def in ExpansionCatalog.expansionDefs)
                    if (def != null && string.Equals(((ScriptableObject)def).name, name, StringComparison.OrdinalIgnoreCase)) return def;
            }
            catch (Exception)
            {
                // Catalog not initialized yet.
            }
            return null;
        }

        internal static void OnCatalogReady()
        {
            foreach (KeyValuePair<ExpansionRequirementComponent, string> pair in tracked)
            {
                if (pair.Key == null) continue;
                ExpansionDef def = FindInCatalog(pair.Value);
                if (def != null) pair.Key.requiredExpansion = def;
                else if (pair.Key.requiredExpansion == null)
                    Log.Error("Expansion '" + pair.Value + "' does not exist in this game; " + pair.Key.name + " will not be gated by it.");
            }
        }
    }
}

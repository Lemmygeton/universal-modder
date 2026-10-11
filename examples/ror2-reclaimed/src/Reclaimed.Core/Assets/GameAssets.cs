using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace Reclaimed.Core.Assets
{
    /// <summary>
    /// Synchronous, cached Addressables loads keyed by GUID (see <see cref="GameKeys"/> and the knowledge base's
    /// asset_keys.tsv). GUIDs survive asset moves between patches, unlike "RoR2/Base/..." paths.
    /// Loads block, so call them during plugin Awake or other load-time code, not every frame.
    /// </summary>
    public static class GameAssets
    {
        private static readonly Dictionary<string, Object> cache = new Dictionary<string, Object>();
        private static readonly HashSet<string> failed = new HashSet<string>();

        /// <summary>Loads an asset or returns null, logging a clear error with <paramref name="context"/> on failure.</summary>
        public static T Load<T>(string guid, string context = null) where T : Object
        {
            T asset;
            if (TryLoad(guid, out asset)) return asset;
            Log.Error("Asset " + typeof(T).Name + " '" + guid + "' could not be loaded" + (context != null ? " (needed by " + context + ")" : "") +
                      ". The GUID may be wrong for this game version or the asset belongs to an unowned/uninstalled DLC.");
            return null;
        }

        /// <summary>Loads an asset without logging; false when the key is empty, unknown or of another type.</summary>
        public static bool TryLoad<T>(string guid, out T asset) where T : Object
        {
            asset = null;
            if (string.IsNullOrEmpty(guid)) return false;
            string key = typeof(T).FullName + "|" + guid;
            Object cached;
            if (cache.TryGetValue(key, out cached))
            {
                asset = cached as T;
                return asset != null;
            }
            if (failed.Contains(key)) return false;
            try
            {
                asset = Addressables.LoadAssetAsync<T>(guid).WaitForCompletion();
            }
            catch (Exception e)
            {
                Log.Debug("Addressables load of " + guid + " threw: " + e.Message);
                asset = null;
            }
            if (asset == null)
            {
                failed.Add(key);
                return false;
            }
            cache[key] = asset;
            return true;
        }

        /// <summary>The body prefab referenced by a CharacterMaster prefab (for bodies only reachable through their master).</summary>
        public static GameObject BodyFromMaster(string masterGuid, string context = null)
        {
            GameObject master = Load<GameObject>(masterGuid, context);
            if (master == null) return null;
            CharacterMaster component = master.GetComponent<CharacterMaster>();
            if (component == null || component.bodyPrefab == null)
            {
                Log.Error("Master '" + master.name + "' (" + masterGuid + ") has no bodyPrefab" + (context != null ? " (needed by " + context + ")" : "") + ".");
                return null;
            }
            return component.bodyPrefab;
        }

        /// <summary>Loads a prefab and returns its component of type <typeparamref name="T"/>, or null.</summary>
        public static T LoadComponent<T>(string prefabGuid, string context = null) where T : Component
        {
            GameObject prefab = Load<GameObject>(prefabGuid, context);
            return prefab != null ? prefab.GetComponent<T>() : null;
        }
    }
}

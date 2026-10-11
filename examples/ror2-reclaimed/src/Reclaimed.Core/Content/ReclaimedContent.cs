using System;
using System.Collections.Generic;
using System.Text;
using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;
using RoR2.Projectile;

namespace Reclaimed.Core.Content
{
    /// <summary>
    /// Thin, logging wrappers over <see cref="ContentAddition"/>. Register everything in plugin Awake.
    /// R2API rejects (and we log) additions made after the matching catalog has initialized, but additions made
    /// after the content packs are built and before the catalogs initialize are accepted and silently lost,
    /// and plugin Start has no guaranteed order relative to that build.
    /// R2API attributes everything added through these wrappers to the Reclaimed.Core content pack.
    /// UnlockableDefs go through ContentAddition.AddUnlockableDef (R2API.ContentManagement 1.0.11), so no
    /// custom IContentPackProvider is needed.
    /// </summary>
    public static class ReclaimedContent
    {
        private static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private static readonly HashSet<Type> entityStates = new HashSet<Type>();
        private static int rejected;

        private static bool Count(string kind, bool added, string what)
        {
            if (added)
            {
                int n;
                counts.TryGetValue(kind, out n);
                counts[kind] = n + 1;
            }
            else
            {
                rejected++;
                Log.Error("Content rejected: " + kind + " '" + what + "' (see the R2API.ContentManagement error above).");
            }
            return added;
        }

        private static bool Null(object o, string kind)
        {
            if (o != null && !(o is UnityEngine.Object uo && uo == null)) return false;
            Log.Error("Tried to register a null " + kind + ".");
            rejected++;
            return true;
        }

        public static bool AddBody(GameObject bodyPrefab)
        {
            if (Null(bodyPrefab, "body")) return false;
            return Count("bodies", ContentAddition.AddBody(bodyPrefab), bodyPrefab.name);
        }

        public static bool AddMaster(GameObject masterPrefab)
        {
            if (Null(masterPrefab, "master")) return false;
            return Count("masters", ContentAddition.AddMaster(masterPrefab), masterPrefab.name);
        }

        public static bool AddSurvivor(SurvivorDef survivorDef)
        {
            if (Null(survivorDef, "survivor def")) return false;
            return Count("survivors", ContentAddition.AddSurvivorDef(survivorDef), survivorDef.cachedName);
        }

        public static bool AddSkill(SkillDef skillDef)
        {
            if (Null(skillDef, "skill def")) return false;
            return Count("skills", ContentAddition.AddSkillDef(skillDef), skillDef.skillName);
        }

        public static bool AddSkillFamily(SkillFamily family)
        {
            if (Null(family, "skill family")) return false;
            return Count("skill families", ContentAddition.AddSkillFamily(family), ((ScriptableObject)family).name);
        }

        /// <summary>Registers an EntityState type once; repeated calls for the same type are no-ops.</summary>
        public static SerializableEntityStateType AddEntityState(Type stateType)
        {
            if (Null(stateType, "entity state type")) return default(SerializableEntityStateType);
            if (entityStates.Contains(stateType)) return new SerializableEntityStateType(stateType);
            bool added;
            SerializableEntityStateType result = ContentAddition.AddEntityState(stateType, out added);
            if (Count("entity states", added, stateType.FullName)) entityStates.Add(stateType);
            return added ? result : new SerializableEntityStateType(stateType);
        }

        public static SerializableEntityStateType AddEntityState<T>() where T : EntityState
        {
            return AddEntityState(typeof(T));
        }

        /// <summary>
        /// Registers an effect prefab. Adds an EffectComponent and VFXAttributes when missing (EffectManager
        /// cannot spawn a prefab without the former; the latter drives VFX budget culling).
        /// </summary>
        public static bool AddEffect(GameObject effectPrefab, bool applyScale = false, string soundName = null)
        {
            if (Null(effectPrefab, "effect")) return false;
            EffectComponent effect = effectPrefab.GetComponent<EffectComponent>();
            if (effect == null)
            {
                Log.Warn("Effect '" + effectPrefab.name + "' had no EffectComponent; adding one.");
                effect = effectPrefab.AddComponent<EffectComponent>();
            }
            effect.applyScale |= applyScale;
            if (!string.IsNullOrEmpty(soundName)) effect.soundName = soundName;
            if (effectPrefab.GetComponent<VFXAttributes>() == null)
            {
                VFXAttributes vfx = effectPrefab.AddComponent<VFXAttributes>();
                vfx.vfxPriority = VFXAttributes.VFXPriority.Medium;
                vfx.vfxIntensity = VFXAttributes.VFXIntensity.Medium;
            }
            if (effectPrefab.GetComponent<NetworkIdentity>() != null)
                Log.Warn("Effect '" + effectPrefab.name + "' has a NetworkIdentity; effects are spawned locally by EffectManager.");
            return Count("effects", ContentAddition.AddEffect(effectPrefab), effectPrefab.name);
        }

        public static bool AddProjectile(GameObject projectilePrefab)
        {
            if (Null(projectilePrefab, "projectile")) return false;
            if (projectilePrefab.GetComponent<ProjectileController>() == null)
            {
                Log.Error("Projectile '" + projectilePrefab.name + "' has no ProjectileController.");
                rejected++;
                return false;
            }
            return Count("projectiles", ContentAddition.AddProjectile(projectilePrefab), projectilePrefab.name);
        }

        public static bool AddNetworkedObject(GameObject prefab)
        {
            if (Null(prefab, "networked object")) return false;
            return Count("networked objects", ContentAddition.AddNetworkedObject(prefab), prefab.name);
        }

        public static bool AddBuff(BuffDef buffDef)
        {
            if (Null(buffDef, "buff def")) return false;
            return Count("buffs", ContentAddition.AddBuffDef(buffDef), ((ScriptableObject)buffDef).name);
        }

        public static bool AddUnlockable(UnlockableDef unlockableDef)
        {
            if (Null(unlockableDef, "unlockable def")) return false;
            return Count("unlockables", ContentAddition.AddUnlockableDef(unlockableDef), unlockableDef.cachedName);
        }

        public static bool AddNetworkSoundEvent(NetworkSoundEventDef def)
        {
            if (Null(def, "network sound event")) return false;
            return Count("network sounds", ContentAddition.AddNetworkSoundEventDef(def), def.eventName);
        }

        /// <summary>One line per content kind; Core logs it when the game finishes loading.</summary>
        public static string Summary()
        {
            var sb = new StringBuilder("Reclaimed content registered:");
            if (counts.Count == 0) sb.Append(" nothing");
            foreach (KeyValuePair<string, int> kv in counts) sb.Append(' ').Append(kv.Value).Append(' ').Append(kv.Key).Append(',');
            if (sb[sb.Length - 1] == ',') sb.Length--;
            if (rejected > 0) sb.Append(" (").Append(rejected).Append(" rejected, see errors above)");
            return sb.ToString();
        }

        internal static int RejectedCount { get { return rejected; } }
    }
}

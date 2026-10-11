using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace Reclaimed.Core.Animation
{
    /// <summary>
    /// Plays the first animator state that exists from a list of candidates. Boss controllers differ between
    /// patches and between phases, so skills name several (layer, state) candidates and the resolver picks
    /// the first <see cref="Animator.HasState"/> hit. Results are cached per controller and request, the
    /// choice is logged once, and a total miss logs the controller's clip names to help pick new candidates.
    /// A config entry under [AnimationOverrides] ("Survivor.Skill = Layer/State") is tried before the list.
    /// </summary>
    public static class AnimationResolver
    {
        private struct Choice
        {
            public bool Found;
            public int Layer;
            public int StateHash;
            public string Description;
        }

        private static readonly Dictionary<string, Choice> cache = new Dictionary<string, Choice>();
        private static readonly Dictionary<string, ConfigEntry<string>> overrides = new Dictionary<string, ConfigEntry<string>>();
        private static readonly Dictionary<int, HashSet<string>> parameterCache = new Dictionary<int, HashSet<string>>();
        private static ConfigFile config;

        public const string OverrideSection = "AnimationOverrides";

        internal static void Init(ConfigFile coreConfig)
        {
            config = coreConfig;
        }

        /// <summary>
        /// Plays the first existing state. <paramref name="rateParam"/> (optional) is scaled so the clip lasts
        /// <paramref name="duration"/> seconds, like vanilla EntityState.PlayAnimation. Returns false when no
        /// candidate exists (nothing is played).
        /// </summary>
        public static bool TryPlay(Animator animator, string[] layers, string[] states, string rateParam, float duration,
            float crossfade = 0f, string overrideKey = null)
        {
            int layer;
            int stateHash;
            if (!TryResolve(animator, layers, states, out layer, out stateHash, overrideKey)) return false;
            Play(animator, layer, stateHash, rateParam, duration, crossfade);
            return true;
        }

        /// <summary>Resolves without playing. <paramref name="stateHash"/> is Animator.StringToHash(state name).</summary>
        public static bool TryResolve(Animator animator, string[] layers, string[] states, out int layer, out int stateHash,
            string overrideKey = null)
        {
            layer = -1;
            stateHash = 0;
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            layers = layers ?? new string[0];
            states = states ?? new string[0];

            string overrideValue = GetOverride(overrideKey);
            string key = animator.runtimeAnimatorController.GetInstanceID() + "|" + overrideKey + "|" + overrideValue + "|" +
                         string.Join(",", layers) + "|" + string.Join(",", states);
            Choice choice;
            if (!cache.TryGetValue(key, out choice))
            {
                choice = Resolve(animator, layers, states, overrideValue);
                cache[key] = choice;
                string who = (overrideKey ?? animator.runtimeAnimatorController.name);
                if (choice.Found) Log.Info("Animation [" + who + "] -> " + choice.Description);
                else Log.Warn("Animation [" + who + "]: none of " + Describe(layers, states) + " exist on controller '" +
                              animator.runtimeAnimatorController.name + "'. " + DescribeController(animator));
            }
            if (!choice.Found) return false;
            layer = choice.Layer;
            stateHash = choice.StateHash;
            return true;
        }

        private static Choice Resolve(Animator animator, string[] layers, string[] states, string overrideValue)
        {
            if (!string.IsNullOrEmpty(overrideValue))
            {
                int slash = overrideValue.IndexOf('/');
                if (slash > 0)
                {
                    Choice c = Check(animator, overrideValue.Substring(0, slash).Trim(), overrideValue.Substring(slash + 1).Trim());
                    if (c.Found)
                    {
                        c.Description += " (config override)";
                        return c;
                    }
                }
                Log.Warn("Animation override '" + overrideValue + "' does not exist on '" + animator.runtimeAnimatorController.name + "'; using the defaults.");
            }
            // States are the outer loop: the first state wins on whichever candidate layer has it.
            foreach (string state in states)
            {
                foreach (string layerName in layers)
                {
                    Choice c = Check(animator, layerName, state);
                    if (c.Found) return c;
                }
            }
            return new Choice();
        }

        private static Choice Check(Animator animator, string layerName, string state)
        {
            int layer = animator.GetLayerIndex(layerName);
            if (layer < 0 || string.IsNullOrEmpty(state)) return new Choice();
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(layer, hash)) return new Choice();
            return new Choice { Found = true, Layer = layer, StateHash = hash, Description = layerName + "/" + state };
        }

        /// <summary>Plays a resolved state, scaling <paramref name="rateParam"/> to fit <paramref name="duration"/>.</summary>
        public static void Play(Animator animator, int layer, int stateHash, string rateParam, float duration, float crossfade)
        {
            if (animator == null) return;
            bool scale = !string.IsNullOrEmpty(rateParam) && duration > 0f && HasParameter(animator, rateParam);
            if (scale) animator.SetFloat(rateParam, 1f);
            if (crossfade > 0f) animator.CrossFadeInFixedTime(stateHash, crossfade, layer);
            else animator.Play(stateHash, layer, 0f);
            if (!scale) return;
            animator.Update(0f);
            AnimatorStateInfo info = animator.IsInTransition(layer) ? animator.GetNextAnimatorStateInfo(layer) : animator.GetCurrentAnimatorStateInfo(layer);
            if (info.length > 0f) animator.SetFloat(rateParam, info.length / duration);
        }

        /// <summary>True when the animator has a parameter with this name (cached per controller).</summary>
        public static bool HasParameter(Animator animator, string name)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return false;
            int id = animator.runtimeAnimatorController.GetInstanceID();
            HashSet<string> names;
            if (!parameterCache.TryGetValue(id, out names))
            {
                names = new HashSet<string>(animator.parameters.Select(p => p.name));
                parameterCache[id] = names;
            }
            return names.Contains(name);
        }

        private static string GetOverride(string overrideKey)
        {
            if (string.IsNullOrEmpty(overrideKey) || config == null) return null;
            ConfigEntry<string> entry;
            if (!overrides.TryGetValue(overrideKey, out entry))
            {
                entry = config.Bind(OverrideSection, overrideKey, "",
                    "Force an animator state for this skill as Layer/State (see reclaimed_dump_animator). Empty = automatic.");
                overrides[overrideKey] = entry;
            }
            return entry.Value;
        }

        private static string Describe(string[] layers, string[] states)
        {
            return "[" + string.Join(", ", states) + "] on layers [" + string.Join(", ", layers) + "]";
        }

        private static string DescribeController(Animator animator)
        {
            var sb = new StringBuilder("Layers: ");
            for (int i = 0; i < animator.layerCount; i++) sb.Append(i == 0 ? "" : ", ").Append(animator.GetLayerName(i));
            sb.Append(". Clips: ");
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            sb.Append(string.Join(", ", clips.Where(c => c != null).Select(c => c.name).Distinct().OrderBy(n => n)));
            return sb.ToString();
        }

        /// <summary>Multi-line description of an animator: layers, parameters (with types) and clip names.</summary>
        public static string Dump(Animator animator)
        {
            if (animator == null) return "(no animator)";
            if (animator.runtimeAnimatorController == null) return "Animator on '" + animator.name + "' has no controller.";
            var sb = new StringBuilder();
            sb.AppendLine("Animator '" + animator.name + "', controller '" + animator.runtimeAnimatorController.name + "'");
            sb.AppendLine("Layers (" + animator.layerCount + "):");
            for (int i = 0; i < animator.layerCount; i++)
                sb.AppendLine("  [" + i + "] " + animator.GetLayerName(i) + " weight=" + animator.GetLayerWeight(i).ToString("0.##"));
            sb.AppendLine("Parameters (" + animator.parameterCount + "):");
            foreach (AnimatorControllerParameter p in animator.parameters) sb.AppendLine("  " + p.name + " : " + p.type);
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            string[] names = clips.Where(c => c != null).Select(c => c.name + " (" + c.length.ToString("0.##") + "s)").Distinct().OrderBy(n => n).ToArray();
            sb.AppendLine("Clips (" + names.Length + "):");
            foreach (string n in names) sb.AppendLine("  " + n);
            sb.Append("State names are not enumerable at runtime; clip names usually match them.");
            return sb.ToString();
        }

        /// <summary>Drops cached choices (after editing overrides in game).</summary>
        public static void ClearCache()
        {
            cache.Clear();
        }
    }
}

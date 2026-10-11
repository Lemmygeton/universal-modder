using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Reclaimed.Core.Survivors
{
    /// <summary>
    /// Removes or disables components on a cloned boss body by type name, so Core never needs compile-time
    /// references to DLC-only boss types. Works on the clone only. Names match the simple or full type name;
    /// a leading '*' matches by suffix (e.g. "*EncounterController").
    /// </summary>
    public static class BodyComponentScrubber
    {
        /// <summary>Removed from every reclaimed body: boss rewards, AI and encounter glue that a player body must not run.</summary>
        public static readonly string[] DefaultRemove =
        {
            "DeathRewards",
            "BaseAI",
            "AISkillDriver",
            "*EncounterController",
            "*BossController",
            "*HealthBarOverlayProvider",
            "*AISkillDriverController",
        };

        /// <summary>Movement components replaced by a CharacterMotor for player control.</summary>
        public static readonly string[] NonCharacterMotors =
        {
            "RigidbodyMotor", "RigidbodyDirection", "QuaternionPID", "VectorPID", "RailMotor",
        };

        public static int Remove(GameObject root, IEnumerable<string> names, ICollection<string> keep, string context)
        {
            var patterns = names.Where(n => !string.IsNullOrEmpty(n)).ToArray();
            if (patterns.Length == 0) return 0;
            int removed = 0;
            // Several passes: [RequireComponent] blocks removing a dependency before its dependents.
            for (int pass = 0; pass < 4; pass++)
            {
                bool progress = false;
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform) continue;
                    Type type = c.GetType();
                    if (!Matches(type, patterns) || Matches(type, keep)) continue;
                    string desc = Describe(root, c);
                    Object.DestroyImmediate(c);
                    if (c == null)
                    {
                        removed++;
                        progress = true;
                        Log.Debug(context + ": removed " + desc);
                    }
                }
                if (!progress) break;
            }
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
                if (c != null && !(c is Transform) && Matches(c.GetType(), patterns) && !Matches(c.GetType(), keep))
                    Log.Warn(context + ": could not remove " + Describe(root, c) + " (another component requires it).");
            return removed;
        }

        public static int Disable(GameObject root, IEnumerable<string> names, string context)
        {
            var patterns = names.Where(n => !string.IsNullOrEmpty(n)).ToArray();
            if (patterns.Length == 0) return 0;
            int disabled = 0;
            foreach (Behaviour b in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (b == null || !Matches(b.GetType(), patterns)) continue;
                b.enabled = false;
                disabled++;
                Log.Debug(context + ": disabled " + Describe(root, b));
            }
            return disabled;
        }

        /// <summary>True if the type (or a base type) matches any name or '*suffix' pattern.</summary>
        public static bool Matches(Type type, IEnumerable<string> patterns)
        {
            if (patterns == null) return false;
            for (Type t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(Component); t = t.BaseType)
            {
                foreach (string p in patterns)
                {
                    if (string.IsNullOrEmpty(p)) continue;
                    if (p[0] == '*')
                    {
                        if (t.Name.EndsWith(p.Substring(1), StringComparison.Ordinal)) return true;
                    }
                    else if (t.Name == p || t.FullName == p) return true;
                }
            }
            return false;
        }

        /// <summary>Removes every component of type T under root (used for skills and state machines being rebuilt).</summary>
        public static int RemoveAll<T>(GameObject root) where T : Component
        {
            int n = 0;
            foreach (T c in root.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(c);
                n++;
            }
            return n;
        }

        private static string Describe(GameObject root, Component c)
        {
            return c.GetType().Name + (c.gameObject == root ? "" : " on '" + c.gameObject.name + "'");
        }
    }
}

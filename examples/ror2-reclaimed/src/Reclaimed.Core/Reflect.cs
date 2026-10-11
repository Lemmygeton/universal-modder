using System;
using System.Collections.Generic;
using System.Reflection;

namespace Reclaimed.Core
{
    /// <summary>
    /// Field access by name for game members that are not part of a stable public API (private backing fields,
    /// fields added or renamed in recent patches). A missing member returns false/null instead of throwing a
    /// MissingFieldException when the calling method is compiled.
    /// </summary>
    internal static class Reflect
    {
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Dictionary<string, FieldInfo> cache = new Dictionary<string, FieldInfo>();

        public static FieldInfo Field(Type type, string name)
        {
            string key = type.FullName + "::" + name;
            FieldInfo field;
            if (!cache.TryGetValue(key, out field))
            {
                field = type.GetField(name, Instance);
                cache[key] = field;
            }
            return field;
        }

        /// <summary>Sets the first field in <paramref name="names"/> that exists and accepts the value.</summary>
        public static bool TrySetField(object target, object value, params string[] names)
        {
            if (target == null) return false;
            foreach (string name in names)
            {
                FieldInfo field = Field(target.GetType(), name);
                if (field == null || (value != null && !field.FieldType.IsInstanceOfType(value))) continue;
                field.SetValue(target, value);
                return true;
            }
            return false;
        }

        /// <summary>The first field in <paramref name="names"/> that exists and holds a <typeparamref name="T"/>, else the first field of that type.</summary>
        public static T GetField<T>(object target, params string[] names) where T : class
        {
            if (target == null) return null;
            Type type = target.GetType();
            foreach (string name in names)
            {
                FieldInfo field = Field(type, name);
                if (field != null && typeof(T).IsAssignableFrom(field.FieldType)) return field.GetValue(target) as T;
            }
            foreach (FieldInfo field in type.GetFields(Instance))
                if (typeof(T).IsAssignableFrom(field.FieldType)) return field.GetValue(target) as T;
            return null;
        }
    }
}

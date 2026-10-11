using System;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace Reclaimed.Core
{
    /// <summary>
    /// Registers config entries with Risk of Options (com.rune580.riskofoptions) through reflection, so the
    /// mod has no compile-time or load-time dependency on it. Every call is a silent no-op when it is absent
    /// or when its API does not match what we expect.
    /// </summary>
    public static class RiskOfOptionsBridge
    {
        public const string Guid = "com.rune580.riskofoptions";

        private static bool resolved;
        private static bool available;
        private static Assembly assembly;
        private static Type manager;

        public static bool IsAvailable
        {
            get
            {
                Resolve();
                return available;
            }
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            try
            {
                if (!Chainloader.PluginInfos.ContainsKey(Guid)) return;
                assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RiskOfOptions");
                manager = assembly != null ? assembly.GetType("RiskOfOptions.ModSettingsManager") : null;
                available = manager != null;
                if (!available) Log.Warn("Risk of Options is installed but its ModSettingsManager was not found; options menu disabled.");
            }
            catch (Exception e)
            {
                available = false;
                Log.Warn("Risk of Options lookup failed, options menu disabled: " + e.Message);
            }
        }

        public static void AddBool(ConfigEntry<bool> entry, string modGuid, string modName, bool restartRequired = false)
        {
            AddOption("RiskOfOptions.Options.CheckBoxOption", "RiskOfOptions.OptionConfigs.CheckBoxConfig", entry, modGuid, modName,
                restartRequired, null);
        }

        public static void AddFloatSlider(ConfigEntry<float> entry, float min, float max, string modGuid, string modName,
            string format = "{0:0.##}", bool restartRequired = false)
        {
            AddOption("RiskOfOptions.Options.SliderOption", "RiskOfOptions.OptionConfigs.SliderConfig", entry, modGuid, modName,
                restartRequired, cfg =>
                {
                    SetMember(cfg, "min", min);
                    SetMember(cfg, "max", max);
                    SetMember(cfg, "formatString", format);
                });
        }

        public static void AddIntSlider(ConfigEntry<int> entry, int min, int max, string modGuid, string modName, bool restartRequired = false)
        {
            AddOption("RiskOfOptions.Options.IntSliderOption", "RiskOfOptions.OptionConfigs.IntSliderConfig", entry, modGuid, modName,
                restartRequired, cfg =>
                {
                    SetMember(cfg, "min", min);
                    SetMember(cfg, "max", max);
                });
        }

        /// <summary>Free numeric input; falls back to a wide slider on Risk of Options builds without field options.</summary>
        public static void AddFloatField(ConfigEntry<float> entry, string modGuid, string modName, bool restartRequired = false)
        {
            if (!IsAvailable) return;
            if (assembly.GetType("RiskOfOptions.Options.FloatFieldOption") != null)
                AddOption("RiskOfOptions.Options.FloatFieldOption", "RiskOfOptions.OptionConfigs.FloatFieldConfig", entry, modGuid, modName, restartRequired, null);
            else
                AddFloatSlider(entry, Mathf.Min(0f, entry.Value * 4f), Mathf.Max(10f, Mathf.Abs(entry.Value) * 4f), modGuid, modName, "{0:0.##}", restartRequired);
        }

        public static void AddIntField(ConfigEntry<int> entry, string modGuid, string modName, bool restartRequired = false)
        {
            if (!IsAvailable) return;
            if (assembly.GetType("RiskOfOptions.Options.IntFieldOption") != null)
                AddOption("RiskOfOptions.Options.IntFieldOption", "RiskOfOptions.OptionConfigs.IntFieldConfig", entry, modGuid, modName, restartRequired, null);
            else
                AddIntSlider(entry, Mathf.Min(0, entry.Value * 4), Mathf.Max(10, Mathf.Abs(entry.Value) * 4), modGuid, modName, restartRequired);
        }

        public static void AddKeyBind(ConfigEntry<KeyboardShortcut> entry, string modGuid, string modName)
        {
            AddOption("RiskOfOptions.Options.KeyBindOption", "RiskOfOptions.OptionConfigs.KeyBindConfig", entry, modGuid, modName, false, null);
        }

        public static void AddString(ConfigEntry<string> entry, string modGuid, string modName, bool restartRequired = false)
        {
            AddOption("RiskOfOptions.Options.StringInputFieldOption", "RiskOfOptions.OptionConfigs.InputFieldConfig", entry, modGuid, modName,
                restartRequired, null);
        }

        public static void SetModDescription(string description, string modGuid, string modName)
        {
            InvokeManager("SetModDescription", new object[] { description, modGuid, modName });
        }

        public static void SetModIcon(Sprite icon, string modGuid, string modName)
        {
            if (icon == null) return;
            InvokeManager("SetModIcon", new object[] { icon, modGuid, modName });
        }

        private static void AddOption(string optionTypeName, string configTypeName, ConfigEntryBase entry, string modGuid, string modName,
            bool restartRequired, Action<object> configure)
        {
            if (!IsAvailable || entry == null) return;
            try
            {
                Type optionType = assembly.GetType(optionTypeName);
                if (optionType == null)
                {
                    Log.WarnOnce("roo.type." + optionTypeName, "Risk of Options has no " + optionTypeName + "; skipping those options.");
                    return;
                }
                object option = null;
                Type configType = assembly.GetType(configTypeName);
                if (configType != null)
                {
                    object config = Activator.CreateInstance(configType);
                    SetMember(config, "restartRequired", restartRequired);
                    if (configure != null) configure(config);
                    ConstructorInfo withConfig = optionType.GetConstructor(new[] { entry.GetType(), configType });
                    if (withConfig != null) option = withConfig.Invoke(new[] { entry, config });
                }
                if (option == null)
                {
                    ConstructorInfo plain = optionType.GetConstructor(new[] { entry.GetType() });
                    if (plain == null)
                    {
                        Log.WarnOnce("roo.ctor." + optionTypeName, "Risk of Options " + optionTypeName + " has no constructor for " + entry.GetType().Name + ".");
                        return;
                    }
                    option = plain.Invoke(new object[] { entry });
                }
                InvokeManager("AddOption", new[] { option, modGuid, modName });
            }
            catch (Exception e)
            {
                Log.WarnOnce("roo.add." + optionTypeName, "Risk of Options rejected an option (" + entry.Definition + "): " + e.Message);
            }
        }

        private static void InvokeManager(string methodName, object[] args)
        {
            if (!IsAvailable) return;
            try
            {
                MethodInfo method = manager.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == methodName && ParametersAccept(m.GetParameters(), args));
                if (method == null)
                {
                    Log.WarnOnce("roo.method." + methodName, "Risk of Options has no compatible " + methodName + "; skipping.");
                    return;
                }
                method.Invoke(null, args);
            }
            catch (Exception e)
            {
                Log.WarnOnce("roo.call." + methodName, "Risk of Options " + methodName + " failed: " + (e.InnerException ?? e).Message);
            }
        }

        private static bool ParametersAccept(ParameterInfo[] parameters, object[] args)
        {
            if (parameters.Length != args.Length) return false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null) continue;
                if (!parameters[i].ParameterType.IsInstanceOfType(args[i])) return false;
            }
            return true;
        }

        // Risk of Options has used both public fields and properties, in camelCase and PascalCase, across versions.
        private static void SetMember(object target, string name, object value)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, flags);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(target, value);
                return;
            }
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanWrite && property.PropertyType.IsInstanceOfType(value))
                property.SetValue(target, value, null);
        }
    }
}

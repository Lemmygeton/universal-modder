using System;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace Reclaimed.Core
{
    /// <summary>
    /// Per-plugin config helper. Every <c>Bind</c> writes to the plugin's BepInEx config file and, when Risk of
    /// Options is installed, adds a matching widget to that plugin's options page.
    /// </summary>
    /// <example>
    /// <code>
    /// var cfg = new ReclaimedConfig(this);
    /// ConfigEntry&lt;float&gt; damage = cfg.Bind("Primary", "Damage", 2.4f, "Damage coefficient per swing.", 0.1f, 10f);
    /// </code>
    /// </example>
    public sealed class ReclaimedConfig
    {
        public ConfigFile File { get; private set; }
        public string ModGuid { get; private set; }
        public string ModName { get; private set; }

        public ReclaimedConfig(BaseUnityPlugin plugin)
            : this(plugin.Config, plugin.Info.Metadata.GUID, plugin.Info.Metadata.Name)
        {
        }

        public ReclaimedConfig(ConfigFile file, string modGuid, string modName)
        {
            if (file == null) throw new ArgumentNullException("file");
            File = file;
            ModGuid = modGuid;
            ModName = modName;
        }

        /// <summary>
        /// Binds an entry. For int/float, <paramref name="min"/>/<paramref name="max"/> (both set) clamp the
        /// value and produce a slider, otherwise a numeric field; bool/KeyboardShortcut/string get a checkbox,
        /// keybind or text field.
        /// </summary>
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description,
            float min = float.NaN, float max = float.NaN, bool restartRequired = false)
        {
            bool ranged = !float.IsNaN(min) && !float.IsNaN(max);
            AcceptableValueBase range = null;
            if (ranged && typeof(T) == typeof(float)) range = new AcceptableValueRange<float>(min, max);
            else if (ranged && typeof(T) == typeof(int)) range = new AcceptableValueRange<int>(Mathf.RoundToInt(min), Mathf.RoundToInt(max));

            string text = restartRequired ? description + " (restart required)" : description;
            ConfigEntry<T> entry = File.Bind(section, key, defaultValue, new ConfigDescription(text, range));
            Register(entry, ranged, min, max, restartRequired);
            return entry;
        }

        /// <summary>Binds a value that only exists in the config file (no Risk of Options widget).</summary>
        public ConfigEntry<T> BindHidden<T>(string section, string key, T defaultValue, string description)
        {
            return File.Bind(section, key, defaultValue, description);
        }

        private void Register<T>(ConfigEntry<T> entry, bool ranged, float min, float max, bool restartRequired)
        {
            object boxed = entry;
            if (boxed is ConfigEntry<bool> b) RiskOfOptionsBridge.AddBool(b, ModGuid, ModName, restartRequired);
            else if (boxed is ConfigEntry<float> f)
            {
                if (ranged) RiskOfOptionsBridge.AddFloatSlider(f, min, max, ModGuid, ModName, "{0:0.##}", restartRequired);
                else RiskOfOptionsBridge.AddFloatField(f, ModGuid, ModName, restartRequired);
            }
            else if (boxed is ConfigEntry<int> i)
            {
                if (ranged) RiskOfOptionsBridge.AddIntSlider(i, Mathf.RoundToInt(min), Mathf.RoundToInt(max), ModGuid, ModName, restartRequired);
                else RiskOfOptionsBridge.AddIntField(i, ModGuid, ModName, restartRequired);
            }
            else if (boxed is ConfigEntry<KeyboardShortcut> k) RiskOfOptionsBridge.AddKeyBind(k, ModGuid, ModName);
            else if (boxed is ConfigEntry<string> s) RiskOfOptionsBridge.AddString(s, ModGuid, ModName, restartRequired);
        }

        public void SetModDescription(string description)
        {
            RiskOfOptionsBridge.SetModDescription(description, ModGuid, ModName);
        }

        public void SetModIcon(Sprite icon)
        {
            RiskOfOptionsBridge.SetModIcon(icon, ModGuid, ModName);
        }
    }
}

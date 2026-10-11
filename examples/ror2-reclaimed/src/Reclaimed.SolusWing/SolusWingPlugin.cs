using BepInEx;
using R2API;
using Reclaimed.Core;

[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace Reclaimed.SolusWing
{
    /// <summary>Solus Wing, reclaimed from SolusWingBody (DLC3).</summary>
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInDependency(ReclaimedCorePlugin.Guid)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    public sealed class SolusWingPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.reclaimed.soluswing";
        public const string PluginName = "Reclaimed Solus Wing";
        public const string Version = "0.1.0";

        private void Awake()
        {
            // Vertical slice pending: the body, kit and purification unlock are built in a later phase.
            // Until then the plugin only announces itself so load order and packaging can be verified.
            Log.Info(PluginName + " " + Version + " loaded (vertical slice pending).");
        }
    }
}

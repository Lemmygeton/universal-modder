using BepInEx;
using R2API;
using Reclaimed.Core;

[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace Reclaimed.Voidling
{
    /// <summary>Voidling, Diviner of the Deep, reclaimed from MiniVoidRaidCrabBodyPhase1-3 (Planetarium, DLC1).</summary>
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInDependency(ReclaimedCorePlugin.Guid)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    public sealed class VoidlingPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.reclaimed.voidling";
        public const string PluginName = "Reclaimed Voidling";
        public const string Version = "0.1.0";

        private void Awake()
        {
            // Vertical slice pending: the body, kit and purification unlock are built in a later phase.
            // Until then the plugin only announces itself so load order and packaging can be verified.
            Log.Info(PluginName + " " + Version + " loaded (vertical slice pending).");
        }
    }
}

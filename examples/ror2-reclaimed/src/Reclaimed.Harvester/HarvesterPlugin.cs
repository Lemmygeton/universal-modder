using BepInEx;
using R2API;
using Reclaimed.Core;

[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace Reclaimed.Harvester
{
    /// <summary>Anima Harvester, reclaimed from ReaperBossBody (DLC4).</summary>
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInDependency(ReclaimedCorePlugin.Guid)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    public sealed class HarvesterPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.reclaimed.harvester";
        public const string PluginName = "Reclaimed Anima Harvester";
        public const string Version = "0.1.0";

        private void Awake()
        {
            // Vertical slice pending: the body, kit and purification unlock are built in a later phase.
            // Until then the plugin only announces itself so load order and packaging can be verified.
            Log.Info(PluginName + " " + Version + " loaded (vertical slice pending).");
        }
    }
}

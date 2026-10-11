using System;
using BepInEx;
using BepInEx.Configuration;
using R2API;
using R2API.Networking;
using Reclaimed.Core.Animation;
using Reclaimed.Core.Bosses;
using Reclaimed.Core.Combat;
using Reclaimed.Core.Content;
using Reclaimed.Core.Debug;
using Reclaimed.Core.Resources;
using Reclaimed.Core.Skills;
using Reclaimed.Core.Survivors;
using Reclaimed.Core.Unlocks;
using RoR2;

namespace Reclaimed.Core
{
    /// <summary>
    /// Shared frameworks for the Reclaimed survivors. Survivor plugins depend on this GUID, so everything here
    /// is installed before their Awake runs.
    /// </summary>
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(R2API.ContentManagement.R2APIContentManager.PluginGUID)]
    [BepInDependency(NetworkingAPI.PluginGUID)]
    [BepInDependency(Skins.PluginGUID)]
    [BepInDependency(RecalculateStatsAPI.PluginGUID)]
    [BepInDependency(DamageAPI.PluginGUID)]
    [BepInDependency(RiskOfOptionsBridge.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class ReclaimedCorePlugin : BaseUnityPlugin
    {
        public const string Guid = "com.reclaimed.core";
        public const string PluginName = "Reclaimed Core";
        public const string Version = "0.1.0";

        public static ReclaimedCorePlugin Instance { get; private set; }

        /// <summary>Core's own config (com.reclaimed.core.cfg): debug flags and animation overrides.</summary>
        public static ReclaimedConfig CoreConfig { get; private set; }

        public static ConfigEntry<bool> AllowCheatCommands { get; private set; }
        public static ConfigEntry<bool> ShowResourceBars { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log.Init(Logger);
            CoreConfig = new ReclaimedConfig(this);
            CoreConfig.SetModDescription("Shared settings for Reclaimed: playable bosses, False Son style.");
            AllowCheatCommands = CoreConfig.Bind("Debug", "AllowCheatCommands", false,
                "Enables reclaimed_grant_unlock and other testing commands.");
            ShowResourceBars = CoreConfig.Bind("HUD", "ShowResourceBars", true, "Show survivor resource bars under the health bar.",
                restartRequired: true);

            // Each subsystem installs on its own: a game-API change that breaks one (MissingMemberException when
            // its installer is compiled) must not take the others down with it.
            TryInstall("animation overrides", () => AnimationResolver.Init(Config));
            TryInstall("resource meter networking", ResourceMeterNetwork.Install);
            if (ShowResourceBars.Value) TryInstall("resource meter HUD", ResourceMeterHud.Install);
            TryInstall("adaptive armor", AdaptiveArmor.Install);
            TryInstall("boss fight recorder", BossFightRecorder.Install);
            TryInstall("boss defeat tracker", BossDefeatTracker.Install);
            TryInstall("boss recognition", BossRecognition.Install);
            TryInstall("console commands", () => ReclaimedCommands.Install(AllowCheatCommands));
            // Content must be registered before R2API builds the content packs; Awake is the only safe point.
            TryInstall("placeholder skill", SkillFamilyBuilder.CreatePlaceholder);

            RoR2Application.onLoad += OnGameLoaded;
            Log.Info(PluginName + " " + Version + " ready.");
        }

        private void Start()
        {
            // After every plugin's Awake. Only appends an already registered SkillDef to families (the content pack
            // holds families by reference and SkillCatalog reads their variants when it initializes).
            SkillFamilyBuilder.FillEmptyFamilies();
        }

        private static void TryInstall(string subsystem, Action install)
        {
            try
            {
                install();
            }
            catch (Exception e)
            {
                Log.Exception(e, "Could not install " + subsystem + "; it is disabled for this session");
            }
        }

        private void OnGameLoaded()
        {
            ExpansionResolver.OnCatalogReady();
            Log.Info(ReclaimedContent.Summary());
        }
    }
}

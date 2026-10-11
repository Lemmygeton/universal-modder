using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Reclaimed.Core.Animation;
using Reclaimed.Core.Unlocks;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Reclaimed.Core.Debug
{
    /// <summary>
    /// Console commands. They are injected into RoR2.Console's command table directly (what R2API.CommandHelper
    /// used to do) rather than through [ConCommand]: the game's attribute scan can run before plugins load,
    /// and a later duplicate scan would throw on our names.
    /// </summary>
    public static class ReclaimedCommands
    {
        private sealed class Command
        {
            public string Name;
            public string Help;
            public bool Cheat;
            public Action<ConCommandArgs> Action;
        }

        private static readonly Dictionary<string, Command> commands = new Dictionary<string, Command>();
        private static ConfigEntry<bool> allowCheats;
        private static bool installed;

        internal static void Install(ConfigEntry<bool> allowCheatCommands)
        {
            if (installed) return;
            installed = true;
            allowCheats = allowCheatCommands;
            Register("reclaimed_dump_animator", "Prints layers, parameters and clip names of your body's model animator.", DumpAnimator);
            Register("reclaimed_grant_unlock", "reclaimed_grant_unlock <id>: grants a Reclaimed purification to you (host only; needs Debug.AllowCheatCommands).",
                GrantUnlock, true);
            Register("reclaimed_list_purifications", "Lists purification ids and their conditions.", ListPurifications);

            RoR2Application.onLoad += InjectAll;
            // Fallback for a console created after onLoad: inject on first use of one of our names.
            On.RoR2.Console.RunCmd += (orig, self, sender, name, args) =>
            {
                if (name != null && commands.ContainsKey(name.ToLowerInvariant())) Inject(self);
                orig(self, sender, name, args);
            };
        }

        /// <summary>
        /// Adds a console command (lowercase name). <paramref name="cheat"/> commands only run when the config entry
        /// Debug.AllowCheatCommands is true.
        /// </summary>
        public static void Register(string name, string help, Action<ConCommandArgs> action, bool cheat = false)
        {
            name = name.ToLowerInvariant();
            commands[name] = new Command { Name = name, Help = help, Cheat = cheat, Action = action };
            if (RoR2.Console.instance != null) Inject(RoR2.Console.instance);
        }

        private static void InjectAll()
        {
            if (RoR2.Console.instance != null) Inject(RoR2.Console.instance);
        }

        private static void Inject(RoR2.Console console)
        {
            if (console == null) return;
            Dictionary<string, RoR2.Console.ConCommand> catalog =
                Reflect.GetField<Dictionary<string, RoR2.Console.ConCommand>>(console, "concommandCatalog");
            if (catalog == null)
            {
                Log.WarnOnce("console.catalog", "RoR2.Console has no command table in this game version; Reclaimed console commands are unavailable.");
                return;
            }
            foreach (Command c in commands.Values)
            {
                if (catalog.ContainsKey(c.Name)) continue;
                Command captured = c;
                catalog[c.Name] = new RoR2.Console.ConCommand
                {
                    flags = ConVarFlags.None,
                    helpText = c.Help,
                    action = args => Run(captured, args),
                };
            }
        }

        private static void Run(Command command, ConCommandArgs args)
        {
            if (command.Cheat && (allowCheats == null || !allowCheats.Value))
            {
                UnityEngine.Debug.Log(command.Name + " is disabled. Set [Debug] AllowCheatCommands = true in com.reclaimed.core.cfg.");
                return;
            }
            try
            {
                command.Action(args);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(command.Name + " failed: " + e.Message);
                Log.Exception(e, command.Name);
            }
        }

        private static CharacterBody LocalBody()
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            return user != null ? user.cachedBody : null;
        }

        private static void DumpAnimator(ConCommandArgs args)
        {
            CharacterBody body = LocalBody();
            if (body == null || body.modelLocator == null || body.modelLocator.modelTransform == null)
            {
                UnityEngine.Debug.Log("reclaimed_dump_animator: you have no body with a model.");
                return;
            }
            Animator animator = body.modelLocator.modelTransform.GetComponent<Animator>();
            string dump = AnimationResolver.Dump(animator);
            UnityEngine.Debug.Log(dump);
            Log.Info("Animator of " + body.name + ":\n" + dump);
        }

        private static void GrantUnlock(ConCommandArgs args)
        {
            if (args.Count < 1)
            {
                UnityEngine.Debug.Log("usage: reclaimed_grant_unlock <id>  (see reclaimed_list_purifications)");
                return;
            }
            PurificationSpec spec = PurificationRegistry.Find(args[0]);
            if (spec == null)
            {
                UnityEngine.Debug.Log("No purification '" + args[0] + "'. See reclaimed_list_purifications.");
                return;
            }
            if (!NetworkServer.active)
            {
                UnityEngine.Debug.Log("reclaimed_grant_unlock must run on the host during a run.");
                return;
            }
            NetworkUser user = args.sender;
            if (user == null)
            {
                LocalUser local = LocalUserManager.GetFirstLocalUser();
                user = local != null ? local.currentNetworkUser : null;
            }
            if (user == null)
            {
                UnityEngine.Debug.Log("reclaimed_grant_unlock: start a run first (achievements are granted to a network user).");
                return;
            }
            BossDefeatTracker.ForceGrant(spec, user);
            UnityEngine.Debug.Log("Granted purification '" + spec.Id + "' to " + user.userName + ".");
        }

        private static void ListPurifications(ConCommandArgs args)
        {
            foreach (PurificationSpec spec in PurificationRegistry.Specs)
                UnityEngine.Debug.Log(spec.Id + " (" + spec.ResolvedAchievementId + " -> " + spec.ResolvedUnlockableName + "): " + spec.DescribeConditions());
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using Reclaimed.Core.Lore;
using Reclaimed.Core.Unlocks;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Reclaimed.Core.Bosses
{
    /// <summary>
    /// When a boss spawns and a player is playing its reclaimed survivor, the boss "sees its reclaimed self"
    /// and says one line in chat, once per stage. Nothing about the boss's behaviour changes.
    /// Lines are registered as language tokens so every client reads them in its own language.
    /// </summary>
    /// <example>
    /// <code>
    /// BossRecognition.Register("BrotherBody", "ReclaimedMithrixBody", "Mithrix", new Color(0.62f, 0.8f, 1f),
    ///     "A hollow copy of my strength... you wear my pride like a borrowed coat.",
    ///     "So my brother's vermin learned to wear my face.");
    /// </code>
    /// </example>
    public static class BossRecognition
    {
        private sealed class Entry
        {
            public string BossBodyName;
            public string SurvivorBodyName;
            public readonly List<string> LineTokens = new List<string>();
        }

        private static readonly Dictionary<string, List<Entry>> byBoss = new Dictionary<string, List<Entry>>();
        private static readonly HashSet<string> spokenThisStage = new HashSet<string>();
        private static bool installed;

        /// <summary>Delay after the boss spawns before it speaks, so the line is not lost in spawn spam.</summary>
        public static float SpeakDelay = 2.5f;

        /// <summary>
        /// Registers lines for <paramref name="bossBodyName"/> meeting <paramref name="survivorBodyName"/>.
        /// <paramref name="speaker"/> is shown as the name; the whole line uses <paramref name="color"/>.
        /// </summary>
        public static void Register(string bossBodyName, string survivorBodyName, string speaker, Color color, params string[] lines)
        {
            if (string.IsNullOrEmpty(bossBodyName) || string.IsNullOrEmpty(survivorBodyName) || lines == null || lines.Length == 0) return;
            var entry = new Entry { BossBodyName = bossBodyName, SurvivorBodyName = survivorBodyName };
            string hex = ColorUtility.ToHtmlStringRGB(color);
            for (int i = 0; i < lines.Length; i++)
            {
                string token = "RECLAIMED_BOSSLINE_" + bossBodyName.ToUpperInvariant() + "_" + survivorBodyName.ToUpperInvariant() + "_" + i;
                Tokens.Add(token, "<color=#" + hex + "><style=cEvent>" + speaker + ":</style> " + lines[i] + "</color>");
                entry.LineTokens.Add(token);
            }
            List<Entry> list;
            if (!byBoss.TryGetValue(bossBodyName, out list))
            {
                list = new List<Entry>();
                byBoss[bossBodyName] = list;
            }
            list.Add(entry);
        }

        internal static void Install()
        {
            if (installed) return;
            installed = true;
            CharacterBody.onBodyStartGlobal += OnBodyStart;
            Stage.onServerStageBegin += stage => spokenThisStage.Clear();
            Run.onRunStartGlobal += run => spokenThisStage.Clear();
        }

        private static void OnBodyStart(CharacterBody body)
        {
            if (!NetworkServer.active || body == null || byBoss.Count == 0) return;
            if (!BossFightRecorder.IsHostileBoss(body)) return;
            List<Entry> entries;
            if (!byBoss.TryGetValue(BodyCatalog.GetBodyName(body.bodyIndex), out entries)) return;
            foreach (Entry entry in entries)
            {
                string key = entry.BossBodyName + "|" + entry.SurvivorBodyName;
                if (spokenThisStage.Contains(key) || !AnyPlayerPlaying(entry.SurvivorBodyName)) continue;
                spokenThisStage.Add(key);
                string token = entry.LineTokens[Random.Range(0, entry.LineTokens.Count)];
                if (ReclaimedCorePlugin.Instance != null) ReclaimedCorePlugin.Instance.StartCoroutine(Speak(token, SpeakDelay));
                else Speak(token);
                return;
            }
        }

        private static IEnumerator Speak(string token, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (NetworkServer.active) Speak(token);
        }

        private static void Speak(string token)
        {
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = token });
        }

        private static bool AnyPlayerPlaying(string survivorBodyName)
        {
            BodyIndex index = BodyCatalog.FindBodyIndex(survivorBodyName);
            if (index == BodyIndex.None) return false;
            foreach (PlayerCharacterMasterController pcmc in PlayerCharacterMasterController.instances)
            {
                if (pcmc == null || pcmc.master == null) continue;
                CharacterBody body = pcmc.master.GetBody();
                if (body != null && body.bodyIndex == index) return true;
                GameObject prefab = pcmc.master.bodyPrefab;
                if (prefab != null && prefab.name == survivorBodyName) return true;
            }
            return false;
        }
    }
}

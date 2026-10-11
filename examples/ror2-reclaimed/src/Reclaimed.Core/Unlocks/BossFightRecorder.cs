using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Reclaimed.Core.Unlocks
{
    /// <summary>Per-player facts gathered during one boss fight (server).</summary>
    public sealed class PlayerFightRecord
    {
        public CharacterMaster Master;
        public NetworkUser User;
        public float DamageTaken;
        public int HitsTaken;
        public int Deaths;
        public float DamageDealtToBoss;
        public int HitsOnBoss;
        /// <summary>Lowest health+shield fraction reached during the fight (1 = never hurt).</summary>
        public float LowestHealthFraction = 1f;
        public readonly HashSet<EquipmentIndex> EquipmentUsed = new HashSet<EquipmentIndex>();
        public readonly Dictionary<ItemIndex, int> ItemsGained = new Dictionary<ItemIndex, int>();
        /// <summary>Damage taken per attacker body name (e.g. which boss attack hit you).</summary>
        public readonly Dictionary<string, float> DamageTakenBySource = new Dictionary<string, float>();

        public bool DealtDamage { get { return HitsOnBoss > 0; } }
    }

    /// <summary>A recorded fight for one <see cref="PurificationSpec"/>.</summary>
    public sealed class BossFight
    {
        public PurificationSpec Spec;
        public float StartRunTime;
        public string SceneName;
        public readonly HashSet<CharacterBody> BossBodies = new HashSet<CharacterBody>();
        public readonly Dictionary<CharacterMaster, PlayerFightRecord> Players = new Dictionary<CharacterMaster, PlayerFightRecord>();

        public PlayerFightRecord RecordFor(CharacterMaster master)
        {
            PlayerFightRecord record;
            if (master == null) return null;
            if (!Players.TryGetValue(master, out record))
            {
                record = new PlayerFightRecord { Master = master };
                PlayerCharacterMasterController pcmc = master.playerCharacterMasterController;
                if (pcmc != null) record.User = pcmc.networkUser;
                Players[master] = record;
            }
            return record;
        }

        public bool IsBoss(CharacterBody body)
        {
            if (body == null) return false;
            if (BossBodies.Contains(body)) return true;
            return BossFightRecorder.IsHostileBoss(body) && Spec.IsFightBody(BodyCatalog.GetBodyName(body.bodyIndex));
        }

        /// <summary>
        /// Whether a hostile copy of one of the spec's defeat bodies, other than <paramref name="except"/>, is still
        /// alive (e.g. the second Mithrix under Artifact of Swarms).
        /// </summary>
        public bool AnyDefeatBodyAlive(CharacterBody except)
        {
            BossBodies.RemoveWhere(b => b == null);
            foreach (CharacterBody body in BossBodies)
            {
                if (body == except || body.healthComponent == null || !body.healthComponent.alive) continue;
                if (Spec.IsDefeatBody(BodyCatalog.GetBodyName(body.bodyIndex))) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Records per-player facts while a purification-relevant boss is alive: damage taken (total, by source,
    /// lowest health), deaths, damage dealt to the boss, equipment used and items gained. A fight starts when
    /// one of the spec's fight bodies starts on the server (hostile copies only, see <see cref="IsHostileBoss"/>) and
    /// ends when the last defeat body dies, or at stage change or run end.
    /// Conditions read these through <see cref="PurificationContext.Record"/>.
    /// </summary>
    public static class BossFightRecorder
    {
        private static readonly Dictionary<string, BossFight> active = new Dictionary<string, BossFight>();
        private static bool installed;

        public static IEnumerable<BossFight> ActiveFights { get { return active.Values; } }

        /// <summary>The fight being recorded for a spec, or null.</summary>
        public static BossFight GetActiveFight(string specId)
        {
            BossFight fight;
            return specId != null && active.TryGetValue(specId, out fight) ? fight : null;
        }

        internal static void Install()
        {
            if (installed) return;
            installed = true;
            CharacterBody.onBodyStartGlobal += OnBodyStart;
            GlobalEventManager.onServerDamageDealt += OnDamageDealt;
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
            Stage.onServerStageBegin += stage => active.Clear();
            Run.onRunStartGlobal += run => active.Clear();
            Run.onRunDestroyGlobal += run => active.Clear();
            // Items and equipment were reworked in recent patches and these events cannot be verified against the
            // live game here; if one is gone only its fact is lost, not the whole recorder.
            TrySubscribe("equipment used", SubscribeEquipment);
            TrySubscribe("items gained", SubscribeItems);
        }

        private static void TrySubscribe(string fact, Action subscribe)
        {
            try
            {
                subscribe();
            }
            catch (Exception e)
            {
                Log.Warn("Boss fight recorder: '" + fact + "' is not recorded in this game version (" + e.GetType().Name + ": " + e.Message + ").");
            }
        }

        // Separate, non-inlined: a missing event accessor throws when this method is compiled, inside TrySubscribe.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SubscribeEquipment()
        {
            EquipmentSlot.onServerEquipmentActivated += OnEquipment;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SubscribeItems()
        {
            Inventory.onServerItemGiven += OnItemGiven;
        }

        internal static BossFight StartOrGet(PurificationSpec spec)
        {
            BossFight fight;
            if (active.TryGetValue(spec.Id, out fight)) return fight;
            fight = new BossFight
            {
                Spec = spec,
                StartRunTime = Run.instance ? Run.instance.GetRunStopwatch() : 0f,
                SceneName = SceneCatalog.mostRecentSceneDef ? SceneCatalog.mostRecentSceneDef.cachedName : "",
            };
            active[spec.Id] = fight;
            Log.Info("Purification '" + spec.Id + "': recording fight on " + fight.SceneName + ".");
            return fight;
        }

        internal static void End(PurificationSpec spec)
        {
            active.Remove(spec.Id);
        }

        private static void OnBodyStart(CharacterBody body)
        {
            if (!NetworkServer.active || !IsHostileBoss(body)) return;
            string name = BodyCatalog.GetBodyName(body.bodyIndex);
            foreach (PurificationSpec spec in PurificationRegistry.Specs)
            {
                if (!spec.IsFightBody(name)) continue;
                StartOrGet(spec).BossBodies.Add(body);
            }
        }

        private static void OnDamageDealt(DamageReport report)
        {
            if (active.Count == 0 || report == null) return;
            foreach (BossFight fight in active.Values)
            {
                PlayerFightRecord victimRecord = IsPlayer(report.victimBody) ? fight.RecordFor(report.victimMaster) : null;
                if (victimRecord != null)
                {
                    PlayerFightRecord record = victimRecord;
                    record.DamageTaken += report.damageDealt;
                    record.HitsTaken++;
                    string source = report.attackerBody != null ? BodyCatalog.GetBodyName(report.attackerBody.bodyIndex) : "World";
                    float sum;
                    record.DamageTakenBySource.TryGetValue(source, out sum);
                    record.DamageTakenBySource[source] = sum + report.damageDealt;
                    HealthComponent health = report.victimBody.healthComponent;
                    if (health != null) record.LowestHealthFraction = Mathf.Min(record.LowestHealthFraction, health.combinedHealthFraction);
                }
                if (report.attackerMaster != null && report.attackerBody != null && IsPlayer(report.attackerBody) && fight.IsBoss(report.victimBody))
                {
                    PlayerFightRecord record = fight.RecordFor(report.attackerMaster);
                    record.DamageDealtToBoss += report.damageDealt;
                    record.HitsOnBoss++;
                }
            }
        }

        private static void OnDeath(DamageReport report)
        {
            if (active.Count == 0 || report == null || report.victimBody == null || !IsPlayer(report.victimBody)) return;
            foreach (BossFight fight in active.Values)
            {
                PlayerFightRecord record = fight.RecordFor(report.victimMaster);
                if (record != null) record.Deaths++;
            }
        }

        private static void OnEquipment(EquipmentSlot slot, EquipmentIndex equipment)
        {
            if (active.Count == 0 || slot == null || slot.characterBody == null || !IsPlayer(slot.characterBody)) return;
            foreach (BossFight fight in active.Values)
            {
                PlayerFightRecord record = fight.RecordFor(slot.characterBody.master);
                if (record != null) record.EquipmentUsed.Add(equipment);
            }
        }

        private static void OnItemGiven(Inventory inventory, ItemIndex item, int count)
        {
            if (active.Count == 0 || inventory == null) return;
            CharacterMaster master = inventory.GetComponent<CharacterMaster>();
            if (master == null || master.playerCharacterMasterController == null) return;
            foreach (BossFight fight in active.Values)
            {
                PlayerFightRecord record = fight.RecordFor(master);
                int n;
                record.ItemsGained.TryGetValue(item, out n);
                record.ItemsGained[item] = n + count;
            }
        }

        internal static bool IsPlayer(CharacterBody body)
        {
            if (body == null) return false;
            CharacterMaster master = body.master;
            return master != null && master.playerCharacterMasterController != null;
        }

        /// <summary>
        /// A body that can count as a boss: not player-controlled and not on the players' team. Allied copies that
        /// share the boss's body prefab (Happiest Mask ghosts) never start or end a fight.
        /// </summary>
        public static bool IsHostileBoss(CharacterBody body)
        {
            if (body == null || IsPlayer(body)) return false;
            TeamComponent team = body.teamComponent;
            return team == null || team.teamIndex != TeamIndex.Player;
        }
    }
}

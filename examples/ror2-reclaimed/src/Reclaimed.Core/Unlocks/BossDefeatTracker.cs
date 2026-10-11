using System;
using System.Collections.Generic;
using System.Text;
using RoR2;
using UnityEngine.Networking;

namespace Reclaimed.Core.Unlocks
{
    /// <summary>
    /// Server: when the last hostile body named in a spec's <see cref="PurificationSpec.BossBodyNames"/> dies (copies
    /// from Artifact of Swarms or multi-boss spawns count as one fight), evaluates that spec for every participating player and raises <see cref="PurificationRegistry.OnPurifiedServer"/> for
    /// each one that passes (immediately, or when the run is won for <see cref="PurificationSpec.RequiresWinningRun"/>).
    /// Every evaluation is logged with the conditions that failed, which is how unlocks are tested.
    /// </summary>
    public static class BossDefeatTracker
    {
        private struct Pending
        {
            public PurificationSpec Spec;
            public CharacterMaster Master;
        }

        private static readonly List<Pending> pending = new List<Pending>();
        private static bool installed;

        internal static void Install()
        {
            if (installed) return;
            installed = true;
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
            Run.onServerGameOver += OnGameOver;
            Run.onRunStartGlobal += run => Reset();
            Run.onRunDestroyGlobal += run => Reset();
        }

        private static void Reset()
        {
            pending.Clear();
            PurificationRegistry.ClearRunFlags();
        }

        private static void OnDeath(DamageReport report)
        {
            if (!NetworkServer.active || report == null || report.victimBody == null) return;
            if (report.victimTeamIndex == TeamIndex.Player || !BossFightRecorder.IsHostileBoss(report.victimBody)) return;
            string bodyName = BodyCatalog.GetBodyName(report.victimBody.bodyIndex);
            foreach (PurificationSpec spec in PurificationRegistry.Specs)
            {
                if (!spec.IsDefeatBody(bodyName)) continue;
                BossFight fight = BossFightRecorder.GetActiveFight(spec.Id);
                if (fight == null)
                {
                    // Never judge a defeat against a fresh, empty record: every record-based condition would pass.
                    Log.Warn("Purification '" + spec.Id + "': " + bodyName + " died with no recorded fight; not evaluated.");
                    continue;
                }
                fight.BossBodies.Remove(report.victimBody);
                if (fight.AnyDefeatBodyAlive(report.victimBody))
                {
                    Log.Info("Purification '" + spec.Id + "': one " + bodyName + " down, another is still alive.");
                    continue;
                }
                try
                {
                    Evaluate(spec, fight, report);
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Purification '" + spec.Id + "' evaluation failed");
                }
                BossFightRecorder.End(spec);
            }
        }

        private static void Evaluate(PurificationSpec spec, BossFight fight, DamageReport report)
        {
            Run run = Run.instance;
            DifficultyIndex difficulty = run ? run.selectedDifficulty : DifficultyIndex.Invalid;
            float runTime = run ? run.GetRunStopwatch() : 0f;

            foreach (PlayerCharacterMasterController pcmc in PlayerCharacterMasterController.instances)
            {
                if (pcmc == null || pcmc.master == null) continue;
                CharacterMaster master = pcmc.master;
                CharacterBody body = master.GetBody();
                PlayerFightRecord record = fight.RecordFor(master);
                var ctx = new PurificationContext
                {
                    Spec = spec,
                    Fight = fight,
                    Record = record,
                    BossBody = report.victimBody,
                    KillReport = report,
                    User = pcmc.networkUser,
                    Master = master,
                    PlayerBody = body,
                    IsAlive = body != null && body.healthComponent != null && body.healthComponent.alive,
                    IsKiller = report.attackerMaster == master,
                    Difficulty = difficulty,
                    DifficultyDef = DifficultyCatalog.GetDifficultyDef(difficulty),
                    SceneName = fight.SceneName,
                    RunTime = runTime,
                    FightDuration = runTime - fight.StartRunTime,
                    StageClearCount = run ? run.stageClearCount : 0,
                };
                string who = pcmc.networkUser != null ? pcmc.networkUser.userName : master.name;

                if (spec.RequireParticipation && !record.DealtDamage && !ctx.IsAlive)
                {
                    Log.Info("Purification '" + spec.Id + "' skipped for " + who + ": did not take part.");
                    continue;
                }

                var failed = new StringBuilder();
                foreach (PurificationCondition condition in spec.Conditions)
                {
                    bool ok;
                    try
                    {
                        ok = condition.Predicate == null || condition.Predicate(ctx);
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Purification '" + spec.Id + "' condition '" + condition.Description + "' threw");
                        ok = false;
                    }
                    if (!ok) failed.Append(failed.Length > 0 ? "; " : "").Append(condition.Description);
                }
                if (failed.Length > 0)
                {
                    Log.Info("Purification '" + spec.Id + "' failed for " + who + ": " + failed);
                    continue;
                }
                if (spec.RequiresWinningRun)
                {
                    pending.Add(new Pending { Spec = spec, Master = master });
                    Log.Info("Purification '" + spec.Id + "' passed for " + who + "; granted if the run is won.");
                    continue;
                }
                PurificationRegistry.RaisePurified(spec, pcmc.networkUser);
            }
        }

        private static void OnGameOver(Run run, GameEndingDef ending)
        {
            if (!NetworkServer.active) return;
            bool won = ending != null && ending.isWin;
            foreach (Pending p in pending)
            {
                if (!won)
                {
                    Log.Info("Purification '" + p.Spec.Id + "' not granted: the run was not won.");
                    continue;
                }
                PlayerCharacterMasterController pcmc = p.Master != null ? p.Master.playerCharacterMasterController : null;
                if (pcmc != null && pcmc.networkUser != null) PurificationRegistry.RaisePurified(p.Spec, pcmc.networkUser);
            }
            pending.Clear();
        }

        /// <summary>Server, debug: grants a spec to a user without a fight (see reclaimed_grant_unlock).</summary>
        internal static void ForceGrant(PurificationSpec spec, NetworkUser user)
        {
            if (!NetworkServer.active || spec == null || user == null) return;
            PurificationRegistry.RaisePurified(spec, user);
        }
    }
}

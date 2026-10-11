using System;
using System.Collections.Generic;
using System.Linq;

namespace Reclaimed.Core.Unlocks
{
    /// <summary>One condition of a purification: a predicate and the sentence players read in the logbook.</summary>
    public sealed class PurificationCondition
    {
        public readonly string Description;
        public readonly Func<PurificationContext, bool> Predicate;

        public PurificationCondition(string description, Func<PurificationContext, bool> predicate)
        {
            Description = description;
            Predicate = predicate;
        }
    }

    /// <summary>
    /// "Defeat this boss under these conditions" — evaluated on the server for every participating player
    /// when one of <see cref="BossBodyNames"/> dies. All conditions must hold.
    /// </summary>
    /// <example>
    /// <code>
    /// var spec = new PurificationSpec("ReclaimedMithrix", "Mithrix", "BrotherHurtBody")
    /// {
    ///     FightStartBodyNames = new[] { "BrotherBody" },
    ///     AchievementName = "Mithrix: Reclaimed",
    /// }
    /// .AddCondition("carry no lunar items", ctx => ctx.LunarItemCount() == 0)
    /// .AddCondition("lose no more than half your health in the fight", ctx => ctx.Record.LowestHealthFraction >= 0.5f);
    /// PurificationRegistry.Register(spec);
    /// </code>
    /// </example>
    public sealed class PurificationSpec
    {
        /// <summary>Stable id, also the survivor's internal name (e.g. "ReclaimedMithrix").</summary>
        public string Id;
        /// <summary>Shown in the generated achievement description (e.g. "Mithrix").</summary>
        public string BossDisplayName;
        /// <summary>Body names whose death counts as the defeat (non-player-controlled only).</summary>
        public string[] BossBodyNames = new string[0];
        /// <summary>Body names whose spawn starts recording the fight; defaults to <see cref="BossBodyNames"/>.</summary>
        public string[] FightStartBodyNames;
        public readonly List<PurificationCondition> Conditions = new List<PurificationCondition>();
        /// <summary>Grant only when the run that contained the defeat ends in a win.</summary>
        public bool RequiresWinningRun;
        /// <summary>Only players that damaged the boss or were alive at the kill qualify.</summary>
        public bool RequireParticipation = true;

        /// <summary>Achievement identifier; defaults to Id + "Purified".</summary>
        public string AchievementId;
        /// <summary>Unlockable this achievement rewards; defaults to "Characters." + Id.</summary>
        public string UnlockableName;
        public string AchievementName;
        /// <summary>Null generates "Defeat {boss} while ... ." from the conditions.</summary>
        public string AchievementDescription;

        public PurificationSpec()
        {
        }

        public PurificationSpec(string id, string bossDisplayName, params string[] bossBodyNames)
        {
            Id = id;
            BossDisplayName = bossDisplayName;
            BossBodyNames = bossBodyNames ?? new string[0];
        }

        public PurificationSpec AddCondition(string description, Func<PurificationContext, bool> predicate)
        {
            Conditions.Add(new PurificationCondition(description, predicate));
            return this;
        }

        public string ResolvedAchievementId { get { return string.IsNullOrEmpty(AchievementId) ? Id + "Purified" : AchievementId; } }
        public string ResolvedUnlockableName { get { return string.IsNullOrEmpty(UnlockableName) ? "Characters." + Id : UnlockableName; } }
        public string[] ResolvedFightStartBodyNames
        {
            get { return FightStartBodyNames != null && FightStartBodyNames.Length > 0 ? FightStartBodyNames : BossBodyNames; }
        }

        public bool IsDefeatBody(string bodyName)
        {
            return bodyName != null && BossBodyNames != null && Array.IndexOf(BossBodyNames, bodyName) >= 0;
        }

        public bool IsFightBody(string bodyName)
        {
            return bodyName != null && (IsDefeatBody(bodyName) || Array.IndexOf(ResolvedFightStartBodyNames, bodyName) >= 0);
        }

        public string DescribeConditions()
        {
            string conditions = string.Join(", ", Conditions.Select(c => c.Description).Where(d => !string.IsNullOrEmpty(d)).ToArray());
            string text = "Defeat " + (BossDisplayName ?? Id);
            if (conditions.Length > 0) text += " while you " + conditions;
            if (RequiresWinningRun) text += ", then win the run";
            return text + ".";
        }
    }
}

using RoR2;
using RoR2.Achievements;

namespace Reclaimed.Core.Unlocks
{
    /// <summary>
    /// Client half of a purification achievement. A survivor declares one tiny subclass:
    /// <code>
    /// [RegisterAchievement("ReclaimedMithrixPurified", "Characters.ReclaimedMithrix", null, 5, typeof(MithrixPurifiedServer))]
    /// public class MithrixPurified : PurificationAchievement { }
    /// public class MithrixPurifiedServer : PurificationServerAchievement { }
    /// </code>
    /// The achievement id must match <see cref="PurificationSpec.ResolvedAchievementId"/> and the unlockable
    /// name an UnlockableDef registered with <see cref="UnlockableFactory"/>. The game finds the class by
    /// scanning assemblies that reference RoR2 (RoR2BepInExPack's SaferAchievementManager); it builds the
    /// ACHIEVEMENT_{ID}_NAME/_DESCRIPTION tokens that <see cref="PurificationRegistry.Register"/> provides.
    /// </summary>
    public abstract class PurificationAchievement : BaseAchievement
    {
        public override void OnInstall()
        {
            base.OnInstall();
            SetServerTracked(true);
        }

        public override void OnUninstall()
        {
            SetServerTracked(false);
            base.OnUninstall();
        }
    }

    /// <summary>
    /// Server half: listens to <see cref="PurificationRegistry.OnPurifiedServer"/> and grants the achievement to
    /// its own NetworkUser. The spec is found from the achievement id, so subclasses need no code; override
    /// <see cref="PurificationId"/> only when the ids do not follow the default naming.
    /// </summary>
    public abstract class PurificationServerAchievement : BaseServerAchievement
    {
        protected virtual string PurificationId
        {
            get
            {
                PurificationSpec spec = achievementDef != null ? PurificationRegistry.FindByAchievement(achievementDef.identifier) : null;
                return spec != null ? spec.Id : null;
            }
        }

        public override void OnInstall()
        {
            base.OnInstall();
            if (PurificationId == null)
                Log.Error("Server achievement " + GetType().Name + " (" + (achievementDef != null ? achievementDef.identifier : "?") +
                          ") matches no registered PurificationSpec.");
            PurificationRegistry.OnPurifiedServer += OnPurified;
        }

        public override void OnUninstall()
        {
            PurificationRegistry.OnPurifiedServer -= OnPurified;
            base.OnUninstall();
        }

        private void OnPurified(PurificationSpec spec, NetworkUser user)
        {
            if (user != null && user == networkUser && spec.Id == PurificationId) Grant();
        }
    }
}

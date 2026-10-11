using R2API;
using RoR2;

namespace Reclaimed.Core.Combat
{
    /// <summary>
    /// Wrappers over R2API.DamageType's modded damage types, which ride along DamageTypeCombo/DamageInfo
    /// and are networked by R2API. Reserve once at load time and keep the returned value in a static.
    /// </summary>
    public static class ReclaimedDamageTypes
    {
        public static DamageAPI.ModdedDamageType Reserve(string debugName)
        {
            DamageAPI.ModdedDamageType type = DamageAPI.ReserveDamageType();
            Log.Debug("Reserved modded damage type " + debugName + " = " + (int)type);
            return type;
        }

        public static void Add(ref DamageTypeCombo combo, DamageAPI.ModdedDamageType type)
        {
            combo.AddModdedDamageType(type);
        }

        public static bool Has(DamageInfo damageInfo, DamageAPI.ModdedDamageType type)
        {
            return damageInfo != null && damageInfo.HasModdedDamageType(type);
        }
    }
}

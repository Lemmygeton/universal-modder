using RoR2;
using UnityEngine;

namespace Reclaimed.Core.Unlocks
{
    /// <summary>Everything a purification condition can look at, for one player at the moment of the defeat.</summary>
    public sealed class PurificationContext
    {
        public PurificationSpec Spec;
        public BossFight Fight;
        /// <summary>Never null: players with no recorded events get an empty record.</summary>
        public PlayerFightRecord Record;
        public CharacterBody BossBody;
        public DamageReport KillReport;

        public NetworkUser User;
        public CharacterMaster Master;
        /// <summary>The player's current body; null when dead.</summary>
        public CharacterBody PlayerBody;
        public bool IsAlive;
        /// <summary>This player landed the killing blow.</summary>
        public bool IsKiller;

        public DifficultyIndex Difficulty;
        public DifficultyDef DifficultyDef;
        public string SceneName;
        public float RunTime;
        public float FightDuration;
        public int StageClearCount;

        public Inventory Inventory { get { return Master != null ? Master.inventory : null; } }

        /// <summary>Stack count of an item by its internal name (e.g. "Hoof"); 0 if unknown or absent.</summary>
        public int ItemCount(string itemName)
        {
            ItemIndex index = ItemCatalog.FindItemIndex(itemName);
            return index == ItemIndex.None || Inventory == null ? 0 : Inventory.GetItemCount(index);
        }

        public bool HasItem(string itemName)
        {
            return ItemCount(itemName) > 0;
        }

        /// <summary>Total stacks of items of a tier (e.g. ItemTier.Lunar) held at the kill.</summary>
        public int ItemCountOfTier(ItemTier tier)
        {
            if (Inventory == null) return 0;
            int total = 0;
            foreach (ItemIndex index in Inventory.itemAcquisitionOrder)
            {
                ItemDef def = ItemCatalog.GetItemDef(index);
                if (def != null && def.tier == tier) total += Inventory.GetItemCount(index);
            }
            return total;
        }

        public int LunarItemCount()
        {
            return ItemCountOfTier(ItemTier.Lunar);
        }

        /// <summary>Held equipment's internal name (active slot), or null.</summary>
        public string EquipmentName
        {
            get
            {
                if (Inventory == null) return null;
                EquipmentDef def = EquipmentCatalog.GetEquipmentDef(Inventory.currentEquipmentIndex);
                return def != null ? def.name : null;
            }
        }

        public bool HasEquipment(string equipmentName)
        {
            return EquipmentName == equipmentName;
        }

        public bool UsedEquipment(string equipmentName)
        {
            EquipmentIndex index = EquipmentCatalog.FindEquipmentIndex(equipmentName);
            return index != EquipmentIndex.None && Record.EquipmentUsed.Contains(index);
        }

        /// <summary>True if the player is (or was, when dead) playing the named body, e.g. "ReclaimedMithrixBody".</summary>
        public bool PlayingBody(string bodyName)
        {
            if (Master == null) return false;
            GameObject prefab = Master.bodyPrefab;
            return prefab != null && prefab.name == bodyName;
        }

        /// <summary>Difficulty scaling (Drizzle 1, Rainstorm 2, Monsoon 3, Eclipse 3+) at least this value.</summary>
        public bool DifficultyAtLeast(float scalingValue)
        {
            return DifficultyDef != null && DifficultyDef.scalingValue >= scalingValue;
        }

        /// <summary>A flag a kit-specific tracker set for this player this run (see <see cref="PurificationRegistry.SetRunFlag"/>).</summary>
        public bool HasRunFlag(string key)
        {
            return PurificationRegistry.GetRunFlag(Master, key) != null;
        }

        public object GetRunFlag(string key)
        {
            return PurificationRegistry.GetRunFlag(Master, key);
        }
    }
}

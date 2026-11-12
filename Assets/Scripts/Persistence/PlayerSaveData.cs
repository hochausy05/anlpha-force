using System;
using System.Collections.Generic;
using Looter.Items;

namespace Looter.Persistence
{
    [Serializable]
    public sealed class SavedItemRecord
    {
        public string itemId;
        public string instanceId;
        public int quantity;
        public int currentDurability;
        public int effectiveMaxDurability;
        public int currentMagazine;

        public bool IsEmpty => string.IsNullOrWhiteSpace(itemId);
        public void Clear()
        {
            itemId = string.Empty;
            instanceId = string.Empty;
            quantity = 0;
            currentDurability = 0;
            effectiveMaxDurability = 0;
            currentMagazine = 0;
        }
        public static SavedItemRecord Empty() => new SavedItemRecord();
        public static SavedItemRecord FromStack(ItemStack stack) => stack == null || stack.Item == null
            ? Empty()
            : new SavedItemRecord
            {
                itemId = stack.Item.Id,
                instanceId = stack.InstanceId,
                quantity = stack.Quantity,
                currentDurability = stack.CurrentDurability,
                effectiveMaxDurability = stack.EffectiveMaxDurability,
                currentMagazine = stack.CurrentMagazine
            };
        public ItemStack ToStack(ItemCatalog catalog)
        {
            ItemDefinition definition = catalog != null ? catalog.Find(itemId) : null;
            if (definition == null || quantity <= 0) return null;
            ItemStack stack = new ItemStack(definition, quantity, instanceId, currentDurability, effectiveMaxDurability);
            stack.SetCurrentMagazine(currentMagazine);
            return stack;
        }
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public int schemaVersion = 1;
        public int currency;
        public bool raidInProgress;
        public string lastRaidSummary;
        public List<SavedItemRecord> stash = new List<SavedItemRecord>();
        public List<SavedItemRecord> carried = new List<SavedItemRecord>();
        public List<SavedItemRecord> safe = new List<SavedItemRecord>();
        // 0: weapon, 1: backpack, 2: drone.
        public List<SavedItemRecord> equipment = new List<SavedItemRecord>();
    }

    public enum PlayerStorage { Stash, Carried, Safe, WeaponEquipment, BackpackEquipment, DroneEquipment }
}

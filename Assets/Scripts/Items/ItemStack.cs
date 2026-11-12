using System;
using UnityEngine;

namespace Looter.Items
{
    [Serializable]
    public sealed class ItemStack
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField] private string instanceId;
        [SerializeField, Min(0)] private int currentDurability;
        [SerializeField, Min(0)] private int effectiveMaxDurability;
        [SerializeField, Min(0)] private int currentMagazine;

        public ItemDefinition Item => item;
        public int Quantity => quantity;
        public int CurrentMagazine => currentMagazine;
        public int CurrentDurability => item != null && item.Type == ItemType.Drone
            ? (currentDurability <= 0 && effectiveMaxDurability <= 0 ? item.BaseMaxDurability : Mathf.Clamp(currentDurability, 0, EffectiveMaxDurability)) : 0;
        public int EffectiveMaxDurability => item != null && item.Type == ItemType.Drone
            ? (effectiveMaxDurability <= 0 ? item.BaseMaxDurability : Mathf.Clamp(effectiveMaxDurability, 1, item.BaseMaxDurability)) : 0;
        public int BaseMaxDurability => item != null && item.Type == ItemType.Drone ? item.BaseMaxDurability : 0;
        public bool IsDroneDisabled => item != null && item.Type == ItemType.Drone && CurrentDurability <= 0;
        public string InstanceId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(instanceId)) instanceId = Guid.NewGuid().ToString("N");
                return instanceId;
            }
        }

        public ItemStack() { }

        public ItemStack(ItemDefinition definition, int count)
        {
            item = definition;
            quantity = Mathf.Max(1, count);
            instanceId = Guid.NewGuid().ToString("N");
        }

        public ItemStack(ItemDefinition definition, int count, string stableInstanceId)
        {
            item = definition;
            quantity = Mathf.Max(1, count);
            instanceId = string.IsNullOrWhiteSpace(stableInstanceId) ? Guid.NewGuid().ToString("N") : stableInstanceId;
            if (definition != null && definition.Type == ItemType.Drone)
            {
                currentDurability = definition.BaseMaxDurability;
                effectiveMaxDurability = definition.BaseMaxDurability;
            }
        }

        public ItemStack(ItemDefinition definition, int count, string stableInstanceId,
            int savedCurrentDurability, int savedEffectiveMaxDurability) : this(definition, count, stableInstanceId)
        {
            if (definition == null || definition.Type != ItemType.Drone) return;
            effectiveMaxDurability = savedEffectiveMaxDurability <= 0
                ? definition.BaseMaxDurability
                : Mathf.Clamp(savedEffectiveMaxDurability, 1, definition.BaseMaxDurability);
            currentDurability = savedCurrentDurability <= 0 && savedEffectiveMaxDurability <= 0
                ? effectiveMaxDurability
                : Mathf.Clamp(savedCurrentDurability, 0, effectiveMaxDurability);
        }

        public ItemStack Copy()
        {
            ItemStack copy = new ItemStack(item, quantity, InstanceId, currentDurability, effectiveMaxDurability);
            copy.currentMagazine = currentMagazine;
            return copy;
        }

        internal void SetQuantity(int count) => quantity = Mathf.Max(0, count);

        public int ApplyDurabilityDamage(int amount)
        {
            if (item == null || item.Type != ItemType.Drone || amount <= 0 || CurrentDurability <= 0) return 0;
            int absorbed = Mathf.Min(amount, CurrentDurability);
            currentDurability = CurrentDurability - absorbed;
            effectiveMaxDurability = EffectiveMaxDurability;
            return absorbed;
        }

        public void SetDroneDurability(int current, int effectiveMax)
        {
            if (item == null || item.Type != ItemType.Drone) return;
            effectiveMaxDurability = Mathf.Clamp(effectiveMax, 1, item.BaseMaxDurability);
            currentDurability = Mathf.Clamp(current, 0, effectiveMaxDurability);
        }

        public void SetCurrentMagazine(int amount) => currentMagazine = Mathf.Max(0, amount);
    }
}

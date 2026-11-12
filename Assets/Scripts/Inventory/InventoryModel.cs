using System;
using System.Collections.Generic;
using Looter.Combat;
using Looter.Items;
using UnityEngine;

namespace Looter.Inventory
{
    public enum InventoryStorage { Backpack, Safe }

    public sealed class InventoryModel : MonoBehaviour
    {
        [SerializeField, Min(1)] private int backpackCapacity = 10;
        [SerializeField, Min(1)] private int safeSlotCapacity = 2;
        [SerializeField] private List<ItemStack> backpack = new List<ItemStack>();
        [SerializeField] private List<ItemStack> safeSlots = new List<ItemStack>();

        public int BackpackCapacity => backpackCapacity;
        public int SafeSlotCapacity => safeSlotCapacity;
        public event Action Changed;

        public int SlotCount(InventoryStorage storage) => GetSlots(storage).Count;

        private void Awake()
        {
            EnsureSlotCounts();
            SplitLegacyNonStackableStacks(backpack);
            SplitLegacyNonStackableStacks(safeSlots);
        }

        public ItemStack GetStack(InventoryStorage storage, int index)
        {
            List<ItemStack> slots = GetSlots(storage);
            return index >= 0 && index < slots.Count ? slots[index] : null;
        }

        public List<ItemStack> Snapshot(InventoryStorage storage)
        {
            List<ItemStack> copy = new List<ItemStack>();
            foreach (ItemStack stack in GetSlots(storage))
                if (stack != null && stack.Item != null)
                    copy.Add(stack.Copy());
            return copy;
        }

        public bool TrySetBackpackCapacity(int capacity)
        {
            capacity = Mathf.Clamp(capacity, 1, 30);
            if (capacity < backpack.Count)
                for (int i = capacity; i < backpack.Count; i++)
                    if (backpack[i] != null) return false;
            if (backpackCapacity == capacity) return true;
            backpackCapacity = capacity;
            Resize(backpack, backpackCapacity);
            Changed?.Invoke();
            return true;
        }

        public bool ReplaceContents(IReadOnlyList<ItemStack> backpackContents,
            IReadOnlyList<ItemStack> safeContents, int capacity)
        {
            capacity = Mathf.Clamp(capacity, 1, 30);
            if (backpackContents == null || safeContents == null || backpackContents.Count > capacity ||
                safeContents.Count > safeSlotCapacity) return false;
            for (int i = 0; i < backpackContents.Count; i++)
            {
                ItemStack stack = backpackContents[i];
                if (stack != null && (stack.Item == null ||
                    (!stack.Item.IsStackable && stack.Quantity != 1) ||
                    (stack.Item.IsStackable && stack.Quantity > stack.Item.MaxStackSize))) return false;
            }
            foreach (ItemStack stack in safeContents)
                if (stack != null && (stack.Item == null || !stack.Item.CanStoreInSafeSlot || !stack.Item.IsStackable && stack.Quantity != 1))
                    return false;

            backpackCapacity = capacity;
            Resize(backpack, backpackCapacity);
            Resize(safeSlots, safeSlotCapacity);
            for (int i = 0; i < backpack.Count; i++)
                backpack[i] = i < backpackContents.Count ? backpackContents[i] : null;
            for (int i = 0; i < safeSlots.Count; i++)
                safeSlots[i] = i < safeContents.Count ? safeContents[i] : null;
            Changed?.Invoke();
            return true;
        }

        public void ClearStorage(InventoryStorage storage)
        {
            List<ItemStack> slots = GetSlots(storage);
            bool changed = false;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;
                slots[i] = null;
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        public bool TryAdd(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0) return false;
            EnsureSlotCounts();

            if (!item.IsStackable)
            {
                int emptySlots = 0;
                foreach (ItemStack stack in backpack) if (stack == null) emptySlots++;
                if (emptySlots < quantity) return false;

                int remainingItems = quantity;
                for (int i = 0; i < backpack.Count && remainingItems > 0; i++)
                {
                    if (backpack[i] != null) continue;
                    backpack[i] = new ItemStack(item, 1);
                    remainingItems--;
                }
                Changed?.Invoke();
                return true;
            }

            long available = 0;
            foreach (ItemStack stack in backpack)
                if (IsSameItem(stack, item)) available += Mathf.Max(0, item.MaxStackSize - stack.Quantity);
                else if (stack == null) available += item.MaxStackSize;
            if (available < quantity) return false;

            int remaining = quantity;
            for (int i = 0; i < backpack.Count && remaining > 0; i++)
            {
                ItemStack stack = backpack[i];
                if (!IsSameItem(stack, item) || stack.Quantity >= item.MaxStackSize) continue;
                int added = Mathf.Min(remaining, item.MaxStackSize - stack.Quantity);
                stack.SetQuantity(stack.Quantity + added);
                remaining -= added;
            }

            for (int i = 0; i < backpack.Count && remaining > 0; i++)
            {
                if (backpack[i] != null) continue;
                int added = Mathf.Min(remaining, item.MaxStackSize);
                backpack[i] = new ItemStack(item, added);
                remaining -= added;
            }

            Changed?.Invoke();
            return remaining == 0;
        }

        public bool TryRemove(InventoryStorage storage, int index, out ItemStack removed)
        {
            List<ItemStack> slots = GetSlots(storage);
            removed = null;
            if (index < 0 || index >= slots.Count || slots[index] == null) return false;
            ItemStack source = slots[index];
            removed = source.Copy();
            slots[index] = null;
            Changed?.Invoke();
            return true;
        }

        public bool TryConsume(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0 || !item.IsStackable) return false;
            int available = 0;
            foreach (ItemStack stack in backpack)
                if (stack != null && stack.Item == item) available += stack.Quantity;
            if (available < quantity) return false;
            int remaining = quantity;
            for (int i = 0; i < backpack.Count && remaining > 0; i++)
            {
                ItemStack stack = backpack[i];
                if (stack == null || stack.Item != item) continue;
                int consumed = Mathf.Min(remaining, stack.Quantity);
                if (consumed == stack.Quantity) backpack[i] = null;
                else stack.SetQuantity(stack.Quantity - consumed);
                remaining -= consumed;
            }
            Changed?.Invoke();
            return remaining == 0;
        }

        public bool TryRepairDroneWithKit(ItemStack drone, DroneSystemSettings settings, out string reason, bool notify = true)
        {
            reason = null;
            if (settings == null || settings.RepairKit == null)
            { reason = "Chưa cấu hình Repair Kit."; return false; }
            if (!DroneRepairService.TryCalculateRepairKit(drone, settings, out DroneRepairResult repair, out reason)) return false;

            int kitIndex = -1;
            for (int i = 0; i < backpack.Count; i++)
            {
                ItemStack candidate = backpack[i];
                if (candidate != null && candidate.Item == settings.RepairKit && candidate.Quantity > 0)
                { kitIndex = i; break; }
            }
            if (kitIndex < 0) { reason = "Không có Repair Kit trong balo."; return false; }

            ItemStack kit = backpack[kitIndex];
            if (kit.Quantity == 1) backpack[kitIndex] = null;
            else kit.SetQuantity(kit.Quantity - 1);
            drone.SetDroneDurability(repair.CurrentDurability, repair.EffectiveMaxDurability);
            if (notify) Changed?.Invoke();
            return true;
        }

        public bool HasRepairKit(DroneSystemSettings settings)
        {
            if (settings == null || settings.RepairKit == null) return false;
            foreach (ItemStack stack in backpack)
                if (stack != null && stack.Item == settings.RepairKit && stack.Quantity > 0) return true;
            return false;
        }

        public bool TryTransfer(InventoryStorage sourceStorage, int sourceIndex,
            InventoryStorage destinationStorage, int destinationIndex)
        {
            List<ItemStack> sourceSlots = GetSlots(sourceStorage);
            List<ItemStack> destinationSlots = GetSlots(destinationStorage);
            if (sourceIndex < 0 || sourceIndex >= sourceSlots.Count ||
                destinationIndex < 0 || destinationIndex >= destinationSlots.Count ||
                (sourceStorage == destinationStorage && sourceIndex == destinationIndex)) return false;

            ItemStack source = sourceSlots[sourceIndex];
            ItemStack destination = destinationSlots[destinationIndex];
            if (source == null || source.Item == null || !CanStore(destinationStorage, source.Item)) return false;

            if (destination == null)
            {
                destinationSlots[destinationIndex] = source;
                sourceSlots[sourceIndex] = null;
                Changed?.Invoke();
                return true;
            }

            if (destination.Item == null) return false;
            bool matchingStackableItems = destination.Item.Id == source.Item.Id &&
                source.Item.IsStackable && destination.Item.IsStackable;
            if (matchingStackableItems)
            {
                int transferred = Mathf.Min(source.Quantity, destination.Item.MaxStackSize - destination.Quantity);
                if (transferred > 0)
                {
                    destination.SetQuantity(destination.Quantity + transferred);
                    if (transferred == source.Quantity) sourceSlots[sourceIndex] = null;
                    else source.SetQuantity(source.Quantity - transferred);
                    Changed?.Invoke();
                    return true;
                }
            }

            if (!CanStore(sourceStorage, destination.Item)) return false;

            sourceSlots[sourceIndex] = destination;
            destinationSlots[destinationIndex] = source;
            Changed?.Invoke();
            return true;
        }

        public bool CanStore(InventoryStorage storage, ItemDefinition item)
        {
            return storage == InventoryStorage.Backpack || (item != null && item.CanStoreInSafeSlot);
        }

        private List<ItemStack> GetSlots(InventoryStorage storage) =>
            storage == InventoryStorage.Safe ? safeSlots : backpack;

        private void EnsureSlotCounts()
        {
            backpackCapacity = Mathf.Max(1, backpackCapacity);
            safeSlotCapacity = Mathf.Max(1, safeSlotCapacity);
            Resize(backpack, backpackCapacity);
            Resize(safeSlots, safeSlotCapacity);
        }

        private static void Resize(List<ItemStack> slots, int size)
        {
            while (slots.Count < size) slots.Add(null);
            if (slots.Count > size) slots.RemoveRange(size, slots.Count - size);
        }

        private static bool IsSameItem(ItemStack stack, ItemDefinition item) =>
            stack != null && stack.Item != null && stack.Item.Id == item.Id;

        private static void SplitLegacyNonStackableStacks(List<ItemStack> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                ItemStack stack = slots[i];
                if (stack == null || stack.Item == null || stack.Item.IsStackable || stack.Quantity <= 1) continue;

                int requiredSlots = stack.Quantity - 1;
                int emptySlots = 0;
                foreach (ItemStack candidate in slots) if (candidate == null) emptySlots++;
                if (emptySlots < requiredSlots) continue;

                ItemDefinition item = stack.Item;
                int remaining = requiredSlots;
                stack.SetQuantity(1);
                for (int slotIndex = 0; slotIndex < slots.Count && remaining > 0; slotIndex++)
                {
                    if (slots[slotIndex] != null) continue;
                    slots[slotIndex] = new ItemStack(item, 1);
                    remaining--;
                }
            }
        }

        private void OnValidate()
        {
            backpackCapacity = Mathf.Max(1, backpackCapacity);
            safeSlotCapacity = Mathf.Max(1, safeSlotCapacity);
        }
    }
}

using System;
using System.Collections.Generic;
using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    [Serializable]
    public struct EnemyItemOption
    {
        public ItemDefinition Item;
        [Min(0f)] public float Weight;
    }

    /// <summary>Rolls a bot's equipment and carried loot once when that bot spawns.</summary>
    public sealed class EnemyInventory : MonoBehaviour
    {
        [SerializeField] private EnemyItemOption[] weaponOptions = Array.Empty<EnemyItemOption>();
        [SerializeField] private EnemyItemOption[] backpackOptions = Array.Empty<EnemyItemOption>();
        [SerializeField] private EnemyItemOption[] droneOptions = Array.Empty<EnemyItemOption>();
        [SerializeField, Range(0f, 1f)] private float weaponChance = 0.45f;
        [SerializeField, Range(0f, 1f)] private float backpackChance = 0.4f;
        [SerializeField, Range(0f, 1f)] private float droneChance = 0.08f;
        [SerializeField] private LootTableDefinition inventoryLootTable;
        [SerializeField] private ItemDefinition fixedWeapon;

        private ItemDefinition equippedWeapon;
        private ItemDefinition equippedBackpack;
        private ItemDefinition equippedDrone;
        private List<ItemStack> inventoryItems = new List<ItemStack>();
        private bool generated;
        private bool transferredToCorpse;

        public ItemDefinition EquippedWeapon => equippedWeapon;
        public ItemDefinition EquippedBackpack => equippedBackpack;
        public ItemDefinition EquippedDrone => equippedDrone;
        public IReadOnlyList<ItemStack> InventoryItems => inventoryItems;
        public LootTableDefinition InventoryLootTable { get => inventoryLootTable; set => inventoryLootTable = value; }

        public bool TryConsumeAmmunition(ItemDefinition ammunition, int quantity)
        {
            if (ammunition == null || quantity <= 0 || inventoryItems == null) return false;
            int available = 0;
            foreach (ItemStack stack in inventoryItems)
                if (stack != null && stack.Item == ammunition) available += stack.Quantity;
            if (available < quantity) return false;

            int remaining = quantity;
            for (int i = inventoryItems.Count - 1; i >= 0 && remaining > 0; i--)
            {
                ItemStack stack = inventoryItems[i];
                if (stack == null || stack.Item != ammunition) continue;
                int take = Mathf.Min(remaining, stack.Quantity);
                stack.SetQuantity(stack.Quantity - take);
                remaining -= take;
                if (stack.Quantity <= 0) inventoryItems.RemoveAt(i);
            }
            return remaining == 0;
        }

        public void AddCarriedItem(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0) return;
            GenerateLoadout();
            if (inventoryItems == null) inventoryItems = new List<ItemStack>();
            if (item.IsStackable)
            {
                int remaining = quantity;
                foreach (ItemStack stack in inventoryItems)
                {
                    if (stack == null || stack.Item != item || stack.Quantity >= item.MaxStackSize) continue;
                    int add = Mathf.Min(remaining, item.MaxStackSize - stack.Quantity);
                    stack.SetQuantity(stack.Quantity + add);
                    remaining -= add;
                    if (remaining == 0) return;
                }
                while (remaining > 0)
                {
                    int amount = Mathf.Min(remaining, item.MaxStackSize);
                    inventoryItems.Add(new ItemStack(item, amount));
                    remaining -= amount;
                }
                return;
            }
            for (int i = 0; i < quantity; i++) inventoryItems.Add(new ItemStack(item, 1));
        }

        public void SetGuaranteedWeapon(ItemDefinition weapon)
        {
            if (weapon == null) return;
            GenerateLoadout();
            equippedWeapon = weapon;
        }

        private void Awake() => GenerateLoadout();

        public void Configure(EnemyItemOption[] weapons, EnemyItemOption[] backpacks,
            EnemyItemOption[] drones, LootTableDefinition lootTable, float weaponRollChance = 0.45f,
            float backpackRollChance = 0.4f, float droneRollChance = 0.08f, ItemDefinition guaranteedWeapon = null)
        {
            weaponOptions = weapons ?? Array.Empty<EnemyItemOption>();
            backpackOptions = backpacks ?? Array.Empty<EnemyItemOption>();
            droneOptions = drones ?? Array.Empty<EnemyItemOption>();
            inventoryLootTable = lootTable;
            weaponChance = Mathf.Clamp01(weaponRollChance);
            backpackChance = Mathf.Clamp01(backpackRollChance);
            droneChance = Mathf.Clamp01(droneRollChance);
            fixedWeapon = guaranteedWeapon;
        }

        public List<ItemStack> TakeForCorpse()
        {
            GenerateLoadout();
            if (transferredToCorpse) return new List<ItemStack>();

            List<ItemStack> result = new List<ItemStack>();
            AddEquipment(result, equippedWeapon);
            AddEquipment(result, equippedBackpack);
            AddEquipment(result, equippedDrone);
            if (inventoryItems != null)
                foreach (ItemStack stack in inventoryItems)
                    if (stack != null && stack.Item != null)
                        result.Add(stack.Copy());

            transferredToCorpse = true;
            return result;
        }

        public void ResetForNewRaid()
        {
            generated = false;
            transferredToCorpse = false;
            equippedWeapon = null;
            equippedBackpack = null;
            equippedDrone = null;
            inventoryItems = new List<ItemStack>();
            GenerateLoadout();
        }

        private void GenerateLoadout()
        {
            if (generated) return;
            generated = true;
            equippedWeapon = RollOptional(weaponOptions, weaponChance);
            if (fixedWeapon != null) equippedWeapon = fixedWeapon;
            equippedBackpack = RollOptional(backpackOptions, backpackChance);
            equippedDrone = RollOptional(droneOptions, droneChance);
            inventoryItems = inventoryLootTable != null
                ? inventoryLootTable.GenerateLoot()
                : new List<ItemStack>();
        }

        private static ItemDefinition RollOptional(EnemyItemOption[] options, float chance)
        {
            if (options == null || options.Length == 0 || UnityEngine.Random.value >= chance) return null;
            float totalWeight = 0f;
            foreach (EnemyItemOption option in options)
                if (option.Item != null) totalWeight += Mathf.Max(0f, option.Weight);
            if (totalWeight <= 0f) return null;

            float roll = UnityEngine.Random.value * totalWeight;
            foreach (EnemyItemOption option in options)
            {
                if (option.Item == null || option.Weight <= 0f) continue;
                roll -= option.Weight;
                if (roll <= 0f) return option.Item;
            }
            for (int i = options.Length - 1; i >= 0; i--)
                if (options[i].Item != null && options[i].Weight > 0f) return options[i].Item;
            return null;
        }

        private static void AddEquipment(List<ItemStack> destination, ItemDefinition item)
        {
            if (item != null) destination.Add(new ItemStack(item, 1));
        }
    }
}

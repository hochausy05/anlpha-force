using System;
using System.Collections.Generic;
using UnityEngine;

namespace Looter.Items
{
    [Serializable]
    public sealed class LootTableEntry
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(0f)] private float weight = 1f;
        [SerializeField, Min(1)] private int minimumQuantity = 1;
        [SerializeField, Min(1)] private int maximumQuantity = 1;

        public ItemDefinition Item => item;
        public float Weight => Mathf.Max(0f, weight);
        public int MinimumQuantity => Mathf.Max(1, minimumQuantity);
        public int MaximumQuantity => Mathf.Max(MinimumQuantity, maximumQuantity);
    }

    [CreateAssetMenu(fileName = "NewLootTable", menuName = "Pixel Looter/Loot Table")]
    public sealed class LootTableDefinition : ScriptableObject
    {
        [SerializeField] private List<LootTableEntry> entries = new List<LootTableEntry>();
        [SerializeField, Min(0)] private int minimumDrops = 1;
        [SerializeField, Min(1)] private int maximumDrops = 1;
        [SerializeField] private ItemDefinition guaranteedItem;
        [SerializeField, Min(1)] private int guaranteedQuantity = 1;

        public ItemDefinition GuaranteedItem => guaranteedItem;

        public List<ItemStack> GenerateLoot()
        {
            List<ItemStack> result = new List<ItemStack>();
            if (guaranteedItem != null) result.Add(new ItemStack(guaranteedItem, Mathf.Max(1, guaranteedQuantity)));
            if (entries == null || entries.Count == 0) return result;

            int dropCount = UnityEngine.Random.Range(Mathf.Max(0, minimumDrops), Mathf.Max(minimumDrops, maximumDrops) + 1);
            for (int i = 0; i < dropCount; i++)
            {
                LootTableEntry entry = RollEntry();
                if (entry == null || entry.Item == null) continue;
                int quantity = UnityEngine.Random.Range(entry.MinimumQuantity, entry.MaximumQuantity + 1);
                result.Add(new ItemStack(entry.Item, quantity));
            }
            return result;
        }

        private LootTableEntry RollEntry()
        {
            float totalWeight = 0f;
            foreach (LootTableEntry entry in entries)
                if (entry != null && entry.Item != null) totalWeight += entry.Weight;
            if (totalWeight <= 0f) return null;

            float roll = UnityEngine.Random.value * totalWeight;
            foreach (LootTableEntry entry in entries)
            {
                if (entry == null || entry.Item == null || entry.Weight <= 0f) continue;
                roll -= entry.Weight;
                if (roll <= 0f) return entry;
            }
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i] != null && entries[i].Item != null && entries[i].Weight > 0f) return entries[i];
            return null;
        }

        private void OnValidate()
        {
            minimumDrops = Mathf.Max(0, minimumDrops);
            maximumDrops = Mathf.Max(minimumDrops, maximumDrops);
            guaranteedQuantity = Mathf.Max(1, guaranteedQuantity);
        }
    }
}

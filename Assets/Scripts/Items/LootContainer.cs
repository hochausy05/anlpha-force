using System.Collections.Generic;
using Looter.Inventory;
using UnityEngine;

namespace Looter.Items
{
    public enum LootContainerType { WoodenBox, WoodenCrate, CommonChest, EnemyCorpse, EliteEnemyCorpse, BossCorpse, BossChest }

    public sealed class LootContainer : MonoBehaviour
    {
        [SerializeField] private LootContainerType containerType;
        [SerializeField] private LootTableDefinition lootTable;
        [SerializeField] private List<ItemStack> contents = new List<ItemStack>();
        [SerializeField] private bool lootGenerated;
        [SerializeField] private bool locked;
        [SerializeField] private string lockedPrompt = "Rương đang bị khóa.";

        public string DisplayName => containerType switch
        {
            LootContainerType.WoodenBox => "Hộp gỗ",
            LootContainerType.WoodenCrate => "Thùng gỗ",
            LootContainerType.CommonChest => "Rương thường",
            LootContainerType.EnemyCorpse => "Hòm xác — Bot thường",
            LootContainerType.EliteEnemyCorpse => "Hòm xác — Bot tinh nhuệ",
            LootContainerType.BossCorpse => "Hòm xác — Boss",
            LootContainerType.BossChest => "Rương hiếm của Boss",
            _ => "Thùng đồ"
        };
        public string Prompt => containerType == LootContainerType.EnemyCorpse || containerType == LootContainerType.EliteEnemyCorpse
            ? "Nhấn E để lục xác"
            : $"Nhấn E để mở {DisplayName}";
        public IReadOnlyList<ItemStack> Contents => contents;
        public LootTableDefinition LootTable { get => lootTable; set => lootTable = value; }
        public LootContainerType Type { get => containerType; set => containerType = value; }
        public bool LootGenerated => lootGenerated;
        public bool IsLocked => locked;
        public bool IsCorpse => containerType == LootContainerType.EnemyCorpse || containerType == LootContainerType.EliteEnemyCorpse ||
            containerType == LootContainerType.BossCorpse;

        public void ResetForNewRaid()
        {
            if (IsCorpse) return;
            contents = new List<ItemStack>();
            lootGenerated = false;
        }

        public void InitializeCorpse(bool elite, Sprite corpseSprite, LootTableDefinition table, List<ItemStack> corpseContents)
        {
            containerType = elite ? LootContainerType.EliteEnemyCorpse : LootContainerType.EnemyCorpse;
            lootTable = table;
            contents = corpseContents ?? new List<ItemStack>();
            lootGenerated = true;
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = corpseSprite;
                renderer.color = Color.white;
                renderer.sortingOrder = 1;
            }
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                collider.isTrigger = true;
                collider.size = new Vector2(1f, 0.8f);
            }
        }

        public void InitializeBossCorpse(Sprite corpseSprite, LootTableDefinition table, List<ItemStack> corpseContents)
        {
            containerType = LootContainerType.BossCorpse;
            lootTable = table;
            contents = corpseContents ?? new List<ItemStack>();
            lootGenerated = true;
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer != null) { renderer.sprite = corpseSprite; renderer.color = Color.white; renderer.sortingOrder = 2; }
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            if (collider != null) { collider.isTrigger = true; collider.size = new Vector2(1.3f, 1f); }
        }

        public void Open(InventoryPresenter presenter)
        {
            if (locked)
            {
                if (presenter != null) presenter.ShowMessage(lockedPrompt);
                return;
            }
            if (contents == null) contents = new List<ItemStack>();
            if (!lootGenerated)
            {
                contents = lootTable != null ? lootTable.GenerateLoot() : new List<ItemStack>();
                lootGenerated = true;
            }
            if (presenter != null) presenter.OpenContainer(this);
        }

        public void SetLocked(bool value, string message = null)
        {
            locked = value;
            if (!string.IsNullOrWhiteSpace(message)) lockedPrompt = message;
        }

        public bool TryTake(int index, InventoryModel inventory)
        {
            if (contents == null) contents = new List<ItemStack>();
            if (inventory == null || index < 0 || index >= contents.Count) return false;
            ItemStack stack = contents[index];
            if (stack == null || stack.Item == null) return false;
            if (!inventory.TryAdd(stack.Item, stack.Quantity)) return false;
            contents[index] = null;
            return true;
        }
    }
}

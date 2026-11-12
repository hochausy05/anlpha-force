using System;
using System.Collections.Generic;
using Looter.Combat;
using Looter.Inventory;
using Looter.Items;
using Looter.Persistence;
using UnityEditor;
using UnityEngine;

namespace Looter.EditorTools
{
    public static class WeaponSystemSelfTest
    {
        [MenuItem("Pixel Looter/Chạy tự kiểm tra vũ khí & đạn (không save thật)")]
        public static void Run()
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Data/Catalogs/ItemCatalog.asset");
            ItemDefinition arrows = catalog != null ? catalog.Find("arrow") : null;
            // Installer item IDs preserve Roman numeral casing (for example, "pistol_III").
            ItemDefinition pistol = catalog != null ? catalog.Find("pistol_III") : null;
            ItemDefinition bullets = catalog != null ? catalog.Find("pistol_ammo") : null;
            if (arrows == null || pistol == null || bullets == null)
            {
                Debug.LogError("[PixelLooter][WeaponSelfTest] CẦN CHẠY INSTALLER: thiếu ammo hoặc dữ liệu vũ khí.");
                return;
            }

            List<string> failures = new List<string>();
            GameObject temporary = new GameObject("WeaponSelfTest — transient");
            temporary.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                InventoryModel inventory = temporary.AddComponent<InventoryModel>();
                List<ItemStack> stacks = new List<ItemStack> { new ItemStack(arrows, 40), new ItemStack(arrows, 30) };
                if (!inventory.ReplaceContents(stacks, Array.Empty<ItemStack>(), 10)) failures.Add("Khởi tạo inventory thử nghiệm thất bại.");
                string firstId = inventory.GetStack(InventoryStorage.Backpack, 0).InstanceId;
                string secondId = inventory.GetStack(InventoryStorage.Backpack, 1).InstanceId;
                int totalBefore = Count(inventory, arrows);
                if (!inventory.TryTransfer(InventoryStorage.Backpack, 1, InventoryStorage.Backpack, 0))
                    failures.Add("Không thể gộp một phần stack ammo.");
                if (inventory.GetStack(InventoryStorage.Backpack, 0)?.Quantity != 60 ||
                    inventory.GetStack(InventoryStorage.Backpack, 1)?.Quantity != 10 || Count(inventory, arrows) != totalBefore)
                    failures.Add("Gộp stack không giữ đúng 60 + 10 hoặc làm thay đổi tổng số lượng.");
                if (inventory.GetStack(InventoryStorage.Backpack, 0)?.InstanceId != firstId ||
                    inventory.GetStack(InventoryStorage.Backpack, 1)?.InstanceId != secondId)
                    failures.Add("Gộp một phần làm thay đổi Instance ID của stack.");
                if (!AmmoService.TryConsume(inventory, arrows, 11) || Count(inventory, arrows) != 59)
                    failures.Add("Tiêu hao 11 mũi tên không cho kết quả còn 59.");

                inventory.ReplaceContents(new List<ItemStack> { new ItemStack(arrows, 4), new ItemStack(bullets, 5) },
                    Array.Empty<ItemStack>(), 10);
                if (!inventory.TryTransfer(InventoryStorage.Backpack, 1, InventoryStorage.Backpack, 0) ||
                    inventory.GetStack(InventoryStorage.Backpack, 0)?.Item != bullets ||
                    inventory.GetStack(InventoryStorage.Backpack, 1)?.Item != arrows ||
                    Count(inventory, arrows) != 4 || Count(inventory, bullets) != 5)
                    failures.Add("Mũi tên và đạn súng lục bị gộp sai loại hoặc mất số lượng.");

                ItemStack gun = new ItemStack(pistol, 1);
                gun.SetCurrentMagazine(6);
                SavedItemRecord record = JsonUtility.FromJson<SavedItemRecord>(JsonUtility.ToJson(SavedItemRecord.FromStack(gun)));
                ItemStack restored = record.ToStack(catalog);
                if (restored == null || restored.CurrentMagazine != 6 || restored.InstanceId != gun.InstanceId)
                    failures.Add("Dữ liệu băng đạn hoặc Instance ID không round-trip qua save record.");
                if (!arrows.IsStackable || arrows.MaxStackSize != 60)
                    failures.Add("Cấu hình mũi tên không stack tối đa 60.");
                if (bullets == null || !bullets.IsStackable || bullets.MaxStackSize != 60 || bullets == arrows)
                    failures.Add("Cấu hình đạn súng lục không stack độc lập tối đa 60.");
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }

            if (failures.Count == 0) Debug.Log("[PixelLooter][WeaponSelfTest] ĐẠT — stack, consume, tách biệt loại ammo, catalog và magazine JSON; không dùng save người chơi.");
            else Debug.LogError("[PixelLooter][WeaponSelfTest] THẤT BẠI\n- " + string.Join("\n- ", failures));
        }

        private static int Count(InventoryModel inventory, ItemDefinition item)
        {
            int result = 0;
            for (int i = 0; i < inventory.SlotCount(InventoryStorage.Backpack); i++)
            {
                ItemStack stack = inventory.GetStack(InventoryStorage.Backpack, i);
                if (stack != null && stack.Item == item) result += stack.Quantity;
            }
            return result;
        }
    }
}

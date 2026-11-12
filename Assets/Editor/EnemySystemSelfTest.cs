using System.Collections.Generic;
using Looter.Combat;
using Looter.Items;
using UnityEditor;
using UnityEngine;

namespace Looter.EditorTools
{
    public static class EnemySystemSelfTest
    {
        [MenuItem("Pixel Looter/Chạy tự kiểm tra Enemy & Boss (không save thật)")]
        public static void Run()
        {
            EnemyDefinition slime = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Slime.asset");
            EnemyDefinition raider = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Raider.asset");
            EnemyDefinition archer = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/Archer.asset");
            EnemyDefinition elite = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/EliteGuard.asset");
            BossDefinition boss = AssetDatabase.LoadAssetAtPath<BossDefinition>("Assets/Data/Enemies/RuinsWarden.asset");
            LootTableDefinition chestLoot = AssetDatabase.LoadAssetAtPath<LootTableDefinition>("Assets/Data/LootTables/BossChestLootTable.asset");
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Data/Catalogs/ItemCatalog.asset");
            ItemDefinition ammo = catalog != null ? catalog.Find("arrow") : null;
            ItemDefinition weapon = catalog != null ? catalog.Find("bot_knife_i") : null;
            if (slime == null || raider == null || archer == null || elite == null || boss == null || chestLoot == null || ammo == null || weapon == null)
            {
                Debug.LogError("[PixelLooter][EnemySelfTest] CẦN CHẠY INSTALLER M1.8 TRƯỚC.");
                return;
            }

            List<string> failures = new List<string>();
            if (slime.MaximumHealth != 40 || raider.MaximumHealth != 65 || archer.MaximumHealth != 50 || elite.MaximumHealth != 150)
                failures.Add("HP archetype không khớp cấu hình M1.8.");
            if (boss.MaximumHealth != 400 || !BossController.ShouldEnterPhaseTwo(160, 400, 0.4f) ||
                BossController.ShouldEnterPhaseTwo(161, 400, 0.4f))
                failures.Add("Ngưỡng phase boss 40% không chính xác.");
            if (chestLoot.GuaranteedItem == null || chestLoot.GuaranteedItem.Tier < ItemTier.IV)
                failures.Add("Boss Chest không bảo đảm vật phẩm tối thiểu bậc IV.");

            GameObject temporary = new GameObject("EnemySelfTest — transient");
            temporary.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                EnemyInventory inventory = temporary.AddComponent<EnemyInventory>();
                inventory.Configure(new[] { new EnemyItemOption { Item = weapon, Weight = 1f } },
                    null, null, null, 1f, 0f, 0f);
                inventory.SetGuaranteedWeapon(weapon);
                inventory.AddCarriedItem(ammo, 12);
                List<ItemStack> firstCorpse = inventory.TakeForCorpse();
                List<ItemStack> secondCorpse = inventory.TakeForCorpse();
                bool hasWeapon = false;
                int arrowCount = 0;
                foreach (ItemStack stack in firstCorpse)
                {
                    if (stack == null) continue;
                    if (stack.Item == weapon) hasWeapon = true;
                    if (stack.Item == ammo) arrowCount += stack.Quantity;
                }
                if (!hasWeapon || arrowCount != 12) failures.Add("Trang bị/ammo bot không được chuyển đúng vào hòm xác.");
                if (secondCorpse.Count != 0) failures.Add("Cùng một EnemyInventory chuyển loot vào hòm xác nhiều lần.");

                GameObject chestObject = new GameObject("BossChest SelfTest — transient");
                chestObject.hideFlags = HideFlags.HideAndDontSave;
                LootContainer chest = chestObject.AddComponent<LootContainer>();
                BossChestLock lockComponent = chestObject.AddComponent<BossChestLock>();
                lockComponent.LockForNewRaid();
                if (!chest.IsLocked) failures.Add("Boss chest không khóa khi bắt đầu raid.");
                lockComponent.Unlock();
                if (chest.IsLocked) failures.Add("Boss chest không mở khóa sau khi boss chết.");
                Object.DestroyImmediate(chestObject);
            }
            catch (System.Exception exception) { failures.Add(exception.ToString()); }
            finally { Object.DestroyImmediate(temporary); }

            if (failures.Count == 0)
                Debug.Log("[PixelLooter][EnemySelfTest] ĐẠT — archetype, phase, phần thưởng bậc IV, loot corpse một lần và khóa/mở rương; không dùng save người chơi.");
            else Debug.LogError("[PixelLooter][EnemySelfTest] THẤT BẠI\n- " + string.Join("\n- ", failures));
        }
    }
}

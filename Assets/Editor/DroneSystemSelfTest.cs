using System;
using System.Collections.Generic;
using System.IO;
using Looter.Combat;
using Looter.Inventory;
using Looter.Items;
using Looter.Persistence;
using UnityEditor;
using UnityEngine;

namespace Looter.EditorTools
{
    public static class DroneSystemSelfTest
    {
        [MenuItem("Pixel Looter/Chạy tự kiểm tra Drone & Damage (không save thật)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[PixelLooter][DroneSelfTest] Hãy chạy kiểm tra ngoài Play Mode.");
                return;
            }

            List<UnityEngine.Object> temporaryObjects = new List<UnityEngine.Object>();
            string tempFolder = Path.Combine(Path.GetTempPath(), "PixelLooterDroneTest-" + Guid.NewGuid().ToString("N"));
            GameObject inventoryObject = null;
            GameObject profileObject = null;
            try
            {
                DroneSystemSettings settings = ScriptableObject.CreateInstance<DroneSystemSettings>();
                temporaryObjects.Add(settings);
                ItemDefinition droneDefinition = CreateItem("test_drone", ItemType.Drone, ItemTier.II, 100);
                ItemDefinition repairKit = CreateItem("test_repair_kit", ItemType.Consumable, ItemTier.I, 0);
                temporaryObjects.Add(droneDefinition);
                temporaryObjects.Add(repairKit);
                SerializedObject settingsObject = new SerializedObject(settings);
                settingsObject.FindProperty("repairKit").objectReferenceValue = repairKit;
                settingsObject.ApplyModifiedPropertiesWithoutUndo();

                ItemStack drone = new ItemStack(droneDefinition, 1, "test-instance", 100, 100);
                AssertDamage(20, ItemTier.I, drone, 20, 0, "kẻ địch mạnh hơn");
                AssertDamage(20, ItemTier.II, drone, 10, 10, "cùng bậc");
                AssertDamage(5, ItemTier.II, drone, 3, 2, "sát thương lẻ cùng bậc");
                AssertDamage(20, ItemTier.III, drone, 20, 0, "kẻ địch mạnh hơn một bậc");
                AssertDamage(20, ItemTier.I, null, 20, 0, "không có drone");
                drone.SetDroneDurability(0, 100);
                AssertDamage(20, ItemTier.I, drone, 20, 0, "drone đã hỏng");
                drone.SetDroneDurability(5, 100);
                DamageResolution overflow = DamageResolver.Resolve(20, ItemTier.I, drone);
                int absorbed = drone.ApplyDurabilityDamage(overflow.DroneDamage);
                Assert(20 - absorbed == 15 && drone.CurrentDurability == 0,
                    "Sát thương tràn khi drone còn 5 độ bền phải gây 15 HP và drone về 0.");

                drone.SetDroneDurability(20, 100);
                Assert(DroneRepairService.TryCalculateRepairKit(drone, settings, out DroneRepairResult kitRepair, out _),
                    "Không tính được sửa bằng Repair Kit.");
                Assert(kitRepair.WearAmount == 15 && kitRepair.EffectiveMaxDurability == 85 &&
                       kitRepair.CurrentDurability == 63, "Kết quả sửa kit/hạ max ở mức hư hỏng nặng sai.");
                Assert(DroneRepairService.TryCalculateBaseHubRepair(drone, settings, out DroneRepairResult baseRepair, out _),
                    "Không tính được sửa tại BaseHub.");
                Assert(baseRepair.WearAmount == 15 && baseRepair.CurrentDurability == 85 &&
                       baseRepair.EffectiveMaxDurability == 85 && baseRepair.Cost > 0,
                    "Sửa BaseHub phải đầy độ bền mới và giảm max theo mức hư hỏng.");

                ItemStack floorDrone = new ItemStack(droneDefinition, 1, "floor-test", 0, 25);
                Assert(DroneRepairService.TryCalculateRepairKit(floorDrone, settings, out DroneRepairResult floorRepair, out _) &&
                       floorRepair.EffectiveMaxDurability == 20,
                    "Độ bền tối đa sau nhiều lần sửa không được thấp hơn 20% độ bền gốc.");

                inventoryObject = new GameObject("Drone self-test inventory");
                inventoryObject.hideFlags = HideFlags.HideAndDontSave;
                InventoryModel inventory = inventoryObject.AddComponent<InventoryModel>();
                drone.SetDroneDurability(20, 100);
                Assert(inventory.TryAdd(repairKit, 1), "Không thể thêm Repair Kit vào inventory tự kiểm tra.");
                Assert(inventory.TryRepairDroneWithKit(drone, settings, out _, false) &&
                       !inventory.HasRepairKit(settings) && drone.CurrentDurability == 63 && drone.EffectiveMaxDurability == 85,
                    "Sửa thành công phải tiêu hao đúng một kit, phục hồi drone và giảm max đúng mức.");
                drone.SetDroneDurability(20, 100);
                Assert(!inventory.TryRepairDroneWithKit(drone, settings, out _, false) &&
                       drone.CurrentDurability == 20 && drone.EffectiveMaxDurability == 100,
                    "Thiếu Repair Kit không được tiêu hao tài nguyên hoặc đổi độ bền.");

                ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Data/Catalogs/ItemCatalog.asset");
                ItemDefinition savedDrone = catalog != null ? catalog.Find("bot_drone_ii") : null;
                Assert(savedDrone != null, "Không tải được bot_drone_ii để thử giao dịch BaseHub.");
                Directory.CreateDirectory(tempFolder);
                string savePath = Path.Combine(tempFolder, "profile.json");
                PlayerSaveData seed = CreateEmptyProfile();
                seed.currency = 0;
                seed.stash[0] = new SavedItemRecord
                {
                    itemId = savedDrone.Id, instanceId = "test-basehub-drone", quantity = 1,
                    currentDurability = 20, effectiveMaxDurability = Mathf.Max(1, savedDrone.BaseMaxDurability - 10)
                };
                profileObject = new GameObject("Drone self-test isolated profile");
                profileObject.SetActive(false);
                PlayerProfileStore profile = profileObject.AddComponent<PlayerProfileStore>();
                profile.InitializeIsolatedDiagnosticProfile(catalog, seed, savePath);
                profileObject.hideFlags = HideFlags.HideAndDontSave;
                profileObject.SetActive(true);
                Assert(profile.TryMove(PlayerStorage.Stash, 0, PlayerStorage.DroneEquipment, 0),
                    "Không thể di chuyển drone thử nghiệm từ stash vào equipment.");
                ItemStack movedDrone = profile.GetItemStack(PlayerStorage.DroneEquipment, 0);
                string savedBeforeFailedRepair = File.ReadAllText(savePath);
                Assert(movedDrone.InstanceId == "test-basehub-drone" && movedDrone.CurrentDurability == 20 &&
                       movedDrone.EffectiveMaxDurability == Mathf.Max(1, savedDrone.BaseMaxDurability - 10),
                    "Chuyển drone làm thay đổi Instance ID hoặc durability.");
                Assert(!profile.TryRepairEquippedDrone(settings, out _) && profile.Currency == 0 &&
                       profile.GetItemStack(PlayerStorage.DroneEquipment, 0).CurrentDurability == 20 &&
                       File.ReadAllText(savePath) == savedBeforeFailedRepair,
                    "Thiếu Xu không được sửa drone, trừ tiền hoặc thay đổi save.");

                Debug.Log("[PixelLooter][DroneSelfTest] ĐẠT: phân bậc/chia damage, damage lẻ/tràn, drone disabled, hao mòn/giới hạn, sửa kit, thiếu kit/Xu, chuyển durability/ID và save tạm; không sử dụng save thật.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[PixelLooter][DroneSelfTest] THẤT BẠI: " + exception);
            }
            finally
            {
                if (profileObject != null) UnityEngine.Object.DestroyImmediate(profileObject);
                if (inventoryObject != null) UnityEngine.Object.DestroyImmediate(inventoryObject);
                foreach (UnityEngine.Object temporary in temporaryObjects)
                    if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
                string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullFolder = Path.GetFullPath(tempFolder);
                if (fullFolder.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullFolder))
                    Directory.Delete(fullFolder, true);
            }
        }

        private static ItemDefinition CreateItem(string id, ItemType type, ItemTier tier, int durability)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("itemType").intValue = (int)type;
            serialized.FindProperty("tier").intValue = (int)tier;
            serialized.FindProperty("baseMaxDurability").intValue = Mathf.Max(1, durability);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static PlayerSaveData CreateEmptyProfile()
        {
            PlayerSaveData data = new PlayerSaveData();
            Fill(data.stash, 60); Fill(data.carried, 30); Fill(data.safe, 2); Fill(data.equipment, 3);
            return data;
        }

        private static void Fill(List<SavedItemRecord> list, int count)
        {
            while (list.Count < count) list.Add(SavedItemRecord.Empty());
        }

        private static void AssertDamage(int baseDamage, ItemTier attackTier, ItemStack drone,
            int expectedHp, int expectedDrone, string scenario)
        {
            DamageResolution result = DamageResolver.Resolve(baseDamage, attackTier, drone);
            Assert(result.PlayerDamage == expectedHp && result.DroneDamage == expectedDrone &&
                   result.PlayerDamage + result.DroneDamage == baseDamage,
                $"Phân chia damage sai ở trường hợp {scenario}: HP {result.PlayerDamage}, drone {result.DroneDamage}.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

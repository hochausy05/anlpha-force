using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Looter.Items;
using Looter.Persistence;
using UnityEditor;
using UnityEngine;

namespace Looter.EditorTools
{
    public static class EquipmentTransactionSelfTest
    {
        private const string CatalogPath = "Assets/Data/Catalogs/ItemCatalog.asset";

        [MenuItem("Pixel Looter/Chạy tự kiểm tra giao dịch trang bị (save tạm)")]
        public static void Run()
        {
            string folder = Path.Combine(Path.GetTempPath(), "PixelLooterEquipmentTest-" + Guid.NewGuid().ToString("N"));
            string savePath = Path.Combine(folder, "profile.json");
            GameObject host = null;
            try
            {
                Directory.CreateDirectory(folder);
                ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
                Require(catalog != null, "Không tải được ItemCatalog.");
                PlayerSaveData seed = CreateSeed();
                host = new GameObject("Hồ sơ kiểm thử cô lập");
                host.SetActive(false);
                PlayerProfileStore profile = host.AddComponent<PlayerProfileStore>();
                profile.InitializeIsolatedDiagnosticProfile(catalog, seed, savePath);
                host.SetActive(true);

                int changed = 0;
                profile.Changed += () => changed++;
                Require(profile.GetDefinition(PlayerStorage.Stash, 0)?.Type == ItemType.Backpack, "Không tìm thấy balo I hợp lệ trong nguồn.");
                Require(profile.CanMove(PlayerStorage.Stash, 0, PlayerStorage.BackpackEquipment, 0, out string reason), "CanMove từ chối balo I: " + reason);
                string backpackIId = profile.GetInstanceId(PlayerStorage.Stash, 0);
                Require(profile.TryMove(PlayerStorage.Stash, 0, PlayerStorage.BackpackEquipment, 0), "Giao dịch trực tiếp balo I thất bại: " + profile.StatusMessage);
                Require(profile.GetInstanceId(PlayerStorage.BackpackEquipment, 0) == backpackIId, "Instance ID balo I không được bảo toàn.");
                Require(profile.GetInstanceId(PlayerStorage.Stash, 0) == string.Empty, "Balo I còn bị nhân đôi ở nguồn.");
                Require(profile.Capacity(PlayerStorage.Carried) == 10, "Dung lượng balo I phải là 10.");

                string backpackIIId = profile.GetInstanceId(PlayerStorage.Stash, 1);
                Require(profile.TryMove(PlayerStorage.Stash, 1, PlayerStorage.BackpackEquipment, 0), "Hoán đổi balo II thất bại: " + profile.StatusMessage);
                Require(profile.GetInstanceId(PlayerStorage.BackpackEquipment, 0) == backpackIIId, "Instance ID balo II sai sau hoán đổi.");
                Require(profile.GetInstanceId(PlayerStorage.Stash, 1) == backpackIId, "Balo I cũ không được trả về ô nguồn sau hoán đổi.");
                Require(profile.Capacity(PlayerStorage.Carried) == 14, "Dung lượng balo II phải là 14.");

                seed.carried[13] = Record("scrap_iron", "selftest-capacity-item");
                Require(!profile.TryMove(PlayerStorage.Stash, 1, PlayerStorage.BackpackEquipment, 0),
                    "Không được hạ xuống balo I khi vật phẩm đang nằm ngoài sức chứa 10 ô.");
                Require(profile.GetInstanceId(PlayerStorage.BackpackEquipment, 0) == backpackIIId &&
                        profile.GetInstanceId(PlayerStorage.Stash, 1) == backpackIId,
                    "Giao dịch giảm dung lượng bị từ chối nhưng dữ liệu trang bị/kho đã đổi.");

                string droneId = profile.GetInstanceId(PlayerStorage.Stash, 2);
                Require(profile.TryMove(PlayerStorage.Stash, 2, PlayerStorage.DroneEquipment, 0), "Trang bị Drone II thất bại: " + profile.StatusMessage);
                Require(profile.GetInstanceId(PlayerStorage.DroneEquipment, 0) == droneId, "Instance ID drone không được bảo toàn.");
                string knifeId = profile.GetInstanceId(PlayerStorage.Stash, 3);
                Require(profile.TryMove(PlayerStorage.Stash, 3, PlayerStorage.WeaponEquipment, 0), "Trang bị dao thất bại: " + profile.StatusMessage);
                Require(profile.GetInstanceId(PlayerStorage.WeaponEquipment, 0) == knifeId, "Instance ID dao không được bảo toàn.");
                Require(!profile.TryMove(PlayerStorage.Stash, 4, PlayerStorage.WeaponEquipment, 0), "Balo vào WeaponSlot lẽ ra phải bị từ chối.");
                Require(profile.GetInstanceId(PlayerStorage.Stash, 4) == "selftest-invalid-backpack", "Từ chối sai loại đã làm mất vật phẩm nguồn.");
                Require(changed >= 4, "Sự kiện Changed không được phát sau các giao dịch.");

                MethodInfo loadProfile = typeof(PlayerProfileStore).GetMethod("LoadProfile", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(loadProfile != null, "Không tìm thấy hàm nạp profile để kiểm tra save/load.");
                loadProfile.Invoke(profile, null);
                Require(profile.IsReady, "Không nạp lại được profile save tạm.");
                PlayerSaveData loaded = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(savePath));
                Require(loaded.equipment[0].instanceId == knifeId && loaded.equipment[1].instanceId == backpackIIId &&
                        loaded.equipment[2].instanceId == droneId, "Trang bị không tồn tại chính xác trong save tạm.");
                HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (List<SavedItemRecord> storage in new[] { loaded.stash, loaded.carried, loaded.safe, loaded.equipment })
                    foreach (SavedItemRecord record in storage)
                        if (!record.IsEmpty) Require(ids.Add(record.instanceId), "Instance ID bị trùng giữa các storage.");

                Debug.Log($"[PixelLooter][EquipmentSelfTest] ĐẠT: balo I/II, hoán đổi, drone II, dao, từ chối sai loại, Changed, ID ổn định và lưu/tải cô lập. Changed={changed}. Save tạm={savePath}");
            }
            catch (Exception exception)
            {
                Debug.LogError("[PixelLooter][EquipmentSelfTest] THẤT BẠI: " + exception);
            }
            finally
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullFolder = Path.GetFullPath(folder);
                if (fullFolder.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullFolder))
                    Directory.Delete(fullFolder, true);
            }
        }

        private static PlayerSaveData CreateSeed()
        {
            PlayerSaveData data = new PlayerSaveData { schemaVersion = 1, currency = 321 };
            Fill(data.stash, 60);
            Fill(data.carried, 30);
            Fill(data.safe, 2);
            Fill(data.equipment, 3);
            data.stash[0] = Record("bot_backpack_i", "selftest-backpack-i");
            data.stash[1] = Record("bot_backpack_ii", "selftest-backpack-ii");
            data.stash[2] = Record("bot_drone_ii", "selftest-drone-ii");
            data.stash[3] = Record("bot_knife_i", "selftest-knife");
            data.stash[4] = Record("bot_backpack_i", "selftest-invalid-backpack");
            return data;
        }

        private static void Fill(List<SavedItemRecord> records, int count)
        {
            while (records.Count < count) records.Add(SavedItemRecord.Empty());
        }

        private static SavedItemRecord Record(string itemId, string instanceId) =>
            new SavedItemRecord { itemId = itemId, instanceId = instanceId, quantity = 1 };

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

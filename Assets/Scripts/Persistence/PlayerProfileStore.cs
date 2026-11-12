using System;
using System.Collections.Generic;
using System.IO;
using Looter.Economy;
using Looter.Combat;
using Looter.Inventory;
using Looter.Items;
using Looter.Raid;
using UnityEngine;

namespace Looter.Persistence
{
    public sealed class PlayerProfileStore : MonoBehaviour
    {
        private const int CurrentSchemaVersion = 1;
        private const int StashCapacity = 60;
        private const int MaximumBackpackCapacity = 30;
        private const int SafeCapacity = 2;
        [SerializeField, Min(0)] private int initialCurrency = 1000;
        [SerializeField] private ItemCatalog itemCatalog;
        [SerializeField] private bool logEquipmentTransactions;

        private static PlayerProfileStore instance;
        private PlayerSaveData data;
        private string savePath;
        private InventoryModel activeRaidInventory;
        private bool ready;
        private bool readOnly;
        private string statusMessage;
#if UNITY_EDITOR
        private bool isolatedDiagnosticProfile;
#endif

        public static PlayerProfileStore Instance => instance;
        public bool IsReady => ready;
        public bool IsReadOnly => readOnly;
        public string StatusMessage => statusMessage;
        public int Currency => data != null ? data.currency : 0;
        public string LastRaidSummary => data != null ? data.lastRaidSummary : string.Empty;
        public bool RaidInProgress => data != null && data.raidInProgress;
        public ItemCatalog Catalog => itemCatalog;
        public event Action Changed;

        private void Awake()
        {
#if UNITY_EDITOR
            if (isolatedDiagnosticProfile)
            {
                instance = this;
                return;
            }
#endif
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            savePath = Path.Combine(Application.persistentDataPath, "player-profile.json");
            LoadProfile();
        }

        private void OnDestroy()
        {
            UnbindRaidInventory();
            if (instance == this) instance = null;
        }

#if UNITY_EDITOR
        public void InitializeIsolatedDiagnosticProfile(ItemCatalog catalog, PlayerSaveData initialData, string isolatedSavePath)
        {
            if (gameObject.activeInHierarchy)
                throw new InvalidOperationException("Tạo hồ sơ chẩn đoán trên GameObject đang inactive.");
            itemCatalog = catalog;
            data = initialData;
            savePath = isolatedSavePath;
            ready = true;
            readOnly = false;
            statusMessage = string.Empty;
            isolatedDiagnosticProfile = true;
        }
#endif

        private void OnApplicationPause(bool paused)
        {
            if (paused && ready && !readOnly) Save();
        }

        private void OnApplicationQuit()
        {
            if (ready && !readOnly) Save();
        }

        public int Capacity(PlayerStorage storage)
        {
            switch (storage)
            {
                case PlayerStorage.Stash: return StashCapacity;
                case PlayerStorage.Carried: return GetBackpackCapacity();
                case PlayerStorage.Safe: return SafeCapacity;
                case PlayerStorage.WeaponEquipment:
                case PlayerStorage.BackpackEquipment:
                case PlayerStorage.DroneEquipment: return 1;
                default: return 0;
            }
        }

        public ItemDefinition GetDefinition(PlayerStorage storage, int index)
        {
            SavedItemRecord record = GetRecord(storage, index);
            return record == null || record.IsEmpty || itemCatalog == null ? null : itemCatalog.Find(record.itemId);
        }

        public int GetQuantity(PlayerStorage storage, int index)
        {
            SavedItemRecord record = GetRecord(storage, index);
            return record == null || record.IsEmpty ? 0 : Mathf.Max(0, record.quantity);
        }

        public ItemStack GetItemStack(PlayerStorage storage, int index)
        {
            SavedItemRecord record = GetRecord(storage, index);
            return record == null || record.IsEmpty ? null : record.ToStack(itemCatalog);
        }

        public string GetInstanceId(PlayerStorage storage, int index)
        {
            SavedItemRecord record = GetRecord(storage, index);
            return record == null || record.IsEmpty ? string.Empty : record.instanceId;
        }

        public bool TryMove(PlayerStorage sourceStorage, int sourceIndex, PlayerStorage destinationStorage, int destinationIndex)
        {
            bool trace = logEquipmentTransactions && (IsEquipmentStorage(sourceStorage) || IsEquipmentStorage(destinationStorage));
            if (!CanMove(sourceStorage, sourceIndex, destinationStorage, destinationIndex, out string rejection))
                return RejectMove(rejection, trace, $"{sourceStorage}[{sourceIndex}] → {destinationStorage}[{destinationIndex}]");
            SavedItemRecord source = GetRecord(sourceStorage, sourceIndex);
            SavedItemRecord destination = GetRecord(destinationStorage, destinationIndex);
            ItemDefinition sourceDefinition = itemCatalog.Find(source.itemId);
            string sourceInstanceId = source.instanceId;
            string transaction = $"{source.itemId} ({sourceStorage}[{sourceIndex}] → {destinationStorage}[{destinationIndex}]), instance={sourceInstanceId}";
            if (trace)
                Debug.Log($"[PixelLooter][EquipmentTxn] Bắt đầu: {transaction}; loại={(sourceDefinition != null ? sourceDefinition.Type.ToString() : "thiếu định nghĩa")}.", this);
            ItemDefinition destinationDefinition = destination.IsEmpty ? null : itemCatalog.Find(destination.itemId);
            PlayerSaveData before = CloneData();

            if (destination.IsEmpty)
            {
                CopyRecord(source, destination);
                source.Clear();
                return CommitMove(before, trace, transaction, sourceInstanceId, destinationStorage, destinationIndex);
            }

            if (sourceDefinition.Id == destinationDefinition.Id && sourceDefinition.IsStackable && destinationDefinition.IsStackable)
            {
                int transferred = Mathf.Min(source.quantity, destinationDefinition.MaxStackSize - destination.quantity);
                if (transferred <= 0)
                {
                    statusMessage = "Stack đích đã đầy.";
                    Changed?.Invoke();
                    return false;
                }
                destination.quantity += transferred;
                source.quantity -= transferred;
                if (source.quantity <= 0) source.Clear();
                return CommitMove(before, trace, transaction, sourceInstanceId, destinationStorage, destinationIndex);
            }

            SavedItemRecord temporary = CopyRecord(source);
            CopyRecord(destination, source);
            CopyRecord(temporary, destination);
            return CommitMove(before, trace, transaction, sourceInstanceId, destinationStorage, destinationIndex);
        }

        public bool CanMove(PlayerStorage sourceStorage, int sourceIndex,
            PlayerStorage destinationStorage, int destinationIndex, out string rejection)
        {
            rejection = null;
            if (!CanMutate) { rejection = "Hồ sơ chưa sẵn sàng hoặc đang ở chế độ chỉ đọc."; return false; }
            if (sourceStorage == destinationStorage && sourceIndex == destinationIndex)
            { rejection = "Hãy thả vật phẩm vào một ô khác."; return false; }
            SavedItemRecord source = GetRecord(sourceStorage, sourceIndex);
            SavedItemRecord destination = GetRecord(destinationStorage, destinationIndex);
            if (source == null || source.IsEmpty) { rejection = "Ô nguồn không có vật phẩm để chuyển."; return false; }
            if (destination == null) { rejection = "Ô đích không hợp lệ."; return false; }
            ItemDefinition sourceDefinition = itemCatalog != null ? itemCatalog.Find(source.itemId) : null;
            if (sourceDefinition == null) { rejection = "Không tìm thấy định nghĩa vật phẩm trong catalog."; return false; }
            if (!CanStore(destinationStorage, destinationIndex, sourceDefinition))
            { rejection = $"Ô {destinationStorage} không nhận loại vật phẩm {sourceDefinition.Type}."; return false; }

            ItemDefinition destinationDefinition = destination.IsEmpty || itemCatalog == null
                ? null : itemCatalog.Find(destination.itemId);
            if (!destination.IsEmpty && destinationDefinition == null)
            { rejection = "Không tìm thấy định nghĩa của vật phẩm ở ô đích."; return false; }
            if (destinationDefinition != null && !CanStore(sourceStorage, sourceIndex, destinationDefinition))
            { rejection = $"Không thể hoán đổi: ô {sourceStorage} không nhận loại {destinationDefinition.Type}."; return false; }
            if (!CanApplyBackpackChange(sourceStorage, sourceIndex, destinationStorage, destinationIndex,
                    sourceDefinition, destinationDefinition))
            { rejection = "Không thể đổi balo: số vật phẩm đang mang vượt sức chứa mới. Hãy giải phóng các ô phía cuối trước."; return false; }
            return true;
        }

        private bool CommitMove(PlayerSaveData before, bool trace, string transaction, string sourceInstanceId,
            PlayerStorage destinationStorage, int destinationIndex)
        {
            bool committed = CommitMutation(before);
            if (trace)
            {
                string destinationId = GetInstanceId(destinationStorage, destinationIndex);
                int subscriberCount = Changed != null ? Changed.GetInvocationList().Length : 0;
                Debug.Log($"[PixelLooter][EquipmentTxn] Kết quả={(committed ? "thành công" : "thất bại")}; " +
                          $"instance nguồn={sourceInstanceId}; instance tại ô đích={destinationId}; " +
                          $"Changed subscribers={subscriberCount}; thông báo='{statusMessage}'.", this);
            }
            return committed;
        }

        private bool RejectMove(string message, bool trace = false, string transaction = "")
        {
            statusMessage = message;
            if (trace) Debug.LogWarning($"[PixelLooter][EquipmentTxn] Từ chối: {transaction}; lý do='{message}'.", this);
            Changed?.Invoke();
            return false;
        }

        private static bool IsEquipmentStorage(PlayerStorage storage) =>
            storage == PlayerStorage.WeaponEquipment || storage == PlayerStorage.BackpackEquipment ||
            storage == PlayerStorage.DroneEquipment;

        public bool TryBuy(ShopOffer offer)
        {
            if (!CanMutate || offer == null || offer.Item == null) return false;
            int quantity = offer.Item.IsStackable ? offer.PurchaseQuantity : 1;
            long totalPriceLong = (long)offer.BuyPrice * quantity;
            if (totalPriceLong > int.MaxValue || data.currency < totalPriceLong)
            { statusMessage = "Không đủ Xu để mua vật phẩm."; Changed?.Invoke(); return false; }
            if (!CanFitStash(offer.Item, quantity))
            { statusMessage = "Kho vĩnh viễn không đủ chỗ cho số lượng này."; Changed?.Invoke(); return false; }
            PlayerSaveData before = CloneData();
            AddToStash(offer.Item, quantity);
            data.currency -= (int)totalPriceLong;
            return CommitMutation(before);
        }

        private bool CanFitStash(ItemDefinition item, int quantity)
        {
            if (!item.IsStackable) return quantity == 1 && FirstEmpty(data.stash) != null;
            long free = 0;
            foreach (SavedItemRecord record in data.stash)
            {
                if (record.IsEmpty) free += item.MaxStackSize;
                else if (record.itemId == item.Id) free += Mathf.Max(0, item.MaxStackSize - record.quantity);
            }
            return free >= quantity;
        }

        private void AddToStash(ItemDefinition item, int quantity)
        {
            int remaining = quantity;
            if (item.IsStackable)
            {
                foreach (SavedItemRecord record in data.stash)
                {
                    if (remaining <= 0) break;
                    if (record.IsEmpty || record.itemId != item.Id) continue;
                    int amount = Mathf.Min(remaining, item.MaxStackSize - record.quantity);
                    record.quantity += amount;
                    remaining -= amount;
                }
            }
            while (remaining > 0)
            {
                SavedItemRecord slot = FirstEmpty(data.stash);
                if (slot == null) break;
                int amount = item.IsStackable ? Mathf.Min(remaining, item.MaxStackSize) : 1;
                slot.itemId = item.Id;
                slot.instanceId = Guid.NewGuid().ToString("N");
                slot.quantity = amount;
                remaining -= amount;
            }
        }

        public bool TrySell(PlayerStorage storage, int index)
        {
            if (!CanMutate || storage != PlayerStorage.Stash) return false;
            SavedItemRecord record = GetRecord(storage, index);
            if (record == null || record.IsEmpty) return false;
            ItemDefinition definition = itemCatalog.Find(record.itemId);
            if (definition == null || definition.SellValue < 0) return false;
            long value = (long)definition.SellValue * Mathf.Max(1, record.quantity);
            if (value > int.MaxValue - data.currency) return false;
            PlayerSaveData before = CloneData();
            data.currency += (int)value;
            record.Clear();
            return CommitMutation(before);
        }

        public bool TryRepairEquippedDrone(DroneSystemSettings settings, out string reason)
        {
            reason = null;
            if (!CanMutate) { reason = "Hồ sơ chưa sẵn sàng hoặc đang ở chế độ chỉ đọc."; return false; }
            if (data.raidInProgress) { reason = "Không thể dùng dịch vụ sửa căn cứ khi đang trong raid."; return false; }
            SavedItemRecord record = data.equipment[2];
            ItemStack drone = record != null && !record.IsEmpty ? record.ToStack(itemCatalog) : null;
            if (!DroneRepairService.TryCalculateBaseHubRepair(drone, settings, out DroneRepairResult repair, out reason)) return false;
            if (data.currency < repair.Cost) { reason = "Không đủ Xu để sửa drone."; return false; }

            PlayerSaveData before = CloneData();
            data.currency -= repair.Cost;
            record.currentDurability = repair.CurrentDurability;
            record.effectiveMaxDurability = repair.EffectiveMaxDurability;
            if (!CommitMutation(before))
            {
                reason = statusMessage;
                return false;
            }
            reason = $"Đã sửa drone với giá {repair.Cost} Xu; độ bền tối đa giảm {repair.WearAmount}.";
            statusMessage = reason;
            return true;
        }

        public bool UpdateEquippedDroneDurability(ItemStack drone)
        {
            if (!CanMutate || drone == null || drone.Item == null || drone.Item.Type != ItemType.Drone) return false;
            SavedItemRecord record = data.equipment[2];
            if (record == null || record.IsEmpty || record.instanceId != drone.InstanceId || record.itemId != drone.Item.Id) return false;
            if (record.currentDurability == drone.CurrentDurability &&
                record.effectiveMaxDurability == drone.EffectiveMaxDurability) return true;
            PlayerSaveData before = CloneData();
            if (data.raidInProgress && activeRaidInventory != null) CaptureInventory(activeRaidInventory, true);
            record.currentDurability = drone.CurrentDurability;
            record.effectiveMaxDurability = drone.EffectiveMaxDurability;
            return CommitMutation(before);
        }

        public bool UpdateEquippedWeaponMagazine(ItemStack weapon)
        {
            if (!CanMutate || weapon == null || weapon.Item == null || weapon.Item.Type != ItemType.Weapon) return false;
            SavedItemRecord record = data.equipment[0];
            if (record == null || record.IsEmpty || record.instanceId != weapon.InstanceId || record.itemId != weapon.Item.Id) return false;
            if (record.currentMagazine == weapon.CurrentMagazine) return true;
            PlayerSaveData before = CloneData();
            if (data.raidInProgress && activeRaidInventory != null) CaptureInventory(activeRaidInventory, true);
            record.currentMagazine = weapon.CurrentMagazine;
            return CommitMutation(before);
        }

        public bool BeginRaid()
        {
            if (!CanMutate || data.raidInProgress) return false;
            PlayerSaveData before = CloneData();
            data.raidInProgress = true;
            data.lastRaidSummary = string.Empty;
            return CommitMutation(before);
        }

        public bool LoadRaidInventory(InventoryModel inventory, EquipmentLoadout loadout)
        {
            if (!ready || !data.raidInProgress || inventory == null) return false;
            int capacity = GetBackpackCapacity();
            List<ItemStack> carried = ResolveSlots(data.carried, capacity);
            List<ItemStack> safe = ResolveSlots(data.safe, SafeCapacity);
            if (carried == null || safe == null || !inventory.ReplaceContents(carried, safe, capacity)) return false;
            if (loadout != null)
                loadout.SetSlots(ResolveEquipment(0), ResolveEquipment(1), ResolveEquipment(2));
            BindRaidInventory(inventory);
            return true;
        }

        public bool CompleteRaid(RaidOutcome outcome, InventoryModel inventory)
        {
            if (!ready || !data.raidInProgress) return false;
            UnbindRaidInventory();
            PlayerSaveData before = CloneData();
            CaptureInventory(inventory, outcome == RaidOutcome.Extracted);
            if (outcome == RaidOutcome.Failed)
            {
                ClearRecords(data.carried);
                for (int i = 0; i < data.equipment.Count; i++) data.equipment[i].Clear();
                data.lastRaidSummary = "Raid thất bại. Vật phẩm trong ô an toàn được giữ; balo và trang bị mang theo đã mất.";
            }
            else
            {
                data.lastRaidSummary = "Extraction thành công. Loot đang ở balo và ô an toàn; hãy chuyển đồ vào kho tại căn cứ.";
            }
            data.raidInProgress = false;
            bool saved = Save();
            if (!saved) data = before;
            if (saved) Changed?.Invoke();
            return saved;
        }

        public void BindRaidInventory(InventoryModel inventory)
        {
            if (activeRaidInventory == inventory) return;
            UnbindRaidInventory();
            activeRaidInventory = inventory;
            if (activeRaidInventory != null) activeRaidInventory.Changed += OnRaidInventoryChanged;
        }

        public void UnbindRaidInventory()
        {
            if (activeRaidInventory != null) activeRaidInventory.Changed -= OnRaidInventoryChanged;
            activeRaidInventory = null;
        }

        private void OnRaidInventoryChanged()
        {
            if (!ready || readOnly || !data.raidInProgress) return;
            PlayerSaveData before = CloneData();
            CaptureInventory(activeRaidInventory, true);
            if (!Save()) data = before;
        }

        private void CaptureInventory(InventoryModel inventory, bool includeCarried)
        {
            if (inventory == null) return;
            if (includeCarried)
                WriteSlots(data.carried, inventory, InventoryStorage.Backpack);
            WriteSlots(data.safe, inventory, InventoryStorage.Safe);
        }

        private static void WriteSlots(List<SavedItemRecord> destination, InventoryModel inventory, InventoryStorage storage)
        {
            for (int i = 0; i < destination.Count; i++)
                destination[i] = i < inventory.SlotCount(storage)
                    ? SavedItemRecord.FromStack(inventory.GetStack(storage, i))
                    : SavedItemRecord.Empty();
        }

        private ItemStack ResolveEquipment(int index)
        {
            if (data.equipment == null || index < 0 || index >= data.equipment.Count) return null;
            SavedItemRecord record = data.equipment[index];
            return record == null || record.IsEmpty ? null : record.ToStack(itemCatalog);
        }

        private List<ItemStack> ResolveSlots(List<SavedItemRecord> source, int capacity)
        {
            List<ItemStack> result = new List<ItemStack>(capacity);
            for (int i = 0; i < capacity; i++)
            {
                SavedItemRecord record = i < source.Count ? source[i] : null;
                if (record == null || record.IsEmpty) result.Add(null);
                else
                {
                    ItemStack stack = record.ToStack(itemCatalog);
                    if (stack == null) return null;
                    result.Add(stack);
                }
            }
            return result;
        }

        private void LoadProfile()
        {
            ready = false;
            readOnly = false;
            string backupPath = savePath + ".bak";
            if (File.Exists(savePath))
            {
                if (!TryRead(savePath, out data, out string mainError))
                {
                    if (File.Exists(backupPath) && TryRead(backupPath, out data, out string backupError))
                    {
                        readOnly = true;
                        statusMessage = "Save chính bị lỗi; đã mở bản dự phòng ở chế độ chỉ đọc. Sao lưu player-profile.json trước khi sửa.";
                        Debug.LogError($"Không thể đọc save chính: {mainError}. Bản dự phòng được dùng: {backupError}");
                    }
                    else
                    {
                        statusMessage = "Save bị lỗi; không tạo save trống để tránh ghi đè tiến trình hiện có.";
                        Debug.LogError($"Không thể đọc save player-profile.json: {mainError}");
                        return;
                    }
                }
            }
            else if (File.Exists(backupPath))
            {
                if (!TryRead(backupPath, out data, out string backupError))
                {
                    statusMessage = "Không đọc được save dự phòng; dữ liệu hiện có được giữ nguyên.";
                    Debug.LogError($"Không thể đọc save dự phòng: {backupError}");
                    return;
                }
                readOnly = true;
                statusMessage = "Đã nạp save dự phòng ở chế độ chỉ đọc; phục hồi player-profile.json trước khi tiếp tục.";
            }
            else
            {
                data = CreateNewProfile();
                ready = true;
                if (!Save()) { ready = false; return; }
            }

            string validationError = null;
            if (data == null || !ValidateAndNormalize(data, out validationError))
            {
                ready = false;
                readOnly = true;
                statusMessage = "Dữ liệu save không hợp lệ; không ghi đè file.";
                Debug.LogError($"Save không hợp lệ: {validationError}");
                return;
            }

            MigrateLegacyStacks(data.stash);
            MigrateLegacyStacks(data.carried);
            MigrateLegacyStacks(data.safe);

            ready = true;
            if (data.raidInProgress && !readOnly)
            {
                ClearRecords(data.carried);
                for (int i = 0; i < data.equipment.Count; i++) data.equipment[i].Clear();
                data.raidInProgress = false;
                data.lastRaidSummary = "Phiên trước kết thúc khi raid đang diễn ra; tài sản mang theo bị mất, ô an toàn được giữ.";
                if (!Save()) ready = false;
            }
        }

        private bool TryRead(string path, out PlayerSaveData loaded, out string error)
        {
            loaded = null;
            error = null;
            try
            {
                loaded = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(path));
                if (loaded == null) { error = "JSON rỗng hoặc không thể giải mã"; return false; }
                if (!ValidateAndNormalize(loaded, out error)) { loaded = null; return false; }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private bool ValidateAndNormalize(PlayerSaveData candidate, out string error)
        {
            error = null;
            if (candidate.schemaVersion != CurrentSchemaVersion) { error = $"Phiên bản save {candidate.schemaVersion} không hỗ trợ"; return false; }
            if (candidate.currency < 0) { error = "Số dư âm"; return false; }
            if (!NormalizeList(candidate.stash, StashCapacity, out error) ||
                !NormalizeList(candidate.carried, MaximumBackpackCapacity, out error) ||
                !NormalizeList(candidate.safe, SafeCapacity, out error) ||
                !NormalizeList(candidate.equipment, 3, out error)) return false;

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            List<SavedItemRecord> all = new List<SavedItemRecord>();
            all.AddRange(candidate.stash); all.AddRange(candidate.carried); all.AddRange(candidate.safe); all.AddRange(candidate.equipment);
            foreach (SavedItemRecord record in all)
            {
                if (record.IsEmpty)
                {
                    if (record.quantity != 0 || !string.IsNullOrWhiteSpace(record.instanceId))
                    { error = "Ô trống còn giữ quantity hoặc instance ID"; return false; }
                    continue;
                }
                ItemDefinition definition = itemCatalog != null ? itemCatalog.Find(record.itemId) : null;
                if (definition == null) { error = $"Không tìm thấy ItemDefinition '{record.itemId}' trong ItemCatalog"; return false; }
                if (record.quantity <= 0) { error = $"Quantity không hợp lệ cho '{record.itemId}'"; return false; }
                if (definition.IsStackable && record.quantity > definition.MaxStackSize)
                { error = $"Stack '{record.itemId}' vượt MaxStackSize"; return false; }
                if (definition.Type == ItemType.Drone)
                {
                    bool legacyDroneWithoutDurability = record.effectiveMaxDurability <= 0 && record.currentDurability <= 0;
                    if (legacyDroneWithoutDurability)
                    {
                        record.currentDurability = definition.BaseMaxDurability;
                        record.effectiveMaxDurability = definition.BaseMaxDurability;
                    }
                    record.effectiveMaxDurability = Mathf.Clamp(record.effectiveMaxDurability, 1, definition.BaseMaxDurability);
                    record.currentDurability = Mathf.Clamp(record.currentDurability, 0, record.effectiveMaxDurability);
                }
                if (definition.Type == ItemType.Weapon)
                    record.currentMagazine = definition.WeaponDefinition != null
                        ? Mathf.Clamp(record.currentMagazine, 0, definition.WeaponDefinition.MagazineCapacity)
                        : 0;
                if (string.IsNullOrWhiteSpace(record.instanceId)) record.instanceId = Guid.NewGuid().ToString("N");
                if (!ids.Add(record.instanceId)) { error = $"Instance ID bị trùng: {record.instanceId}"; return false; }
            }
            if (!ValidEquipment(candidate, 0, ItemType.Weapon, out error) ||
                !ValidEquipment(candidate, 1, ItemType.Backpack, out error) ||
                !ValidEquipment(candidate, 2, ItemType.Drone, out error)) return false;
            foreach (SavedItemRecord record in candidate.safe)
            {
                if (record.IsEmpty) continue;
                ItemDefinition definition = itemCatalog.Find(record.itemId);
                if (definition == null || !definition.CanStoreInSafeSlot)
                { error = "Ô an toàn chứa vật phẩm không hợp lệ"; return false; }
            }
            return true;
        }

        private static bool NormalizeList(List<SavedItemRecord> list, int expectedCount, out string error)
        {
            error = null;
            if (list == null) { error = "Thiếu vùng dữ liệu trong save"; return false; }
            if (list.Count > expectedCount) { error = "Save có nhiều ô hơn dung lượng hỗ trợ"; return false; }
            while (list.Count < expectedCount) list.Add(SavedItemRecord.Empty());
            for (int i = 0; i < list.Count; i++)
                if (list[i] == null) list[i] = SavedItemRecord.Empty();
            return true;
        }

        private bool ValidEquipment(PlayerSaveData candidate, int index, ItemType expected, out string error)
        {
            error = null;
            SavedItemRecord record = candidate.equipment[index];
            if (record.IsEmpty) return true;
            ItemDefinition definition = itemCatalog.Find(record.itemId);
            if (definition == null || definition.Type != expected || definition.IsStackable || record.quantity != 1)
            {
                error = $"Ô trang bị {expected} chứa dữ liệu không tương thích";
                return false;
            }
            return true;
        }

        private PlayerSaveData CreateNewProfile()
        {
            PlayerSaveData fresh = new PlayerSaveData { schemaVersion = CurrentSchemaVersion, currency = Mathf.Max(0, initialCurrency) };
            NormalizeList(fresh.stash, StashCapacity, out _);
            NormalizeList(fresh.carried, MaximumBackpackCapacity, out _);
            NormalizeList(fresh.safe, SafeCapacity, out _);
            NormalizeList(fresh.equipment, 3, out _);
            return fresh;
        }

        private void MigrateLegacyStacks(List<SavedItemRecord> slots)
        {
            foreach (SavedItemRecord stack in slots)
            {
                if (stack.IsEmpty || stack.quantity <= 1) continue;
                ItemDefinition definition = itemCatalog.Find(stack.itemId);
                if (definition == null || definition.IsStackable) continue;
                int emptyCount = 0;
                foreach (SavedItemRecord candidate in slots) if (candidate.IsEmpty) emptyCount++;
                int required = stack.quantity - 1;
                if (emptyCount < required) continue;
                int quantity = stack.quantity;
                stack.quantity = 1;
                for (int i = 0; i < slots.Count && quantity > 1; i++)
                {
                    if (!slots[i].IsEmpty) continue;
                    slots[i] = new SavedItemRecord
                    {
                        itemId = definition.Id,
                        instanceId = Guid.NewGuid().ToString("N"),
                        quantity = 1
                    };
                    quantity--;
                }
            }
        }

        private bool Save()
        {
            if (readOnly || data == null) return false;
            string temporaryPath = savePath + ".tmp";
            string backupPath = savePath + ".bak";
            try
            {
                string directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(savePath))
                {
                    File.Copy(savePath, backupPath, true);
                    File.Replace(temporaryPath, savePath, null);
                }
                else File.Move(temporaryPath, savePath);
                statusMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                statusMessage = "Không thể lưu tiến trình. Giao dịch chưa được xác nhận.";
                Debug.LogError($"Không thể ghi save: {exception.Message}");
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                return false;
            }
        }

        private bool CanMutate => ready && !readOnly && data != null;

        private bool CommitMutation(PlayerSaveData before)
        {
            if (!Save()) { data = before; return false; }
            statusMessage = "Đã lưu thay đổi.";
            Changed?.Invoke();
            return true;
        }

        private PlayerSaveData CloneData() => JsonUtility.FromJson<PlayerSaveData>(JsonUtility.ToJson(data));

        private SavedItemRecord GetRecord(PlayerStorage storage, int index)
        {
            if (index < 0 || index >= Capacity(storage)) return null;
            switch (storage)
            {
                case PlayerStorage.Stash: return data.stash[index];
                case PlayerStorage.Carried: return data.carried[index];
                case PlayerStorage.Safe: return data.safe[index];
                case PlayerStorage.WeaponEquipment: return data.equipment[0];
                case PlayerStorage.BackpackEquipment: return data.equipment[1];
                case PlayerStorage.DroneEquipment: return data.equipment[2];
                default: return null;
            }
        }

        private List<SavedItemRecord> GetList(PlayerStorage storage) => storage switch
        {
            PlayerStorage.Stash => data.stash,
            PlayerStorage.Carried => data.carried,
            PlayerStorage.Safe => data.safe,
            _ => null
        };

        private bool CanStore(PlayerStorage storage, int index, ItemDefinition item)
        {
            if (item == null || index < 0 || index >= Capacity(storage)) return false;
            return storage switch
            {
                PlayerStorage.Stash => true,
                PlayerStorage.Carried => true,
                PlayerStorage.Safe => item.CanStoreInSafeSlot,
                PlayerStorage.WeaponEquipment => item.Type == ItemType.Weapon && !item.IsStackable,
                PlayerStorage.BackpackEquipment => item.Type == ItemType.Backpack && !item.IsStackable,
                PlayerStorage.DroneEquipment => item.Type == ItemType.Drone && !item.IsStackable,
                _ => false
            };
        }

        private bool CanApplyBackpackChange(PlayerStorage sourceStorage, int sourceIndex,
            PlayerStorage destinationStorage, int destinationIndex, ItemDefinition source, ItemDefinition destination)
        {
            if (sourceStorage != PlayerStorage.BackpackEquipment && destinationStorage != PlayerStorage.BackpackEquipment) return true;
            ItemDefinition resultingBackpack = destinationStorage == PlayerStorage.BackpackEquipment
                ? source
                : sourceStorage == PlayerStorage.BackpackEquipment ? destination : null;
            int capacity = resultingBackpack != null ? resultingBackpack.BackpackCapacity : 10;
            List<SavedItemRecord> carriedAfter = new List<SavedItemRecord>(data.carried);
            if (sourceStorage == PlayerStorage.Carried)
                carriedAfter[sourceIndex] = destinationStorage == PlayerStorage.BackpackEquipment ? data.equipment[1] : SavedItemRecord.Empty();
            if (destinationStorage == PlayerStorage.Carried)
                carriedAfter[destinationIndex] = GetRecord(sourceStorage, sourceIndex);
            for (int i = capacity; i < MaximumBackpackCapacity; i++)
                if (!carriedAfter[i].IsEmpty) return false;
            if (destinationStorage == PlayerStorage.Carried && destinationIndex >= capacity) return false;
            return true;
        }

        private int GetBackpackCapacity()
        {
            // Dung lượng cơ bản khi không trang bị balo là 10 ô; balo I cũng có 10 ô.
            if (data == null || data.equipment == null || data.equipment.Count < 2 || data.equipment[1].IsEmpty) return 10;
            ItemDefinition definition = itemCatalog.Find(data.equipment[1].itemId);
            return definition != null ? Mathf.Clamp(definition.BackpackCapacity, 10, MaximumBackpackCapacity) : 10;
        }

        private static SavedItemRecord FirstEmpty(List<SavedItemRecord> slots)
        {
            foreach (SavedItemRecord slot in slots) if (slot.IsEmpty) return slot;
            return null;
        }

        private static SavedItemRecord CopyRecord(SavedItemRecord source)
        {
            return new SavedItemRecord
            {
                itemId = source.itemId,
                instanceId = source.instanceId,
                quantity = source.quantity,
                currentDurability = source.currentDurability,
                effectiveMaxDurability = source.effectiveMaxDurability,
                currentMagazine = source.currentMagazine
            };
        }

        private static void CopyRecord(SavedItemRecord source, SavedItemRecord destination)
        {
            destination.itemId = source.itemId;
            destination.instanceId = source.instanceId;
            destination.quantity = source.quantity;
            destination.currentDurability = source.currentDurability;
            destination.effectiveMaxDurability = source.effectiveMaxDurability;
            destination.currentMagazine = source.currentMagazine;
        }

        private static void ClearRecords(List<SavedItemRecord> slots)
        {
            foreach (SavedItemRecord slot in slots) slot.Clear();
        }
    }
}

using System.IO;
using System.Linq;
using Looter.Combat;
using Looter.Economy;
using Looter.Hub;
using Looter.Items;
using Looter.Raid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Looter.EditorTools
{
    public static class DroneSystemInstaller
    {
        private const string ItemFolder = "Assets/Data/Items";
        private const string ConfigFolder = "Assets/Data/Configs";
        private const string CatalogPath = "Assets/Data/Catalogs/ItemCatalog.asset";
        private const string ShopPath = "Assets/Data/Catalogs/ShopCatalog.asset";
        private const string SettingsPath = ConfigFolder + "/DroneSystemSettings.asset";
        private const string KitPath = ItemFolder + "/repair_kit.asset";
        private const string HubScenePath = "Assets/Scenes/BaseHub.unity";
        private const string RaidScenePath = "Assets/Scenes/FoundationPlayground.unity";
        private static readonly int[] SuggestedDurability = { 60, 90, 130, 180, 240, 320 };

        [MenuItem("Pixel Looter/Tích hợp Milestone 1.6 — Drone & Damage System")]
        public static void Install()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(ConfigFolder);
            ItemDefinition[] existing = AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemFolder })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>)
                .Where(item => item != null).ToArray();
            ItemDefinition existingDrone = existing.FirstOrDefault(item => item.Type == ItemType.Drone && item.Tier == ItemTier.II);
            if (existingDrone == null)
            {
                EditorUtility.DisplayDialog("Thiếu Drone II", "Không tìm thấy ItemDefinition drone bậc II hiện có. Chưa thay đổi scene.", "Đóng");
                return;
            }

            Sprite droneSprite = existingDrone.DroneWorldSprite != null ? existingDrone.DroneWorldSprite : existingDrone.Icon;
            ItemDefinition[] drones = CreateDroneDefinitions(existing, existingDrone, droneSprite);
            ItemDefinition repairKit = CreateOrUpdateRepairKit(existing);
            DroneSystemSettings settings = CreateOrUpdateSettings(repairKit);
            UpdateCatalogs(drones.Append(repairKit).ToArray());
            ConfigureScene(HubScenePath, settings, true);
            ConfigureScene(RaidScenePath, settings, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Dữ liệu sáu bậc drone, Repair Kit, cấu hình, catalog và tham chiếu M1.6 đã được tích hợp lặp an toàn.");
            EditorUtility.DisplayDialog("Đã tích hợp M1.6", "Đã cập nhật dữ liệu và tham chiếu scene mà không dựng lại scene. Chờ Unity biên dịch rồi kiểm tra Console và checklist TASKS.md.", "Đóng");
        }

        private static ItemDefinition[] CreateDroneDefinitions(ItemDefinition[] existing, ItemDefinition droneTwo, Sprite sprite)
        {
            ItemDefinition[] result = new ItemDefinition[6];
            for (int tier = 1; tier <= 6; tier++)
            {
                ItemDefinition definition = existing.FirstOrDefault(item => item.Type == ItemType.Drone && (int)item.Tier == tier);
                if (definition == null && tier == 2) definition = droneTwo;
                bool createdNew = false;
                if (definition == null)
                {
                    string assetPath = $"{ItemFolder}/drone_{Roman(tier)}.asset";
                    definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
                    if (definition == null)
                    {
                        definition = ScriptableObject.CreateInstance<ItemDefinition>();
                        AssetDatabase.CreateAsset(definition, assetPath);
                        createdNew = true;
                    }
                    SerializedObject serialized = new SerializedObject(definition);
                    SetString(serialized, "itemId", $"drone_{Roman(tier).ToLowerInvariant()}");
                    SetString(serialized, "displayName", $"Drone trinh sát bậc {Roman(tier)}");
                    SetString(serialized, "description", "Drone hỗ trợ phòng thủ; độ bền được lưu riêng theo từng vật phẩm.");
                    serialized.FindProperty("icon").objectReferenceValue = sprite;
                    serialized.FindProperty("itemType").intValue = (int)ItemType.Drone;
                    serialized.FindProperty("tier").intValue = tier;
                    serialized.FindProperty("sellValue").intValue = tier * 120;
                    serialized.FindProperty("buyPrice").intValue = tier * 180;
                    serialized.FindProperty("backpackCapacity").intValue = 10;
                    serialized.FindProperty("isStackable").boolValue = false;
                    serialized.FindProperty("maxStackSize").intValue = 1;
                    serialized.FindProperty("allowedInSafeSlots").boolValue = false;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                SerializedObject droneData = new SerializedObject(definition);
                SerializedProperty maxDurability = droneData.FindProperty("baseMaxDurability");
                if (createdNew || maxDurability.intValue <= 0 || (definition == droneTwo && maxDurability.intValue == 90))
                    maxDurability.intValue = SuggestedDurability[tier - 1];
                if (droneData.FindProperty("droneWorldSprite").objectReferenceValue == null)
                    droneData.FindProperty("droneWorldSprite").objectReferenceValue = sprite;
                droneData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
                result[tier - 1] = definition;
            }
            return result;
        }

        private static ItemDefinition CreateOrUpdateRepairKit(ItemDefinition[] existing)
        {
            ItemDefinition kit = existing.FirstOrDefault(item => item.Id == "repair_kit") ??
                                 AssetDatabase.LoadAssetAtPath<ItemDefinition>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }
            ItemDefinition medkit = existing.FirstOrDefault(item => item.Id == "field_medkit");
            SerializedObject serialized = new SerializedObject(kit);
            SetString(serialized, "itemId", "repair_kit");
            SetString(serialized, "displayName", "Repair Kit");
            SetString(serialized, "description", "Dùng trong raid để phục hồi một phần độ bền drone; mỗi lần sửa làm giảm độ bền tối đa.");
            if (serialized.FindProperty("icon").objectReferenceValue == null && medkit != null)
                serialized.FindProperty("icon").objectReferenceValue = medkit.Icon;
            serialized.FindProperty("itemType").intValue = (int)ItemType.Consumable;
            serialized.FindProperty("tier").intValue = (int)ItemTier.I;
            serialized.FindProperty("sellValue").intValue = 35;
            serialized.FindProperty("buyPrice").intValue = 100;
            serialized.FindProperty("isStackable").boolValue = false;
            serialized.FindProperty("maxStackSize").intValue = 1;
            serialized.FindProperty("allowedInSafeSlots").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(kit);
            return kit;
        }

        private static DroneSystemSettings CreateOrUpdateSettings(ItemDefinition repairKit)
        {
            DroneSystemSettings settings = AssetDatabase.LoadAssetAtPath<DroneSystemSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DroneSystemSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            SerializedObject serialized = new SerializedObject(settings);
            serialized.FindProperty("repairKit").objectReferenceValue = repairKit;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void UpdateCatalogs(ItemDefinition[] items)
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            if (catalog != null)
            {
                SerializedObject serialized = new SerializedObject(catalog);
                SerializedProperty definitions = serialized.FindProperty("definitions");
                foreach (ItemDefinition item in items)
                {
                    bool found = false;
                    for (int i = 0; i < definitions.arraySize; i++)
                        if (definitions.GetArrayElementAtIndex(i).objectReferenceValue == item) { found = true; break; }
                    if (!found) definitions.GetArrayElementAtIndex(definitions.arraySize++).objectReferenceValue = item;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
            }

            ShopCatalog shop = AssetDatabase.LoadAssetAtPath<ShopCatalog>(ShopPath);
            if (shop == null) return;
            SerializedObject shopSerialized = new SerializedObject(shop);
            SerializedProperty offers = shopSerialized.FindProperty("offers");
            foreach (ItemDefinition item in items)
            {
                int foundIndex = -1;
                for (int i = 0; i < offers.arraySize; i++)
                    if (offers.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item)
                    { foundIndex = i; break; }
                if (foundIndex >= 0) continue;
                SerializedProperty offer = offers.GetArrayElementAtIndex(offers.arraySize++);
                offer.FindPropertyRelative("item").objectReferenceValue = item;
                offer.FindPropertyRelative("buyPrice").intValue = item.BuyPrice;
            }
            shopSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shop);
        }

        private static void ConfigureScene(string scenePath, DroneSystemSettings settings, bool hub)
        {
            if (!File.Exists(scenePath)) return;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (hub)
            {
                BaseHubController controller = Object.FindAnyObjectByType<BaseHubController>();
                if (controller != null)
                {
                    SerializedObject serialized = new SerializedObject(controller);
                    serialized.FindProperty("droneSettings").objectReferenceValue = settings;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(controller);
                }
            }
            else
            {
                RaidManager manager = Object.FindAnyObjectByType<RaidManager>();
                if (manager != null)
                {
                    SerializedObject serialized = new SerializedObject(manager);
                    serialized.FindProperty("droneSettings").objectReferenceValue = settings;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(manager);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void SetString(SerializedObject serialized, string property, string value) =>
            serialized.FindProperty(property).stringValue = value;

        private static string Roman(int tier) => tier switch
        {
            1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => "VI"
        };

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

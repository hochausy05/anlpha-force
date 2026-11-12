using System.IO;
using Looter.Combat;
using Looter.Inventory;
using Looter.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Looter.EditorTools
{
    public static class LootInventorySceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/FoundationPlayground.unity";
        private const string ItemFolder = "Assets/Data/Items";
        private const string LootFolder = "Assets/Data/LootTables";
        private const string PhysicsFolder = "Assets/Data/Physics";
        private const string IconFolder = "Assets/PixelLooter/Art/Items";
        private const string ContainerArtFolder = "Assets/PixelLooter/Art/Containers";
        private const string NoFrictionPath = PhysicsFolder + "/NoFriction.physicsMaterial2D";
        private const float GroundTop = -3f;

        [MenuItem("Pixel Looter/Tích hợp Loot & Inventory vào FoundationPlayground")]
        public static void InstallLootAndInventory()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                EditorUtility.DisplayDialog("Chưa mở đúng scene", "Hãy mở Assets/Scenes/FoundationPlayground.unity trước khi tích hợp loot.", "Đóng");
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Không tìm thấy nhân vật", "Scene không có GameObject Player. Không có thay đổi nào được lưu.", "Đóng");
                return;
            }

            EnsureFolders();
            ItemDefinition[] items = CreateSampleItems();
            LootTableDefinition groundTable = CreateLootTable("GroundLootTable", items, new float[]
                { 35f, 35f, 12.5f, 12.5f, 2.5f, 2.5f, 0, 0, 0, 0, 0, 0 }, 1, 1, 1, 3);
            LootTableDefinition boxTable = CreateLootTable("WoodenBoxLootTable", items, new float[]
                { 35f, 35f, 15f, 15f }, 1, 2, 1, 4);
            LootTableDefinition crateTable = CreateLootTable("WoodenCrateLootTable", items, new float[]
                { 25f, 25f, 17.5f, 17.5f, 7.5f, 7.5f }, 2, 3, 1, 5);
            LootTableDefinition chestTable = CreateLootTable("CommonChestLootTable", items, new float[]
                { 12.5f, 12.5f, 17.5f, 17.5f, 15f, 15f, 5f, 5f }, 2, 3, 1, 5);
            ItemDefinition[] enemyEquipment = CreateEnemyEquipmentItems();
            ItemDefinition medkit = CreateItem(new ItemConfig("field_medkit", "Bộ cứu thương", "Vật tư hồi phục mang theo.",
                ItemType.Consumable, ItemTier.I, 35), 12);
            ItemDefinition[] corpseItems = new ItemDefinition[items.Length + 1];
            items.CopyTo(corpseItems, 0);
            corpseItems[items.Length] = medkit;
            LootTableDefinition enemyTable = CreateLootTable("CommonEnemyCorpseLootTable", corpseItems, new float[]
                { 28f, 28f, 20f, 14f, 3f, 3f, 0, 0, 0, 0, 0, 0, 4f }, 0, 3, 1, 2);
            Sprite corpseSprite = CreateEnemyCorpseSprite();
            PhysicsMaterial2D noFriction = GetOrCreateNoFrictionMaterial();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Tích hợp Loot và Inventory");
            int undoGroup = Undo.GetCurrentGroup();

            GetOrAdd<InventoryModel>(player);
            GetOrAdd<InventoryPresenter>(player);
            GetOrAdd<WorldInteractionController>(player);
            int playerLayer = LayerMask.NameToLayer("PlayerCharacter");
            if (playerLayer >= 0) FoundationHierarchyOrganizer.SetColliderLayers(player, playerLayer);
            ApplyNoFrictionToPlayerAndTerrain(player, noFriction);
            AddLootToEnemies(enemyTable, corpseSprite, enemyEquipment);
            CreateSpawnPoints(groundTable);
            CreateContainer("WoodenBox — Hộp gỗ", LootContainerType.WoodenBox, boxTable, -8f, 0);
            CreateContainer("WoodenCrate — Thùng gỗ", LootContainerType.WoodenCrate, crateTable, 2f, 1);
            CreateContainer("CommonChest — Rương thường", LootContainerType.CommonChest, chestTable, 13f, 2);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Loot, Inventory và material không ma sát đã được tích hợp mà không dựng lại FoundationPlayground.");
            EditorUtility.DisplayDialog("Đã tích hợp Loot & Inventory", "Scene đã được bổ sung item mẫu, điểm loot, container, inventory và ô an toàn. Nhấn Play để kiểm thử.", "Đóng");
        }

        private static void EnsureFolders()
        {
            EnsureFolder(ItemFolder);
            EnsureFolder(LootFolder);
            EnsureFolder(PhysicsFolder);
            EnsureFolder(IconFolder);
            EnsureFolder(ContainerArtFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static ItemDefinition[] CreateSampleItems()
        {
            ItemConfig[] configs =
            {
                new ItemConfig("scrap_iron", "Mảnh sắt gỉ", "Mảnh kim loại cũ còn dùng được.", ItemType.Material, ItemTier.I, 20),
                new ItemConfig("rubble_stone", "Đá vụn", "Đá thô nhặt được từ tàn tích.", ItemType.Material, ItemTier.I, 30),
                new ItemConfig("copper_ore", "Quặng đồng", "Quặng đồng có thể trao đổi.", ItemType.Material, ItemTier.II, 70),
                new ItemConfig("old_component", "Linh kiện cũ", "Linh kiện máy móc đã qua sử dụng.", ItemType.Valuable, ItemTier.II, 110),
                new ItemConfig("silver_ore", "Quặng bạc", "Quặng bạc tinh khiết.", ItemType.Material, ItemTier.III, 240),
                new ItemConfig("energy_crystal", "Tinh thể năng lượng", "Tinh thể phát sáng yếu.", ItemType.Valuable, ItemTier.III, 360),
                new ItemConfig("energy_core", "Lõi năng lượng", "Lõi năng lượng còn hoạt động.", ItemType.Material, ItemTier.IV, 900),
                new ItemConfig("magic_relic", "Cổ vật ma thuật", "Cổ vật từ một nền văn minh đã mất.", ItemType.Valuable, ItemTier.IV, 1400),
                new ItemConfig("gold_crystal", "Tinh thể vàng", "Tinh thể hiếm có giá trị cao.", ItemType.Valuable, ItemTier.V, 3500),
                new ItemConfig("ancient_processor", "Bộ xử lý cổ đại", "Vi xử lý công nghệ tiền đại.", ItemType.Material, ItemTier.V, 5500),
                new ItemConfig("red_crystal", "Tinh thể đỏ", "Tinh thể đỏ chứa năng lượng bất ổn.", ItemType.Valuable, ItemTier.VI, 15000),
                new ItemConfig("lost_relic", "Di vật thất lạc", "Di vật cực hiếm của thế giới cũ.", ItemType.Valuable, ItemTier.VI, 23000)
            };

            ItemDefinition[] results = new ItemDefinition[configs.Length];
            for (int i = 0; i < configs.Length; i++) results[i] = CreateItem(configs[i], i);
            return results;
        }

        private static ItemDefinition[] CreateEnemyEquipmentItems()
        {
            ItemConfig[] configs =
            {
                new ItemConfig("bot_knife_i", "Dao cũ", "Vũ khí cận chiến bậc I bot mang theo.", ItemType.Weapon, ItemTier.I, 80),
                new ItemConfig("bot_sword_iii", "Kiếm máy bậc III", "Vũ khí hiếm bot mạnh có thể mang theo.", ItemType.Weapon, ItemTier.III, 1200),
                new ItemConfig("bot_backpack_i", "Balo dã chiến bậc I", "Balo cơ bản bot có thể mang theo.", ItemType.Backpack, ItemTier.I, 60),
                new ItemConfig("bot_backpack_ii", "Balo dã chiến bậc II", "Balo cải tiến bot đôi khi mang theo.", ItemType.Backpack, ItemTier.II, 280),
                new ItemConfig("bot_drone_ii", "Drone trinh sát bậc II", "Drone bậc II bot hiếm khi mang theo.", ItemType.Drone, ItemTier.II, 450)
            };
            ItemDefinition[] results = new ItemDefinition[configs.Length];
            for (int i = 0; i < configs.Length; i++) results[i] = CreateItem(configs[i], i + 13);
            return results;
        }

        private static ItemDefinition CreateItem(ItemConfig config, int iconVariant)
        {
            string path = $"{ItemFolder}/{config.Id}.asset";
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            bool created = definition == null;
            if (created)
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            Sprite icon = CreateItemIcon(config.Id, config.Tier, iconVariant);
            SerializedObject serialized = new SerializedObject(definition);
            if (created || string.IsNullOrEmpty(serialized.FindProperty("itemId").stringValue))
            {
                serialized.FindProperty("itemId").stringValue = config.Id;
                serialized.FindProperty("displayName").stringValue = config.Name;
                serialized.FindProperty("description").stringValue = config.Description;
                serialized.FindProperty("itemType").enumValueIndex = (int)config.Type;
                serialized.FindProperty("tier").enumValueIndex = (int)config.Tier - 1;
                serialized.FindProperty("sellValue").intValue = config.Value;
                serialized.FindProperty("isStackable").boolValue = false;
                serialized.FindProperty("maxStackSize").intValue = 1;
                serialized.FindProperty("allowedInSafeSlots").boolValue = true;
            }
            if (serialized.FindProperty("icon").objectReferenceValue == null)
                serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static Sprite CreateItemIcon(string id, ItemTier tier, int variant)
        {
            string path = $"{IconFolder}/{id}.png";
            if (!File.Exists(path))
            {
                Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                Color32 clear = new Color32(0, 0, 0, 0);
                Color32 main = ItemDefinition.GetTierColor(tier);
                Color32 dark = new Color32(35, 39, 45, 255);
                Color32 highlight = new Color32(235, 238, 244, 255);
                Color32[] pixels = new Color32[256];
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        int dx = Mathf.Abs(x - 7), dy = Mathf.Abs(y - 7);
                        bool shape = (variant % 3) switch
                        {
                            0 => dx + dy <= 6,
                            1 => x >= 3 && x <= 12 && y >= 3 && y <= 12,
                            _ => dx <= 5 && dy <= 5 && (dx + dy <= 8 || x == 7 || y == 7)
                        };
                        Color32 color = !shape ? clear : (dx + dy >= 6 ? dark : main);
                        if (shape && ((x + variant) % 7 == 0 || (y + variant) % 6 == 0)) color = highlight;
                        pixels[y * 16 + x] = color;
                    }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.spritePixelsPerUnit != 16 || importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static LootTableDefinition CreateLootTable(string name, ItemDefinition[] items, float[] weights,
            int minimumDrops, int maximumDrops, int minimumQuantity, int maximumQuantity)
        {
            string path = $"{LootFolder}/{name}.asset";
            LootTableDefinition table = AssetDatabase.LoadAssetAtPath<LootTableDefinition>(path);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LootTableDefinition>();
                AssetDatabase.CreateAsset(table, path);
            }

            SerializedObject serialized = new SerializedObject(table);
            SerializedProperty entries = serialized.FindProperty("entries");
            if (entries.arraySize == 0)
            {
                entries.arraySize = items.Length;
                for (int i = 0; i < items.Length; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("item").objectReferenceValue = items[i];
                    entry.FindPropertyRelative("weight").floatValue = i < weights.Length ? weights[i] : 0f;
                    entry.FindPropertyRelative("minimumQuantity").intValue = minimumQuantity;
                    entry.FindPropertyRelative("maximumQuantity").intValue = maximumQuantity;
                }
                serialized.FindProperty("minimumDrops").intValue = minimumDrops;
                serialized.FindProperty("maximumDrops").intValue = maximumDrops;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(table);
            }
            return table;
        }

        private static PhysicsMaterial2D GetOrCreateNoFrictionMaterial()
        {
            PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);
            if (material != null) return material;
            material = new PhysicsMaterial2D("No Friction")
            {
                friction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine2D.Minimum,
                bounceCombine = PhysicsMaterialCombine2D.Minimum
            };
            AssetDatabase.CreateAsset(material, NoFrictionPath);
            return material;
        }

        private static void ApplyNoFrictionToPlayerAndTerrain(GameObject player, PhysicsMaterial2D material)
        {
            CapsuleCollider2D playerCollider = player.GetComponent<CapsuleCollider2D>();
            if (playerCollider != null)
            {
                Undo.RecordObject(playerCollider, "Bỏ ma sát ở nhân vật");
                playerCollider.sharedMaterial = material;
            }

            foreach (Collider2D collider in Object.FindObjectsByType<Collider2D>())
            {
                if (collider == null || collider.isTrigger || collider.gameObject == player) continue;
                string objectName = collider.gameObject.name;
                bool terrain = objectName == "Ground" || objectName.StartsWith("Platform_") || objectName.EndsWith("Boundary");
                if (!terrain || collider.sharedMaterial == material) continue;
                Undo.RecordObject(collider, "Bỏ ma sát ở địa hình");
                collider.sharedMaterial = material;
            }
        }

        private static void AddLootToEnemies(LootTableDefinition table, Sprite corpseSprite, ItemDefinition[] equipment)
        {
            EnemyItemOption[] weapons =
            {
                new EnemyItemOption { Item = equipment[0], Weight = 95f },
                new EnemyItemOption { Item = equipment[1], Weight = 5f }
            };
            EnemyItemOption[] backpacks =
            {
                new EnemyItemOption { Item = equipment[2], Weight = 85f },
                new EnemyItemOption { Item = equipment[3], Weight = 15f }
            };
            EnemyItemOption[] drones =
            {
                new EnemyItemOption { Item = equipment[4], Weight = 100f }
            };

            foreach (EnemyAI enemy in Object.FindObjectsByType<EnemyAI>())
            {
                if (enemy == null) continue;
                EnemyInventory inventory = enemy.GetComponent<EnemyInventory>();
                int enemyLayer = LayerMask.NameToLayer("EnemyCharacter");
                if (enemyLayer >= 0) FoundationHierarchyOrganizer.SetColliderLayers(enemy.gameObject, enemyLayer);
                if (inventory == null) inventory = Undo.AddComponent<EnemyInventory>(enemy.gameObject);
                Undo.RecordObject(inventory, "Cấu hình trang bị và túi đồ bot");
                inventory.Configure(weapons, backpacks, drones, table);

                EnemyLootDrop drop = enemy.GetComponent<EnemyLootDrop>();
                if (drop == null) drop = Undo.AddComponent<EnemyLootDrop>(enemy.gameObject);
                Undo.RecordObject(drop, "Cấu hình hòm xác bot");
                drop.LootTable = table;
                drop.CorpseSprite = corpseSprite;
            }
        }

        private static void CreateSpawnPoints(LootTableDefinition table)
        {
            float[] positions = { -14f, -3f, 6f, 17f };
            for (int i = 0; i < positions.Length; i++)
            {
                string name = $"World Loot Spawn {i + 1}";
                GameObject point = GameObject.Find(name);
                if (point == null)
                {
                    point = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(point, "Tạo điểm xuất hiện loot");
                    point.transform.position = new Vector3(positions[i], GroundTop, 0f);
                }
                WorldLootSpawnPoint spawnPoint = point.GetComponent<WorldLootSpawnPoint>();
                if (spawnPoint == null) spawnPoint = Undo.AddComponent<WorldLootSpawnPoint>(point);
                if (spawnPoint.LootTable == null) spawnPoint.LootTable = table;
                FoundationHierarchyOrganizer.ParentObject(point, "Spawns/WorldLoot");
            }
        }

        private static void CreateContainer(string objectName, LootContainerType type, LootTableDefinition table,
            float x, int spriteVariant)
        {
            GameObject containerObject = GameObject.Find(objectName);
            bool created = containerObject == null;
            if (created)
            {
                containerObject = new GameObject(objectName);
                Undo.RegisterCreatedObjectUndo(containerObject, "Tạo container loot");
                containerObject.transform.position = new Vector3(x, GroundTop + 0.48f, 0f);
            }

            SpriteRenderer renderer = containerObject.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(containerObject);
            if (created || renderer.sprite == null) renderer.sprite = CreateContainerSprite(type, spriteVariant);
            renderer.sortingOrder = 1;

            BoxCollider2D collider = containerObject.GetComponent<BoxCollider2D>();
            if (collider == null) collider = Undo.AddComponent<BoxCollider2D>(containerObject);
            collider.isTrigger = true;
            collider.size = new Vector2(1f, 0.8f);

            LootContainer container = containerObject.GetComponent<LootContainer>();
            if (container == null) container = Undo.AddComponent<LootContainer>(containerObject);
            FoundationHierarchyOrganizer.ParentObject(containerObject, "Interactables");
            if (created) container.Type = type;
            if (container.LootTable == null) container.LootTable = table;
        }

        private static Sprite CreateContainerSprite(LootContainerType type, int variant)
        {
            string name = type.ToString();
            string path = $"{ContainerArtFolder}/{name}.png";
            if (!File.Exists(path))
            {
                Color32 main = type switch
                {
                    LootContainerType.WoodenBox => new Color32(156, 103, 58, 255),
                    LootContainerType.WoodenCrate => new Color32(128, 78, 44, 255),
                    _ => new Color32(183, 142, 58, 255)
                };
                Color32 dark = new Color32(51, 43, 40, 255);
                Color32 light = new Color32(225, 190, 112, 255);
                Texture2D texture = new Texture2D(16, 12, TextureFormat.RGBA32, false);
                Color32[] pixels = new Color32[16 * 12];
                for (int y = 0; y < 12; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        bool body = x >= 1 && x <= 14 && y >= 1 && y <= 9;
                        bool border = body && (x == 1 || x == 14 || y == 1 || y == 9);
                        bool detail = type == LootContainerType.WoodenCrate ? (x == 5 || x == 10 || y == 5) :
                            type == LootContainerType.CommonChest && ((x == 7 || x == 8) && y >= 3 && y <= 6);
                        pixels[y * 16 + x] = !body ? new Color32(0, 0, 0, 0) :
                            border ? dark : detail ? light : ((x + y + variant) % 5 == 0 ? light : main);
                    }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single || importer.spritePixelsPerUnit != 16 ||
                importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite CreateEnemyCorpseSprite()
        {
            const string path = ContainerArtFolder + "/EnemyCorpse.png";
            if (!File.Exists(path))
            {
                const int width = 16;
                const int height = 16;
                Color32 clear = new Color32(0, 0, 0, 0);
                Color32 outline = new Color32(42, 38, 39, 255);
                Color32 body = new Color32(105, 113, 102, 255);
                Color32 accent = new Color32(155, 104, 57, 255);
                Color32[] pixels = new Color32[width * height];
                for (int y = 3; y <= 10; y++)
                    for (int x = 1; x <= 14; x++)
                    {
                        bool silhouette = (x >= 2 && x <= 12 && y >= 5 && y <= 9) ||
                            (x >= 10 && x <= 14 && y >= 4 && y <= 9) ||
                            (x >= 3 && x <= 6 && y >= 9 && y <= 11);
                        if (!silhouette) continue;
                        bool edge = x == 2 || x == 12 || y == 5 || y == 9 || (x == 14 && y == 4);
                        pixels[y * width + x] = edge ? outline :
                            (x >= 11 && y >= 6 && y <= 7 ? accent : body);
                    }
                Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single || importer.spritePixelsPerUnit != 16 ||
                importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        private sealed class ItemConfig
        {
            public string Id { get; }
            public string Name { get; }
            public string Description { get; }
            public ItemType Type { get; }
            public ItemTier Tier { get; }
            public int Value { get; }
            public ItemConfig(string id, string name, string description, ItemType type, ItemTier tier, int value)
            {
                Id = id; Name = name; Description = description; Type = type; Tier = tier; Value = value;
            }
        }
    }
}

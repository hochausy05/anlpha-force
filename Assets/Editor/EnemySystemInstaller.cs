using System;
using System.Collections.Generic;
using System.IO;
using Looter.Combat;
using Looter.Foundation;
using Looter.Items;
using Looter.Raid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Looter.EditorTools
{
    public static class EnemySystemInstaller
    {
        private const string ScenePath = "Assets/Scenes/FoundationPlayground.unity";
        private const string EnemyDataPath = "Assets/Data/Enemies";
        private const string PrefabPath = "Assets/Prefabs/Enemies";
        private const string ArtPath = "Assets/PixelLooter/Art/Enemies";

        [MenuItem("Pixel Looter/Tích hợp Milestone 1.8 — Enemy & Boss")]
        public static void Install()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                EditorUtility.DisplayDialog("Pixel Looter", "Mở FoundationPlayground trước khi tích hợp M1.8. Không có scene nào bị thay đổi.", "Đóng");
                return;
            }
            if (GameObject.FindGameObjectWithTag("Player") == null ||
                AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Data/Catalogs/ItemCatalog.asset") == null)
            {
                EditorUtility.DisplayDialog("Pixel Looter", "Thiếu Player hoặc ItemCatalog. Scene chưa được chỉnh sửa.", "Đóng");
                return;
            }

            EnsureFolder("Assets/Data", "Enemies");
            EnsureFolder("Assets/Prefabs", "Enemies");
            EnsureFolder("Assets/PixelLooter/Art", "Enemies");
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Data/Catalogs/ItemCatalog.asset");
            Sprite slimeSprite = CreateSprite("slime_placeholder", new Color32(93, 205, 113, 255), 0);
            Sprite raiderSprite = CreateSprite("raider_placeholder", new Color32(196, 130, 67, 255), 1);
            Sprite archerSprite = CreateSprite("archer_placeholder", new Color32(79, 163, 194, 255), 2);
            Sprite eliteSprite = CreateSprite("elite_placeholder", new Color32(176, 81, 196, 255), 3);
            Sprite bossSprite = CreateSprite("ruins_warden_placeholder", new Color32(202, 64, 62, 255), 4);
            Sprite corpseSprite = CreateSprite("boss_corpse_placeholder", new Color32(91, 61, 69, 255), 5);
            Sprite chestSprite = CreateSprite("boss_chest_placeholder", new Color32(231, 166, 48, 255), 6);

            ItemDefinition knifeI = catalog.Find("bot_knife_i");
            ItemDefinition knifeII = catalog.Find("bot_knife_II");
            ItemDefinition bowI = catalog.Find("bow_I");
            ItemDefinition bowII = catalog.Find("bow_II");
            ItemDefinition swordIII = catalog.Find("bot_sword_iii");
            ItemDefinition swordIV = catalog.Find("bot_sword_IV");
            ItemDefinition pistolIII = catalog.Find("pistol_III");
            ItemDefinition backpackI = catalog.Find("bot_backpack_i");
            ItemDefinition backpackII = catalog.Find("bot_backpack_ii");
            ItemDefinition droneII = catalog.Find("bot_drone_ii");
            ItemDefinition energyCore = catalog.Find("energy_core");
            ItemDefinition ancientProcessor = catalog.Find("ancient_processor");
            ItemDefinition redCrystal = catalog.Find("red_crystal");
            if (knifeI == null || bowI == null || swordIII == null || pistolIII == null || energyCore == null)
            {
                EditorUtility.DisplayDialog("Pixel Looter", "Thiếu item/vũ khí M1.7 trong catalog. Hãy chạy installer vũ khí trước.", "Đóng");
                return;
            }

            LootTableDefinition commonLoot = AssetDatabase.LoadAssetAtPath<LootTableDefinition>("Assets/Data/LootTables/CommonEnemyLootTable.asset");
            LootTableDefinition eliteLoot = CreateLootTable("EliteGuardLootTable", null,
                (catalog.Find("energy_crystal"), 0.48f), (ancientProcessor, 0.18f), (redCrystal, 0.04f), (catalog.Find("scrap_iron"), 0.65f));
            LootTableDefinition bossLoot = CreateLootTable("BossCorpseLootTable", null,
                (energyCore, 0.7f), (ancientProcessor, 0.25f), (redCrystal, 0.08f));
            LootTableDefinition bossChestLoot = CreateLootTable("BossChestLootTable", energyCore,
                (ancientProcessor, 0.12f), (redCrystal, 0.025f));

            EnemyDefinition slime = CreateEnemyDefinition("Slime", "Slime", EnemyArchetype.Slime,
                40, 8, ItemTier.I, 2f, 5f, 1.2f, 1.2f, 9f, 2.5f, 0.45f, null, 0f, 0, new Color32(155, 247, 157, 255));
            EnemyDefinition raider = CreateEnemyDefinition("Raider", "Raider", EnemyArchetype.Raider,
                65, 10, ItemTier.I, 2.8f, 7f, 1.5f, 1.15f, 10f, 3.5f, 0.3f, knifeI, 1.2f, 0, new Color32(240, 194, 130, 255));
            EnemyDefinition archer = CreateEnemyDefinition("Archer", "Archer", EnemyArchetype.Archer,
                50, 8, ItemTier.I, 2.2f, 7f, 9f, 1.35f, 10f, 2.5f, 0.4f, bowI, 5f, 12, new Color32(156, 225, 242, 255));
            EnemyDefinition elite = CreateEnemyDefinition("EliteGuard", "Elite Guard", EnemyArchetype.EliteGuard,
                150, 18, ItemTier.III, 3f, 10f, 1.7f, 0.95f, 11f, 1f, 0.5f, swordIII, 1.3f, 18, new Color32(232, 157, 255, 255));
            BossDefinition bossDefinition = CreateBossDefinition(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/PistolProjectile.prefab"));

            GameObject slimePrefab = CreateEnemyPrefab("Slime", slime, slimeSprite, commonLoot, commonLoot,
                Array.Empty<EnemyItemOption>(), new[] { Option(backpackI, 1f) }, new[] { Option(droneII, 1f) }, 0f, 0.12f);
            GameObject raiderPrefab = CreateEnemyPrefab("Raider", raider, raiderSprite, commonLoot, commonLoot,
                new[] { Option(knifeI, 0.65f), Option(knifeII, 0.35f) }, new[] { Option(backpackI, 1f), Option(backpackII, 0.25f) },
                new[] { Option(droneII, 1f) }, 0.95f, 0.3f);
            GameObject archerPrefab = CreateEnemyPrefab("Archer", archer, archerSprite, commonLoot, commonLoot,
                new[] { Option(bowI, 0.75f), Option(bowII, 0.25f) }, new[] { Option(backpackI, 1f), Option(backpackII, 0.2f) },
                new[] { Option(droneII, 1f) }, 1f, 0.25f);
            GameObject elitePrefab = CreateEnemyPrefab("EliteGuard", elite, eliteSprite, eliteLoot, eliteLoot,
                new[] { Option(swordIII, 0.65f), Option(swordIV, 0.25f), Option(pistolIII, 0.1f) },
                new[] { Option(backpackII, 1f) }, new[] { Option(droneII, 1f) }, 1f, 0.55f);
            GameObject bossPrefab = CreateBossPrefab(bossDefinition, bossSprite, corpseSprite, bossLoot, pistolIII);

            GameObject managerObject = GameObject.Find("Enemy Spawn Director");
            bool directorCreated = managerObject == null;
            if (directorCreated) managerObject = new GameObject("Enemy Spawn Director");
            EnemySpawnDirector director = GetOrAdd<EnemySpawnDirector>(managerObject);
            FoundationHierarchyOrganizer.ParentObject(managerObject, "Systems");
            if (directorCreated)
            {
                SerializedObject directorData = new SerializedObject(director);
                directorData.FindProperty("maximumActiveEnemies").intValue = 16;
                directorData.FindProperty("minimumPlayerDistance").floatValue = 4f;
                directorData.FindProperty("minimumExtractionDistance").floatValue = 4f;
                directorData.ApplyModifiedProperties();
            }

            float groundY = FindSpawnY();
            CreateSpawn("M18 Spawn Safe Slime A", -13f, groundY, slimePrefab, slime, EnemySpawnZone.Safe, 2, 3.5f, false, null);
            CreateSpawn("M18 Spawn Safe Raider B", -8f, groundY, raiderPrefab, raider, EnemySpawnZone.Safe, 2, 3.5f, false, null);
            CreateSpawn("M18 Spawn Medium Raider A", -1.5f, groundY, raiderPrefab, raider, EnemySpawnZone.Medium, 2, 3f, false, null);
            CreateSpawn("M18 Spawn Medium Archer B", 4f, groundY, archerPrefab, archer, EnemySpawnZone.Medium, 1, 2f, false, null);
            CreateSpawn("M18 Spawn High Elite", 7.8f, groundY, elitePrefab, elite, EnemySpawnZone.High, 2, 3f, false, null);

            LootContainer bossChest = CreateBossChest(13f, groundY, chestSprite, bossChestLoot);
            BossChestLock bossChestLock = bossChest.GetComponent<BossChestLock>();
            EnemySpawnPoint bossSpawn = CreateSpawn("M18 Boss Spawn", 10f, groundY, bossPrefab, null, EnemySpawnZone.High, 1, 0f, true,
                bossChestLock);
            CreateBossArena("Boss Arena — Vệ Binh Phế Tích", new Vector3(10f, groundY, 0f), bossSpawn, bossChestLock);
            CreateBossHud();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Milestone 1.8 đã tích hợp",
                "Đã thêm cấu hình bot, spawn theo vùng, Boss Arena và rương khóa vào FoundationPlayground. Scene hiện có không bị dựng lại. Chạy Play Mode để kiểm tra.", "Đóng");
        }

        private static EnemyDefinition CreateEnemyDefinition(string id, string displayName, EnemyArchetype archetype,
            int hp, int damage, ItemTier tier, float speed, float detection, float range, float cooldown,
            float leash, float patrol, float idle, ItemDefinition weapon, float preferredRange, int ammo, Color tint)
        {
            string path = $"{EnemyDataPath}/{id}.asset";
            EnemyDefinition asset = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<EnemyDefinition>(); AssetDatabase.CreateAsset(asset, path);
            Set(asset, "enemyId", id); Set(asset, "displayName", displayName); Set(asset, "archetype", (int)archetype);
            Set(asset, "maximumHealth", hp); Set(asset, "unarmedDamage", damage); Set(asset, "attackTier", (int)tier);
            Set(asset, "movementSpeed", speed); Set(asset, "detectionRange", detection); Set(asset, "attackRange", range);
            Set(asset, "attackCooldown", cooldown); Set(asset, "leashRadius", leash); Set(asset, "patrolDistance", patrol);
            Set(asset, "idleDuration", idle); Set(asset, "weapon", weapon); Set(asset, "preferredRange", preferredRange);
            Set(asset, "startingAmmunition", ammo); Set(asset, "visualTint", tint);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static BossDefinition CreateBossDefinition(GameObject projectile)
        {
            const string path = "Assets/Data/Enemies/RuinsWarden.asset";
            BossDefinition asset = AssetDatabase.LoadAssetAtPath<BossDefinition>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<BossDefinition>(); AssetDatabase.CreateAsset(asset, path);
            Set(asset, "displayName", "Vệ Binh Phế Tích — Ruins Warden"); Set(asset, "maximumHealth", 400);
            Set(asset, "attackTier", (int)ItemTier.IV); Set(asset, "meleeDamage", 25); Set(asset, "movementSpeed", 2.5f);
            Set(asset, "meleeRange", 1.65f); Set(asset, "detectionRange", 12f); Set(asset, "leashRadius", 14f);
            Set(asset, "phaseTwoHealthFraction", 0.4f); Set(asset, "attackInterval", 2.1f);
            Set(asset, "heavyWindup", 0.7f); Set(asset, "energyDamage", 20); Set(asset, "projectileSpeed", 9f);
            Set(asset, "projectileRange", 12f); Set(asset, "projectilePrefab", projectile);
            Set(asset, "phaseTwoTint", new Color32(255, 100, 75, 255));
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static GameObject CreateEnemyPrefab(string id, EnemyDefinition definition, Sprite sprite,
            LootTableDefinition inventoryLoot, LootTableDefinition corpseLoot, EnemyItemOption[] weapons,
            EnemyItemOption[] backpacks, EnemyItemOption[] drones, float weaponChance, float droneChance)
        {
            string path = $"{PrefabPath}/{id}.prefab";
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existingPrefab != null) return existingPrefab;
            GameObject root = new GameObject(id);
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = 3;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>(); body.gravityScale = 3f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            CapsuleCollider2D capsule = root.AddComponent<CapsuleCollider2D>(); capsule.size = new Vector2(0.9f, 0.9f);
            CombatHealth health = root.AddComponent<CombatHealth>(); health.Faction = CombatFaction.Enemy;
            health.ConfigureMaximumHealth(definition.MaximumHealth);
            EnemyInventory inventory = root.AddComponent<EnemyInventory>();
            inventory.Configure(weapons, backpacks, drones, inventoryLoot, weaponChance, 0.45f, droneChance);
            EnemyAI ai = root.AddComponent<EnemyAI>(); ai.Configure(definition);
            EnemyLootDrop loot = root.AddComponent<EnemyLootDrop>();
            loot.LootTable = corpseLoot; loot.CorpseSprite = sprite; loot.Elite = definition.Archetype == EnemyArchetype.EliteGuard;
            root.AddComponent<EnemyHealthBar>();
            int enemyLayer = LayerMask.NameToLayer("EnemyCharacter");
            if (enemyLayer >= 0) root.layer = enemyLayer;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBossPrefab(BossDefinition definition, Sprite sprite, Sprite corpseSprite,
            LootTableDefinition lootTable, ItemDefinition weapon)
        {
            const string path = PrefabPath + "/RuinsWarden.prefab";
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existingPrefab != null) return existingPrefab;
            GameObject root = new GameObject("Vệ Binh Phế Tích — Ruins Warden");
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = 4;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>(); body.gravityScale = 3f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            CapsuleCollider2D capsule = root.AddComponent<CapsuleCollider2D>(); capsule.size = new Vector2(1.55f, 1.5f);
            CombatHealth health = root.AddComponent<CombatHealth>(); health.Faction = CombatFaction.Enemy;
            health.ConfigureMaximumHealth(definition.MaximumHealth);
            EnemyInventory inventory = root.AddComponent<EnemyInventory>();
            inventory.Configure(new[] { Option(weapon, 1f) }, Array.Empty<EnemyItemOption>(), Array.Empty<EnemyItemOption>(),
                lootTable, 1f, 0f, 0f, weapon);
            EnemyLootDrop loot = root.AddComponent<EnemyLootDrop>(); loot.LootTable = lootTable; loot.CorpseSprite = corpseSprite; loot.Boss = true;
            BossController boss = root.AddComponent<BossController>(); boss.Configure(definition);
            int enemyLayer = LayerMask.NameToLayer("EnemyCharacter");
            if (enemyLayer >= 0) root.layer = enemyLayer;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static EnemySpawnPoint CreateSpawn(string name, float x, float y, GameObject prefab,
            EnemyDefinition definition, EnemySpawnZone zone, int quota, float spread, bool boss, BossChestLock chest)
        {
            GameObject point = GameObject.Find(name);
            bool created = point == null;
            if (created) { point = new GameObject(name); point.transform.position = new Vector3(x, y, 0f); }
            EnemySpawnPoint spawn = GetOrAdd<EnemySpawnPoint>(point);
            if (created || spawn.EnemyPrefab == null)
                spawn.Configure(prefab, definition, zone, quota, spread, boss, chest);
            FoundationHierarchyOrganizer.ParentObject(point, boss ? "Spawns/Boss" : $"Spawns/Enemies/{zone}");
            return spawn;
        }

        private static LootContainer CreateBossChest(float x, float y, Sprite sprite, LootTableDefinition lootTable)
        {
            GameObject chest = GameObject.Find("BossChest");
            bool created = chest == null;
            if (created) { chest = new GameObject("BossChest"); chest.transform.position = new Vector3(x, y, 0f); }
            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(chest);
            if (created || renderer.sprite == null) renderer.sprite = sprite;
            renderer.sortingOrder = 2;
            BoxCollider2D collider = GetOrAdd<BoxCollider2D>(chest); collider.isTrigger = true;
            collider.size = new Vector2(1.25f, 0.9f);
            LootContainer container = GetOrAdd<LootContainer>(chest);
            container.Type = LootContainerType.BossChest;
            if (container.LootTable == null) container.LootTable = lootTable;
            container.SetLocked(true, "Rương bị khóa khi Vệ Binh Phế Tích còn sống.");
            GetOrAdd<BossChestLock>(chest);
            FoundationHierarchyOrganizer.ParentObject(chest, "Interactables");
            return container;
        }

        private static void CreateBossArena(string name, Vector3 position, EnemySpawnPoint spawn, BossChestLock chest)
        {
            GameObject arena = GameObject.Find(name);
            bool created = arena == null;
            if (created) { arena = new GameObject(name); arena.transform.position = position; }
            BossArena component = arena.GetComponent<BossArena>();
            bool configure = created || component == null;
            if (component == null) component = GetOrAdd<BossArena>(arena);
            if (configure) component.Configure(14f, 8f, spawn, chest);
            FoundationHierarchyOrganizer.ParentObject(arena, "Enemies/Boss Area");
        }

        private static void CreateBossHud()
        {
            GameObject hud = GameObject.Find("Boss Health HUD");
            if (hud == null) hud = new GameObject("Boss Health HUD");
            GetOrAdd<BossHealthBar>(hud);
            FoundationHierarchyOrganizer.ParentObject(hud, "UI");
        }

        private static EnemyItemOption Option(ItemDefinition item, float weight) => new EnemyItemOption { Item = item, Weight = weight };

        private static LootTableDefinition CreateLootTable(string name, ItemDefinition guaranteed,
            params (ItemDefinition item, float weight)[] choices)
        {
            string path = $"Assets/Data/LootTables/{name}.asset";
            LootTableDefinition table = AssetDatabase.LoadAssetAtPath<LootTableDefinition>(path);
            if (table != null) return table;
            table = ScriptableObject.CreateInstance<LootTableDefinition>(); AssetDatabase.CreateAsset(table, path);
            SerializedObject serialized = new SerializedObject(table);
            serialized.FindProperty("minimumDrops").intValue = 1;
            serialized.FindProperty("maximumDrops").intValue = 2;
            serialized.FindProperty("guaranteedItem").objectReferenceValue = guaranteed;
            serialized.FindProperty("guaranteedQuantity").intValue = 1;
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.ClearArray();
            foreach ((ItemDefinition item, float weight) choice in choices)
            {
                if (choice.item == null) continue;
                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("item").objectReferenceValue = choice.item;
                entry.FindPropertyRelative("weight").floatValue = choice.weight;
                entry.FindPropertyRelative("minimumQuantity").intValue = 1;
                entry.FindPropertyRelative("maximumQuantity").intValue = 1;
            }
            serialized.ApplyModifiedProperties(); EditorUtility.SetDirty(table);
            return table;
        }

        private static Sprite CreateSprite(string id, Color32 body, int style)
        {
            string path = $"{ArtPath}/{id}.png";
            Sprite found = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (found != null) return found;
            const int size = 24;
            Color32 clear = new Color32(0, 0, 0, 0), dark = new Color32(35, 31, 48, 255), highlight = new Color32(255, 228, 147, 255);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float nx = (x - 11.5f) / 10f, ny = (y - 11.5f) / 10f;
                bool inside = style == 0 ? nx * nx + ny * ny < 0.88f : Mathf.Abs(nx) < 0.82f && Mathf.Abs(ny) < 0.9f;
                if (style == 4) inside = Mathf.Abs(nx) < 0.94f && Mathf.Abs(ny) < 0.96f && !(y > 19 && x > 17);
                if (style == 6) inside = y > 4 && y < 20 && x > 2 && x < 22;
                if (!inside) { pixels[y * size + x] = clear; continue; }
                bool edge = x < 3 || x > 20 || y < 3 || y > 20;
                bool eyes = (style < 5 && y > 12 && y < 16 && ((x > 6 && x < 9) || (x > 15 && x < 18)));
                bool detail = style == 6 && y > 7 && y < 11 && x > 8 && x < 16;
                pixels[y * size + x] = eyes || detail ? highlight : edge ? dark : body;
            }
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
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

        private static float FindSpawnY()
        {
            RaidSpawnPoint[] points = UnityEngine.Object.FindObjectsByType<RaidSpawnPoint>();
            return points.Length > 0 ? points[0].transform.position.y : -2.42f;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        private static void Set(UnityEngine.Object target, string propertyName, object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            if (value is string text) property.stringValue = text;
            else if (value is int number) property.intValue = number;
            else if (value is float scalar) property.floatValue = scalar;
            else if (value is Color color) property.colorValue = color;
            else if (value is UnityEngine.Object reference) property.objectReferenceValue = reference;
            serialized.ApplyModifiedProperties();
        }

        private static void EnsureFolder(string parent, string folder)
        {
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(parent).Replace('\\', '/'), Path.GetFileName(parent));
            if (!AssetDatabase.IsValidFolder($"{parent}/{folder}")) AssetDatabase.CreateFolder(parent, folder);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using Looter.Combat;
using Looter.Economy;
using Looter.Items;
using Looter.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Looter.EditorTools
{
    public static class WeaponSystemInstaller
    {
        private const string ItemRoot = "Assets/Data/Items";
        private const string WeaponRoot = "Assets/Data/Weapons";
        private const string IconRoot = "Assets/PixelLooter/Art/Weapons";
        private const string PrefabRoot = "Assets/Prefabs/Weapons";

        private sealed class Spec
        {
            public string id, name;
            public ItemTier tier;
            public WeaponAttackMode mode;
            public int damage;
            public float cooldown, range, speed, gravity;
            public int magazine;
            public ItemDefinition ammo;
            public Spec(string itemId, string displayName, ItemTier itemTier, WeaponAttackMode attackMode,
                int baseDamage, float attackCooldown, float attackRange, float projectileSpeed = 12f,
                float projectileGravity = 0f, int magazineSize = 0, ItemDefinition ammunition = null)
            {
                id = itemId; name = displayName; tier = itemTier; mode = attackMode; damage = baseDamage;
                cooldown = attackCooldown; range = attackRange; speed = projectileSpeed; gravity = projectileGravity;
                magazine = magazineSize; ammo = ammunition;
            }
        }

        [MenuItem("Pixel Looter/Tích hợp Milestone 1.7 — Vũ khí & Đạn")]
        public static void Install()
        {
            EnsureFolder("Assets/Data", "Weapons");
            EnsureFolder("Assets/PixelLooter/Art", "Weapons");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Weapons");

            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Data/Catalogs/ItemCatalog.asset");
            ShopCatalog shop = AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Data/Catalogs/ShopCatalog.asset");
            if (catalog == null || shop == null)
            {
                EditorUtility.DisplayDialog("Pixel Looter", "Không tìm thấy ItemCatalog hoặc ShopCatalog hiện có. Không thay đổi scene.", "Đóng");
                return;
            }

            ItemDefinition arrows = CreateAmmo("arrow", "Mũi tên", 2, new Color32(180, 125, 54, 255), 15);
            ItemDefinition pistolAmmo = CreateAmmo("pistol_ammo", "Đạn súng lục", 4, new Color32(235, 185, 55, 255), 15);
            GameObject arrowPrefab = CreateProjectilePrefab("ArrowProjectile", "arrow_projectile", true);
            GameObject bulletPrefab = CreateProjectilePrefab("PistolProjectile", "pistol_projectile", false);

            List<Spec> specs = BuildSpecs(arrows, pistolAmmo, arrowPrefab, bulletPrefab);
            List<ItemDefinition> all = new List<ItemDefinition>();
            foreach (Spec spec in specs) all.Add(CreateWeapon(spec));
            all.Add(arrows);
            all.Add(pistolAmmo);
            AddCatalogItems(catalog, all);
            AddShopOffers(shop, specs, all);
            AddAmmoToLoot("Assets/Data/LootTables/CommonChestLootTable.asset", arrows, pistolAmmo);
            AddAmmoToLoot("Assets/Data/LootTables/WoodenBoxLootTable.asset", arrows, pistolAmmo);
            AddAmmoToLoot("Assets/Data/LootTables/WoodenCrateLootTable.asset", arrows, pistolAmmo);
            AddAmmoToLoot("Assets/Data/LootTables/CommonEnemyCorpseLootTable.asset", arrows, pistolAmmo);
            AddAmmoToLoot("Assets/Data/LootTables/GroundLootTable.asset", arrows, pistolAmmo);
            AddWeaponsToLoot("Assets/Data/LootTables/CommonChestLootTable.asset", all);
            AddWeaponsToLoot("Assets/Data/LootTables/CommonEnemyCorpseLootTable.asset", all);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Pixel Looter", "Đã tích hợp dữ liệu vũ khí/ammo, catalog, shop và loot. Scene không bị dựng lại. Vào FoundationPlayground để kiểm thử M1.7.", "Đóng");
        }

        private static List<Spec> BuildSpecs(ItemDefinition arrows, ItemDefinition pistolAmmo,
            GameObject arrowPrefab, GameObject bulletPrefab)
        {
            List<Spec> result = new List<Spec>
            {
                new Spec("wooden_club_i", "Gậy gỗ", ItemTier.I, WeaponAttackMode.Melee, 12, 0.7f, 1.0f)
            };
            for (int tier = 1; tier <= 3; tier++)
                result.Add(new Spec(tier == 1 ? "bot_knife_i" : $"bot_knife_{Roman(tier)}",
                    $"Dao sắt {Roman(tier)}", (ItemTier)tier, WeaponAttackMode.Melee,
                    20 + (tier - 1) * 8, 0.5f, 1.2f));
            for (int tier = 3; tier <= 6; tier++)
                result.Add(new Spec(tier == 3 ? "bot_sword_iii" : $"bot_sword_{Roman(tier)}",
                    $"Kiếm {Roman(tier)}", (ItemTier)tier, WeaponAttackMode.Melee,
                    35 + (tier - 3) * 8, 0.8f, 1.7f));
            for (int tier = 1; tier <= 4; tier++)
                result.Add(new Spec($"bow_{Roman(tier)}", $"Cung {Roman(tier)}", (ItemTier)tier,
                    WeaponAttackMode.Bow, 18 + (tier - 1) * 7, 0.9f, 12f, 12f, 0.22f, 0, arrows));
            for (int tier = 3; tier <= 6; tier++)
                result.Add(new Spec($"pistol_{Roman(tier)}", $"Súng lục {Roman(tier)}", (ItemTier)tier,
                    WeaponAttackMode.Pistol, 32 + (tier - 3) * 9, 0.35f, 30f, 30f, 0f, 8, pistolAmmo));
            foreach (Spec spec in result)
                spec.speed = spec.mode == WeaponAttackMode.Bow ? 12f : spec.mode == WeaponAttackMode.Pistol ? 30f : 12f;
            // Range/cooldown/damage values remain serialized per definition; prefab art is replaceable.
            return result;
        }

        private static ItemDefinition CreateAmmo(string id, string name, int unitPrice, Color32 color, int quantity)
        {
            string path = $"{ItemRoot}/{id}.asset";
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null) item = FindById(id);
            if (item == null) item = ScriptableObject.CreateInstance<ItemDefinition>();
            Sprite icon = CreatePixelIcon(id, color, id == "arrow");
            Set(item, "itemId", id); Set(item, "displayName", name); Set(item, "description", "Đạn dùng cho vũ khí tầm xa.");
            Set(item, "icon", icon); Set(item, "itemType", (int)ItemType.Ammunition); Set(item, "tier", (int)ItemTier.I - 1);
            Set(item, "sellValue", Mathf.Max(0, unitPrice / 2)); Set(item, "buyPrice", unitPrice);
            Set(item, "isStackable", true); Set(item, "maxStackSize", 60); Set(item, "allowedInSafeSlots", true);
            if (AssetDatabase.GetAssetPath(item) != path) AssetDatabase.CreateAsset(item, path);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static ItemDefinition CreateWeapon(Spec spec)
        {
            string itemPath = $"{ItemRoot}/{spec.id}.asset";
            ItemDefinition item = FindById(spec.id) ?? AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            if (item == null) item = ScriptableObject.CreateInstance<ItemDefinition>();
            Sprite icon = CreatePixelIcon(spec.id, TierColor(spec.tier), false);
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponRoot}/{spec.id}_weapon.asset");
            if (weapon == null) weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            string prefabName = spec.mode == WeaponAttackMode.Bow ? "ArrowProjectile" : "PistolProjectile";
            GameObject projectile = spec.mode == WeaponAttackMode.Bow
                ? AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{prefabName}.prefab") :
                  spec.mode == WeaponAttackMode.Pistol ? AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{prefabName}.prefab") : null;
            Set(weapon, "weaponId", spec.id); Set(weapon, "attackMode", (int)spec.mode);
            Set(weapon, "damage", spec.damage); Set(weapon, "attackCooldown", spec.cooldown);
            Set(weapon, "attackRange", spec.range); Set(weapon, "ammunition", spec.ammo);
            Set(weapon, "magazineCapacity", spec.magazine); Set(weapon, "projectileSpeed", spec.speed);
            Set(weapon, "projectileGravityScale", spec.gravity); Set(weapon, "projectilePrefab", projectile); Set(weapon, "icon", icon);
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(weapon))) AssetDatabase.CreateAsset(weapon, $"{WeaponRoot}/{spec.id}_weapon.asset");
            EditorUtility.SetDirty(weapon);

            Set(item, "itemId", spec.id); Set(item, "displayName", spec.name); Set(item, "description", $"Vũ khí {spec.mode} bậc {Roman((int)spec.tier)}.");
            Set(item, "icon", icon); Set(item, "itemType", (int)ItemType.Weapon); Set(item, "tier", (int)spec.tier - 1);
            Set(item, "sellValue", spec.damage * 4); Set(item, "buyPrice", spec.damage * 10);
            Set(item, "isStackable", false); Set(item, "maxStackSize", 1); Set(item, "allowedInSafeSlots", false);
            Set(item, "weaponDefinition", weapon);
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(item))) AssetDatabase.CreateAsset(item, itemPath);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static GameObject CreateProjectilePrefab(string prefabName, string iconName, bool arrow)
        {
            string path = $"{PrefabRoot}/{prefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<Projectile2D>() != null) return prefab;
            GameObject temporary = new GameObject(prefabName);
            SpriteRenderer renderer = temporary.AddComponent<SpriteRenderer>();
            renderer.sprite = CreatePixelIcon(iconName, arrow ? new Color32(180, 125, 54, 255) : new Color32(235, 185, 55, 255), arrow);
            renderer.sortingOrder = 8;
            Rigidbody2D body = temporary.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic; body.gravityScale = arrow ? 0.22f : 0f;
            CircleCollider2D collider = temporary.AddComponent<CircleCollider2D>(); collider.isTrigger = true;
            temporary.AddComponent<Projectile2D>();
            temporary.transform.localScale = arrow ? new Vector3(0.45f, 0.08f, 1f) : new Vector3(0.16f, 0.16f, 1f);
            prefab = PrefabUtility.SaveAsPrefabAsset(temporary, path);
            Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static Sprite CreatePixelIcon(string id, Color32 color, bool arrow)
        {
            string path = $"{IconRoot}/{id}.png";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32 dark = new Color32(35, 35, 42, 255);
            Color32[] pixels = new Color32[256];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = transparent;
            for (int y = 2; y < 14; y++) for (int x = 2; x < 14; x++)
                if (x == 2 || x == 13 || y == 2 || y == 13) pixels[y * 16 + x] = dark;
            if (arrow)
            {
                for (int x = 4; x < 12; x++) pixels[7 * 16 + x] = color;
                pixels[8 * 16 + 10] = color; pixels[6 * 16 + 10] = color;
                pixels[7 * 16 + 12] = dark; pixels[8 * 16 + 11] = dark; pixels[6 * 16 + 11] = dark;
            }
            else
            {
                for (int i = 4; i < 12; i++)
                {
                    int y = (i + (id.Contains("bow") ? 3 : 0)) % 12 + 2;
                    pixels[y * 16 + i] = color;
                    if (y + 1 < 14) pixels[(y + 1) * 16 + i] = dark;
                }
                for (int y = 5; y < 11; y++) pixels[y * 16 + 7] = color;
            }
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void AddCatalogItems(ItemCatalog catalog, List<ItemDefinition> items)
        {
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty list = serialized.FindProperty("definitions");
            foreach (ItemDefinition item in items)
            {
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == item) { present = true; break; }
                if (!present) { int index = list.arraySize; list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index).objectReferenceValue = item; }
            }
            serialized.ApplyModifiedProperties(); EditorUtility.SetDirty(catalog);
        }

        private static void AddShopOffers(ShopCatalog catalog, List<Spec> specs, List<ItemDefinition> items)
        {
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty offers = serialized.FindProperty("offers");
            foreach (ItemDefinition item in items)
            {
                int index = -1;
                for (int i = 0; i < offers.arraySize; i++)
                    if (offers.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item) { index = i; break; }
                if (index < 0) { index = offers.arraySize; offers.InsertArrayElementAtIndex(index); }
                SerializedProperty offer = offers.GetArrayElementAtIndex(index);
                offer.FindPropertyRelative("item").objectReferenceValue = item;
                Spec spec = specs.Find(entry => entry.id == item.Id);
                int price = spec != null ? spec.damage * 10 : item.BuyPrice;
                offer.FindPropertyRelative("buyPrice").intValue = price;
                offer.FindPropertyRelative("purchaseQuantity").intValue = item.IsStackable ? 15 : 1;
            }
            serialized.ApplyModifiedProperties(); EditorUtility.SetDirty(catalog);
        }

        private static void AddAmmoToLoot(string path, ItemDefinition arrows, ItemDefinition bullets)
        {
            LootTableDefinition table = AssetDatabase.LoadAssetAtPath<LootTableDefinition>(path);
            if (table == null) return;
            SerializedObject serialized = new SerializedObject(table);
            SerializedProperty entries = serialized.FindProperty("entries");
            EnsureLootEntry(entries, arrows, 2f, 2, 6);
            EnsureLootEntry(entries, bullets, 1.5f, 2, 6);
            serialized.ApplyModifiedProperties(); EditorUtility.SetDirty(table);
        }

        private static void AddWeaponsToLoot(string path, List<ItemDefinition> weapons)
        {
            LootTableDefinition table = AssetDatabase.LoadAssetAtPath<LootTableDefinition>(path);
            if (table == null) return;
            SerializedObject serialized = new SerializedObject(table);
            SerializedProperty entries = serialized.FindProperty("entries");
            foreach (ItemDefinition weapon in weapons)
            {
                if (weapon == null || weapon.Type != ItemType.Weapon) continue;
                int index = -1;
                for (int i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == weapon)
                    { index = i; break; }
                if (index < 0) { index = entries.arraySize; entries.InsertArrayElementAtIndex(index); }
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("item").objectReferenceValue = weapon;
                entry.FindPropertyRelative("weight").floatValue = weapon.Tier == ItemTier.I ? 0.7f : 0.2f;
                entry.FindPropertyRelative("minimumQuantity").intValue = 1;
                entry.FindPropertyRelative("maximumQuantity").intValue = 1;
            }
            serialized.ApplyModifiedProperties(); EditorUtility.SetDirty(table);
        }

        private static void EnsureLootEntry(SerializedProperty entries, ItemDefinition item, float weight, int min, int max)
        {
            int index = -1;
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item) { index = i; break; }
            if (index < 0) { index = entries.arraySize; entries.InsertArrayElementAtIndex(index); }
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("weight").floatValue = weight;
            entry.FindPropertyRelative("minimumQuantity").intValue = min;
            entry.FindPropertyRelative("maximumQuantity").intValue = max;
        }

        private static ItemDefinition FindById(string id)
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemRoot });
            foreach (string guid in guids)
            {
                ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && item.Id == id) return item;
            }
            return null;
        }

        private static void Set(Object target, string propertyName, object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            if (value is string text) property.stringValue = text;
            else if (value is int number)
            {
                if (property.propertyType == SerializedPropertyType.Enum) property.enumValueIndex = number;
                else property.intValue = number;
            }
            else if (value is float decimalNumber) property.floatValue = decimalNumber;
            else if (value is bool flag) property.boolValue = flag;
            else if (value is Object reference) property.objectReferenceValue = reference;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color32 TierColor(ItemTier tier) => tier switch
        {
            ItemTier.I => new Color32(156, 163, 175, 255), ItemTier.II => new Color32(34, 197, 94, 255),
            ItemTier.III => new Color32(59, 130, 246, 255), ItemTier.IV => new Color32(168, 85, 247, 255),
            ItemTier.V => new Color32(250, 204, 21, 255), _ => new Color32(239, 68, 68, 255)
        };

        private static string Roman(int value) => value switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", _ => "?" };

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}

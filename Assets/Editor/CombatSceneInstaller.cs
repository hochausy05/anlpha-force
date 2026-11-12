using System.IO;
using Looter.Combat;
using Looter.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Looter.EditorTools
{
    public static class CombatSceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/FoundationPlayground.unity";
        private const string EnemyName = "Enemy - Slime biến dị";
        private const string ArtPath = "Assets/PixelLooter/Art/Enemy.png";

        [MenuItem("Pixel Looter/Tích hợp Basic Combat vào FoundationPlayground")]
        public static void InstallCombat()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                EditorUtility.DisplayDialog("Chưa mở đúng scene", "Hãy mở Assets/Scenes/FoundationPlayground.unity trước khi tích hợp combat.", "Đóng");
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Không tìm thấy nhân vật", "Scene hiện tại không có GameObject tên Player. Không có thay đổi nào được lưu.", "Đóng");
                return;
            }

            Sprite enemySprite = CreateEnemySprite();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Tích hợp Basic Combat");
            int undoGroup = Undo.GetCurrentGroup();

            player.tag = "Player";
            CombatHealth playerHealth = GetOrAdd<CombatHealth>(player);
            playerHealth.Faction = CombatFaction.Player;
            SetPrivateField(playerHealth, "maxHealth", 100);
            SetPrivateField(playerHealth, "invulnerabilitySeconds", 0.3f);
            GetOrAdd<PlayerMeleeAttack>(player);
            GetOrAdd<PlayerDeathHandler>(player);

            GameObject hudObject = GameObject.Find("Basic Combat HUD");
            if (hudObject == null)
            {
                hudObject = new GameObject("Basic Combat HUD");
                Undo.RegisterCreatedObjectUndo(hudObject, "Tạo HUD chiến đấu");
            }
            CombatHud hud = GetOrAdd<CombatHud>(hudObject);
            FoundationHierarchyOrganizer.ParentObject(hudObject, "UI");
            hud.Configure(playerHealth);

            GameObject enemy = GameObject.Find(EnemyName);
            if (enemy == null) enemy = CreateEnemy(player.transform.position + new Vector3(5f, 0f, 0f), enemySprite);
            FoundationHierarchyOrganizer.ParentObject(enemy, "Enemies");
            int enemyLayer = LayerMask.NameToLayer("EnemyCharacter");
            if (enemyLayer >= 0) FoundationHierarchyOrganizer.SetColliderLayers(enemy, enemyLayer);
            CombatHealth enemyHealth = GetOrAdd<CombatHealth>(enemy);
            enemyHealth.Faction = CombatFaction.Enemy;
            SetPrivateField(enemyHealth, "maxHealth", 40);
            GetOrAdd<EnemyAI>(enemy);
            GetOrAdd<EnemyHealthBar>(enemy);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("Basic Combat đã được tích hợp vào FoundationPlayground mà không dựng lại scene.");
            EditorUtility.DisplayDialog("Combat đã được thêm", "Scene FoundationPlayground đã được cập nhật. Nhấn Play để kiểm thử.", "Đóng");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        private static void SetPrivateField<T>(Component component, string fieldName, T value)
        {
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null) return;
            if (typeof(T) == typeof(int)) property.intValue = (int)(object)value;
            else if (typeof(T) == typeof(float)) property.floatValue = (float)(object)value;
            serialized.ApplyModifiedProperties();
        }

        private static GameObject CreateEnemy(Vector3 position, Sprite sprite)
        {
            GameObject enemy = new GameObject(EnemyName);
            Undo.RegisterCreatedObjectUndo(enemy, "Tạo quái thử nghiệm");
            enemy.transform.position = position;
            SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 2;
            Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            CapsuleCollider2D collider = enemy.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.9f, 0.85f);
            return enemy;
        }

        private static Sprite CreateEnemySprite()
        {
            if (!File.Exists(ArtPath))
            {
                Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                Color32[] pixels = new Color32[256];
                Color32 clear = new Color32(0, 0, 0, 0);
                Color32 body = new Color32(151, 65, 122, 255);
                Color32 dark = new Color32(72, 38, 83, 255);
                Color32 eye = new Color32(255, 225, 140, 255);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        bool silhouette = y >= 2 && y <= 12 && x >= 2 && x <= 13 && !(y < 5 && (x < 4 || x > 11));
                        Color32 color = !silhouette ? clear : (x == 4 || x == 11 || y == 3 ? dark : body);
                        if (y >= 8 && y <= 9 && (x == 5 || x == 10)) color = eye;
                        pixels[y * 16 + x] = color;
                    }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(ArtPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
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
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        }
    }
}

using System.IO;
using Looter.Combat;
using Looter.Foundation;
using Looter.Inventory;
using Looter.Raid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Looter.EditorTools
{
    public static class RaidSceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/FoundationPlayground.unity";
        private const string ArtFolder = "Assets/PixelLooter/Art/Extraction";
        private const string SpritePath = ArtFolder + "/ExtractionZone.png";

        [MenuItem("Pixel Looter/Tích hợp Milestone 1.4 vào FoundationPlayground")]
        public static void InstallRaidLoop()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                EditorUtility.DisplayDialog("Chưa mở đúng scene", "Hãy mở Assets/Scenes/FoundationPlayground.unity trước.", "Đóng");
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Thiếu Player", "Không tìm thấy GameObject Player. Scene chưa được thay đổi.", "Đóng");
                return;
            }

            EnsureFolder(ArtFolder);
            Sprite zoneSprite = CreateExtractionSprite();
            CameraFollow2D follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
            int playerLayer = LayerMask.NameToLayer("PlayerCharacter");
            int enemyLayer = LayerMask.NameToLayer("EnemyCharacter");
            if (playerLayer >= 0) FoundationHierarchyOrganizer.SetColliderLayers(player, playerLayer);
            if (enemyLayer >= 0)
                foreach (EnemyAI enemy in Object.FindObjectsByType<EnemyAI>()) FoundationHierarchyOrganizer.SetColliderLayers(enemy.gameObject, enemyLayer);

            GameObject managerObject = GameObject.Find("RaidManager");
            if (managerObject == null)
            {
                managerObject = new GameObject("RaidManager");
                Undo.RegisterCreatedObjectUndo(managerObject, "Tạo Raid Manager");
            }
            RaidManager manager = managerObject.GetComponent<RaidManager>();
            if (manager == null) manager = Undo.AddComponent<RaidManager>(managerObject);
            FoundationHierarchyOrganizer.ParentObject(managerObject, "Systems");
            Undo.RecordObject(manager, "Cấu hình Raid Manager");
            manager.Configure(player, follow);

            CreateSpawnPoint("Raid Spawn Point 1", -14f, -2.42f);
            CreateSpawnPoint("Raid Spawn Point 2", -2f, -2.42f);
            CreateSpawnPoint("Raid Spawn Point 3", 12f, -2.42f);
            CreateExtractionZone("Extraction Zone West", "Điểm rút phía Tây", -20f, zoneSprite);
            CreateExtractionZone("Extraction Zone East", "Điểm rút phía Đông", 20f, zoneSprite);

            WorldInteractionController interactions = player.GetComponent<WorldInteractionController>();
            if (interactions != null)
            {
                Undo.RecordObject(interactions, "Kết nối tương tác raid");
                interactions.ConfigureRaidManager(manager);
            }
            foreach (CombatHud hud in Object.FindObjectsByType<CombatHud>())
            {
                Undo.RecordObject(hud, "Kết nối HUD raid");
                hud.ConfigureRaid(manager);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Raid Manager, ba điểm spawn và hai Extraction Zone đã được tích hợp vào FoundationPlayground.");
            EditorUtility.DisplayDialog("Đã tích hợp Milestone 1.4", "Scene đã được cập nhật an toàn. Thời lượng mặc định là 20 phút; có thể chỉnh Raid Duration Seconds trên RaidManager để kiểm thử.", "Đóng");
        }

        private static void CreateSpawnPoint(string objectName, float x, float y)
        {
            GameObject point = GameObject.Find(objectName);
            bool created = point == null;
            if (created)
            {
                point = new GameObject(objectName);
                Undo.RegisterCreatedObjectUndo(point, "Tạo điểm spawn raid");
                point.transform.position = new Vector3(x, y, 0f);
            }
            RaidSpawnPoint component = point.GetComponent<RaidSpawnPoint>();
            if (component == null) component = Undo.AddComponent<RaidSpawnPoint>(point);
            FoundationHierarchyOrganizer.ParentObject(point, "Spawns/Raid");
            Undo.RecordObject(component, "Đặt nhãn điểm spawn");
            component.Configure(objectName.Replace("Raid Spawn Point", "Điểm xuất phát"));
        }

        private static void CreateExtractionZone(string objectName, string label, float x, Sprite sprite)
        {
            GameObject zone = GameObject.Find(objectName);
            bool created = zone == null;
            if (created)
            {
                zone = new GameObject(objectName);
                Undo.RegisterCreatedObjectUndo(zone, "Tạo điểm extraction");
                zone.transform.position = new Vector3(x, -2.4f, 0f);
            }
            SpriteRenderer renderer = zone.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(zone);
            if (created || renderer.sprite == null) renderer.sprite = sprite;
            renderer.color = new Color(0.55f, 0.9f, 1f, 0.9f);
            renderer.sortingOrder = 0;

            CircleCollider2D collider = zone.GetComponent<CircleCollider2D>();
            if (collider == null) collider = Undo.AddComponent<CircleCollider2D>(zone);
            collider.isTrigger = true;

            ExtractionZone component = zone.GetComponent<ExtractionZone>();
            if (component == null) component = Undo.AddComponent<ExtractionZone>(zone);
            FoundationHierarchyOrganizer.ParentObject(zone, "Interactables");
            Undo.RecordObject(component, "Cấu hình điểm extraction");
            component.Configure(label, 1.65f);
        }

        private static Sprite CreateExtractionSprite()
        {
            if (!File.Exists(SpritePath))
            {
                const int size = 16;
                Color32 clear = new Color32(0, 0, 0, 0);
                Color32 outer = new Color32(22, 72, 98, 255);
                Color32 inner = new Color32(48, 190, 215, 255);
                Color32 highlight = new Color32(175, 250, 255, 255);
                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int dx = x - 7, dy = y - 7;
                        int distance = dx * dx + dy * dy;
                        if (distance <= 48 && distance >= 25) pixels[y * size + x] = outer;
                        else if (distance < 25 && (Mathf.Abs(dx) <= 2 || Mathf.Abs(dy) <= 2)) pixels[y * size + x] = highlight;
                        else if (distance < 25) pixels[y * size + x] = inner;
                        else pixels[y * size + x] = clear;
                    }

                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
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
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}

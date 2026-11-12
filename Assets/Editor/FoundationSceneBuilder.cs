using System.IO;
using Looter.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace Looter.EditorTools
{
    public static class FoundationSceneBuilder
    {
        private const string Root = "Assets/PixelLooter";
        private const float MinX = -24f, MaxX = 24f;

        [MenuItem("Pixel Looter/Tạo scene Foundation")]
        public static void CreateFoundationScene()
        {
            EnsureFolders();
            Sprite ground = CreatePixelSprite("GroundTile", 16, 16, GroundPixel);
            Sprite platform = CreatePixelSprite("PlatformTile", 16, 16, PlatformPixel);
            Sprite player = CreatePixelSprite("Player", 16, 24, PlayerPixel);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreatePlayer(player);
            CreateCamera();
            CreateGround(ground);
            CreatePlatforms(platform);
            GameObject lightObject = new GameObject("Global Light 2D");
            Light2D light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            FoundationHierarchyOrganizer.OrganizeScene(scene);
            string path = "Assets/Scenes/FoundationPlayground.unity";
            EditorSceneManager.SaveScene(scene, path);
            AddSceneToBuildSettings(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Foundation Playground đã được tạo. Nhấn Play để chạy thử.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scripts")) AssetDatabase.CreateFolder("Assets", "Scripts");
            if (!AssetDatabase.IsValidFolder("Assets/Scripts/Player")) AssetDatabase.CreateFolder("Assets/Scripts", "Player");
            if (!AssetDatabase.IsValidFolder("Assets/Scripts/Camera")) AssetDatabase.CreateFolder("Assets/Scripts", "Camera");
            if (!AssetDatabase.IsValidFolder("Assets/Editor")) AssetDatabase.CreateFolder("Assets", "Editor");
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "PixelLooter");
            if (!AssetDatabase.IsValidFolder(Root + "/Art")) AssetDatabase.CreateFolder(Root, "Art");
        }

        private static void CreatePlayer(Sprite sprite)
        {
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(-16f, -1.4f, 0f);
            SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 2;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.72f, 1.2f);
            player.AddComponent<PlayerController2D>();
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(-16f, -1.4f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.backgroundColor = new Color32(119, 184, 210, 255);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            CameraFollow2D follow = cameraObject.AddComponent<CameraFollow2D>();
            follow.Configure(GameObject.Find("Player").transform,
                new Vector2(MinX, MaxX), new Vector2(-2f, 6f));
        }

        private static void CreateGround(Sprite sprite)
        {
            for (int x = -24; x < 24; x++) CreateBlock("Ground", sprite, new Vector2(x + 0.5f, -3.5f), Vector2.one);
            CreateBarrier(sprite, "LeftBoundary", new Vector2(MinX - 0.5f, 3f), new Vector2(1f, 14f));
            CreateBarrier(sprite, "RightBoundary", new Vector2(MaxX + 0.5f, 3f), new Vector2(1f, 14f));
            CreateBarrier(sprite, "CeilingBoundary", new Vector2(0f, 10.5f), new Vector2(50f, 1f));
        }

        private static void CreateBarrier(Sprite sprite, string name, Vector2 center, Vector2 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.position = center;
            wall.transform.localScale = size;
            SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color32(64, 89, 102, 255);
            renderer.sortingOrder = -1;
            wall.AddComponent<BoxCollider2D>();
        }

        private static void CreatePlatforms(Sprite sprite)
        {
            CreatePlatformRun(sprite, "Platform_Low", -11, -6, -0.8f);
            CreatePlatformRun(sprite, "Platform_Mid", -3, 2, 1.2f);
            CreatePlatformRun(sprite, "Platform_High", 5, 10, 3.2f);
            CreatePlatformRun(sprite, "Platform_East", 13, 18, 0.2f);
            CreatePlatformRun(sprite, "Platform_Upper", -18, -14, 2.2f);
            CreatePlatformRun(sprite, "Platform_RightHigh", 8, 12, 5.2f);
        }

        private static void CreatePlatformRun(Sprite sprite, string name, int startX, int endX, float y)
        {
            for (int x = startX; x < endX; x++) CreateBlock(name, sprite, new Vector2(x + 0.5f, y), Vector2.one);
        }

        private static void CreateBlock(string name, Sprite sprite, Vector2 position, Vector2 scale)
        {
            GameObject block = new GameObject(name);
            block.transform.position = position;
            block.transform.localScale = scale;
            SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -1;
            block.AddComponent<BoxCollider2D>();
        }

        private static Sprite CreatePixelSprite(string name, int width, int height, System.Func<int, int, Color32> pixel)
        {
            string path = Root + "/Art/" + name + ".png";
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) pixels[y * width + x] = pixel(x, y);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Color32 GroundPixel(int x, int y)
        {
            if (y >= 13) return new Color32(98, 181, 74, 255);
            if (y == 12) return new Color32(57, 133, 61, 255);
            return ((x * 7 + y * 11) % 13) < 2 ? new Color32(110, 76, 53, 255) : new Color32(128, 86, 57, 255);
        }
        private static Color32 PlatformPixel(int x, int y)
        {
            if (y >= 13) return new Color32(173, 131, 73, 255);
            return y >= 11 ? new Color32(105, 76, 51, 255) : new Color32(118, 80, 49, 255);
        }
        private static Color32 PlayerPixel(int x, int y)
        {
            if (x < 3 || x > 12 || y < 1 || y > 22) return new Color32(0, 0, 0, 0);
            if (y >= 17) return new Color32(220, 174, 111, 255);
            if (y >= 9) return new Color32(46, 91, 124, 255);
            if (y >= 4) return new Color32(55, 69, 76, 255);
            return new Color32(35, 46, 50, 255);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene scene in scenes) if (scene.path == path) return;
            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[scenes.Length + 1];
            System.Array.Copy(scenes, updated, scenes.Length);
            updated[scenes.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}

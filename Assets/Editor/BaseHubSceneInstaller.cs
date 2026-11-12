using System.IO;
using System.Linq;
using Looter.Economy;
using Looter.Hub;
using Looter.Items;
using Looter.Persistence;
using Looter.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Looter.EditorTools
{
    public static class BaseHubSceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/BaseHub.unity";
        private const string CatalogFolder = "Assets/Data/Catalogs";
        private const string ItemCatalogPath = CatalogFolder + "/ItemCatalog.asset";
        private const string ShopCatalogPath = CatalogFolder + "/ShopCatalog.asset";

        [MenuItem("Pixel Looter/Tạo hoặc cập nhật BaseHub")]
        public static void CreateOrUpdateBaseHub()
        {
            EnsureFolder(CatalogFolder);
            ItemDefinition[] definitions = FindItemDefinitions();
            ItemCatalog itemCatalog = CreateOrUpdateItemCatalog(definitions);
            ShopCatalog shopCatalog = CreateOrUpdateShopCatalog(definitions);

            Scene scene;
            if (File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            else scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            PlayerProfileStore profile = Object.FindAnyObjectByType<PlayerProfileStore>();
            if (profile == null) profile = new GameObject("Player Profile").AddComponent<PlayerProfileStore>();
            SerializedObject profileSerialized = new SerializedObject(profile);
            profileSerialized.FindProperty("itemCatalog").objectReferenceValue = itemCatalog;
            profileSerialized.ApplyModifiedPropertiesWithoutUndo();

            BaseHubController controller = Object.FindAnyObjectByType<BaseHubController>();
            if (controller == null) controller = new GameObject("BaseHub Controller").AddComponent<BaseHubController>();
            SerializedObject controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("shopCatalog").objectReferenceValue = shopCatalog;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            BaseHubCanvas view = Object.FindAnyObjectByType<BaseHubCanvas>();
            if (view == null)
            {
                GameObject canvasObject = new GameObject("BaseHub Canvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(BaseHubCanvas));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                view = canvasObject.GetComponent<BaseHubCanvas>();
            }
            SerializedObject viewSerialized = new SerializedObject(view);
            viewSerialized.FindProperty("uiFont").objectReferenceValue = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            viewSerialized.ApplyModifiedPropertiesWithoutUndo();

            EnsureEventSystem(scene);
            EnsureBaseHubCamera(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("BaseHub được tạo/cập nhật theo cấu trúc hiện có; ItemCatalog, ShopCatalog và scene build settings đã đồng bộ.");
            EditorUtility.DisplayDialog("BaseHub sẵn sàng", "Scene BaseHub đã được lưu. Nhấn Play để tạo/nạp save; dùng BẮT ĐẦU RAID để chuyển sang FoundationPlayground.", "Đóng");
        }

        private static ItemDefinition[] FindItemDefinitions()
        {
            return AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Data/Items" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<ItemDefinition>(path))
                .Where(item => item != null)
                .OrderBy(item => item.Id)
                .ToArray();
        }

        private static ItemCatalog CreateOrUpdateItemCatalog(ItemDefinition[] definitions)
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, ItemCatalogPath);
            }
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty list = serialized.FindProperty("definitions");
            foreach (ItemDefinition definition in definitions)
            {
                bool exists = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == definition) { exists = true; break; }
                if (exists) continue;
                int index = list.arraySize++;
                list.GetArrayElementAtIndex(index).objectReferenceValue = definition;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static ShopCatalog CreateOrUpdateShopCatalog(ItemDefinition[] definitions)
        {
            ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(ShopCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ShopCatalog>();
                AssetDatabase.CreateAsset(catalog, ShopCatalogPath);
            }
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty offers = serialized.FindProperty("offers");
            foreach (ItemDefinition definition in definitions)
            {
                if (definition.Type == ItemType.Valuable || definition.Type == ItemType.Material || definition.Type == ItemType.Consumable ||
                    definition.Type == ItemType.Weapon || definition.Type == ItemType.Backpack || definition.Type == ItemType.Drone)
                {
                    int index = -1;
                    for (int i = 0; i < offers.arraySize; i++)
                        if (offers.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == definition)
                        { index = i; break; }
                    if (index < 0)
                    {
                        index = offers.arraySize++;
                        SerializedProperty added = offers.GetArrayElementAtIndex(index);
                        added.FindPropertyRelative("item").objectReferenceValue = definition;
                        int price = definition.BuyPrice > 0 ? definition.BuyPrice : Mathf.Max(1, Mathf.CeilToInt(definition.SellValue * 1.5f));
                        added.FindPropertyRelative("buyPrice").intValue = price;
                    }
                }

                SerializedObject itemSerialized = new SerializedObject(definition);
                SerializedProperty buyPrice = itemSerialized.FindProperty("buyPrice");
                if (buyPrice.intValue <= 0 && definition.SellValue > 0)
                {
                    buyPrice.intValue = Mathf.Max(1, Mathf.CeilToInt(definition.SellValue * 1.5f));
                    itemSerialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(definition);
                }
                if (definition.Type == ItemType.Backpack && definition.Tier > ItemTier.I)
                {
                    SerializedProperty capacity = itemSerialized.FindProperty("backpackCapacity");
                    int expected = 10 + ((int)definition.Tier - 1) * 4;
                    if (capacity.intValue < expected)
                    {
                        capacity.intValue = expected;
                        itemSerialized.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(definition);
                    }
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null) return;
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            InputSystemUIInputModule input = eventObject.GetComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
            if (eventObject.scene != scene) SceneManager.MoveGameObjectToScene(eventObject, scene);
        }

        private static void EnsureBaseHubCamera(Scene scene)
        {
            Transform systems = scene.GetRootGameObjects()
                .Select(root => root.transform)
                .FirstOrDefault(root => root.name == "BaseHub Systems");
            if (systems == null)
            {
                GameObject systemsObject = new GameObject("BaseHub Systems");
                if (systemsObject.scene != scene) SceneManager.MoveGameObjectToScene(systemsObject, scene);
                systems = systemsObject.transform;
            }

            Camera hubCamera = Object.FindObjectsByType<Camera>()
                .FirstOrDefault(camera => camera.gameObject.scene == scene);
            if (hubCamera == null)
            {
                GameObject cameraObject = new GameObject("BaseHub Camera", typeof(Camera));
                if (cameraObject.scene != scene) SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.transform.SetParent(systems, false);
                hubCamera = cameraObject.GetComponent<Camera>();
            }
            else
            {
                hubCamera.name = "BaseHub Camera";
                if (hubCamera.transform.parent != systems) hubCamera.transform.SetParent(systems, true);
            }

            hubCamera.enabled = true;
            hubCamera.tag = "MainCamera";
            hubCamera.targetDisplay = 0;
            hubCamera.clearFlags = CameraClearFlags.SolidColor;
            hubCamera.backgroundColor = new Color32(12, 17, 23, 255);
            hubCamera.orthographic = true;
            hubCamera.orthographicSize = 5f;
            hubCamera.depth = -10f;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Any(scene => scene.path == scenePath && scene.enabled)) return;
            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[scenes.Length + (scenes.Any(scene => scene.path == scenePath) ? 0 : 1)];
            int targetIndex = 0;
            updated[targetIndex++] = new EditorBuildSettingsScene(scenePath, true);
            foreach (EditorBuildSettingsScene scene in scenes)
                if (scene.path != scenePath) updated[targetIndex++] = scene;
            EditorBuildSettings.scenes = updated;
        }

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

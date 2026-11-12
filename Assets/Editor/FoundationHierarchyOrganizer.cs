using System;
using Looter.Combat;
using Looter.Items;
using Looter.Raid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Looter.EditorTools
{
    public static class FoundationHierarchyOrganizer
    {
        [MenuItem("Pixel Looter/Sắp xếp hierarchy FoundationPlayground")]
        public static void OrganizeActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/FoundationPlayground.unity")
            {
                EditorUtility.DisplayDialog("Chưa mở đúng scene", "Hãy mở FoundationPlayground trước.", "Đóng");
                return;
            }

            int playerLayer = LayerMask.NameToLayer("PlayerCharacter");
            int enemyLayer = LayerMask.NameToLayer("EnemyCharacter");
            int itemLayer = LayerMask.NameToLayer("WorldItem");
            if (playerLayer < 0 || enemyLayer < 0 || itemLayer < 0)
            {
                EditorUtility.DisplayDialog("Thiếu layer", "Kiểm tra Project Settings > Tags and Layers: PlayerCharacter, EnemyCharacter, WorldItem.", "Đóng");
                return;
            }

            OrganizeScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Đã nhóm các GameObject hiện có, giữ nguyên world transform; layer Player/Enemy/WorldItem đã sẵn sàng.");
            EditorUtility.DisplayDialog("Đã sắp xếp hierarchy", "Các nhóm đã được tạo theo những đối tượng hiện có. Vật phẩm runtime và hòm xác sẽ được đặt dưới RuntimeObjects.", "Đóng");
        }

        public static void OrganizeScene(Scene scene)
        {
            int playerLayer = LayerMask.NameToLayer("PlayerCharacter");
            int enemyLayer = LayerMask.NameToLayer("EnemyCharacter");
            if (playerLayer < 0 || enemyLayer < 0) return;
            Organize(scene, playerLayer, enemyLayer);
        }

        public static void ParentObject(GameObject target, string path)
        {
            if (target == null) return;
            Transform parent = GetPath(target.scene, path);
            if (target.transform == parent || target.transform.IsChildOf(parent)) return;
            Undo.SetTransformParent(target.transform, parent, "Đặt đối tượng trong hierarchy " + path);
        }

        public static void SetColliderLayers(GameObject target, int layer)
        {
            if (target == null || layer < 0) return;
            foreach (Collider2D collider in target.GetComponentsInChildren<Collider2D>(true))
            {
                Undo.RecordObject(collider.gameObject, "Gán layer va chạm");
                collider.gameObject.layer = layer;
            }
            Undo.RecordObject(target, "Gán layer va chạm");
            target.layer = layer;
        }

        private static void Organize(Scene scene, int playerLayer, int enemyLayer)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (IsGroup(root.name)) continue;
                Transform destination = null;
                string folder = null;
                if (root.name == "Player" || root.CompareTag("Player")) folder = "Player";
                else if (root.GetComponent<Camera>() != null || root.name.Contains("Camera")) folder = "Camera";
                else if (root.GetComponent<RaidManager>() != null || root.name == "Global Light 2D") folder = "Systems";
                else if (root.GetComponent<EnemyAI>() != null) folder = "Enemies";
                else if (root.GetComponent<WorldLootSpawnPoint>() != null) folder = "Spawns/WorldLoot";
                else if (root.GetComponent<RaidSpawnPoint>() != null) folder = "Spawns/Raid";
                else if (root.GetComponent<LootContainer>() != null || root.GetComponent<ExtractionZone>() != null) folder = "Interactables";
                else if (root.GetComponent<CombatHud>() != null || root.name.Contains("HUD")) folder = "UI";
                else if (root.name == "Ground" || root.name.StartsWith("Platform", StringComparison.Ordinal) || root.name.Contains("Boundary"))
                    folder = root.name == "Ground" ? "Environment/Ground" : root.name.StartsWith("Platform", StringComparison.Ordinal) ? "Environment/Platforms" : "Environment/Boundaries";
                if (string.IsNullOrEmpty(folder)) continue;

                if (folder == "Player" && root.name == "Player")
                {
                    Undo.RecordObject(root, "Đổi tên đối tượng Player để tạo nhóm hierarchy");
                    root.name = "Player Character";
                }
                destination = GetPath(scene, folder);
                if (destination == root.transform || root.transform.IsChildOf(destination)) continue;
                Undo.SetTransformParent(root.transform, destination, "Sắp xếp hierarchy Foundation");
                if (root.GetComponent<EnemyAI>() != null) SetColliderLayers(root, enemyLayer);
                if (folder == "Player") SetColliderLayers(root, playerLayer);
            }
            // Runtime groups are required as stable parents for spawned loot and corpses.
            GetPath(scene, "RuntimeObjects");
        }

        public static Transform GetPath(Scene scene, string path)
        {
            string[] names = path.Split('/');
            Transform parent = null;
            foreach (string name in names)
            {
                Transform child = parent != null ? parent.Find(name) : FindRoot(scene, name);
                if (child == null)
                {
                    GameObject group = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(group, "Tạo nhóm hierarchy " + name);
                    if (group.scene != scene) SceneManager.MoveGameObjectToScene(group, scene);
                    if (parent != null) Undo.SetTransformParent(group.transform, parent, "Tạo nhóm hierarchy " + name);
                    child = group.transform;
                }
                parent = child;
            }
            return parent;
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root.transform;
            return null;
        }

        private static bool IsGroup(string name) => name == "Systems" || name == "Player" || name == "Camera" ||
            name == "Environment" || name == "Spawns" || name == "Enemies" || name == "Interactables" ||
            name == "RuntimeObjects" || name == "UI";

    }
}

using UnityEngine;

namespace Looter.Items
{
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public sealed class WorldItemPickup : MonoBehaviour
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(1)] private int quantity = 1;
        private SpriteRenderer spriteRenderer;
        private static PhysicsMaterial2D settlingMaterial;

        public ItemDefinition Item => item;
        public int Quantity => quantity;
        public string Prompt => item == null ? "Vật phẩm không hợp lệ" :
            $"Nhấn E để nhặt {item.DisplayName} ({item.TierLabel}) x{quantity}";

        private void Awake()
        {
            ApplyCollisionLayers();
            SetLayerRecursively(gameObject, LayerMask.NameToLayer("WorldItem"));
            spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyDefinition();
            BoxCollider2D pickupCollider = GetComponent<BoxCollider2D>();
            pickupCollider.isTrigger = false;
            pickupCollider.size = new Vector2(0.38f, 0.38f);
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 3f;
            body.linearDamping = 7f;
            body.mass = 0.25f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (settlingMaterial == null)
            {
                settlingMaterial = new PhysicsMaterial2D("World Item Settling")
                {
                    friction = 0.65f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine2D.Maximum,
                    bounceCombine = PhysicsMaterialCombine2D.Minimum
                };
            }
            pickupCollider.sharedMaterial = settlingMaterial;
            IgnoreCharacterCollisions(pickupCollider);
        }

        public void Initialize(ItemDefinition definition, int count)
        {
            item = definition;
            quantity = Mathf.Max(1, count);
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyDefinition();
        }

        public static WorldItemPickup Spawn(ItemDefinition definition, int count, Vector3 position)
        {
            if (definition == null || count <= 0) return null;
            GameObject pickupObject = new GameObject($"Loot - {definition.DisplayName}");
            pickupObject.transform.position = position;
            pickupObject.transform.SetParent(GetRuntimeObjectsRoot(), true);
            pickupObject.AddComponent<SpriteRenderer>();
            pickupObject.AddComponent<BoxCollider2D>();
            pickupObject.AddComponent<Rigidbody2D>();
            WorldItemPickup pickup = pickupObject.AddComponent<WorldItemPickup>();
            pickup.Initialize(definition, count);
            return pickup;
        }

        public static bool TryFindGroundPosition(Vector3 desiredPosition, out Vector3 position)
        {
            const float halfHeight = 0.19f;
            float[] offsets = { 0f, -0.45f, 0.45f, -0.9f, 0.9f };
            foreach (float offset in offsets)
            {
                float x = desiredPosition.x + offset;
                Vector2 rayOrigin = new Vector2(x, desiredPosition.y + 1.5f);
                RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, Vector2.down, 6f);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit2D hit in hits)
                {
                    if (hit.collider == null || hit.collider.isTrigger || hit.normal.y < 0.5f) continue;
                    if (hit.collider.GetComponentInParent<WorldItemPickup>() != null) continue;
                    if (hit.collider.GetComponentInParent<Looter.Combat.EnemyAI>() != null) continue;
                    Vector2 center = hit.point + Vector2.up * (halfHeight + 0.025f);
                    Collider2D[] overlaps = Physics2D.OverlapBoxAll(center, new Vector2(0.34f, 0.34f), 0f);
                    bool blocked = false;
                    foreach (Collider2D overlap in overlaps)
                    {
                        if (overlap == null || overlap.isTrigger || overlap == hit.collider) continue;
                        if (overlap.GetComponentInParent<WorldItemPickup>() != null) continue;
                        if (overlap.GetComponentInParent<Looter.Combat.EnemyAI>() != null) continue;
                        blocked = true;
                        break;
                    }
                    if (blocked) continue;
                    position = new Vector3(x, center.y, desiredPosition.z);
                    return true;
                }
            }
            position = desiredPosition;
            return false;
        }

        public static Transform GetRuntimeObjectsRoot()
        {
            GameObject root = GameObject.Find("RuntimeObjects");
            if (root == null) root = new GameObject("RuntimeObjects");
            return root.transform;
        }

        private static void ApplyCollisionLayers()
        {
            int itemLayer = LayerMask.NameToLayer("WorldItem");
            int playerLayer = LayerMask.NameToLayer("PlayerCharacter");
            int enemyLayer = LayerMask.NameToLayer("EnemyCharacter");
            if (itemLayer < 0) return;
            if (playerLayer >= 0) Physics2D.IgnoreLayerCollision(itemLayer, playerLayer, true);
            if (enemyLayer >= 0) Physics2D.IgnoreLayerCollision(itemLayer, enemyLayer, true);
            Physics2D.IgnoreLayerCollision(itemLayer, itemLayer, true);
            if ((playerLayer >= 0 && !Physics2D.GetIgnoreLayerCollision(itemLayer, playerLayer)) ||
                (enemyLayer >= 0 && !Physics2D.GetIgnoreLayerCollision(itemLayer, enemyLayer)) ||
                !Physics2D.GetIgnoreLayerCollision(itemLayer, itemLayer))
                Debug.LogError("Không thể xác nhận cấu hình bỏ va chạm layer WorldItem với Player, Enemy và WorldItem.");
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            if (layer < 0) return;
            target.layer = layer;
            foreach (Transform child in target.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private static void IgnoreCharacterCollisions(Collider2D pickupCollider)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) IgnoreColliders(pickupCollider, player.GetComponentsInChildren<Collider2D>());

            Looter.Combat.EnemyAI[] enemies = FindObjectsByType<Looter.Combat.EnemyAI>();
            foreach (Looter.Combat.EnemyAI enemy in enemies)
                if (enemy != null) IgnoreColliders(pickupCollider, enemy.GetComponentsInChildren<Collider2D>());
        }

        private static void IgnoreColliders(Collider2D pickupCollider, Collider2D[] characterColliders)
        {
            foreach (Collider2D characterCollider in characterColliders)
                if (characterCollider != null && characterCollider != pickupCollider)
                    Physics2D.IgnoreCollision(characterCollider, pickupCollider, true);
        }

        private void ApplyDefinition()
        {
            if (spriteRenderer == null || item == null) return;
            spriteRenderer.sprite = item.Icon;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 3;
        }

        private void OnValidate() => quantity = Mathf.Max(1, quantity);
    }
}

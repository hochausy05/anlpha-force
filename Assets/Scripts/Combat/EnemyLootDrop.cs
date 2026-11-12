using System.Collections.Generic;
using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    [RequireComponent(typeof(CombatHealth))]
    public sealed class EnemyLootDrop : MonoBehaviour
    {
        [SerializeField] private LootTableDefinition lootTable;
        [SerializeField] private Sprite corpseSprite;
        [SerializeField] private bool elite;
        [SerializeField] private bool boss;

        private CombatHealth health;
        private EnemyInventory enemyInventory;
        private bool handledDeath;

        public LootTableDefinition LootTable { get => lootTable; set => lootTable = value; }
        public Sprite CorpseSprite { get => corpseSprite; set => corpseSprite = value; }
        public bool Elite { get => elite; set => elite = value; }
        public bool Boss { get => boss; set => boss = value; }

        public void ResetForNewRaid() => handledDeath = false;

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            enemyInventory = GetComponent<EnemyInventory>();
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponent<CombatHealth>();
            if (health != null) health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied(CombatHealth _)
        {
            if (handledDeath) return;
            handledDeath = true;

            EnemyLootDrop[] handlers = GetComponents<EnemyLootDrop>();
            if (handlers.Length > 0 && handlers[0] != this) return;

            if (enemyInventory == null) enemyInventory = GetComponent<EnemyInventory>();
            List<ItemStack> contents = enemyInventory != null
                ? enemyInventory.TakeForCorpse()
                : lootTable != null ? lootTable.GenerateLoot() : new List<ItemStack>();

            Vector3 corpsePosition = FindGroundPosition(transform.position);
            GameObject corpse = new GameObject(boss ? "Hòm xác — Boss" : elite ? "Hòm xác — Bot tinh nhuệ" : "Hòm xác — Bot thường");
            corpse.transform.position = corpsePosition;
            corpse.transform.SetParent(WorldItemPickup.GetRuntimeObjectsRoot(), true);
            SpriteRenderer renderer = corpse.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 1;
            BoxCollider2D collider = corpse.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            LootContainer container = corpse.AddComponent<LootContainer>();
            if (boss) container.InitializeBossCorpse(corpseSprite, lootTable, contents);
            else container.InitializeCorpse(elite, corpseSprite, lootTable, contents);
        }

        private Vector3 FindGroundPosition(Vector3 deathPosition)
        {
            Vector2 origin = (Vector2)deathPosition + Vector2.up * 0.2f;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 5f);
            RaycastHit2D nearestGround = default;
            float nearestDistance = float.PositiveInfinity;
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.normal.y < 0.5f) continue;
                if (hit.distance < nearestDistance)
                {
                    nearestGround = hit;
                    nearestDistance = hit.distance;
                }
            }
            if (nearestGround.collider != null) return nearestGround.point + Vector2.up * 0.42f;
            return deathPosition + Vector3.up * 0.42f;
        }
    }
}

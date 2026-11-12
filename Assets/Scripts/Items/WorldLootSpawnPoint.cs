using UnityEngine;

namespace Looter.Items
{
    public sealed class WorldLootSpawnPoint : MonoBehaviour
    {
        [SerializeField] private LootTableDefinition lootTable;
        [SerializeField, Range(0f, 1f)] private float spawnChance = 0.75f;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.42f, 0f);
        public LootTableDefinition LootTable { get => lootTable; set => lootTable = value; }
        public float SpawnChance { get => spawnChance; set => spawnChance = Mathf.Clamp01(value); }

        private void Start()
        {
            SpawnLoot();
        }

        public void SpawnLoot()
        {
            if (lootTable == null || Random.value > spawnChance) return;
            var stacks = lootTable.GenerateLoot();
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                Vector3 desiredPosition = transform.position + spawnOffset + Vector3.right * (i * 0.55f);
                if (stack == null || !WorldItemPickup.TryFindGroundPosition(desiredPosition, out Vector3 validPosition))
                    continue;
                WorldItemPickup.Spawn(stack.Item, stack.Quantity, validPosition);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }
}

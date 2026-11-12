using UnityEngine;

namespace Looter.Combat
{
    public enum EnemySpawnZone { Safe, Medium, High }

    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private EnemyDefinition enemyDefinition;
        [SerializeField] private EnemySpawnZone zone;
        [SerializeField, Min(1)] private int quota = 1;
        [SerializeField, Min(0f)] private float spreadRadius = 1.5f;
        [SerializeField] private bool bossSpawn;
        [SerializeField] private BossChestLock bossChest;

        public GameObject EnemyPrefab => enemyPrefab;
        public EnemyDefinition Definition => enemyDefinition;
        public EnemySpawnZone Zone => zone;
        public int Quota => Mathf.Max(1, quota);
        public float SpreadRadius => Mathf.Max(0f, spreadRadius);
        public bool IsBossSpawn => bossSpawn;
        public BossChestLock BossChest => bossChest;

        public void Configure(GameObject prefab, EnemyDefinition definition, EnemySpawnZone spawnZone,
            int spawnQuota, float spread, bool isBoss, BossChestLock linkedBossChest = null)
        {
            enemyPrefab = prefab;
            enemyDefinition = definition;
            zone = spawnZone;
            quota = Mathf.Max(1, spawnQuota);
            spreadRadius = Mathf.Max(0f, spread);
            bossSpawn = isBoss;
            bossChest = linkedBossChest;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = bossSpawn ? new Color(1f, 0.2f, 0.15f) : zone switch
            {
                EnemySpawnZone.Safe => new Color(0.25f, 0.9f, 0.35f),
                EnemySpawnZone.Medium => new Color(1f, 0.7f, 0.2f),
                _ => new Color(0.9f, 0.2f, 0.2f)
            };
            Gizmos.DrawWireSphere(transform.position, spreadRadius);
        }
    }
}

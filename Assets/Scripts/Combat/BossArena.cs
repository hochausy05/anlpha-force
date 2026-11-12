using UnityEngine;

namespace Looter.Combat
{
    public sealed class BossArena : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float horizontalRadius = 7f;
        [SerializeField, Min(1f)] private float verticalRadius = 4f;
        [SerializeField] private EnemySpawnPoint bossSpawnPoint;
        [SerializeField] private BossChestLock rareChest;
        public float HorizontalRadius => Mathf.Max(1f, horizontalRadius);
        public float VerticalRadius => Mathf.Max(1f, verticalRadius);

        public void Configure(float width, float height, EnemySpawnPoint spawnPoint = null, BossChestLock chest = null)
        {
            horizontalRadius = Mathf.Max(1f, width * 0.5f);
            verticalRadius = Mathf.Max(1f, height * 0.5f);
            if (spawnPoint != null) bossSpawnPoint = spawnPoint;
            if (chest != null) rareChest = chest;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.24f, 0.16f, 0.25f);
            Gizmos.DrawWireCube(transform.position, new Vector3(HorizontalRadius * 2f, VerticalRadius * 2f, 0f));
        }
    }
}

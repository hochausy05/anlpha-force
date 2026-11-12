using System.Collections.Generic;
using Looter.Items;
using Looter.Raid;
using UnityEngine;

namespace Looter.Combat
{
    public sealed class EnemySpawnDirector : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximumActiveEnemies = 16;
        [SerializeField, Min(1f)] private float minimumPlayerDistance = 4f;
        [SerializeField, Min(0.1f)] private float minimumExtractionDistance = 4f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private bool initialized;

        public void BeginRaid(bool newRaid)
        {
            if (!newRaid && initialized) return;
            if (newRaid) ClearSpawned();
            initialized = true;
            EnemySpawnPoint[] points = FindObjectsByType<EnemySpawnPoint>();
            int remaining = maximumActiveEnemies;
            bool bossSpawned = false;
            foreach (EnemySpawnPoint point in points)
            {
                if (point == null || point.EnemyPrefab == null || remaining <= 0 || point.IsBossSpawn && bossSpawned) continue;
                int quota = point.IsBossSpawn ? 1 : Mathf.Min(point.Quota, remaining);
                for (int index = 0; index < quota; index++)
                {
                    if (!TryFindSpawnPosition(point, index, out Vector2 position)) continue;
                    GameObject enemy = Instantiate(point.EnemyPrefab, position, point.transform.rotation,
                        WorldItemPickup.GetRuntimeObjectsRoot());
                    enemy.name = point.Definition != null ? point.Definition.DisplayName : point.EnemyPrefab.name;
                    EnemyAI ai = enemy.GetComponent<EnemyAI>();
                    if (ai != null && point.Definition != null) ai.Configure(point.Definition);
                    BossController boss = enemy.GetComponent<BossController>();
                    if (boss != null) boss.ConfigureChest(point.BossChest);
                    spawned.Add(enemy);
                    remaining--;
                    if (point.IsBossSpawn) { bossSpawned = true; break; }
                }
            }
        }

        private bool TryFindSpawnPosition(EnemySpawnPoint point, int ordinal, out Vector2 position)
        {
            position = point.transform.position;
            CapsuleCollider2D prefabCapsule = point.EnemyPrefab.GetComponent<CapsuleCollider2D>();
            float verticalOffset = prefabCapsule != null ? Mathf.Max(0f, prefabCapsule.size.y * 0.5f - 0.45f) : 0f;
            Vector2 spawnAnchor = (Vector2)point.transform.position + Vector2.up * verticalOffset;
            Vector2 playerPosition = Vector2.positiveInfinity;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerPosition = player.transform.position;
            ExtractionZone[] extractionZones = FindObjectsByType<ExtractionZone>();
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 offset;
                if (attempt == 0) offset = Vector2.zero;
                else
                {
                    float angle = (ordinal * 137.5f + attempt * 91f) * Mathf.Deg2Rad;
                    float radius = point.SpreadRadius * (0.25f + 0.75f * ((attempt - 1) % 4 + 1) / 4f);
                    offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.2f) * radius;
                }
                Vector2 candidate = spawnAnchor + offset;
                if (playerPosition != Vector2.positiveInfinity && Vector2.Distance(candidate, playerPosition) < minimumPlayerDistance) continue;
                bool nearExtraction = false;
                foreach (ExtractionZone zone in extractionZones)
                    if (zone != null && Vector2.Distance(candidate, zone.transform.position) < minimumExtractionDistance)
                    { nearExtraction = true; break; }
                if (nearExtraction || !HasGround(candidate) || IsBlocked(candidate)) continue;
                position = candidate;
                return true;
            }
            return false;
        }

        private bool HasGround(Vector2 position)
        {
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(position + Vector2.up * 0.1f,
                Vector2.down, 1.2f, obstacleMask))
                if (hit.collider != null && !hit.collider.isTrigger && hit.normal.y > 0.5f &&
                    hit.collider.GetComponentInParent<CombatHealth>() == null &&
                    hit.collider.GetComponentInParent<LootContainer>() == null) return true;
            return false;
        }

        private bool IsBlocked(Vector2 position)
        {
            Collider2D[] overlaps = Physics2D.OverlapBoxAll(position + Vector2.up * 0.45f,
                new Vector2(0.78f, 0.88f), 0f, obstacleMask);
            foreach (Collider2D collider in overlaps)
            {
                if (collider == null || collider.isTrigger || collider.GetComponentInParent<WorldItemPickup>() != null) continue;
                return true;
            }
            return false;
        }

        private void ClearSpawned()
        {
            foreach (GameObject instance in spawned)
                if (instance != null) Destroy(instance);
            spawned.Clear();
            initialized = false;
        }

        private void OnValidate()
        {
            maximumActiveEnemies = Mathf.Max(1, maximumActiveEnemies);
            minimumPlayerDistance = Mathf.Max(1f, minimumPlayerDistance);
            minimumExtractionDistance = Mathf.Max(0.1f, minimumExtractionDistance);
        }
    }
}

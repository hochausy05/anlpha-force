using System.Collections.Generic;
using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class Projectile2D : MonoBehaviour
    {
        private Rigidbody2D body;
        private GameObject owner;
        private ItemTier tier;
        private int damage;
        private float lifeRemaining;
        private float maximumDistance;
        private float travelledDistance;
        private bool spent;
        private CombatFaction targetFaction = CombatFaction.Enemy;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(12);
        private ContactFilter2D hitFilter;

        public void Initialize(GameObject source, ItemTier sourceTier, int amount, Vector2 direction,
            float speed, float gravityScale, float range)
        {
            Initialize(source, sourceTier, amount, direction, speed, gravityScale, range, CombatFaction.Enemy);
        }

        public void Initialize(GameObject source, ItemTier sourceTier, int amount, Vector2 direction,
            float speed, float gravityScale, float range, CombatFaction factionToHit)
        {
            owner = source;
            tier = sourceTier;
            damage = Mathf.Max(1, amount);
            lifeRemaining = 5f;
            maximumDistance = Mathf.Max(0.1f, range);
            travelledDistance = 0f;
            targetFaction = factionToHit;
            body = GetComponent<Rigidbody2D>();
            hitFilter = new ContactFilter2D { useTriggers = false };
            hitFilter.SetLayerMask(~0);
            body.gravityScale = Mathf.Max(0f, gravityScale);
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.linearVelocity = direction.normalized * Mathf.Max(0.1f, speed);
            transform.right = direction.normalized;
        }

        public static void CancelOwnedBy(GameObject source)
        {
            if (source == null) return;
            foreach (Projectile2D projectile in FindObjectsByType<Projectile2D>())
                if (projectile != null && projectile.owner == source) Destroy(projectile.gameObject);
        }

        public static void CancelFactionProjectiles(CombatFaction ownerFaction)
        {
            foreach (Projectile2D projectile in FindObjectsByType<Projectile2D>())
            {
                if (projectile == null || projectile.owner == null) continue;
                CombatHealth ownerHealth = projectile.owner.GetComponent<CombatHealth>();
                if (ownerHealth != null && ownerHealth.Faction == ownerFaction) Destroy(projectile.gameObject);
            }
        }

        private void FixedUpdate()
        {
            if (spent || body == null) return;
            lifeRemaining -= Time.fixedDeltaTime;
            if (lifeRemaining <= 0f) { Destroy(gameObject); return; }

            Vector2 velocity = body.linearVelocity;
            float distance = velocity.magnitude * Time.fixedDeltaTime;
            if (distance <= 0f) return;
            transform.right = velocity.normalized;
            float remainingRange = maximumDistance - travelledDistance;
            if (remainingRange <= 0f) { Destroy(gameObject); return; }
            float castDistance = Mathf.Min(distance, remainingRange);
            hits.Clear();
            int count = Physics2D.CircleCast(body.position, 0.045f, velocity.normalized,
                hitFilter, hits, castDistance);
            float nearest = float.PositiveInfinity;
            RaycastHit2D selected = default;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.collider == null || hit.collider.isTrigger || hit.transform == transform ||
                    (owner != null && (hit.transform == owner.transform || hit.transform.IsChildOf(owner.transform))) ||
                    hit.collider.GetComponentInParent<WorldItemPickup>() != null) continue;
                if (hit.distance < nearest) { nearest = hit.distance; selected = hit; }
            }
            if (selected.collider != null)
            {
                CombatHealth target = selected.collider.GetComponentInParent<CombatHealth>();
                if (target != null && target.Faction == targetFaction)
                    target.TryTakeDamage(new DamageContext(damage, tier, owner, target, DamageType.Projectile));
                spent = true;
                Destroy(gameObject);
                return;
            }

            travelledDistance += castDistance;
            if (castDistance < distance)
            {
                body.position += velocity.normalized * castDistance;
                Destroy(gameObject);
            }
        }
    }
}

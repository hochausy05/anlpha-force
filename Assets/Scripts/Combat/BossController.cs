using System.Collections;
using Looter.Foundation;
using Looter.Items;
using Looter.Raid;
using UnityEngine;

namespace Looter.Combat
{
    [RequireComponent(typeof(CombatHealth), typeof(Rigidbody2D))]
    public sealed class BossController : MonoBehaviour
    {
        private enum Phase { One, Two, Dead }

        [SerializeField] private BossDefinition definition;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private BossChestLock protectedChest;
        [SerializeField] private Color telegraphColor = new Color(1f, 0.82f, 0.2f, 1f);
        [SerializeField, Min(0.1f)] private float meleeWarningSeconds = 0.7f;
        [SerializeField, Min(0.1f)] private float combatStartRange = 12f;
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        private CombatHealth health;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Collider2D[] colliders;
        private Transform player;
        private CombatHealth playerHealth;
        private RaidManager raidManager;
        private Vector3 homePosition;
        private Vector2 homeBodyPosition;
        private Color baseColor = Color.white;
        private Phase phase = Phase.One;
        private Coroutine attackRoutine;
        private float nextPlayerSearch;
        private float lastVisibleTime;
        private float nextAttackTime;
        private bool engaged;
        private bool deathHandled;
        private bool rangedNext;

        public string DisplayName => definition != null ? definition.DisplayName : "Vệ Binh Phế Tích";
        public int CurrentHealth => health != null ? health.CurrentHealth : 0;
        public int MaximumHealth => health != null ? health.MaxHealth : 1;
        public int CurrentPhase => phase == Phase.Two ? 2 : 1;
        public bool IsHudVisible => engaged;
        public bool IsDead => phase == Phase.Dead;
        public static bool ShouldEnterPhaseTwo(int currentHealth, int maximumHealth, float threshold) =>
            maximumHealth > 0 && currentHealth <= maximumHealth * Mathf.Clamp(threshold, 0.05f, 0.9f);

        public void Configure(BossDefinition bossDefinition)
        {
            definition = bossDefinition;
            if (health != null && definition != null) health.ConfigureMaximumHealth(definition.MaximumHealth);
        }

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            colliders = GetComponentsInChildren<Collider2D>();
            raidManager = FindAnyObjectByType<RaidManager>();
            homePosition = transform.position;
            homeBodyPosition = body.position;
            if (spriteRenderer != null) baseColor = spriteRenderer.color;
            if (definition != null) health.ConfigureMaximumHealth(definition.MaximumHealth);
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponent<CombatHealth>();
            if (health != null)
            {
                health.HealthChanged += OnHealthChanged;
                health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
                health.Died -= OnDied;
            }
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = null;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void Update()
        {
            if (phase == Phase.Dead || health == null || !health.IsAlive) return;
            if (raidManager == null) raidManager = FindAnyObjectByType<RaidManager>();
            if (raidManager != null && !raidManager.CanInteract)
            {
                if (attackRoutine != null) StopCoroutine(attackRoutine);
                attackRoutine = null;
                if (body != null) body.linearVelocity = Vector2.zero;
                return;
            }
            FindPlayerIfNeeded();
            if (player == null || playerHealth == null || !playerHealth.IsAlive) { StopCombat(); return; }
            float distance = Vector2.Distance(transform.position, player.position);
            bool inLeash = Vector2.Distance(homePosition, player.position) <= (definition != null ? definition.LeashRadius : 14f);
            float alertRange = definition != null ? definition.DetectionRange : combatStartRange;
            bool visible = inLeash && distance <= alertRange && HasLineOfSight();
            if (!visible)
            {
                if (Time.time - lastVisibleTime > 3f)
                {
                    engaged = false;
                    if (attackRoutine != null) StopCombat();
                }
                if (body != null)
                {
                    float delta = homePosition.x - transform.position.x;
                    body.linearVelocity = new Vector2(Mathf.Abs(delta) > 0.3f ? Mathf.Sign(delta) * MoveSpeed : 0f, body.linearVelocity.y);
                }
                return;
            }
            lastVisibleTime = Time.time;
            engaged = true;
            if (attackRoutine == null && Time.time >= nextAttackTime) attackRoutine = StartCoroutine(CombatLoop());
            MoveTowardTarget(distance);
        }

        public void ConfigureChest(BossChestLock chest)
        {
            protectedChest = chest;
            if (protectedChest != null && !IsDead) protectedChest.LockForNewRaid();
        }

        public void StopForRaidEnd()
        {
            StopCombat();
            Projectile2D.CancelOwnedBy(gameObject);
        }

        public void ResetForNewRaid()
        {
            StopCombat();
            deathHandled = false;
            phase = Phase.One;
            engaged = false;
            rangedNext = false;
            nextAttackTime = 0f;
            lastVisibleTime = 0f;
            transform.position = homePosition;
            if (definition != null && health != null) health.ConfigureMaximumHealth(definition.MaximumHealth);
            if (body != null)
            {
                body.simulated = true;
                body.position = homeBodyPosition;
                body.linearVelocity = Vector2.zero;
            }
            foreach (Collider2D collider in colliders) if (collider != null) collider.enabled = true;
            if (spriteRenderer != null) { spriteRenderer.enabled = true; spriteRenderer.color = baseColor; }
            if (protectedChest != null) protectedChest.LockForNewRaid();
        }

        private IEnumerator CombatLoop()
        {
            while (phase != Phase.Dead && player != null && playerHealth != null && playerHealth.IsAlive)
            {
                float distance = Vector2.Distance(transform.position, player.position);
                if (distance <= MeleeRange && (!rangedNext || definition == null || definition.ProjectilePrefab == null))
                    yield return HeavyMeleeAttack();
                else
                    yield return EnergyAttack();
                rangedNext = !rangedNext;
                float interval = definition != null ? definition.AttackInterval : 2.1f;
                if (phase == Phase.Two) interval *= 0.8f;
                nextAttackTime = Time.time + interval;
                while (phase != Phase.Dead && Time.time < nextAttackTime)
                    yield return null;
            }
            attackRoutine = null;
        }

        private IEnumerator HeavyMeleeAttack()
        {
            float windup = definition != null ? definition.HeavyWindup : meleeWarningSeconds;
            SetTint(telegraphColor);
            float until = Time.time + windup;
            while (Time.time < until)
            {
                if (!CanContinueAttack()) { RestoreTint(); yield break; }
                yield return null;
            }
            RestoreTint();
            if (!CanContinueAttack() || Vector2.Distance(transform.position, player.position) > MeleeRange || !HasLineOfSight()) yield break;
            int damage = definition != null ? definition.MeleeDamage : 25;
            ItemTier tier = definition != null ? definition.AttackTier : ItemTier.IV;
            playerHealth.TryTakeDamage(new DamageContext(damage, tier, gameObject, playerHealth, DamageType.Melee));
        }

        private IEnumerator EnergyAttack()
        {
            SetTint(telegraphColor);
            float windup = Mathf.Max(0.2f, (definition != null ? definition.HeavyWindup : meleeWarningSeconds) * 0.65f);
            float until = Time.time + windup;
            while (Time.time < until)
            {
                if (!CanContinueAttack()) { RestoreTint(); yield break; }
                yield return null;
            }
            RestoreTint();
            if (!CanContinueAttack() || !HasLineOfSight()) yield break;
            Vector2 origin = projectileOrigin != null ? projectileOrigin.position : transform.position + Vector3.up * 0.25f;
            Vector2 target = (Vector2)player.position + Vector2.up * 0.25f;
            GameObject projectile = definition != null && definition.ProjectilePrefab != null
                ? Instantiate(definition.ProjectilePrefab, origin, Quaternion.identity)
                : CreateFallbackProjectile(origin);
            Projectile2D projectileLogic = projectile.GetComponent<Projectile2D>();
            if (projectileLogic == null) { Destroy(projectile); yield break; }
            projectileLogic.Initialize(gameObject, definition != null ? definition.AttackTier : ItemTier.IV,
                definition != null ? definition.EnergyDamage : 20, target - origin,
                definition != null ? definition.ProjectileSpeed : 9f, 0f,
                definition != null ? definition.ProjectileRange : 12f, CombatFaction.Player);
        }

        private void MoveTowardTarget(float distance)
        {
            if (body == null) return;
            float targetRange = MeleeRange * 0.75f;
            float direction = player.position.x - transform.position.x;
            if (spriteRenderer != null && Mathf.Abs(direction) > 0.02f) spriteRenderer.flipX = direction < 0f;
            float speed = MoveSpeed * (phase == Phase.Two ? 1.2f : 1f);
            float moveDirection = distance > targetRange ? Mathf.Sign(direction) : 0f;
            body.linearVelocity = new Vector2(moveDirection != 0f && CanMove(moveDirection) ? moveDirection * speed : 0f,
                body.linearVelocity.y);
        }

        private bool CanMove(float direction)
        {
            Collider2D ownCollider = GetComponent<Collider2D>();
            Bounds bounds = ownCollider != null ? ownCollider.bounds : new Bounds(transform.position, Vector3.one);
            Vector2 side = new Vector2(direction > 0f ? bounds.max.x : bounds.min.x, bounds.center.y);
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(side, Vector2.right * direction, 0.4f, lineOfSightMask))
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.transform == transform || hit.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<WorldItemPickup>() != null || hit.collider.GetComponentInParent<LootContainer>() != null) continue;
                return false;
            }
            Vector2 foot = new Vector2(side.x + direction * 0.35f, bounds.min.y + 0.04f);
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(foot, Vector2.down, 0.35f, lineOfSightMask))
                if (hit.collider != null && !hit.collider.isTrigger && hit.normal.y > 0.45f &&
                    hit.collider.GetComponentInParent<CombatHealth>() == null) return true;
            return false;
        }

        private bool HasLineOfSight()
        {
            if (player == null) return false;
            Vector2 origin = transform.position + Vector3.up * 0.3f;
            Vector2 delta = (Vector2)player.position + Vector2.up * 0.25f - origin;
            float distance = delta.magnitude;
            if (distance < 0.05f) return true;
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(origin, delta / distance, distance, lineOfSightMask))
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.transform == transform || hit.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<WorldItemPickup>() != null ||
                    hit.collider.GetComponentInParent<LootContainer>() != null ||
                    hit.collider.GetComponentInParent<DroneVisualFollower>() != null) continue;
                return hit.collider.GetComponentInParent<CombatHealth>() == playerHealth;
            }
            return true;
        }

        private bool CanContinueAttack() => phase != Phase.Dead && health != null && health.IsAlive &&
            (raidManager == null || raidManager.CanInteract) && playerHealth != null && playerHealth.IsAlive;

        private void FindPlayerIfNeeded()
        {
            if (player != null || Time.time < nextPlayerSearch) return;
            nextPlayerSearch = Time.time + 0.75f;
            GameObject target = GameObject.FindGameObjectWithTag("Player");
            if (target == null) return;
            player = target.transform;
            playerHealth = target.GetComponent<CombatHealth>();
        }

        private void OnHealthChanged(int current, int maximum)
        {
            if (phase != Phase.One || definition == null ||
                !ShouldEnterPhaseTwo(current, maximum, definition.PhaseTwoHealthFraction)) return;
            phase = Phase.Two;
            if (spriteRenderer != null) spriteRenderer.color = definition.PhaseTwoTint;
        }

        private void OnDied(CombatHealth _)
        {
            if (deathHandled) return;
            deathHandled = true;
            phase = Phase.Dead;
            StopCombat();
            Projectile2D.CancelOwnedBy(gameObject);
            if (protectedChest != null) protectedChest.Unlock();
            if (body != null) { body.linearVelocity = Vector2.zero; body.simulated = false; }
            foreach (Collider2D collider in colliders) if (collider != null) collider.enabled = false;
            if (spriteRenderer != null) spriteRenderer.enabled = false;
        }

        private void StopCombat()
        {
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = null;
            if (body != null) body.linearVelocity = Vector2.zero;
            RestoreTint();
        }

        private void SetTint(Color tint) { if (spriteRenderer != null) spriteRenderer.color = tint; }
        private void RestoreTint()
        {
            if (spriteRenderer == null) return;
            spriteRenderer.color = phase == Phase.Two && definition != null ? definition.PhaseTwoTint : baseColor;
        }
        private float MoveSpeed => definition != null ? definition.MovementSpeed : 2.5f;
        private float MeleeRange => definition != null ? definition.MeleeRange : 1.65f;

        private static GameObject CreateFallbackProjectile(Vector2 position)
        {
            GameObject projectile = new GameObject("Boss Energy Projectile");
            projectile.transform.position = position;
            projectile.AddComponent<Rigidbody2D>().gravityScale = 0f;
            projectile.AddComponent<CircleCollider2D>().isTrigger = true;
            projectile.AddComponent<Projectile2D>();
            return projectile;
        }
    }
}

using Looter.Foundation;
using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    public sealed class EnemyAI : MonoBehaviour
    {
        public enum State { Idle, Patrol, Chase, Attack, Return, Dead }

        [Header("Sinh lực và chiến đấu")]
        [SerializeField, Min(1)] private int attackDamage = 10;
        [SerializeField, Min(0.1f)] private float attackRange = 1.2f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.2f;
        [SerializeField] private ItemTier attackTier = ItemTier.I;
        [Header("Di chuyển và phát hiện")]
        [SerializeField, Min(0f)] private float movementSpeed = 2f;
        [SerializeField, Min(0.1f)] private float detectionRange = 6f;
        [SerializeField, Min(0.1f)] private float returnRange = 9f;
        [SerializeField, Min(0.1f)] private float patrolDistance = 3f;
        [SerializeField, Min(0f)] private float idleDuration = 0.5f;
        [Header("Cấu hình enemy mở rộng")]
        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private LayerMask lineOfSightObstacles = ~0;
        [SerializeField, Min(0.1f)] private float lostTargetMemorySeconds = 3f;
        [SerializeField, Min(0.1f)] private float wallProbeDistance = 0.45f;

        private CombatHealth health;
        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Transform player;
        private CombatHealth playerHealth;
        private float homeX;
        private Vector3 homePosition;
        private float direction = 1f;
        private float nextAttackTime;
        private float idleUntil;
        private float nextPlayerSearch;
        private State state;
        private Color originalColor = Color.white;
        private EnemyInventory enemyInventory;
        private EnemyPatrolRoute patrolRoute;
        private CapsuleCollider2D capsule;
        private int waypointIndex;
        private float lastSeenTime;
        private Vector2 lastKnownPlayerPosition;
        private Color configuredOriginalColor;
        private bool startingAmmunitionApplied;

        public State CurrentState => state;
        public EnemyDefinition Definition => definition;
        public bool IsElite => definition != null && definition.Archetype == EnemyArchetype.EliteGuard;

        public void Configure(EnemyDefinition enemyDefinition)
        {
            definition = enemyDefinition;
            ApplyDefinition();
        }

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            enemyInventory = GetComponent<EnemyInventory>();
            patrolRoute = GetComponent<EnemyPatrolRoute>();
            capsule = GetComponent<CapsuleCollider2D>();
            homeX = transform.position.x;
            homePosition = transform.position;
            if (spriteRenderer != null) originalColor = spriteRenderer.color;
            ApplyDefinition();
            EnterIdle();
        }

        private void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        private void Start()
        {
            ApplyDefinition();
            AddStartingAmmunition();
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void Update()
        {
            if (health == null || !health.IsAlive || state == State.Dead) return;
            FindPlayerIfNeeded();
            bool visible = player != null && HasLineOfSight(player.position);
            if (visible)
            {
                lastSeenTime = Time.time;
                lastKnownPlayerPosition = player.position;
            }
            float distance = player != null ? Vector2.Distance(transform.position, player.position) : float.PositiveInfinity;
            bool targetValid = playerHealth != null && playerHealth.IsAlive;
            bool insideLeash = player != null && Vector2.Distance(homePosition, player.position) <= returnRange;

            if (!targetValid)
            {
                player = null;
                playerHealth = null;
                if (state == State.Chase || state == State.Attack) state = State.Return;
            }
            else if ((state == State.Idle || state == State.Patrol) && visible && distance <= detectionRange && insideLeash)
            {
                state = State.Chase;
            }
            else if ((state == State.Chase || state == State.Attack) && (distance > returnRange || !insideLeash ||
                !visible && Time.time - lastSeenTime > lostTargetMemorySeconds))
            {
                state = State.Return;
            }

            switch (state)
            {
                case State.Idle: TickIdle(); break;
                case State.Patrol: TickPatrol(); break;
                case State.Chase: TickChase(distance, visible); break;
                case State.Attack: TickAttack(distance); break;
                case State.Return: TickReturn(); break;
            }
        }

        private void TickIdle()
        {
            SetHorizontalVelocity(0f);
            if (Time.time >= idleUntil) state = State.Patrol;
        }

        private void TickPatrol()
        {
            if (patrolRoute != null && patrolRoute.Count > 1)
            {
                Vector3 point = patrolRoute.GetPoint(waypointIndex);
                float delta = point.x - transform.position.x;
                if (Mathf.Abs(delta) <= 0.16f)
                {
                    waypointIndex = (waypointIndex + 1) % patrolRoute.Count;
                    EnterIdle();
                    return;
                }
                if (!CanMove(Mathf.Sign(delta))) { EnterIdle(); return; }
                MoveInDirection(Mathf.Sign(delta));
                return;
            }
            float destination = homeX + direction * patrolDistance;
            if (Mathf.Abs(transform.position.x - destination) <= 0.12f)
            {
                direction *= -1f;
                EnterIdle();
                return;
            }
            if (!CanMove(direction)) { direction *= -1f; EnterIdle(); return; }
            MoveInDirection(Mathf.Sign(destination - transform.position.x));
        }

        private void TickChase(float distance, bool currentlyVisible)
        {
            if (player == null) { state = State.Return; return; }
            if (!currentlyVisible && Time.time - lastSeenTime <= lostTargetMemorySeconds &&
                Vector2.Distance(transform.position, lastKnownPlayerPosition) > attackRange)
            {
                float searchDirection = Mathf.Sign(lastKnownPlayerPosition.x - transform.position.x);
                if (CanMove(searchDirection)) MoveInDirection(searchDirection);
                else { SetHorizontalVelocity(0f); state = State.Return; }
                return;
            }
            WeaponDefinition weapon = GetWeaponDefinition();
            if (weapon != null && weapon.AttackMode != WeaponAttackMode.Melee)
            {
                float preferred = definition != null ? definition.PreferredRange : weapon.AttackRange * 0.7f;
                if (distance < preferred * 0.65f)
                {
                    float away = Mathf.Sign(transform.position.x - player.position.x);
                    if (CanMove(away)) MoveInDirection(away); else SetHorizontalVelocity(0f);
                    return;
                }
                if (distance > preferred && CanMove(Mathf.Sign(player.position.x - transform.position.x)))
                { MoveInDirection(Mathf.Sign(player.position.x - transform.position.x)); return; }
                SetHorizontalVelocity(0f);
                Face(player.position.x - transform.position.x);
                if (visibleTarget()) TickAttack(distance);
                return;
            }
            if (distance <= attackRange)
            {
                SetHorizontalVelocity(0f);
                Face(player.position.x - transform.position.x);
                state = State.Attack;
                TickAttack(distance);
                return;
            }
            float chaseDirection = Mathf.Sign(player.position.x - transform.position.x);
            if (CanMove(chaseDirection)) MoveInDirection(chaseDirection);
            else { SetHorizontalVelocity(0f); state = State.Return; }

            bool visibleTarget() => player != null && HasLineOfSight(player.position);
        }

        private void TickAttack(float distance)
        {
            SetHorizontalVelocity(0f);
            WeaponDefinition configuredWeapon = GetWeaponDefinition();
            float effectiveRange = configuredWeapon != null && configuredWeapon.AttackMode != WeaponAttackMode.Melee
                ? configuredWeapon.AttackRange : attackRange;
            if (player == null || playerHealth == null || !playerHealth.IsAlive || distance > effectiveRange)
            {
                state = State.Chase;
                return;
            }
            Face(player.position.x - transform.position.x);
            if (Time.time < nextAttackTime) return;
            if (!HasLineOfSight(player.position)) return;
            WeaponDefinition weapon = GetWeaponDefinition();
            if (weapon != null && weapon.AttackMode != WeaponAttackMode.Melee)
            {
                if (!FireAtPlayer(weapon))
                {
                    nextAttackTime = Time.time + 0.6f;
                    if (distance <= attackRange) ApplyMeleeDamage(attackDamage, attackTier);
                }
                return;
            }
            ItemTier tier = enemyInventory != null && enemyInventory.EquippedWeapon != null
                ? enemyInventory.EquippedWeapon.Tier : definition != null ? definition.AttackTier : attackTier;
            int damage = weapon != null ? weapon.Damage : attackDamage;
            float cooldown = weapon != null ? weapon.AttackCooldown : attackCooldown;
            nextAttackTime = Time.time + cooldown;
            ApplyMeleeDamage(damage, tier);
        }

        private void ApplyMeleeDamage(int damage, ItemTier tier)
        {
            if (playerHealth == null || !playerHealth.IsAlive || !HasLineOfSight(player.position)) return;
            playerHealth.TryTakeDamage(new DamageContext(damage, tier, gameObject, playerHealth, DamageType.Melee));
        }

        private void TickReturn()
        {
            float offset = homeX - transform.position.x;
            if (Mathf.Abs(offset) <= 0.12f)
            {
                SetHorizontalVelocity(0f);
                EnterIdle();
                return;
            }
            float returnDirection = Mathf.Sign(offset);
            if (CanMove(returnDirection)) MoveInDirection(returnDirection);
            else SetHorizontalVelocity(0f);
        }

        private void MoveInDirection(float value)
        {
            direction = Mathf.Sign(value);
            Face(direction);
            SetHorizontalVelocity(direction * movementSpeed);
        }

        private bool CanMove(float moveDirection)
        {
            if (Mathf.Abs(moveDirection) < 0.01f) return true;
            Bounds bounds = capsule != null ? capsule.bounds : new Bounds(transform.position, Vector3.one * 0.6f);
            Vector2 side = new Vector2(moveDirection > 0f ? bounds.max.x : bounds.min.x, bounds.center.y);
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(side, Vector2.right * moveDirection,
                wallProbeDistance, lineOfSightObstacles))
            {
                if (IsIgnoredWorldCollider(hit.collider) || hit.distance <= 0.02f) continue;
                return false;
            }
            Vector2 foot = new Vector2(side.x + moveDirection * wallProbeDistance, bounds.min.y + 0.04f);
            bool hasGround = false;
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(foot, Vector2.down, 0.28f, lineOfSightObstacles))
            {
                if (IsIgnoredWorldCollider(hit.collider) || hit.normal.y < 0.45f) continue;
                hasGround = true;
                break;
            }
            return hasGround;
        }

        private bool HasLineOfSight(Vector3 targetPosition)
        {
            Vector2 origin = transform.position + Vector3.up * (capsule != null ? capsule.bounds.extents.y * 0.55f : 0.35f);
            Vector2 target = targetPosition + Vector3.up * 0.25f;
            Vector2 delta = target - origin;
            float distance = delta.magnitude;
            if (distance < 0.05f) return true;
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(origin, delta / distance, distance, lineOfSightObstacles))
            {
                Collider2D collider = hit.collider;
                if (collider == null || IsIgnoredWorldCollider(collider)) continue;
                CombatHealth hitHealth = collider.GetComponentInParent<CombatHealth>();
                if (hitHealth == playerHealth) return true;
                return false;
            }
            return true;
        }

        private bool IsIgnoredWorldCollider(Collider2D collider)
        {
            if (collider == null || collider.isTrigger || collider.transform == transform || collider.transform.IsChildOf(transform)) return true;
            if (collider.GetComponentInParent<WorldItemPickup>() != null ||
                collider.GetComponentInParent<LootContainer>() != null ||
                collider.GetComponentInParent<DroneVisualFollower>() != null) return true;
            return false;
        }

        private WeaponDefinition GetWeaponDefinition()
        {
            ItemDefinition item = enemyInventory != null ? enemyInventory.EquippedWeapon : null;
            if (item == null && definition != null) item = definition.Weapon;
            return item != null ? item.WeaponDefinition : null;
        }

        private bool FireAtPlayer(WeaponDefinition weapon)
        {
            if (player == null || weapon == null || weapon.Ammunition == null || enemyInventory == null ||
                !enemyInventory.TryConsumeAmmunition(weapon.Ammunition, 1))
            {
                SetHorizontalVelocity(0f);
                return false;
            }
            Vector2 origin = transform.position + Vector3.up * 0.2f;
            Vector2 directionToPlayer = ((Vector2)player.position + Vector2.up * 0.25f - origin).normalized;
            GameObject projectile = weapon.ProjectilePrefab != null
                ? Instantiate(weapon.ProjectilePrefab, origin, Quaternion.identity)
                : CreateFallbackProjectile(origin);
            Projectile2D projectileLogic = projectile.GetComponent<Projectile2D>();
            if (projectileLogic == null)
            {
                Destroy(projectile);
                return false;
            }
            projectileLogic.Initialize(gameObject,
                enemyInventory.EquippedWeapon != null ? enemyInventory.EquippedWeapon.Tier : definition.AttackTier,
                weapon.Damage, directionToPlayer, weapon.ProjectileSpeed,
                weapon.ProjectileGravityScale, weapon.AttackRange, CombatFaction.Player);
            nextAttackTime = Time.time + weapon.AttackCooldown;
            return true;
        }

        private GameObject CreateFallbackProjectile(Vector2 position)
        {
            GameObject instance = new GameObject("Enemy Projectile");
            instance.transform.position = position;
            Rigidbody2D projectileBody = instance.AddComponent<Rigidbody2D>();
            projectileBody.gravityScale = 0f;
            CircleCollider2D projectileCollider = instance.AddComponent<CircleCollider2D>();
            projectileCollider.isTrigger = true;
            instance.AddComponent<Projectile2D>();
            return instance;
        }

        private void ApplyDefinition()
        {
            if (definition == null) return;
            if (enemyInventory != null && definition.Weapon != null && enemyInventory.EquippedWeapon == null)
                enemyInventory.SetGuaranteedWeapon(definition.Weapon);
            attackDamage = definition.UnarmedDamage;
            attackTier = definition.AttackTier;
            movementSpeed = definition.MovementSpeed;
            detectionRange = definition.DetectionRange;
            attackRange = definition.AttackRange;
            attackCooldown = definition.AttackCooldown;
            returnRange = definition.LeashRadius;
            patrolDistance = definition.PatrolDistance;
            idleDuration = definition.IdleDuration;
            if (health != null) health.ConfigureMaximumHealth(definition.MaximumHealth);
            if (spriteRenderer != null)
            {
                if (configuredOriginalColor == default) configuredOriginalColor = spriteRenderer.color;
                spriteRenderer.color = definition.VisualTint;
            }
        }

        private void AddStartingAmmunition()
        {
            if (startingAmmunitionApplied) return;
            startingAmmunitionApplied = true;
            if (enemyInventory == null || definition == null || definition.StartingAmmunition <= 0) return;
            WeaponDefinition weapon = GetWeaponDefinition();
            if (weapon != null && weapon.Ammunition != null)
                enemyInventory.AddCarriedItem(weapon.Ammunition, definition.StartingAmmunition);
        }

        private void Face(float value)
        {
            if (Mathf.Abs(value) < 0.001f || spriteRenderer == null) return;
            spriteRenderer.flipX = value < 0f;
        }

        private void SetHorizontalVelocity(float value)
        {
            if (body == null) return;
            Vector2 velocity = body.linearVelocity;
            velocity.x = value;
            body.linearVelocity = velocity;
        }

        private void EnterIdle()
        {
            state = State.Idle;
            idleUntil = Time.time + idleDuration;
        }

        private void FindPlayerIfNeeded()
        {
            if (player != null || Time.time < nextPlayerSearch) return;
            nextPlayerSearch = Time.time + 1f;
            GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
            if (foundPlayer == null) return;
            CombatHealth foundHealth = foundPlayer.GetComponent<CombatHealth>();
            if (foundHealth == null || foundHealth.Faction != CombatFaction.Player) return;
            player = foundPlayer.transform;
            playerHealth = foundHealth;
        }

        private void OnDied(CombatHealth _)
        {
            if (state == State.Dead) return;
            state = State.Dead;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
            foreach (Collider2D hitbox in GetComponentsInChildren<Collider2D>()) hitbox.enabled = false;
            if (spriteRenderer != null) spriteRenderer.enabled = false;
        }

        public void ResetForNewRaid()
        {
            transform.position = homePosition;
            homeX = homePosition.x;
            direction = 1f;
            player = null;
            playerHealth = null;
            nextAttackTime = 0f;
            nextPlayerSearch = 0f;
            if (body != null)
            {
                body.simulated = true;
                body.linearVelocity = Vector2.zero;
            }
            foreach (Collider2D hitbox in GetComponentsInChildren<Collider2D>()) hitbox.enabled = true;
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = definition != null ? definition.VisualTint : originalColor;
            }
            if (health != null) health.ResetForNewRaid();
            waypointIndex = 0;
            lastSeenTime = 0f;
            ApplyDefinition();
            startingAmmunitionApplied = false;
            AddStartingAmmunition();
            enabled = true;
            EnterIdle();
        }

        private void OnValidate()
        {
            attackDamage = Mathf.Max(1, attackDamage);
            attackTier = (ItemTier)Mathf.Clamp((int)attackTier, (int)ItemTier.I, (int)ItemTier.VI);
            attackRange = Mathf.Max(0.1f, attackRange);
            attackCooldown = Mathf.Max(0.1f, attackCooldown);
            movementSpeed = Mathf.Max(0f, movementSpeed);
            detectionRange = Mathf.Max(0.1f, detectionRange);
            returnRange = Mathf.Max(detectionRange, returnRange);
            patrolDistance = Mathf.Max(0.1f, patrolDistance);
            idleDuration = Mathf.Max(0f, idleDuration);
            lostTargetMemorySeconds = Mathf.Max(0.1f, lostTargetMemorySeconds);
            wallProbeDistance = Mathf.Max(0.1f, wallProbeDistance);
        }
    }
}

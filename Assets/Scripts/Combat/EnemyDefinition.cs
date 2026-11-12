using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    public enum EnemyArchetype { Slime, Raider, Archer, EliteGuard }

    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Pixel Looter/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private string enemyId;
        [SerializeField] private string displayName = "Bot";
        [SerializeField] private EnemyArchetype archetype;
        [SerializeField, Min(1)] private int maximumHealth = 40;
        [SerializeField, Min(1)] private int unarmedDamage = 8;
        [SerializeField] private ItemTier attackTier = ItemTier.I;
        [SerializeField, Min(0f)] private float movementSpeed = 2f;
        [SerializeField, Min(0.1f)] private float detectionRange = 5f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.2f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.2f;
        [SerializeField, Min(0.1f)] private float leashRadius = 9f;
        [SerializeField, Min(0f)] private float patrolDistance = 3f;
        [SerializeField, Min(0f)] private float idleDuration = 0.5f;
        [SerializeField] private ItemDefinition weapon;
        [SerializeField, Min(0f)] private float preferredRange = 4.5f;
        [SerializeField, Min(0)] private int startingAmmunition = 12;
        [SerializeField] private Color visualTint = Color.white;

        public string EnemyId => enemyId;
        public string DisplayName => displayName;
        public EnemyArchetype Archetype => archetype;
        public int MaximumHealth => Mathf.Max(1, maximumHealth);
        public int UnarmedDamage => Mathf.Max(1, unarmedDamage);
        public ItemTier AttackTier => attackTier;
        public float MovementSpeed => Mathf.Max(0f, movementSpeed);
        public float DetectionRange => Mathf.Max(0.1f, detectionRange);
        public float AttackRange => Mathf.Max(0.1f, attackRange);
        public float AttackCooldown => Mathf.Max(0.1f, attackCooldown);
        public float LeashRadius => Mathf.Max(detectionRange, leashRadius);
        public float PatrolDistance => Mathf.Max(0f, patrolDistance);
        public float IdleDuration => Mathf.Max(0f, idleDuration);
        public ItemDefinition Weapon => weapon;
        public float PreferredRange => Mathf.Max(0f, preferredRange);
        public int StartingAmmunition => Mathf.Max(0, startingAmmunition);
        public Color VisualTint => visualTint;

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(1, maximumHealth);
            unarmedDamage = Mathf.Max(1, unarmedDamage);
            movementSpeed = Mathf.Max(0f, movementSpeed);
            detectionRange = Mathf.Max(0.1f, detectionRange);
            attackRange = Mathf.Max(0.1f, attackRange);
            attackCooldown = Mathf.Max(0.1f, attackCooldown);
            leashRadius = Mathf.Max(detectionRange, leashRadius);
            patrolDistance = Mathf.Max(0f, patrolDistance);
            idleDuration = Mathf.Max(0f, idleDuration);
            startingAmmunition = Mathf.Max(0, startingAmmunition);
            attackTier = (ItemTier)Mathf.Clamp((int)attackTier, (int)ItemTier.I, (int)ItemTier.VI);
        }
    }
}

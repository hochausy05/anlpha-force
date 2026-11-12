using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    [CreateAssetMenu(fileName = "BossDefinition", menuName = "Pixel Looter/Boss Definition")]
    public sealed class BossDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Vệ Binh Phế Tích";
        [SerializeField, Min(1)] private int maximumHealth = 400;
        [SerializeField] private ItemTier attackTier = ItemTier.IV;
        [SerializeField, Min(1)] private int meleeDamage = 25;
        [SerializeField, Min(0.1f)] private float movementSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float meleeRange = 1.65f;
        [SerializeField, Min(0.1f)] private float detectionRange = 12f;
        [SerializeField, Min(0.1f)] private float leashRadius = 14f;
        [SerializeField, Range(0.05f, 0.9f)] private float phaseTwoHealthFraction = 0.4f;
        [SerializeField, Min(0.1f)] private float attackInterval = 2.1f;
        [SerializeField, Min(0f)] private float heavyWindup = 0.7f;
        [SerializeField, Min(1)] private int energyDamage = 20;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 9f;
        [SerializeField, Min(0.1f)] private float projectileRange = 12f;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Color phaseTwoTint = new Color(1f, 0.35f, 0.28f, 1f);

        public string DisplayName => displayName;
        public int MaximumHealth => Mathf.Max(1, maximumHealth);
        public ItemTier AttackTier => attackTier;
        public int MeleeDamage => Mathf.Max(1, meleeDamage);
        public float MovementSpeed => Mathf.Max(0.1f, movementSpeed);
        public float MeleeRange => Mathf.Max(0.1f, meleeRange);
        public float DetectionRange => Mathf.Max(0.1f, detectionRange);
        public float LeashRadius => Mathf.Max(0.1f, leashRadius);
        public float PhaseTwoHealthFraction => Mathf.Clamp(phaseTwoHealthFraction, 0.05f, 0.9f);
        public float AttackInterval => Mathf.Max(0.1f, attackInterval);
        public float HeavyWindup => Mathf.Max(0f, heavyWindup);
        public int EnergyDamage => Mathf.Max(1, energyDamage);
        public float ProjectileSpeed => Mathf.Max(0.1f, projectileSpeed);
        public float ProjectileRange => Mathf.Max(0.1f, projectileRange);
        public GameObject ProjectilePrefab => projectilePrefab;
        public Color PhaseTwoTint => phaseTwoTint;

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(1, maximumHealth);
            meleeDamage = Mathf.Max(1, meleeDamage);
            movementSpeed = Mathf.Max(0.1f, movementSpeed);
            meleeRange = Mathf.Max(0.1f, meleeRange);
            detectionRange = Mathf.Max(0.1f, detectionRange);
            leashRadius = Mathf.Max(detectionRange, leashRadius);
            phaseTwoHealthFraction = Mathf.Clamp(phaseTwoHealthFraction, 0.05f, 0.9f);
            attackInterval = Mathf.Max(0.1f, attackInterval);
            heavyWindup = Mathf.Max(0f, heavyWindup);
            energyDamage = Mathf.Max(1, energyDamage);
            projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            projectileRange = Mathf.Max(0.1f, projectileRange);
            attackTier = (ItemTier)Mathf.Clamp((int)attackTier, (int)ItemTier.I, (int)ItemTier.VI);
        }
    }
}

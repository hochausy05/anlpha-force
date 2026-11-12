using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    public enum WeaponAttackMode { Melee, Bow, Pistol }

    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Pixel Looter/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string weaponId;
        [SerializeField] private WeaponAttackMode attackMode;
        [SerializeField, Min(1)] private int damage = 10;
        [SerializeField, Min(0.05f)] private float attackCooldown = 0.5f;
        [SerializeField, Min(0.1f)] private float attackRange = 1.2f;
        [SerializeField] private ItemDefinition ammunition;
        [SerializeField, Min(0)] private int magazineCapacity;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 14f;
        [SerializeField, Min(0f)] private float projectileGravityScale;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Sprite icon;

        public string WeaponId => weaponId;
        public WeaponAttackMode AttackMode => attackMode;
        public int Damage => Mathf.Max(1, damage);
        public float AttackCooldown => Mathf.Max(0.05f, attackCooldown);
        public float AttackRange => Mathf.Max(0.1f, attackRange);
        public ItemDefinition Ammunition => ammunition;
        public int MagazineCapacity => Mathf.Max(0, magazineCapacity);
        public float ProjectileSpeed => Mathf.Max(0.1f, projectileSpeed);
        public float ProjectileGravityScale => Mathf.Max(0f, projectileGravityScale);
        public GameObject ProjectilePrefab => projectilePrefab;
        public Sprite Icon => icon;

        private void OnValidate()
        {
            damage = Mathf.Max(1, damage);
            attackCooldown = Mathf.Max(0.05f, attackCooldown);
            attackRange = Mathf.Max(0.1f, attackRange);
            magazineCapacity = Mathf.Max(0, magazineCapacity);
            projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            projectileGravityScale = Mathf.Max(0f, projectileGravityScale);
        }
    }
}

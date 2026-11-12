using UnityEngine;
using Looter.Items;

namespace Looter.Combat
{
    public enum DamageType { Generic, Melee, Projectile, Skill }

    public readonly struct DamageContext
    {
        public int BaseDamage { get; }
        public ItemTier AttackerTier { get; }
        public GameObject Attacker { get; }
        public CombatHealth Recipient { get; }
        public DamageType Type { get; }

        public DamageContext(int baseDamage, ItemTier attackerTier, GameObject attacker,
            CombatHealth recipient, DamageType type = DamageType.Generic)
        {
            BaseDamage = Mathf.Max(0, baseDamage);
            AttackerTier = attackerTier;
            Attacker = attacker;
            Recipient = recipient;
            Type = type;
        }
    }

    public readonly struct DamageResolution
    {
        public int PlayerDamage { get; }
        public int DroneDamage { get; }

        public DamageResolution(int playerDamage, int droneDamage)
        {
            PlayerDamage = Mathf.Max(0, playerDamage);
            DroneDamage = Mathf.Max(0, droneDamage);
        }
    }
}

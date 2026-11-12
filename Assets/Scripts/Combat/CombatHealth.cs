using System;
using System.Collections;
using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    public enum CombatFaction { Neutral, Player, Enemy }

    public sealed class CombatHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0f)] private float invulnerabilitySeconds;
        [SerializeField] private CombatFaction faction;
        [SerializeField, Min(0f)] private float damageFlashSeconds = 0.12f;

        private int currentHealth;
        private float nextDamageTime;
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private Coroutine flashRoutine;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsAlive => currentHealth > 0;
        public CombatFaction Faction { get => faction; set => faction = value; }
        public event Action<int, int> HealthChanged;
        public event Action<CombatHealth> Died;

        public void ConfigureMaximumHealth(int value)
        {
            maxHealth = Mathf.Max(1, value);
            currentHealth = maxHealth;
            nextDamageTime = 0f;
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Awake()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = maxHealth;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) originalColor = spriteRenderer.color;
        }

        public bool TryTakeDamage(int amount, GameObject source)
        {
            return TryTakeDamage(amount, source, ItemTier.I, DamageType.Generic);
        }

        public bool TryTakeDamage(int amount, GameObject source, ItemTier attackerTier,
            DamageType damageType = DamageType.Generic)
        {
            return TryTakeDamage(new DamageContext(amount, attackerTier, source, this, damageType));
        }

        public bool TryTakeDamage(DamageContext context)
        {
            if (context.Recipient != this) return false;
            int amount = context.BaseDamage;
            if (!IsAlive || amount <= 0 || Time.time < nextDamageTime) return false;

            int finalHealthDamage = amount;
            if (faction == CombatFaction.Player)
            {
                DroneProtection protection = GetComponent<DroneProtection>();
                DamageResolution resolution = DamageResolver.Resolve(context.BaseDamage, context.AttackerTier,
                    protection != null && protection.IsOperational ? protection.EquippedDrone : null);
                int absorbed = protection != null ? protection.ApplyDamage(resolution.DroneDamage) : 0;
                finalHealthDamage = amount - absorbed;
            }
            if (finalHealthDamage <= 0) return false;

            nextDamageTime = Time.time + invulnerabilitySeconds;
            currentHealth = Mathf.Clamp(currentHealth - finalHealthDamage, 0, maxHealth);
            HealthChanged?.Invoke(currentHealth, maxHealth);
            Flash();

            if (currentHealth == 0) Died?.Invoke(this);
            return true;
        }

        public void RestoreHealth(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            int updatedHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
            if (updatedHealth == currentHealth) return;
            currentHealth = updatedHealth;
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void ResetForNewRaid()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = maxHealth;
            nextDamageTime = Time.time;
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Flash()
        {
            if (spriteRenderer == null || damageFlashSeconds <= 0f) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(DamageFlash());
        }

        private IEnumerator DamageFlash()
        {
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(damageFlashSeconds);
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            flashRoutine = null;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            invulnerabilitySeconds = Mathf.Max(0f, invulnerabilitySeconds);
            damageFlashSeconds = Mathf.Max(0f, damageFlashSeconds);
        }
    }
}

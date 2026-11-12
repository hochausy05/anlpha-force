using System.Collections.Generic;
using Looter.Foundation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Looter.Combat
{
    [RequireComponent(typeof(PlayerController2D), typeof(CombatHealth))]
    public sealed class PlayerMeleeAttack : MonoBehaviour
    {
        [Header("Dao bậc I")]
        [SerializeField, Min(1)] private int damage = 20;
        [SerializeField, Min(0.1f)] private float attackRange = 1.2f;
        [SerializeField, Min(0f)] private float attackCooldown = 0.5f;
        [SerializeField] private Vector2 hitboxSize = new Vector2(0.9f, 1.2f);

        private PlayerController2D playerController;
        private CombatHealth ownerHealth;
        private LineRenderer slashEffect;
        private Material slashMaterial;
        private float nextAttackTime;
        private float effectEndTime;

        private void Awake()
        {
            playerController = GetComponent<PlayerController2D>();
            ownerHealth = GetComponent<CombatHealth>();
            CreateSlashEffect();
        }

        private void Update()
        {
            if (ownerHealth == null || !ownerHealth.IsAlive) return;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) TryAttack();
            if (slashEffect != null && Time.time >= effectEndTime) slashEffect.positionCount = 0;
        }

        public bool TryAttack()
        {
            if (ownerHealth == null || !ownerHealth.IsAlive || Time.time < nextAttackTime) return false;
            nextAttackTime = Time.time + attackCooldown;

            float facing = playerController.FacingDirection;
            Vector2 center = (Vector2)transform.position + new Vector2(facing * (attackRange - hitboxSize.x * 0.5f), 0.05f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, hitboxSize, 0f);
            HashSet<CombatHealth> damagedTargets = new HashSet<CombatHealth>();
            bool hitSomething = false;

            foreach (Collider2D hit in hits)
            {
                CombatHealth target = hit.GetComponentInParent<CombatHealth>();
                if (target == null || target == ownerHealth || target.Faction != CombatFaction.Enemy) continue;
                if (!damagedTargets.Add(target)) continue;
                if (target.TryTakeDamage(damage, gameObject)) hitSomething = true;
            }

            ShowSlash(facing, hitSomething);
            return hitSomething;
        }

        private void CreateSlashEffect()
        {
            GameObject effectObject = new GameObject("Melee Slash Effect");
            effectObject.transform.SetParent(transform, false);
            slashEffect = effectObject.AddComponent<LineRenderer>();
            slashEffect.useWorldSpace = true;
            slashEffect.positionCount = 0;
            slashEffect.startWidth = 0.1f;
            slashEffect.endWidth = 0.035f;
            slashEffect.sortingOrder = 10;
            slashEffect.numCapVertices = 2;
            slashEffect.numCornerVertices = 2;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                slashMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                slashEffect.sharedMaterial = slashMaterial;
            }
        }

        private void ShowSlash(float facing, bool hit)
        {
            ShowSlash(facing, hit, 0.9f);
        }

        private void ShowSlash(float facing, bool hit, float radius)
        {
            if (slashEffect == null) return;
            const int points = 6;
            Vector3[] arc = new Vector3[points];
            Vector2 origin = (Vector2)transform.position + Vector2.up * 0.05f;
            for (int i = 0; i < points; i++)
            {
                float angle = Mathf.Lerp(-65f, 65f, i / (points - 1f)) * Mathf.Deg2Rad;
                arc[i] = origin + new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle)) * radius;
            }
            slashEffect.startColor = hit ? new Color(1f, 0.95f, 0.45f, 1f) : Color.white;
            slashEffect.endColor = hit ? new Color(1f, 0.45f, 0.15f, 0.6f) : new Color(0.8f, 0.9f, 1f, 0.5f);
            slashEffect.positionCount = points;
            slashEffect.SetPositions(arc);
            effectEndTime = Time.time + 0.14f;
        }

        public void ShowWeaponSlash(float facing, bool hit, float range)
        {
            ShowSlash(facing, hit, Mathf.Max(0.5f, range));
        }

        private void OnValidate()
        {
            damage = Mathf.Max(1, damage);
            attackRange = Mathf.Max(0.1f, attackRange);
            attackCooldown = Mathf.Max(0f, attackCooldown);
            hitboxSize.x = Mathf.Max(0.1f, hitboxSize.x);
            hitboxSize.y = Mathf.Max(0.1f, hitboxSize.y);
        }

        private void OnDestroy()
        {
            if (slashMaterial != null)
            {
                if (Application.isPlaying) Destroy(slashMaterial);
                else DestroyImmediate(slashMaterial);
            }
        }
    }
}

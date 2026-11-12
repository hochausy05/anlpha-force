using Looter.Foundation;
using UnityEngine;

namespace Looter.Combat
{
    [RequireComponent(typeof(CombatHealth), typeof(PlayerController2D))]
    public sealed class PlayerDeathHandler : MonoBehaviour
    {
        private CombatHealth health;
        private PlayerController2D movement;
        private PlayerMeleeAttack attack;
        private Rigidbody2D body;

        private void Awake()
        {
            health = GetComponent<CombatHealth>();
            movement = GetComponent<PlayerController2D>();
            attack = GetComponent<PlayerMeleeAttack>();
            body = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied(CombatHealth _) 
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
            if (movement != null) movement.enabled = false;
            if (attack != null) attack.enabled = false;
        }

        public void PrepareForNewRaid()
        {
            if (body != null)
            {
                body.simulated = true;
                body.linearVelocity = Vector2.zero;
            }
            if (movement != null) movement.enabled = true;
            if (movement != null) movement.ResetForNewRaid();
            if (attack != null) attack.enabled = true;
            if (health != null) health.ResetForNewRaid();
        }
    }
}

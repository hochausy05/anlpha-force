using UnityEngine;

namespace Looter.Raid
{
    [RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
    public sealed class ExtractionZone : MonoBehaviour
    {
        [SerializeField] private string displayName = "Điểm rút quân";
        [SerializeField, Min(0.5f)] private float interactionRadius = 1.65f;

        public string DisplayName => displayName;
        public float InteractionRadius => interactionRadius;

        public bool IsPlayerInside(Transform target)
        {
            return target != null && Vector2.Distance(target.position, transform.position) <= interactionRadius;
        }

        public void Configure(string label, float radius)
        {
            displayName = label;
            interactionRadius = Mathf.Max(0.5f, radius);
            CircleCollider2D trigger = GetComponent<CircleCollider2D>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
                trigger.radius = interactionRadius;
            }
        }

        private void OnValidate()
        {
            interactionRadius = Mathf.Max(0.5f, interactionRadius);
            CircleCollider2D trigger = GetComponent<CircleCollider2D>();
            if (trigger != null) trigger.radius = interactionRadius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.25f);
            Gizmos.DrawSphere(transform.position, interactionRadius);
        }
    }
}

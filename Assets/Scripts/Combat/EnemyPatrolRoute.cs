using UnityEngine;

namespace Looter.Combat
{
    public sealed class EnemyPatrolRoute : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        public int Count => waypoints != null ? waypoints.Length : 0;
        public Vector3 GetPoint(int index) => waypoints[index] != null ? waypoints[index].position : transform.position;

        private void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Length < 2) return;
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.8f);
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawSphere(waypoints[i].position, 0.12f);
                Transform next = waypoints[(i + 1) % waypoints.Length];
                if (next != null) Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }
    }
}

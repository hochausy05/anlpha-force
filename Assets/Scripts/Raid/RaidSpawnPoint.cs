using UnityEngine;

namespace Looter.Raid
{
    public sealed class RaidSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string displayName = "Điểm xuất phát";
        public string DisplayName => displayName;

        public void Configure(string label) => displayName = label;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 1.4f, 0f));
        }
    }
}

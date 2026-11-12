using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    [RequireComponent(typeof(LootContainer))]
    public sealed class BossChestLock : MonoBehaviour
    {
        [SerializeField] private bool startsLocked = true;
        private LootContainer container;

        public bool IsUnlocked => container != null && !container.IsLocked;

        private void Awake()
        {
            container = GetComponent<LootContainer>();
            if (startsLocked) container.SetLocked(true, "Rương bị khóa khi Vệ Binh Phế Tích còn sống.");
        }

        public void Unlock()
        {
            if (container == null) container = GetComponent<LootContainer>();
            if (container != null) container.SetLocked(false);
        }

        public void LockForNewRaid()
        {
            if (container == null) container = GetComponent<LootContainer>();
            if (container != null) container.SetLocked(true, "Rương bị khóa khi Vệ Binh Phế Tích còn sống.");
        }
    }
}

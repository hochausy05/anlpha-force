using Looter.Economy;
using Looter.Combat;
using Looter.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Looter.Hub
{
    public sealed class BaseHubController : MonoBehaviour
    {
        [SerializeField] private ShopCatalog shopCatalog;
        [SerializeField] private DroneSystemSettings droneSettings;
        private PlayerProfileStore profile;

        public PlayerProfileStore Profile => profile;
        public ShopCatalog Shop => shopCatalog;
        public DroneSystemSettings DroneSettings => droneSettings;

        private void Start()
        {
            profile = PlayerProfileStore.Instance;
            if (profile == null) profile = FindAnyObjectByType<PlayerProfileStore>();
        }

        public bool TryMove(PlayerStorage from, int fromIndex, PlayerStorage to, int toIndex) =>
            profile != null && profile.TryMove(from, fromIndex, to, toIndex);

        public bool CanMove(PlayerStorage from, int fromIndex, PlayerStorage to, int toIndex, out string rejection)
        {
            if (profile == null) { rejection = "Không tìm thấy dữ liệu người chơi."; return false; }
            return profile.CanMove(from, fromIndex, to, toIndex, out rejection);
        }

        public bool TryBuy(ShopOffer offer) => profile != null && profile.TryBuy(offer);
        public bool TrySell(PlayerStorage storage, int index) => profile != null && profile.TrySell(storage, index);

        public bool TryRepairEquippedDrone(out string reason)
        {
            if (profile == null) { reason = "Không tìm thấy hồ sơ người chơi."; return false; }
            return profile.TryRepairEquippedDrone(droneSettings, out reason);
        }

        public bool BeginRaid()
        {
            if (profile == null || !profile.IsReady || profile.IsReadOnly ||
                !Application.CanStreamedLevelBeLoaded("FoundationPlayground")) return false;
            if (!profile.BeginRaid()) return false;
            SceneManager.LoadScene("FoundationPlayground");
            return true;
        }
    }
}

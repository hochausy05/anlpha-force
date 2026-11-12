using Looter.Combat;
using Looter.Items;
using Looter.Raid;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Looter.Inventory
{
    [RequireComponent(typeof(InventoryModel), typeof(InventoryPresenter))]
    public sealed class WorldInteractionController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float interactionRange = 1.65f;
        [SerializeField] private CombatHealth playerHealth;
        [SerializeField] private RaidManager raidManager;
        private InventoryModel inventory;
        private InventoryPresenter presenter;
        private WorldItemPickup nearbyPickup;
        private LootContainer nearbyContainer;
        private ExtractionZone nearbyExtraction;
        private DroneProtection droneProtection;
        private GUIStyle promptStyle;

        private void Awake()
        {
            inventory = GetComponent<InventoryModel>();
            presenter = GetComponent<InventoryPresenter>();
            if (playerHealth == null) playerHealth = GetComponent<CombatHealth>();
            droneProtection = GetComponent<DroneProtection>();
            if (raidManager == null) raidManager = FindAnyObjectByType<RaidManager>();
        }

        public void ConfigureRaidManager(RaidManager manager) => raidManager = manager;

        private void Update()
        {
            if (droneProtection == null) droneProtection = GetComponent<DroneProtection>();
            if ((playerHealth != null && !playerHealth.IsAlive) || presenter == null || presenter.IsOpen ||
                (raidManager != null && !raidManager.CanInteract))
            {
                nearbyPickup = null;
                nearbyContainer = null;
                nearbyExtraction = null;
                return;
            }

            FindNearbyInteractable();
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.fKey.wasPressedThisFrame && raidManager != null && droneProtection != null)
            {
                if (raidManager.TryRepairDroneWithKit(inventory, droneProtection, out string repairMessage))
                {
                    presenter.ShowMessage("Đã dùng Repair Kit; " + repairMessage);
                }
                else presenter.ShowMessage(repairMessage);
            }
            if (!keyboard.eKey.wasPressedThisFrame) return;

            if (nearbyExtraction != null && raidManager != null) raidManager.TryExtract(nearbyExtraction);
            else if (nearbyPickup != null) TryPickUp(nearbyPickup);
            else if (nearbyContainer != null) nearbyContainer.Open(presenter);
        }

        private void FindNearbyInteractable()
        {
            nearbyPickup = null;
            nearbyContainer = null;
            nearbyExtraction = null;
            float nearestDistance = interactionRange * interactionRange;
            int nearestPriority = int.MaxValue;
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, interactionRange);
            foreach (Collider2D overlap in overlaps)
            {
                if (overlap == null) continue;
                ExtractionZone extraction = overlap.GetComponentInParent<ExtractionZone>();
                WorldItemPickup pickup = overlap.GetComponentInParent<WorldItemPickup>();
                LootContainer container = overlap.GetComponentInParent<LootContainer>();
                if (extraction != null)
                    ConsiderTarget(extraction.transform.position, 0, extraction, null, null,
                        ref nearestDistance, ref nearestPriority);
                if (container != null)
                    ConsiderTarget(container.transform.position, 1, null, null, container,
                        ref nearestDistance, ref nearestPriority);
                if (pickup != null)
                    ConsiderTarget(pickup.transform.position, 2, null, pickup, null,
                        ref nearestDistance, ref nearestPriority);
            }
        }

        private void ConsiderTarget(Vector2 targetPosition, int priority, ExtractionZone extraction,
            WorldItemPickup pickup, LootContainer container, ref float nearestDistance, ref int nearestPriority)
        {
            float distance = ((Vector2)transform.position - targetPosition).sqrMagnitude;
            bool closer = distance < nearestDistance - 0.0001f;
            bool tieWithHigherPriority = Mathf.Abs(distance - nearestDistance) <= 0.0001f && priority < nearestPriority;
            if (!closer && !tieWithHigherPriority) return;

            nearestDistance = distance;
            nearestPriority = priority;
            nearbyExtraction = extraction;
            nearbyPickup = pickup;
            nearbyContainer = container;
        }

        private void TryPickUp(WorldItemPickup pickup)
        {
            if (pickup == null || pickup.Item == null)
            {
                presenter.ShowMessage("Vật phẩm không hợp lệ.");
                return;
            }
            if (!inventory.TryAdd(pickup.Item, pickup.Quantity))
            {
                presenter.ShowMessage("Balo đã đầy hoặc không đủ chỗ.");
                return;
            }
            string pickedName = pickup.Item.DisplayName;
            int count = pickup.Quantity;
            Destroy(pickup.gameObject);
            presenter.ShowMessage($"Đã nhặt {pickedName} x{count}.");
        }

        private void OnGUI()
        {
            if (presenter == null || presenter.IsOpen) return;
            string prompt = nearbyExtraction != null ? $"Nhấn E để rút quân — {nearbyExtraction.DisplayName}" :
                nearbyPickup != null ? nearbyPickup.Prompt : nearbyContainer != null ? nearbyContainer.Prompt : null;
            if (!string.IsNullOrEmpty(prompt) && promptStyle == null)
                promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (!string.IsNullOrEmpty(prompt))
            {
                promptStyle.normal.textColor = nearbyExtraction != null ? new Color(0.25f, 0.8f, 1f) :
                    nearbyPickup != null && nearbyPickup.Item != null ? nearbyPickup.Item.TierColor : Color.white;
                GUI.Box(new Rect(Screen.width * 0.5f - 220f, Screen.height - 100f, 440f, 38f), prompt, promptStyle);
            }
            if (raidManager != null && raidManager.CanInteract && droneProtection != null && droneProtection.EquippedDrone != null &&
                inventory.HasRepairKit(raidManager.DroneSettings) &&
                droneProtection.EquippedDrone.CurrentDurability < droneProtection.EquippedDrone.EffectiveMaxDurability)
                GUI.Box(new Rect(Screen.width * 0.5f - 150f, Screen.height - 148f, 300f, 34f), "Nhấn F để dùng Repair Kit cho drone");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }

        private void OnValidate() => interactionRange = Mathf.Max(0.1f, interactionRange);
    }
}

using System;
using Looter.Foundation;
using Looter.Items;
using Looter.Persistence;
using UnityEngine;

namespace Looter.Combat
{
    [DisallowMultipleComponent]
    public sealed class DroneProtection : MonoBehaviour
    {
        [SerializeField] private DroneSystemSettings settings;
        [SerializeField] private EquipmentLoadout loadout;
        [SerializeField] private PlayerController2D playerController;
        private ItemStack equippedDrone;
        private GameObject visualInstance;
        private DroneVisualFollower visualFollower;

        public ItemStack EquippedDrone => equippedDrone;
        public bool IsOperational => equippedDrone != null && equippedDrone.Item != null &&
                                     equippedDrone.Item.Type == ItemType.Drone && !equippedDrone.IsDroneDisabled;
        public int CurrentDurability => equippedDrone != null ? equippedDrone.CurrentDurability : 0;
        public int EffectiveMaxDurability => equippedDrone != null ? equippedDrone.EffectiveMaxDurability : 0;
        public event Action<ItemStack> DurabilityChanged;
        public event Action DroneDisabled;

        private void Awake()
        {
            if (loadout == null) loadout = GetComponent<EquipmentLoadout>();
            if (playerController == null) playerController = GetComponent<PlayerController2D>();
        }

        public void Configure(EquipmentLoadout currentLoadout, PlayerController2D controller,
            ItemStack drone, DroneSystemSettings systemSettings)
        {
            loadout = currentLoadout;
            playerController = controller;
            if (systemSettings != null) settings = systemSettings;
            SetEquippedDrone(drone);
        }

        public void RefreshFromLoadout()
        {
            SetEquippedDrone(loadout != null ? loadout.Drone : null);
        }

        public int ApplyDamage(int requestedDamage)
        {
            if (!IsOperational) return 0;
            int before = equippedDrone.CurrentDurability;
            int absorbed = equippedDrone.ApplyDurabilityDamage(requestedDamage);
            if (absorbed <= 0) return 0;
            UpdateVisualState();
            DurabilityChanged?.Invoke(equippedDrone);
            if (before > 0 && equippedDrone.CurrentDurability == 0) DroneDisabled?.Invoke();
            return absorbed;
        }

        private void SetEquippedDrone(ItemStack drone)
        {
            bool sameInstance = equippedDrone != null && drone != null && equippedDrone.InstanceId == drone.InstanceId;
            equippedDrone = drone != null && drone.Item != null && drone.Item.Type == ItemType.Drone ? drone : null;
            if (!sameInstance) CreateOrReplaceVisual();
            UpdateVisualState();
        }

        private void CreateOrReplaceVisual()
        {
            if (visualInstance != null) Destroy(visualInstance);
            visualInstance = null;
            visualFollower = null;
            if (equippedDrone == null || equippedDrone.Item == null) return;

            GameObject prefab = equippedDrone.Item.DroneFollowerPrefab;
            if (prefab != null) visualInstance = Instantiate(prefab, transform.position, Quaternion.identity);
            else
            {
                visualInstance = new GameObject("Drone Visual");
                visualInstance.transform.position = transform.position;
                SpriteRenderer renderer = visualInstance.AddComponent<SpriteRenderer>();
                renderer.sprite = equippedDrone.Item.DroneWorldSprite != null
                    ? equippedDrone.Item.DroneWorldSprite : equippedDrone.Item.Icon;
                renderer.sortingOrder = 5;
                visualFollower = visualInstance.AddComponent<DroneVisualFollower>();
                ConfigureVisual(renderer);
            }

            foreach (Collider2D collider in visualInstance.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (Rigidbody2D body in visualInstance.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
            if (visualFollower == null) visualFollower = visualInstance.GetComponent<DroneVisualFollower>();
            if (visualFollower == null) visualFollower = visualInstance.AddComponent<DroneVisualFollower>();
            SpriteRenderer foundRenderer = visualInstance.GetComponentInChildren<SpriteRenderer>(true);
            ConfigureVisual(foundRenderer);
        }

        private void ConfigureVisual(SpriteRenderer renderer)
        {
            if (renderer != null && equippedDrone != null && equippedDrone.Item != null &&
                equippedDrone.Item.DroneWorldSprite != null) renderer.sprite = equippedDrone.Item.DroneWorldSprite;
            visualFollower?.Configure(transform, playerController, settings, renderer);
        }

        private void UpdateVisualState()
        {
            if (visualFollower != null) visualFollower.SetDisabled(equippedDrone == null || equippedDrone.IsDroneDisabled);
        }

        private void OnDestroy()
        {
            if (visualInstance != null) Destroy(visualInstance);
        }
    }
}

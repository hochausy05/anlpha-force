using Looter.Items;
using UnityEngine;

namespace Looter.Persistence
{
    public sealed class EquipmentLoadout : MonoBehaviour
    {
        [SerializeField] private ItemStack weapon;
        [SerializeField] private ItemStack backpack;
        [SerializeField] private ItemStack drone;

        public ItemStack Weapon => weapon;
        public ItemStack Backpack => backpack;
        public ItemStack Drone => drone;

        public void SetSlots(ItemStack newWeapon, ItemStack newBackpack, ItemStack newDrone)
        {
            weapon = newWeapon;
            backpack = newBackpack;
            drone = newDrone;
        }

        public void Clear() => SetSlots(null, null, null);
    }
}

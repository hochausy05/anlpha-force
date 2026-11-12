using Looter.Inventory;
using Looter.Items;

namespace Looter.Combat
{
    public static class AmmoService
    {
        public static bool TryConsume(InventoryModel inventory, ItemDefinition ammunition, int quantity)
        {
            return inventory != null && inventory.TryConsume(ammunition, quantity);
        }

        public static int Count(InventoryModel inventory, ItemDefinition ammunition)
        {
            if (inventory == null || ammunition == null) return 0;
            int count = 0;
            for (int i = 0; i < inventory.SlotCount(InventoryStorage.Backpack); i++)
            {
                ItemStack stack = inventory.GetStack(InventoryStorage.Backpack, i);
                if (stack != null && stack.Item == ammunition) count += stack.Quantity;
            }
            return count;
        }
    }
}

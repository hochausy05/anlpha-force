using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    public static class DamageResolver
    {
        public static DamageResolution Resolve(int baseDamage, ItemTier attackerTier, ItemStack drone)
        {
            int damage = Mathf.Max(0, baseDamage);
            if (damage == 0 || drone == null || drone.Item == null || drone.Item.Type != ItemType.Drone || drone.IsDroneDisabled)
                return new DamageResolution(damage, 0);

            if ((int)attackerTier > (int)drone.Item.Tier)
                return new DamageResolution(damage, 0);

            if (attackerTier == drone.Item.Tier)
            {
                int playerDamage = (damage + 1) / 2;
                return new DamageResolution(playerDamage, damage - playerDamage);
            }

            int minimumPlayerDamage = Mathf.Min(1, damage);
            return new DamageResolution(minimumPlayerDamage, damage - minimumPlayerDamage);
        }

        public static int GetWearAmount(ItemStack drone, DroneSystemSettings settings)
        {
            if (!CanRepair(drone, settings)) return 0;
            float ratio = drone.CurrentDurability / (float)drone.EffectiveMaxDurability;
            float wear = ratio >= 0.7f ? settings.WearAboveSeventyPercent :
                ratio >= 0.35f ? settings.WearAboveThirtyFivePercent : settings.WearBelowThirtyFivePercent;
            int amount = Mathf.Max(0, Mathf.RoundToInt(drone.BaseMaxDurability * wear));
            int minimumMax = Mathf.CeilToInt(drone.BaseMaxDurability * settings.MinimumMaxDurabilityFraction);
            return Mathf.Min(amount, Mathf.Max(0, drone.EffectiveMaxDurability - minimumMax));
        }

        public static int GetRepairCost(ItemStack drone, DroneSystemSettings settings)
        {
            if (!CanRepair(drone, settings)) return 0;
            int missing = drone.EffectiveMaxDurability - drone.CurrentDurability;
            float tierFactor = 1f + Mathf.Max(0, (int)drone.Item.Tier - 1) * settings.RepairCostPerTier;
            return Mathf.Max(1, Mathf.CeilToInt(missing * settings.BaseRepairCostPerDurability * tierFactor));
        }

        public static bool CanRepair(ItemStack drone, DroneSystemSettings settings) =>
            drone != null && drone.Item != null && drone.Item.Type == ItemType.Drone &&
            settings != null && drone.CurrentDurability < drone.EffectiveMaxDurability;
    }
}

using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    public readonly struct DroneRepairResult
    {
        public int CurrentDurability { get; }
        public int EffectiveMaxDurability { get; }
        public int WearAmount { get; }
        public int Cost { get; }

        public DroneRepairResult(int current, int maximum, int wear, int cost)
        {
            CurrentDurability = current;
            EffectiveMaxDurability = maximum;
            WearAmount = wear;
            Cost = cost;
        }
    }

    public static class DroneRepairService
    {
        public static bool TryCalculateBaseHubRepair(ItemStack drone, DroneSystemSettings settings,
            out DroneRepairResult result, out string reason)
        {
            result = default;
            reason = null;
            if (drone == null || drone.Item == null || drone.Item.Type != ItemType.Drone)
            { reason = "Không có drone hợp lệ để sửa."; return false; }
            if (settings == null) { reason = "Thiếu cấu hình sửa drone."; return false; }
            if (drone.CurrentDurability >= drone.EffectiveMaxDurability)
            { reason = "Drone đang đầy độ bền."; return false; }

            int wear = DamageResolver.GetWearAmount(drone, settings);
            int newMaximum = Mathf.Max(1, drone.EffectiveMaxDurability - wear);
            int cost = DamageResolver.GetRepairCost(drone, settings);
            result = new DroneRepairResult(newMaximum, newMaximum, wear, cost);
            return true;
        }

        public static bool TryCalculateRepairKit(ItemStack drone, DroneSystemSettings settings,
            out DroneRepairResult result, out string reason)
        {
            result = default;
            reason = null;
            if (!DamageResolver.CanRepair(drone, settings))
            { reason = "Drone không cần sửa hoặc cấu hình chưa hợp lệ."; return false; }
            int wear = DamageResolver.GetWearAmount(drone, settings);
            int newMaximum = Mathf.Max(1, drone.EffectiveMaxDurability - wear);
            int restored = Mathf.Max(1, Mathf.CeilToInt(newMaximum * settings.RepairKitRestoreFraction));
            int current = Mathf.Min(newMaximum, drone.CurrentDurability + restored);
            result = new DroneRepairResult(current, newMaximum, wear, 0);
            return current > drone.CurrentDurability;
        }
    }
}

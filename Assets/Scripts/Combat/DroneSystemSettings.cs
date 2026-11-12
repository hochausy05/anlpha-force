using Looter.Items;
using UnityEngine;

namespace Looter.Combat
{
    [CreateAssetMenu(fileName = "DroneSystemSettings", menuName = "Pixel Looter/Drone System Settings")]
    public sealed class DroneSystemSettings : ScriptableObject
    {
        [Header("Bảo vệ")]
        [SerializeField, Min(0.1f)] private float followSmoothTime = 0.18f;
        [SerializeField, Min(0f)] private float followBobAmplitude = 0.08f;
        [SerializeField, Min(0f)] private float followBobFrequency = 1.8f;
        [SerializeField] private Vector2 followOffset = new Vector2(0.8f, 0.6f);
        [SerializeField, Min(0f)] private float repairKitRestoreFraction = 0.5f;
        [SerializeField, Min(0f)] private float wearAboveSeventyPercent = 0.04f;
        [SerializeField, Min(0f)] private float wearAboveThirtyFivePercent = 0.08f;
        [SerializeField, Min(0f)] private float wearBelowThirtyFivePercent = 0.15f;
        [SerializeField, Range(0.01f, 1f)] private float minimumMaxDurabilityFraction = 0.2f;
        [SerializeField, Min(0f)] private float baseRepairCostPerDurability = 0.8f;
        [SerializeField, Min(0f)] private float repairCostPerTier = 0.35f;
        [SerializeField] private ItemDefinition repairKit;

        public float FollowSmoothTime => Mathf.Max(0.01f, followSmoothTime);
        public float FollowBobAmplitude => Mathf.Max(0f, followBobAmplitude);
        public float FollowBobFrequency => Mathf.Max(0f, followBobFrequency);
        public Vector2 FollowOffset => followOffset;
        public float RepairKitRestoreFraction => Mathf.Clamp01(repairKitRestoreFraction);
        public float WearAboveSeventyPercent => Mathf.Clamp01(wearAboveSeventyPercent);
        public float WearAboveThirtyFivePercent => Mathf.Clamp01(wearAboveThirtyFivePercent);
        public float WearBelowThirtyFivePercent => Mathf.Clamp01(wearBelowThirtyFivePercent);
        public float MinimumMaxDurabilityFraction => Mathf.Clamp(minimumMaxDurabilityFraction, 0.01f, 1f);
        public float BaseRepairCostPerDurability => Mathf.Max(0f, baseRepairCostPerDurability);
        public float RepairCostPerTier => Mathf.Max(0f, repairCostPerTier);
        public ItemDefinition RepairKit => repairKit;

        private void OnValidate()
        {
            followSmoothTime = Mathf.Max(0.01f, followSmoothTime);
            repairKitRestoreFraction = Mathf.Clamp01(repairKitRestoreFraction);
            minimumMaxDurabilityFraction = Mathf.Clamp(minimumMaxDurabilityFraction, 0.01f, 1f);
            baseRepairCostPerDurability = Mathf.Max(0f, baseRepairCostPerDurability);
            repairCostPerTier = Mathf.Max(0f, repairCostPerTier);
        }
    }
}

using UnityEngine;
using Looter.Combat;

namespace Looter.Items
{
    public enum ItemType { Valuable, Weapon, Backpack, Drone, Consumable, Ammunition, Material }
    public enum ItemTier { I = 1, II, III, IV, V, VI }

    [CreateAssetMenu(fileName = "NewItem", menuName = "Pixel Looter/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [TextArea, SerializeField] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private ItemType itemType;
        [SerializeField] private ItemTier tier = ItemTier.I;
        [SerializeField, Min(0)] private int sellValue;
        [SerializeField, Min(0)] private int buyPrice;
        [SerializeField, Min(1)] private int backpackCapacity = 10;
        [SerializeField] private bool isStackable;
        [SerializeField, Min(1)] private int maxStackSize = 1;
        // Giữ trường cũ để Unity không làm mất dữ liệu asset khi nâng cấp; gameplay không còn đọc trường này.
        [SerializeField, HideInInspector, Min(1)] private int maxStack = 1;
        [SerializeField] private bool allowedInSafeSlots = true;
        [Header("Drone (chỉ dùng khi Item Type là Drone)")]
        [SerializeField, Min(1)] private int baseMaxDurability = 90;
        [SerializeField] private Sprite droneWorldSprite;
        [SerializeField] private GameObject droneFollowerPrefab;
        [Header("Weapon (chỉ dùng khi Item Type là Weapon)")]
        [SerializeField] private WeaponDefinition weaponDefinition;

        public string Id => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemType Type => itemType;
        public ItemTier Tier => tier;
        public int SellValue => sellValue;
        public int BuyPrice => Mathf.Max(0, buyPrice);
        public int BackpackCapacity => itemType == ItemType.Backpack ? Mathf.Max(1, backpackCapacity) : 0;
        public bool IsStackable => isStackable;
        public int MaxStackSize => isStackable ? Mathf.Max(1, maxStackSize) : 1;
        public int MaxStack => MaxStackSize;
        private int LegacyMaxStack => maxStack;
        public bool CanStoreInSafeSlot => allowedInSafeSlots &&
            itemType != ItemType.Weapon && itemType != ItemType.Backpack && itemType != ItemType.Drone;
        public int BaseMaxDurability => itemType == ItemType.Drone ? Mathf.Max(1, baseMaxDurability) : 0;
        public Sprite DroneWorldSprite => droneWorldSprite;
        public GameObject DroneFollowerPrefab => droneFollowerPrefab;
        public WeaponDefinition WeaponDefinition => weaponDefinition;
        public Color TierColor => GetTierColor(tier);
        public string TierLabel => tier switch
        {
            ItemTier.I => "I",
            ItemTier.II => "II",
            ItemTier.III => "III",
            ItemTier.IV => "IV",
            ItemTier.V => "V",
            ItemTier.VI => "VI",
            _ => "?"
        };

        public static Color GetTierColor(ItemTier itemTier)
        {
            string hex = itemTier switch
            {
                ItemTier.I => "9CA3AF",
                ItemTier.II => "22C55E",
                ItemTier.III => "3B82F6",
                ItemTier.IV => "A855F7",
                ItemTier.V => "FACC15",
                ItemTier.VI => "EF4444",
                _ => "FFFFFF"
            };
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }

        private void OnValidate()
        {
            maxStackSize = Mathf.Max(1, maxStackSize);
            sellValue = Mathf.Max(0, sellValue);
            baseMaxDurability = Mathf.Max(1, baseMaxDurability);
        }
    }
}

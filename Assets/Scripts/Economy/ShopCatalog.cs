using System;
using System.Collections.Generic;
using Looter.Items;
using UnityEngine;

namespace Looter.Economy
{
    [Serializable]
    public sealed class ShopOffer
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(0)] private int buyPrice;
        [SerializeField, Min(1)] private int purchaseQuantity = 1;
        public ItemDefinition Item => item;
        public int BuyPrice => Mathf.Max(0, buyPrice);
        public int PurchaseQuantity => Mathf.Max(1, purchaseQuantity);

        private void OnValidate() => purchaseQuantity = Mathf.Max(1, purchaseQuantity);
    }

    [CreateAssetMenu(fileName = "ShopCatalog", menuName = "Pixel Looter/Shop Catalog")]
    public sealed class ShopCatalog : ScriptableObject
    {
        [SerializeField] private List<ShopOffer> offers = new List<ShopOffer>();
        public IReadOnlyList<ShopOffer> Offers => offers;
    }
}

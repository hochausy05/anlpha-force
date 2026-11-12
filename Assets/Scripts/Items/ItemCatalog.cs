using System.Collections.Generic;
using UnityEngine;

namespace Looter.Items
{
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Pixel Looter/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> definitions = new List<ItemDefinition>();

        public IReadOnlyList<ItemDefinition> Definitions => definitions;

        public ItemDefinition Find(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || definitions == null) return null;
            foreach (ItemDefinition definition in definitions)
                if (definition != null && definition.Id == itemId) return definition;
            return null;
        }
    }
}

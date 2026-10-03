using System.Collections.Generic;

namespace PoeClone.Inventory
{
    /// <summary>
    /// The gear a new character finds lying around them at the start (StarterLoot puts it on the
    /// ground): plain, Normal-rarity items with just their base stats, including a sword and a bow.
    /// </summary>
    public static class ItemCatalog
    {
        private static readonly string[] StarterBases = { "rusty_sword", "short_bow", "iron_helmet", "studded_vest", "leather_boots" };

        public static List<ItemData> CreateStarterItems()
        {
            // Normal items have no random part, so any seed gives the same items.
            var rng = new System.Random(0);
            var items = new List<ItemData>();
            foreach (string id in StarterBases)
                items.Add(ItemGenerator.Generate(rng, id, 1, ItemRarity.Normal));
            return items;
        }
    }
}

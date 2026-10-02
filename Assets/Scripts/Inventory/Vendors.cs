using System;
using System.Collections.Generic;

namespace PoeClone.Inventory
{
    /// <summary>A trader's goods: a grid of items for sale, restocked now and then.</summary>
    public sealed class VendorStock
    {
        public const int Width = 12;
        public const int Height = 12;

        public string Name { get; }
        public InventoryGrid Grid { get; } = new InventoryGrid(Width, Height);

        internal int StockedForLevel = -1;
        internal double StockedAt = double.NegativeInfinity;

        public VendorStock(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// The traders' stock and prices. Each trader keeps a grid of goods suited to the player's
    /// level, restocked when the player has grown two levels or a while has passed; what the
    /// player sells lands in the trader's grid (so it can be bought back). Pure C#, testable.
    /// </summary>
    public static class Vendors
    {
        public const double RestockSeconds = 300.0;

        private static readonly Dictionary<string, VendorStock> stocks = new Dictionary<string, VendorStock>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            stocks.Clear();
        }

        /// <summary>
        /// The trader's goods, restocked first if they're stale. <paramref name="sells"/> picks
        /// which item types they carry; <paramref name="now"/> is the current time in seconds.
        /// </summary>
        public static VendorStock Get(string trader, int playerLevel, Func<ItemType, bool> sells, double now, Random rng)
        {
            if (!stocks.TryGetValue(trader, out VendorStock stock))
            {
                stock = new VendorStock(trader);
                stocks[trader] = stock;
            }

            if (playerLevel >= stock.StockedForLevel + 2 || now - stock.StockedAt >= RestockSeconds)
            {
                Restock(stock, playerLevel, sells, rng);
                stock.StockedAt = now;
            }
            return stock;
        }

        private static void Restock(VendorStock stock, int playerLevel, Func<ItemType, bool> sells, Random rng)
        {
            foreach (PlacedItem placed in new List<PlacedItem>(stock.Grid.Items))
                stock.Grid.Remove(placed.Item);

            var bases = new List<string>();
            foreach (string id in ItemGenerator.BaseIds)
            {
                ItemData sample = ItemGenerator.Display(id, null, ItemRarity.Normal);
                if (sample != null && sells(sample.Type) && ItemGenerator.MinLevelOf(id) <= playerLevel + 1)
                    bases.Add(id);
            }

            int wanted = 14 + rng.Next(6);
            for (int k = 0; k < wanted && bases.Count > 0; k++)
            {
                // Mostly plain and magic goods; now and then a rare.
                double roll = rng.NextDouble();
                ItemRarity rarity = roll < 0.12 ? ItemRarity.Rare : roll < 0.6 ? ItemRarity.Magic : ItemRarity.Normal;
                ItemData item = ItemGenerator.Generate(rng, bases[rng.Next(bases.Count)], Math.Max(1, playerLevel), rarity);
                stock.Grid.TryAutoPlace(item);
            }

            stock.StockedForLevel = playerLevel;
        }

        // ------------------------------------------------------------------ prices

        /// <summary>What a trader pays for an item: by rarity, plus a little per stat line.</summary>
        public static int SellPrice(ItemData item)
        {
            if (item == null)
                return 0;
            int baseValue;
            switch (item.Rarity)
            {
                case ItemRarity.Unique: baseValue = 50; break;
                case ItemRarity.Rare: baseValue = 20; break;
                case ItemRarity.Magic: baseValue = 8; break;
                default: baseValue = 3; break;
            }
            return baseValue + item.Modifiers.Count;
        }

        /// <summary>What a trader asks for an item: several times what they'd pay for it.</summary>
        public static int BuyPrice(ItemData item)
        {
            return SellPrice(item) * 5;
        }
    }
}

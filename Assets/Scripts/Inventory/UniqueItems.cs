using System.Collections.Generic;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Hand-made items with fixed names and stats (orange), stronger than anything rolled.
    /// Each boss always drops one; any other drop has a small chance to be one.
    /// </summary>
    public static class UniqueItems
    {
        private sealed class Unique
        {
            public string BaseId;
            public string Name;
            public string Flavour;
            public StatModifier[] Mods;
        }

        private static StatModifier Mod(StatType stat, float value) => new StatModifier(stat, value);

        private static readonly Unique[] All =
        {
            new Unique
            {
                BaseId = "hand_axe", Name = "Bonehew",
                Flavour = "Mortis swung it for a thousand years. It remembers every one.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 16), Mod(StatType.AttackSpeed, 10), Mod(StatType.MaxLife, 20) }
            },
            new Unique
            {
                BaseId = "short_bow", Name = "Whisperwind",
                Flavour = "The arrow arrives before the sound.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 11), Mod(StatType.AttackSpeed, 20), Mod(StatType.Dexterity, 15) }
            },
            new Unique
            {
                BaseId = "iron_mace", Name = "Ashfall",
                Flavour = "What the fire left, the mace finishes.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 18), Mod(StatType.Strength, 15), Mod(StatType.FireResistance, 20) }
            },
            new Unique
            {
                BaseId = "iron_helmet", Name = "Crown of Ash",
                Flavour = "Taken from the Warlord's brow, still warm.",
                Mods = new[] { Mod(StatType.Armour, 70), Mod(StatType.MaxLife, 30), Mod(StatType.FireResistance, 25) }
            },
            new Unique
            {
                BaseId = "studded_vest", Name = "Sexton's Hide",
                Flavour = "Dug a hundred graves. Filled them too.",
                Mods = new[] { Mod(StatType.Armour, 80), Mod(StatType.MaxLife, 45), Mod(StatType.ColdResistance, 15) }
            },
            new Unique
            {
                BaseId = "leather_boots", Name = "Wanderer's Stride",
                Flavour = "Every road leads somewhere. These find it faster.",
                Mods = new[] { Mod(StatType.MovementSpeed, 15), Mod(StatType.Evasion, 45), Mod(StatType.Dexterity, 10) }
            },
            new Unique
            {
                BaseId = "wooden_shield", Name = "The Keeper's Ward",
                Flavour = "The graveyard's last keeper held the gate with this. For a while.",
                Mods = new[] { Mod(StatType.Armour, 50), Mod(StatType.BlockChance, 15), Mod(StatType.ColdResistance, 20) }
            },
            new Unique
            {
                BaseId = "ruby_ring", Name = "Emberheart",
                Flavour = "It beats, faintly.",
                Mods = new[] { Mod(StatType.FireResistance, 30), Mod(StatType.Strength, 12), Mod(StatType.PhysicalDamage, 4) }
            },
            new Unique
            {
                BaseId = "jade_amulet", Name = "Elder's Charm",
                Flavour = "Haven's elders have worn it since before the fire.",
                Mods = new[] { Mod(StatType.Intelligence, 18), Mod(StatType.MaxMana, 40), Mod(StatType.LightningResistance, 20) }
            },
        };

        private static readonly Dictionary<string, string> flavourByName = new Dictionary<string, string>();

        static UniqueItems()
        {
            foreach (Unique u in All)
                flavourByName[u.Name] = u.Flavour;
        }

        public static int Count => All.Length;

        /// <summary>A random unique item.</summary>
        public static ItemData Random(System.Random rng)
        {
            return Create(rng.Next(All.Length));
        }

        public static ItemData Create(int index)
        {
            Unique u = All[index % All.Length];
            ItemData shape = ItemGenerator.Display(u.BaseId, u.Name, ItemRarity.Unique);
            var item = new ItemData(shape.Id, u.Name, shape.Type, shape.Width, shape.Height, shape.Tint, u.Mods,
                hasCape: false, weaponType: shape.WeaponType, rarity: ItemRarity.Unique);
            item.ArtId = shape.ArtId;
            item.ArtTint = shape.ArtTint;
            return item;
        }

        /// <summary>The unique's line of lore for its tooltip (null for anything else).</summary>
        public static string FlavourFor(ItemData item)
        {
            if (item == null || item.Rarity != ItemRarity.Unique)
                return null;
            return flavourByName.TryGetValue(item.Name, out string text) ? text : null;
        }
    }
}

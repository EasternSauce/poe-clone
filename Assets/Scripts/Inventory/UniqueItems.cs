using System.Collections.Generic;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Hand-made items with fixed names and stats (orange), stronger than anything rolled, each
    /// with at least one special stat that changes how the character plays (extra arrows, leech,
    /// culling...; see the end of StatType). Each boss always drops one; any other drop has a
    /// small chance to be one. New uniques go at the end (saves keep them by name, not index).
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
                Mods = new[] { Mod(StatType.PhysicalDamage, 16), Mod(StatType.AttackSpeed, 10), Mod(StatType.MaxLife, 20), Mod(StatType.LifeLeech, 3) }
            },
            new Unique
            {
                BaseId = "short_bow", Name = "Whisperwind",
                Flavour = "The arrow arrives before the sound.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 9), Mod(StatType.AttackSpeed, 15), Mod(StatType.Dexterity, 15), Mod(StatType.AdditionalArrows, 2) }
            },
            new Unique
            {
                BaseId = "iron_mace", Name = "Ashfall",
                Flavour = "What the fire left, the mace finishes.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 18), Mod(StatType.Strength, 15), Mod(StatType.FireResistance, 20), Mod(StatType.CullingStrike, 1) }
            },
            new Unique
            {
                BaseId = "iron_helmet", Name = "Crown of Ash",
                Flavour = "Taken from the Warlord's brow, still warm.",
                Mods = new[] { Mod(StatType.Armour, 70), Mod(StatType.MaxLife, 30), Mod(StatType.FireResistance, 25), Mod(StatType.AreaOfEffect, 25) }
            },
            new Unique
            {
                BaseId = "studded_vest", Name = "Sexton's Hide",
                Flavour = "Dug a hundred graves. Filled them too.",
                Mods = new[] { Mod(StatType.Armour, 80), Mod(StatType.MaxLife, 45), Mod(StatType.ColdResistance, 15), Mod(StatType.LifeOnKill, 6) }
            },
            new Unique
            {
                BaseId = "leather_boots", Name = "Wanderer's Stride",
                Flavour = "Every road leads somewhere. These find it faster.",
                Mods = new[] { Mod(StatType.MovementSpeed, 20), Mod(StatType.Evasion, 45), Mod(StatType.Dexterity, 10), Mod(StatType.LifeRegen, 3) }
            },
            new Unique
            {
                BaseId = "wooden_shield", Name = "The Keeper's Ward",
                Flavour = "The graveyard's last keeper held the gate with this. For a while.",
                Mods = new[] { Mod(StatType.Armour, 50), Mod(StatType.BlockChance, 15), Mod(StatType.ColdResistance, 20), Mod(StatType.ManaAbsorb, 15) }
            },
            new Unique
            {
                BaseId = "ruby_ring", Name = "Emberheart",
                Flavour = "It beats, faintly.",
                Mods = new[] { Mod(StatType.FireResistance, 30), Mod(StatType.Intelligence, 12), Mod(StatType.SpellDamage, 25) }
            },
            new Unique
            {
                BaseId = "jade_amulet", Name = "Elder's Charm",
                Flavour = "Haven's elders have worn it since before the fire.",
                Mods = new[] { Mod(StatType.Intelligence, 18), Mod(StatType.MaxMana, 40), Mod(StatType.LightningResistance, 20), Mod(StatType.AdditionalChains, 1) }
            },
            new Unique
            {
                BaseId = "leather_quiver", Name = "Rimefletch",
                Flavour = "Feathered with frost that never melts.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 5), Mod(StatType.ColdResistance, 20), Mod(StatType.ChillOnHit, 30) }
            },
            new Unique
            {
                BaseId = "steel_dagger", Name = "Leechfang",
                Flavour = "It drinks first. You drink after.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 8), Mod(StatType.AttackSpeed, 15), Mod(StatType.LifeLeech, 4), Mod(StatType.LifeOnKill, 4) }
            },
            new Unique
            {
                BaseId = "sapphire_ring", Name = "Twinflame Loop",
                Flavour = "Every spark it touches is born a twin.",
                Mods = new[] { Mod(StatType.Intelligence, 10), Mod(StatType.MaxMana, 20), Mod(StatType.AdditionalSpellProjectiles, 2) }
            },
            new Unique
            {
                BaseId = "sage_circlet", Name = "Stormcaller's Circlet",
                Flavour = "The sky listens to whoever wears it, and answers twice.",
                Mods = new[] { Mod(StatType.Intelligence, 15), Mod(StatType.MaxMana, 25), Mod(StatType.AdditionalChains, 2), Mod(StatType.LightningResistance, 15) }
            },
            new Unique
            {
                BaseId = "rope_belt", Name = "Bloodroot Cord",
                Flavour = "Braided from roots that drank the battlefield dry.",
                Mods = new[] { Mod(StatType.MaxLife, 35), Mod(StatType.LifeRegen, 6), Mod(StatType.Strength, 10) }
            },
            new Unique
            {
                BaseId = "silk_robe", Name = "Mantle of the Still Mind",
                Flavour = "Pain is a rumour. Mana is the truth.",
                Mods = new[] { Mod(StatType.Evasion, 40), Mod(StatType.MaxMana, 60), Mod(StatType.ManaAbsorb, 20), Mod(StatType.ManaRegen, 40) }
            },
            new Unique
            {
                BaseId = "grimoire", Name = "The Ossuary Codex",
                Flavour = "Every page a name. Every name still answers.",
                Mods = new[] { Mod(StatType.GrantDeathMark, 8), Mod(StatType.MinionLevels, 1), Mod(StatType.AdditionalSkeletons, 1), Mod(StatType.MinionLife, 25), Mod(StatType.Intelligence, 12) }
            },
            new Unique
            {
                BaseId = "bone_sceptre", Name = "Gravewarden's Rod",
                Flavour = "The dead keep the watch now. They never sleep on it.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 9), Mod(StatType.GrantRaiseSkeletons, 6), Mod(StatType.RaiseSkeletonsLevels, 2), Mod(StatType.MinionDamage, 30), Mod(StatType.SoulBond, 2) }
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

        /// <summary>A random unique of a kind the filter accepts (any unique if none fits).</summary>
        public static ItemData Random(System.Random rng, System.Func<ItemType, bool> kind)
        {
            var fitting = new List<ItemData>();
            for (int k = 0; k < All.Length; k++)
            {
                ItemData item = Create(k);
                if (kind(item.Type))
                    fitting.Add(item);
            }
            return fitting.Count > 0 ? fitting[rng.Next(fitting.Count)] : Random(rng);
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

        /// <summary>The unique with this name as it is designed today, or null if there's none.</summary>
        public static ItemData Current(string name)
        {
            for (int k = 0; k < All.Length; k++)
            {
                if (All[k].Name == name)
                    return Create(k);
            }
            return null;
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

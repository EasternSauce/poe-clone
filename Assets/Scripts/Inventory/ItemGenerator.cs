using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Random loot. Every drop is one of the item bases that has art (an icon and a 3D look, keyed by
    /// id like the starter items), with the base's own fixed stats plus random extra stats by rarity:
    /// Normal none, Magic one or two, Rare three or four, rolled stronger as the item level rises.
    /// Takes a System.Random so tests can make it repeatable.
    /// </summary>
    public static class ItemGenerator
    {
        public const int MaxItemLevel = 40;

        private sealed class ItemBase
        {
            public string Id;
            public string Name;
            public ItemType Type;
            public int Width;
            public int Height;
            public Color Tint;
            public WeaponType WeaponType = WeaponType.Sword;
            public StatModifier[] Implicits;
        }

        private sealed class Affix
        {
            public StatType Stat;
            public float Min;
            public float Max;
            public bool ScalesWithLevel;
            public ItemType[] On;
        }

        private static readonly ItemBase[] Bases =
        {
            Base("iron_helmet", "Iron Helmet", ItemType.Helmet, 2, 2, new Color(0.62f, 0.66f, 0.72f), Mod(StatType.Armour, 20)),
            Base("bronze_helmet", "Bronze Helmet", ItemType.Helmet, 2, 2, new Color(0.80f, 0.55f, 0.25f), Mod(StatType.Armour, 12), Mod(StatType.Evasion, 12)),
            Base("studded_vest", "Studded Vest", ItemType.BodyArmour, 2, 3, new Color(0.55f, 0.40f, 0.26f), Mod(StatType.Armour, 30), Mod(StatType.Evasion, 15)),
            Base("leather_gloves", "Leather Gloves", ItemType.Gloves, 2, 2, new Color(0.50f, 0.36f, 0.24f), Mod(StatType.Evasion, 14)),
            Base("leather_boots", "Leather Boots", ItemType.Boots, 2, 2, new Color(0.45f, 0.32f, 0.22f), Mod(StatType.Evasion, 15)),
            Base("rope_belt", "Rope Belt", ItemType.Belt, 2, 1, new Color(0.72f, 0.62f, 0.40f), Mod(StatType.MaxLife, 10)),
            Base("jade_amulet", "Jade Amulet", ItemType.Amulet, 1, 1, new Color(0.30f, 0.80f, 0.55f), Mod(StatType.Dexterity, 8)),
            Base("iron_ring", "Iron Ring", ItemType.Ring, 1, 1, new Color(0.70f, 0.72f, 0.76f), Mod(StatType.PhysicalDamage, 1)),
            Base("ruby_ring", "Ruby Ring", ItemType.Ring, 1, 1, new Color(0.90f, 0.25f, 0.30f), Mod(StatType.FireResistance, 10)),
            Base("sapphire_ring", "Sapphire Ring", ItemType.Ring, 1, 1, new Color(0.30f, 0.50f, 0.95f), Mod(StatType.ColdResistance, 10)),
            Weapon("rusty_sword", "Rusty Sword", 1, 3, new Color(0.65f, 0.62f, 0.58f), WeaponType.Sword, Mod(StatType.PhysicalDamage, 5)),
            Weapon("hand_axe", "Hand Axe", 2, 3, new Color(0.58f, 0.60f, 0.62f), WeaponType.Axe, Mod(StatType.PhysicalDamage, 7)),
            Weapon("iron_mace", "Iron Mace", 1, 3, new Color(0.50f, 0.52f, 0.56f), WeaponType.Mace, Mod(StatType.PhysicalDamage, 8)),
            Weapon("steel_dagger", "Steel Dagger", 1, 2, new Color(0.78f, 0.80f, 0.84f), WeaponType.Dagger, Mod(StatType.PhysicalDamage, 3)),
            Weapon("short_bow", "Short Bow", 2, 3, new Color(0.62f, 0.44f, 0.26f), WeaponType.Bow, Mod(StatType.PhysicalDamage, 5)),
            Base("wooden_shield", "Wooden Shield", ItemType.Shield, 2, 2, new Color(0.60f, 0.42f, 0.25f), Mod(StatType.Armour, 10), Mod(StatType.BlockChance, 10)),
            Base("leather_quiver", "Leather Quiver", ItemType.Quiver, 2, 3, new Color(0.55f, 0.36f, 0.20f), Mod(StatType.PhysicalDamage, 2)),
        };

        private static readonly ItemType[] Armour = { ItemType.Helmet, ItemType.BodyArmour, ItemType.Gloves, ItemType.Boots, ItemType.Shield };
        private static readonly ItemType[] Jewellery = { ItemType.Amulet, ItemType.Ring, ItemType.Belt };
        private static readonly ItemType[] NotWeapon =
            { ItemType.Helmet, ItemType.BodyArmour, ItemType.Gloves, ItemType.Boots, ItemType.Shield, ItemType.Amulet, ItemType.Ring, ItemType.Belt, ItemType.Quiver };

        private static readonly Affix[] Affixes =
        {
            Aff(StatType.MaxLife, 8, 25, true, NotWeapon),
            Aff(StatType.MaxMana, 8, 20, true, ItemType.Helmet, ItemType.Gloves, ItemType.Amulet, ItemType.Ring, ItemType.Belt),
            Aff(StatType.Strength, 4, 12, true, ItemType.Helmet, ItemType.BodyArmour, ItemType.Gloves, ItemType.Belt, ItemType.Amulet, ItemType.Ring, ItemType.Weapon),
            Aff(StatType.Dexterity, 4, 12, true, ItemType.Gloves, ItemType.Boots, ItemType.Amulet, ItemType.Ring, ItemType.Weapon, ItemType.Quiver),
            Aff(StatType.Intelligence, 4, 12, true, ItemType.Helmet, ItemType.Gloves, ItemType.Amulet, ItemType.Ring),
            Aff(StatType.Armour, 10, 40, true, Armour),
            Aff(StatType.Evasion, 10, 40, true, Armour),
            Aff(StatType.BlockChance, 3, 8, false, ItemType.Shield),
            Aff(StatType.PhysicalDamage, 1, 3, true, ItemType.Gloves, ItemType.Ring, ItemType.Amulet, ItemType.Quiver),
            Aff(StatType.PhysicalDamage, 3, 8, true, ItemType.Weapon),
            Aff(StatType.AttackSpeed, 3, 10, false, ItemType.Gloves, ItemType.Ring, ItemType.Amulet, ItemType.Weapon, ItemType.Quiver),
            Aff(StatType.FireResistance, 6, 24, false, NotWeapon),
            Aff(StatType.ColdResistance, 6, 24, false, NotWeapon),
            Aff(StatType.LightningResistance, 6, 24, false, NotWeapon),
            Aff(StatType.MovementSpeed, 5, 15, false, ItemType.Boots),
        };

        // Magic items are named after their first stats, PoE style: "Hale Iron Helmet of the Fox".
        private static readonly Dictionary<StatType, string> Prefixes = new Dictionary<StatType, string>
        {
            { StatType.MaxLife, "Hale" },
            { StatType.MaxMana, "Azure" },
            { StatType.Armour, "Reinforced" },
            { StatType.Evasion, "Shadowy" },
            { StatType.PhysicalDamage, "Heavy" },
            { StatType.MovementSpeed, "Runner's" },
        };

        private static readonly Dictionary<StatType, string> Suffixes = new Dictionary<StatType, string>
        {
            { StatType.Strength, "of the Brute" },
            { StatType.Dexterity, "of the Fox" },
            { StatType.Intelligence, "of the Owl" },
            { StatType.AttackSpeed, "of Skill" },
            { StatType.BlockChance, "of the Wall" },
            { StatType.FireResistance, "of the Whelpling" },
            { StatType.ColdResistance, "of the Seal" },
            { StatType.LightningResistance, "of the Cloud" },
        };

        private static readonly string[] RareFirstWords =
            { "Doom", "Grim", "Storm", "Blood", "Gale", "Rune", "Dusk", "Ember", "Frost", "Wrath", "Vortex", "Bramble", "Hollow", "Ash" };

        private static readonly Dictionary<ItemType, string[]> RareSecondWords = new Dictionary<ItemType, string[]>
        {
            { ItemType.Helmet, new[] { "Crown", "Visor", "Brow", "Cowl" } },
            { ItemType.BodyArmour, new[] { "Shell", "Coat", "Hide", "Carapace" } },
            { ItemType.Gloves, new[] { "Grip", "Fist", "Hand", "Claw" } },
            { ItemType.Boots, new[] { "Stride", "Road", "Trail", "Spur" } },
            { ItemType.Belt, new[] { "Coil", "Lash", "Clasp", "Bind" } },
            { ItemType.Amulet, new[] { "Charm", "Locket", "Beads", "Heart" } },
            { ItemType.Ring, new[] { "Loop", "Band", "Knuckle", "Eye" } },
            { ItemType.Weapon, new[] { "Bane", "Edge", "Fang", "Song" } },
            { ItemType.Shield, new[] { "Ward", "Wall", "Bastion", "Guard" } },
            { ItemType.Quiver, new[] { "Quill", "Rest", "Sheath", "Hold" } },
        };

        /// <summary>Ids of every base a drop can be, for tests and replication.</summary>
        public static IEnumerable<string> BaseIds
        {
            get
            {
                foreach (ItemBase b in Bases)
                    yield return b.Id;
            }
        }

        /// <summary>Rolls Normal (50%), Magic (35%) or Rare (15%), with <paramref name="rareBonus"/> moved from Normal to Rare.</summary>
        public static ItemRarity RollRarity(System.Random rng, float rareBonus = 0f)
        {
            double roll = rng.NextDouble();
            double rare = 0.15 + rareBonus;
            if (roll < rare)
                return ItemRarity.Rare;
            if (roll < rare + 0.35)
                return ItemRarity.Magic;
            return ItemRarity.Normal;
        }

        /// <summary>A random item of a random base.</summary>
        public static ItemData Generate(System.Random rng, int itemLevel, ItemRarity rarity)
        {
            ItemBase b = Bases[rng.Next(Bases.Length)];
            return Generate(rng, b, itemLevel, rarity);
        }

        /// <summary>A random item of the given base id (null if there is no such base).</summary>
        public static ItemData Generate(System.Random rng, string baseId, int itemLevel, ItemRarity rarity)
        {
            ItemBase b = Find(baseId);
            return b != null ? Generate(rng, b, itemLevel, rarity) : null;
        }

        /// <summary>
        /// An item that only needs to look right (spectators drawing the player's loot): the base's
        /// art and size with the given name and rarity, no stats. Null if the base is unknown.
        /// </summary>
        public static ItemData Display(string baseId, string name, ItemRarity rarity)
        {
            ItemBase b = Find(baseId);
            if (b == null)
                return null;
            return new ItemData(b.Id, string.IsNullOrEmpty(name) ? b.Name : name, b.Type, b.Width, b.Height, b.Tint,
                null, hasCape: false, weaponType: b.WeaponType, rarity: rarity);
        }

        private static ItemData Generate(System.Random rng, ItemBase b, int itemLevel, ItemRarity rarity)
        {
            int level = Math.Max(1, Math.Min(MaxItemLevel, itemLevel));
            float levelScale = 1f + 0.06f * (level - 1);

            int affixCount = 0;
            if (rarity == ItemRarity.Magic)
                affixCount = rng.Next(1, 3);
            else if (rarity == ItemRarity.Rare)
                affixCount = rng.Next(3, 5);

            // Each stat at most once per item, and only stats that make sense on this kind of gear.
            var candidates = new List<Affix>();
            foreach (Affix a in Affixes)
            {
                if (Array.IndexOf(a.On, b.Type) >= 0)
                    candidates.Add(a);
            }

            var mods = new List<StatModifier>(b.Implicits);
            var rolled = new List<StatType>();

            while (rolled.Count < affixCount && candidates.Count > 0)
            {
                int pick = rng.Next(candidates.Count);
                Affix a = candidates[pick];
                candidates.RemoveAt(pick);
                if (rolled.Contains(a.Stat))
                    continue;

                float value = a.Min + (float)rng.NextDouble() * (a.Max - a.Min);
                if (a.ScalesWithLevel)
                    value *= levelScale;

                mods.Add(new StatModifier(a.Stat, Mathf.Max(1f, Mathf.Round(value))));
                rolled.Add(a.Stat);
            }

            // The rarity shown matches what actually rolled (a base can run out of stats to give).
            if (rarity != ItemRarity.Normal && rolled.Count == 0)
                rarity = ItemRarity.Normal;
            else if (rarity == ItemRarity.Rare && rolled.Count < 3)
                rarity = ItemRarity.Magic;

            string name = NameFor(rng, b, rarity, rolled);
            return new ItemData(b.Id, name, b.Type, b.Width, b.Height, b.Tint, mods,
                hasCape: false, weaponType: b.WeaponType, rarity: rarity);
        }

        private static string NameFor(System.Random rng, ItemBase b, ItemRarity rarity, List<StatType> rolled)
        {
            if (rarity == ItemRarity.Rare)
            {
                string first = RareFirstWords[rng.Next(RareFirstWords.Length)];
                string[] seconds = RareSecondWords[b.Type];
                return first + " " + seconds[rng.Next(seconds.Length)];
            }

            if (rarity == ItemRarity.Magic)
            {
                string prefix = null;
                string suffix = null;
                foreach (StatType stat in rolled)
                {
                    if (prefix == null && Prefixes.TryGetValue(stat, out string p))
                        prefix = p;
                    else if (suffix == null && Suffixes.TryGetValue(stat, out string s))
                        suffix = s;
                }

                string name = b.Name;
                if (prefix != null)
                    name = prefix + " " + name;
                if (suffix != null)
                    name = name + " " + suffix;
                return name;
            }

            return b.Name;
        }

        private static ItemBase Find(string id)
        {
            foreach (ItemBase b in Bases)
            {
                if (b.Id == id)
                    return b;
            }
            return null;
        }

        private static ItemBase Base(string id, string name, ItemType type, int w, int h, Color tint, params StatModifier[] implicits)
        {
            return new ItemBase { Id = id, Name = name, Type = type, Width = w, Height = h, Tint = tint, Implicits = implicits };
        }

        private static ItemBase Weapon(string id, string name, int w, int h, Color tint, WeaponType weaponType, params StatModifier[] implicits)
        {
            ItemBase b = Base(id, name, ItemType.Weapon, w, h, tint, implicits);
            b.WeaponType = weaponType;
            return b;
        }

        private static Affix Aff(StatType stat, float min, float max, bool scales, params ItemType[] on)
        {
            return new Affix { Stat = stat, Min = min, Max = max, ScalesWithLevel = scales, On = on };
        }

        private static StatModifier Mod(StatType stat, float value)
        {
            return new StatModifier(stat, value);
        }
    }
}

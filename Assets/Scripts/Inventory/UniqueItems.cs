using System.Collections.Generic;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Hand-made items with fixed names and stats (orange), each with at least one uncommon
    /// stat or effect (extra arrows, leech, culling...; see the end of StatType).
    /// Each boss always drops one; any other drop has a
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
            public bool ShepherdOnly;
        }

        private static StatModifier Mod(StatType stat, float value) => new StatModifier(stat, value);

        private static readonly Unique[] All =
        {
            new Unique
            {
                BaseId = "hand_axe", Name = "Bonehew",
                Flavour = "Mortis swung it for a thousand years. It remembers every one.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 16), Mod(StatType.AttackSpeed, 10), Mod(StatType.MaxLife, 20), Mod(StatType.LifeLeech, 3), Mod(StatType.ArmourPenetration, 10) }
            },
            new Unique
            {
                BaseId = "short_bow", Name = "Whisperwind",
                Flavour = "The arrow arrives before the sound.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 9), Mod(StatType.AttackSpeed, 15), Mod(StatType.Dexterity, 15), Mod(StatType.AdditionalArrows, 2), Mod(StatType.ArmourPenetration, 8) }
            },
            new Unique
            {
                BaseId = "iron_mace", Name = "Ashfall",
                Flavour = "What the fire left, the mace finishes.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 18), Mod(StatType.Strength, 15), Mod(StatType.FireResistance, 20), Mod(StatType.CullingStrike, 1), Mod(StatType.FirePenetration, 12) }
            },
            new Unique
            {
                BaseId = "iron_helmet", Name = "Crown of Ash",
                Flavour = "Taken from the Warlord's brow, still warm.",
                Mods = new[] { Mod(StatType.Armour, 55), Mod(StatType.MaxLife, 20), Mod(StatType.FireResistance, 20), Mod(StatType.AreaOfEffect, 15), Mod(StatType.IgniteChance, 15) }
            },
            new Unique
            {
                BaseId = "studded_vest", Name = "Sexton's Hide",
                Flavour = "Dug a hundred graves. Filled them too.",
                Mods = new[] { Mod(StatType.Armour, 65), Mod(StatType.MaxLife, 30), Mod(StatType.ColdResistance, 15), Mod(StatType.LifeOnKill, 5), Mod(StatType.AvoidStun, 12) }
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
                Mods = new[] { Mod(StatType.Armour, 50), Mod(StatType.BlockChance, 15), Mod(StatType.ColdResistance, 20), Mod(StatType.AvoidStun, 25) }
            },
            new Unique
            {
                BaseId = "ruby_ring", Name = "Emberheart",
                Flavour = "It beats, faintly.",
                Mods = new[] { Mod(StatType.FireResistance, 25), Mod(StatType.CastSpeed, 10), Mod(StatType.SpellDamage, 18), Mod(StatType.FirePenetration, 8), Mod(StatType.IgniteChance, 15) }
            },
            new Unique
            {
                BaseId = "jade_amulet", Name = "Elder's Charm",
                Flavour = "Haven's elders have worn it since before the fire.",
                Mods = new[] { Mod(StatType.CastSpeed, 12), Mod(StatType.MaxMana, 40), Mod(StatType.LightningResistance, 20), Mod(StatType.AdditionalChains, 1) }
            },
            new Unique
            {
                BaseId = "leather_quiver", Name = "Rimefletch",
                Flavour = "Feathered with frost that never melts.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 4), Mod(StatType.ColdResistance, 15), Mod(StatType.ChillOnHit, 22), Mod(StatType.ColdPenetration, 8), Mod(StatType.DamageVsChilled, 12) }
            },
            new Unique
            {
                BaseId = "steel_dagger", Name = "Leechfang",
                Flavour = "It drinks first. You drink after.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 6), Mod(StatType.AttackSpeed, 12), Mod(StatType.LifeLeech, 3), Mod(StatType.LifeOnKill, 3), Mod(StatType.ArmourPenetration, 10), Mod(StatType.OnslaughtOnKill, 15) }
            },
            new Unique
            {
                BaseId = "sapphire_ring", Name = "Twinflame Loop",
                Flavour = "Every spark it touches is born a twin.",
                Mods = new[] { Mod(StatType.Intelligence, 10), Mod(StatType.MaxMana, 20), Mod(StatType.AdditionalSpellProjectiles, 2), Mod(StatType.ElementalPenetration, 10) }
            },
            new Unique
            {
                BaseId = "sage_circlet", Name = "Stormcaller's Circlet",
                Flavour = "The sky listens to whoever wears it, and answers twice.",
                Mods = new[] { Mod(StatType.CastSpeed, 12), Mod(StatType.MaxMana, 25), Mod(StatType.AdditionalChains, 2), Mod(StatType.LightningResistance, 15), Mod(StatType.LightningPenetration, 12) }
            },
            new Unique
            {
                BaseId = "rope_belt", Name = "Bloodroot Cord",
                Flavour = "Braided from roots that drank the battlefield dry.",
                Mods = new[] { Mod(StatType.MaxLife, 22), Mod(StatType.LifeRegen, 3), Mod(StatType.Strength, 8), Mod(StatType.HealthPotionRecovery, 25) }
            },
            new Unique
            {
                BaseId = "silk_robe", Name = "Mantle of the Still Mind",
                Flavour = "Pain is a rumour. Mana is the truth.",
                Mods = new[] { Mod(StatType.Evasion, 40), Mod(StatType.MaxMana, 60), Mod(StatType.CastSpeed, 30), Mod(StatType.ManaRegen, 40) }
            },
            new Unique
            {
                BaseId = "grimoire", Name = "The Ossuary Codex",
                Flavour = "Every page a name. Every name still answers.",
                Mods = new[] { Mod(StatType.GrantDeathMark, 8), Mod(StatType.MinionLevels, 1), Mod(StatType.AdditionalMinions, 2), Mod(StatType.AdditionalSkeletons, 1), Mod(StatType.MinionDamagePenalty, 20), Mod(StatType.MinionLife, 25), Mod(StatType.Intelligence, 12) }
            },
            new Unique
            {
                BaseId = "bone_sceptre", Name = "Gravewarden's Rod",
                Flavour = "The dead keep the watch now. They never sleep on it.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 9), Mod(StatType.GrantRaiseSkeletons, 6), Mod(StatType.RaiseSkeletonsLevels, 2), Mod(StatType.MinionDamage, 18), Mod(StatType.SoulBond, 2) }
            },
            new Unique
            {
                BaseId = "steel_dagger", Name = "Shepherd's Fang", ShepherdOnly = true,
                Flavour = "A little of the saint still lives in every wound.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 15), Mod(StatType.AttackSpeed, 18), Mod(StatType.GrantFangStrike, 7), Mod(StatType.Dexterity, 15), Mod(StatType.PoisonPenetration, 20) }
            },
            new Unique
            {
                BaseId = "short_bow", Name = "Widow's Choir", ShepherdOnly = true,
                Flavour = "Where its arrows fall, the air remembers his breath.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 18), Mod(StatType.GrantVenomArrow, 7), Mod(StatType.Dexterity, 20), Mod(StatType.PoisonPenetration, 15) }
            },
            new Unique { BaseId = "bone_sceptre", Name = "Crook of the Last Shepherd", ShepherdOnly = true,
                Flavour = "It taught the roots to hunger.", Mods = new[] { Mod(StatType.GrantVenomSpout, 7), Mod(StatType.SpellDamage, 30), Mod(StatType.PoisonDamage, 25), Mod(StatType.PoisonPenetration, 12) } },
            new Unique { BaseId = "grimoire", Name = "Book of Shed Skin", ShepherdOnly = true,
                Flavour = "Something small coils between every page.", Mods = new[] { Mod(StatType.GrantSummonViper, 7), Mod(StatType.MinionLevels, 1), Mod(StatType.AdditionalMinions, 3), Mod(StatType.MinionDamagePenalty, 30), Mod(StatType.MinionLife, 20), Mod(StatType.PoisonDamage, 15) } },
            new Unique { BaseId = "jade_amulet", Name = "Widow's Brood", ShepherdOnly = true,
                Flavour = "The venom passes from mother to daughter.", Mods = new[] { Mod(StatType.PoisonDamage, 35), Mod(StatType.DamageOverTime, 25), Mod(StatType.PoisonResistance, 25), Mod(StatType.PoisonPenetration, 15), Mod(StatType.VenomCloudOnHit, 30) } },

            // Ordinary drops: uncommon utility and restrained versions of existing tree effects.
            new Unique { BaseId = "gnarled_staff", Name = "Frostglass Bough",
                Flavour = "Its branches ring like glass when winter takes a life.",
                Mods = new[] { Mod(StatType.GrantIceShard, 6), Mod(StatType.SpellDamage, 14), Mod(StatType.CastSpeed, 12), Mod(StatType.ColdPenetration, 8), Mod(StatType.DamageVsChilled, 12) } },
            new Unique { BaseId = "leather_gloves", Name = "Thundergrip",
                Flavour = "The first spark always finds another hand to shake.",
                Mods = new[] { Mod(StatType.Evasion, 32), Mod(StatType.CriticalChance, 40), Mod(StatType.LightningResistance, 15), Mod(StatType.Stormblade, 35) } },
            new Unique { BaseId = "silk_slippers", Name = "Winter's Passage",
                Flavour = "Every hurried step leaves a little winter behind.",
                Mods = new[] { Mod(StatType.Evasion, 24), Mod(StatType.ColdResistance, 18), Mod(StatType.CastSpeed, 5), Mod(StatType.GrantDash, 5), Mod(StatType.GlacialStep, 1) } },
            new Unique { BaseId = "sage_circlet", Name = "Mercy's Echo",
                Flavour = "The last prayer is never spoken only once.",
                Mods = new[] { Mod(StatType.MaxMana, 25), Mod(StatType.LifeRegen, 2), Mod(StatType.GrantWarCry, 5), Mod(StatType.SecondWind, 1) } },
            new Unique { BaseId = "grimoire", Name = "Ledger of the Fallen",
                Flavour = "The final entry always points to the next name.",
                Mods = new[] { Mod(StatType.GrantDeathMark, 6), Mod(StatType.MinionDamage, 12), Mod(StatType.MinionLife, 15), Mod(StatType.DeathsHerald, 1) } },
            new Unique { BaseId = "kite_shield", Name = "Stillstone Aegis",
                Flavour = "Behind it, even a wounded heart remembers its rhythm.",
                Mods = new[] { Mod(StatType.Armour, 38), Mod(StatType.BlockChance, 12), Mod(StatType.MaxLife, 18), Mod(StatType.PercentLifeRegen, 0.4f) } },
            new Unique { BaseId = "jade_amulet", Name = "Gravesong Charm",
                Flavour = "The dead hum softly to whoever keeps their names.",
                Mods = new[] { Mod(StatType.Intelligence, 10), Mod(StatType.MinionLife, 18), Mod(StatType.MinionDamage, 8), Mod(StatType.SoulBond, 1) } },
            new Unique { BaseId = "great_mallet", Name = "Cinderwake",
                Flavour = "One blow buries the foe. The next scatters the ashes.",
                Mods = new[] { Mod(StatType.PhysicalDamage, 22), Mod(StatType.AreaOfEffect, 12), Mod(StatType.FireResistance, 15), Mod(StatType.FirePenetration, 10) } },
            new Unique { BaseId = "topaz_ring", Name = "Storm's Receipt",
                Flavour = "A debt paid in lightning is never settled once.",
                Mods = new[] { Mod(StatType.LightningResistance, 16), Mod(StatType.LightningDamage, 10), Mod(StatType.ManaOnKill, 2), Mod(StatType.AdditionalChains, 1) } },
            new Unique { BaseId = "iron_ring", Name = "The Last Draught",
                Flavour = "Courage comes in a bottle, and leaves in a heartbeat.",
                Mods = new[] { Mod(StatType.MaxLife, 14), Mod(StatType.LifeRegen, 1), Mod(StatType.AvoidStun, 8), Mod(StatType.OnslaughtOnHealthPotion, 1) } },
            // Single-ability tree bonuses belong on gear that also grants their ability.
            new Unique { BaseId = "gnarled_staff", Name = "Conductor's Reach",
                Flavour = "The storm follows wherever its bearer points.",
                Mods = new[] { Mod(StatType.GrantChainLightning, 6), Mod(StatType.LightningDamage, 15), Mod(StatType.CastSpeed, 12), Mod(StatType.AdditionalChains, 1), Mod(StatType.LightningPenetration, 8) } },
            new Unique { BaseId = "jade_amulet", Name = "Death's Grip",
                Flavour = "One whispered name commands a hundred restless hands.",
                Mods = new[] { Mod(StatType.GrantDeathMark, 6), Mod(StatType.MarkEffect, 20), Mod(StatType.MinionDamage, 4), Mod(StatType.MaxLife, 15) } },
            new Unique { BaseId = "gnarled_staff", Name = "The Unfinished Sentence",
                Flavour = "The next spell begins before the last word fades.",
                Mods = new[] { Mod(StatType.GrantFireBolt, 6), Mod(StatType.SpellDamage, 20), Mod(StatType.CastSpeed, 36), Mod(StatType.MaxMana, 30) } },
            new Unique { BaseId = "silk_gloves", Name = "Spellweaver's Hands",
                Flavour = "A hundred gestures, between one heartbeat and the next.",
                Mods = new[] { Mod(StatType.CastSpeed, 24), Mod(StatType.MaxMana, 25), Mod(StatType.ManaRegen, 20) } },
            new Unique { BaseId = "jade_amulet", Name = "Pendant of the Fleeting Thought",
                Flavour = "Catch the thought before the world can answer.",
                Mods = new[] { Mod(StatType.CastSpeed, 22), Mod(StatType.SpellDamage, 12), Mod(StatType.ManaRegen, 25), Mod(StatType.LightningResistance, 18) } },
        };

        private static readonly Dictionary<string, string> flavourByName = new Dictionary<string, string>();
        private static readonly int[] OrdinaryPool = BuildPool(false);
        private static readonly int[] ShepherdPool = BuildPool(true);

        private static int[] BuildPool(bool shepherdOnly)
        {
            var indices = new List<int>();
            for (int i = 0; i < All.Length; i++)
                if (All[i].ShepherdOnly == shepherdOnly) indices.Add(i);
            return indices.ToArray();
        }

        static UniqueItems()
        {
            foreach (Unique u in All)
                flavourByName[u.Name] = u.Flavour;
        }

        public static int Count => All.Length;

        /// <summary>A random unique item.</summary>
        public static ItemData Random(System.Random rng)
        {
            return Create(OrdinaryPool[rng.Next(OrdinaryPool.Length)]);
        }

        public static ItemData ShepherdReward(System.Random rng) => Create(ShepherdPool[rng.Next(ShepherdPool.Length)]);

        /// <summary>A random unique of a kind the filter accepts (any unique if none fits).</summary>
        public static ItemData Random(System.Random rng, System.Func<ItemType, bool> kind)
        {
            var fitting = new List<ItemData>();
            for (int k = 0; k < All.Length; k++)
            {
                if (All[k].ShepherdOnly) continue;
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

using System.Collections.Generic;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Hand-made items with fixed names and modifier sets (orange), and ranged numeric bonuses.
    /// Each has at least one uncommon stat or effect (extra arrows, leech, culling...; see the end of StatType).
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
            public UniqueModifier[] Mods;
            public bool ShepherdOnly;
            public int RequiredLevel = 7;
        }

        private readonly struct UniqueModifier
        {
            public readonly StatType Stat;
            public readonly float Min;
            public readonly float Max;

            public UniqueModifier(StatType stat, float min, float max)
            {
                Stat = stat;
                Min = min;
                Max = max;
            }

            public float Roll(System.Random rng) => Min == Max ? Min : rng.Next((int)Min, (int)Max + 1);
        }

        private static readonly System.Random DefaultRng = new System.Random();
        private static UniqueModifier Mod(StatType stat, float value) => new UniqueModifier(stat, value, value);
        private static UniqueModifier Roll(StatType stat, int min, int max) => new UniqueModifier(stat, min, max);

        private static readonly Unique[] All =
        {
            new Unique
            {
                BaseId = "hand_axe", Name = "Bonehew",
                Flavour = "Mortis swung it for a thousand years. It remembers every one.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 13, 19), Roll(StatType.AttackSpeed, 8, 12), Roll(StatType.MaxLife, 16, 24), Mod(StatType.LifeLeech, 3), Mod(StatType.ArmourPenetration, 10) }
            },
            new Unique
            {
                BaseId = "short_bow", Name = "Whisperwind",
                Flavour = "The arrow arrives before the sound.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 7, 11), Roll(StatType.AttackSpeed, 12, 18), Roll(StatType.Dexterity, 12, 18), Mod(StatType.AdditionalArrows, 2), Mod(StatType.ArmourPenetration, 8) }
            },
            new Unique
            {
                BaseId = "iron_mace", Name = "Ashfall",
                Flavour = "What the fire left, the mace finishes.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 14, 22), Roll(StatType.Strength, 12, 18), Roll(StatType.FireResistance, 16, 24), Mod(StatType.CullingStrike, 1), Mod(StatType.FirePenetration, 12) }
            },
            new Unique
            {
                BaseId = "iron_helmet", Name = "Crown of Ash",
                Flavour = "Taken from the Warlord's brow, still warm.",
                Mods = new[] { Roll(StatType.Armour, 44, 66), Roll(StatType.MaxLife, 16, 24), Roll(StatType.FireResistance, 16, 24), Roll(StatType.AreaOfEffect, 12, 18), Mod(StatType.IgniteChance, 15) }
            },
            new Unique
            {
                BaseId = "studded_vest", Name = "Sexton's Hide",
                Flavour = "Dug a hundred graves. Filled them too.",
                Mods = new[] { Roll(StatType.Armour, 52, 78), Roll(StatType.MaxLife, 24, 36), Roll(StatType.ColdResistance, 12, 18), Mod(StatType.LifeOnKill, 5), Roll(StatType.AvoidStun, 10, 14) }
            },
            new Unique
            {
                BaseId = "leather_boots", Name = "Wanderer's Stride",
                Flavour = "Every road leads somewhere. These find it faster.",
                Mods = new[] { Roll(StatType.MovementSpeed, 16, 24), Roll(StatType.Evasion, 36, 54), Roll(StatType.Dexterity, 8, 12), Roll(StatType.LifeRegen, 2, 4) }
            },
            new Unique
            {
                BaseId = "wooden_shield", Name = "The Keeper's Ward",
                Flavour = "The graveyard's last keeper held the gate with this. For a while.",
                Mods = new[] { Roll(StatType.Armour, 40, 60), Mod(StatType.BlockChance, 15), Roll(StatType.ColdResistance, 16, 24), Roll(StatType.AvoidStun, 20, 30) }
            },
            new Unique
            {
                BaseId = "ruby_ring", Name = "Emberheart",
                Flavour = "It beats, faintly.",
                Mods = new[] { Roll(StatType.FireResistance, 20, 30), Roll(StatType.CastSpeed, 8, 12), Roll(StatType.SpellDamage, 14, 22), Mod(StatType.FirePenetration, 8), Mod(StatType.IgniteChance, 15) }
            },
            new Unique
            {
                BaseId = "jade_amulet", Name = "Elder's Charm",
                Flavour = "Haven's elders have worn it since before the fire.",
                Mods = new[] { Roll(StatType.CastSpeed, 10, 14), Roll(StatType.MaxMana, 32, 48), Roll(StatType.LightningResistance, 16, 24), Mod(StatType.AdditionalChains, 1) }
            },
            new Unique
            {
                BaseId = "leather_quiver", Name = "Rimefletch",
                Flavour = "Feathered with frost that never melts.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 3, 5), Roll(StatType.ColdResistance, 12, 18), Mod(StatType.ChillOnHit, 22), Mod(StatType.ColdPenetration, 8), Mod(StatType.DamageVsChilled, 12) }
            },
            new Unique
            {
                BaseId = "steel_dagger", Name = "Leechfang",
                Flavour = "It drinks first. You drink after.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 5, 7), Roll(StatType.AttackSpeed, 10, 14), Mod(StatType.LifeLeech, 3), Mod(StatType.LifeOnKill, 3), Mod(StatType.ArmourPenetration, 10), Mod(StatType.OnslaughtOnKill, 15) }
            },
            new Unique
            {
                BaseId = "sapphire_ring", Name = "Twinflame Loop",
                Flavour = "Every spark it touches is born a twin.",
                Mods = new[] { Roll(StatType.Intelligence, 8, 12), Roll(StatType.MaxMana, 16, 24), Mod(StatType.AdditionalSpellProjectiles, 2), Mod(StatType.ElementalPenetration, 10) }
            },
            new Unique
            {
                BaseId = "sage_circlet", Name = "Stormcaller's Circlet",
                Flavour = "The sky listens to whoever wears it, and answers twice.",
                Mods = new[] { Roll(StatType.CastSpeed, 10, 14), Roll(StatType.MaxMana, 20, 30), Mod(StatType.AdditionalChains, 2), Roll(StatType.LightningResistance, 12, 18), Mod(StatType.LightningPenetration, 12) }
            },
            new Unique
            {
                BaseId = "rope_belt", Name = "Bloodroot Cord",
                Flavour = "Braided from roots that drank the battlefield dry.",
                Mods = new[] { Roll(StatType.MaxLife, 18, 26), Roll(StatType.LifeRegen, 2, 4), Roll(StatType.Strength, 6, 10), Mod(StatType.HealthPotionRecovery, 25) }
            },
            new Unique
            {
                BaseId = "silk_robe", Name = "Mantle of the Still Mind",
                Flavour = "Pain is a rumour. Mana is the truth.",
                Mods = new[] { Roll(StatType.Evasion, 32, 48), Roll(StatType.MaxMana, 48, 72), Roll(StatType.CastSpeed, 24, 36), Roll(StatType.ManaRegen, 32, 48) }
            },
            new Unique
            {
                BaseId = "grimoire", Name = "The Ossuary Codex",
                Flavour = "Every page a name. Every name still answers.",
                Mods = new[] { Mod(StatType.GrantDeathMark, 8), Mod(StatType.MinionLevels, 1), Mod(StatType.AdditionalMinions, 2), Mod(StatType.AdditionalSkeletons, 1), Mod(StatType.MinionDamagePenalty, 20), Roll(StatType.MinionLife, 20, 30), Roll(StatType.Intelligence, 10, 14) }
            },
            new Unique
            {
                BaseId = "bone_sceptre", Name = "Gravewarden's Rod",
                Flavour = "The dead keep the watch now. They never sleep on it.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 7, 11), Mod(StatType.GrantRaiseSkeletons, 6), Mod(StatType.RaiseSkeletonsLevels, 2), Roll(StatType.MinionDamage, 14, 22), Mod(StatType.SoulBond, 2) }
            },
            new Unique
            {
                BaseId = "steel_dagger", Name = "Shepherd's Fang", ShepherdOnly = true,
                Flavour = "A little of the saint still lives in every wound.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 12, 18), Roll(StatType.AttackSpeed, 14, 22), Mod(StatType.GrantFangStrike, 7), Roll(StatType.Dexterity, 12, 18), Mod(StatType.PoisonPenetration, 20) }
            },
            new Unique
            {
                BaseId = "short_bow", Name = "Widow's Choir", ShepherdOnly = true,
                Flavour = "Where its arrows fall, the air remembers his breath.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 14, 22), Mod(StatType.GrantVenomArrow, 7), Roll(StatType.Dexterity, 16, 24), Mod(StatType.PoisonPenetration, 15) }
            },
            new Unique { BaseId = "bone_sceptre", Name = "Crook of the Last Shepherd", ShepherdOnly = true,
                Flavour = "It taught the roots to hunger.", Mods = new[] { Mod(StatType.GrantVenomSpout, 7), Roll(StatType.SpellDamage, 24, 36), Roll(StatType.PoisonDamage, 20, 30), Mod(StatType.PoisonPenetration, 12) } },
            new Unique { BaseId = "grimoire", Name = "Book of Shed Skin", ShepherdOnly = true,
                Flavour = "Something small coils between every page.", Mods = new[] { Mod(StatType.GrantSummonViper, 7), Mod(StatType.MinionLevels, 1), Mod(StatType.AdditionalMinions, 3), Mod(StatType.MinionDamagePenalty, 30), Roll(StatType.MinionLife, 16, 24), Roll(StatType.PoisonDamage, 12, 18) } },
            new Unique { BaseId = "jade_amulet", Name = "Widow's Brood", ShepherdOnly = true,
                Flavour = "The venom passes from mother to daughter.", Mods = new[] { Roll(StatType.PoisonDamage, 28, 42), Roll(StatType.DamageOverTime, 20, 30), Roll(StatType.PoisonResistance, 20, 30), Mod(StatType.PoisonPenetration, 15), Mod(StatType.VenomCloudOnHit, 30) } },

            // Ordinary drops: uncommon utility and restrained versions of existing tree effects.
            new Unique { BaseId = "gnarled_staff", Name = "Frostglass Bough",
                Flavour = "Its branches ring like glass when winter takes a life.",
                Mods = new[] { Mod(StatType.GrantIceShard, 6), Roll(StatType.SpellDamage, 11, 17), Roll(StatType.CastSpeed, 10, 14), Mod(StatType.ColdPenetration, 8), Mod(StatType.DamageVsChilled, 12) } },
            new Unique { BaseId = "leather_gloves", Name = "Thundergrip",
                Flavour = "The first spark always finds another hand to shake.",
                Mods = new[] { Roll(StatType.Evasion, 26, 38), Roll(StatType.CriticalChance, 22, 34), Roll(StatType.LightningResistance, 12, 18), Mod(StatType.Stormblade, 35) } },
            new Unique { BaseId = "silk_slippers", Name = "Winter's Passage",
                Flavour = "Every hurried step leaves a little winter behind.",
                Mods = new[] { Roll(StatType.Evasion, 19, 29), Roll(StatType.ColdResistance, 14, 22), Roll(StatType.CastSpeed, 4, 6), Mod(StatType.GrantDash, 5), Mod(StatType.GlacialStep, 1) } },
            new Unique { BaseId = "sage_circlet", Name = "Mercy's Echo",
                Flavour = "The last prayer is never spoken only once.",
                Mods = new[] { Roll(StatType.MaxMana, 20, 30), Roll(StatType.LifeRegen, 1, 3), Mod(StatType.GrantWarCry, 5), Mod(StatType.SecondWind, 1) } },
            new Unique { BaseId = "grimoire", Name = "Ledger of the Fallen",
                Flavour = "The final entry always points to the next name.",
                Mods = new[] { Mod(StatType.GrantDeathMark, 6), Roll(StatType.MinionDamage, 10, 14), Roll(StatType.MinionLife, 12, 18), Mod(StatType.DeathsHerald, 1) } },
            new Unique { BaseId = "kite_shield", Name = "Stillstone Aegis",
                Flavour = "Behind it, even a wounded heart remembers its rhythm.",
                Mods = new[] { Roll(StatType.Armour, 30, 46), Mod(StatType.BlockChance, 12), Roll(StatType.MaxLife, 14, 22), Mod(StatType.PercentLifeRegen, 0.4f) } },
            new Unique { BaseId = "jade_amulet", Name = "Gravesong Charm",
                Flavour = "The dead hum softly to whoever keeps their names.",
                Mods = new[] { Roll(StatType.Intelligence, 8, 12), Roll(StatType.MinionLife, 14, 22), Roll(StatType.MinionDamage, 6, 10), Mod(StatType.SoulBond, 1) } },
            new Unique { BaseId = "great_mallet", Name = "Cinderwake",
                Flavour = "One blow buries the foe. The next scatters the ashes.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 18, 26), Roll(StatType.AreaOfEffect, 10, 14), Roll(StatType.FireResistance, 12, 18), Mod(StatType.FirePenetration, 10) } },
            new Unique { BaseId = "topaz_ring", Name = "Storm's Receipt",
                Flavour = "A debt paid in lightning is never settled once.",
                Mods = new[] { Roll(StatType.LightningResistance, 13, 19), Roll(StatType.LightningDamage, 8, 12), Mod(StatType.ManaOnKill, 2), Mod(StatType.AdditionalChains, 1) } },
            new Unique { BaseId = "iron_ring", Name = "The Last Draught",
                Flavour = "Courage comes in a bottle, and leaves in a heartbeat.",
                Mods = new[] { Roll(StatType.MaxLife, 11, 17), Roll(StatType.LifeRegen, 1, 2), Roll(StatType.AvoidStun, 6, 10), Mod(StatType.OnslaughtOnHealthPotion, 1) } },
            // Single-ability tree bonuses belong on gear that also grants their ability.
            new Unique { BaseId = "gnarled_staff", Name = "Conductor's Reach",
                Flavour = "The storm follows wherever its bearer points.",
                Mods = new[] { Mod(StatType.GrantChainLightning, 6), Roll(StatType.LightningDamage, 12, 18), Roll(StatType.CastSpeed, 10, 14), Mod(StatType.AdditionalChains, 1), Mod(StatType.LightningPenetration, 8) } },
            new Unique { BaseId = "jade_amulet", Name = "Death's Grip",
                Flavour = "One whispered name commands a hundred restless hands.",
                Mods = new[] { Mod(StatType.GrantDeathMark, 6), Mod(StatType.MarkEffect, 20), Roll(StatType.MinionDamage, 3, 5), Roll(StatType.MaxLife, 12, 18) } },
            new Unique { BaseId = "gnarled_staff", Name = "The Unfinished Sentence",
                Flavour = "The next spell begins before the last word fades.",
                Mods = new[] { Mod(StatType.GrantFireBolt, 6), Roll(StatType.SpellDamage, 16, 24), Roll(StatType.CastSpeed, 29, 43), Roll(StatType.MaxMana, 24, 36) } },
            new Unique { BaseId = "silk_gloves", Name = "Spellweaver's Hands",
                Flavour = "A hundred gestures, between one heartbeat and the next.",
                Mods = new[] { Roll(StatType.CastSpeed, 19, 29), Roll(StatType.MaxMana, 20, 30), Roll(StatType.ManaRegen, 16, 24) } },
            new Unique { BaseId = "jade_amulet", Name = "Pendant of the Fleeting Thought",
                Flavour = "Catch the thought before the world can answer.",
                Mods = new[] { Roll(StatType.CastSpeed, 18, 26), Roll(StatType.SpellDamage, 10, 14), Roll(StatType.ManaRegen, 20, 30), Roll(StatType.LightningResistance, 14, 22) } },
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

        public static int RequiredLevelFor(string name)
        {
            foreach (Unique u in All)
                if (u.Name == name) return u.ShepherdOnly ? System.Math.Max(21, u.RequiredLevel) : u.RequiredLevel;
            return 0;
        }

        /// <summary>A random unique item.</summary>
        public static ItemData Random(System.Random rng)
        {
            return Create(OrdinaryPool[rng.Next(OrdinaryPool.Length)], rng);
        }

        public static ItemData ShepherdReward(System.Random rng) => Create(ShepherdPool[rng.Next(ShepherdPool.Length)], rng);

        /// <summary>A random unique of a kind the filter accepts (any unique if none fits).</summary>
        public static ItemData Random(System.Random rng, System.Func<ItemType, bool> kind)
        {
            var fitting = new List<int>();
            for (int k = 0; k < All.Length; k++)
            {
                if (All[k].ShepherdOnly) continue;
                ItemData shape = ItemGenerator.Display(All[k].BaseId, All[k].Name, ItemRarity.Unique);
                if (kind(shape.Type))
                    fitting.Add(k);
            }
            return fitting.Count > 0 ? Create(fitting[rng.Next(fitting.Count)], rng) : Random(rng);
        }

        public static ItemData Create(int index) => Create(index, DefaultRng);

        /// <summary>Roll a fresh unique using the caller's random source.</summary>
        public static ItemData Create(int index, System.Random rng) => Build(All[index % All.Length], rng);

        private static ItemData Build(Unique u, System.Random rng, ItemData saved = null)
        {
            var mods = new List<StatModifier>();
            foreach (UniqueModifier mod in u.Mods)
            {
                // Loading must never reroll. Clamp older values to the current range and
                // restore newly added modifiers at their midpoint; signature effects stay fixed.
                float value = saved == null ? mod.Roll(rng) : (mod.Min + mod.Max) * 0.5f;
                if (saved != null)
                {
                    foreach (StatModifier previous in saved.Modifiers)
                    {
                        if (previous.Stat != mod.Stat) continue;
                        value = UnityEngine.Mathf.Clamp(previous.Value, mod.Min, mod.Max);
                        break;
                    }
                }
                mods.Add(new StatModifier(mod.Stat, value));
            }
            ItemData shape = ItemGenerator.Display(u.BaseId, u.Name, ItemRarity.Unique);
            var item = new ItemData(shape.Id, u.Name, shape.Type, shape.Width, shape.Height, shape.Tint, mods,
                hasCape: false, weaponType: shape.WeaponType, rarity: ItemRarity.Unique);
            item.ArtId = shape.ArtId;
            item.ArtTint = shape.ArtTint;
            return item;
        }

        /// <summary>Apply today's modifier design while preserving the saved unique's rolls.</summary>
        public static ItemData Legalize(ItemData item)
        {
            foreach (Unique u in All)
            {
                if (u.Name != item.Name) continue;
                ItemData current = Build(u, null, item);
                current.ItemLevel = item.ItemLevel;
                return current;
            }
            return item;
        }

        /// <summary>A fresh roll of the named unique, or null if there's none.</summary>
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

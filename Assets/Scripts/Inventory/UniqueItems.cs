using System.Collections.Generic;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Hand-made items with fixed names and modifier sets (orange), and ranged numeric bonuses.
    /// Each has a few stats its archetype wants and one or two effects of its own (see the end
    /// of StatType). Each boss always drops one; any other drop has a small chance to be one.
    /// Only uniques the area level reaches can drop there, weighted so the strongest are rarest.
    /// Saves keep them by name, not index.
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
            public int RequiredLevel = 1;
            public int Weight = Common;
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

        // How often a unique drops against the others its area level allows: the strongest are rarest.
        private const int Common = 100, Uncommon = 45, Rare = 18, Mythic = 6;

        // Each unique: two to four stats its archetype wants, then one or two that are its own.
        // A unique drops only where the area level reaches its required level, and never needs
        // less than its base. Grouped by the areas that first drop them (1, 9, 17, 25, 34).
        private static readonly Unique[] All =
        {
            // ---- Level 1: the Greenwood
            new Unique
            {
                BaseId = "hand_axe", Name = "Bonehew", RequiredLevel = 1, Weight = Common,
                Flavour = "Mortis swung it for a thousand years. It remembers every one.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 4, 7), Roll(StatType.Strength, 8, 12), Roll(StatType.MaxLife, 12, 20), Roll(StatType.LifePercentOnKill, 2, 3) }
            },
            new Unique
            {
                BaseId = "short_bow", Name = "Whisperwind", RequiredLevel = 1, Weight = Common,
                Flavour = "The arrow arrives before the sound.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 3, 5), Roll(StatType.AttackSpeed, 10, 15), Roll(StatType.Dexterity, 8, 12), Mod(StatType.AdditionalArrows, 1) }
            },
            new Unique
            {
                BaseId = "ruby_ring", Name = "Emberheart", RequiredLevel = 1, Weight = Common,
                Flavour = "It beats, faintly.",
                Mods = new[] { Roll(StatType.FireResistance, 15, 25), Roll(StatType.FireDamage, 15, 25), Roll(StatType.CastSpeed, 4, 6), Roll(StatType.ExplodeOnKill, 8, 12) }
            },
            new Unique
            {
                BaseId = "studded_vest", Name = "Sexton's Hide", RequiredLevel = 1, Weight = Common,
                Flavour = "Dug a hundred graves. Filled them too.",
                Mods = new[] { Roll(StatType.Armour, 30, 45), Roll(StatType.Evasion, 20, 30), Roll(StatType.MaxLife, 15, 25), Roll(StatType.CurseOnHit, 8, 12) }
            },
            new Unique
            {
                BaseId = "leather_quiver", Name = "Rimefletch", RequiredLevel = 1, Weight = Common,
                Flavour = "Feathered with frost that never melts.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 2, 3), Roll(StatType.ColdDamage, 15, 25), Roll(StatType.ChillOnHit, 15, 25), Mod(StatType.PhysicalToCold, 1), Roll(StatType.DamageVsChilled, 15, 20) }
            },

            // ---- Level 9: the Lost Hollows and the Warren
            new Unique
            {
                BaseId = "flanged_mace", Name = "Ashfall", RequiredLevel = 9, Weight = Uncommon,
                Flavour = "What the fire left, the mace finishes.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 7, 10), Roll(StatType.Strength, 10, 14), Roll(StatType.FireDamage, 20, 30), Mod(StatType.PhysicalToFire, 1), Roll(StatType.IgniteChance, 15, 25) }
            },
            new Unique
            {
                BaseId = "assassin_dagger", Name = "Leechfang", RequiredLevel = 9, Weight = Uncommon,
                Flavour = "It drinks first. You drink after.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 3, 5), Roll(StatType.AttackSpeed, 8, 12), Roll(StatType.CriticalChance, 30, 40), Mod(StatType.LifeLeech, 3), Roll(StatType.OnslaughtOnKill, 20, 30) }
            },
            new Unique
            {
                BaseId = "sapphire_ring", Name = "Twinflame Loop", RequiredLevel = 9, Weight = Uncommon,
                Flavour = "Every spark it touches is born a twin.",
                Mods = new[] { Roll(StatType.Intelligence, 8, 12), Roll(StatType.SpellDamage, 10, 15), Roll(StatType.ColdResistance, 15, 20), Mod(StatType.AdditionalProjectiles, 1) }
            },
            new Unique
            {
                BaseId = "kite_shield", Name = "The Keeper's Ward", RequiredLevel = 9, Weight = Common,
                Flavour = "The graveyard's last keeper held the gate with this. For a while.",
                Mods = new[] { Roll(StatType.Armour, 40, 60), Roll(StatType.BlockChance, 12, 16), Roll(StatType.MaxLife, 15, 25), Roll(StatType.BlockRetaliation, 25, 35) }
            },
            new Unique
            {
                BaseId = "cut_lapis_amulet", Name = "Elder's Charm", RequiredLevel = 9, Weight = Common,
                Flavour = "Haven's elders have worn it since before the fire.",
                Mods = new[] { Roll(StatType.Intelligence, 10, 14), Roll(StatType.MaxMana, 20, 30), Roll(StatType.ManaRegen, 20, 30), Roll(StatType.ManaCostReduction, 20, 25) }
            },
            new Unique
            {
                BaseId = "stalker_boots", Name = "Wanderer's Stride", RequiredLevel = 9, Weight = Uncommon,
                Flavour = "Every road leads somewhere. These find it faster.",
                Mods = new[] { Roll(StatType.Evasion, 30, 45), Roll(StatType.MovementSpeed, 15, 20), Roll(StatType.Dexterity, 8, 12), Mod(StatType.OnslaughtAtFullLife, 1) }
            },
            new Unique
            {
                BaseId = "grave_sceptre", Name = "Gravewarden's Rod", RequiredLevel = 9, Weight = Common,
                Flavour = "The dead keep the watch now. They never sleep on it.",
                Mods = new[] { Roll(StatType.MinionDamage, 15, 25), Roll(StatType.MinionLife, 15, 25), Roll(StatType.Intelligence, 8, 12), Roll(StatType.GrantRaiseSkeletons, 4, 5), Mod(StatType.AdditionalSkeletons, 1) }
            },
            new Unique
            {
                BaseId = "heavy_belt", Name = "Bloodroot Cord", RequiredLevel = 9, Weight = Common,
                Flavour = "Braided from roots that drank the battlefield dry.",
                Mods = new[] { Roll(StatType.MaxLife, 25, 35), Roll(StatType.Strength, 8, 12), Roll(StatType.HealthPotionRecovery, 20, 30), Mod(StatType.PercentLifeRegen, 1) }
            },

            // ---- Level 17: the Haunted Graveyard and the Drowned Belfry
            new Unique
            {
                BaseId = "great_helm", Name = "Crown of Ash", RequiredLevel = 17, Weight = Uncommon,
                Flavour = "Taken from the Warlord's brow, still warm.",
                Mods = new[] { Roll(StatType.Armour, 50, 75), Roll(StatType.MaxLife, 25, 35), Roll(StatType.FireResistance, 20, 30), Roll(StatType.AreaOfEffect, 12, 18), Roll(StatType.ExplodeOnKill, 20, 25) }
            },
            new Unique
            {
                BaseId = "arcane_circlet", Name = "Stormcaller's Circlet", RequiredLevel = 17, Weight = Uncommon,
                Flavour = "The sky listens to whoever wears it, and answers twice.",
                Mods = new[] { Roll(StatType.Intelligence, 12, 16), Roll(StatType.MaxMana, 30, 40), Roll(StatType.LightningDamage, 20, 30), Roll(StatType.ShockChance, 20, 30), Mod(StatType.AdditionalChains, 2) }
            },
            new Unique
            {
                BaseId = "assassin_gloves", Name = "Thundergrip", RequiredLevel = 17, Weight = Uncommon,
                Flavour = "The first spark always finds another hand to shake.",
                Mods = new[] { Roll(StatType.Evasion, 30, 45), Roll(StatType.AttackSpeed, 8, 12), Roll(StatType.CriticalChance, 30, 40), Roll(StatType.Stormblade, 35, 45), Roll(StatType.ShockChance, 15, 20) }
            },
            new Unique
            {
                BaseId = "archmage_staff", Name = "Frostglass Bough", RequiredLevel = 17, Weight = Uncommon,
                Flavour = "Its branches ring like glass when winter takes a life.",
                Mods = new[] { Roll(StatType.GrantIceShard, 5, 6), Roll(StatType.SpellDamage, 20, 30), Roll(StatType.ColdDamage, 15, 25), Roll(StatType.CastSpeed, 8, 12), Roll(StatType.Shatter, 25, 35), Roll(StatType.ColdPenetration, 8, 12) }
            },
            new Unique
            {
                BaseId = "arcane_slippers", Name = "Winter's Passage", RequiredLevel = 17, Weight = Common,
                Flavour = "Every hurried step leaves a little winter behind.",
                Mods = new[] { Roll(StatType.MovementSpeed, 15, 20), Roll(StatType.ColdResistance, 20, 30), Roll(StatType.Intelligence, 10, 14), Roll(StatType.GrantDash, 5, 6), Mod(StatType.GlacialStep, 1) }
            },
            new Unique
            {
                BaseId = "mystic_circlet", Name = "Mercy's Echo", RequiredLevel = 17, Weight = Common,
                Flavour = "The last prayer is never spoken only once.",
                Mods = new[] { Roll(StatType.MaxLife, 20, 30), Roll(StatType.MaxMana, 25, 35), Roll(StatType.CastSpeed, 6, 10), Roll(StatType.GrantWarCry, 5, 6), Mod(StatType.SecondWind, 1) }
            },
            new Unique
            {
                BaseId = "lich_codex", Name = "Ledger of the Fallen", RequiredLevel = 17, Weight = Common,
                Flavour = "The final entry always points to the next name.",
                Mods = new[] { Roll(StatType.GrantDeathMark, 5, 6), Roll(StatType.MinionDamage, 20, 30), Roll(StatType.MinionSpeed, 10, 15), Roll(StatType.MarkEffect, 25, 35), Mod(StatType.DeathsHerald, 1) }
            },
            new Unique
            {
                BaseId = "earthbreaker", Name = "Cinderwake", RequiredLevel = 17, Weight = Uncommon,
                Flavour = "One blow buries the foe. The next scatters the ashes.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 14, 20), Roll(StatType.Strength, 12, 16), Roll(StatType.AreaOfEffect, 15, 20), Roll(StatType.MeleeSplash, 40, 50) }
            },
            new Unique
            {
                BaseId = "mithril_ring", Name = "The Last Draught", RequiredLevel = 17, Weight = Common,
                Flavour = "Courage comes in a bottle, and leaves in a heartbeat.",
                Mods = new[] { Roll(StatType.MaxLife, 20, 30), Roll(StatType.PhysicalDamage, 2, 4), Roll(StatType.HealthPotionRecovery, 25, 35), Mod(StatType.OnslaughtOnHealthPotion, 1) }
            },
            new Unique
            {
                BaseId = "arcane_gloves", Name = "Spellweaver's Hands", RequiredLevel = 17, Weight = Uncommon,
                Flavour = "A hundred gestures, between one heartbeat and the next.",
                Mods = new[] { Roll(StatType.CastSpeed, 14, 20), Roll(StatType.MaxMana, 25, 35), Roll(StatType.Intelligence, 10, 14), Roll(StatType.SpellLeech, 2, 3) }
            },
            new Unique
            {
                BaseId = "flawless_lapis_amulet", Name = "Gravesong Charm", RequiredLevel = 17, Weight = Common,
                Flavour = "The dead hum softly to whoever keeps their names.",
                Mods = new[] { Roll(StatType.Intelligence, 12, 16), Roll(StatType.MinionLife, 20, 30), Roll(StatType.MaxLife, 15, 25), Roll(StatType.SoulBond, 2, 3), Roll(StatType.BoneArmour, 12, 18) }
            },
            new Unique
            {
                BaseId = "flawless_topaz_ring", Name = "Storm's Receipt", RequiredLevel = 17, Weight = Uncommon,
                Flavour = "A debt paid in lightning is never settled once.",
                Mods = new[] { Roll(StatType.LightningResistance, 20, 30), Roll(StatType.LightningDamage, 15, 25), Roll(StatType.AttackSpeed, 5, 8), Mod(StatType.PhysicalToLightning, 1), Roll(StatType.ShockChance, 10, 15) }
            },

            // ---- Level 25: the Ashen Ruins
            new Unique
            {
                BaseId = "archon_vestment", Name = "Mantle of the Still Mind", RequiredLevel = 25, Weight = Rare,
                Flavour = "Pain is a rumour. Mana is the truth.",
                Mods = new[] { Roll(StatType.Evasion, 40, 60), Roll(StatType.MaxMana, 50, 70), Roll(StatType.IncreasedMana, 15, 20), Roll(StatType.ManaAbsorb, 25, 30), Roll(StatType.ManaAsDamage, 3, 5) }
            },
            new Unique
            {
                BaseId = "eldritch_staff", Name = "The Unfinished Sentence", RequiredLevel = 25, Weight = Rare,
                Flavour = "The next spell begins before the last word fades.",
                Mods = new[] { Roll(StatType.GrantFireBolt, 7, 8), Roll(StatType.SpellDamage, 25, 35), Roll(StatType.FireDamage, 15, 25), Roll(StatType.CastSpeed, 12, 18), Roll(StatType.SpellEcho, 20, 30) }
            },
            new Unique
            {
                BaseId = "eldritch_staff", Name = "Conductor's Reach", RequiredLevel = 25, Weight = Uncommon,
                Flavour = "The storm follows wherever its bearer points.",
                Mods = new[] { Roll(StatType.GrantChainLightning, 7, 8), Roll(StatType.SpellDamage, 20, 30), Roll(StatType.LightningDamage, 25, 35), Roll(StatType.CastSpeed, 10, 14), Mod(StatType.AdditionalChains, 2), Roll(StatType.LightningPenetration, 12, 18) }
            },
            new Unique
            {
                BaseId = "executioner_axe", Name = "Gorebrand", RequiredLevel = 25, Weight = Rare,
                Flavour = "It grows heavier with every life, and swings all the faster for it.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 18, 26), Roll(StatType.AttackSpeed, 8, 12), Roll(StatType.MaxLife, 30, 40), Roll(StatType.Rampage, 4, 6), Roll(StatType.LifePercentOnKill, 2, 3) }
            },
            new Unique
            {
                BaseId = "bastion_shield", Name = "Stillstone Aegis", RequiredLevel = 25, Weight = Rare,
                Flavour = "Behind it, even a wounded heart remembers its rhythm.",
                Mods = new[] { Roll(StatType.Armour, 80, 120), Roll(StatType.BlockChance, 16, 20), Roll(StatType.MaxLife, 30, 45), Roll(StatType.AvoidStun, 25, 35), Roll(StatType.DamageTaken, -12, -8) }
            },
            new Unique
            {
                BaseId = "barbed_quiver", Name = "Hollowpoint", RequiredLevel = 25, Weight = Uncommon,
                Flavour = "Close enough to see their eyes. Close enough to miss nothing.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 6, 9), Roll(StatType.BowDamage, 20, 30), Roll(StatType.CriticalChance, 25, 35), Mod(StatType.PointBlank, 1), Roll(StatType.CriticalMultiplier, 25, 35) }
            },
            new Unique
            {
                BaseId = "necronomicon", Name = "The Ossuary Codex", RequiredLevel = 25, Weight = Rare,
                Flavour = "Every page a name. Every name still answers. Every answer costs.",
                Mods = new[] { Roll(StatType.GrantDeathMark, 7, 8), Roll(StatType.MinionLife, 25, 35), Roll(StatType.Intelligence, 15, 20), Mod(StatType.AdditionalMinions, 3), Mod(StatType.MinionLevels, 1), Roll(StatType.DamageTaken, 15, 20) }
            },
            new Unique
            {
                BaseId = "royal_lapis_amulet", Name = "Pendant of the Fleeting Thought", RequiredLevel = 25, Weight = Rare,
                Flavour = "Catch the thought before the world can answer.",
                Mods = new[] { Roll(StatType.CastSpeed, 12, 18), Roll(StatType.SpellDamage, 15, 20), Roll(StatType.ManaRegen, 30, 40), Mod(StatType.AllSpellLevels, 1) }
            },
            new Unique
            {
                BaseId = "royal_jade_amulet", Name = "Death's Grip", RequiredLevel = 25, Weight = Uncommon,
                Flavour = "It closes on the weak, and does not open again.",
                Mods = new[] { Roll(StatType.Dexterity, 15, 20), Roll(StatType.CriticalChance, 30, 40), Roll(StatType.CriticalMultiplier, 20, 30), Mod(StatType.CullingStrike, 1), Roll(StatType.OnslaughtOnKill, 25, 35) }
            },

            // ---- Level 30+: the Frozen Hollow. The strongest, and the rarest.
            new Unique
            {
                BaseId = "imperial_bow", Name = "Starfall", RequiredLevel = 32, Weight = Mythic,
                Flavour = "Loosed at the sky. The sky sends it back in pieces.",
                Mods = new[] { Roll(StatType.PhysicalDamage, 16, 22), Roll(StatType.AttackSpeed, 12, 18), Roll(StatType.Dexterity, 18, 24), Mod(StatType.AdditionalArrows, 2), Roll(StatType.ArmourPenetration, 15, 20) }
            },
            new Unique
            {
                BaseId = "warlord_plate", Name = "The Red Covenant", RequiredLevel = 30, Weight = Mythic,
                Flavour = "Sign in blood. Every wound is paid for in kind, and then some.",
                Mods = new[] { Roll(StatType.Armour, 120, 160), Roll(StatType.MaxLife, 60, 80), Roll(StatType.Strength, 15, 20), Roll(StatType.MoreDamage, 35, 45), Mod(StatType.NoLifeRegen, 1) }
            },
            new Unique
            {
                BaseId = "archon_diadem", Name = "The Glass Diadem", RequiredLevel = 34, Weight = Mythic,
                Flavour = "It shows the wearer everything. It cannot bear to be dropped.",
                Mods = new[] { Roll(StatType.Intelligence, 20, 25), Roll(StatType.CastSpeed, 10, 15), Roll(StatType.SpellDamage, 25, 35), Mod(StatType.AllSpellLevels, 2), Roll(StatType.MoreLife, -40, -35) }
            },
            new Unique
            {
                BaseId = "nightstalker_gloves", Name = "Hands of the Butcher-King", RequiredLevel = 34, Weight = Mythic,
                Flavour = "No blow ends where it lands.",
                Mods = new[] { Roll(StatType.AttackSpeed, 10, 14), Roll(StatType.PhysicalDamage, 5, 8), Roll(StatType.MaxLife, 30, 40), Roll(StatType.MeleeSplash, 30, 40), Roll(StatType.Rampage, 3, 4) }
            },

            // ---- The Shepherd's own: only he drops these.
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

        /// <summary>A random unique item from any area level.</summary>
        public static ItemData Random(System.Random rng) => Random(rng, int.MaxValue);

        public static ItemData ShepherdReward(System.Random rng) => Create(ShepherdPool[rng.Next(ShepherdPool.Length)], rng);

        /// <summary>
        /// A random unique that can drop at this area level, of a kind the filter accepts if one
        /// does (any droppable unique otherwise). Rarer uniques come up less often.
        /// </summary>
        public static ItemData Random(System.Random rng, int areaLevel, System.Func<ItemType, bool> kind = null)
        {
            var fitting = new List<int>();
            if (kind != null)
            {
                foreach (int k in OrdinaryPool)
                    if (CanDrop(k, areaLevel) && kind(ItemGenerator.Display(All[k].BaseId, All[k].Name, ItemRarity.Unique).Type))
                        fitting.Add(k);
            }
            if (fitting.Count == 0)
            {
                foreach (int k in OrdinaryPool)
                    if (CanDrop(k, areaLevel)) fitting.Add(k);
            }
            // The lowest-level uniques can always drop, so the pool is never empty.
            if (fitting.Count == 0)
                fitting.AddRange(OrdinaryPool);
            return Create(PickWeighted(rng, fitting), rng);
        }

        /// <summary>Whether a unique (by index) can drop at this area level. Shepherd rewards never drop elsewhere.</summary>
        public static bool CanDrop(int index, int areaLevel) =>
            !All[index].ShepherdOnly && All[index].RequiredLevel <= System.Math.Max(1, areaLevel);

        /// <summary>How likely a unique is to be chosen against the others that can drop.</summary>
        public static int DropWeight(int index) => All[index].Weight;

        public static string NameOf(int index) => All[index].Name;
        public static string BaseIdOf(int index) => All[index].BaseId;
        public static bool IsShepherdOnly(int index) => All[index].ShepherdOnly;
        public static int ShepherdRewardCount => ShepherdPool.Length;

        /// <summary>The unique's modifier lines with their roll ranges, for debug tools.</summary>
        public static string[] ModifierRanges(int index)
        {
            UniqueModifier[] mods = All[index].Mods;
            var lines = new string[mods.Length];
            for (int i = 0; i < mods.Length; i++)
            {
                UniqueModifier m = mods[i];
                string line = StatFormatter.ItemLine(new StatModifier(m.Stat, m.Max));
                lines[i] = m.Min == m.Max ? line
                    : line + "  (" + StatFormatter.Number(m.Min) + " to " + StatFormatter.Number(m.Max) + ")";
            }
            return lines;
        }

        /// <summary>
        /// Chance this unique is the one picked when an ordinary unique drops at this area level
        /// (0 if it can't drop there).
        /// </summary>
        public static float ShareAtAreaLevel(int index, int areaLevel)
        {
            if (!CanDrop(index, areaLevel)) return 0f;
            int total = 0;
            foreach (int k in OrdinaryPool)
                if (CanDrop(k, areaLevel)) total += All[k].Weight;
            return total > 0 ? (float)All[index].Weight / total : 0f;
        }

        /// <summary>Picks one of these unique indices, weighted by drop weight.</summary>
        public static int PickWeighted(System.Random rng, IList<int> indices)
        {
            int total = 0;
            foreach (int k in indices) total += All[k].Weight;
            int roll = rng.Next(total);
            foreach (int k in indices)
            {
                roll -= All[k].Weight;
                if (roll < 0) return k;
            }
            return indices[indices.Count - 1];
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

using System;
using System.Collections.Generic;

namespace PoeClone.Inventory
{
    public enum PassiveBranch
    {
        Origin,
        Might,     // strength: life, armour, melee, Cleave
        Grace,     // dexterity: evasion, speed, bows, chill
        Wisdom,    // intelligence: mana, spells, resistances
        Fury,      // between Might and Grace: attacks, critical strikes, Onslaught
        Storm,     // between Grace and Wisdom: lightning and cold, cast speed, cooldowns, Dash
        Zeal       // between Wisdom and Might: fire, burning, regeneration, Rejuvenate
    }

    /// <summary>One passive: its stats, where it's drawn, and which passives it connects to.</summary>
    public sealed class PassiveNode
    {
        public string Id;
        public string Name;
        public PassiveBranch Branch;
        public bool Notable;
        public bool Keystone;   // a rule-changer at the end of a path (special stats)
        public float X, Y;      // layout, in tree units (the inner ring is about half a unit out)
        public StatModifier[] Mods;
        public readonly List<string> Links = new List<string>();

        /// <summary>Devotion: these stats, once for every passive of <see cref="Branch"/> taken (this one included).</summary>
        public StatModifier[] PerBranchMods = new StatModifier[0];
    }

    /// <summary>
    /// The passive tree. From the centre, an inner ring of six gates: the three attributes (Might,
    /// Grace, Wisdom) and between each pair a hybrid (Fury, Storm, Zeal). Only the three attribute
    /// gates touch the centre, so a hybrid costs a step around the ring. Each sector has its own
    /// shape: Might a wheel, Fury a figure of eight, Grace one long winding loop, Storm a spiral,
    /// Wisdom a trident with crossbars, Zeal a star with a crown. Bridges join neighbouring sectors
    /// out at the rim, so a build can cut across instead of walking back to the centre.
    /// Keystones (diamonds) change the rules, and several tie two of the game's skills together.
    /// Devotion passives grow with every passive taken in their own sector, rewarding a build that
    /// commits over one that dabbles everywhere.
    /// Every level after the first gives one point; a passive can be taken once one next to it is.
    /// </summary>
    public static class PassiveTree
    {
        public const string OriginId = "origin";

        private static readonly Dictionary<string, PassiveNode> byId = new Dictionary<string, PassiveNode>();
        private static readonly List<PassiveNode> nodes = new List<PassiveNode>();

        public static IReadOnlyList<PassiveNode> Nodes => nodes;

        public static PassiveNode Get(string id)
        {
            return id != null && byId.TryGetValue(id, out PassiveNode node) ? node : null;
        }

        static PassiveTree()
        {
            Add(OriginId, "Origin", PassiveBranch.Origin, false, 0f, 0f);

            BuildMight();
            BuildFury();
            BuildGrace();
            BuildStorm();
            BuildWisdom();
            BuildZeal();

            // The inner ring: each gate links to the next, all the way round.
            Chain("m1", "f1", "g1", "s1", "w1", "z1", "m1");
            Chain("m1", OriginId, "g1");
            Chain(OriginId, "w1");

            // The rim: bridges between neighbouring sectors.
            Bridge("b_bloodrage", "Blood Rage", "m8", "f_berserk", Mod(StatType.AttackDamage, 6), Mod(StatType.LifeLeech, 0.5f));
            Bridge("b_skirmisher", "Skirmisher", "f_frenzy", "g_hoarfrost", Mod(StatType.Evasion, 25), Mod(StatType.CriticalChance, 3));
            Bridge("b_arctic", "Arctic Archer", "g_deadeye", "s_stride", Mod(StatType.ChillOnHit, 8), Mod(StatType.ColdDamage, 10));
            Bridge("b_surge", "Arcane Surge", "s_sp5", "w_l3", Mod(StatType.CastSpeed, 5), Mod(StatType.ManaRegen, 15));
            Bridge("b_fireward", "Fire Ward", "w_r3", "z_t1", Mod(StatType.FireResistance, 12), Mod(StatType.FireDamage, 8));
            Bridge("b_ironfaith", "Iron Faith", "z_t4", "m5", Mod(StatType.Armour, 25), Mod(StatType.LifeRegen, 2));
        }

        // ------------------------------------------------------------------ the sectors
        // Each sector is laid out in its own frame: u straight out from the centre along the
        // sector's angle, v across it (positive v leans toward the next sector anticlockwise).

        // Might (down-left): a trunk that forks round a wheel of six, three ways in.
        private static void BuildMight()
        {
            Sector(PassiveBranch.Might, 210f);
            N("m1", "Toughness", 0.55f, 0f, Mod(StatType.MaxLife, 12));
            N("m2", "Brawn", 0.9f, 0f, Mod(StatType.Strength, 6));
            Chain("m1", "m2");

            // Left arm: defence.
            N("m3", "Iron Skin", 1.2f, -0.3f, Mod(StatType.Armour, 25));
            N("m4", "Thick Hide", 1.5f, -0.45f, Mod(StatType.MaxLife, 15), Mod(StatType.Armour, 15));
            Nt("m5", "Juggernaut", 1.85f, -0.55f, Mod(StatType.MaxLife, 30), Mod(StatType.Armour, 50), Mod(StatType.BlockChance, 5));
            Chain("m2", "m3", "m4", "m5");

            // Right arm: offence.
            N("m6", "Heavy Hands", 1.2f, 0.3f, Mod(StatType.PhysicalDamage, 2));
            N("m7", "Power", 1.5f, 0.45f, Mod(StatType.Strength, 8));
            Nt("m8", "Brute Force", 1.85f, 0.55f, Mod(StatType.PhysicalDamage, 6), Mod(StatType.Strength, 10), Mod(StatType.MeleeRange, 15));
            Chain("m2", "m6", "m7", "m8");

            // Straight up the middle.
            N("m9", "Endurance", 1.45f, 0f, Mod(StatType.IncreasedLife, 4));
            Chain("m2", "m9");

            // The wheel: a ring of six round Bloodbath, reached from either arm or the middle.
            string[] wheel = Wheel("m_w", 2.45f, 0f, 0.42f,
                new[] { "Cleaving", "Bloodlust", "Cleaving", "Bloodlust", "Cleaving", "Bloodlust" },
                new[] { Mod(StatType.AreaOfEffect, 6) }, new[] { Mod(StatType.AttackDamage, 6) });
            Nt("m_bloodbath", "Bloodbath", 2.45f, 0f, Mod(StatType.AreaOfEffect, 15), Mod(StatType.MeleeRange, 10), Mod(StatType.LifeLeech, 1));
            Chain(wheel[0], "m_bloodbath", wheel[3]);
            Chain("m9", wheel[0]);
            Chain("m5", wheel[5]);
            Chain("m8", wheel[1]);

            Ks("k_unbreakable", "Unbreakable", 3.3f, 0f, Mod(StatType.Armour, 40));
            Devotion("k_unbreakable", Mod(StatType.IncreasedLife, 1), Mod(StatType.AttackDamage, 1));
            Chain(wheel[3], "k_unbreakable");

            Ks("k_executioner", "Executioner", 2.4f, -0.8f, Mod(StatType.CullingStrike, 1), Mod(StatType.LifeOnKill, 5));
            Chain("m5", "k_executioner");
            Ks("k_bloodthirst", "Bloodthirst", 2.4f, 0.8f, Mod(StatType.LifeLeech, 2));
            Chain("m8", "k_bloodthirst");
        }

        // Fury (down): two diamonds sharing a point, a figure of eight, with a keystone off each end.
        private static void BuildFury()
        {
            Sector(PassiveBranch.Fury, 270f);
            N("f1", "Battle Rhythm", 0.55f, 0f, Mod(StatType.AttackSpeed, 3), Mod(StatType.PhysicalDamage, 1));
            N("f2", "Sharpened Edges", 0.9f, 0f, Mod(StatType.CriticalChance, 3));
            Chain("f1", "f2");

            N("f_a1", "Keen Eye", 1.2f, -0.3f, Mod(StatType.CriticalChance, 4));
            N("f_a2", "Fervour", 1.2f, 0.3f, Mod(StatType.AttackDamage, 8));
            Nt("f_dancer", "Bladedancer", 1.5f, 0f, Mod(StatType.AttackSpeed, 8), Mod(StatType.CriticalChance, 5));
            Chain("f2", "f_a1", "f_dancer", "f_a2", "f2");

            N("f_b1", "Assassin's Mark", 1.85f, -0.35f, Mod(StatType.CriticalMultiplier, 15));
            N("f_b2", "Momentum", 1.85f, 0.35f, Mod(StatType.OnslaughtOnKill, 10));
            Nt("f_precision", "Deadly Precision", 2.2f, 0f, Mod(StatType.CriticalChance, 6), Mod(StatType.CriticalMultiplier, 20));
            Devotion("f_precision", Mod(StatType.CriticalMultiplier, 2));
            Chain("f_dancer", "f_b1", "f_precision", "f_b2", "f_dancer");

            Ks("k_stormblade", "Stormblade", 2.65f, 0f, Mod(StatType.Stormblade, 35), Mod(StatType.LightningDamage, 15));
            Chain("f_precision", "k_stormblade");

            Nt("f_berserk", "Berserker", 2.25f, -0.7f, Mod(StatType.DamageWhileLowLife, 25), Mod(StatType.LifeLeech, 1));
            Chain("f_b1", "f_berserk");
            Ks("k_pain", "Pain Attunement", 2.65f, -1.0f, Mod(StatType.DamageWhileLowLife, 50), Mod(StatType.IncreasedLife, -10));
            Chain("f_berserk", "k_pain");

            Nt("f_frenzy", "Frenzy", 2.25f, 0.7f, Mod(StatType.OnslaughtOnKill, 15), Mod(StatType.AttackSpeed, 6));
            Chain("f_b2", "f_frenzy");
            Ks("k_relentless", "Relentless", 2.65f, 1.0f, Mod(StatType.OnslaughtOnKill, 35), Mod(StatType.MovementSpeed, 5));
            Chain("f_frenzy", "k_relentless");
        }

        // Grace (down-right): one long loop - out one arm, round the far end and back the other -
        // with spurs hanging off it.
        private static void BuildGrace()
        {
            Sector(PassiveBranch.Grace, 330f);
            N("g1", "Nimble", 0.55f, 0f, Mod(StatType.Evasion, 20));
            N("g2", "Agility", 0.9f, 0f, Mod(StatType.Dexterity, 6));
            Chain("g1", "g2");

            N("g3", "Light Step", 1.2f, -0.25f, Mod(StatType.MovementSpeed, 4));
            N("g4", "Dodge", 1.5f, -0.35f, Mod(StatType.Evasion, 30));
            Nt("g5", "Wind Dancer", 1.8f, -0.2f, Mod(StatType.MovementSpeed, 8), Mod(StatType.Evasion, 60));
            N("g9", "Fleet", 2.05f, 0.1f, Mod(StatType.MovementSpeed, 3), Mod(StatType.Evasion, 15));
            N("g10", "Fletching", 2.3f, 0.35f, Mod(StatType.AttackDamage, 6));
            Chain("g2", "g3", "g4", "g5", "g9", "g10");

            N("g6", "Quick Hands", 1.2f, 0.3f, Mod(StatType.AttackSpeed, 5));
            N("g7", "Precision", 1.5f, 0.45f, Mod(StatType.PhysicalDamage, 2), Mod(StatType.Dexterity, 4));
            Nt("g8", "Flurry", 1.8f, 0.55f, Mod(StatType.AttackSpeed, 12), Mod(StatType.PhysicalDamage, 3));
            Chain("g2", "g6", "g7", "g8", "g10");

            Nt("g_deadeye", "Deadeye", 2.6f, 0.3f, Mod(StatType.AttackDamage, 10), Mod(StatType.CriticalChance, 4));
            Devotion("g_deadeye", Mod(StatType.AttackSpeed, 0.5f));
            Chain("g10", "g_deadeye");
            Ks("k_volley", "Volley", 3.0f, 0.35f, Mod(StatType.AdditionalArrows, 1));
            Chain("g_deadeye", "k_volley");

            Nt("g_phase", "Phase Run", 2.4f, -0.2f, Mod(StatType.MovementSpeed, 6), Mod(StatType.OnslaughtOnKill, 10), Mod(StatType.Evasion, 40));
            Chain("g9", "g_phase");

            Nt("g_hoarfrost", "Hoarfrost", 2.1f, -0.6f, Mod(StatType.ChillOnHit, 8), Mod(StatType.DamageVsChilled, 10));
            Chain("g5", "g_hoarfrost");
            Ks("k_frostbite", "Frostbite", 2.5f, -0.9f, Mod(StatType.ChillOnHit, 20), Mod(StatType.Shatter, 25), Mod(StatType.DamageVsChilled, 15));
            Chain("g_hoarfrost", "k_frostbite");
        }

        // Storm (up-right): a spiral winding out round the Eye of the Storm.
        private static void BuildStorm()
        {
            Sector(PassiveBranch.Storm, 30f);
            N("s1", "Quickened Mind", 0.55f, 0f, Mod(StatType.CastSpeed, 4));
            N("s2", "Static", 0.9f, 0f, Mod(StatType.LightningDamage, 8));
            Chain("s1", "s2");

            N("s_sp1", "Spark", 1.3f, 0f, Mod(StatType.LightningDamage, 8));
            N("s_sp2", "Rime", 1.49f, -0.45f, Mod(StatType.ColdDamage, 8), Mod(StatType.DamageVsChilled, 6));
            N("s_sp3", "Overcharge", 2.05f, -0.51f, Mod(StatType.ShockChance, 10));
            Nt("s_conductor", "Conductor", 2.41f, 0f, Mod(StatType.LightningDamage, 15), Mod(StatType.AdditionalChains, 1));
            N("s_sp5", "Haste", 2.12f, 0.63f, Mod(StatType.CastSpeed, 5));
            N("s_sp6", "Flow", 1.4f, 0.69f, Mod(StatType.CooldownRecovery, 6));
            Nt("s_eye", "Eye of the Storm", 1.8f, 0.05f, Mod(StatType.CastSpeed, 8), Mod(StatType.CooldownRecovery, 8), Mod(StatType.ManaRegen, 20));
            Devotion("s_eye", Mod(StatType.CastSpeed, 0.5f), Mod(StatType.CooldownRecovery, 0.5f));
            Chain("s2", "s_sp1", "s_sp2", "s_sp3", "s_conductor", "s_sp5", "s_sp6", "s_eye");

            Ks("k_thunderlord", "Thunderlord", 2.85f, 0f, Mod(StatType.ShockChance, 25), Mod(StatType.AdditionalChains, 1));
            Chain("s_conductor", "k_thunderlord");

            Nt("s_stride", "Frozen Stride", 2.3f, -0.95f, Mod(StatType.ColdDamage, 10), Mod(StatType.MovementSpeed, 5), Mod(StatType.CooldownRecovery, 5));
            Chain("s_sp3", "s_stride");
            Ks("k_glacial", "Glacial Step", 2.75f, -1.15f, Mod(StatType.GlacialStep, 1), Mod(StatType.CooldownRecovery, 10));
            Chain("s_stride", "k_glacial");
        }

        // Wisdom (up): a trident - three prongs out of one hub, tied by crossbars halfway along.
        private static void BuildWisdom()
        {
            Sector(PassiveBranch.Wisdom, 90f);
            N("w1", "Focus", 0.55f, 0f, Mod(StatType.MaxMana, 12));
            N("w2", "Insight", 0.9f, 0f, Mod(StatType.Intelligence, 6));
            N("w3", "Spellcraft", 1.2f, 0f, Mod(StatType.SpellDamage, 6));
            Chain("w1", "w2", "w3");

            // Mind: mana, ending in Mind over Matter.
            N("w4", "Deep Well", 1.5f, -0.45f, Mod(StatType.MaxMana, 20));
            N("w_l2", "Clarity", 1.85f, -0.5f, Mod(StatType.ManaRegen, 15));
            Nt("w_l3", "Arcane Mind", 2.2f, -0.5f, Mod(StatType.Intelligence, 15), Mod(StatType.MaxMana, 30), Mod(StatType.IncreasedMana, 10));
            Chain("w3", "w4", "w_l2", "w_l3");
            Ks("k_mom", "Mind over Matter", 2.65f, -0.6f, Mod(StatType.ManaAbsorb, 30), Mod(StatType.ManaOnKill, 3));
            Chain("w_l3", "k_mom");

            // Power: spell damage, ending in Twin Casting.
            N("w_m1", "Potency", 1.55f, 0f, Mod(StatType.SpellDamage, 8));
            N("w_m2", "Sorcery", 1.9f, 0f, Mod(StatType.SpellDamage, 6), Mod(StatType.Intelligence, 4));
            Nt("w_archmage", "Archmage", 2.25f, 0f, Mod(StatType.SpellDamage, 15), Mod(StatType.AreaOfEffect, 12));
            Devotion("w_archmage", Mod(StatType.SpellDamage, 1));
            Chain("w3", "w_m1", "w_m2", "w_archmage");
            Ks("k_twincast", "Twin Casting", 2.7f, 0f, Mod(StatType.AdditionalSpellProjectiles, 1), Mod(StatType.SpellDamage, 10));
            Chain("w_archmage", "k_twincast");

            // Wards: resistances.
            N("w6", "Warding", 1.5f, 0.45f, Mod(StatType.FireResistance, 12), Mod(StatType.ColdResistance, 12));
            N("w7", "Grounding", 1.85f, 0.5f, Mod(StatType.LightningResistance, 15), Mod(StatType.MaxLife, 8));
            Nt("w_r3", "Elemental Ward", 2.2f, 0.5f, Mod(StatType.FireResistance, 15), Mod(StatType.ColdResistance, 15), Mod(StatType.LightningResistance, 15));
            Chain("w3", "w6", "w7", "w_r3");

            // Crossbars.
            Chain("w_l2", "w_m2", "w7");
        }

        // Zeal (up-left): a hub with four spokes, their tips joined in a crown.
        private static void BuildZeal()
        {
            Sector(PassiveBranch.Zeal, 150f);
            N("z1", "Kindling", 0.55f, 0f, Mod(StatType.FireDamage, 6));
            N("z2", "Vigour", 0.9f, 0f, Mod(StatType.MaxLife, 10), Mod(StatType.LifeRegen, 1));
            N("z3", "Hearth", 1.25f, 0f, Mod(StatType.FireDamage, 8));
            Nt("z_hub", "Sacred Flame", 1.65f, 0f, Mod(StatType.FireDamage, 12), Mod(StatType.IgniteChance, 10));
            Devotion("z_hub", Mod(StatType.FireDamage, 1));
            Chain("z1", "z2", "z3", "z_hub");

            N("z_s1", "Smoulder", 1.93f, -0.48f, Mod(StatType.IgniteChance, 8));
            N("z_s2", "Combustion", 2.17f, -0.19f, Mod(StatType.ExplodeOnKill, 6));
            N("z_s3", "Renewal", 2.17f, 0.19f, Mod(StatType.PercentLifeRegen, 0.3f));
            N("z_s4", "Fervent Heart", 1.93f, 0.48f, Mod(StatType.MaxLife, 15));
            Nt("z_t1", "Wildfire", 2.15f, -0.9f, Mod(StatType.IgniteChance, 15), Mod(StatType.FireDamage, 12));
            Nt("z_t2", "Funeral Pyre", 2.6f, -0.34f, Mod(StatType.ExplodeOnKill, 12), Mod(StatType.FireDamage, 6));
            Nt("z_t3", "Lifeblood", 2.6f, 0.34f, Mod(StatType.PercentLifeRegen, 0.6f), Mod(StatType.IncreasedLife, 6));
            Nt("z_t4", "Zealot's Vigour", 2.15f, 0.9f, Mod(StatType.LifeRegen, 4), Mod(StatType.MaxLife, 25));
            Chain("z_hub", "z_s1", "z_t1");
            Chain("z_hub", "z_s2", "z_t2");
            Chain("z_hub", "z_s3", "z_t3");
            Chain("z_hub", "z_s4", "z_t4");
            Chain("z_t1", "z_t2", "z_t3", "z_t4");

            Ks("k_inferno", "Avatar of Fire", 3.05f, -0.45f, Mod(StatType.ExplodeOnKill, 20), Mod(StatType.FireDamage, 30),
                Mod(StatType.ColdDamage, -30), Mod(StatType.LightningDamage, -30));
            Chain("z_t2", "k_inferno");
            Ks("k_secondwind", "Second Wind", 3.05f, 0.45f, Mod(StatType.SecondWind, 1), Mod(StatType.CooldownRecovery, 10));
            Chain("z_t3", "k_secondwind");
        }

        // ------------------------------------------------------------------ building helpers

        private static PassiveBranch sector;
        private static float sectorCos, sectorSin;

        private static void Sector(PassiveBranch branch, float angle)
        {
            sector = branch;
            double rad = angle * Math.PI / 180.0;
            sectorCos = (float)Math.Cos(rad);
            sectorSin = (float)Math.Sin(rad);
        }

        private static PassiveNode Place(string id, string name, bool notable, float u, float v, StatModifier[] mods)
        {
            return Add(id, name, sector, notable, u * sectorCos - v * sectorSin, u * sectorSin + v * sectorCos, mods);
        }

        private static PassiveNode N(string id, string name, float u, float v, params StatModifier[] mods) => Place(id, name, false, u, v, mods);
        private static PassiveNode Nt(string id, string name, float u, float v, params StatModifier[] mods) => Place(id, name, true, u, v, mods);

        private static PassiveNode Ks(string id, string name, float u, float v, params StatModifier[] mods)
        {
            PassiveNode node = Place(id, name, true, u, v, mods);
            node.Keystone = true;
            return node;
        }

        private static void Devotion(string id, params StatModifier[] perPassive)
        {
            byId[id].PerBranchMods = perPassive;
        }

        // A ring of passives round (u, v), the first one on the side facing the centre, going
        // anticlockwise; names and stats alternate between the two lists given.
        private static string[] Wheel(string prefix, float u, float v, float radius, string[] names, StatModifier[] even, StatModifier[] odd)
        {
            var ids = new string[names.Length];
            for (int k = 0; k < names.Length; k++)
            {
                double a = Math.PI + k * 2.0 * Math.PI / names.Length;
                ids[k] = prefix + (k + 1);
                N(ids[k], names[k], u + radius * (float)Math.Cos(a), v - radius * (float)Math.Sin(a), k % 2 == 0 ? even : odd);
                if (k > 0)
                    Link(ids[k - 1], ids[k]);
            }
            Link(ids[names.Length - 1], ids[0]);
            return ids;
        }

        // A passive halfway between two in neighbouring sectors, linking them.
        private static void Bridge(string id, string name, string a, string b, params StatModifier[] mods)
        {
            PassiveNode from = byId[a];
            PassiveNode to = byId[b];
            Add(id, name, from.Branch, false, (from.X + to.X) * 0.5f, (from.Y + to.Y) * 0.5f, mods);
            Chain(a, id, b);
        }

        private static void Chain(params string[] ids)
        {
            for (int k = 1; k < ids.Length; k++)
                Link(ids[k - 1], ids[k]);
        }

        private static StatModifier Mod(StatType stat, float value) => new StatModifier(stat, value);

        private static PassiveNode Add(string id, string name, PassiveBranch branch, bool notable, float x, float y, params StatModifier[] mods)
        {
            if (byId.ContainsKey(id))
                throw new InvalidOperationException("Duplicate passive id " + id);
            var node = new PassiveNode { Id = id, Name = name, Branch = branch, Notable = notable, X = x, Y = y, Mods = mods ?? new StatModifier[0] };
            nodes.Add(node);
            byId[id] = node;
            return node;
        }

        private static void Link(string a, string b)
        {
            PassiveNode na = byId[a];
            PassiveNode nb = byId[b];
            if (a == b || na.Links.Contains(b))
                return;
            na.Links.Add(b);
            nb.Links.Add(a);
        }
    }

    /// <summary>Which passives a character has taken, and the rules for taking them.</summary>
    public sealed class PassiveAllocation
    {
        private readonly HashSet<string> taken = new HashSet<string> { PassiveTree.OriginId };

        public event Action Changed;

        /// <summary>Passives taken, not counting the free origin.</summary>
        public int Spent => taken.Count - 1;

        public bool Has(string id) => taken.Contains(id);

        /// <summary>Every passive taken, the origin included.</summary>
        public IEnumerable<string> Taken => taken;

        public static int PointsForLevel(int level) => Math.Max(0, level - 1);

        public int Unspent(int level) => PointsForLevel(level) - Spent;

        /// <summary>Not taken yet, next to one that is, and a point to spend.</summary>
        public bool CanTake(string id, int level)
        {
            PassiveNode node = PassiveTree.Get(id);
            if (node == null || taken.Contains(id) || Unspent(level) <= 0)
                return false;
            foreach (string link in node.Links)
            {
                if (taken.Contains(link))
                    return true;
            }
            return false;
        }

        public bool Take(string id, int level)
        {
            if (!CanTake(id, level))
                return false;
            taken.Add(id);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Gives a passive back, if everything else taken stays connected to the origin without it
        /// (so only the ends of a path can be given back).
        /// </summary>
        public bool CanRefund(string id)
        {
            if (id == PassiveTree.OriginId || !taken.Contains(id))
                return false;

            var reached = new HashSet<string> { PassiveTree.OriginId };
            var open = new Stack<string>();
            open.Push(PassiveTree.OriginId);
            while (open.Count > 0)
            {
                foreach (string link in PassiveTree.Get(open.Pop()).Links)
                {
                    if (link != id && taken.Contains(link) && reached.Add(link))
                        open.Push(link);
                }
            }
            return reached.Count == taken.Count - 1;
        }

        public bool Refund(string id)
        {
            if (!CanRefund(id))
                return false;
            taken.Remove(id);
            Changed?.Invoke();
            return true;
        }

        public void ResetAll()
        {
            if (taken.Count <= 1)
                return;
            taken.Clear();
            taken.Add(PassiveTree.OriginId);
            Changed?.Invoke();
        }

        /// <summary>How many passives of this sector are taken.</summary>
        public int CountIn(PassiveBranch branch)
        {
            int count = 0;
            foreach (string id in taken)
            {
                PassiveNode node = PassiveTree.Get(id);
                if (node != null && node.Branch == branch && id != PassiveTree.OriginId)
                    count++;
            }
            return count;
        }

        /// <summary>Every stat line from the passives taken (devotion passives counted up by their sector).</summary>
        public List<StatModifier> Modifiers()
        {
            var list = new List<StatModifier>();
            foreach (string id in taken)
            {
                PassiveNode node = PassiveTree.Get(id);
                if (node == null)
                    continue;
                list.AddRange(node.Mods);
                if (node.PerBranchMods.Length > 0)
                {
                    int count = CountIn(node.Branch);
                    foreach (StatModifier m in node.PerBranchMods)
                        list.Add(new StatModifier(m.Stat, m.Value * count));
                }
            }
            return list;
        }
    }
}

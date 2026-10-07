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
        Zeal,      // between Wisdom and Might: fire, burning, regeneration, Rejuvenate
        Necromancy // out past Fire Ward, between Wisdom and Zeal: minions and Death Mark
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

        // Kept empty for compatibility with tooling; branch-count scaling has been removed.
        public readonly StatModifier[] PerBranchMods = new StatModifier[0];
    }

    /// <summary>
    /// The passive tree. From the centre, an inner ring of six gates: the three attributes (Might,
    /// Grace, Wisdom) and between each pair a hybrid (Fury, Storm, Zeal). Only the three attribute
    /// gates touch the centre, so a hybrid costs a step around the ring. Each sector has its own
    /// shape: Might a wheel, Fury a figure of eight, Grace one long winding loop, Storm a spiral,
    /// Wisdom a trident with crossbars, Zeal a star with a crown. Bridges join neighbouring sectors
    /// out at the rim, so a build can cut across instead of walking back to the centre.
    /// Keystones (diamonds) change broad mechanics. Short optional life branches let every build
    /// trade specialised damage for survival, without bonuses for filling an entire sector.
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
            Bridge("b_bloodrage", "Blood Rage", "m8", "f_berserk", BridgeReward("b_bloodrage"));
            Bridge("b_skirmisher", "Skirmisher", "f_frenzy", "g_hoarfrost", BridgeReward("b_skirmisher"));
            Bridge("b_arctic", "Arctic Archer", "g_deadeye", "s_stride", BridgeReward("b_arctic"));
            Bridge("b_surge", "Arcane Surge", "s_sp5", "w_l3", BridgeReward("b_surge"));
            Bridge("b_fireward", "Fire Ward", "w_r3", "z_t1", BridgeReward("b_fireward"));
            Bridge("b_ironfaith", "Iron Faith", "z_t4", "m5", BridgeReward("b_ironfaith"));

            // Out past Fire Ward: the summoner's cluster (needs the bridge, so it's a commitment).
            BuildNecromancy();
            BuildVenom();
            AddLifeBranches();
            AddCriticalBranches();
            AddTravelNodes();
            AddKeystoneApproaches();
            ExpandLayout();
        }

        // Necromancy: the first ward branches from Wisdom so a level-four summoner can invest in
        // survival. The rest of the cluster still costs several dedicated passive points.
        private static void BuildNecromancy()
        {
            Sector(PassiveBranch.Necromancy, 117f);
            N("n_ward", "Grave Ward", 1.6f, 0.3f, Mod(StatType.MinionLife, 10), Mod(StatType.MinionArmour, 60));
            N("n1", "Grave Whispers", 2.6f, 0f, Mod(StatType.MinionLife, 8), Mod(StatType.MinionArmour, 100), Mod(StatType.MinionResistances, 5));
            N("n2", "Bone Servants", 2.9f, -0.35f, Mod(StatType.MinionDamage, 7));
            N("n3", "Ossuary", 2.9f, 0.35f, Mod(StatType.MinionLife, 10), Mod(StatType.MinionArmour, 120), Mod(StatType.MinionResistances, 5));
            Nt("n_lord", "Lord of Bones", 3.2f, 0f, Mod(StatType.MinionLevels, 1), Mod(StatType.MinionLife, 20));
            N("n4", "Death's Grip", 3.2f, -0.62f, Mod(StatType.MarkEffect, 20), Mod(StatType.MinionDamage, 4));
            N("n5", "Pack Leader", 3.2f, 0.62f, Mod(StatType.MinionSpeed, 4), Mod(StatType.MinionDuration, 8));
            Chain("w2", "n_ward");
            Chain("b_fireward", "n1");
            Chain("n1", "n2", "n_lord", "n3", "n1");
            Chain("n2", "n4", "n_lord", "n5", "n3");

            Ks("k_legion", "Bone Legion", 3.6f, -0.28f, Mod(StatType.AdditionalMinions, 1));
            Chain("n_lord", "k_legion");
            Nt("k_soulbond", "Undying Host", 3.6f, 0.28f, Mod(StatType.MinionLife, 20), Mod(StatType.MinionArmour, 250), Mod(StatType.MinionResistances, 10));
            Chain("n_lord", "k_soulbond");
            Nt("k_herald", "Grave Dominion", 3.5f, -0.95f, Mod(StatType.MinionDamage, 16), Mod(StatType.MinionSpeed, 10));
            Chain("n4", "k_herald");
            Nt("k_spiritpact", "Spirit Pact", 3.5f, 0.95f, Mod(StatType.MinionDuration, 25), Mod(StatType.MinionSpeed, 12));
            Chain("n5", "k_spiritpact");
        }

        private static void BuildVenom()
        {
            // Fan the poison path below the bow loop. Keeping it in Grace's frame makes its
            // spacing from Deadeye, Volley and Phase Run easier to maintain.
            Sector(PassiveBranch.Grace, 330f);
            N("v1", "Toxic Blades", 2.81f, -0.17f, Mod(StatType.PoisonDamage, 10), Mod(StatType.DamageOverTime, 6));
            N("v2", "Venomous Wounds", 3.18f, -0.30f, Mod(StatType.PoisonPenetration, 6), Mod(StatType.PoisonDamage, 8));
            Nt("v3", "Path of Venom", 3.69f, -0.23f, Mod(StatType.PoisonDamage, 18), Mod(StatType.DamageOverTime, 12), Mod(StatType.PoisonPenetration, 8));
            Nt("k_viper", "Viper's Kiss", 4.16f, -0.23f, Mod(StatType.PoisonDamage, 14), Mod(StatType.DamageOverTime, 8), Mod(StatType.PoisonResistance, 10));
            Chain("g10", "v1", "v2", "v3", "k_viper");
            N("v4", "Caustic Blood", 3.16f, 0.09f, Mod(StatType.PoisonResistance, 12), Mod(StatType.LifeRegen, 2));
            N("v5", "Lingering Toxin", 3.55f, 0.15f, Mod(StatType.DamageOverTime, 10), Mod(StatType.PoisonPenetration, 5));
            Chain("g_deadeye", "v4", "v5", "v3");
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
            N("m3", "Iron Skin", 1.2f, -0.3f, Mod(StatType.Armour, 25), Mod(StatType.AvoidStun, 3));
            N("m4", "Thick Hide", 1.5f, -0.45f, Mod(StatType.MaxLife, 15), Mod(StatType.Armour, 15), Mod(StatType.AvoidStun, 4));
            Nt("m5", "Juggernaut", 1.85f, -0.55f, Mod(StatType.MaxLife, 30), Mod(StatType.Armour, 50), Mod(StatType.BlockChance, 5), Mod(StatType.AvoidStun, 8));
            Chain("m2", "m3", "m4", "m5");

            // Right arm: offence.
            N("m6", "Heavy Hands", 1.2f, 0.3f, Mod(StatType.PhysicalDamage, 1), Mod(StatType.ArmourPenetration, 3));
            N("m7", "Power", 1.5f, 0.45f, Mod(StatType.Strength, 8));
            Nt("m8", "Brute Force", 1.85f, 0.55f, Mod(StatType.PhysicalDamage, 6), Mod(StatType.Strength, 10), Mod(StatType.MeleeRange, 15), Mod(StatType.ArmourPenetration, 8));
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

            Ks("k_unbreakable", "Iron Reflexes", 3.3f, 0f, Mod(StatType.IronReflexes, 1));
            Chain(wheel[3], "k_unbreakable");

            Ks("k_executioner", "Executioner", 2.4f, -0.8f, Mod(StatType.CullingStrike, 1));
            Chain("m5", "k_executioner");
            Ks("k_bloodthirst", "Blood Magic", 2.4f, 0.8f, Mod(StatType.BloodMagic, 1), Mod(StatType.MoreLife, 10));
            Chain("m8", "k_bloodthirst");
        }

        // Fury (down): two diamonds sharing a point, a figure of eight, with a keystone off each end.
        private static void BuildFury()
        {
            Sector(PassiveBranch.Fury, 270f);
            N("f1", "Battle Rhythm", 0.55f, 0f, Mod(StatType.AttackSpeed, 3));
            N("f2", "Sharpened Edges", 0.9f, 0f, Mod(StatType.CriticalChance, 20));
            Chain("f1", "f2");
            LifeSpur("f_life", "Battle Scars", "f2");

            N("f_a1", "Keen Eye", 1.2f, -0.3f, Mod(StatType.CriticalChance, 20));
            N("f_a2", "Fervour", 1.2f, 0.3f, Mod(StatType.AttackDamage, 8));
            Nt("f_dancer", "Bladedancer", 1.5f, 0f, Mod(StatType.AttackSpeed, 8), Mod(StatType.CriticalChance, 40));
            Chain("f2", "f_a1", "f_dancer", "f_a2", "f2");

            // Early sustain for both melee and bow attacks, off the attack-damage arm.
            N("f_recovery", "Vital Strikes", 1.2f, 0.7f, Mod(StatType.LifeOnAttackHit, 2));
            Nt("f_feast", "Feast of Blades", 1.55f, 0.9f, Mod(StatType.LifeOnAttackHit, 4));
            Chain("f_a2", "f_recovery", "f_feast");

            N("f_b1", "Assassin's Mark", 1.85f, -0.35f, Mod(StatType.CriticalMultiplier, 15));
            N("f_b2", "Momentum", 1.85f, 0.35f, Mod(StatType.OnslaughtOnKill, 10));
            Nt("f_precision", "Deadly Precision", 2.2f, 0f, Mod(StatType.CriticalChance, 60), Mod(StatType.CriticalMultiplier, 25), Mod(StatType.ArmourPenetration, 5));
            Chain("f_dancer", "f_b1", "f_precision", "f_b2", "f_dancer");

            Nt("k_stormblade", "Battle Focus", 2.65f, 0f, Mod(StatType.CriticalChance, 50), Mod(StatType.AttackDamage, 12));
            Chain("f_precision", "k_stormblade");

            Nt("f_berserk", "Berserker", 2.25f, -0.7f, Mod(StatType.AttackDamage, 16), Mod(StatType.LifeLeech, 1));
            Chain("f_b1", "f_berserk");
            Nt("k_pain", "Lasting Resolve", 2.65f, -1.0f, Mod(StatType.AttackDamage, 12), Mod(StatType.AvoidStun, 15));
            Chain("f_berserk", "k_pain");

            Nt("f_frenzy", "Frenzy", 2.25f, 0.7f, Mod(StatType.OnslaughtOnKill, 15), Mod(StatType.AttackSpeed, 6));
            Chain("f_b2", "f_frenzy");
            Nt("k_relentless", "Relentless", 2.65f, 1.0f, Mod(StatType.OnslaughtOnKill, 20), Mod(StatType.MovementSpeed, 5));
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
            LifeSpur("g_life", "Hardy", "g2");

            N("g3", "Light Step", 1.2f, -0.25f, Mod(StatType.MovementSpeed, 4));
            N("g4", "Dodge", 1.5f, -0.35f, Mod(StatType.Evasion, 30));
            Nt("g5", "Wind Dancer", 1.8f, -0.2f, Mod(StatType.MovementSpeed, 8), Mod(StatType.Evasion, 60));
            N("g9", "Fleet", 2.05f, 0.1f, Mod(StatType.MovementSpeed, 3), Mod(StatType.Evasion, 15));
            N("g10", "Fletching", 2.3f, 0.35f, Mod(StatType.AttackDamage, 6));
            Chain("g2", "g3", "g4", "g5", "g9", "g10");

            N("g6", "Quick Hands", 1.2f, 0.3f, Mod(StatType.AttackSpeed, 5));
            N("g7", "Precision", 1.5f, 0.45f, Mod(StatType.PhysicalDamage, 1), Mod(StatType.Dexterity, 4));
            Nt("g8", "Flurry", 1.8f, 0.55f, Mod(StatType.AttackSpeed, 12), Mod(StatType.PhysicalDamage, 3));
            Chain("g2", "g6", "g7", "g8", "g10");

            Nt("g_deadeye", "Deadeye", 2.6f, 0.3f, Mod(StatType.BowDamage, 18), Mod(StatType.CriticalChance, 50));
            Chain("g10", "g_deadeye");
            Ks("k_volley", "Volley", 2.83f, 0.69f, Mod(StatType.AdditionalArrows, 1));
            Chain("g_deadeye", "k_volley");
            N("g_shaft", "True Shaft", 3.3f, 0.62f, Mod(StatType.BowDamage, 6));
            Nt("g_longshot", "Longshot", 3.6f, 0.78f, Mod(StatType.BowDamage, 18), Mod(StatType.CriticalMultiplier, 15));
            Chain("g_deadeye", "g_shaft", "g_longshot");

            Nt("g_phase", "Phase Run", 2.4f, -0.2f, Mod(StatType.MovementSpeed, 6), Mod(StatType.OnslaughtOnKill, 10), Mod(StatType.Evasion, 40));
            Chain("g9", "g_phase");

            Nt("g_hoarfrost", "Hoarfrost", 2.1f, -0.6f, Mod(StatType.ChillOnHit, 8), Mod(StatType.DamageVsChilled, 10));
            Chain("g5", "g_hoarfrost");
            Ks("k_frostbite", "Point Blank", 2.5f, -0.9f, Mod(StatType.PointBlank, 1));
            Chain("g_hoarfrost", "k_frostbite");
        }

        // Storm (up-right): a spiral winding out round the Eye of the Storm.
        private static void BuildStorm()
        {
            Sector(PassiveBranch.Storm, 30f);
            N("s1", "Quickened Mind", 0.55f, 0f, Mod(StatType.CastSpeed, 4));
            N("s2", "Static", 0.9f, 0f, Mod(StatType.LightningDamage, 8), Mod(StatType.LightningPenetration, 4));
            Chain("s1", "s2");
            LifeSpur("s_life", "Grounded Body", "s2");

            N("s_sp1", "Spark", 1.3f, 0f, Mod(StatType.LightningDamage, 8));
            N("s_sp2", "Rime", 1.49f, -0.45f, Mod(StatType.ColdDamage, 8), Mod(StatType.DamageVsChilled, 6), Mod(StatType.ColdPenetration, 4));
            N("s_sp3", "Overcharge", 2.05f, -0.51f, Mod(StatType.ShockChance, 10));
            Nt("s_conductor", "Conductor", 2.41f, 0f, Mod(StatType.LightningDamage, 15), Mod(StatType.AdditionalChains, 1), Mod(StatType.LightningPenetration, 8));
            N("s_sp5", "Haste", 2.12f, 0.63f, Mod(StatType.CastSpeed, 5));
            N("s_sp6", "Flow", 1.4f, 0.69f, Mod(StatType.CooldownRecovery, 6));
            Nt("s_eye", "Eye of the Storm", 1.8f, 0.05f, Mod(StatType.CastSpeed, 8), Mod(StatType.CooldownRecovery, 8), Mod(StatType.ManaRegen, 20));
            Chain("s2", "s_sp1", "s_sp2", "s_sp3", "s_conductor", "s_sp5", "s_sp6", "s_eye");

            Nt("k_thunderlord", "Thunderlord", 2.85f, 0f, Mod(StatType.ShockChance, 15), Mod(StatType.LightningDamage, 18));
            Chain("s_conductor", "k_thunderlord");

            Nt("s_stride", "Frozen Stride", 2.3f, -0.95f, Mod(StatType.ColdDamage, 10), Mod(StatType.MovementSpeed, 5), Mod(StatType.CooldownRecovery, 5), Mod(StatType.ColdPenetration, 6));
            Chain("s_sp3", "s_stride");
            Nt("k_glacial", "Winter's Reach", 2.75f, -1.15f, Mod(StatType.ColdDamage, 16), Mod(StatType.ColdPenetration, 8));
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
            LifeSpur("w_life", "Sound Body", "w2");

            // Mind: mana, ending in Mind over Matter.
            N("w4", "Deep Well", 1.5f, -0.45f, Mod(StatType.MaxMana, 20));
            N("w_l2", "Clarity", 1.85f, -0.5f, Mod(StatType.ManaRegen, 15));
            Nt("w_l3", "Arcane Mind", 2.2f, -0.5f, Mod(StatType.Intelligence, 15), Mod(StatType.MaxMana, 30), Mod(StatType.IncreasedMana, 10));
            Chain("w3", "w4", "w_l2", "w_l3");
            Ks("k_mom", "Mind over Matter", 2.65f, -0.6f, Mod(StatType.ManaAbsorb, 30));
            Chain("w_l3", "k_mom");

            // Power: spell damage, ending in a projectile keystone shared with bow builds.
            N("w_m1", "Potency", 1.55f, 0f, Mod(StatType.SpellDamage, 8));
            N("w_m2", "Sorcery", 1.9f, 0f, Mod(StatType.SpellDamage, 6), Mod(StatType.Intelligence, 4));
            Nt("w_archmage", "Archmage", 2.25f, 0f, Mod(StatType.SpellDamage, 15), Mod(StatType.AreaOfEffect, 12), Mod(StatType.ElementalPenetration, 5));
            Chain("w3", "w_m1", "w_m2", "w_archmage");
            Ks("k_twincast", "Convergence", 2.7f, 0f, Mod(StatType.AdditionalProjectiles, 1));
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
            N("z3", "Hearth", 1.25f, 0f, Mod(StatType.FireDamage, 8), Mod(StatType.FirePenetration, 4));
            Nt("z_hub", "Sacred Flame", 1.65f, 0f, Mod(StatType.FireDamage, 18), Mod(StatType.IgniteChance, 15));
            Chain("z1", "z2", "z3", "z_hub");
            LifeSpur("z_life", "Warm Blood", "z2");

            N("z_s1", "Smoulder", 1.93f, -0.48f, Mod(StatType.IgniteChance, 8));
            N("z_s2", "Combustion", 2.17f, -0.19f, Mod(StatType.FireDamage, 6));
            N("z_s3", "Renewal", 2.17f, 0.19f, Mod(StatType.PercentLifeRegen, 0.3f));
            N("z_s4", "Fervent Heart", 1.93f, 0.48f, Mod(StatType.MaxLife, 15));
            Nt("z_t1", "Wildfire", 2.15f, -0.9f, Mod(StatType.IgniteChance, 15), Mod(StatType.FireDamage, 12), Mod(StatType.FirePenetration, 8));
            Nt("z_t2", "Funeral Pyre", 2.6f, -0.34f, Mod(StatType.FireDamage, 18), Mod(StatType.IgniteChance, 10));
            Nt("z_t3", "Lifeblood", 2.6f, 0.34f, Mod(StatType.PercentLifeRegen, 0.6f), Mod(StatType.IncreasedLife, 6));
            Nt("z_t4", "Zealot's Vigour", 2.15f, 0.9f, Mod(StatType.LifeRegen, 4), Mod(StatType.MaxLife, 25));
            Chain("z_hub", "z_s1", "z_t1");
            Chain("z_hub", "z_s2", "z_t2");
            Chain("z_hub", "z_s3", "z_t3");
            Chain("z_hub", "z_s4", "z_t4");
            Chain("z_t1", "z_t2", "z_t3", "z_t4");

            Ks("k_inferno", "Pyre", 3.05f, -0.45f, Mod(StatType.ExplodeOnKill, 20));
            Chain("z_t2", "k_inferno");
            Nt("k_secondwind", "Renewed Vigour", 3.05f, 0.45f, Mod(StatType.PercentLifeRegen, 0.6f), Mod(StatType.CooldownRecovery, 8));
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

        // A lone life passive just off a sector's second step, in the gap on its clockwise side:
        // every sector offers a little life without walking into a tanky branch for it.
        private static void LifeSpur(string id, string name, string from)
        {
            N(id, name, 0.85f, -0.45f, Mod(StatType.IncreasedLife, 3));
            Nt(id + "_heart", name, 0.85f, -0.78f, Mod(StatType.IncreasedLife, 6));
            Chain(from, id, id + "_heart");
        }

        private static void LifeBranch(string id, string name, string from, float u, float v, bool pair)
        {
            if (pair)
            {
                N(id, name, u, v, Mod(StatType.IncreasedLife, 3));
                Nt(id + "_heart", name, u, v + 0.33f, Mod(StatType.IncreasedLife, 6));
                Chain(from, id, id + "_heart");
            }
            else
            {
                Nt(id, name, u, v, Mod(StatType.IncreasedLife, 8));
                Chain(from, id);
            }
        }

        private static void AddLifeBranches()
        {
            Sector(PassiveBranch.Might, 210f);
            LifeBranch("m_life", "Stout Heart", "m2", 0.88f, -0.75f, true);
            LifeBranch("m_life_guard", "Steel Heart", "m5", 1.7f, -1.05f, false);
            LifeBranch("m_life_outer", "Warrior's Heart", "m_bloodbath", 2.7f, 0.85f, true);
            Sector(PassiveBranch.Fury, 270f);
            LifeBranch("f_life_mid", "Battle Hardened", "f_dancer", 1.5f, 0.7f, false);
            LifeBranch("f_life_outer", "Survivor", "f_precision", 2.45f, 0.45f, true);
            Sector(PassiveBranch.Grace, 330f);
            LifeBranch("g_life_mid", "Surefooted", "g5", 1.75f, -1.2f, false);
            LifeBranch("g_life_outer", "Hunter's Heart", "g_longshot", 3.75f, 1.2f, true);
            LifeBranch("v_life", "Venom Hardened", "v2", 3.12f, -0.85f, false);
            Sector(PassiveBranch.Storm, 30f);
            LifeBranch("s_life_mid", "Storm Shelter", "s_sp2", 1.5f, -1.15f, true);
            LifeBranch("s_life_outer", "Weathered", "s_sp5", 2.5f, 1.0f, false);
            Sector(PassiveBranch.Wisdom, 90f);
            LifeBranch("w_life_mid", "Vital Mind", "w4", 1.5f, -1.1f, false);
            LifeBranch("w_life_outer", "Living Ward", "w_r3", 2.4f, 1.0f, true);
            Sector(PassiveBranch.Zeal, 150f);
            LifeBranch("z_life_mid", "Ember Guard", "z_hub", 1.5f, 0.8f, false);
            LifeBranch("z_life_outer", "Enduring Flame", "z_t4", 2.35f, 1.45f, true);
            Sector(PassiveBranch.Necromancy, 117f);
            LifeBranch("n_life_inner", "Living Keeper", "n_ward", 1.85f, 0.5f, false);
            LifeBranch("n_life_mid", "Grave Survivor", "n1", 2.55f, -0.8f, true);
            LifeBranch("n_life_outer", "Deathless Heart", "n5", 3.15f, 1.35f, false);
        }

        private static void AddCriticalBranches()
        {
            Sector(PassiveBranch.Might, 210f);
            N("m_crit", "Sharp Intent", 1.55f, 0.95f, Mod(StatType.CriticalChance, 20));
            Nt("m_crit_master", "Lethal Blows", 1.85f, 1.25f, Mod(StatType.CriticalChance, 50), Mod(StatType.CriticalMultiplier, 20));
            Chain("m7", "m_crit", "m_crit_master");
            Sector(PassiveBranch.Grace, 330f);
            N("g_crit", "Steady Aim", 2.05f, 0.85f, Mod(StatType.CriticalChance, 20));
            Nt("g_crit_master", "Killing Shot", 2.25f, 1.15f, Mod(StatType.CriticalChance, 50), Mod(StatType.CriticalMultiplier, 20));
            Chain("g8", "g_crit", "g_crit_master");
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

        // Crossing sectors and reaching the outer notables should cost points. Saved allocations
        // are restored separately so an existing character keeps its already bought passives.
        private static void AddTravelNodes()
        {
            foreach (string bridge in new[] { "b_bloodrage", "b_skirmisher", "b_arctic", "b_surge", "b_fireward", "b_ironfaith" })
            {
                byId[bridge].Mods = new[] { BridgeReward(bridge) };
                string[] ends = byId[bridge].Links.ToArray();
                for (int k = 0; k < ends.Length; k++)
                    InsertTravel(bridge, ends[k], 1);
            }

            foreach (string[] edge in new[]
            {
                new[] { "m4", "m5" }, new[] { "m7", "m8" },
                new[] { "f_b1", "f_berserk" }, new[] { "f_b2", "f_frenzy" },
                new[] { "g10", "g_deadeye" }, new[] { "g9", "g_phase" },
                new[] { "s_sp3", "s_conductor" }, new[] { "s_sp3", "s_stride" },
                new[] { "w_l2", "w_l3" }, new[] { "w7", "w_r3" },
                new[] { "z_s1", "z_t1" }, new[] { "z_s2", "z_t2" },
                new[] { "z_s3", "z_t3" }, new[] { "z_s4", "z_t4" },
                new[] { "n2", "n_lord" }, new[] { "v2", "v3" }
            })
                InsertTravel(edge[0], edge[1], 1);
        }

        // One travel point normally; two for exceptional rewards. Never longer corridors.
        private static void AddKeystoneApproaches()
        {
            foreach (PassiveNode keystone in nodes.ToArray())
            {
                if (!keystone.Keystone)
                    continue;
                if (keystone.Links.Count != 1)
                    throw new InvalidOperationException("Keystone must have one approach: " + keystone.Id);

                PassiveNode from = byId[keystone.Links[0]];
                float dx = keystone.X - from.X;
                float dy = keystone.Y - from.Y;
                float length = (float)Math.Sqrt(dx * dx + dy * dy);
                if (length < 0.001f)
                    throw new InvalidOperationException("Keystone overlaps its approach: " + keystone.Id);

                int count = keystone.Id == "k_bloodthirst" || keystone.Id == "k_mom" ||
                    keystone.Id == "k_twincast" || keystone.Id == "k_inferno" ? 2 : 1;
                float approachLength = count == 2 ? 0.95f : 0.65f;
                keystone.X = from.X + dx / length * approachLength;
                keystone.Y = from.Y + dy / length * approachLength;
                InsertTravel(from.Id, keystone.Id, count);
            }
        }

        // Spread every existing node, including the new travel points, without changing the
        // graph or saved passive IDs. The UI keeps node glyphs the same size at a given zoom.
        private static void ExpandLayout()
        {
            const float spacing = 2.2f;
            foreach (PassiveNode node in nodes)
            {
                node.X *= spacing;
                node.Y *= spacing;
            }
        }

        private static void InsertTravel(string a, string b, int count)
        {
            PassiveNode from = byId[a], to = byId[b];
            if (!from.Links.Remove(b) || !to.Links.Remove(a))
                throw new InvalidOperationException("Missing travel link " + a + " - " + b);

            // Pick once per edge so a longer approach never changes reward halfway through.
            StatModifier reward = TravelReward(a, b, from);
            string previous = a;
            for (int k = 1; k <= count; k++)
            {
                float t = (float)k / (count + 1);
                string id = "travel_" + a + "_" + b + "_" + k;
                Add(id, "Travel", from.Branch, false,
                    from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t, reward);
                Link(previous, id);
                previous = id;
            }
            Link(previous, b);
        }

        private static StatModifier BridgeReward(string bridge)
        {
            switch (bridge)
            {
                case "b_bloodrage": return Mod(StatType.AttackDamage, 3);
                case "b_skirmisher": return Mod(StatType.Dexterity, 3);
                case "b_arctic": return Mod(StatType.ColdDamage, 3);
                case "b_surge": return Mod(StatType.ManaRegen, 5);
                case "b_fireward": return Mod(StatType.FireResistance, 4);
                default: return Mod(StatType.Armour, 8);
            }
        }

        private static StatModifier TravelReward(string a, string b, PassiveNode from)
        {
            // Both arms and the centre share exactly one reward (three points per crossing).
            string bridge = a.StartsWith("b_") ? a : b.StartsWith("b_") ? b : null;
            if (bridge != null) return BridgeReward(bridge);

            // Every point on a keystone approach shares one modest supporting stat.
            switch (b)
            {
                case "k_legion": return Mod(StatType.MinionLife, 4);
                case "k_herald": return Mod(StatType.MarkEffect, 3);
                case "k_viper": return Mod(StatType.PoisonDamage, 3);
                case "k_frostbite": return Mod(StatType.Dexterity, 3);
                case "k_thunderlord": return Mod(StatType.ShockChance, 3);
                case "k_twincast": return Mod(StatType.SpellDamage, 3);
                case "k_bloodthirst": return Mod(StatType.IncreasedLife, 3);
                case "k_mom": return Mod(StatType.MaxMana, 5);
                case "k_unbreakable": return Mod(StatType.Armour, 8);
                case "k_volley": return Mod(StatType.BowDamage, 3);
                case "k_inferno": return Mod(StatType.FireDamage, 3);
                case "k_secondwind": return Mod(StatType.CooldownRecovery, 2);
            }

            // Single steps toward notables hint at the reward without equalling a normal node.
            switch (b)
            {
                case "m5": return Mod(StatType.Armour, 8);
                case "f_berserk": return Mod(StatType.AttackDamage, 3);
                case "g_deadeye": return Mod(StatType.BowDamage, 1);
                case "s_conductor": return Mod(StatType.LightningDamage, 3);
                case "w_l3": return Mod(StatType.MaxMana, 5);
                case "z_t1": return Mod(StatType.FireDamage, 3);
                case "n_lord": return Mod(StatType.MinionDamage, 2);
            }

            // The remaining routes keep an attribute reward, now worth three per point.
            StatType attribute = from.Branch == PassiveBranch.Might || from.Branch == PassiveBranch.Zeal
                ? StatType.Strength : from.Branch == PassiveBranch.Grace || from.Branch == PassiveBranch.Fury
                ? StatType.Dexterity : StatType.Intelligence;
            return Mod(attribute, 3);
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
            var node = new PassiveNode { Id = id, Name = notable || id == OriginId ? name : null, Branch = branch, Notable = notable, X = x, Y = y, Mods = mods ?? new StatModifier[0] };
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

        /// <summary>Restore previously bought nodes after a tree layout change, without charging
        /// the character for new travel points between them.</summary>
        public void RestoreSaved(IEnumerable<string> ids, int level)
        {
            foreach (string id in ids)
            {
                if (id != PassiveTree.OriginId && PassiveTree.Get(id) != null && !taken.Contains(id) && Spent < PointsForLevel(level))
                    taken.Add(id);
            }
            Changed?.Invoke();
        }

        /// <summary>
        /// Gives a passive back, if everything else taken stays connected to the origin without it
        /// (so only the ends of a path can be given back).
        /// </summary>
        public bool CanRefund(string id)
        {
            if (id == PassiveTree.OriginId || !taken.Contains(id))
                return false;

            HashSet<string> connectedBefore = ReachableFromOrigin(null);
            HashSet<string> connectedAfter = ReachableFromOrigin(id);
            foreach (string node in connectedBefore)
                if (node != id && !connectedAfter.Contains(node))
                    return false;
            return true;
        }

        private HashSet<string> ReachableFromOrigin(string excluded)
        {
            var reached = new HashSet<string> { PassiveTree.OriginId };
            var open = new Stack<string>();
            open.Push(PassiveTree.OriginId);
            while (open.Count > 0)
            {
                foreach (string link in PassiveTree.Get(open.Pop()).Links)
                {
                    if (link != excluded && taken.Contains(link) && reached.Add(link))
                        open.Push(link);
                }
            }
            return reached;
        }

        public bool Refund(string id)
        {
            if (!CanRefund(id))
                return false;
            taken.Remove(id);
            Changed?.Invoke();
            return true;
        }

        public void ReplaceWith(IEnumerable<string> ids)
        {
            var replacement = new HashSet<string>(ids);
            taken.Clear();
            taken.Add(PassiveTree.OriginId);
            foreach (string id in replacement)
                if (PassiveTree.Get(id) != null) taken.Add(id);
            Changed?.Invoke();
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

        /// <summary>Every stat line from the passives taken.</summary>
        public List<StatModifier> Modifiers()
        {
            var list = new List<StatModifier>();
            foreach (string id in taken)
            {
                PassiveNode node = PassiveTree.Get(id);
                if (node == null)
                    continue;
                list.AddRange(node.Mods);
            }
            return list;
        }
    }
}

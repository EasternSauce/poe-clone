using System.Collections.Generic;
using System.Text;
using PoeClone.Inventory;
using PoeClone.Skills;

namespace PoeClone.World
{
    /// <summary>
    /// One build the debug gauntlet can play: which weapons and armour it wears, which skills it
    /// is about, how much it values each stat when picking between rolled rares, the passives it
    /// heads for (in order) and the uniques it may swap in. Edit a build here and rerun the
    /// gauntlet to compare; every result carries the build's <see cref="Fingerprint"/>.
    /// </summary>
    public sealed class GauntletBuild
    {
        public string Id;
        public string Name;
        public string Summary;
        /// <summary>Fights in melee range: values defence about as much as damage.</summary>
        public bool Melee;

        /// <summary>Base lines (see ItemGenerator) the main hand rolls from, and the off hand with them.</summary>
        public string[] MainHand;
        public string[] OffHand = new string[0];
        /// <summary>The attack spell its staves carry (null: random, or not a staff).</summary>
        public StatType? StaffSpell;
        /// <summary>The attribute its armour, belt and amulet lean on: "str", "dex", "int" or "hyb".</summary>
        public string Armour;

        /// <summary>The skills the build is about: at least one is guaranteed on the gear.</summary>
        public StatType[] CoreSkills;
        /// <summary>Skills worth having alongside.</summary>
        public StatType[] SupportSkills = new StatType[0];
        /// <summary>The bow main skill to roll for this build.</summary>
        public SkillId? BowSkill;
        /// <summary>Weapon tree allocation order; each selected weapon spends its own rank budget.</summary>
        public string[] WeaponNodes = { "v2_0_0", "v2_0_1", "v2_0_2", "v2_0_3", "v2_0_4", "v2_0_5", "v2_1_0", "v2_1_1", "v2_1_2", "v2_1_3" };

        /// <summary>Stat weights for picking rares, relative to <see cref="GauntletLoadout.UnitValue"/>.</summary>
        public Dictionary<StatType, float> Weights = new Dictionary<StatType, float>();
        /// <summary>Passives to head for, most wanted first; leftover points follow <see cref="Weights"/>.</summary>
        public string[] Passives;
        /// <summary>
        /// Uniques by name, most wanted first; "Name@21" only from level 21. A "!" marks one the
        /// build is made around (always worn); the others are worn only where they beat the best
        /// rolled rare. At most two are worn.
        /// </summary>
        public string[] Uniques = new string[0];

        public float Weight(StatType stat) => Weights.TryGetValue(stat, out float w) ? w : 0f;

        /// <summary>A short hash of everything above, so results from an edited build are told apart.</summary>
        public string Fingerprint
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append(string.Join(",", MainHand)).Append('|').Append(string.Join(",", OffHand)).Append('|')
                  .Append(StaffSpell).Append('|').Append(Armour).Append('|').Append(string.Join(",", CoreSkills)).Append('|')
                  .Append(string.Join(",", SupportSkills)).Append('|').Append(string.Join(",", Passives)).Append('|')
                  .Append(string.Join(",", Uniques)).Append('|').Append("weapon-trees-v2|").Append(string.Join(",", WeaponNodes)).Append('|');
                var keys = new List<StatType>(Weights.Keys);
                keys.Sort();
                foreach (StatType k in keys)
                    sb.Append(k).Append('=').Append(Weights[k].ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(';');
                unchecked
                {
                    uint hash = 2166136261;
                    foreach (char c in sb.ToString())
                        hash = (hash ^ c) * 16777619;
                    return hash.ToString("x8");
                }
            }
        }
    }

    public static class GauntletBuilds
    {
        /// <summary>The character levels a gauntlet can start at.</summary>
        public static readonly int[] Levels = { 1, 7, 14, 21, 28, 34 };

        /// <summary>The builds the automated run plays by default.</summary>
        public static readonly string[] AutomatedDefaults = { "pyromancer", "volley_archer", "mace_juggernaut", "necromancer" };

        public static GauntletBuild Get(string id)
        {
            foreach (GauntletBuild b in All)
                if (b.Id == id) return b;
            return null;
        }

        // Shared stat profiles. Melee stands in the enemy's reach, so it buys real defence;
        // casters and archers lean on mana and some life.
        private static Dictionary<StatType, float> Caster(params object[] extra) => Profile(new object[]
        {
            StatType.MaxLife, 1f, StatType.IncreasedLife, 1f, StatType.MaxMana, 1f, StatType.IncreasedMana, 0.8f,
            StatType.ManaRegen, 1f, StatType.ManaCostReduction, 1f, StatType.Intelligence, 0.8f,
            StatType.FireResistance, 0.5f, StatType.ColdResistance, 0.5f, StatType.LightningResistance, 0.5f,
            StatType.Evasion, 0.3f, StatType.CastSpeed, 1.4f, StatType.SpellDamage, 1.6f, StatType.Damage, 1.2f,
            StatType.MovementSpeed, 0.6f, StatType.AllSpellLevels, 1.5f
        }, extra);

        private static Dictionary<StatType, float> Archer(params object[] extra) => Profile(new object[]
        {
            StatType.MaxLife, 1.1f, StatType.IncreasedLife, 1.1f, StatType.Dexterity, 0.8f, StatType.Evasion, 0.7f,
            StatType.FireResistance, 0.5f, StatType.ColdResistance, 0.5f, StatType.LightningResistance, 0.5f,
            StatType.MaxMana, 0.5f, StatType.ManaRegen, 0.5f, StatType.ManaCostReduction, 0.5f,
            StatType.PhysicalDamage, 1.6f, StatType.AttackSpeed, 1.5f, StatType.AttackDamage, 1.3f, StatType.BowDamage, 1.5f,
            StatType.Damage, 1.1f, StatType.CriticalChance, 0.8f, StatType.CriticalMultiplier, 0.8f,
            StatType.AdditionalArrows, 2f, StatType.MovementSpeed, 0.7f, StatType.LifeOnAttackHit, 0.6f
        }, extra);

        private static Dictionary<StatType, float> Melee(params object[] extra) => Profile(new object[]
        {
            StatType.MaxLife, 1.7f, StatType.IncreasedLife, 1.7f, StatType.Armour, 1f, StatType.Strength, 0.8f,
            StatType.FireResistance, 0.9f, StatType.ColdResistance, 0.9f, StatType.LightningResistance, 0.9f,
            StatType.BlockChance, 0.8f, StatType.LifeLeech, 1.4f, StatType.LifeOnAttackHit, 1.2f, StatType.LifeRegen, 0.8f,
            StatType.PercentLifeRegen, 1f, StatType.LifeOnKill, 0.6f, StatType.AvoidStun, 0.5f, StatType.DamageTaken, 1.6f,
            StatType.MaxMana, 0.4f, StatType.ManaRegen, 0.4f, StatType.ManaCostReduction, 0.5f,
            StatType.PhysicalDamage, 1.2f, StatType.AttackSpeed, 1.1f, StatType.AttackDamage, 1f, StatType.Damage, 0.9f,
            StatType.MeleeRange, 0.6f, StatType.AreaOfEffect, 0.6f, StatType.MovementSpeed, 0.5f
        }, extra);

        private static Dictionary<StatType, float> Summoner(params object[] extra) => Profile(new object[]
        {
            StatType.MaxLife, 1.2f, StatType.IncreasedLife, 1.2f, StatType.MaxMana, 1f, StatType.IncreasedMana, 0.8f,
            StatType.ManaRegen, 1f, StatType.ManaCostReduction, 1.2f, StatType.Intelligence, 0.6f,
            StatType.FireResistance, 0.5f, StatType.ColdResistance, 0.5f, StatType.LightningResistance, 0.5f,
            StatType.Evasion, 0.3f, StatType.CastSpeed, 0.6f, StatType.MovementSpeed, 0.6f,
            StatType.MinionDamage, 1.8f, StatType.MinionLife, 1.3f, StatType.MinionSpeed, 1.2f, StatType.MinionLevels, 2.5f,
            StatType.AdditionalMinions, 2.5f, StatType.MinionArmour, 0.5f, StatType.MinionResistances, 0.5f,
            StatType.MarkEffect, 0.8f, StatType.SoulBond, 0.6f, StatType.BoneArmour, 0.6f
        }, extra);

        private static Dictionary<StatType, float> Profile(object[] shared, object[] extra)
        {
            var d = new Dictionary<StatType, float>();
            for (int k = 0; k + 1 < shared.Length; k += 2)
                d[(StatType)shared[k]] = (float)shared[k + 1];
            for (int k = 0; k + 1 < extra.Length; k += 2)
                d[(StatType)extra[k]] = (float)extra[k + 1];
            return d;
        }

        public static readonly GauntletBuild[] All =
        {
            // ---- Conventional
            new GauntletBuild
            {
                Id = "lightning_caster", Name = "Lightning Caster",
                Summary = "Chain Lightning damage caster: Conductor, Thunderlord, Eye of the Storm and Archmage. Mantle of the Still Mind adds mana as damage; Conductor's Reach competes with rare staves.",
                MainHand = new[] { "staff" }, StaffSpell = StatType.GrantChainLightning, Armour = "int",
                CoreSkills = new[] { StatType.GrantChainLightning },
                Weights = Caster(StatType.SpellDamage, 2f, StatType.CastSpeed, 2f, StatType.LightningDamage, 2f,
                    StatType.MaxMana, 2f, StatType.IncreasedMana, 2f, StatType.ManaRegen, 1.4f,
                    StatType.LightningSpellLevels, 3f, StatType.AllSpellLevels, 3f, StatType.LightningPenetration, 1.5f,
                    StatType.ElementalPenetration, 1.5f, StatType.ShockChance, 1.5f, StatType.AdditionalChains, 0.5f,
                    StatType.MoreLightningDamage, 2.5f, StatType.MoreSpellDamage, 2.5f, StatType.ManaAsDamage, 2f,
                    StatType.SpellEcho, 3f, StatType.PercentManaRegen, 1.5f),
                Passives = new[] { "s_conductor", "k_thunderlord", "s_eye", "w_archmage", "w_l3", "w_life_heart", "s_life_mid_heart" },
                Uniques = new[] { "!Mantle of the Still Mind", "Conductor's Reach", "Pendant of the Fleeting Thought", "Stormcaller's Circlet" }
            },
            new GauntletBuild
            {
                Id = "pyromancer", Name = "Pyromancer",
                Summary = "Fire Bolt staff caster: Wisdom and Zeal, the Pyre keystone. The Unfinished Sentence from level 25.",
                MainHand = new[] { "staff" }, StaffSpell = StatType.GrantFireBolt, Armour = "int",
                CoreSkills = new[] { StatType.GrantFireBolt }, SupportSkills = new[] { StatType.GrantFrostNova, StatType.GrantWarCry },
                Weights = Caster(StatType.FireDamage, 1.6f, StatType.FireSpellLevels, 2f, StatType.IgniteChance, 0.6f,
                    StatType.FirePenetration, 1.2f, StatType.ElementalPenetration, 1.2f, StatType.ExplodeOnKill, 0.4f, StatType.MoreFireDamage, 2f,
                    StatType.SpellEcho, 3f),
                Passives = new[] { "z_hub", "w_m2", "w_archmage", "z_t2", "k_inferno", "z_t1", "w_l3", "s_eye", "z_life_heart", "w_life_heart", "k_twincast" },
                Uniques = new[] { "The Unfinished Sentence", "Emberheart", "Elder's Charm" }
            },
            new GauntletBuild
            {
                Id = "frost_caster", Name = "Frost Caster",
                Summary = "Ice Shard and Frost Nova: Storm and Wisdom. Frostglass Bough (shatter) from level 17.",
                MainHand = new[] { "staff" }, StaffSpell = StatType.GrantIceShard, Armour = "int",
                CoreSkills = new[] { StatType.GrantIceShard }, SupportSkills = new[] { StatType.GrantFrostNova },
                Weights = Caster(StatType.ColdDamage, 1.6f, StatType.ColdSpellLevels, 2f, StatType.ColdPenetration, 1.2f,
                    StatType.ElementalPenetration, 1.2f, StatType.DamageVsChilled, 1f, StatType.Shatter, 0.6f, StatType.MoreColdDamage, 2f,
                    StatType.AdditionalSpellProjectiles, 1.5f, StatType.AdditionalProjectiles, 1.5f),
                Passives = new[] { "s_sp2", "k_glacial", "s_eye", "w_m2", "w_archmage", "s_stride", "w_l3", "s_life_mid_heart", "w_life_heart", "k_twincast" },
                Uniques = new[] { "Frostglass Bough", "Twinflame Loop", "Elder's Charm" }
            },
            new GauntletBuild
            {
                Id = "volley_archer", Name = "Volley Archer",
                Summary = "Split Shot and Rain of Arrows: Grace and the Volley keystone. Whisperwind (extra arrow) early.",
                MainHand = new[] { "bow" }, OffHand = new[] { "quiver" }, Armour = "dex",
                CoreSkills = new[] { StatType.GrantSplitShot, StatType.GrantRainOfArrows }, SupportSkills = new[] { StatType.GrantBurningArrow, StatType.GrantPiercingShot },
                BowSkill = SkillId.SplitShot,
                Weights = Archer(),
                Passives = new[] { "g8", "k_volley", "g_deadeye", "g_crit_master", "g_longshot", "g_life_heart", "g5", "f_feast" },
                Uniques = new[] { "Whisperwind", "Starfall" }
            },
            new GauntletBuild
            {
                Id = "mace_juggernaut", Name = "Mace Juggernaut", Melee = true,
                Summary = "Pulverize and Fissure with a maul: Might and Iron Reflexes, built tanky. Cinderwake (melee splash) from level 17.",
                MainHand = new[] { "maul", "mace" }, OffHand = new[] { "shield_str" }, Armour = "str",
                CoreSkills = new[] { StatType.GrantPulverize, StatType.GrantFissure }, SupportSkills = new[] { StatType.GrantWarCry },
                Weights = Melee(StatType.ArmourPenetration, 0.8f, StatType.MeleeSplash, 2f, StatType.MoreMeleeDamage, 2f),
                Passives = new[] { "m_life_heart", "m8", "m_bloodbath", "m5", "k_unbreakable", "f_feast", "m_life_guard", "f_life_heart" },
                Uniques = new[] { "Cinderwake", "Bloodroot Cord" }
            },
            new GauntletBuild
            {
                Id = "necromancer", Name = "Necromancer",
                Summary = "Skeletons, Skeleton Mages and a Bone Golem, attacking with a Death Mark grimoire: Necromancy and Bone Legion. Gravewarden's Rod from level 9.",
                MainHand = new[] { "sceptre" }, OffHand = new[] { "grimoire" }, Armour = "int",
                CoreSkills = new[] { StatType.GrantRaiseSkeletons, StatType.GrantSkeletonMages },
                SupportSkills = new[] { StatType.GrantBoneGolem, StatType.GrantSpiritWolves, StatType.GrantGraveRot },
                Weights = Summoner(StatType.RaiseSkeletonsLevels, 2f, StatType.SkeletonMagesLevels, 2f, StatType.AdditionalSkeletons, 2f),
                Passives = new[] { "n_ward", "n1", "n2", "n_lord", "k_legion", "k_herald", "k_soulbond", "n_life_inner", "w_life_heart", "n_life_outer" },
                Uniques = new[] { "Gravewarden's Rod", "Gravesong Charm" }
            },

            // ---- Unconventional
            new GauntletBuild
            {
                Id = "venom_assassin", Name = "Venom Assassin", Melee = true,
                Summary = "Fang Strike and Venom Slash daggers stacking poison, with a buckler. Leechfang from level 9.",
                MainHand = new[] { "dagger" }, OffHand = new[] { "shield_dex" }, Armour = "dex",
                CoreSkills = new[] { StatType.GrantFangStrike, StatType.GrantVenomSlash },
                Weights = Melee(StatType.Armour, 0.4f, StatType.Evasion, 1f, StatType.Dexterity, 0.8f, StatType.Strength, 0.2f,
                    StatType.PoisonDamage, 1.6f, StatType.DamageOverTime, 1.4f, StatType.PoisonOnHit, 1.6f, StatType.PoisonPenetration, 1.2f,
                    StatType.VenomCloudOnHit, 1f, StatType.MorePoisonDamage, 2f),
                Passives = new[] { "v3", "k_viper", "f_feast", "g8", "v5", "f_dancer", "v_life", "g_life_heart" },
                Uniques = new[] { "Leechfang", "Widow's Brood@21", "Shepherd's Fang@21" }
            },
            new GauntletBuild
            {
                Id = "plague_caster", Name = "Plague Caster",
                Summary = "Venom Spout poison eruptions and damage over time. Crook of the Last Shepherd and Widow's Brood from level 21.",
                MainHand = new[] { "staff" }, Armour = "int",
                CoreSkills = new[] { StatType.GrantVenomSpout }, SupportSkills = new[] { StatType.GrantFrostNova },
                Weights = Caster(StatType.PoisonDamage, 1.8f, StatType.DamageOverTime, 1.5f, StatType.PoisonPenetration, 1.3f, StatType.MorePoisonDamage, 2f),
                Passives = new[] { "v3", "k_viper", "v5", "w_m2", "w_archmage", "s_eye", "g_life_heart", "w_life_heart" },
                Uniques = new[] { "!Crook of the Last Shepherd@21", "!Widow's Brood@21", "Elder's Charm" }
            },
            new GauntletBuild
            {
                Id = "viper_brood", Name = "Viper Brood",
                Summary = "A swarm of Summon Viper minions. Book of Shed Skin (+3 minions, weaker each) from level 21.",
                MainHand = new[] { "sceptre" }, OffHand = new[] { "grimoire" }, Armour = "int",
                CoreSkills = new[] { StatType.GrantSummonViper }, SupportSkills = new[] { StatType.GrantRaiseSkeletons, StatType.GrantGraveRot },
                Weights = Summoner(StatType.PoisonDamage, 0.6f),
                Passives = new[] { "n_ward", "n1", "n2", "n_lord", "k_legion", "k_herald", "k_spiritpact", "n_life_inner", "w_life_heart" },
                Uniques = new[] { "!Book of Shed Skin@21", "Gravesong Charm" }
            },
            new GauntletBuild
            {
                Id = "stormblade_duelist", Name = "Stormblade Duelist", Melee = true,
                Summary = "A critical-strike sword with Lunging Thrust whose crits arc lightning. Thundergrip and Storm's Receipt from level 17.",
                MainHand = new[] { "sword" }, OffHand = new[] { "shield_dex" }, Armour = "dex",
                CoreSkills = new[] { StatType.GrantLungingThrust, StatType.GrantCleave },
                Weights = Melee(StatType.Armour, 0.4f, StatType.Evasion, 1f, StatType.Dexterity, 0.7f, StatType.CriticalChance, 1.4f, StatType.CriticalMultiplier, 1.2f,
                    StatType.Stormblade, 1.2f, StatType.LightningDamage, 0.9f, StatType.ShockChance, 0.6f),
                Passives = new[] { "f_dancer", "f_precision", "f_feast", "k_stormblade", "f_life_heart", "s2", "k_thunderlord" },
                Uniques = new[] { "!Thundergrip", "!Storm's Receipt" }
            },
            new GauntletBuild
            {
                Id = "mana_battlemage", Name = "Mana Battlemage",
                Summary = "Stacks mana for Mind over Matter, and Chain Lightning hits add mana as damage. Mantle of the Still Mind from level 25, Elder's Charm from 9.",
                MainHand = new[] { "staff" }, StaffSpell = StatType.GrantChainLightning, Armour = "int",
                CoreSkills = new[] { StatType.GrantChainLightning },
                Weights = Caster(StatType.MaxMana, 2f, StatType.IncreasedMana, 2f, StatType.ManaRegen, 1.4f, StatType.ManaAbsorb, 1.5f,
                    StatType.ManaAsDamage, 2f, StatType.LightningDamage, 1.4f, StatType.LightningSpellLevels, 2f, StatType.LightningPenetration, 1.2f,
                    StatType.AdditionalChains, 1.5f, StatType.MoreLightningDamage, 2f),
                Passives = new[] { "w_l3", "k_mom", "s_eye", "s_conductor", "w_archmage", "w_life_mid", "k_thunderlord", "s_life_mid_heart" },
                Uniques = new[] { "!Mantle of the Still Mind", "Elder's Charm" }
            },
            new GauntletBuild
            {
                Id = "rampage_berserker", Name = "Rampage Berserker", Melee = true,
                Summary = "Reaping Arc with an axe; kills stack Rampage. Gorebrand from level 25, Hands of the Butcher-King at 34.",
                MainHand = new[] { "greataxe", "axe" }, OffHand = new[] { "shield_str" }, Armour = "str",
                CoreSkills = new[] { StatType.GrantReapingArc, StatType.GrantCleave }, SupportSkills = new[] { StatType.GrantWarCry },
                Weights = Melee(StatType.Rampage, 1.5f, StatType.OnslaughtOnKill, 0.6f, StatType.LifePercentOnKill, 1f, StatType.MoreMeleeDamage, 2f),
                Passives = new[] { "m8", "f_berserk", "m_bloodbath", "f_feast", "k_pain", "m_life_heart", "m5" },
                Uniques = new[] { "!Gorebrand", "!Hands of the Butcher-King", "Bonehew" }
            },
            new GauntletBuild
            {
                Id = "whirling_blademaster", Name = "Whirling Blademaster", Melee = true,
                Summary = "Whirlwind with a greatsword, the attack held down to spin through packs, War Cry for Onslaught.",
                MainHand = new[] { "greatsword", "sword" }, OffHand = new[] { "shield_str" }, Armour = "str",
                CoreSkills = new[] { StatType.GrantWhirlwind }, SupportSkills = new[] { StatType.GrantWarCry },
                Weights = Melee(StatType.MoreMeleeDamage, 2f, StatType.MeleeSplash, 1f),
                Passives = new[] { "m8", "m_bloodbath", "f_feast", "m_life_heart", "m5", "f_life_heart" }
            },
            new GauntletBuild
            {
                Id = "rimefletch_ranger", Name = "Rimefletch Ranger",
                Summary = "Bow attacks converted to cold, chilling, with Arctic Archer. Rimefletch quiver from level 7.",
                MainHand = new[] { "bow" }, OffHand = new[] { "quiver" }, Armour = "dex",
                CoreSkills = new[] { StatType.GrantSplitShot, StatType.GrantPiercingShot }, SupportSkills = new[] { StatType.GrantRainOfArrows },
                BowSkill = SkillId.SplitShot,
                Weights = Archer(StatType.ColdDamage, 1.4f, StatType.ChillOnHit, 0.8f, StatType.DamageVsChilled, 1.2f, StatType.ColdPenetration, 1f,
                    StatType.Shatter, 0.5f),
                Passives = new[] { "g_hoarfrost", "g8", "k_volley", "g_deadeye", "b_arctic", "s_stride", "k_glacial", "g_life_heart" },
                Uniques = new[] { "!Rimefletch@7" }
            },
            new GauntletBuild
            {
                Id = "point_blank_sniper", Name = "Point-Blank Sniper",
                Summary = "Piercing Shot critical strikes from up close with Point Blank. Hollowpoint from level 25.",
                MainHand = new[] { "bow" }, OffHand = new[] { "quiver" }, Armour = "dex",
                CoreSkills = new[] { StatType.GrantPiercingShot }, SupportSkills = new[] { StatType.GrantSplitShot },
                BowSkill = SkillId.PiercingShot,
                Weights = Archer(StatType.CriticalChance, 1.5f, StatType.CriticalMultiplier, 1.4f, StatType.ArmourPenetration, 0.8f, StatType.MoreBowDamage, 2f),
                Passives = new[] { "g8", "k_frostbite", "g_deadeye", "g_crit_master", "g_longshot", "f_precision", "g_life_heart" },
                Uniques = new[] { "!Hollowpoint", "Death's Grip" }
            },
            new GauntletBuild
            {
                Id = "blood_mage", Name = "Blood Mage",
                Summary = "Chain Lightning paid for with life (Blood Magic), on life-stacking gear.",
                MainHand = new[] { "staff" }, StaffSpell = StatType.GrantChainLightning, Armour = "int",
                CoreSkills = new[] { StatType.GrantChainLightning },
                Weights = Caster(StatType.MaxLife, 2f, StatType.IncreasedLife, 2f, StatType.LifeRegen, 1.2f, StatType.PercentLifeRegen, 1.5f,
                    StatType.SpellLeech, 1.5f, StatType.Armour, 0.5f, StatType.MaxMana, 0f, StatType.IncreasedMana, 0f, StatType.ManaRegen, 0f,
                    StatType.LightningDamage, 1.5f, StatType.LightningSpellLevels, 2f, StatType.LightningPenetration, 1.2f, StatType.AdditionalChains, 1.5f,
                    StatType.MoreLightningDamage, 2f, StatType.Intelligence, 0.5f, StatType.Strength, 0.5f),
                Passives = new[] { "k_bloodthirst", "m_life_heart", "s_conductor", "s_eye", "k_thunderlord", "w_archmage", "z_t4", "z_t3" },
                Uniques = new[] { "Conductor's Reach", "Stormcaller's Circlet", "Bloodroot Cord" }
            },
            new GauntletBuild
            {
                Id = "glass_archmage", Name = "Glass Diadem Archmage",
                Summary = "Ice Shard spell-level stacking; The Glass Diadem (+2 spell levels, 35-40% less life) at level 34, Pendant of the Fleeting Thought from 25.",
                MainHand = new[] { "staff" }, StaffSpell = StatType.GrantIceShard, Armour = "int",
                CoreSkills = new[] { StatType.GrantIceShard }, SupportSkills = new[] { StatType.GrantFrostNova },
                Weights = Caster(StatType.AllSpellLevels, 3f, StatType.ColdSpellLevels, 2.5f, StatType.ColdDamage, 1.4f, StatType.ColdPenetration, 1f,
                    StatType.AdditionalSpellProjectiles, 1.5f, StatType.AdditionalProjectiles, 1.5f, StatType.MoreSpellDamage, 2f),
                Passives = new[] { "w_m2", "w_archmage", "k_twincast", "s_eye", "s_sp2", "k_glacial", "w_l3", "w_life_heart", "s_life_mid_heart" },
                Uniques = new[] { "!The Glass Diadem", "Pendant of the Fleeting Thought" }
            },
        };
    }
}

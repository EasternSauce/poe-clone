using System;
using System.Collections.Generic;
using System.Globalization;

namespace PoeClone.Inventory
{
    /// <summary>Everything an item can modify, and everything the character sheet shows.</summary>
    public enum StatType
    {
        Strength,
        Dexterity,
        Intelligence,
        MaxLife,
        MaxMana,
        Armour,
        Evasion,
        BlockChance,
        PhysicalDamage,
        AttackSpeed,
        FireResistance,
        ColdResistance,
        LightningResistance,
        MovementSpeed,

        // Special stats: rare on gear (uniques) and on the tree's keystones, each changing how the
        // character plays rather than adding a bit more of something. Saved by number: add at the end.
        AdditionalArrows,           // bow attacks loose this many extra arrows in a spread
        AdditionalSpellProjectiles, // Fire Bolt and Ice Shard fire this many extra projectiles
        SpellDamage,                // % increased spell damage
        AreaOfEffect,               // % increased area of Frost Nova, Cleave and Fire Bolt's burst
        LifeRegen,                  // life regenerated per second
        ManaRegen,                  // % increased mana regeneration
        LifeLeech,                  // % of attack damage dealt returned as life
        ManaAbsorb,                 // % more of every hit taken from mana before life (Mind over Matter)
        AdditionalChains,           // Chain Lightning arcs this many more times
        ChillOnHit,                 // % chance for attacks to chill
        CullingStrike,              // 1: hits kill non-boss enemies left under 10% life
        LifeOnKill,                 // life gained for each enemy killed
        MeleeRange,                 // % increased reach of melee attacks and Cleave

        // Skills granted by gear (see SkillGrants): the value is the skill's level. A weapon's
        // staff's first spell becomes its attack. Saved by number like everything above: add at the end.
        GrantCleave,
        GrantFireBolt,
        GrantDash,
        GrantFrostNova,
        GrantRejuvenate,
        GrantChainLightning,
        GrantIceShard,

        AllSpellLevels,             // +N to the level of every spell granted by gear
        FireSpellLevels,
        ColdSpellLevels,
        LightningSpellLevels,
        CastSpeed,                  // % increased cast speed (spells used as the attack)
        CooldownRecovery,           // % faster skill cooldowns

        // Mostly from the passive tree (see HitEffects for what each does in a fight).
        FireDamage,                 // % increased fire damage
        ColdDamage,                 // % increased cold damage
        LightningDamage,            // % increased lightning damage
        IgniteChance,               // % chance for fire hits to set the enemy burning
        ShockChance,                // % chance for lightning hits to shock (takes more damage for a while)
        DamageVsChilled,            // % increased damage against chilled enemies
        CriticalChance,             // % chance for a hit to be critical
        CriticalMultiplier,         // % added to the critical damage multiplier
        AttackDamage,               // % increased damage with attacks
        Damage,                     // % increased damage of every kind
        DamageWhileLowLife,         // % increased damage while under half life
        IncreasedLife,              // % increased maximum life
        IncreasedMana,              // % increased maximum mana
        PercentLifeRegen,           // % of maximum life regenerated per second
        ManaOnKill,                 // mana gained for each enemy killed
        OnslaughtOnKill,            // % chance on kill to gain Onslaught (faster attacks, casts and movement)
        ExplodeOnKill,              // % chance for a killed enemy to explode, burning those around it

        // Skill synergies (keystones): each makes two of the game's skills or effects work together.
        GlacialStep,                // 1: Dash ends in a Frost Nova
        Shatter,                    // % chance for a chilled enemy killed to burst into ice, chilling and hurting those near
        SecondWind,                 // 1: Rejuvenate also refills mana and grants Onslaught
        Stormblade,                 // % chance for an attack critical strike to arc lightning to nearby enemies

        GrantTeleport,              // a skill grant like the ones above (added later, so it sits here)
        GrantRaiseSkeletons
    }

    /// <summary>
    /// The stats that grant a skill (their value is its level). Pure data, so the item side knows
    /// which grants exist without knowing the skills themselves (those live in Skills.SkillBook).
    /// </summary>
    public static class SkillGrants
    {
        /// <summary>Highest level a skill drops at (gear "+N levels" can still push it past this).</summary>
        public const int MaxDropLevel = 10;

        /// <summary>Spells a staff's attack can be: one always rolls on every staff.</summary>
        public static readonly StatType[] StaffMain = { StatType.GrantFireBolt, StatType.GrantChainLightning, StatType.GrantIceShard };

        public static bool IsGrant(StatType stat)
        {
            return (stat >= StatType.GrantCleave && stat <= StatType.GrantIceShard) || stat == StatType.GrantTeleport || stat == StatType.GrantRaiseSkeletons;
        }

        /// <summary>
        /// A spammable spell that, on a staff, replaces its plain attack (the first one on the staff
        /// does; any further one is an ordinary skill for the bar).
        /// </summary>
        public static bool IsMain(StatType stat)
        {
            return Array.IndexOf(StaffMain, stat) >= 0;
        }

        public static string SkillName(StatType grant)
        {
            switch (grant)
            {
                case StatType.GrantCleave: return "Cleave";
                case StatType.GrantFireBolt: return "Fire Bolt";
                case StatType.GrantDash: return "Dash";
                case StatType.GrantFrostNova: return "Frost Nova";
                case StatType.GrantRejuvenate: return "Rejuvenate";
                case StatType.GrantChainLightning: return "Chain Lightning";
                case StatType.GrantIceShard: return "Ice Shard";
                case StatType.GrantTeleport: return "Teleport";
                case StatType.GrantRaiseSkeletons: return "Raise Skeletons";
                default: return grant.ToString();
            }
        }

        /// <summary>
        /// The skill level a drop of this item level rolls: up to 1 at item level 1, up to 10 at
        /// item level 15 (the toughest things in the Frozen Hollow), often a level or two below the top.
        /// </summary>
        public static int RollLevel(Random rng, int itemLevel)
        {
            int top = Math.Max(1, Math.Min(MaxDropLevel, (int)Math.Round(1 + (itemLevel - 1) * 9.0 / 14.0)));
            double roll = rng.NextDouble();
            int below = roll < 0.5 ? 0 : roll < 0.85 ? 1 : 2;
            return Math.Max(1, top - below);
        }
    }

    /// <summary>One line of an item's stats: "+30 Armour" is (Armour, 30).</summary>
    public readonly struct StatModifier
    {
        public StatType Stat { get; }
        public float Value { get; }

        public StatModifier(StatType stat, float value)
        {
            Stat = stat;
            Value = value;
        }
    }

    /// <summary>The character's own stats before gear, plus the level/XP shown on the sheet.</summary>
    public sealed class BaseStats
    {
        private readonly float[] values = new float[Enum.GetValues(typeof(StatType)).Length];

        public int Level = 1;
        public int Experience;
        public int ExperienceRequired = 200;

        public float Get(StatType stat)
        {
            return values[(int)stat];
        }

        public BaseStats Set(StatType stat, float value)
        {
            values[(int)stat] = value;
            return this;
        }
    }

    /// <summary>
    /// The character's final stats: base + everything worn + what attributes grant.
    /// Pure C# (no Unity types) so it can be unit tested.
    /// </summary>
    public sealed class StatSheet
    {
        // Each point of an attribute from gear also grants a little of a related stat.
        public const float LifePerStrength = 0.5f;
        public const float ManaPerIntelligence = 0.5f;
        public const float EvasionPerDexterity = 1f;

        public const float ResistanceCap = 75f;
        public const float BlockCap = 75f;

        private readonly float[] baseValues;
        private readonly float[] gear;
        private readonly float[] derived;

        public int Level { get; }
        public int Experience { get; }
        public int ExperienceRequired { get; }

        private StatSheet(BaseStats baseStats, EquipmentSet equipment, IEnumerable<StatModifier> extra)
        {
            int count = Enum.GetValues(typeof(StatType)).Length;
            baseValues = new float[count];
            gear = new float[count];
            derived = new float[count];

            for (int i = 0; i < count; i++)
                baseValues[i] = baseStats.Get((StatType)i);

            Level = baseStats.Level;
            Experience = baseStats.Experience;
            ExperienceRequired = baseStats.ExperienceRequired;

            if (equipment != null)
            {
                foreach (EquipSlot slot in SlotRules.AllSlots)
                {
                    ItemData item = equipment.Get(slot);
                    if (item == null)
                        continue;

                    foreach (StatModifier m in item.Modifiers)
                        gear[(int)m.Stat] += m.Value;
                }
            }

            // Passives count like gear (and their attributes grant the same life/mana/evasion).
            if (extra != null)
            {
                foreach (StatModifier m in extra)
                    gear[(int)m.Stat] += m.Value;
            }

            derived[(int)StatType.MaxLife] = (float)Math.Floor(gear[(int)StatType.Strength] * LifePerStrength);
            derived[(int)StatType.MaxMana] = (float)Math.Floor(gear[(int)StatType.Intelligence] * ManaPerIntelligence);
            derived[(int)StatType.Evasion] = (float)Math.Floor(gear[(int)StatType.Dexterity] * EvasionPerDexterity);

            // "% increased" life and mana scale the whole pool, the character's own included; the
            // extra counts as coming from gear so the character picks it up with the rest.
            Increase(StatType.MaxLife, StatType.IncreasedLife);
            Increase(StatType.MaxMana, StatType.IncreasedMana);
        }

        private void Increase(StatType pool, StatType percent)
        {
            float increased = baseValues[(int)percent] + gear[(int)percent];
            if (increased == 0f)
                return;
            float whole = baseValues[(int)pool] + gear[(int)pool] + derived[(int)pool];
            derived[(int)pool] += (float)Math.Floor(whole * Math.Max(-0.9f, increased / 100f));
        }

        public static StatSheet Build(BaseStats baseStats, EquipmentSet equipment, IEnumerable<StatModifier> extra = null)
        {
            if (baseStats == null)
                throw new ArgumentNullException(nameof(baseStats));

            return new StatSheet(baseStats, equipment, extra);
        }

        /// <summary>The character's own value, before gear.</summary>
        public float Base(StatType stat)
        {
            return baseValues[(int)stat];
        }

        /// <summary>The total granted directly by worn items.</summary>
        public float Gear(StatType stat)
        {
            return gear[(int)stat];
        }

        /// <summary>What the stat gets indirectly from gear attributes (e.g. life from strength).</summary>
        public float Derived(StatType stat)
        {
            return derived[(int)stat];
        }

        /// <summary>Everything gear adds to this stat, directly or through attributes.</summary>
        public float FromGear(StatType stat)
        {
            return Gear(stat) + Derived(stat);
        }

        /// <summary>The final value, with resistance and block caps applied.</summary>
        public float Total(StatType stat)
        {
            float total = Base(stat) + FromGear(stat);

            switch (stat)
            {
                case StatType.FireResistance:
                case StatType.ColdResistance:
                case StatType.LightningResistance:
                    return Math.Min(ResistanceCap, total);
                case StatType.BlockChance:
                    return Math.Min(BlockCap, total);
                default:
                    return total;
            }
        }
    }

    /// <summary>Turns stats into the text shown on tooltips and the character page.</summary>
    public static class StatFormatter
    {
        public static string Label(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return "Strength";
                case StatType.Dexterity: return "Dexterity";
                case StatType.Intelligence: return "Intelligence";
                case StatType.MaxLife: return "Maximum Life";
                case StatType.MaxMana: return "Maximum Mana";
                case StatType.Armour: return "Armour";
                case StatType.Evasion: return "Evasion";
                case StatType.BlockChance: return "Chance to Block";
                case StatType.PhysicalDamage: return "Physical Damage";
                case StatType.AttackSpeed: return "Attack Speed";
                case StatType.FireResistance: return "Fire Resistance";
                case StatType.ColdResistance: return "Cold Resistance";
                case StatType.LightningResistance: return "Lightning Resistance";
                case StatType.MovementSpeed: return "Movement Speed";
                case StatType.AdditionalArrows: return "Additional Arrows";
                case StatType.AdditionalSpellProjectiles: return "Additional Spell Projectiles";
                case StatType.SpellDamage: return "Spell Damage";
                case StatType.AreaOfEffect: return "Area of Effect";
                case StatType.LifeRegen: return "Life Regeneration";
                case StatType.ManaRegen: return "Mana Regeneration";
                case StatType.LifeLeech: return "Life Leech";
                case StatType.ManaAbsorb: return "Damage Taken from Mana";
                case StatType.AdditionalChains: return "Additional Chains";
                case StatType.ChillOnHit: return "Chance to Chill";
                case StatType.CullingStrike: return "Culling Strike";
                case StatType.LifeOnKill: return "Life on Kill";
                case StatType.MeleeRange: return "Melee Range";
                case StatType.AllSpellLevels: return "Spell Levels";
                case StatType.FireSpellLevels: return "Fire Spell Levels";
                case StatType.ColdSpellLevels: return "Cold Spell Levels";
                case StatType.LightningSpellLevels: return "Lightning Spell Levels";
                case StatType.CastSpeed: return "Cast Speed";
                case StatType.CooldownRecovery: return "Cooldown Recovery";
                case StatType.FireDamage: return "Fire Damage";
                case StatType.ColdDamage: return "Cold Damage";
                case StatType.LightningDamage: return "Lightning Damage";
                case StatType.IgniteChance: return "Chance to Ignite";
                case StatType.ShockChance: return "Chance to Shock";
                case StatType.DamageVsChilled: return "Damage vs Chilled";
                case StatType.CriticalChance: return "Critical Strike Chance";
                case StatType.CriticalMultiplier: return "Critical Multiplier";
                case StatType.AttackDamage: return "Attack Damage";
                case StatType.Damage: return "Damage";
                case StatType.DamageWhileLowLife: return "Damage on Low Life";
                case StatType.IncreasedLife: return "Increased Life";
                case StatType.IncreasedMana: return "Increased Mana";
                case StatType.PercentLifeRegen: return "Life Regen (% of max)";
                case StatType.ManaOnKill: return "Mana on Kill";
                case StatType.OnslaughtOnKill: return "Onslaught on Kill";
                case StatType.ExplodeOnKill: return "Corpse Explosion";
                case StatType.GlacialStep: return "Glacial Step";
                case StatType.Shatter: return "Shatter";
                case StatType.SecondWind: return "Second Wind";
                case StatType.Stormblade: return "Stormblade";
                default:
                    if (SkillGrants.IsGrant(stat))
                        return SkillGrants.SkillName(stat);
                    return stat.ToString();
            }
        }

        /// <summary>The special stats (see the end of StatType), shown in their own list.</summary>
        public static bool IsSpecial(StatType stat)
        {
            return stat >= StatType.AdditionalArrows;
        }

        /// <summary>Stats that are shown as a percentage.</summary>
        public static bool IsPercent(StatType stat)
        {
            switch (stat)
            {
                case StatType.BlockChance:
                case StatType.AttackSpeed:
                case StatType.FireResistance:
                case StatType.ColdResistance:
                case StatType.LightningResistance:
                case StatType.MovementSpeed:
                case StatType.SpellDamage:
                case StatType.AreaOfEffect:
                case StatType.ManaRegen:
                case StatType.LifeLeech:
                case StatType.ManaAbsorb:
                case StatType.ChillOnHit:
                case StatType.MeleeRange:
                case StatType.CastSpeed:
                case StatType.CooldownRecovery:
                case StatType.FireDamage:
                case StatType.ColdDamage:
                case StatType.LightningDamage:
                case StatType.IgniteChance:
                case StatType.ShockChance:
                case StatType.DamageVsChilled:
                case StatType.CriticalChance:
                case StatType.CriticalMultiplier:
                case StatType.AttackDamage:
                case StatType.Damage:
                case StatType.DamageWhileLowLife:
                case StatType.IncreasedLife:
                case StatType.IncreasedMana:
                case StatType.PercentLifeRegen:
                case StatType.OnslaughtOnKill:
                case StatType.ExplodeOnKill:
                case StatType.Shatter:
                case StatType.Stormblade:
                    return true;
                default:
                    return false;
            }
        }

        public static string Number(float value)
        {
            if (Math.Abs(value - Math.Round(value)) < 0.001f)
                return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>A stat line on an item, in Path of Exile wording ("+30 to Armour").</summary>
        public static string ItemLine(StatModifier m)
        {
            string n = Number(Math.Abs(m.Value));
            string sign = m.Value < 0f ? "-" : "+";

            switch (m.Stat)
            {
                case StatType.Strength: return sign + n + " to Strength";
                case StatType.Dexterity: return sign + n + " to Dexterity";
                case StatType.Intelligence: return sign + n + " to Intelligence";
                case StatType.MaxLife: return sign + n + " to Maximum Life";
                case StatType.MaxMana: return sign + n + " to Maximum Mana";
                case StatType.Armour: return sign + n + " to Armour";
                case StatType.Evasion: return sign + n + " to Evasion";
                case StatType.BlockChance: return sign + n + "% Chance to Block";
                case StatType.PhysicalDamage: return "Adds " + n + " Physical Damage";
                case StatType.AttackSpeed: return n + "% " + (m.Value < 0f ? "reduced" : "increased") + " Attack Speed";
                case StatType.FireResistance: return sign + n + "% to Fire Resistance";
                case StatType.ColdResistance: return sign + n + "% to Cold Resistance";
                case StatType.LightningResistance: return sign + n + "% to Lightning Resistance";
                case StatType.MovementSpeed: return n + "% " + (m.Value < 0f ? "reduced" : "increased") + " Movement Speed";
                case StatType.AdditionalArrows: return "Bow attacks fire " + n + " additional arrow" + (n == "1" ? "" : "s");
                case StatType.AdditionalSpellProjectiles: return "Fire Bolt and Ice Shard fire " + n + " additional projectile" + (n == "1" ? "" : "s");
                case StatType.SpellDamage: return n + "% increased Spell Damage";
                case StatType.AreaOfEffect: return n + "% increased Area of Effect";
                case StatType.LifeRegen: return "Regenerate " + n + " Life per second";
                case StatType.ManaRegen: return n + "% increased Mana Regeneration";
                case StatType.LifeLeech: return n + "% of Attack Damage Leeched as Life";
                case StatType.ManaAbsorb: return n + "% of Damage is taken from Mana before Life";
                case StatType.AdditionalChains: return "Chain Lightning arcs " + n + " additional time" + (n == "1" ? "" : "s");
                case StatType.ChillOnHit: return n + "% chance to Chill enemies with Attacks";
                case StatType.CullingStrike: return "Culling Strike: kill enemies left below 10% Life";
                case StatType.LifeOnKill: return "Gain " + n + " Life per enemy killed";
                case StatType.MeleeRange: return n + "% increased Melee Range";
                case StatType.AllSpellLevels: return sign + n + " to Level of all Spells";
                case StatType.FireSpellLevels: return sign + n + " to Level of all Fire Spells";
                case StatType.ColdSpellLevels: return sign + n + " to Level of all Cold Spells";
                case StatType.LightningSpellLevels: return sign + n + " to Level of all Lightning Spells";
                case StatType.CastSpeed: return n + "% increased Cast Speed";
                case StatType.CooldownRecovery: return n + "% faster Skill Cooldowns";
                case StatType.FireDamage: return Increased(m, "Fire Damage");
                case StatType.ColdDamage: return Increased(m, "Cold Damage");
                case StatType.LightningDamage: return Increased(m, "Lightning Damage");
                case StatType.IgniteChance: return n + "% chance to Ignite with Fire hits (burns for 60% of the hit over 3s)";
                case StatType.ShockChance: return n + "% chance to Shock with Lightning hits (shocked enemies take 25% more damage)";
                case StatType.DamageVsChilled: return Increased(m, "Damage against Chilled enemies");
                case StatType.CriticalChance: return n + "% chance to deal a Critical Strike";
                case StatType.CriticalMultiplier: return sign + n + "% to Critical Strike Multiplier";
                case StatType.AttackDamage: return Increased(m, "Attack Damage");
                case StatType.Damage: return Increased(m, "Damage");
                case StatType.DamageWhileLowLife: return Increased(m, "Damage while on Low Life (under half)");
                case StatType.IncreasedLife: return Increased(m, "Maximum Life");
                case StatType.IncreasedMana: return Increased(m, "Maximum Mana");
                case StatType.PercentLifeRegen: return "Regenerate " + n + "% of Maximum Life per second";
                case StatType.ManaOnKill: return "Gain " + n + " Mana per enemy killed";
                case StatType.OnslaughtOnKill: return n + "% chance on kill to gain Onslaught (20% faster attacks, casts and movement for 4s)";
                case StatType.ExplodeOnKill: return n + "% chance for enemies you kill to explode, dealing a sixth of their life as Fire damage around them";
                case StatType.GlacialStep: return "Dash ends in a Frost Nova that chills everything around you";
                case StatType.Shatter: return n + "% chance for Chilled enemies you kill to Shatter, dealing Cold damage and chilling those nearby";
                case StatType.SecondWind: return "Rejuvenate also restores a third of your Mana and grants Onslaught";
                case StatType.Stormblade: return n + "% chance for Attack Critical Strikes to arc Lightning to 3 nearby enemies";
                default:
                    if (SkillGrants.IsGrant(m.Stat))
                        return "Grants Level " + n + " " + SkillGrants.SkillName(m.Stat);
                    return sign + n + " " + Label(m.Stat);
            }
        }

        private static string Increased(StatModifier m, string what)
        {
            return Number(Math.Abs(m.Value)) + "% " + (m.Value < 0f ? "reduced " : "increased ") + what;
        }

        /// <summary>A final value on the character page ("42", "20%").</summary>
        public static string Value(StatType stat, float value)
        {
            return Number(value) + (IsPercent(stat) ? "%" : "");
        }
    }
}

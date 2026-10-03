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
        AdditionalSpellProjectiles, // Fire Bolt hurls this many extra bolts
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
        MeleeRange                  // % increased reach of melee attacks and Cleave
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
                default: return stat.ToString();
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
                case StatType.AdditionalSpellProjectiles: return "Fire Bolt hurls " + n + " additional bolt" + (n == "1" ? "" : "s");
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
                default: return sign + n + " " + Label(m.Stat);
            }
        }

        /// <summary>A final value on the character page ("42", "20%").</summary>
        public static string Value(StatType stat, float value)
        {
            return Number(value) + (IsPercent(stat) ? "%" : "");
        }
    }
}

using System;

namespace PoeClone.Inventory
{
    /// <summary>
    /// What the defensive and resource stats actually do, in one place so combat, the character
    /// page and the tests all agree. Loosely Path of Exile shaped, scaled to this game's small
    /// damage numbers. Pure C# (no Unity types) so it can be unit tested.
    /// </summary>
    public static class DefenceMath
    {
        public const float MaxEvadeChance = 0.6f;

        // Evasion needed for a 50% chance to evade.
        private const float EvasionForHalf = 600f;

        // Armour equal to this many times a hit's damage stops half of that hit.
        private const float ArmourPerDamageForHalf = 40f;

        // Mana regenerated per second: a share of the pool, plus a little per point of Intelligence.
        public const float ManaRegenFraction = 0.01f;
        public const float ManaRegenPerIntelligence = 0.04f;

        /// <summary>
        /// Share of every hit taken from mana instead of life while there is mana to take it from
        /// (a little of PoE's Mind over Matter for everyone; the Mind over Matter keystone and some
        /// gear add more through StatType.ManaAbsorb). More mana, and faster mana regeneration, soak more.
        /// </summary>
        public const float ManaAbsorbShare = 0.1f;

        /// <summary>Life regenerated per second by everyone, as a share of maximum life: very slow.</summary>
        public const float LifeRegenFraction = 0.002f;

        /// <summary>Enemies left below this share of their life by a culling hit die.</summary>
        public const float CullThreshold = 0.1f;

        /// <summary>The damage a typical enemy hit deals, used to describe armour on the character page.</summary>
        public const float ReferenceHit = 10f;

        /// <summary>Chance (0..1) to evade an enemy attack entirely.</summary>
        public static float EvadeChance(float evasion)
        {
            if (evasion <= 0f)
                return 0f;
            return Math.Min(MaxEvadeChance, evasion / (evasion + EvasionForHalf));
        }

        /// <summary>
        /// Share (0..1) of a physical hit that armour stops. Like PoE, armour is far better against
        /// many small hits than one big one: the bigger the hit, the less of it armour stops.
        /// </summary>
        public static float ArmourReduction(float armour, float hitDamage)
        {
            if (armour <= 0f || hitDamage <= 0f)
                return 0f;
            return armour / (armour + ArmourPerDamageForHalf * hitDamage);
        }

        /// <summary>Chance (0..1) to block a hit entirely, from the (already capped) block stat in percent.</summary>
        public static float BlockChance(float blockPercent)
        {
            return Math.Max(0f, Math.Min(StatSheet.BlockCap, blockPercent)) / 100f;
        }

        /// <summary>What's left of an elemental hit after resistance (in percent, capped like the sheet).</summary>
        public static float AfterResistance(float damage, float resistancePercent)
        {
            float resistance = Math.Min(StatSheet.ResistanceCap, resistancePercent);
            return damage * (1f - resistance / 100f);
        }

        /// <summary>How much of a hit (after armour/resistance) the mana pool soaks up.</summary>
        public static float ManaAbsorbed(float damage, float currentMana, float extraPercent = 0f)
        {
            if (damage <= 0f || currentMana <= 0f)
                return 0f;
            return Math.Min(currentMana, damage * TotalManaAbsorbShare(extraPercent));
        }

        /// <summary>The share (0..0.9) of each hit taken from mana, with the extra from gear and passives in percent.</summary>
        public static float TotalManaAbsorbShare(float extraPercent)
        {
            return Math.Min(0.9f, ManaAbsorbShare + Math.Max(0f, extraPercent) / 100f);
        }

        public static float ManaRegenPerSecond(float maxMana, float intelligence, float increasedPercent = 0f)
        {
            float regen = maxMana * ManaRegenFraction + intelligence * ManaRegenPerIntelligence;
            return Math.Max(0f, regen * (1f + increasedPercent / 100f));
        }

        public static float LifeRegenPerSecond(float maxLife, float flat, float percentOfMax = 0f)
        {
            return Math.Max(0f, maxLife * (LifeRegenFraction + percentOfMax / 100f) + flat);
        }

        /// <summary>Radius multiplier for an area bigger by this many percent (area grows with radius squared).</summary>
        public static float RadiusMultiplier(float areaPercent)
        {
            return (float)Math.Sqrt(Math.Max(0.1f, 1f + areaPercent / 100f));
        }
    }
}

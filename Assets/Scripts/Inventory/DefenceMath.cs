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
        public const float MaxEvadeChance = 0.75f;
        public const float MaxArmourReduction = 0.9f;

        // Evasion needed for a 50% chance to evade.
        private const float EvasionForHalf = 200f;

        // Armour equal to this many times a hit's damage stops half of that hit.
        private const float ArmourPerDamageForHalf = 12f;

        // Mana regenerated per second: a share of the pool, plus a little per point of Intelligence.
        public const float ManaRegenFraction = 0.02f;
        public const float ManaRegenPerIntelligence = 0.05f;

        /// <summary>
        /// Share of every hit taken from mana instead of life while there is mana to take it from
        /// (PoE's Mind over Matter). It's what Maximum Mana and Intelligence are for: more mana, and
        /// faster mana regeneration, soak more damage.
        /// </summary>
        public const float ManaAbsorbShare = 0.3f;

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
            return Math.Min(MaxArmourReduction, armour / (armour + ArmourPerDamageForHalf * hitDamage));
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
        public static float ManaAbsorbed(float damage, float currentMana)
        {
            if (damage <= 0f || currentMana <= 0f)
                return 0f;
            return Math.Min(currentMana, damage * ManaAbsorbShare);
        }

        public static float ManaRegenPerSecond(float maxMana, float intelligence)
        {
            return Math.Max(0f, maxMana * ManaRegenFraction + intelligence * ManaRegenPerIntelligence);
        }
    }
}

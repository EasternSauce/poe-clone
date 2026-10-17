using System;

namespace PoeClone.Inventory
{
    /// <summary>Effigies inherit spell bonuses and have their own defensive and offensive passives.</summary>
    public static class EffigyRules
    {
        public const float Lifetime = 15f;
        public static int Limit(StatSheet sheet) => 2 + Math.Max(0, (int)(sheet?.Total(StatType.AdditionalEffigies) ?? 0f));
        public static float Life(StatSheet sheet) => (90f + 12f * Math.Max(0, (sheet?.Level ?? 1) - 1)) *
            Math.Max(0.1f, 1f + (sheet?.Total(StatType.EffigyLife) ?? 0f) / 100f);
        public static float Armour(StatSheet sheet) => 50f + 10f * Math.Max(0, (sheet?.Level ?? 1) - 1) +
            Math.Max(0f, sheet?.Total(StatType.EffigyArmour) ?? 0f);
        public static float Resistance(StatSheet sheet) => 30f + (sheet?.Total(StatType.EffigyResistances) ?? 0f);
        public static float DamageMultiplier(StatSheet sheet) => 0.5f * Math.Max(0.1f, 1f + (sheet?.Total(StatType.EffigyDamage) ?? 0f) / 100f);
        public static float DamageTaken(float amount, bool physical, StatSheet sheet) => physical
            ? amount * (1f - DefenceMath.ArmourReduction(Armour(sheet), amount))
            : DefenceMath.AfterResistance(amount, Resistance(sheet));
        public static float CastRate(StatSheet sheet, float skillSpeed, float temporaryMultiplier = 1f) =>
            Math.Max(0.5f, 1f + ((sheet?.Total(StatType.CastSpeed) ?? 0f) +
                (sheet?.Total(StatType.EffigyCastSpeed) ?? 0f) + skillSpeed) / 100f) * temporaryMultiplier;
    }
}

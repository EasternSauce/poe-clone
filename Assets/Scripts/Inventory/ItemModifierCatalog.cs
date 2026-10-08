using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    [Serializable]
    public sealed class ItemModifierTier
    {
        public int RequiredItemLevel = 1;
        public int Min = 1;
        public int Max = 1;
        public float Weight = 1f;

        public ItemModifierTier() { }
        public ItemModifierTier(int level, float min, float max, float weight)
        { RequiredItemLevel = level; Min = (int)min; Max = (int)max; Weight = weight; }
        public bool Eligible(int level) => RequiredItemLevel <= level && Weight > 0f &&
            !float.IsInfinity(Weight) && Min <= Max && Max < int.MaxValue;
    }

    [Serializable]
    public sealed class ItemModifierDefinition
    {
        public string Label;
        public bool Enabled = true;
        public StatType Stat;
        public float Weight = 1f;
        public ItemType[] On = Array.Empty<ItemType>();
        public bool AnyWeapon = true;
        public WeaponType[] Weapons = Array.Empty<WeaponType>();
        public ItemModifierTier[] Tiers = Array.Empty<ItemModifierTier>();

        public bool Allows(ItemType type, WeaponType weapon) => On != null &&
            Array.IndexOf(On, type) >= 0 && (type != ItemType.Weapon || AnyWeapon ||
            (Weapons != null && Array.IndexOf(Weapons, weapon) >= 0));
        public bool HasTier(int level) => Tiers != null && Array.Exists(Tiers, t => t != null && t.Eligible(level));
    }

    /// <summary>Persistent loot balance, loaded from Resources in both the editor and builds.</summary>
    public sealed class ItemModifierCatalog : ScriptableObject
    {
        public const string AssetPath = "Assets/Resources/ItemModifierCatalog.asset";
        public ItemModifierDefinition[] Modifiers = Array.Empty<ItemModifierDefinition>();
        public ItemModifierTier[] MainSkillTiers = Array.Empty<ItemModifierTier>();

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (Modifiers == null) errors.Add("Modifier list is missing.");
            else foreach (var mod in Modifiers)
            {
                if (mod == null) { errors.Add("Empty modifier entry."); continue; }
                if (string.IsNullOrWhiteSpace(mod.Label)) errors.Add("A modifier needs a label.");
                if (mod.Weight < 0 || float.IsNaN(mod.Weight) || float.IsInfinity(mod.Weight))
                    errors.Add(mod.Label + ": weight must be finite and nonnegative.");
                ValidateTiers(mod.Tiers, mod.Label, errors);
            }
            ValidateTiers(MainSkillTiers, "Main attack skill", errors);
            if (MainSkillTiers != null && !Array.Exists(MainSkillTiers, t => t != null && t.Eligible(1)))
                errors.Add("Main attack skills need a positive-weight tier available at item level 1.");
            return errors;
        }

        private static void ValidateTiers(ItemModifierTier[] tiers, string label, List<string> errors)
        {
            if (tiers == null || tiers.Length == 0) { errors.Add(label + ": add at least one tier."); return; }
            for (int i = 0; i < tiers.Length; i++)
            {
                var tier = tiers[i];
                if (tier == null || tier.RequiredItemLevel < 1 || tier.RequiredItemLevel > ItemGenerator.MaxItemLevel ||
                    tier.Min < 1 || tier.Max < tier.Min || tier.Max == int.MaxValue ||
                    tier.Weight < 0 || float.IsNaN(tier.Weight) || float.IsInfinity(tier.Weight))
                    errors.Add(label + " tier " + (tiers.Length - i) + ": use level 1–100, 1 ≤ min ≤ max, and a finite nonnegative weight.");
            }
        }
    }
}

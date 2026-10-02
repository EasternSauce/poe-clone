using UnityEngine;

namespace PoeClone.Skills
{
    public enum SkillId
    {
        Cleave,
        FireBolt,
        Dash,
        FrostNova,
        Rejuvenate,
        ChainLightning
    }

    /// <summary>One skill: what it costs, when it unlocks, and how it's shown.</summary>
    public sealed class SkillDefinition
    {
        public SkillId Id;
        public string Name;
        public string Short;        // two or three letters for the bar button
        public string Description;
        public int UnlockLevel;
        public float ManaCost;
        public float Cooldown;
        public Color Color;
    }

    /// <summary>
    /// Every skill in the game. Deliberately small and simple (not PoE's gem system): each one
    /// unlocks at a character level, sits in one of four bar slots, costs mana and has a cooldown.
    /// Damage spells scale with Intelligence, the melee one with the weapon and Strength.
    /// </summary>
    public static class SkillBook
    {
        public const int SlotCount = 4;

        public static readonly SkillDefinition[] All =
        {
            new SkillDefinition
            {
                Id = SkillId.Cleave, Name = "Cleave", Short = "CLV", UnlockLevel = 1,
                ManaCost = 8f, Cooldown = 2.5f, Color = new Color(0.85f, 0.75f, 0.55f),
                Description = "Swing all around you, hitting every enemy in reach for 140% weapon damage."
            },
            new SkillDefinition
            {
                Id = SkillId.FireBolt, Name = "Fire Bolt", Short = "FB", UnlockLevel = 2,
                ManaCost = 10f, Cooldown = 0.9f, Color = new Color(1f, 0.5f, 0.15f),
                Description = "Hurl a bolt of fire that bursts on the first enemy it hits. Scales with Intelligence."
            },
            new SkillDefinition
            {
                Id = SkillId.Dash, Name = "Dash", Short = "DSH", UnlockLevel = 3,
                ManaCost = 6f, Cooldown = 3f, Color = new Color(0.7f, 0.9f, 0.7f),
                Description = "Dart forward a short distance, out of trouble or into the fight."
            },
            new SkillDefinition
            {
                Id = SkillId.FrostNova, Name = "Frost Nova", Short = "FN", UnlockLevel = 5,
                ManaCost = 16f, Cooldown = 6f, Color = new Color(0.55f, 0.85f, 1f),
                Description = "A ring of frost bursts from you, damaging and slowing nearby enemies. Scales with Intelligence."
            },
            new SkillDefinition
            {
                Id = SkillId.Rejuvenate, Name = "Rejuvenate", Short = "REJ", UnlockLevel = 7,
                ManaCost = 20f, Cooldown = 14f, Color = new Color(0.45f, 0.95f, 0.45f),
                Description = "Restore 35% of your life over three seconds."
            },
            new SkillDefinition
            {
                Id = SkillId.ChainLightning, Name = "Chain Lightning", Short = "CL", UnlockLevel = 9,
                ManaCost = 18f, Cooldown = 2f, Color = new Color(1f, 0.95f, 0.4f),
                Description = "Lightning strikes the enemy under your cursor (or the nearest one) and arcs to two more. Hits harder aimed straight at an enemy, and harder still up close. Scales with Intelligence."
            },
        };

        public static SkillDefinition Get(SkillId id)
        {
            foreach (SkillDefinition s in All)
            {
                if (s.Id == id)
                    return s;
            }
            return All[0];
        }

        /// <summary>Spell damage multiplier from Intelligence: +1.5% per point.</summary>
        public static float SpellMultiplier(int intelligence)
        {
            return 1f + intelligence * 0.015f;
        }
    }
}

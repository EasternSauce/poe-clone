using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.Skills
{
    public enum SkillId
    {
        // Saved and replicated by number: add new skills at the end.
        Cleave,
        FireBolt,
        Dash,
        FrostNova,
        Rejuvenate,
        ChainLightning,
        IceShard,
        Teleport
    }

    /// <summary>The element a spell belongs to, for "+1 to level of all Fire Spells" and the like.</summary>
    public enum SkillElement
    {
        None,
        Fire,
        Cold,
        Lightning
    }

    /// <summary>One skill: what it costs, how it scales with its level, where it comes from, how it's shown.</summary>
    public sealed class SkillDefinition
    {
        public SkillId Id;
        public string Name;
        public string Short;        // two or three letters for the bar button
        public string Description;
        public StatType Grant;      // the gear stat that grants it (its value is the level)
        public string RollsOn;      // where it can be found, for the skills panel
        public bool Spell;
        public SkillElement Element;
        public float ManaCost;      // at level 1
        public float Cooldown;      // at level 1; for a staff's attack spell, the time between casts
        public float BaseDamage;    // spells: damage at level 1, before Intelligence and Spell Damage
        public Color Color;

        /// <summary>A staff spell: on the staff it is the attack (left click / the aim stick).</summary>
        public bool Main => SkillGrants.IsMain(Grant);

        /// <summary>
        /// Mana per use: 40% more each level (4.6x at level 10), so the cost keeps pace with a
        /// growing mana pool. Casting nonstop empties the pool in about ten seconds without mana
        /// regeneration gear, at any stage: casters live on mana potions and regen.
        /// </summary>
        public float ManaCostAt(int level)
        {
            return ManaCost * (1f + 0.4f * (Mathf.Max(1, level) - 1));
        }

        /// <summary>Cooldown (or time between casts for an attack spell): a little shorter each level.</summary>
        public float CooldownAt(int level)
        {
            float perLevel = Main ? 0.01f : 0.025f;
            return Cooldown * Mathf.Max(0.4f, 1f - perLevel * (Mathf.Max(1, level) - 1));
        }

        /// <summary>A spell's damage at this level before Intelligence and Spell Damage (about 4x from level 1 to 10).</summary>
        public float DamageAt(int level)
        {
            return BaseDamage * (1f + 0.35f * (Mathf.Max(1, level) - 1));
        }
    }

    /// <summary>
    /// Every skill in the game. Skills come from gear, not from character levels: each piece of
    /// gear can carry skills at a level (1 to 10 as drops, higher with "+N to level" gear), and a
    /// higher level hits harder, costs a little more and comes back sooner. A staff always carries
    /// a spell, which is its attack; any other skill goes in one of the bar slots (Q E R F and
    /// the spare mouse buttons), costs mana and has a cooldown.
    /// Damage spells scale with Intelligence, Cleave with the weapon and Strength.
    /// </summary>
    public static class SkillBook
    {
        /// <summary>Bar slots: Q E R F, then right, middle, back and forward mouse buttons.</summary>
        public const int SlotCount = 8;

        /// <summary>The slots the touch layout has round buttons for (the first four).</summary>
        public const int TouchSlotCount = 4;

        public static readonly SkillDefinition[] All =
        {
            new SkillDefinition
            {
                Id = SkillId.Cleave, Name = "Cleave", Short = "CLV", Grant = StatType.GrantCleave, RollsOn = "melee weapons",
                ManaCost = 8f, Cooldown = 3f, Color = new Color(0.85f, 0.75f, 0.55f),
                Description = "Swing all around you, hitting every enemy in reach for 140% weapon damage (+10% per level). Needs a melee weapon."
            },
            new SkillDefinition
            {
                Id = SkillId.FireBolt, Name = "Fire Bolt", Short = "FB", Grant = StatType.GrantFireBolt, RollsOn = "staves",
                Spell = true, Element = SkillElement.Fire, BaseDamage = 7f,
                ManaCost = 7f, Cooldown = 1.0f, Color = new Color(1f, 0.5f, 0.15f),
                Description = "Hurl a bolt of fire that bursts on the first enemy it hits."
            },
            new SkillDefinition
            {
                Id = SkillId.Dash, Name = "Dash", Short = "DSH", Grant = StatType.GrantDash, RollsOn = "boots",
                ManaCost = 6f, Cooldown = 3.5f, Color = new Color(0.7f, 0.9f, 0.7f),
                Description = "Dart forward a short distance, out of trouble or into the fight. Goes further each level."
            },
            new SkillDefinition
            {
                Id = SkillId.FrostNova, Name = "Frost Nova", Short = "FN", Grant = StatType.GrantFrostNova, RollsOn = "staves, helmets, gloves",
                Spell = true, Element = SkillElement.Cold, BaseDamage = 6f,
                ManaCost = 20f, Cooldown = 8f, Color = new Color(0.55f, 0.85f, 1f),
                Description = "A ring of frost bursts from you, damaging and slowing nearby enemies."
            },
            new SkillDefinition
            {
                Id = SkillId.Rejuvenate, Name = "Rejuvenate", Short = "REJ", Grant = StatType.GrantRejuvenate, RollsOn = "staves, amulets, belts",
                Spell = true, ManaCost = 22f, Cooldown = 18f, Color = new Color(0.45f, 0.95f, 0.45f),
                Description = "Restore 35% of your life (+2.5% per level) over three seconds."
            },
            new SkillDefinition
            {
                Id = SkillId.ChainLightning, Name = "Chain Lightning", Short = "CL", Grant = StatType.GrantChainLightning, RollsOn = "staves",
                Spell = true, Element = SkillElement.Lightning, BaseDamage = 8f,
                ManaCost = 8.5f, Cooldown = 1.1f, Color = new Color(1f, 0.95f, 0.4f),
                Description = "Strikes the enemy under your cursor (or the nearest) and arcs on to others, one more arc at levels 5 and 9. Hits harder when aimed, and up close."
            },
            new SkillDefinition
            {
                Id = SkillId.IceShard, Name = "Ice Shard", Short = "IS", Grant = StatType.GrantIceShard, RollsOn = "staves",
                Spell = true, Element = SkillElement.Cold, BaseDamage = 6f,
                ManaCost = 5.5f, Cooldown = 0.85f, Color = new Color(0.6f, 0.9f, 1f),
                Description = "Fling three chilling shards of ice in a narrow fan. Each enemy is hit by one shard at most, so the fan is for crowds."
            },
            new SkillDefinition
            {
                Id = SkillId.Teleport, Name = "Teleport", Short = "TP", Grant = StatType.GrantTeleport, RollsOn = "amulets, rings, gloves",
                Spell = true, ManaCost = 18f, Cooldown = 0.6f, Color = new Color(0.75f, 0.45f, 1f),
                Description = "Vanish and reappear where you aim, up to half a screen away - straight through walls, trees and enemies. Costly in mana, but ready again almost at once."
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

        /// <summary>The skill a gear stat grants, or null if it grants none.</summary>
        public static SkillDefinition ForGrant(StatType grant)
        {
            foreach (SkillDefinition s in All)
            {
                if (s.Grant == grant)
                    return s;
            }
            return null;
        }

        /// <summary>Spell damage multiplier from Intelligence: +0.6% per point.</summary>
        public static float SpellMultiplier(int intelligence)
        {
            return 1f + intelligence * 0.006f;
        }
    }
}

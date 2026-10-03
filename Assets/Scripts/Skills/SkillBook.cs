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
        Teleport,
        RaiseSkeletons,
        DeathMark,
        SkeletonMages,
        SpiritWolves,
        BoneGolem,
        GraveRot,
        SplitShot,
        PiercingShot,
        RainOfArrows,
        BurningArrow
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
        /// Raises minions. Its level only grows with summon levels ("+1 to level of all Summon
        /// Skills", "+1 to level of Raise Skeletons"), not with spell levels, and its minions never
        /// use the player's own damage stats (see <see cref="Minion"/>).
        /// </summary>
        public bool Summon => SkillGrants.IsSummon(Grant);

        /// <summary>
        /// A bow skill: toggled on and off from the bar (one at a time, no mana, no cooldown), and
        /// while it's on, the bow's attack is this skill (see PlayerSkills.ReleaseBow).
        /// </summary>
        public bool Bow => SkillGrants.IsBowSkill(Grant);

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
            new SkillDefinition
            {
                Id = SkillId.RaiseSkeletons, Name = "Raise Skeletons", Short = "SKL", Grant = StatType.GrantRaiseSkeletons, RollsOn = "sceptres, grimoires, helmets",
                Spell = true, ManaCost = 14f, Cooldown = 4f, Color = new Color(0.55f, 1f, 0.6f),
                Description = "Raise two skeleton warriors that stay until they are destroyed. At most 2 (+1 at levels 7 and 12). Enemies fight them: their life and blows grow steeply with the skill's level, Minion Life and Minion Damage - without those they fall apart quickly."
            },
            new SkillDefinition
            {
                Id = SkillId.DeathMark, Name = "Death Mark", Short = "DM", Grant = StatType.GrantDeathMark, RollsOn = "grimoires",
                Spell = true, BaseDamage = 3f, ManaCost = 2.5f, Cooldown = 0.8f, Color = new Color(0.55f, 1f, 0.45f),
                Description = "A grimoire's attack: a bolt of grave-light that marks the enemy it strikes. Your minions rush the marked enemy and deal 30% more damage to it (more with Death Mark Effect). The bolt itself barely hurts."
            },
            new SkillDefinition
            {
                Id = SkillId.SkeletonMages, Name = "Skeleton Mages", Short = "SKM", Grant = StatType.GrantSkeletonMages, RollsOn = "sceptres, grimoires, gloves",
                Spell = true, ManaCost = 18f, Cooldown = 5f, Color = new Color(0.5f, 0.85f, 1f),
                Description = "Raise a skeleton mage that hangs back and hurls bolts. At most 1 (+1 at levels 8 and 13). Frail: even more than the warriors it needs Minion Life and levels to last."
            },
            new SkillDefinition
            {
                Id = SkillId.SpiritWolves, Name = "Spirit Wolves", Short = "WLF", Grant = StatType.GrantSpiritWolves, RollsOn = "sceptres, grimoires, boots, amulets",
                Spell = true, ManaCost = 24f, Cooldown = 18f, Color = new Color(0.6f, 0.9f, 1f),
                Description = "Call two ghostly wolves (three from level 8) that hunt for 12 seconds (+0.5s per level). Enemies can't touch them, but they don't hold anything off either."
            },
            new SkillDefinition
            {
                Id = SkillId.BoneGolem, Name = "Bone Golem", Short = "GLM", Grant = StatType.GrantBoneGolem, RollsOn = "sceptres, grimoires, body armour, belts",
                Spell = true, ManaCost = 30f, Cooldown = 30f, Color = new Color(0.92f, 0.88f, 0.7f),
                Description = "Raise a hulking golem of bone for 18 seconds (+1s per level) that taunts the enemies round it into attacking it. Only as sturdy as your investment in minions: an unsupported golem crumbles fast."
            },
            new SkillDefinition
            {
                Id = SkillId.GraveRot, Name = "Grave Rot", Short = "ROT", Grant = StatType.GrantGraveRot, RollsOn = "grimoires, helmets",
                Spell = true, ManaCost = 16f, Cooldown = 6f, Color = Curse.RotColor,
                Description = "Curse the ground where you aim: every enemy there rots for 6 seconds (+0.3s per level), dealing 20% less damage (+1% per level, to you and your minions) and taking 15% more damage from every hit (+1.5% per level)."
            },
            new SkillDefinition
            {
                Id = SkillId.SplitShot, Name = "Split Shot", Short = "SPL", Grant = StatType.GrantSplitShot, RollsOn = "bows, quivers",
                Color = new Color(0.75f, 0.95f, 0.5f),
                Description = "Bow skill (toggle): each shot splits into a wide fan, 2 more arrows (+1 at levels 4 and 8) for 75% damage each (+2.5% per level). Each enemy is hit by one arrow at most."
            },
            new SkillDefinition
            {
                Id = SkillId.PiercingShot, Name = "Piercing Shot", Short = "PRC", Grant = StatType.GrantPiercingShot, RollsOn = "bows, quivers",
                Color = new Color(0.85f, 0.9f, 1f),
                Description = "Bow skill (toggle): a heavy draw that looses an arrow through every enemy in its path, flying 30% further for 110% damage (+5% per level). 10% slower."
            },
            new SkillDefinition
            {
                Id = SkillId.RainOfArrows, Name = "Rain of Arrows", Short = "RAIN", Grant = StatType.GrantRainOfArrows, RollsOn = "bows, quivers",
                Color = new Color(0.95f, 0.8f, 0.45f),
                Description = "Bow skill (toggle): shoot into the sky and 6 arrows (+1 at levels 4 and 8, +2 per extra arrow) rain down where you aim, each hitting what it lands near for 45% damage (+2.5% per level). 15% slower."
            },
            new SkillDefinition
            {
                Id = SkillId.BurningArrow, Name = "Burning Arrow", Short = "BRN", Grant = StatType.GrantBurningArrow, RollsOn = "bows, quivers",
                Color = new Color(1f, 0.5f, 0.2f),
                Description = "Bow skill (toggle): arrows of fire for 100% damage (+4% per level) as Fire, bursting to scorch those around the target, with a 20% chance (+2% per level) to set it burning."
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

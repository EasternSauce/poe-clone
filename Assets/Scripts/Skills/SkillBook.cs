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
        WarCry,
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
        BurningArrow,
        FangStrike,
        VenomArrow,
        VenomSpout,
        SummonViper,
        Pulverize,
        ReapingArc,
        LungingThrust
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
        public string Short;        // legacy abbreviation retained for data compatibility
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
        /// A bow skill: toggled on and off from the bar (one at a time, free toggle), and
        /// while it's on, the bow's attack is this skill (see PlayerSkills.ReleaseBow).
        /// </summary>
        public bool Bow => SkillGrants.IsBowSkill(Grant);

        /// <summary>
        /// Mana per use: movement skills gain at most 8 mana by level 10, including bonus
        /// levels. Other skills cost 40% more per level. Summons get a low-level discount so
        /// a new summoner can replace an early army without exhausting the entire mana pool,
        /// and damage spells get a larger one so an early caster can keep casting; both
        /// discounts fade out by skill level 10.
        /// </summary>
        public float ManaCostAt(int level)
        {
            if (Bow)
                return ManaCost + 0.15f * Mathf.Clamp(level - 1, 0, 9);
            if (Id == SkillId.Dash || Id == SkillId.Teleport)
                return ManaCost + 8f * Mathf.Clamp01((Mathf.Max(1, level) - 1) / 9f);

            float cost = ManaCost * (1f + 0.4f * (Mathf.Max(1, level) - 1));
            if (Summon)
                cost *= Mathf.Lerp(0.6f, 1f, Mathf.Clamp01((level - 1) / 9f));
            else if (DamageSpell)
                cost *= Mathf.Lerp(0.5f, 1f, Mathf.Clamp01((level - 1) / 9f));
            return cost;
        }

        /// <summary>Cooldown (or time between casts for an attack spell) at a given skill level.</summary>
        public float CooldownAt(int level)
        {
            if (Id == SkillId.Dash)
            {
                // Scale uses per second evenly from the level 1 cooldown to 2.2/s at level 10.
                float usesPerSecond = Mathf.Lerp(1f / Cooldown, 2.2f,
                    Mathf.Clamp01((Mathf.Max(1, level) - 1) / 9f));
                return 1f / usesPerSecond;
            }

            float perLevel = Main ? 0.01f : 0.025f;
            return Cooldown * Mathf.Max(0.4f, 1f - perLevel * (Mathf.Max(1, level) - 1));
        }

        /// <summary>A spell that deals damage itself (Fire Bolt, Frost Nova, ...), not a summon, curse or movement spell.</summary>
        public bool DamageSpell => Spell && BaseDamage > 0f && !Summon;

        /// <summary>
        /// A spell's damage at this level before Intelligence and Spell Damage (about 3.2x from
        /// level 1 to 10). Low levels get a bonus (30% at level 1) that fades out by level 10.
        /// </summary>
        public float DamageAt(int level)
        {
            int l = Mathf.Max(1, level);
            return BaseDamage * (1f + 0.35f * (l - 1)) * Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((l - 1) / 9f));
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
        /// <summary>Bar slots: Q E R F, mouse buttons, then 1 2 3 4 (appended to preserve saves).</summary>
        public const int SlotCount = 11;

        /// <summary>The slots the touch layout has round buttons for (the first four).</summary>
        public const int TouchSlotCount = 4;

        public static readonly SkillDefinition[] All =
        {
            new SkillDefinition
            {
                Id = SkillId.Cleave, Name = "Cleave", Short = "CLV", Grant = StatType.GrantCleave, RollsOn = "melee weapons",
                ManaCost = 8f, Cooldown = 3f, Color = new Color(0.85f, 0.75f, 0.55f),
                Description = "Plant your feet for a brief windup, then swing all around you, hitting every enemy in reach for 168% weapon damage (+12% per level). Needs a melee weapon."
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
                Id = SkillId.WarCry, Name = "War Cry", Short = "CRY", Grant = StatType.GrantWarCry, RollsOn = "staves, amulets, belts",
                Spell = true, BaseDamage = 8f, ManaCost = 18f, Cooldown = 10f, Color = new Color(1f, 0.7f, 0.25f),
                Description = "Unleash a physical shockwave around you and gain Onslaught for 4 seconds (+0.2 seconds per level), increasing attack speed, cast speed and movement speed."
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
                Spell = true, ManaCost = 35f, Cooldown = 4f, Color = new Color(0.55f, 1f, 0.6f),
                Description = "Raise two skeleton warriors that stay until destroyed. All summon skills share one army limit, increased by summoner passives and gear. Summon levels improve their strength, not their count."
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
                Spell = true, ManaCost = 45f, Cooldown = 5f, Color = new Color(0.5f, 0.85f, 1f),
                Description = "Raise a skeleton mage that hangs back and hurls bolts. All summon skills share one army limit, increased by summoner passives and gear. Summon levels improve its strength, not its count."
            },
            new SkillDefinition
            {
                Id = SkillId.SpiritWolves, Name = "Spirit Wolves", Short = "WLF", Grant = StatType.GrantSpiritWolves, RollsOn = "sceptres, grimoires, boots, amulets",
                Spell = true, ManaCost = 60f, Cooldown = 18f, Color = new Color(0.6f, 0.9f, 1f),
                Description = "Call a pack of ghostly wolves that hunt briefly. All summon skills share one army limit, increased by summoner passives and gear. Wolves cannot be targeted."
            },
            new SkillDefinition
            {
                Id = SkillId.BoneGolem, Name = "Bone Golem", Short = "GLM", Grant = StatType.GrantBoneGolem, RollsOn = "sceptres, grimoires, body armour, belts",
                Spell = true, ManaCost = 75f, Cooldown = 30f, Color = new Color(0.92f, 0.88f, 0.7f),
                Description = "Raise a hulking golem of bone that taunts nearby enemies. It shares the global army limit with every other summon skill."
            },
            new SkillDefinition
            {
                Id = SkillId.GraveRot, Name = "Grave Rot", Short = "ROT", Grant = StatType.GrantGraveRot, RollsOn = "grimoires, helmets",
                Spell = true, ManaCost = 16f, Cooldown = 6f, Color = Curse.RotColor,
                Description = "Curse the ground where you aim: every enemy there rots for 6 seconds (+0.3s per level), dealing 20% less damage (+1% per level, to you and your minions) and taking 15% more damage from every hit (+1.5% per level)."
            },
            new SkillDefinition
            {
                Id = SkillId.SplitShot, Name = "Split Shot", Short = "SPL", Grant = StatType.GrantSplitShot, RollsOn = "bows, quivers", ManaCost = 3f,
                Color = new Color(0.75f, 0.95f, 0.5f),
                Description = "Bow skill (toggle): each shot splits into a wide fan, 2 more arrows (+1 at levels 4 and 8) for 75% damage each (+2.5% per level). Each enemy is hit by one arrow at most."
            },
            new SkillDefinition
            {
                Id = SkillId.PiercingShot, Name = "Piercing Shot", Short = "PRC", Grant = StatType.GrantPiercingShot, RollsOn = "bows, quivers", ManaCost = 3f,
                Color = new Color(0.85f, 0.9f, 1f),
                Description = "Bow skill (toggle): a heavy draw that looses an arrow through every enemy in its path, flying 30% further for 110% damage (+5% per level). 10% slower."
            },
            new SkillDefinition
            {
                Id = SkillId.RainOfArrows, Name = "Rain of Arrows", Short = "RAIN", Grant = StatType.GrantRainOfArrows, RollsOn = "bows, quivers", ManaCost = 6f,
                Color = new Color(0.95f, 0.8f, 0.45f),
                Description = "Bow skill (toggle): shoot into the sky and 6 arrows (+1 at levels 4 and 8, +2 per extra arrow) rain down where you aim, each hitting what it lands near for 45% damage (+2.5% per level). 15% slower."
            },
            new SkillDefinition
            {
                Id = SkillId.BurningArrow, Name = "Burning Arrow", Short = "BRN", Grant = StatType.GrantBurningArrow, RollsOn = "bows, quivers", ManaCost = 4.5f,
                Color = new Color(1f, 0.5f, 0.2f),
                Description = "Bow skill (toggle): arrows of fire for 100% damage (+4% per level) as Fire, bursting to scorch those around the target, with a 20% chance (+2% per level) to set it burning."
            },
            new SkillDefinition { Id = SkillId.FangStrike, Name = "Fang Strike", Short = "FNG", Grant = StatType.GrantFangStrike, RollsOn = "daggers", ManaCost = 7f, Cooldown = 2f, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Strike the aimed enemy for 156% weapon damage (+9.6% per level) and inflict a potent poison over 3 seconds." },
            new SkillDefinition { Id = SkillId.VenomArrow, Name = "Venom Arrow", Short = "VNM", Grant = StatType.GrantVenomArrow, RollsOn = "bows, quivers", ManaCost = 4.5f, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Bow skill (toggle): attacks inflict a stronger poison and leave a brief venom cloud." },
            new SkillDefinition { Id = SkillId.VenomSpout, Name = "Venom Spout", Short = "SPT", Grant = StatType.GrantVenomSpout, RollsOn = "staves, Shepherd sceptre", Spell = true, BaseDamage = 5f, ManaCost = 14f, Cooldown = 5f, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Erupt poison beneath your target, damaging and poisoning enemies in the area." },
            new SkillDefinition { Id = SkillId.SummonViper, Name = "Summon Viper", Short = "VIP", Grant = StatType.GrantSummonViper, RollsOn = "grimoires, sceptres", Spell = true, ManaCost = 50f, Cooldown = 12f, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Call a viper spirit to fight at your side. It shares the global army limit with every other summon skill." },
            new SkillDefinition { Id = SkillId.Pulverize, Name = "Pulverize", Short = "PVL", Grant = StatType.GrantPulverize, RollsOn = "maces and mauls", ManaCost = 12f, Cooldown = 4.5f, Color = new Color(0.95f, 0.65f, 0.28f), Description = "Plant your feet and bring a mace down in a crushing slam after a heavy windup, dealing 312% weapon damage (+21.6% per level) in a broad area. Maces and mauls only." },
            new SkillDefinition { Id = SkillId.ReapingArc, Name = "Reaping Arc", Short = "RPA", Grant = StatType.GrantReapingArc, RollsOn = "axes", ManaCost = 10f, Cooldown = 3.4f, Color = new Color(0.9f, 0.48f, 0.3f), Description = "Commit to a broad, forward axe sweep. After a deliberate windup, hit enemies in a wide arc for 264% weapon damage (+18% per level). Axes only." },
            new SkillDefinition { Id = SkillId.LungingThrust, Name = "Lunging Thrust", Short = "LTH", Grant = StatType.GrantLungingThrust, RollsOn = "swords", ManaCost = 9f, Cooldown = 2.8f, Color = new Color(0.85f, 0.82f, 0.65f), Description = "Brace, then drive a sword forward in a committed thrust for 288% weapon damage (+19.2% per level). Swords only." },
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

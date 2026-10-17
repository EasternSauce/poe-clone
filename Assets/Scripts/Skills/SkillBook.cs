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
        LungingThrust,
        Whirlwind,
        Fissure,
        Aftershock,
        CrossSlash,
        VenomSlash
    }

    /// <summary>The spell element used by tooltips and damage calculations.</summary>
    public enum SkillElement
    {
        None,
        Fire,
        Cold,
        Lightning
    }

    /// <summary>One skill: its fixed baseline, equipment source and presentation.</summary>
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
        public float[] ManaCosts;   // fixed base cost; array retained for authoring compatibility
        public float Cooldown;      // at level 1; for a staff's attack spell, the time between casts
        public int Charges = 1;     // uses stored up; each full cooldown brings one back
        public float BaseDamage;    // spells: damage at level 1, before Intelligence and Spell Damage
        public Color Color;

        /// <summary>The equipped weapon supplies its main attack and its own specialization tree.</summary>
        public bool Main => WeaponSkillRules.IsWeaponSkill(Grant);

        /// <summary>Rolls on staves (Fire Bolt, Frost Nova, War Cry, ...): costs half at character level 1 (see PlayerSkills.CostOf).</summary>
        public bool StaffSkill => RollsOn != null && RollsOn.StartsWith("staves");

        /// <summary>Summons use their own damage and defences, plus this weapon's allocated nodes.</summary>
        public bool Summon => SkillGrants.IsSummon(Grant);

        /// <summary>A bow skill permanently replaces left click while its weapon is equipped.</summary>
        public bool Bow => SkillGrants.IsBowSkill(Grant);

        /// <summary>
        /// Held rather than repeated: while it's on, it lasts as long as the attack is held, paying
        /// its mana cost every <see cref="PlayerSkills.ChannelTick"/> seconds (see PlayerSkills.Whirlwind.cs).
        /// </summary>
        public bool Channelled => Id == SkillId.Whirlwind;

        /// <summary>A melee weapon skill (Cleave, Pulverize, ...): the one on the weapon in hand is its attack, replacing the swing (see PlayerSkills.Melee.cs).</summary>
        public bool MeleeAttack => !Spell && !Bow && WeaponMultiplierAt(1) > 0f;

        /// <summary>Base mana cost. Skill rank grants tree points and never changes this value.</summary>
        public float ManaCostAt(int level)
        {
            return ManaCosts[0];
        }

        /// <summary>Base cooldown. Speed comes from character stats and allocated tree nodes.</summary>
        public float CooldownAt(int level)
        {
            return Cooldown;
        }

        /// <summary>A spell that deals damage itself (Fire Bolt, Frost Nova, ...), not a summon, curse or movement spell.</summary>
        public bool DamageSpell => Spell && BaseDamage > 0f && !Summon;

        /// <summary>Fixed base spell damage before stats and allocated tree bonuses.</summary>
        public float DamageAt(int level)
        {
            return BaseDamage * (WeaponSkillRules.IsWeaponSkill(Grant) ? 1f : 1.3f);
        }

        /// <summary>Main spells gain base damage with character progression, independently of weapon rank.</summary>
        public float DamageForCharacter(int characterLevel) => DamageAt(1) *
            (Main && DamageSpell ? 1f + .06f * Mathf.Max(0, characterLevel - 1) : 1f);

        /// <summary>Fixed share of weapon damage per hit, independent of skill rank.</summary>
        public float WeaponMultiplierAt(int level)
        {
            switch (Id)
            {
                case SkillId.Cleave: return 1.1f;
                case SkillId.Pulverize: return 1.75f;
                case SkillId.ReapingArc: return 1.4f;
                case SkillId.LungingThrust: return 1.15f;
                case SkillId.Whirlwind: return 0.32f;
                case SkillId.FangStrike: return 0.45f;
                case SkillId.VenomSlash: return 0.38f;
                case SkillId.Fissure: return 1.5f;
                case SkillId.Aftershock: return 1.5f;
                case SkillId.CrossSlash: return 0.4f;
                case SkillId.SplitShot: return 0.75f;
                case SkillId.PiercingShot: return 0.75f;
                case SkillId.BurningArrow: return 0.5f;
                case SkillId.VenomArrow: return 0.6f;
                case SkillId.RainOfArrows: return 0.65f;
                default: return 0f;
            }
        }

        /// <summary>The damage type its hits deal, for tooltips.</summary>
        public StatType DamageElement
        {
            get
            {
                if (Id == SkillId.VenomSpout) return StatType.PoisonDamage;
                if (Id == SkillId.BurningArrow) return StatType.FireDamage;
                switch (Element)
                {
                    case SkillElement.Fire: return StatType.FireDamage;
                    case SkillElement.Cold: return StatType.ColdDamage;
                    case SkillElement.Lightning: return StatType.LightningDamage;
                    default: return StatType.PhysicalDamage;
                }
            }
        }
    }

    /// <summary>Skill identities and fixed baselines. Weapon rank grants specialization points.</summary>
    public static class SkillBook
    {
        /// <summary>Bar slots: Q E R F, mouse buttons, then 1 2 3 4 (appended to preserve saves).</summary>
        public const int SlotCount = 11;

        // Shared by live combat and the offline evaluator; values are percentages.
        public const float FangStrikePoison = 30f;
        public const float VenomSlashPoison = 38f;
        public const float VenomArrowPoison = 35f;
        public const float VenomArrowCloud = 10f;

        /// <summary>The slots the touch layout has round buttons for (the first four).</summary>
        public const int TouchSlotCount = 4;

        // Melee skills cost a little per use, not a cooldown skill's price (with a sword; see
        // PlayerSkills.CostOf for other weapons and higher character levels).
        private static readonly float[] LightMeleeCosts = { 3f };
        private static readonly float[] MeleeCosts = { 4f };
        private static readonly float[] HeavyMeleeCosts = { 5.5f };

        public static readonly SkillDefinition[] All =
        {
            new SkillDefinition
            {
                Id = SkillId.Cleave, Name = "Cleave", Short = "CLV", Grant = StatType.GrantCleave, RollsOn = "swords and axes",
                ManaCosts = MeleeCosts, Color = new Color(0.85f, 0.75f, 0.55f),
                Description = "Melee skill (the weapon's attack): plant your feet for a brief windup, then swing all around you, hitting every enemy in reach for 110% weapon damage. 15% slower. Swords and axes only."
            },
            new SkillDefinition
            {
                Id = SkillId.FireBolt, Name = "Fire Bolt", Short = "FB", Grant = StatType.GrantFireBolt, RollsOn = "staves",
                Spell = true, Element = SkillElement.Fire, BaseDamage = 16f,
                ManaCosts = new[] { 3.5f }, Cooldown = 1.0f, Color = new Color(1f, 0.5f, 0.15f),
                Description = "Hurl a bolt of fire that bursts on the first enemy it hits. Each enemy is hit by one bolt at most."
            },
            new SkillDefinition
            {
                Id = SkillId.Dash, Name = "Dash", Short = "DSH", Grant = StatType.GrantDash, RollsOn = "boots",
                ManaCosts = new[] { 6f }, Cooldown = 3.5f, Charges = 3, Color = new Color(0.7f, 0.9f, 0.7f),
                Description = "Dart forward a short distance, out of trouble or into the fight. Its weapon tree supplies improvements."
            },
            new SkillDefinition
            {
                Id = SkillId.FrostNova, Name = "Frost Nova", Short = "FN", Grant = StatType.GrantFrostNova, RollsOn = "staves, helmets, gloves",
                Spell = true, Element = SkillElement.Cold, BaseDamage = 6f,
                ManaCosts = new[] { 10f }, Cooldown = 8f, Color = new Color(0.55f, 0.85f, 1f),
                Description = "A ring of frost bursts from you, damaging and slowing nearby enemies."
            },
            new SkillDefinition
            {
                Id = SkillId.WarCry, Name = "War Cry", Short = "CRY", Grant = StatType.GrantWarCry, RollsOn = "staves, amulets, belts",
                Spell = true, BaseDamage = 8f, ManaCosts = new[] { 9f }, Cooldown = 10f, Color = new Color(1f, 0.7f, 0.25f),
                Description = "Unleash a physical shockwave around you and gain Onslaught for 4 seconds, increasing attack speed, cast speed and movement speed."
            },
            new SkillDefinition
            {
                Id = SkillId.ChainLightning, Name = "Chain Lightning", Short = "CL", Grant = StatType.GrantChainLightning, RollsOn = "staves",
                Spell = true, Element = SkillElement.Lightning, BaseDamage = 22f,
                ManaCosts = new[] { 4.2f }, Cooldown = 1.1f, Color = new Color(1f, 0.95f, 0.4f),
                Description = "Strikes the enemy under your cursor (or the nearest). Starts with no chains; allocate chaining passives to arc to other enemies. Hits harder when aimed, and up close."
            },
            new SkillDefinition
            {
                Id = SkillId.IceShard, Name = "Ice Shard", Short = "IS", Grant = StatType.GrantIceShard, RollsOn = "staves",
                Spell = true, Element = SkillElement.Cold, BaseDamage = 18f,
                ManaCosts = new[] { 2.8f }, Cooldown = 0.85f, Color = new Color(0.6f, 0.9f, 1f),
                Description = "Fling a chilling shard of ice. Frozen Fan, beside the tree origin, grants +2 projectiles for one point. Each enemy is hit by one shard per volley at most."
            },
            new SkillDefinition
            {
                Id = SkillId.Teleport, Name = "Teleport", Short = "TP", Grant = StatType.GrantTeleport, RollsOn = "amulets, rings, gloves",
                Spell = true, ManaCosts = new[] { 18f }, Cooldown = 0.6f, Color = new Color(0.75f, 0.45f, 1f),
                Description = "Vanish and reappear where you aim, up to half a screen away - straight through walls, trees and enemies. Costly in mana, but ready again almost at once."
            },
            new SkillDefinition
            {
                Id = SkillId.RaiseSkeletons, Name = "Raise Skeletons", Short = "SKL", Grant = StatType.GrantRaiseSkeletons, RollsOn = "sceptres",
                Spell = true, ManaCosts = new[] { 21f }, Cooldown = 4f, Color = new Color(0.55f, 1f, 0.6f),
                Description = "Raise two skeleton warriors that stay until destroyed, up to 2 at once. Weapon tree nodes raise the limit. Only one kind of lasting minion can serve you: raising skeleton warriors dismisses your skeleton mages."
            },
            new SkillDefinition
            {
                Id = SkillId.DeathMark, Name = "Death Mark", Short = "DM", Grant = StatType.GrantDeathMark, RollsOn = "grimoires",
                Spell = true, BaseDamage = 3f, ManaCosts = new[] { 3.1f }, Cooldown = 0.8f, Color = new Color(0.55f, 1f, 0.45f),
                Description = "Support ability from grimoires: mark an enemy for 6 seconds. Your minions rush it and deal 30% more damage to it (more with Death Mark Effect). The bolt itself barely hurts."
            },
            new SkillDefinition
            {
                Id = SkillId.SkeletonMages, Name = "Skeleton Mages", Short = "SKM", Grant = StatType.GrantSkeletonMages, RollsOn = "sceptres",
                Spell = true, ManaCosts = new[] { 27f }, Cooldown = 5f, Color = new Color(0.5f, 0.85f, 1f),
                Description = "Raise a skeleton mage that hangs back and hurls bolts and stays until destroyed, up to 1 at once. Weapon tree nodes raise the limit. Only one kind of lasting minion can serve you: raising skeleton mages dismisses your skeleton warriors."
            },
            new SkillDefinition
            {
                Id = SkillId.SpiritWolves, Name = "Spirit Wolves", Short = "WLF", Grant = StatType.GrantSpiritWolves, RollsOn = "sceptres",
                Spell = true, ManaCosts = new[] { 36f }, Cooldown = 18f, Color = new Color(0.6f, 0.9f, 1f),
                Description = "Call a pack of 2 ghostly wolves that hunt briefly. Wolves cannot be targeted."
            },
            new SkillDefinition
            {
                Id = SkillId.BoneGolem, Name = "Bone Golem", Short = "GLM", Grant = StatType.GrantBoneGolem, RollsOn = "sceptres",
                Spell = true, ManaCosts = new[] { 45f }, Cooldown = 30f, Color = new Color(0.92f, 0.88f, 0.7f),
                Description = "Raise a hulking golem of bone that taunts nearby enemies. One golem before weapon tree improvements."
            },
            new SkillDefinition
            {
                Id = SkillId.GraveRot, Name = "Grave Rot", Short = "ROT", Grant = StatType.GrantGraveRot, RollsOn = "grimoires, helmets",
                Spell = true, ManaCosts = new[] { 16f }, Cooldown = 6f, Color = Curse.RotColor,
                Description = "Curse the ground where you aim: every enemy there rots for 6 seconds, dealing 20% less damage."
            },
            new SkillDefinition
            {
                Id = SkillId.SplitShot, Name = "Split Shot", Short = "SPL", Grant = StatType.GrantSplitShot, RollsOn = "bows", ManaCosts = new[] { 3f },
                Color = new Color(0.75f, 0.95f, 0.5f),
                Description = "Bow skill (the weapon's attack): each shot splits into a wide fan, A base fan of 3 arrows, each dealing 75% weapon damage. Each enemy is hit by one arrow at most."
            },
            new SkillDefinition
            {
                Id = SkillId.PiercingShot, Name = "Piercing Shot", Short = "PRC", Grant = StatType.GrantPiercingShot, RollsOn = "bows", ManaCosts = new[] { 3f },
                Color = new Color(0.85f, 0.9f, 1f),
                Description = "Bow skill (the weapon's attack): hold attack to charge in place, then release a piercing sniper arrow. Damage grows from 75% to 225% weapon damage over a full draw. 80% increased projectile range. A distinct cue signals full charge."
            },
            new SkillDefinition
            {
                Id = SkillId.RainOfArrows, Name = "Rain of Arrows", Short = "RAIN", Grant = StatType.GrantRainOfArrows, RollsOn = "bows", ManaCosts = new[] { 6f },
                Color = new Color(0.95f, 0.8f, 0.45f),
                Description = "Bow skill (the weapon's attack): fire one salvo of 6 arrows into the aimed area. Each arrow deals 65% weapon damage and can overlap other impacts. Additional arrows add to the salvo; area of effect enlarges impacts, not spread. 15% reduced attack speed. Delayed salvos require passives."
            },
            new SkillDefinition
            {
                Id = SkillId.BurningArrow, Name = "Burning Arrow", Short = "BRN", Grant = StatType.GrantBurningArrow, RollsOn = "bows", ManaCosts = new[] { 4.5f },
                Color = new Color(1f, 0.5f, 0.2f),
                Description = "Bow skill (the weapon's attack): arrows of fire for 50% weapon damage as fire, bursting to scorch those around the target, with a 20% chance to set it burning."
            },
            new SkillDefinition { Id = SkillId.FangStrike, Name = "Fang Strike", Short = "FNG", Grant = StatType.GrantFangStrike, RollsOn = "daggers", ManaCosts = LightMeleeCosts, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Melee skill (the weapon's attack): strike the aimed enemy for 45% weapon damage and inflict a poison that deals 30% weapon damage over 3 seconds. Poisons stack. Daggers only." },
            new SkillDefinition { Id = SkillId.VenomArrow, Name = "Venom Arrow", Short = "VNM", Grant = StatType.GrantVenomArrow, RollsOn = "bows", ManaCosts = new[] { 4.5f }, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Bow skill (the weapon's attack): attacks inflict a stronger poison and leave a brief venom cloud." },
            new SkillDefinition { Id = SkillId.VenomSpout, Name = "Venom Spout", Short = "SPT", Grant = StatType.GrantVenomSpout, RollsOn = "staves", Spell = true, BaseDamage = 14f, ManaCosts = new[] { 7f }, Cooldown = 1.3f, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Erupt poison beneath your target, damaging and poisoning enemies in the area." },
            new SkillDefinition { Id = SkillId.SummonViper, Name = "Summon Viper", Short = "VIP", Grant = StatType.GrantSummonViper, RollsOn = "sceptres", Spell = true, ManaCosts = new[] { 30f }, Cooldown = 12f, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Call a viper spirit to fight at your side, up to 1 at once. Weapon tree nodes raise the limit." },
            new SkillDefinition { Id = SkillId.Pulverize, Name = "Pulverize", Short = "PVL", Grant = StatType.GrantPulverize, RollsOn = "maces and mauls", ManaCosts = HeavyMeleeCosts, Color = new Color(0.95f, 0.65f, 0.28f), Description = "Melee skill (the weapon's attack): plant your feet and bring a mace down in a crushing slam after a heavy windup, dealing 175% weapon damage in a broad area. 30% slower. Maces and mauls only." },
            new SkillDefinition { Id = SkillId.ReapingArc, Name = "Reaping Arc", Short = "RPA", Grant = StatType.GrantReapingArc, RollsOn = "axes", ManaCosts = MeleeCosts, Color = new Color(0.9f, 0.48f, 0.3f), Description = "Melee skill (the weapon's attack): commit to a broad, forward axe sweep. After a deliberate windup, hit enemies in a wide arc for 140% weapon damage. 20% slower. Axes only." },
            new SkillDefinition { Id = SkillId.Whirlwind, Name = "Whirlwind", Short = "WHL", Grant = StatType.GrantWhirlwind, RollsOn = "swords and axes", ManaCosts = new[] { 2.5f }, Color = new Color(0.8f, 0.85f, 0.95f), Description = "Melee skill (the weapon's attack): hold the attack to spin with your weapon out, hitting every enemy in reach for 32% weapon damage 4 times a second with a sword (slower weapons hit less often, faster ones more), faster with attack speed. While spinning you pass through enemies and are carried towards the cursor, faster the further away it is, up to sprinting speed. Costs mana every half second until you let go or run dry. Swords and axes only." },
            new SkillDefinition { Id = SkillId.Fissure, Name = "Fissure", Short = "FIS", Grant = StatType.GrantFissure, RollsOn = "maces and mauls", ManaCosts = HeavyMeleeCosts, Color = new Color(0.86f, 0.55f, 0.3f), Description = "Melee skill (the weapon's attack): slam the ground and split it open in a fissure that races ahead of you, hitting every enemy along its path for 150% weapon damage. 25% slower. Maces and mauls only." },
            new SkillDefinition { Id = SkillId.Aftershock, Name = "Aftershock", Short = "AFT", Grant = StatType.GrantAftershock, RollsOn = "maces and mauls", ManaCosts = HeavyMeleeCosts, Color = new Color(0.95f, 0.78f, 0.4f), Description = "Melee skill (the weapon's attack): slam the ground and send 3 widening tremors rolling ahead, each dealing 150% weapon damage. 25% slower. Maces and mauls only." },
            new SkillDefinition { Id = SkillId.CrossSlash, Name = "Cross Slash", Short = "XSL", Grant = StatType.GrantCrossSlash, RollsOn = "daggers", ManaCosts = MeleeCosts, Color = new Color(0.75f, 0.9f, 1f), Description = "Melee skill (the weapon's attack): two quick crossing cuts carve an X around you, each hitting every enemy along its line, in front and behind, for 40% weapon damage. 10% slower. Daggers only." },
            new SkillDefinition { Id = SkillId.VenomSlash, Name = "Venom Slash", Short = "VSL", Grant = StatType.GrantVenomSlash, RollsOn = "daggers", ManaCosts = LightMeleeCosts, Color = new Color(0.55f, 0.85f, 0.25f), Description = "Melee skill (the weapon's attack): a quick, wide slash that hits every enemy in front of you for 38% weapon damage and poisons each one for 38% weapon damage over 3 seconds. Poisons stack. Daggers only." },
            new SkillDefinition { Id = SkillId.LungingThrust, Name = "Lunging Thrust", Short = "LTH", Grant = StatType.GrantLungingThrust, RollsOn = "swords", ManaCosts = MeleeCosts, Color = new Color(0.85f, 0.82f, 0.65f), Description = "Melee skill (the weapon's attack): brace, then lunge at the aimed enemy (up to a few steps away) and drive a sword into it for 115% weapon damage. 15% slower. Swords only." },
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

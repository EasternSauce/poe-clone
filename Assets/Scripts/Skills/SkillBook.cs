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
        public float[] ManaCosts;   // cost at levels 1 to 10 (see ManaCostAt)
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
        /// Held rather than repeated: while it's on, it lasts as long as the attack is held, paying
        /// its mana cost every <see cref="PlayerSkills.ChannelTick"/> seconds (see PlayerSkills.Whirlwind.cs).
        /// </summary>
        public bool Channelled => Id == SkillId.Whirlwind;

        /// <summary>A melee weapon skill (Cleave, Pulverize, ...): toggled like a bow skill, it replaces the weapon's swing (see PlayerSkills.Melee.cs).</summary>
        public bool MeleeAttack => !Spell && !Bow && WeaponMultiplierAt(1) > 0f;

        /// <summary>
        /// Toggled on and off from the bar instead of used (bow and melee skills, one at a time):
        /// while it's on, the weapon's attack is this skill. No cooldown; it costs a little per use.
        /// </summary>
        public bool Toggled => Bow || MeleeAttack;

        /// <summary>
        /// Mana per use at a skill level, before the character-level multiplier (see
        /// PlayerSkills.CostOf), read from <see cref="ManaCosts"/>. Levels 1-4 stay cheap so early
        /// characters can keep using skills; damage spells climb steeply to level 7 and gently after,
        /// summons climb evenly (a level 15 Bone Golem costs 300 at character level 34). Past level 10
        /// (bonus levels) every skill keeps climbing by its last step, from level 9 to 10.
        /// </summary>
        public float ManaCostAt(int level)
        {
            int l = Mathf.Max(1, level);
            int top = ManaCosts.Length;
            if (l <= top)
                return ManaCosts[l - 1];
            return ManaCosts[top - 1] + (ManaCosts[top - 1] - ManaCosts[top - 2]) * (l - top);
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
        /// A spell's damage at this level before Intelligence and Spell Damage (about 2.8x from
        /// level 1 to 10, then half as fast per bonus level). Low levels get a bonus (30% at
        /// level 1) that fades out by level 10.
        /// </summary>
        public float DamageAt(int level)
        {
            int l = Mathf.Max(1, level);
            float growth = 1f + 0.3f * Mathf.Min(l - 1, 9) + 0.15f * Mathf.Max(0, l - 10);
            return BaseDamage * growth * Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((l - 1) / 9f));
        }

        /// <summary>
        /// A weapon skill's share of the weapon's damage per hit (per arrow for bow skills) at this
        /// level, e.g. Cleave 1.68 (+0.12 per level). 0 for skills that don't use the weapon.
        /// </summary>
        public float WeaponMultiplierAt(int level)
        {
            int l = Mathf.Max(1, level) - 1;
            switch (Id)
            {
                case SkillId.Cleave: return 1.1f + 0.045f * l;
                case SkillId.Pulverize: return 1.75f + 0.08f * l;
                case SkillId.ReapingArc: return 1.4f + 0.06f * l;
                case SkillId.LungingThrust: return 1.65f + 0.07f * l;
                case SkillId.Whirlwind: return 0.32f + 0.018f * l;
                case SkillId.FangStrike: return 0.9f + 0.04f * l;
                case SkillId.VenomSlash: return 0.6f + 0.025f * l;
                case SkillId.Fissure: return 1.5f + 0.065f * l;
                case SkillId.Aftershock: return 0.92f + 0.04f * l;
                case SkillId.CrossSlash: return 1.15f + 0.05f * l;
                case SkillId.SplitShot: return 1.2f + 0.04f * l;
                case SkillId.PiercingShot: return 1.76f + 0.08f * l;
                case SkillId.BurningArrow: return 1.6f + 0.064f * l;
                case SkillId.VenomArrow: return 1.44f + 0.064f * l;
                case SkillId.RainOfArrows: return 0.72f + 0.04f * l;
                default: return 0f;
            }
        }

        /// <summary>A spell's hit multiplier on top of <see cref="DamageAt"/> (Venom Spout's eruption grows 12% per level).</summary>
        public float ExtraHitMultiplierAt(int level) =>
            Id == SkillId.VenomSpout ? 1f + 0.12f * (Mathf.Max(1, level) - 1) : 1f;

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

    /// <summary>
    /// Every skill in the game. Skills come from gear, not from character levels: each piece of
    /// gear can carry skills at a level (1 to 10 as drops, higher with "+N to level" gear), and a
    /// higher level hits harder, costs a little more and comes back sooner. A staff always carries
    /// a spell, which is its attack; any other skill goes in one of the bar slots (Q E R F and
    /// the spare mouse buttons) and costs mana. Spells have a cooldown; bow and melee skills are
    /// toggled instead, and while one is on it replaces the weapon's attack.
    /// Damage spells scale with Intelligence, Cleave with the weapon and Strength.
    /// </summary>
    public static class SkillBook
    {
        /// <summary>Bar slots: Q E R F, mouse buttons, then 1 2 3 4 (appended to preserve saves).</summary>
        public const int SlotCount = 11;

        /// <summary>The slots the touch layout has round buttons for (the first four).</summary>
        public const int TouchSlotCount = 4;

        // Melee skills cost a little per use, not a cooldown skill's price (with a sword; see
        // PlayerSkills.CostOf for other weapons and higher character levels).
        private static readonly float[] LightMeleeCosts = { 3f, 3.2f, 3.3f, 3.5f, 4.2f, 5f, 5.9f, 6.1f, 6.3f, 6.5f };
        private static readonly float[] MeleeCosts = { 4f, 4.2f, 4.3f, 4.5f, 5.4f, 6.4f, 7.5f, 7.7f, 7.9f, 8.2f };
        private static readonly float[] HeavyMeleeCosts = { 5.5f, 5.7f, 5.8f, 5.9f, 7.1f, 8.3f, 9.6f, 9.8f, 10f, 10.2f };

        public static readonly SkillDefinition[] All =
        {
            new SkillDefinition
            {
                Id = SkillId.Cleave, Name = "Cleave", Short = "CLV", Grant = StatType.GrantCleave, RollsOn = "swords and axes",
                ManaCosts = MeleeCosts, Color = new Color(0.85f, 0.75f, 0.55f),
                Description = "Melee skill (toggle): plant your feet for a brief windup, then swing all around you, hitting every enemy in reach for 110% weapon damage (+4.5% per level). 15% slower. Swords and axes only."
            },
            new SkillDefinition
            {
                Id = SkillId.FireBolt, Name = "Fire Bolt", Short = "FB", Grant = StatType.GrantFireBolt, RollsOn = "staves",
                Spell = true, Element = SkillElement.Fire, BaseDamage = 7f,
                ManaCosts = new[] { 3.5f, 5.4f, 7.7f, 10.3f, 15.3f, 21.8f, 29.8f, 31.6f, 33.4f, 35.2f }, Cooldown = 1.0f, Color = new Color(1f, 0.5f, 0.15f),
                Description = "Hurl a bolt of fire that bursts on the first enemy it hits. Fires an additional bolt at skill levels 6 and 12. Each enemy is hit by one bolt at most."
            },
            new SkillDefinition
            {
                Id = SkillId.Dash, Name = "Dash", Short = "DSH", Grant = StatType.GrantDash, RollsOn = "boots",
                ManaCosts = new[] { 6f, 6.9f, 7.8f, 8.7f, 11.1f, 13.9f, 17f, 18.3f, 19.7f, 21f }, Cooldown = 3.5f, Color = new Color(0.7f, 0.9f, 0.7f),
                Description = "Dart forward a short distance, out of trouble or into the fight. Goes further each level."
            },
            new SkillDefinition
            {
                Id = SkillId.FrostNova, Name = "Frost Nova", Short = "FN", Grant = StatType.GrantFrostNova, RollsOn = "staves, helmets, gloves",
                Spell = true, Element = SkillElement.Cold, BaseDamage = 6f,
                ManaCosts = new[] { 10f, 15.6f, 22f, 29.3f, 43.8f, 62.2f, 85f, 90.1f, 95.2f, 100.3f }, Cooldown = 8f, Color = new Color(0.55f, 0.85f, 1f),
                Description = "A ring of frost bursts from you, damaging and slowing nearby enemies."
            },
            new SkillDefinition
            {
                Id = SkillId.WarCry, Name = "War Cry", Short = "CRY", Grant = StatType.GrantWarCry, RollsOn = "staves, amulets, belts",
                Spell = true, BaseDamage = 8f, ManaCosts = new[] { 9f, 14f, 19.8f, 26.4f, 39.4f, 56f, 76.5f, 81.1f, 85.7f, 90.3f }, Cooldown = 10f, Color = new Color(1f, 0.7f, 0.25f),
                Description = "Unleash a physical shockwave around you and gain Onslaught for 4 seconds (+0.2 seconds per level), increasing attack speed, cast speed and movement speed."
            },
            new SkillDefinition
            {
                Id = SkillId.ChainLightning, Name = "Chain Lightning", Short = "CL", Grant = StatType.GrantChainLightning, RollsOn = "staves",
                Spell = true, Element = SkillElement.Lightning, BaseDamage = 8f,
                ManaCosts = new[] { 4.2f, 6.6f, 9.4f, 12.5f, 18.6f, 26.4f, 36.1f, 38.3f, 40.4f, 42.6f }, Cooldown = 1.1f, Color = new Color(1f, 0.95f, 0.4f),
                Description = "Strikes the enemy under your cursor (or the nearest) and arcs on to others, one more arc at levels 5 and 9. Hits harder when aimed, and up close."
            },
            new SkillDefinition
            {
                Id = SkillId.IceShard, Name = "Ice Shard", Short = "IS", Grant = StatType.GrantIceShard, RollsOn = "staves",
                Spell = true, Element = SkillElement.Cold, BaseDamage = 6f,
                ManaCosts = new[] { 2.8f, 4.3f, 6f, 8.1f, 12f, 17.1f, 23.4f, 24.8f, 26.2f, 27.6f }, Cooldown = 0.85f, Color = new Color(0.6f, 0.9f, 1f),
                Description = "Fling three chilling shards of ice in a narrow fan. Each enemy is hit by one shard at most, so the fan is for crowds."
            },
            new SkillDefinition
            {
                Id = SkillId.Teleport, Name = "Teleport", Short = "TP", Grant = StatType.GrantTeleport, RollsOn = "amulets, rings, gloves",
                Spell = true, ManaCosts = new[] { 18f, 18.9f, 19.8f, 20.7f, 25.1f, 29.9f, 35f, 36.3f, 37.7f, 39f }, Cooldown = 0.6f, Color = new Color(0.75f, 0.45f, 1f),
                Description = "Vanish and reappear where you aim, up to half a screen away - straight through walls, trees and enemies. Costly in mana, but ready again almost at once."
            },
            new SkillDefinition
            {
                Id = SkillId.RaiseSkeletons, Name = "Raise Skeletons", Short = "SKL", Grant = StatType.GrantRaiseSkeletons, RollsOn = "sceptres, grimoires, helmets",
                Spell = true, ManaCosts = new[] { 21f, 26.2f, 31.3f, 36.5f, 41.7f, 46.8f, 52f, 57.2f, 62.3f, 67.5f }, Cooldown = 4f, Color = new Color(0.55f, 1f, 0.6f),
                Description = "Raise two skeleton warriors that stay until destroyed, up to 2 at once (+1 at levels 7 and 13). Maximum Minions from passives and gear raises the limit. Only one kind of lasting minion can serve you: raising skeleton warriors dismisses your skeleton mages."
            },
            new SkillDefinition
            {
                Id = SkillId.DeathMark, Name = "Death Mark", Short = "DM", Grant = StatType.GrantDeathMark, RollsOn = "grimoires",
                Spell = true, BaseDamage = 3f, ManaCosts = new[] { 3.1f, 4.9f, 7.3f, 9.6f, 14.3f, 20.3f, 27.6f, 29.1f, 30.9f, 32.5f }, Cooldown = 0.8f, Color = new Color(0.55f, 1f, 0.45f),
                Description = "A grimoire's attack: a bolt of grave-light that marks the enemy it strikes. Your minions rush the marked enemy and deal 30% more damage to it (more with Death Mark Effect). The bolt itself barely hurts."
            },
            new SkillDefinition
            {
                Id = SkillId.SkeletonMages, Name = "Skeleton Mages", Short = "SKM", Grant = StatType.GrantSkeletonMages, RollsOn = "sceptres, grimoires, gloves",
                Spell = true, ManaCosts = new[] { 27f, 33.6f, 40.3f, 46.9f, 53.6f, 60.2f, 66.9f, 73.5f, 80.1f, 86.8f }, Cooldown = 5f, Color = new Color(0.5f, 0.85f, 1f),
                Description = "Raise a skeleton mage that hangs back and hurls bolts and stays until destroyed, up to 1 at once (+1 at levels 7 and 13). Maximum Minions from passives and gear raises the limit. Only one kind of lasting minion can serve you: raising skeleton mages dismisses your skeleton warriors."
            },
            new SkillDefinition
            {
                Id = SkillId.SpiritWolves, Name = "Spirit Wolves", Short = "WLF", Grant = StatType.GrantSpiritWolves, RollsOn = "sceptres, grimoires, boots, amulets",
                Spell = true, ManaCosts = new[] { 36f, 44.9f, 53.7f, 62.6f, 71.4f, 80.3f, 89.1f, 98f, 106.9f, 115.7f }, Cooldown = 18f, Color = new Color(0.6f, 0.9f, 1f),
                Description = "Call a pack of 2 ghostly wolves (+1 at levels 7 and 13, and with Maximum Minions) that hunt briefly. Wolves cannot be targeted."
            },
            new SkillDefinition
            {
                Id = SkillId.BoneGolem, Name = "Bone Golem", Short = "GLM", Grant = StatType.GrantBoneGolem, RollsOn = "sceptres, grimoires, body armour, belts",
                Spell = true, ManaCosts = new[] { 45f, 56.1f, 67.1f, 78.2f, 89.3f, 100.4f, 111.4f, 122.5f, 133.6f, 144.6f }, Cooldown = 30f, Color = new Color(0.92f, 0.88f, 0.7f),
                Description = "Raise a hulking golem of bone that taunts nearby enemies. Only one golem can stand at a time."
            },
            new SkillDefinition
            {
                Id = SkillId.GraveRot, Name = "Grave Rot", Short = "ROT", Grant = StatType.GrantGraveRot, RollsOn = "grimoires, helmets",
                Spell = true, ManaCosts = new[] { 16f, 22.4f, 28.8f, 35.2f, 48.5f, 64f, 81.6f, 91.2f, 100.8f, 110.4f }, Cooldown = 6f, Color = Curse.RotColor,
                Description = "Curse the ground where you aim: every enemy there rots for 6 seconds (+0.3s per level), dealing 20% less damage (+1% per level, to you and your minions) and taking 15% more damage from every hit (+1.5% per level)."
            },
            new SkillDefinition
            {
                Id = SkillId.SplitShot, Name = "Split Shot", Short = "SPL", Grant = StatType.GrantSplitShot, RollsOn = "bows, quivers", ManaCosts = new[] { 3f, 3.2f, 3.3f, 3.5f, 4.2f, 5f, 5.9f, 6.1f, 6.3f, 6.5f },
                Color = new Color(0.75f, 0.95f, 0.5f),
                Description = "Bow skill (toggle): each shot splits into a wide fan, 2 more arrows (+1 at levels 4 and 8) for 120% damage each (+4% per level). Each enemy is hit by one arrow at most."
            },
            new SkillDefinition
            {
                Id = SkillId.PiercingShot, Name = "Piercing Shot", Short = "PRC", Grant = StatType.GrantPiercingShot, RollsOn = "bows, quivers", ManaCosts = new[] { 3f, 3.2f, 3.3f, 3.5f, 4.2f, 5f, 5.9f, 6.1f, 6.3f, 6.5f },
                Color = new Color(0.85f, 0.9f, 1f),
                Description = "Bow skill (toggle): a heavy draw that looses an arrow through every enemy in its path, flying 30% further for 176% damage (+8% per level). 10% slower."
            },
            new SkillDefinition
            {
                Id = SkillId.RainOfArrows, Name = "Rain of Arrows", Short = "RAIN", Grant = StatType.GrantRainOfArrows, RollsOn = "bows, quivers", ManaCosts = new[] { 6f, 6.2f, 6.3f, 6.4f, 7.7f, 9f, 10.4f, 10.6f, 10.8f, 11f },
                Color = new Color(0.95f, 0.8f, 0.45f),
                Description = "Bow skill (toggle): shoot into the sky and 6 arrows (+1 at levels 4 and 8, +2 per extra arrow) rain down where you aim, each hitting what it lands near for 72% damage (+4% per level). 15% slower."
            },
            new SkillDefinition
            {
                Id = SkillId.BurningArrow, Name = "Burning Arrow", Short = "BRN", Grant = StatType.GrantBurningArrow, RollsOn = "bows, quivers", ManaCosts = new[] { 4.5f, 4.7f, 4.8f, 4.9f, 5.9f, 7f, 8.1f, 8.3f, 8.5f, 8.8f },
                Color = new Color(1f, 0.5f, 0.2f),
                Description = "Bow skill (toggle): arrows of fire for 160% damage (+6.4% per level) as Fire, bursting to scorch those around the target, with a 20% chance (+2% per level) to set it burning."
            },
            new SkillDefinition { Id = SkillId.FangStrike, Name = "Fang Strike", Short = "FNG", Grant = StatType.GrantFangStrike, RollsOn = "daggers", ManaCosts = LightMeleeCosts, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Melee skill (toggle): strike the aimed enemy for 90% weapon damage (+4% per level) and inflict a potent poison that deals 95% weapon damage (+7% per level) over 3 seconds. Poisons stack. Daggers only." },
            new SkillDefinition { Id = SkillId.VenomArrow, Name = "Venom Arrow", Short = "VNM", Grant = StatType.GrantVenomArrow, RollsOn = "bows, quivers", ManaCosts = new[] { 4.5f, 4.7f, 4.8f, 4.9f, 5.9f, 7f, 8.1f, 8.3f, 8.5f, 8.8f }, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Bow skill (toggle): attacks inflict a stronger poison and leave a brief venom cloud." },
            new SkillDefinition { Id = SkillId.VenomSpout, Name = "Venom Spout", Short = "SPT", Grant = StatType.GrantVenomSpout, RollsOn = "staves, Shepherd sceptre", Spell = true, BaseDamage = 5f, ManaCosts = new[] { 7f, 10.9f, 15.4f, 20.5f, 30.7f, 43.6f, 59.5f, 63.1f, 66.6f, 70.2f }, Cooldown = 5f, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Erupt poison beneath your target, damaging and poisoning enemies in the area." },
            new SkillDefinition { Id = SkillId.SummonViper, Name = "Summon Viper", Short = "VIP", Grant = StatType.GrantSummonViper, RollsOn = "grimoires, sceptres", Spell = true, ManaCosts = new[] { 30f, 37.4f, 44.8f, 52.1f, 59.5f, 66.9f, 74.3f, 81.7f, 89f, 96.4f }, Cooldown = 12f, Color = new Color(0.48f, 0.9f, 0.22f), Description = "Call a viper spirit to fight at your side, up to 1 at once (+1 at levels 7 and 13). Maximum Minions from passives and gear raises the limit." },
            new SkillDefinition { Id = SkillId.Pulverize, Name = "Pulverize", Short = "PVL", Grant = StatType.GrantPulverize, RollsOn = "maces and mauls", ManaCosts = HeavyMeleeCosts, Color = new Color(0.95f, 0.65f, 0.28f), Description = "Melee skill (toggle): plant your feet and bring a mace down in a crushing slam after a heavy windup, dealing 175% weapon damage (+8% per level) in a broad area. 30% slower. Maces and mauls only." },
            new SkillDefinition { Id = SkillId.ReapingArc, Name = "Reaping Arc", Short = "RPA", Grant = StatType.GrantReapingArc, RollsOn = "axes", ManaCosts = MeleeCosts, Color = new Color(0.9f, 0.48f, 0.3f), Description = "Melee skill (toggle): commit to a broad, forward axe sweep. After a deliberate windup, hit enemies in a wide arc for 140% weapon damage (+6% per level). 20% slower. Axes only." },
            new SkillDefinition { Id = SkillId.Whirlwind, Name = "Whirlwind", Short = "WHL", Grant = StatType.GrantWhirlwind, RollsOn = "swords and axes", ManaCosts = new[] { 2.5f, 3.1f, 3.7f, 4.3f, 5.8f, 7.5f, 9.4f, 10.4f, 11.4f, 12.4f }, Color = new Color(0.8f, 0.85f, 0.95f), Description = "Melee skill (toggle): while it's on, hold the attack to spin with your weapon out, hitting every enemy in reach for 32% weapon damage (+1.8% per level) 4 times a second with a sword (slower weapons hit less often, faster ones more), faster with attack speed. While spinning you pass through enemies and are carried towards the cursor, faster the further away it is, up to sprinting speed. Costs mana every half second until you let go or run dry. Swords and axes only." },
            new SkillDefinition { Id = SkillId.Fissure, Name = "Fissure", Short = "FIS", Grant = StatType.GrantFissure, RollsOn = "maces and mauls", ManaCosts = HeavyMeleeCosts, Color = new Color(0.86f, 0.55f, 0.3f), Description = "Melee skill (toggle): slam the ground and split it open in a fissure that races ahead of you, hitting every enemy along its path for 150% weapon damage (+6.5% per level). 25% slower. Maces and mauls only." },
            new SkillDefinition { Id = SkillId.Aftershock, Name = "Aftershock", Short = "AFT", Grant = StatType.GrantAftershock, RollsOn = "maces and mauls", ManaCosts = HeavyMeleeCosts, Color = new Color(0.95f, 0.78f, 0.4f), Description = "Melee skill (toggle): slam the ground and send 3 tremors (+1 at level 6) rolling ahead of you, each further out and wider than the last, each hitting what it lands on for 92% weapon damage (+4% per level). 25% slower. Maces and mauls only." },
            new SkillDefinition { Id = SkillId.CrossSlash, Name = "Cross Slash", Short = "XSL", Grant = StatType.GrantCrossSlash, RollsOn = "daggers", ManaCosts = MeleeCosts, Color = new Color(0.75f, 0.9f, 1f), Description = "Melee skill (toggle): two quick crossing cuts carve an X around you, each hitting every enemy along its line, in front and behind, for 115% weapon damage (+5% per level). 10% slower. Daggers only." },
            new SkillDefinition { Id = SkillId.VenomSlash, Name = "Venom Slash", Short = "VSL", Grant = StatType.GrantVenomSlash, RollsOn = "daggers", ManaCosts = LightMeleeCosts, Color = new Color(0.55f, 0.85f, 0.25f), Description = "Melee skill (toggle): a quick, wide slash that hits every enemy in front of you for 60% weapon damage (+2.5% per level) and poisons each one for 70% weapon damage (+5% per level) over 3 seconds. Poisons stack. Daggers only." },
            new SkillDefinition { Id = SkillId.LungingThrust, Name = "Lunging Thrust", Short = "LTH", Grant = StatType.GrantLungingThrust, RollsOn = "swords", ManaCosts = MeleeCosts, Color = new Color(0.85f, 0.82f, 0.65f), Description = "Melee skill (toggle): brace, then lunge at the aimed enemy (up to a few steps away) and drive a sword into it for 165% weapon damage (+7% per level). 15% slower. Swords only." },
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

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PoeClone.Inventory
{
    /// <summary>Everything an item can modify, and everything the character sheet shows.</summary>
    public enum StatType
    {
        Strength,
        Dexterity,
        Intelligence,
        MaxLife,
        MaxMana,
        Armour,
        Evasion,
        BlockChance,
        PhysicalDamage,
        AttackSpeed,
        FireResistance,
        ColdResistance,
        LightningResistance,
        MovementSpeed,

        // Special stats: rare on gear (uniques) and on the tree's keystones, each changing how the
        // character plays rather than adding a bit more of something. Saved by number: add at the end.
        AdditionalArrows,           // bow attacks loose this many extra arrows in a spread
        AdditionalSpellProjectiles, // Fire Bolt and Ice Shard fire this many extra projectiles
        SpellDamage,                // % increased spell damage
        AreaOfEffect,               // % increased area of Frost Nova, Cleave and Fire Bolt's burst
        LifeRegen,                  // life regenerated per second
        ManaRegen,                  // % increased mana regeneration
        LifeLeech,                  // % of attack damage dealt returned as life
        ManaAbsorb,                 // % more of every hit taken from mana before life (Mind over Matter)
        AdditionalChains,           // Chain Lightning arcs this many more times
        ChillOnHit,                 // % chance for attacks to chill
        CullingStrike,              // 1: hits kill non-boss enemies left under 10% life
        LifeOnKill,                 // life gained for each enemy killed
        MeleeRange,                 // % increased reach of melee attacks and Cleave

        // Skills granted by gear (see SkillGrants): the value is the skill's level. A weapon's
        // staff's first spell becomes its attack. Saved by number like everything above: add at the end.
        GrantCleave,
        GrantFireBolt,
        GrantDash,
        GrantFrostNova,
        GrantWarCry,
        GrantChainLightning,
        GrantIceShard,

        AllSpellLevels,             // +N to the level of every spell granted by gear
        FireSpellLevels,
        ColdSpellLevels,
        LightningSpellLevels,
        CastSpeed,                  // % faster casts and skill cooldowns
        LegacyCooldownRecovery,     // reserved saved ID; migrated to CastSpeed

        // Mostly from the passive tree (see HitEffects for what each does in a fight).
        FireDamage,                 // % increased fire damage
        ColdDamage,                 // % increased cold damage
        LightningDamage,            // % increased lightning damage
        IgniteChance,               // % chance for fire hits to set the enemy burning
        ShockChance,                // % chance for lightning hits to shock (takes more damage for a while)
        DamageVsChilled,            // % increased damage against chilled enemies
        CriticalChance,             // % increased attack critical strike chance, scaling weapon base chance
        CriticalMultiplier,         // % added to the critical damage multiplier
        AttackDamage,               // % increased damage with attacks
        Damage,                     // % increased damage of every kind
        DamageWhileLowLife,         // % increased damage while under half life
        IncreasedLife,              // % increased maximum life
        IncreasedMana,              // % increased maximum mana
        PercentLifeRegen,           // % of maximum life regenerated per second
        ManaOnKill,                 // mana gained for each enemy killed
        OnslaughtOnKill,            // % chance on kill to gain Onslaught (faster attacks, casts and movement)
        ExplodeOnKill,              // % chance for a killed enemy to explode, burning those around it

        // Skill synergies (keystones): each makes two of the game's skills or effects work together.
        GlacialStep,                // 1: Dash ends in a Frost Nova
        Shatter,                    // % chance for a chilled enemy killed to burst into ice, chilling and hurting those near
        SecondWind,                 // 1: War Cry also refills mana and extends Onslaught
        Stormblade,                 // % chance for an attack critical strike to arc lightning to nearby enemies

        GrantTeleport,              // a skill grant like the ones above (added later, so it sits here)
        GrantRaiseSkeletons,

        // The summoner (see Skills.Minion): a grimoire's Death Mark, the summons, and what makes
        // minions stronger. Minions only care about these minion stats, never the player's own
        // damage stats, so a summoner build has to commit to them: without them minions are
        // flimsy helpers, not a wall to hide behind.
        GrantDeathMark,             // a grimoire's attack: marks an enemy for the minions
        GrantSkeletonMages,
        GrantSpiritWolves,
        GrantBoneGolem,
        MinionDamage,               // % increased minion damage
        MinionLife,                 // % increased minion life
        MinionSpeed,                // % increased minion attack and movement speed
        MinionLevels,               // +N to the level of every summon skill
        RaiseSkeletonsLevels,       // +N to the level of Raise Skeletons
        SkeletonMagesLevels,
        SpiritWolvesLevels,
        BoneGolemLevels,
        AdditionalSkeletons,        // rare unique: +N to per-type cap for warriors and mages
        MarkEffect,                 // % increased effect of Death Mark (the extra damage minions deal to it)
        MinionDuration,             // % increased duration of Spirit Wolves and the Bone Golem
        SoulBond,                   // % of minion damage dealt returned to the player as life
        DeathsHerald,               // 1: a marked enemy that dies bursts and passes the mark on
        BoneArmour,                 // % less damage taken by minions
        GrantGraveRot,              // a grimoire's curse (a bar skill)

        // Bow skills (see SkillGrants.BowSkills): on bows and quivers, toggled on the bar, and while
        // one is on it is what the bow shoots. They are attacks: everything that improves bow
        // attacks (Additional Arrows, attack speed and damage) improves them too.
        GrantSplitShot,
        GrantPiercingShot,
        GrantRainOfArrows,
        GrantBurningArrow,
        ExtraArrowChance,           // retired (arrows are never left to chance); kept so saved stat ids stay put
        PoisonOnHit,               // % of attack damage dealt again as poison over three seconds
        VenomCloudOnHit,            // % of attack damage per second in a venom cloud
        FirePenetration,            // percentage points subtracted from enemy fire resistance
        ColdPenetration,            // percentage points subtracted from enemy cold resistance
        LightningPenetration,        // percentage points subtracted from enemy lightning resistance
        ElementalPenetration,        // percentage points subtracted from all elemental resistances
        ArmourPenetration,           // % of enemy armour ignored by physical hits
        PoisonResistance,
        PoisonPenetration,
        PoisonDamage,
        DamageOverTime,
        GrantFangStrike,
        GrantVenomArrow,
        GrantVenomSpout,
        GrantSummonViper,
        AdditionalMinions,
        MinionDamagePenalty,
        GrantPulverize,
        GrantReapingArc,
        GrantLungingThrust,
        MinionArmour,               // flat armour for minions against physical hits
        MinionResistances,          // fire, cold, lightning and poison resistance for minions
        BowDamage,                  // % increased bow attack damage
        AvoidStun,                  // % chance to take a hit without being staggered
        HealthPotionRecovery,       // % increased life restored by health potions
        OnslaughtOnHealthPotion,    // 1: drinking a health potion grants Onslaught for 3 seconds

        // Append only: saved items store the numeric IDs above.
        BloodMagic,
        IronReflexes,
        PointBlank,
        AdditionalProjectiles,     // all projectile skills; does not add arrows to a basic bow shot
        MoreLife,
        MoreDamage,
        MoreAttackDamage,
        MoreSpellDamage,
        MoreBowDamage,
        MoreFireDamage,
        MoreColdDamage,
        MoreLightningDamage,
        MorePoisonDamage,
        MoreMinionDamage,
        LifeOnAttackHit,           // flat life gained per enemy hit by the player's attacks

        // Unique effects (see HitEffects, PlayerStats and PlayerSkills for what each does).
        PhysicalToFire,             // 1: attacks deal Fire damage instead of Physical
        PhysicalToCold,             // 1: attacks deal Cold damage instead of Physical
        PhysicalToLightning,        // 1: attacks deal Lightning damage instead of Physical
        SpellEcho,                  // % chance for a damaging spell to cast again for free
        MeleeSplash,                // % of a melee attack's damage dealt to enemies around each target
        Rampage,                    // % more damage per kill in the last 4 seconds (up to 10 kills)
        DamageTaken,                // % more damage taken (negative: less)
        ManaAsDamage,               // hits deal extra damage equal to this % of maximum mana
        BlockRetaliation,           // blocking deals this % of Armour as Physical damage around you
        SpellLeech,                 // % of spell damage dealt returned as life
        OnslaughtAtFullLife,        // 1: Onslaught while on full life
        CurseOnHit,                 // % chance for hits to curse the enemy with Grave Rot
        NoLifeRegen,                // 1: life does not regenerate
        ManaCostReduction,          // % less mana (or life) spent on skills
        LifePercentOnKill           // % of maximum life recovered on kill
    }

    /// <summary>
    /// The stats that grant a skill (their value is its level). Pure data, so the item side knows
    /// which grants exist without knowing the skills themselves (those live in Skills.SkillBook).
    /// </summary>
    public static class SkillGrants
    {
        /// <summary>Highest level a skill drops at (gear "+N levels" can still push it past this).</summary>
        public const int MaxDropLevel = 10;

        /// <summary>Spells a staff's attack can be: one always rolls on every staff.</summary>
        public static readonly StatType[] StaffMain = { StatType.GrantFireBolt, StatType.GrantChainLightning, StatType.GrantIceShard };

        public static bool IsGrant(StatType stat)
        {
            return (stat >= StatType.GrantCleave && stat <= StatType.GrantIceShard) || stat == StatType.GrantTeleport ||
                   stat == StatType.GrantRaiseSkeletons || (stat >= StatType.GrantDeathMark && stat <= StatType.GrantBoneGolem) ||
                   stat == StatType.GrantGraveRot || IsBowSkill(stat) ||
                   (stat >= StatType.GrantFangStrike && stat <= StatType.GrantSummonViper) ||
                   (stat >= StatType.GrantPulverize && stat <= StatType.GrantLungingThrust);
        }

        /// <summary>The bow skills: they roll on bows and quivers (never as the attack) and are toggled from the bar.</summary>
        public static readonly StatType[] BowSkills =
            { StatType.GrantSplitShot, StatType.GrantPiercingShot, StatType.GrantRainOfArrows, StatType.GrantBurningArrow };

        public static bool IsBowSkill(StatType stat)
        {
            return Array.IndexOf(BowSkills, stat) >= 0 || stat == StatType.GrantVenomArrow;
        }

        /// <summary>The attack spells a grimoire carries (one always rolls on every grimoire).</summary>
        public static readonly StatType[] GrimoireMain = { StatType.GrantDeathMark };

        /// <summary>The summon skills (bar skills that raise minions).</summary>
        public static bool IsSummon(StatType stat)
        {
            return stat == StatType.GrantRaiseSkeletons || (stat >= StatType.GrantSkeletonMages && stat <= StatType.GrantBoneGolem) || stat == StatType.GrantSummonViper;
        }

        /// <summary>The "+N to level of this summon" stat for a summon grant (null for anything else).</summary>
        public static StatType? SummonLevelStat(StatType grant)
        {
            switch (grant)
            {
                case StatType.GrantRaiseSkeletons: return StatType.RaiseSkeletonsLevels;
                case StatType.GrantSkeletonMages: return StatType.SkeletonMagesLevels;
                case StatType.GrantSpiritWolves: return StatType.SpiritWolvesLevels;
                case StatType.GrantBoneGolem: return StatType.BoneGolemLevels;
                default: return null;
            }
        }

        /// <summary>
        /// A spammable spell that, on a staff, replaces its plain attack (the first one on the staff
        /// does; any further one is an ordinary skill for the bar).
        /// </summary>
        public static bool IsMain(StatType stat)
        {
            return Array.IndexOf(StaffMain, stat) >= 0 || Array.IndexOf(GrimoireMain, stat) >= 0;
        }

        public static string SkillName(StatType grant)
        {
            switch (grant)
            {
                case StatType.GrantCleave: return "Cleave";
                case StatType.GrantFireBolt: return "Fire Bolt";
                case StatType.GrantDash: return "Dash";
                case StatType.GrantFrostNova: return "Frost Nova";
                case StatType.GrantWarCry: return "War Cry";
                case StatType.GrantChainLightning: return "Chain Lightning";
                case StatType.GrantIceShard: return "Ice Shard";
                case StatType.GrantTeleport: return "Teleport";
                case StatType.GrantRaiseSkeletons: return "Raise Skeletons";
                case StatType.GrantDeathMark: return "Death Mark";
                case StatType.GrantSkeletonMages: return "Skeleton Mages";
                case StatType.GrantSpiritWolves: return "Spirit Wolves";
                case StatType.GrantBoneGolem: return "Bone Golem";
                case StatType.GrantGraveRot: return "Grave Rot";
                case StatType.GrantSplitShot: return "Split Shot";
                case StatType.GrantPiercingShot: return "Piercing Shot";
                case StatType.GrantRainOfArrows: return "Rain of Arrows";
                case StatType.GrantBurningArrow: return "Burning Arrow";
                case StatType.GrantFangStrike: return "Fang Strike";
                case StatType.GrantVenomArrow: return "Venom Arrow";
                case StatType.GrantVenomSpout: return "Venom Spout";
                case StatType.GrantSummonViper: return "Summon Viper";
                case StatType.GrantPulverize: return "Pulverize";
                case StatType.GrantReapingArc: return "Reaping Arc";
                case StatType.GrantLungingThrust: return "Lunging Thrust";
                default: return grant.ToString();
            }
        }

        /// <summary>
        /// The skill level a drop of this item level rolls: up to 1 at item level 1, up to 10 at
        /// item level 37 (the Shed Sanctuary), with weaker eligible tiers more common than the strongest ones.
        /// </summary>
        public static int RollLevel(Random rng, int itemLevel)
        {
            return ItemGenerator.RollSkillLevel(rng, itemLevel);
        }

        /// <summary>
        /// Bow skills use the same level gates as other skills: stronger ranks unlock in later areas.
        /// </summary>
        public static int RollBowLevel(Random rng, int itemLevel)
        {
            return RollLevel(rng, itemLevel);
        }
    }

    /// <summary>One line of an item's stats: "+30 Armour" is (Armour, 30).</summary>
    public readonly struct StatModifier
    {
        public StatType Stat { get; }
        public float Value { get; }
        // Positive: rolled tier (T1 strongest); -1: implicit; 0: legacy/untiered.
        public int Tier { get; }

        public StatModifier(StatType stat, float value, int tier = 0)
        {
            Stat = stat == StatType.LegacyCooldownRecovery ? StatType.CastSpeed : stat;
            Value = value;
            Tier = tier;
        }
    }

    /// <summary>The character's own stats before gear, plus the level/XP shown on the sheet.</summary>
    public sealed class BaseStats
    {
        private readonly float[] values = new float[Enum.GetValues(typeof(StatType)).Length];

        public int Level = 1;
        public int Experience;
        public int ExperienceRequired = 200;

        public float Get(StatType stat)
        {
            return values[(int)stat];
        }

        public BaseStats Set(StatType stat, float value)
        {
            values[(int)(stat == StatType.LegacyCooldownRecovery ? StatType.CastSpeed : stat)] = value;
            return this;
        }
    }

    /// <summary>
    /// The character's final stats: base + everything worn + what attributes grant.
    /// Pure C# (no Unity types) so it can be unit tested.
    /// </summary>
    public sealed class StatSheet
    {
        // Each point of an attribute from gear also grants a little of a related stat.
        public const float LifePerStrength = 0.5f;
        public const float ManaPerIntelligence = 0.5f;
        public const float EvasionPerDexterity = 1f;

        public const float ResistanceCap = 75f;
        public const float BlockCap = 75f;

        private readonly float[] baseValues;
        private readonly float[] gear;
        private readonly float[] derived;
        private readonly float[] multipliers;

        public WeaponType Weapon { get; }
        public float BaseAttackCriticalChance => WeaponCriticalChance(Weapon);
        public float AttackCriticalChance => Math.Max(0f, Math.Min(100f,
            BaseAttackCriticalChance * (1f + Total(StatType.CriticalChance) / 100f)));

        // Weapon identity determines base crit; it is not an item modifier or tooltip line.
        public static float WeaponCriticalChance(WeaponType weapon)
        {
            switch (weapon)
            {
                case WeaponType.Dagger: return 8f;
                case WeaponType.Sword: return 6f;
                case WeaponType.Greatsword: return 5.5f;
                case WeaponType.Bow: return 5f;
                case WeaponType.Axe: return 5f;
                case WeaponType.Greataxe: return 4.5f;
                case WeaponType.Staff: return 4f;
                case WeaponType.Sceptre: return 4f;
                case WeaponType.Mace: return 4f;
                case WeaponType.Maul: return 3f;
                default: return 3f;
            }
        }

        private static bool IsMore(StatType stat) => stat == StatType.MoreLife ||
            (stat >= StatType.MoreDamage && stat <= StatType.MoreMinionDamage);

        private void AddModifier(StatModifier modifier)
        {
            gear[(int)modifier.Stat] += modifier.Value;
            if (IsMore(modifier.Stat))
                multipliers[(int)modifier.Stat] *= Math.Max(0f, 1f + modifier.Value / 100f);
            else if (modifier.Stat == StatType.MinionDamagePenalty || modifier.Stat == StatType.BoneArmour)
                multipliers[(int)modifier.Stat] *= Math.Max(0f, 1f - modifier.Value / 100f);
        }

        /// <summary>Each separate more/less source multiplies independently, including identical stat types.</summary>
        public float Multiplier(StatType stat) => multipliers[(int)stat];

        /// <summary>Increased cast speed from all sources adds together.</summary>
        public float CastRateMultiplier => Math.Max(0.5f, 1f + Total(StatType.CastSpeed) / 100f);

        public float DamageMultiplier(bool attack, bool bow, bool lowLife, bool chilled,
            StatType element, bool damageOverTime = false, bool spell = true)
        {
            float increased = Total(StatType.Damage);
            float more = Multiplier(StatType.MoreDamage);
            if (attack)
            {
                increased += Total(StatType.AttackDamage) + Total(StatType.Strength);
                more *= Multiplier(StatType.MoreAttackDamage);
                if (bow)
                {
                    increased += Total(StatType.BowDamage);
                    more *= Multiplier(StatType.MoreBowDamage);
                }
            }
            else if (!damageOverTime && spell)
            {
                increased += Total(StatType.SpellDamage) + Total(StatType.Intelligence) * 0.6f;
                more *= Multiplier(StatType.MoreSpellDamage);
            }
            if (element == StatType.FireDamage || element == StatType.ColdDamage ||
                element == StatType.LightningDamage || element == StatType.PoisonDamage)
                increased += Total(element);
            if (element == StatType.FireDamage) more *= Multiplier(StatType.MoreFireDamage);
            else if (element == StatType.ColdDamage) more *= Multiplier(StatType.MoreColdDamage);
            else if (element == StatType.LightningDamage) more *= Multiplier(StatType.MoreLightningDamage);
            else if (element == StatType.PoisonDamage) more *= Multiplier(StatType.MorePoisonDamage);
            if (damageOverTime) increased += Total(StatType.DamageOverTime);
            if (lowLife) increased += Total(StatType.DamageWhileLowLife);
            if (chilled) increased += Total(StatType.DamageVsChilled);
            return Math.Max(0f, 1f + increased / 100f) * more;
        }

        public static float PointBlankMultiplier(float distance)
        {
            // Game distances: full bonus inside 3 units, neutral at 8, full penalty at 16.
            if (distance <= 3f) return 1.3f;
            if (distance <= 8f) return 1.3f - (distance - 3f) * 0.06f;
            return Math.Max(0.7f, 1f - (distance - 8f) * 0.0375f);
        }

        public int Level { get; }
        public int Experience { get; }
        public int ExperienceRequired { get; }

        private StatSheet(BaseStats baseStats, EquipmentSet equipment, IEnumerable<StatModifier> extra)
        {
            int count = Enum.GetValues(typeof(StatType)).Length;
            baseValues = new float[count];
            gear = new float[count];
            derived = new float[count];
            multipliers = new float[count];
            Weapon = equipment?.Get(EquipSlot.MainHand)?.WeaponType ?? WeaponType.Unarmed;

            for (int i = 0; i < count; i++)
            {
                baseValues[i] = baseStats.Get((StatType)i);
                multipliers[i] = IsMore((StatType)i) ? Math.Max(0f, 1f + baseValues[i] / 100f) :
                    (StatType)i == StatType.MinionDamagePenalty || (StatType)i == StatType.BoneArmour
                        ? Math.Max(0f, 1f - baseValues[i] / 100f) : 1f;
            }

            Level = baseStats.Level;
            Experience = baseStats.Experience;
            ExperienceRequired = baseStats.ExperienceRequired;

            if (equipment != null)
            {
                foreach (EquipSlot slot in SlotRules.AllSlots)
                {
                    ItemData item = equipment.Get(slot);
                    if (item == null)
                        continue;

                    foreach (StatModifier m in item.Modifiers)
                        AddModifier(m);
                }
            }

            // Passives count like gear (and their attributes grant the same life/mana/evasion).
            if (extra != null)
            {
                foreach (StatModifier m in extra)
                    AddModifier(m);
            }

            derived[(int)StatType.MaxLife] = (float)Math.Floor(gear[(int)StatType.Strength] * LifePerStrength);
            derived[(int)StatType.MaxMana] = (float)Math.Floor(gear[(int)StatType.Intelligence] * ManaPerIntelligence);
            derived[(int)StatType.Evasion] = (float)Math.Floor(gear[(int)StatType.Dexterity] * EvasionPerDexterity);

            // "% increased" life and mana scale the whole pool, the character's own included; the
            // extra counts as coming from gear so the character picks it up with the rest.
            Increase(StatType.MaxLife, StatType.IncreasedLife);
            Increase(StatType.MaxMana, StatType.IncreasedMana);

            float life = Base(StatType.MaxLife) + FromGear(StatType.MaxLife);
            derived[(int)StatType.MaxLife] += (float)Math.Floor(life * (Multiplier(StatType.MoreLife) - 1f));
            if (Total(StatType.BloodMagic) > 0f)
                derived[(int)StatType.MaxMana] = -Base(StatType.MaxMana) - Gear(StatType.MaxMana);
            if (Total(StatType.IronReflexes) > 0f)
            {
                // Dexterity's evasion bonus is lost; item/passive evasion becomes armour.
                derived[(int)StatType.Armour] += Math.Max(0f, Base(StatType.Evasion) + Gear(StatType.Evasion));
                derived[(int)StatType.Evasion] = -Base(StatType.Evasion) - Gear(StatType.Evasion);
            }
        }

        private void Increase(StatType pool, StatType percent)
        {
            float increased = baseValues[(int)percent] + gear[(int)percent];
            if (increased == 0f)
                return;
            float whole = baseValues[(int)pool] + gear[(int)pool] + derived[(int)pool];
            derived[(int)pool] += (float)Math.Floor(whole * Math.Max(-0.9f, increased / 100f));
        }

        public static StatSheet Build(BaseStats baseStats, EquipmentSet equipment, IEnumerable<StatModifier> extra = null)
        {
            if (baseStats == null)
                throw new ArgumentNullException(nameof(baseStats));

            if (equipment == null || !equipment.EnforceRequirements)
                return new StatSheet(baseStats, equipment, extra);

            // Own attributes count. Start with the full loadout, remove unmet items and repeat
            // so losing one attribute source also disables any gear that depended on it.
            // Rebuild from all worn gear each time so qualifying items reactivate automatically.
            var modifiers = extra == null ? null : new List<StatModifier>(extra);
            var active = new EquipmentSet();
            var slots = new HashSet<EquipSlot>();
            foreach (EquipSlot slot in SlotRules.AllSlots)
            {
                ItemData item = equipment.Get(slot);
                if (item != null && active.Restore(slot, item, out _)) slots.Add(slot);
            }
            StatSheet sheet = new StatSheet(baseStats, active, modifiers);
            bool changed;
            do
            {
                changed = false;
                foreach (EquipSlot slot in SlotRules.AllSlots)
                {
                    ItemData item = active.Get(slot);
                    if (item == null || item.Requirements.MetBy(sheet)) continue;
                    active.Unequip(slot);
                    slots.Remove(slot);
                    changed = true;
                    sheet = new StatSheet(baseStats, active, modifiers);
                }
            } while (changed);
            equipment.SetActiveSlots(slots);
            return sheet;
        }

        /// <summary>The character's own value, before gear.</summary>
        public float Base(StatType stat)
        {
            return baseValues[(int)stat];
        }

        /// <summary>The total granted directly by worn items.</summary>
        public float Gear(StatType stat)
        {
            return gear[(int)stat];
        }

        /// <summary>What the stat gets indirectly from gear attributes (e.g. life from strength).</summary>
        public float Derived(StatType stat)
        {
            return derived[(int)stat];
        }

        /// <summary>Everything gear adds to this stat, directly or through attributes.</summary>
        public float FromGear(StatType stat)
        {
            return Gear(stat) + Derived(stat);
        }

        /// <summary>The final value, with resistance and block caps applied.</summary>
        public float Total(StatType stat)
        {
            if (IsMore(stat)) return (Multiplier(stat) - 1f) * 100f;
            if (stat == StatType.MinionDamagePenalty || stat == StatType.BoneArmour)
                return (1f - Multiplier(stat)) * 100f;
            float total = Base(stat) + FromGear(stat);

            switch (stat)
            {
                case StatType.FireResistance:
                case StatType.ColdResistance:
                case StatType.LightningResistance:
                case StatType.PoisonResistance:
                    return Math.Min(ResistanceCap, total);
                case StatType.BlockChance:
                    return Math.Min(BlockCap, total);
                default:
                    return total;
            }
        }
    }

    /// <summary>Turns stats into the text shown on tooltips and the character page.</summary>
    public static class StatFormatter
    {
        public static string Label(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return "Strength";
                case StatType.Dexterity: return "Dexterity";
                case StatType.Intelligence: return "Intelligence";
                case StatType.MaxLife: return "Maximum Life";
                case StatType.MaxMana: return "Maximum Mana";
                case StatType.Armour: return "Armour";
                case StatType.Evasion: return "Evasion Rating";
                case StatType.BlockChance: return "Attack Block Chance";
                case StatType.AvoidStun: return "Chance to Avoid Stun";
                case StatType.HealthPotionRecovery: return "Health Potion Recovery";
                case StatType.OnslaughtOnHealthPotion: return "Onslaught on Health Potion";
                case StatType.PhysicalDamage: return "Physical Damage";
                case StatType.AttackSpeed: return "Attack Speed";
                case StatType.FireResistance: return "Fire Resistance";
                case StatType.ColdResistance: return "Cold Resistance";
                case StatType.LightningResistance: return "Lightning Resistance";
                case StatType.PoisonResistance: return "Poison Resistance";
                case StatType.PoisonPenetration: return "Poison Penetration";
                case StatType.PoisonDamage: return "Poison Damage";
                case StatType.DamageOverTime: return "Damage over Time";
                case StatType.FirePenetration: return "Fire Penetration";
                case StatType.ColdPenetration: return "Cold Penetration";
                case StatType.LightningPenetration: return "Lightning Penetration";
                case StatType.ElementalPenetration: return "Elemental Penetration";
                case StatType.ArmourPenetration: return "Armour Penetration";
                case StatType.MovementSpeed: return "Movement Speed";
                case StatType.AdditionalArrows: return "Additional Arrows";
                case StatType.AdditionalSpellProjectiles: return "Additional Spell Projectiles";
                case StatType.SpellDamage: return "Spell Damage";
                case StatType.AreaOfEffect: return "Area of Effect";
                case StatType.LifeRegen: return "Life Regeneration";
                case StatType.ManaRegen: return "Mana Regeneration";
                case StatType.LifeLeech: return "Life Leech";
                case StatType.ManaAbsorb: return "Damage Taken from Mana";
                case StatType.AdditionalChains: return "Additional Chains";
                case StatType.ChillOnHit: return "Chance to Chill";
                case StatType.CullingStrike: return "Culling Strike";
                case StatType.LifeOnKill: return "Life on Kill";
                case StatType.LifeOnAttackHit: return "Life on Attack Hit";
                case StatType.MeleeRange: return "Melee Range";
                case StatType.AllSpellLevels: return "Spell Levels";
                case StatType.FireSpellLevels: return "Fire Spell Levels";
                case StatType.ColdSpellLevels: return "Cold Spell Levels";
                case StatType.LightningSpellLevels: return "Lightning Spell Levels";
                case StatType.CastSpeed: return "Cast Speed";
                case StatType.LegacyCooldownRecovery: return "Cast Speed";
                case StatType.FireDamage: return "Fire Damage";
                case StatType.ColdDamage: return "Cold Damage";
                case StatType.LightningDamage: return "Lightning Damage";
                case StatType.IgniteChance: return "Chance to Ignite";
                case StatType.ShockChance: return "Chance to Shock";
                case StatType.DamageVsChilled: return "Damage vs Chilled";
                case StatType.CriticalChance: return "Critical Strike Chance";
                case StatType.CriticalMultiplier: return "Critical Multiplier";
                case StatType.AttackDamage: return "Attack Damage";
                case StatType.BowDamage: return "Bow Damage";
                case StatType.Damage: return "Damage";
                case StatType.DamageWhileLowLife: return "Damage on Low Life";
                case StatType.IncreasedLife: return "Increased Life";
                case StatType.IncreasedMana: return "Increased Mana";
                case StatType.PercentLifeRegen: return "Life Regen (% of max)";
                case StatType.ManaOnKill: return "Mana on Kill";
                case StatType.OnslaughtOnKill: return "Onslaught on Kill";
                case StatType.ExplodeOnKill: return "Corpse Explosion";
                case StatType.GlacialStep: return "Glacial Step";
                case StatType.Shatter: return "Shatter";
                case StatType.SecondWind: return "Second Wind";
                case StatType.Stormblade: return "Stormblade";
                case StatType.MinionDamage: return "Minion Damage";
                case StatType.MinionLife: return "Minion Life";
                case StatType.MinionArmour: return "Minion Armour";
                case StatType.MinionResistances: return "Minion Resistances";
                case StatType.MinionSpeed: return "Minion Speed";
                case StatType.MinionLevels: return "Summon Levels";
                case StatType.RaiseSkeletonsLevels: return "Raise Skeletons Levels";
                case StatType.SkeletonMagesLevels: return "Skeleton Mages Levels";
                case StatType.SpiritWolvesLevels: return "Spirit Wolves Levels";
                case StatType.BoneGolemLevels: return "Bone Golem Levels";
                case StatType.AdditionalSkeletons: return "Extra Skeletons";
                case StatType.AdditionalMinions: return "Maximum Minions";
                case StatType.MinionDamagePenalty: return "Minion Damage Penalty";
                case StatType.MarkEffect: return "Death Mark Effect";
                case StatType.MinionDuration: return "Minion Duration";
                case StatType.SoulBond: return "Soul Bond";
                case StatType.DeathsHerald: return "Death's Herald";
                case StatType.BoneArmour: return "Minion Damage Taken";
                case StatType.PoisonOnHit: return "Poison on Hit";
                case StatType.VenomCloudOnHit: return "Venom Clouds";
                case StatType.BloodMagic: return "Blood Magic";
                case StatType.IronReflexes: return "Iron Reflexes";
                case StatType.PointBlank: return "Point Blank";
                case StatType.AdditionalProjectiles: return "Additional Skill Projectiles";
                case StatType.MoreLife: return "More Maximum Life";
                case StatType.MoreDamage: return "More Damage";
                case StatType.MoreAttackDamage: return "More Attack Damage";
                case StatType.MoreSpellDamage: return "More Spell Damage";
                case StatType.MoreBowDamage: return "More Bow Damage";
                case StatType.MoreFireDamage: return "More Fire Damage";
                case StatType.MoreColdDamage: return "More Cold Damage";
                case StatType.MoreLightningDamage: return "More Lightning Damage";
                case StatType.MorePoisonDamage: return "More Poison Damage";
                case StatType.MoreMinionDamage: return "More Minion Damage";
                case StatType.PhysicalToFire: return "Attacks Deal Fire";
                case StatType.PhysicalToCold: return "Attacks Deal Cold";
                case StatType.PhysicalToLightning: return "Attacks Deal Lightning";
                case StatType.SpellEcho: return "Spell Echo";
                case StatType.MeleeSplash: return "Melee Splash";
                case StatType.Rampage: return "Rampage";
                case StatType.DamageTaken: return "Damage Taken";
                case StatType.ManaAsDamage: return "Mana as Damage";
                case StatType.BlockRetaliation: return "Block Retaliation";
                case StatType.SpellLeech: return "Spell Leech";
                case StatType.OnslaughtAtFullLife: return "Onslaught on Full Life";
                case StatType.CurseOnHit: return "Curse on Hit";
                case StatType.NoLifeRegen: return "No Life Regeneration";
                case StatType.ManaCostReduction: return "Skill Cost Reduction";
                case StatType.LifePercentOnKill: return "Life Recovered on Kill";
                default:
                    if (SkillGrants.IsGrant(stat))
                        return SkillGrants.SkillName(stat);
                    return stat.ToString();
            }
        }

        /// <summary>The special stats (see the end of StatType), shown in their own list.</summary>
        public static bool IsSpecial(StatType stat)
        {
            return stat >= StatType.AdditionalArrows;
        }

        /// <summary>Stats that are shown as a percentage.</summary>
        public static bool IsPercent(StatType stat)
        {
            if (stat >= StatType.MoreLife && stat <= StatType.MoreMinionDamage) return true;
            switch (stat)
            {
                case StatType.BlockChance:
                case StatType.AvoidStun:
                case StatType.HealthPotionRecovery:
                case StatType.AttackSpeed:
                case StatType.FireResistance:
                case StatType.ColdResistance:
                case StatType.LightningResistance:
                case StatType.PoisonResistance:
                case StatType.FirePenetration:
                case StatType.ColdPenetration:
                case StatType.LightningPenetration:
                case StatType.ElementalPenetration:
                case StatType.ArmourPenetration:
                case StatType.PoisonPenetration:
                case StatType.PoisonDamage:
                case StatType.DamageOverTime:
                case StatType.MovementSpeed:
                case StatType.SpellDamage:
                case StatType.AreaOfEffect:
                case StatType.ManaRegen:
                case StatType.LifeLeech:
                case StatType.ManaAbsorb:
                case StatType.ChillOnHit:
                case StatType.MeleeRange:
                case StatType.CastSpeed:
                case StatType.LegacyCooldownRecovery:
                case StatType.FireDamage:
                case StatType.ColdDamage:
                case StatType.LightningDamage:
                case StatType.IgniteChance:
                case StatType.ShockChance:
                case StatType.DamageVsChilled:
                case StatType.CriticalChance:
                case StatType.CriticalMultiplier:
                case StatType.AttackDamage:
                case StatType.BowDamage:
                case StatType.Damage:
                case StatType.DamageWhileLowLife:
                case StatType.IncreasedLife:
                case StatType.IncreasedMana:
                case StatType.PercentLifeRegen:
                case StatType.OnslaughtOnKill:
                case StatType.ExplodeOnKill:
                case StatType.Shatter:
                case StatType.Stormblade:
                case StatType.MinionDamage:
                case StatType.MinionDamagePenalty:
                case StatType.MinionLife:
                case StatType.MinionResistances:
                case StatType.MinionSpeed:
                case StatType.MarkEffect:
                case StatType.MinionDuration:
                case StatType.SoulBond:
                case StatType.BoneArmour:
                case StatType.SpellEcho:
                case StatType.MeleeSplash:
                case StatType.Rampage:
                case StatType.DamageTaken:
                case StatType.ManaAsDamage:
                case StatType.BlockRetaliation:
                case StatType.SpellLeech:
                case StatType.CurseOnHit:
                case StatType.ManaCostReduction:
                case StatType.LifePercentOnKill:
                    return true;
                default:
                    return false;
            }
        }

        public static string Number(float value)
        {
            if (Math.Abs(value - Math.Round(value)) < 0.001f)
                return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>A stat line on an item, in Path of Exile wording ("+30 to Armour").</summary>
        public static string ItemLine(StatModifier m)
        {
            string n = Number(Math.Abs(m.Value));
            string sign = m.Value < 0f ? "-" : "+";
            if (m.Stat >= StatType.MoreLife && m.Stat <= StatType.MoreMinionDamage)
                return n + "% " + (m.Value < 0f ? "less " : "more ") + Label(m.Stat).Substring(5);

            switch (m.Stat)
            {
                case StatType.Strength: return sign + n + " to Strength";
                case StatType.Dexterity: return sign + n + " to Dexterity";
                case StatType.Intelligence: return sign + n + " to Intelligence";
                case StatType.MaxLife: return sign + n + " to Maximum Life";
                case StatType.MaxMana: return sign + n + " to Maximum Mana";
                case StatType.Armour: return sign + n + " to Armour";
                case StatType.Evasion: return sign + n + " to Evasion Rating";
                case StatType.BlockChance: return sign + n + "% Chance to Block Attacks";
                case StatType.AvoidStun: return sign + n + "% Chance to Avoid Stun";
                case StatType.HealthPotionRecovery: return n + "% increased Life recovered by Health Potions";
                case StatType.OnslaughtOnHealthPotion: return "Drinking a Health Potion grants Onslaught for 3 seconds";
                case StatType.PhysicalDamage: return "Adds " + n + " Physical Damage";
                case StatType.AttackSpeed: return n + "% " + (m.Value < 0f ? "reduced" : "increased") + " Attack Speed";
                case StatType.FireResistance: return sign + n + "% to Fire Resistance";
                case StatType.ColdResistance: return sign + n + "% to Cold Resistance";
                case StatType.LightningResistance: return sign + n + "% to Lightning Resistance";
                case StatType.PoisonResistance: return sign + n + "% to Poison Resistance";
                case StatType.PoisonPenetration: return n + "% Poison Penetration";
                case StatType.PoisonDamage: return n + "% increased Poison Damage";
                case StatType.DamageOverTime: return n + "% increased Damage over Time";
                case StatType.MovementSpeed: return n + "% " + (m.Value < 0f ? "reduced" : "increased") + " Movement Speed";
                case StatType.AdditionalArrows: return "Bow attacks fire " + n + " additional arrow" + (n == "1" ? "" : "s");
                case StatType.AdditionalSpellProjectiles: return "Fire Bolt and Ice Shard fire " + n + " additional projectile" + (n == "1" ? "" : "s");
                case StatType.SpellDamage: return n + "% increased Spell Damage";
                case StatType.AreaOfEffect: return n + "% increased Area of Effect";
                case StatType.LifeRegen: return "Regenerate " + n + " Life per second";
                case StatType.ManaRegen: return n + "% increased Mana Regeneration";
                case StatType.LifeLeech: return n + "% of Attack Damage Leeched as Life";
                case StatType.ManaAbsorb: return n + "% of Damage is taken from Mana before Life";
                case StatType.AdditionalChains: return "Chain Lightning arcs " + n + " additional time" + (n == "1" ? "" : "s");
                case StatType.ChillOnHit: return n + "% chance to Chill enemies with Attacks";
                case StatType.CullingStrike: return "Culling Strike: kill enemies left below 10% Life";
                case StatType.LifeOnKill: return "Gain " + n + " Life per enemy killed";
                case StatType.LifeOnAttackHit: return "Gain " + n + " Life per enemy hit with an Attack";
                case StatType.MeleeRange: return n + "% increased Melee Range";
                case StatType.AllSpellLevels: return sign + n + " to Level of all Spells";
                case StatType.FireSpellLevels: return sign + n + " to Level of all Fire Spells";
                case StatType.ColdSpellLevels: return sign + n + " to Level of all Cold Spells";
                case StatType.LightningSpellLevels: return sign + n + " to Level of all Lightning Spells";
                case StatType.CastSpeed: return n + "% increased Cast Speed";
                case StatType.LegacyCooldownRecovery: return n + "% increased Cast Speed";
                case StatType.FireDamage: return Increased(m, "Fire Damage");
                case StatType.ColdDamage: return Increased(m, "Cold Damage");
                case StatType.LightningDamage: return Increased(m, "Lightning Damage");
                case StatType.IgniteChance: return n + "% chance to Ignite with Fire hits";
                case StatType.ShockChance: return n + "% chance to Shock with Lightning hits";
                case StatType.DamageVsChilled: return Increased(m, "Damage against Chilled enemies");
                case StatType.CriticalChance: return Increased(m, "Critical Strike Chance");
                case StatType.CriticalMultiplier: return sign + n + "% to Critical Strike Multiplier";
                case StatType.AttackDamage: return Increased(m, "Attack Damage");
                case StatType.BowDamage: return Increased(m, "Bow Damage");
                case StatType.Damage: return Increased(m, "Damage");
                case StatType.DamageWhileLowLife: return Increased(m, "Damage while on Low Life");
                case StatType.IncreasedLife: return Increased(m, "Maximum Life");
                case StatType.IncreasedMana: return Increased(m, "Maximum Mana");
                case StatType.PercentLifeRegen: return "Regenerate " + n + "% of Maximum Life per second";
                case StatType.ManaOnKill: return "Gain " + n + " Mana per enemy killed";
                case StatType.OnslaughtOnKill: return n + "% chance on kill to gain Onslaught";
                case StatType.ExplodeOnKill: return n + "% chance for enemies you kill to explode, dealing 200% of their Maximum Life as Fire damage around them";
                case StatType.GlacialStep: return "Dash ends in a Frost Nova that chills everything around you";
                case StatType.Shatter: return n + "% chance for Chilled enemies you kill to Shatter, dealing Cold damage and chilling those nearby";
                case StatType.SecondWind: return "War Cry also restores a third of your Mana and extends Onslaught by 2 seconds";
                case StatType.Stormblade: return n + "% chance for Attack Critical Strikes to arc Lightning to 3 nearby enemies";
                case StatType.MinionDamage: return Increased(m, "Minion Damage");
                case StatType.MinionLife: return Increased(m, "Minion Life");
                case StatType.MinionArmour: return sign + n + " to Minion Armour";
                case StatType.MinionResistances: return sign + n + "% to Minion Fire, Cold, Lightning and Poison Resistances";
                case StatType.MinionSpeed: return Increased(m, "Minion Attack and Movement Speed");
                case StatType.MinionLevels: return sign + n + " to Level of all Summon Skills";
                case StatType.RaiseSkeletonsLevels: return sign + n + " to Level of Raise Skeletons";
                case StatType.SkeletonMagesLevels: return sign + n + " to Level of Skeleton Mages";
                case StatType.SpiritWolvesLevels: return sign + n + " to Level of Spirit Wolves";
                case StatType.BoneGolemLevels: return sign + n + " to Level of Bone Golem";
                case StatType.AdditionalSkeletons: return sign + n + " to maximum Skeleton Warriors and Skeleton Mages";
                case StatType.AdditionalMinions: return sign + n + " to maximum number of Minions";
                case StatType.MinionDamagePenalty: return "Minions deal " + n + "% less Damage";
                case StatType.MarkEffect: return Increased(m, "effect of Death Mark");
                case StatType.MinionDuration: return Increased(m, "duration of Spirit Wolves and the Bone Golem");
                case StatType.SoulBond: return n + "% of Minion Damage is returned to you as Life";
                case StatType.DeathsHerald: return "When a Marked enemy dies, the Mark leaps to the nearest enemy";
                case StatType.BloodMagic: return "Removes all Mana. Skills cost Life instead of Mana";
                case StatType.IronReflexes: return "Converts Evasion Rating to Armour. Dexterity grants no Evasion Rating";
                case StatType.PointBlank: return "Projectile Hits deal up to 30% more Damage nearby, and up to 30% less Damage at long range";
                case StatType.AdditionalProjectiles: return "Projectile Skills fire " + n + " additional projectile" + (n == "1" ? "" : "s");
                case StatType.MoreLife: return n + "% more Maximum Life";
                case StatType.MoreDamage: return n + "% more Damage";
                case StatType.MoreAttackDamage: return n + "% more Attack Damage";
                case StatType.MoreSpellDamage: return n + "% more Spell Damage";
                case StatType.MoreBowDamage: return n + "% more Bow Damage";
                case StatType.MoreFireDamage: return n + "% more Fire Damage";
                case StatType.MoreColdDamage: return n + "% more Cold Damage";
                case StatType.MoreLightningDamage: return n + "% more Lightning Damage";
                case StatType.MorePoisonDamage: return n + "% more Poison Damage";
                case StatType.MoreMinionDamage: return n + "% more Minion Damage";
                case StatType.PoisonOnHit: return "Attacks inflict Poison for " + n + "% of base damage over 3 seconds";
                case StatType.VenomCloudOnHit: return "Attacks create Venom Clouds dealing " + n + "% of base damage per second";
                case StatType.FirePenetration: return n + "% Fire Penetration";
                case StatType.ColdPenetration: return n + "% Cold Penetration";
                case StatType.LightningPenetration: return n + "% Lightning Penetration";
                case StatType.ElementalPenetration: return n + "% Elemental Penetration";
                case StatType.ArmourPenetration: return n + "% Armour Penetration";
                case StatType.BoneArmour: return "Minions take " + n + "% " + (m.Value < 0f ? "more" : "less") + " damage";
                case StatType.PhysicalToFire: return "Attacks deal Fire damage instead of Physical";
                case StatType.PhysicalToCold: return "Attacks deal Cold damage instead of Physical";
                case StatType.PhysicalToLightning: return "Attacks deal Lightning damage instead of Physical";
                case StatType.SpellEcho: return n + "% chance for damaging Spells to cast a second time for free";
                case StatType.MeleeSplash: return "Melee basic attacks deal " + n + "% of their damage to enemies around each target";
                case StatType.Rampage: return n + "% more Damage for each enemy killed in the last 4 seconds (up to 10)";
                case StatType.DamageTaken: return "You take " + n + "% " + (m.Value < 0f ? "less" : "more") + " Damage";
                case StatType.ManaAsDamage: return "Hits deal extra damage equal to " + n + "% of your Maximum Mana";
                case StatType.BlockRetaliation: return "When you Block, deal " + n + "% of your Armour as Physical damage to enemies around you";
                case StatType.SpellLeech: return n + "% of Spell Damage Leeched as Life";
                case StatType.OnslaughtAtFullLife: return "You have Onslaught while on Full Life";
                case StatType.CurseOnHit: return n + "% chance for Hits to curse enemies with Grave Rot";
                case StatType.NoLifeRegen: return "You cannot Regenerate Life";
                case StatType.ManaCostReduction: return "Skills cost " + n + "% less";
                case StatType.LifePercentOnKill: return "Recover " + n + "% of Maximum Life when you kill an enemy";
                default:
                    if (SkillGrants.IsGrant(m.Stat))
                        return "Grants Level " + n + " " + SkillGrants.SkillName(m.Stat);
                    return sign + n + " " + Label(m.Stat);
            }
        }

        private static string Increased(StatModifier m, string what)
        {
            return Number(Math.Abs(m.Value)) + "% " + (m.Value < 0f ? "reduced " : "increased ") + what;
        }

        /// <summary>A final value on the character page ("42", "20%").</summary>
        public static string Value(StatType stat, float value)
        {
            return Number(value) + (IsPercent(stat) ? "%" : "");
        }
    }
}

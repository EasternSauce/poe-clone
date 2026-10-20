using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    public enum WeaponSkillEffect
    {
        Damage, Speed, Area, Range, Projectiles, Chains, Pierce, Fork, LegacyRepeat,
        Chill, Shock, Ignite, Shatter, Explode, Poison, Cloud, Duration,
        Minions, MinionLife, LegacyMinionSpeed, MinionArmour, MinionResists,
        LegacyLeech, Cull, Splash, Fire, Cold, Lightning, DamageVsChilled,
        Cost, LegacySingleProjectile, ChainRetention, ExtraWaves, MinionDeathBurst,
        MoreDamage, LifeOnHit, ProjectileSpeed, MinionMoveSpeed, MinionDuration,
        HalfMinions, MinionSize, RainSpread, RainSalvos, FireOrbit, ChargeDamage,
        ChargeTime, ArmourPenetration, LegacyDamageVsLowLife, PackHunt, MinionRegen,
        NoFork, FirstStrike, GolemAftershock, SummonsPerCast, MageCastSpeedAfterSummon,
        ComboDamage, ComboArea, ComboKnockback
    }

    [Serializable]
    public sealed class WeaponSkillProgress
    {
        public int version = 2;
        public int level = 1;
        public int experience;
        public List<string> nodes = new List<string>();
        public int Available => Mathf.Max(0, level - nodes.Count);
        public int RequiredExperience => 250 * level * level;

        public WeaponSkillProgress Copy() => new WeaponSkillProgress
        {
            version = version, level = level, experience = experience,
            nodes = nodes != null ? new List<string>(nodes) : new List<string>()
        };

        public void Sanitize(StatType grant)
        {
            // The new topology and effects cannot safely reinterpret old allocations.
            if (version < 2) { nodes = new List<string>(); version = 2; }
            level = Mathf.Clamp(level, 1, 10);
            experience = level == 10 ? 0 : Mathf.Clamp(experience, 0, RequiredExperience - 1);
            var saved = nodes;
            nodes = new List<string>();
            if (saved != null)
                foreach (string id in saved)
                    Allocate(grant, id);
        }

        public bool CanAllocate(StatType grant, string id)
        {
            var node = WeaponSkillTrees.Find(grant, id);
            if (node == null || Available <= 0 || nodes.Contains(id) || (node.Parent != null && !nodes.Contains(node.Parent))) return false;
            // Full elemental conversions are alternatives, never silently overridden by another school.
            bool converts = Array.Exists(node.Bonuses, b => b.Effect == WeaponSkillEffect.Fire || b.Effect == WeaponSkillEffect.Cold || b.Effect == WeaponSkillEffect.Lightning);
            if (converts) foreach (string owned in nodes)
            {
                var previous = WeaponSkillTrees.Find(grant, owned);
                if (previous != null && Array.Exists(previous.Bonuses, b => b.Effect == WeaponSkillEffect.Fire || b.Effect == WeaponSkillEffect.Cold || b.Effect == WeaponSkillEffect.Lightning)) return false;
            }
            return true;
        }
        public bool Allocate(StatType grant, string id)
        {
            if (!CanAllocate(grant, id)) return false;
            nodes.Add(id);
            return true;
        }

        public bool GainExperience(int amount)
        {
            if (amount <= 0 || level >= 10) return false;
            experience = (int)Math.Min(int.MaxValue, (long)experience + amount);
            bool leveled = false;
            while (level < 10 && experience >= RequiredExperience)
            {
                experience -= RequiredExperience;
                level++;
                leveled = true;
            }
            if (level == 10) experience = 0;
            return leveled;
        }
    }

    public readonly struct WeaponSkillBonus
    {
        public readonly WeaponSkillEffect Effect;
        public readonly float Value;
        public WeaponSkillBonus(WeaponSkillEffect effect, float value) { Effect = effect; Value = value; }
    }

    public sealed class WeaponSkillNode
    {
        public string Id, Parent, Name;
        public StatType Grant;
        public int Depth;
        public bool Notable, Keystone;
        public Vector2 Position;
        public WeaponSkillBonus[] Bonuses;
        public string Description
        {
            get
            {
                var lines = new List<string>();
                foreach (WeaponSkillBonus b in Bonuses) lines.Add(WeaponSkillTrees.Describe(b, Grant));
                return string.Join("\n", lines);
            }
        }
    }

    /// <summary>A snapshot belongs to the skill that produced a hit, projectile or minion.</summary>
    public sealed class WeaponSkillBonuses
    {
        private static readonly int EffectCount = Enum.GetValues(typeof(WeaponSkillEffect)).Length;
        private readonly float[] values = new float[EffectCount];
        private float moreDamage = 1f;
        private float comboMoreDamage = 1f;
        public float Get(WeaponSkillEffect effect) => values[(int)effect];
        public float MoreDamageMultiplier => moreDamage;
        public float ComboDamageMultiplier => comboMoreDamage;
        public float AreaMultiplier => DefenceMath.RadiusMultiplier(Get(WeaponSkillEffect.Area));
        public float RangeMultiplier => Mathf.Max(0.3f, 1f + Get(WeaponSkillEffect.Range) / 100f);
        public float ProjectileSpeedMultiplier => Mathf.Max(0.25f, 1f + Get(WeaponSkillEffect.ProjectileSpeed) / 100f);
        public float DurationMultiplier => Mathf.Max(0.1f, 1f + Get(WeaponSkillEffect.Duration) / 100f);
        public float MinionDurationMultiplier => Mathf.Max(0.1f, 1f + Get(WeaponSkillEffect.MinionDuration) / 100f);
        public int ExtraProjectiles => Mathf.RoundToInt(Get(WeaponSkillEffect.Projectiles));
        public int ExtraMinions => Mathf.RoundToInt(Get(WeaponSkillEffect.Minions));
        public WeaponSkillBonuses(ItemData item)
        {
            if (item?.SkillProgress == null || !item.MainSkillGrant.HasValue) return;
            foreach (string id in item.SkillProgress.nodes)
            {
                WeaponSkillNode node = WeaponSkillTrees.Find(item.MainSkillGrant.Value, id);
                if (node != null) foreach (WeaponSkillBonus b in node.Bonuses)
                {
                    values[(int)b.Effect] += b.Value;
                    if (b.Effect == WeaponSkillEffect.MoreDamage) moreDamage *= Mathf.Max(0f, 1f + b.Value / 100f);
                    if (b.Effect == WeaponSkillEffect.ComboDamage) comboMoreDamage *= Mathf.Max(0f, 1f + b.Value / 100f);
                }
            }
        }
    }

    public static class WeaponSkillRules
    {
        public const float ComboFollowupSpeed = 4f;

        /// <summary>Fast links inside each combo, followed by one normal finisher-to-opener interval.</summary>
        public static float ComboSustainedSpeed(StatType grant)
        {
            int length = ComboLength(grant);
            return length > 0 ? length / (1f + (length - 1f) / ComboFollowupSpeed) : 1f;
        }

        /// <summary>Damage of one completed combo step, shared by gameplay and DPS estimates.</summary>
        public static float ComboHitMultiplier(StatType grant, int step, float finisherMore = 1f)
        {
            int length = ComboLength(grant);
            if (length == 0 || step <= 0) return 1f;
            if (step % length == 0)
                return (grant == StatType.GrantCleave ? 2.25f : 1.75f) * finisherMore;
            return grant == StatType.GrantCleave && step % length == 2 ? 1.35f : 1f;
        }

        /// <summary>Distinct attacks per combo; channelled and non-melee skills have no combo.</summary>
        public static int ComboLength(StatType grant)
        {
            switch (grant)
            {
                case StatType.GrantPulverize:
                case StatType.GrantFissure:
                case StatType.GrantAftershock:
                case StatType.GrantReapingArc: return 3;
                case StatType.GrantCleave:
                case StatType.GrantLungingThrust:
                case StatType.GrantCrossSlash: return 4;
                case StatType.GrantFangStrike:
                case StatType.GrantVenomSlash: return 5;
                default: return 0;
            }
        }

        public static bool IsLevelBonus(StatType stat) =>
            stat == StatType.AllSpellLevels || stat == StatType.FireSpellLevels || stat == StatType.ColdSpellLevels ||
            stat == StatType.LightningSpellLevels || stat == StatType.MinionLevels ||
            stat == StatType.RaiseSkeletonsLevels || stat == StatType.SkeletonMagesLevels ||
            stat == StatType.SpiritWolvesLevels || stat == StatType.BoneGolemLevels;

        public static bool IsSkillMechanic(StatType stat) =>
            stat == StatType.AdditionalArrows || stat == StatType.AdditionalSpellProjectiles ||
            stat == StatType.AdditionalProjectiles || stat == StatType.AdditionalChains ||
            stat == StatType.AdditionalMinions || stat == StatType.AdditionalSkeletons ||
            stat == StatType.LegacySpellEcho || stat == StatType.LegacyShatter || stat == StatType.ExplodeOnKill ||
            stat == StatType.LegacyGlacialStep || stat == StatType.LegacySecondWind || stat == StatType.LegacyStormblade ||
            stat == StatType.LegacyDeathsHerald || stat == StatType.LegacyMeleeSplash;

        public static StatType[] Pool(ItemType type, WeaponType weapon)
        {
            if (type == ItemType.Weapon && weapon == WeaponType.Sceptre)
                return SkillGrants.SceptreMain;
            if (type != ItemType.Weapon || weapon == WeaponType.Unarmed) return Array.Empty<StatType>();
            switch (weapon)
            {
                case WeaponType.Staff: return new[] { StatType.GrantFireBolt, StatType.GrantIceShard, StatType.GrantChainLightning, StatType.GrantVenomSpout };
                case WeaponType.Bow: return new[] { StatType.GrantSplitShot, StatType.GrantPiercingShot, StatType.GrantRainOfArrows, StatType.GrantBurningArrow, StatType.GrantVenomArrow };
                case WeaponType.Mace:
                case WeaponType.Maul: return new[] { StatType.GrantPulverize, StatType.GrantFissure, StatType.GrantAftershock };
                case WeaponType.Axe:
                case WeaponType.Greataxe: return new[] { StatType.GrantCleave, StatType.GrantReapingArc, StatType.GrantWhirlwind };
                case WeaponType.Dagger: return new[] { StatType.GrantFangStrike, StatType.GrantCrossSlash, StatType.GrantVenomSlash };
                default: return new[] { StatType.GrantCleave, StatType.GrantLungingThrust, StatType.GrantWhirlwind };
            }
        }

        public static bool IsWeaponSkill(StatType stat) => SkillGrants.IsBowSkill(stat) || SkillGrants.IsSummon(stat) ||
            Array.IndexOf(SkillGrants.MeleeSkills, stat) >= 0 || Array.IndexOf(SkillGrants.StaffMain, stat) >= 0 || stat == StatType.GrantVenomSpout;

        public static List<StatModifier> Normalize(ItemType type, WeaponType weapon, IEnumerable<StatModifier> source)
        {
            var result = new List<StatModifier>();
            StatType[] pool = Pool(type, weapon);
            StatModifier? main = null;
            int legacyLevel = 1;
            if (source != null) foreach (StatModifier m in source)
            {
                if (IsLevelBonus(m.Stat)) continue;
                if (IsSkillMechanic(m.Stat))
                {
                    // Preserve some value on old gear without retaining skill transformations.
                    result.Add(new StatModifier(StatType.Damage, Mathf.Min(30f, Mathf.Abs(m.Value) * 5f), m.Tier));
                    continue;
                }
                if (type == ItemType.Grimoire && SkillGrants.IsSummon(m.Stat))
                {
                    // Old summon tomes become support tomes without losing their other stats.
                    if (!result.Exists(mod => mod.Stat == StatType.GrantDeathMark))
                        result.Add(new StatModifier(StatType.GrantDeathMark, 1));
                    continue;
                }
                if (m.Stat == StatType.GrantDeathMark && result.Exists(mod => mod.Stat == StatType.GrantDeathMark)) continue;
                if (IsWeaponSkill(m.Stat))
                {
                    legacyLevel = Mathf.Max(legacyLevel, Mathf.RoundToInt(m.Value));
                    if (Array.IndexOf(pool, m.Stat) >= 0 && (!main.HasValue || m.Value > main.Value.Value)) main = m;
                    continue;
                }
                result.Add(m);
            }
            if (pool.Length > 0)
                result.Add(main.HasValue ? new StatModifier(main.Value.Stat, Mathf.Clamp(main.Value.Value, 1, 10), main.Value.Tier)
                    : new StatModifier(pool[0], Mathf.Clamp(legacyLevel, 1, 10)));
            return result;
        }
    }
}

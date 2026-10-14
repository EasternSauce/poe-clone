using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    public enum WeaponSkillEffect
    {
        Damage, Speed, Area, Range, Projectiles, Chains, Pierce, Fork, Repeat,
        Chill, Shock, Ignite, Shatter, Explode, Poison, Cloud, Duration,
        Minions, MinionLife, MinionSpeed, MinionArmour, MinionResists,
        Leech, Cull, Splash, Fire, Cold, Lightning, DamageVsChilled,
        Cost, SingleProjectile, ChainRetention, ExtraWaves, MinionDeathBurst
    }

    [Serializable]
    public sealed class WeaponSkillProgress
    {
        public int version = 1;
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
            level = Mathf.Clamp(level, 1, 10);
            experience = level == 10 ? 0 : Mathf.Clamp(experience, 0, RequiredExperience - 1);
            var valid = new List<string>();
            if (nodes != null)
                foreach (string id in nodes)
                {
                    WeaponSkillNode node = WeaponSkillTrees.Find(grant, id);
                    if (node != null && valid.Count < level && !valid.Contains(id) &&
                        (node.Parent == null || valid.Contains(node.Parent))) valid.Add(id);
                }
            nodes = valid;
        }

        public bool Allocate(StatType grant, string id)
        {
            WeaponSkillNode node = WeaponSkillTrees.Find(grant, id);
            if (node == null || Available <= 0 || nodes.Contains(id) ||
                (node.Parent != null && !nodes.Contains(node.Parent))) return false;
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
        public string Id, Parent, Name, Branch;
        public int Depth;
        public bool Notable, Keystone;
        public Vector2 Position;
        public WeaponSkillBonus[] Bonuses;
        public string Description
        {
            get
            {
                var lines = new List<string>();
                foreach (WeaponSkillBonus b in Bonuses) lines.Add(WeaponSkillTrees.Describe(b));
                return string.Join("\n", lines);
            }
        }
    }

    /// <summary>A snapshot belongs to the skill that produced a hit, projectile or minion.</summary>
    public sealed class WeaponSkillBonuses
    {
        private readonly float[] values = new float[Enum.GetValues(typeof(WeaponSkillEffect)).Length];
        public float Get(WeaponSkillEffect effect) => values[(int)effect];
        public float DamageMultiplier => Mathf.Max(0.1f, 1f + Get(WeaponSkillEffect.Damage) / 100f);
        public float SpeedMultiplier => Mathf.Max(0.25f, 1f + Get(WeaponSkillEffect.Speed) / 100f);
        public float AreaMultiplier => DefenceMath.RadiusMultiplier(Get(WeaponSkillEffect.Area));
        public float RangeMultiplier => Mathf.Max(0.3f, 1f + Get(WeaponSkillEffect.Range) / 100f);
        public float DurationMultiplier => Mathf.Max(0.1f, 1f + Get(WeaponSkillEffect.Duration) / 100f);
        public bool SingleProjectile => Get(WeaponSkillEffect.SingleProjectile) > 0;
        public int ExtraProjectiles => SingleProjectile ? 0 : Mathf.RoundToInt(Get(WeaponSkillEffect.Projectiles));
        public int ExtraMinions => Mathf.RoundToInt(Get(WeaponSkillEffect.Minions));
        public WeaponSkillBonuses(ItemData item)
        {
            if (item?.SkillProgress == null || !item.MainSkillGrant.HasValue) return;
            foreach (string id in item.SkillProgress.nodes)
            {
                WeaponSkillNode node = WeaponSkillTrees.Find(item.MainSkillGrant.Value, id);
                if (node != null) foreach (WeaponSkillBonus b in node.Bonuses) values[(int)b.Effect] += b.Value;
            }
        }
    }

    public static class WeaponSkillRules
    {
        public static bool IsLevelBonus(StatType stat) =>
            stat == StatType.AllSpellLevels || stat == StatType.FireSpellLevels || stat == StatType.ColdSpellLevels ||
            stat == StatType.LightningSpellLevels || stat == StatType.MinionLevels ||
            stat == StatType.RaiseSkeletonsLevels || stat == StatType.SkeletonMagesLevels ||
            stat == StatType.SpiritWolvesLevels || stat == StatType.BoneGolemLevels;

        public static bool IsSkillMechanic(StatType stat) =>
            stat == StatType.AdditionalArrows || stat == StatType.AdditionalSpellProjectiles ||
            stat == StatType.AdditionalProjectiles || stat == StatType.AdditionalChains ||
            stat == StatType.AdditionalMinions || stat == StatType.AdditionalSkeletons ||
            stat == StatType.SpellEcho || stat == StatType.Shatter || stat == StatType.ExplodeOnKill ||
            stat == StatType.GlacialStep || stat == StatType.SecondWind || stat == StatType.Stormblade ||
            stat == StatType.DeathsHerald || stat == StatType.MeleeSplash;

        public static StatType[] Pool(ItemType type, WeaponType weapon)
        {
            if (type == ItemType.Grimoire || (type == ItemType.Weapon && weapon == WeaponType.Sceptre))
                return new[] { StatType.GrantRaiseSkeletons, StatType.GrantSkeletonMages, StatType.GrantSpiritWolves, StatType.GrantBoneGolem, StatType.GrantSummonViper };
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
                if (IsWeaponSkill(m.Stat) || (m.Stat == StatType.GrantDeathMark && pool.Length > 0))
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

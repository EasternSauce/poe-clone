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
        ComboDamage, ComboArea, ComboKnockback, CleaveExplosion, CleaveDoubleSlash, LegacyBossCull, CleaveBleed,
        PulverizeWorldshaker, PulverizeTwinHammer,
        // Retain retired effect IDs so later values stay stable.
        LegacyArmourShred, LegacyPulverizeWindupDefence, LegacyPulverizeUnstoppable,
        PulverizeBonecrusher, PerfectImpact,
        WhirlGaleCutter, ChannelUnbroken, WhirlGatheringStorm, WhirlSlipstream,
        ComboSetupDamage, MeasuredStrike, MeasuredRest, MeasuredStun, ChannelDamageTaken, ChannelMoveSpeed, ChannelRamp,
        PulverizeCataclysm, CleaveRupture, Overkill, OverkillHeal, ExecuteDamage, DamageVsStaggered, ComboDamageTaken,
        WhirlMaelstrom, ChannelRelease, ReleaseStun, FinisherStun
    }

    [Serializable]
    public sealed class WeaponSkillProgress
    {
        public const int MaxLevel = 10;
        public int version = 11;
        public int level = 1;
        public int experience;
        public List<string> nodes = new List<string>();
        /// <summary>One point on reaching each level and one halfway through it; maximum level completes the last pair (20).</summary>
        public int Points => level >= MaxLevel ? 2 * MaxLevel : 2 * level - 1 + (2 * experience >= RequiredExperience ? 1 : 0);
        public int Available => Mathf.Max(0, Points - nodes.Count);
        public int RequiredExperience => 250 * level * level;

        public WeaponSkillProgress Copy() => new WeaponSkillProgress
        {
            version = version, level = level, experience = experience,
            nodes = nodes != null ? new List<string>(nodes) : new List<string>()
        };

        public void Sanitize(StatType grant)
        {
            // The 20-point topology cannot safely reinterpret old allocations.
            if (version < 11) { nodes = new List<string>(); version = 11; }
            level = Mathf.Clamp(level, 1, MaxLevel);
            experience = level == MaxLevel ? 0 : Mathf.Clamp(experience, 0, RequiredExperience - 1);
            var saved = nodes;
            nodes = new List<string>();
            if (saved != null)
                foreach (string id in saved)
                    Allocate(grant, id);
        }

        /// <summary>Connected to the skill origin or to an allocated neighbour.</summary>
        public bool IsReachable(StatType grant, WeaponSkillNode node)
        {
            if (node.Links.Length == 0) return true;
            foreach (string link in node.Links)
                if (link == WeaponSkillNode.Root || nodes.Contains(link)) return true;
            return false;
        }

        /// <summary>An allocated node sharing an exclusive group with this one, or null.</summary>
        public WeaponSkillNode ExclusiveConflict(StatType grant, WeaponSkillNode node)
        {
            if (node.Exclusive.Length == 0) return null;
            foreach (string owned in nodes)
            {
                var previous = WeaponSkillTrees.Find(grant, owned);
                if (previous != null && previous != node && previous.ExcludesWith(node)) return previous;
            }
            return null;
        }

        public bool CanAllocate(StatType grant, string id)
        {
            var node = WeaponSkillTrees.Find(grant, id);
            return node != null && Available > 0 && !nodes.Contains(id) && IsReachable(grant, node) && ExclusiveConflict(grant, node) == null;
        }

        /// <summary>Whether removing this node keeps every other allocation connected to the origin.</summary>
        public bool CanRemove(StatType grant, string id)
        {
            if (!nodes.Contains(id)) return false;
            var connected = new HashSet<string>();
            var frontier = new Queue<string>();
            frontier.Enqueue(WeaponSkillNode.Root);
            while (frontier.Count > 0)
            {
                string at = frontier.Dequeue();
                foreach (string next in WeaponSkillTrees.Neighbours(grant, at))
                    if (next != id && nodes.Contains(next) && connected.Add(next)) frontier.Enqueue(next);
            }
            return connected.Count == nodes.Count - 1;
        }
        public bool Allocate(StatType grant, string id)
        {
            if (!CanAllocate(grant, id)) return false;
            nodes.Add(id);
            return true;
        }

        /// <summary>Character level at which a weapon skill may reach its maximum level.</summary>
        public const int CapCharacterLevel = 34;

        /// <summary>Levels gained so far as a continuous value: 0 at a fresh level 1, 9 at level 10.</summary>
        public float Progress => level >= MaxLevel ? MaxLevel - 1 : level - 1 + (float)experience / RequiredExperience;

        /// <summary>The furthest progress a character level allows, rising evenly from level 1 to <see cref="CapCharacterLevel"/>.</summary>
        public static float ProgressCap(int characterLevel) =>
            Mathf.Clamp01((characterLevel - 1f) / (CapCharacterLevel - 1f)) * (MaxLevel - 1);

        /// <summary>The lowest character level whose cap allows this progress.</summary>
        public int CharacterLevelEquivalent => level >= MaxLevel ? CapCharacterLevel :
            Mathf.CeilToInt(1f + Progress * (CapCharacterLevel - 1f) / (MaxLevel - 1) - 0.0001f);

        /// <summary>The furthest level and experience a character level allows (experience rounded down).</summary>
        public static void CapAt(int characterLevel, out int capLevel, out int capExperience)
        {
            float cap = ProgressCap(characterLevel);
            capLevel = Mathf.Min(MaxLevel, 1 + Mathf.FloorToInt(cap + 0.0001f));
            capExperience = capLevel >= MaxLevel ? 0 : Mathf.FloorToInt((cap - (capLevel - 1)) * 250 * capLevel * capLevel);
        }

        public bool AtCharacterCap(int characterLevel)
        {
            CapAt(characterLevel, out int capLevel, out int capExperience);
            return level < MaxLevel && (level > capLevel || level == capLevel && experience >= capExperience);
        }

        /// <summary>Adds experience up to the character's cap; returns the tree points it earned (levels and halfway marks).</summary>
        public int GainExperience(int amount, int characterLevel)
        {
            // Weapons found above the cap keep their level but stop growing.
            if (amount <= 0 || level >= MaxLevel || AtCharacterCap(characterLevel)) return 0;
            CapAt(characterLevel, out int capLevel, out int capExperience);
            int before = Points;
            experience = (int)Math.Min(int.MaxValue, (long)experience + amount);
            while (level < MaxLevel && experience >= RequiredExperience)
            {
                experience -= RequiredExperience;
                level++;
            }
            if (level > capLevel || level == capLevel && experience > capExperience)
            {
                level = capLevel;
                experience = capExperience;
            }
            if (level == MaxLevel) experience = 0;
            return Points - before;
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
        /// <summary>The skill origin, as a link target.</summary>
        public const string Root = "root";
        public string Id, Name;
        /// <summary>Neighbours towards the origin (Root for the first nodes); allocation needs any one of them.</summary>
        public string[] Links = Array.Empty<string>();
        /// <summary>Nodes sharing a group are alternatives, such as combo forms or elemental conversions.</summary>
        public string[] Exclusive = Array.Empty<string>();
        public StatType Grant;
        /// <summary>Fewest points needed to reach this node, itself included.</summary>
        public int Depth;
        public bool Notable, Keystone;
        public Vector2 Position;
        public WeaponSkillBonus[] Bonuses;
        /// <summary>Rules text after the bonus lines (how a keystone's mechanic plays).</summary>
        public string Note;
        public bool ExcludesWith(WeaponSkillNode other)
        {
            foreach (string group in Exclusive)
                if (Array.IndexOf(other.Exclusive, group) >= 0) return true;
            return false;
        }
        public string Description
        {
            get
            {
                var lines = new List<string>();
                foreach (WeaponSkillBonus b in Bonuses) lines.Add(WeaponSkillTrees.Describe(b, Grant));
                if (Note != null) lines.Add(Note);
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
        private float comboSetupMoreDamage = 1f;
        public float Get(WeaponSkillEffect effect) => values[(int)effect];
        public bool Has(WeaponSkillEffect effect) => values[(int)effect] > 0f;
        public float MoreDamageMultiplier => moreDamage;
        public float ComboDamageMultiplier => comboMoreDamage;
        /// <summary>More or less damage for combo hits before the finisher.</summary>
        public float ComboSetupMultiplier => comboSetupMoreDamage;
        public float CullPercent(bool boss) => boss ? 0f : Get(WeaponSkillEffect.Cull);
        /// <summary>Damage taken while channelling, never below 40%.</summary>
        public float ChannelTakenMultiplier => Mathf.Max(0.4f, 1f + Get(WeaponSkillEffect.ChannelDamageTaken) / 100f);
        public float MeasuredRestSeconds => WeaponSkillRules.MeasuredRest * Mathf.Max(0.2f, 1f + Get(WeaponSkillEffect.MeasuredRest) / 100f);
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
                    if (b.Effect == WeaponSkillEffect.ComboSetupDamage) comboSetupMoreDamage *= Mathf.Max(0f, 1f + b.Value / 100f);
                }
            }
        }
    }

    public static class WeaponSkillRules
    {
        public const int WhirlMaxBlades = 5;
        // Five blades add 2/3 of base melee DPS: 40% of the combined total.
        public const float WhirlBladeDamageShare = 2f / (3f * WhirlMaxBlades);
        public const float WhirlGaleMeleeShare = 0.4f;
        public const float WhirlGaleProjectileShare = 0.2f;
        public const float WhirlGaleRange = 12f;

        // Continuous strikes per normal weapon attack interval, independent of any reference weapon.
        public static float WhirlwindHitsPerSecond(WeaponType weapon, float attackRate) =>
            (10f / 3f) * SlotRules.BaseAttacksPerSecond(weapon) * attackRate;

        // Preserve the 8.85-hit combo total, distributed in a 1:1:1:1.5 ratio.
        public const float CleaveExplosiveSlashDamage = 8.85f / 4.5f;
        public const float CleaveExplosionDamage = CleaveExplosiveSlashDamage * 1.5f;
        public const float CleaveDoubleSlashDamage = 1.75f;
        public const float CleaveDoubleSlashFinisherRadiusMultiplier = 1.35f;
        public const float CleaveDoubleSlashPullDistance = 1.2f;
        public const float CleaveBleedSeconds = 3f;
        public const float CleaveExplosionDuration = 1.56f;
        public static readonly float CleaveExplosionRadiusMultiplier = 1.35f * Mathf.Sqrt(0.6f);
        // Additional time between pairs, measured in normal weapon attack intervals.
        public const float CleaveDoubleSlashPause = 0.35f;
        public static float ComboFollowupSpeed(StatType grant, bool doubleSlash = false, bool twinHammer = false) =>
            grant == StatType.GrantCleave ? (doubleSlash ? 1.7f : 2.25f) : grant == StatType.GrantPulverize && twinHammer ? 6f : 4f;
        public static float ComboRecoveryMultiplier(StatType grant, bool twinHammer = false) =>
            grant == StatType.GrantPulverize && twinHammer ? 0.5f : 1f;
        public const float PulverizeWorldshakerDamage = 3f;
        public const float PulverizeTwinHammerDamage = 1.3f;
        // Redistribute the original 0.875-interval pair cycle into a deliberate opener
        // and shorter recovery. Both strikes keep their full damage and attack-speed scaling.
        public static float PulverizeTwinHammerStepDuration(int step) => step % 2 == 1 ? 0.375f : 0.125f;
        public const float PulverizeWorldshakerDuration = 1.4f;
        public const int BonecrusherShardCount = 6;
        public const float BonecrusherRange = 10f;
        public const float BonecrusherLifeFraction = 0.1f;
        public static float PerfectImpactMultiplier(float distance, float radius) =>
            radius > 0f && distance >= 0f && distance <= radius * 0.5f ? 1.3f : 1f;

        // Measured: seconds of deliberate rest after the next attack is available.
        public const float MeasuredRest = 0.5f;
        // Cataclysm: a channelled slam. Full charge takes this many normal attack intervals.
        public const float CataclysmChargeIntervals = 1.5f;
        public const float CataclysmRecoveryIntervals = 0.5f;
        public const float CataclysmTapDamage = 0.6f;
        public const float CataclysmFullDamage = 4.5f;
        public const float CataclysmFullRadius = 1.4f;
        public const float CataclysmCostMultiplier = 2f;
        public static float CataclysmDamage(float charge) => Mathf.Lerp(CataclysmTapDamage, CataclysmFullDamage, Mathf.Clamp01(charge));
        public static float CataclysmRadius(float charge) => Mathf.Lerp(1f, CataclysmFullRadius, Mathf.Clamp01(charge));
        public const float RuptureMultiplier = 1.5f;
        public const float ExecuteThreshold = 0.35f;
        public const float OverkillRadius = 3f;
        // Whirlwind channelling: spin-rate ramp per second, Maelstrom pull, Tempest Release.
        public const float ChannelRampPerSecond = 5f;
        public const float MaelstromPullReach = 2f;
        public const float MaelstromPullDistance = 0.35f;
        public const float MaelstromStandOff = 0.8f;
        public const float MaelstromDamage = 0.8f;
        public const float ReleaseMinimumSeconds = 1f;
        public const float ReleaseMaximumSeconds = 4f;
        public const float ReleaseRadiusMultiplier = 2f;

        /// <summary>Four target-biased shards with angular spread, plus two free-scattering shards.</summary>
        public static Vector3[] BonecrusherDirections(Vector3 origin, IReadOnlyList<Vector3> targets, int seed)
        {
            var random = new System.Random(seed);
            var directions = new Vector3[BonecrusherShardCount];
            int start = targets.Count > 0 ? random.Next(targets.Count) : 0;
            for (int i = 0; i < directions.Length; i++)
            {
                float angle = (float)random.NextDouble() * 360f;
                if (i < 4 && targets.Count > 0)
                {
                    Vector3 to = targets[(start + i) % targets.Count] - origin;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.001f)
                        angle = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + (float)random.NextDouble() * 20f - 10f;
                }
                directions[i] = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            }
            return directions;
        }

        /// <summary>Fast links inside each combo, followed by one normal finisher-to-opener interval.</summary>
        public static float ComboSustainedSpeed(StatType grant, bool doubleSlash = false, bool twinHammer = false)
        {
            if (grant == StatType.GrantPulverize && twinHammer)
                return 2f / (PulverizeTwinHammerStepDuration(1) + ComboRecoveryMultiplier(grant, true));
            int length = ComboLength(grant, doubleSlash, twinHammer);
            float linkInterval = 1f / ComboFollowupSpeed(grant, doubleSlash, twinHammer);
            if (grant == StatType.GrantCleave && doubleSlash) linkInterval += CleaveDoubleSlashPause;
            return length > 0 ? length / (ComboRecoveryMultiplier(grant, twinHammer) * (1f + (length - 1f) * linkInterval)) : 1f;
        }

        /// <summary>Damage of one completed combo step, shared by gameplay (which passes the tree's
        /// finisher and setup multipliers separately) and DPS estimates.</summary>
        public static float ComboHitMultiplier(StatType grant, int step, float finisherMore = 1f, bool explosiveCleave = false, bool doubleSlash = false, bool worldshaker = false, bool twinHammer = false, float setupMore = 1f)
        {
            int length = ComboLength(grant, doubleSlash, twinHammer);
            if (length == 0 || step <= 0) return 1f;
            bool finisher = step % length == 0;
            return BaseComboHit(grant, finisher, explosiveCleave, doubleSlash, worldshaker, twinHammer) * (finisher ? finisherMore : setupMore);
        }

        private static float BaseComboHit(StatType grant, bool finisher, bool explosiveCleave, bool doubleSlash, bool worldshaker, bool twinHammer)
        {
            // Each Twin Rhythm part contains two separately delivered cuts.
            if (grant == StatType.GrantCleave && doubleSlash) return CleaveDoubleSlashDamage * (finisher ? 1.1f : 1f);
            if (grant == StatType.GrantPulverize)
                return twinHammer ? PulverizeTwinHammerDamage : worldshaker && finisher ? PulverizeWorldshakerDamage : 1f;
            // Explosive Finale spreads its damage evenly across slashes, with a 50% stronger finisher.
            if (grant == StatType.GrantCleave)
                return finisher ? (explosiveCleave ? CleaveExplosionDamage : 1f) : explosiveCleave ? CleaveExplosiveSlashDamage : 1f;
            return finisher ? 1.75f : 1f;
        }

        /// <summary>Distinct attacks per combo; channelled and non-melee skills have no combo.</summary>
        public static int ComboLength(StatType grant, bool doubleSlash = false, bool twinHammer = false)
        {
            if (grant == StatType.GrantCleave && doubleSlash || grant == StatType.GrantPulverize && twinHammer) return 2;
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
                // Retired range rolls are removed from all items, including saved and unique gear.
                if (m.Stat == StatType.MeleeRange || IsLevelBonus(m.Stat)) continue;
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
                result.Add(main.HasValue ? new StatModifier(main.Value.Stat, Mathf.Clamp(main.Value.Value, 1, WeaponSkillProgress.MaxLevel), main.Value.Tier)
                    : new StatModifier(pool[0], Mathf.Clamp(legacyLevel, 1, WeaponSkillProgress.MaxLevel)));
            return result;
        }
    }
}

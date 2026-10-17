using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Skills
{
    /// <summary>
    /// A bow rolls one main skill, permanently replacing its shot. Allocated weapon nodes
    /// change the volley, projectile behaviour, damage, cost and draw speed.
    /// </summary>
    public partial class PlayerSkills
    {
        // Degrees between arrows: Split Shot's wide fan, Piercing Shot's tight one.
        private const float SplitSpread = 13f;
        private const float PierceSpread = 5f;
        private const float PierceRangeMore = 1.8f;
        public const float RainRadius = 2.4f;     // where the arrows land round the aimed spot
        public const float RainHitRadius = 1.1f;  // what each one hits where it lands
        private const float BurnRadius = 1.5f;

        public static float BowProjectileRange(float range, SkillId? skill) =>
            range * PlayerArrow.BowRangeMultiplier * (skill == SkillId.PiercingShot ? PierceRangeMore : 1f);

        /// <summary>How the bow is drawn while this skill is on.</summary>
        public static CharacterAttackAnimator.BowStyle BowStyleOf(SkillId id)
        {
            switch (id)
            {
                case SkillId.SplitShot: return CharacterAttackAnimator.BowStyle.Fan;
                case SkillId.PiercingShot: return CharacterAttackAnimator.BowStyle.Heavy;
                case SkillId.RainOfArrows: return CharacterAttackAnimator.BowStyle.Sky;
                case SkillId.BurningArrow: return CharacterAttackAnimator.BowStyle.Canted;
                case SkillId.VenomArrow: return CharacterAttackAnimator.BowStyle.Canted;
                default: return CharacterAttackAnimator.BowStyle.Plain;
            }
        }

        /// <summary>Attack speed while this skill is on, as a multiplier (the heavy ones are slower).</summary>
        public static float BowSpeed(SkillId id)
        {
            switch (id)
            {
                case SkillId.PiercingShot: return 0.9f;
                case SkillId.RainOfArrows: return 0.85f;
                default: return 1f;
            }
        }

        private const int SplitExtra = 2;
        private static int RainCount(int arrows) => 6 + Mathf.Max(0, arrows - 1);

        /// <summary>Expected impacts on a stationary point at the aim position. Each salvo
        /// places its first arrow at that position; only the remaining arrows scatter.</summary>
        public static float ExpectedRainHits(int arrows, int salvos, float spread, float hitRadius)
        {
            float chance = spread > 0f ? Mathf.Clamp01(hitRadius * hitRadius / (spread * spread)) : 1f;
            return Mathf.Max(1, salvos) * (1f + (RainCount(arrows) - 1) * chance);
        }

        // When the k-th of n rain arrows lands, after the shot.
        private static float RainDelay(int k, int n)
        {
            return 0.25f + 0.4f * k / Mathf.Max(1, n);
        }

        /// <summary>
        /// The bow skill's shot leaves the bow, the way the player faces now. damage: the plain
        /// shot's (weapon and Strength); arrows: how many the plain shot would loose (1 plus
        /// Additional Arrows and any extra-arrow roll); target: the aimed spot (Rain of Arrows).
        /// </summary>
        public void ReleaseBow(SkillId id, int level, float damage, float range, int arrows, Vector3 target, float chargeFraction = 0f)
        {
            PlaySkillSound(id, transform.position);
            SkillDefinition skill = SkillBook.Get(id);
            level = Mathf.Max(1, level);
            var tree = BonusesFor(id);
            float area = SkillArea(id);
            range *= id == SkillId.RainOfArrows ? tree.RangeMultiplier : SkillProjectileRange(id);
            Vector3 forward = transform.forward;
            if (id != SkillId.RainOfArrows)
            {
                float projectileRange = BowProjectileRange(range, id);
                forward = ProjectileAimAssist.Direction(transform.position, forward, projectileRange);
                Face(forward);
            }
            arrows = Mathf.Max(1, arrows + tree.ExtraProjectiles);
            Vector3 shotOrigin = transform.position;

            switch (id)
            {
                case SkillId.SplitShot:
                {
                    int count = arrows + SplitExtra;
                    float each = damage * skill.WeaponMultiplierAt(level);
                    var volley = PlayerArrow.NewVolley();
                    foreach (Vector3 direction in HitEffects.Spread(forward, count, SplitSpread))
                        PlayerArrow.LaunchArrow(transform, range, each, direction, volley).WithSkill(tree);
                    Record(skill, level, range, count);
                    break;
                }

                case SkillId.PiercingShot:
                {
                    float far = range * PierceRangeMore;
                    float each = damage * skill.WeaponMultiplierAt(level);
                    var volley = PlayerArrow.NewVolley();
                    foreach (Vector3 direction in HitEffects.Spread(forward, arrows, PierceSpread))
                        PlayerArrow.LaunchArrow(transform, far, each, direction, volley).Piercing(skill.Color).WithSkill(tree, tree.Get(WeaponSkillEffect.ChargeDamage) * chargeFraction);
                    Record(skill, level, far, arrows);
                    break;
                }

                case SkillId.BurningArrow:
                {
                    float radius = BurnRadius * area;
                    float each = damage * skill.WeaponMultiplierAt(level);
                    float ignite = 20f;
                    var volley = PlayerArrow.NewVolley();
                    foreach (Vector3 direction in HitEffects.Spread(forward, arrows, PlayerCombat.ArrowSpreadDegrees))
                        PlayerArrow.LaunchArrow(transform, range, each, direction, volley).Burning(skill.Color, radius, ignite).WithSkill(tree);
                    Record(skill, level, radius, arrows);
                    break;
                }

                case SkillId.VenomArrow:
                {
                    var volley = PlayerArrow.NewVolley();
                    foreach (Vector3 direction in HitEffects.Spread(forward, arrows, PierceSpread))
                        PlayerArrow.LaunchArrow(transform, range, damage * skill.WeaponMultiplierAt(level), direction, volley, arrowColor: skill.Color).Venomous(1).WithSkill(tree);
                    Record(skill, level, range, arrows);
                    break;
                }

                case SkillId.RainOfArrows:
                {
                    int count = RainCount(arrows);
                    int salvos = 1 + Mathf.RoundToInt(tree.Get(WeaponSkillEffect.RainSalvos));
                    float spread = RainRadius * Mathf.Max(0.25f, 1f + tree.Get(WeaponSkillEffect.RainSpread) / 100f);
                    float hitRadius = RainHitRadius * area;
                    float each = damage * skill.WeaponMultiplierAt(level);
                    var points = new Vector3[count * salvos];
                    for (int k = 0; k < points.Length; k++)
                    {
                        Vector2 offset = k % count == 0 ? Vector2.zero : Random.insideUnitCircle * spread;
                        points[k] = target + new Vector3(offset.x, 0f, offset.y);
                        SkillEffects.FallingArrow(points[k], RainDelay(k % count, count) + k / count * 0.65f, hitRadius, skill.Color,
                            at => RainHit(at, hitRadius, each, shotOrigin, tree));
                    }
                    // A puff at the feet as the volley goes up, so the shot reads before the rain lands.
                    SkillEffects.Shockwave(transform.position, 0.9f, skill.Color, 0.2f, type: SkillEffects.DamagePattern(skill));
                    Record(skill, level, hitRadius, count, points);
                    break;
                }
            }
        }

        // One rain arrow lands: everything it lands near takes the hit (an enemy can be hit by several).
        private void RainHit(Vector3 at, float radius, float damage, Vector3 shotOrigin, WeaponSkillBonuses tree)
        {
            if (this == null || stats == null || stats.IsDead)
                return;
            foreach (EnemyHealth enemy in EnemiesWithin(at, radius))
            {
                Vector3 distance = at - shotOrigin;
                distance.y = 0f;
                HitEffects.Deal(transform, enemy, damage, attack: true, CombatText.PhysicalColor,
                    projectileDistance: distance.magnitude, tree: tree);
            }
        }

        // A spectator's copy of a bow skill's shot (see PlayVisual): the same arrows, harmless.
        private static void PlayBowVisual(CastRecord cast, SkillDefinition skill, Transform caster, Vector3 facing)
        {
            int count = Mathf.Max(1, cast.Count);
            float bowRange = CharacterAttackAnimator.AttackRange(WeaponType.Bow);
            switch (cast.Skill)
            {
                case SkillId.SplitShot:
                    foreach (Vector3 direction in HitEffects.Spread(facing, count, SplitSpread))
                        PlayerArrow.LaunchArrow(caster, cast.Size > 0f ? cast.Size : bowRange, 0f, direction, null, harmless: true);
                    break;

                case SkillId.PiercingShot:
                    foreach (Vector3 direction in HitEffects.Spread(facing, count, PierceSpread))
                        PlayerArrow.LaunchArrow(caster, cast.Size > 0f ? cast.Size : bowRange, 0f, direction, null, harmless: true).Piercing(skill.Color);
                    break;

                case SkillId.BurningArrow:
                    foreach (Vector3 direction in HitEffects.Spread(facing, count, PlayerCombat.ArrowSpreadDegrees))
                        PlayerArrow.LaunchArrow(caster, bowRange, 0f, direction, null, harmless: true).Burning(skill.Color, cast.Size, 0f);
                    break;
                case SkillId.VenomArrow:
                    foreach (Vector3 direction in HitEffects.Spread(facing, count, PierceSpread))
                        PlayerArrow.LaunchArrow(caster, cast.Size > 0f ? cast.Size : bowRange, 0f, direction, null, harmless: true, arrowColor: skill.Color);
                    break;

                case SkillId.RainOfArrows:
                    if (cast.Points != null)
                    {
                        for (int k = 0; k < cast.Points.Length; k++)
                            SkillEffects.FallingArrow(cast.Points[k], RainDelay(k % count, count) + k / count * 0.65f, cast.Size, skill.Color);
                    }
                    break;
            }
        }
    }
}

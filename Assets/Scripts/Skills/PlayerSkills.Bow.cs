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
    /// Bow skills (Split Shot, Piercing Shot, Rain of Arrows, Burning Arrow): they roll on bows and
    /// quivers and sit on the bar like any skill, but pressing one toggles it instead of using it.
    /// One can be on at a time (turning another on swaps them; pressing it again turns it off).
    /// While one is on, the bow's attack (left click / the aim stick) is that skill: its own draw
    /// animation (<see cref="CharacterAttackAnimator.BowStyle"/>) and its own shot, released by
    /// <see cref="PlayerCombat"/> through <see cref="ReleaseBow"/>. Each shot costs a little mana
    /// (life with Blood Magic): Additional Arrows, attack speed and attack damage apply to them.
    /// </summary>
    public partial class PlayerSkills
    {
        private SkillId? toggledBow;
        private int toggledBowSlot = -1;

        // Degrees between arrows: Split Shot's wide fan, Piercing Shot's tight one.
        private const float SplitSpread = 13f;
        private const float PierceSpread = 5f;
        private const float PierceRangeMore = 1.3f;
        private const float RainRadius = 2.4f;     // where the arrows land round the aimed spot
        private const float RainHitRadius = 1.1f;  // what each one hits where it lands
        private const float BurnRadius = 1.5f;

        /// <summary>The bow skill the bow shoots right now (on, granted, and a bow in hand), or null.</summary>
        public SkillId? ActiveBowSkill
        {
            get
            {
                if (toggledBow == null || toggledBowSlot < 0 || Slot(toggledBowSlot) != toggledBow ||
                    LevelAt(toggledBowSlot) <= 0 || CurrentWeapon() != WeaponType.Bow)
                    return null;
                return toggledBow;
            }
        }

        /// <summary>Whether this bow skill is toggled on (for the bar's spinning ring).</summary>
        public bool IsToggledOn(SkillId id)
        {
            return ActiveBowSkill == id;
        }

        public bool IsToggledOnAt(int slot) => ActiveBowSkill != null && toggledBowSlot == slot;
        public int ActiveBowLevel => ActiveBowSkill != null ? LevelAt(toggledBowSlot) : 0;
        public bool CanAffordBowShot => ActiveBowSkill != null && stats != null &&
            stats.CanAffordSkill(SkillBook.Get(ActiveBowSkill.Value).ManaCostAt(ActiveBowLevel));
        public bool TrySpendBowShot(SkillId id, int level) => stats != null &&
            stats.TrySpendMana(SkillBook.Get(id).ManaCostAt(level));

        private bool ToggleBow(SkillDefinition skill, int slot)
        {
            Vector3 above = transform.position + Vector3.up * 2.2f;
            if (toggledBowSlot == slot && toggledBow == skill.Id)
            {
                toggledBow = null;
                toggledBowSlot = -1;
                CombatText.Show(above, skill.Name + " off", UiKit.DimText, 0.8f);
                Changed?.Invoke();
                return true;
            }
            if (CurrentWeapon() != WeaponType.Bow)
            {
                CombatText.Show(above, "Needs a bow", CombatText.PhysicalColor, 0.8f);
                return false;
            }

            toggledBow = skill.Id;
            toggledBowSlot = slot;
            SkillEffects.Shockwave(transform.position, 1.4f, skill.Color, 0.3f);
            CombatText.Show(above, skill.Name, skill.Color, 0.9f);
            Changed?.Invoke();
            return true;
        }

        // Gear changed: a bow skill that's no longer granted, or no bow in hand, switches off.
        private void CheckBowToggle()
        {
            if (toggledBow != null && ActiveBowSkill == null)
            {
                toggledBow = null;
                toggledBowSlot = -1;
            }
        }

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

        private static int SplitExtra(int level)
        {
            return 2 + (level >= 4 ? 1 : 0) + (level >= 8 ? 1 : 0);
        }

        private static int RainCount(int level, int arrows)
        {
            return 6 + (level >= 4 ? 1 : 0) + (level >= 8 ? 1 : 0) + 2 * Mathf.Max(0, arrows - 1);
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
        public void ReleaseBow(SkillId id, int level, float damage, float range, int arrows, Vector3 target)
        {
            SkillDefinition skill = SkillBook.Get(id);
            level = Mathf.Max(1, level);
            float area = DefenceMath.RadiusMultiplier(Stat(StatType.AreaOfEffect));
            Vector3 forward = transform.forward;
            arrows = Mathf.Max(1, arrows + Mathf.Max(0, Mathf.RoundToInt(Stat(StatType.AdditionalProjectiles))));
            Vector3 shotOrigin = transform.position;

            switch (id)
            {
                case SkillId.SplitShot:
                {
                    int count = arrows + SplitExtra(level);
                    float each = damage * (0.75f + 0.025f * (level - 1));
                    var volley = PlayerArrow.NewVolley();
                    foreach (Vector3 direction in HitEffects.Spread(forward, count, SplitSpread))
                        PlayerArrow.LaunchArrow(transform, range, each, direction, volley);
                    Record(skill, level, range, count);
                    break;
                }

                case SkillId.PiercingShot:
                {
                    float far = range * PierceRangeMore;
                    float each = damage * (1.1f + 0.05f * (level - 1));
                    var volley = PlayerArrow.NewVolley();
                    foreach (Vector3 direction in HitEffects.Spread(forward, arrows, PierceSpread))
                        PlayerArrow.LaunchArrow(transform, far, each, direction, volley).Piercing(skill.Color);
                    Record(skill, level, far, arrows);
                    break;
                }

                case SkillId.BurningArrow:
                {
                    float radius = BurnRadius * area;
                    float each = damage * (1f + 0.04f * (level - 1));
                    float ignite = 20f + 2f * (level - 1);
                    var volley = PlayerArrow.NewVolley();
                    foreach (Vector3 direction in HitEffects.Spread(forward, arrows, PlayerCombat.ArrowSpreadDegrees))
                        PlayerArrow.LaunchArrow(transform, range, each, direction, volley).Burning(skill.Color, radius, ignite);
                    Record(skill, level, radius, arrows);
                    break;
                }

                case SkillId.VenomArrow:
                {
                    var volley = PlayerArrow.NewVolley();
                    foreach (Vector3 direction in HitEffects.Spread(forward, arrows, PierceSpread))
                        PlayerArrow.LaunchArrow(transform, range, damage * (0.9f + 0.04f * (level - 1)), direction, volley, arrowColor: skill.Color).Venomous(level);
                    Record(skill, level, range, arrows);
                    break;
                }

                case SkillId.RainOfArrows:
                {
                    int count = RainCount(level, arrows);
                    float spread = RainRadius * area;
                    float hitRadius = RainHitRadius * area;
                    float each = damage * (0.45f + 0.025f * (level - 1));
                    var points = new Vector3[count];
                    for (int k = 0; k < count; k++)
                    {
                        Vector2 offset = k == 0 ? Vector2.zero : Random.insideUnitCircle * spread;
                        points[k] = target + new Vector3(offset.x, 0f, offset.y);
                        SkillEffects.FallingArrow(points[k], RainDelay(k, count), hitRadius, skill.Color,
                            at => RainHit(at, hitRadius, each, shotOrigin));
                    }
                    // A puff at the feet as the volley goes up, so the shot reads before the rain lands.
                    SkillEffects.Shockwave(transform.position, 0.9f, skill.Color, 0.2f);
                    Record(skill, level, hitRadius, count, points);
                    break;
                }
            }
        }

        // One rain arrow lands: everything it lands near takes the hit (an enemy can be hit by several).
        private void RainHit(Vector3 at, float radius, float damage, Vector3 shotOrigin)
        {
            if (this == null || stats == null || stats.IsDead)
                return;
            foreach (EnemyHealth enemy in EnemiesWithin(at, radius))
            {
                Vector3 distance = at - shotOrigin;
                distance.y = 0f;
                HitEffects.Deal(transform, enemy, damage, attack: true, CombatText.PhysicalColor,
                    projectileDistance: distance.magnitude);
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
                            SkillEffects.FallingArrow(cast.Points[k], RainDelay(k, cast.Points.Length), cast.Size, skill.Color);
                    }
                    break;
            }
        }
    }
}

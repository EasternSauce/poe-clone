using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Skills
{
    /// <summary>
    /// Melee skills (Cleave, Pulverize, Fissure, Aftershock, Reaping Arc, Lunging Thrust, Cross
    /// Slash, Fang Strike, Venom Slash): each belongs to a weapon family, and every melee weapon carries
    /// exactly one (<see cref="ActiveMeleeSkill"/>). Like a staff's spell, it is the weapon's attack:
    /// <see cref="PlayerCombat"/> starts each use through <see cref="TryStartMeleeSkill"/>. There is
    /// no cooldown: like a swing, the time between uses is the weapon's attack time, shortened by
    /// attack speed and stretched by the skill's own pace (<see cref="MeleeSpeed"/>). Their hits are
    /// melee attacks (they crit, leech and so on like a swing). Whirlwind instead spins for as long
    /// as the attack is held (see PlayerSkills.Whirlwind.cs).
    /// </summary>
    public partial class PlayerSkills
    {
        /// <summary>Attack speed while this melee skill is on, as a multiplier on the weapon's (the heavy ones are slower).</summary>
        public static float MeleeSpeed(SkillId id)
        {
            switch (id)
            {
                case SkillId.Cleave: return 0.85f;
                case SkillId.Pulverize: return 0.7f;
                case SkillId.Fissure:
                case SkillId.Aftershock: return 0.75f;
                case SkillId.ReapingArc: return 0.8f;
                case SkillId.LungingThrust: return 0.85f;
                case SkillId.CrossSlash: return 0.9f;
                default: return 1f;
            }
        }

        // Seconds one use takes at normal speed (wind-up, blow and recovery), matching its animation.
        private static float MeleeSeconds(SkillId id)
        {
            switch (id)
            {
                case SkillId.Cleave: return CleaveWindup + CleaveRecovery;
                case SkillId.Pulverize:
                case SkillId.Fissure:
                case SkillId.Aftershock: return SlamWindup + StrikeRecovery;
                case SkillId.ReapingArc: return 0.58f + StrikeRecovery;
                case SkillId.LungingThrust: return 0.66f;
                case SkillId.CrossSlash: return 2f * CrossCutStrike + CrossCutGap + CrossCutRecovery;
                case SkillId.FangStrike: return 0.12f + StrikeRecovery;
                case SkillId.VenomSlash: return CrossCutStrike + StrikeRecovery;
                default: return 1f;
            }
        }

        private const float SlamWindup = 0.72f;
        private const float StrikeRecovery = 0.18f;
        // Shared with both animation profiles so the visible cut and damage release stay aligned.
        internal const float CleaveSwingStart = 0.1f;
        internal const float CleaveWindup = 0.91f / 3f;
        internal const float CleaveRecovery = 0.18f;
        internal const float CleaveHalfAngle = 60f;

        /// <summary>Seconds between uses of a melee skill with the weapon in hand and the current attack speed.</summary>
        public float MeleeInterval(SkillId id) => 1f / Mathf.Max(0.01f,
            CharacterAttackAnimator.BaseAttackSpeed(CurrentWeapon()) * SkillAttackRate(id) * MeleeSpeed(id));

        /// <summary>Target selection uses the same specialized reach as the actual attack.</summary>
        public float MeleeSkillReach()
        {
            switch (ActiveMeleeSkill)
            {
                case SkillId.Cleave: return 1.6f * Mathf.Max(2.6f, (CharacterAttackAnimator.AttackRange(CurrentWeapon()) + 0.6f) * MeleeRangeMultiplier) * AreaMultiplier;
                case SkillId.Pulverize: return CharacterAttackAnimator.AttackRange(CurrentWeapon()) + 3.6f * AreaMultiplier * MeleeRangeMultiplier;
                case SkillId.ReapingArc: return 4.6f * MeleeRangeMultiplier * AreaMultiplier;
                case SkillId.LungingThrust: return 3f * MeleeRangeMultiplier + LungeStep * WeaponBonuses.RangeMultiplier;
                case SkillId.Whirlwind: return WhirlReach();
                case SkillId.Fissure: return FissureLength * MeleeRangeMultiplier;
                case SkillId.Aftershock: return 8f * AreaMultiplier * Mathf.Sqrt(WeaponBonuses.RangeMultiplier);
                case SkillId.CrossSlash: return (CharacterAttackAnimator.AttackRange(CurrentWeapon()) + 1.4f) * MeleeRangeMultiplier * AreaMultiplier;
                case SkillId.FangStrike: return 2.4f * MeleeRangeMultiplier;
                case SkillId.VenomSlash: return VenomSlashReach * MeleeRangeMultiplier * AreaMultiplier;
                default: return CharacterAttackAnimator.AttackRange(CurrentWeapon());
            }
        }

        /// <summary>
        /// The melee skill on the weapon in hand (the first, in case an older item carries more),
        /// which is the weapon's attack; null without one, or if it doesn't suit the weapon.
        /// </summary>
        public SkillId? ActiveMeleeSkill
        {
            get
            {
                ItemData weapon = inventory != null ? inventory.Equipment.GetActive(EquipSlot.MainHand) : null;
                if (weapon == null)
                    return null;
                foreach (StatModifier m in weapon.Modifiers)
                {
                    if (Array.IndexOf(SkillGrants.MeleeSkills, m.Stat) < 0 || Mathf.RoundToInt(m.Value) <= 0)
                        continue;
                    SkillDefinition skill = SkillBook.ForGrant(m.Stat);
                    return skill != null && CanUseWithWeapon(skill.Id, CurrentWeapon()) ? skill.Id : (SkillId?)null;
                }
                return null;
            }
        }

        /// <summary>
        /// The attack, with the weapon's melee skill: pays for one use and starts it (Whirlwind:
        /// starts spinning, for as long as the attack is held). False without one, or without the
        /// mana for it (the plain swing is used instead). <paramref name="interval"/>: seconds until
        /// the next attack may start.
        /// </summary>
        public bool TryStartMeleeSkill(out float interval)
        {
            interval = 0f;
            SkillId? id = ActiveMeleeSkill;
            if (id == null || stats == null || controller.IsDashing || controller.IsSkillCommitted || IsWhirling)
                return false;
            int level = ActiveAttackLevel;
            if (!stats.TrySpendMana(CostOf(id.Value, level)))
                return false;

            SkillDefinition skill = SkillBook.Get(id.Value);
            controller.CancelWalk();
            if (skill.Channelled)
            {
                BeginWhirlwind(level);
                return true;
            }

            interval = MeleeInterval(id.Value);
            // Faster attacks speed the whole move up; slower ones leave a pause after it, like a swing.
            float rate = Mathf.Max(1f, MeleeSeconds(id.Value) / interval);
            switch (skill.Id)
            {
                case SkillId.Cleave:
                    StartCoroutine(CommittedStrike(skill.Id, CleaveWindup, CurrentWeapon(), rate,
                        () => Cleave(skill, level), CleaveRecovery));
                    break;
                case SkillId.Pulverize:
                    StartCoroutine(CommittedStrike(skill.Id, SlamWindup, CurrentWeapon(), rate, () => Pulverize(skill, level)));
                    break;
                case SkillId.Fissure:
                    StartCoroutine(CommittedStrike(skill.Id, SlamWindup, CurrentWeapon(), rate, () => Fissure(skill, level)));
                    break;
                case SkillId.Aftershock:
                    StartCoroutine(CommittedStrike(skill.Id, SlamWindup, CurrentWeapon(), rate, () => Aftershock(skill, level)));
                    break;
                case SkillId.ReapingArc:
                    StartCoroutine(CommittedStrike(skill.Id, 0.58f, CurrentWeapon(), rate, () => ReapingArc(skill, level)));
                    break;
                case SkillId.LungingThrust:
                    StartCoroutine(Lunge(skill, level, rate));
                    break;
                case SkillId.CrossSlash:
                    StartCoroutine(CrossSlash(skill, level, rate));
                    break;
                case SkillId.FangStrike:
                    StartCoroutine(CommittedStrike(skill.Id, 0.12f, WeaponType.Dagger, rate, () => FangStrike(skill, level)));
                    break;
                case SkillId.VenomSlash:
                    StartCoroutine(CommittedStrike(skill.Id, CrossCutStrike, WeaponType.Dagger, rate, () => VenomSlash(skill, level)));
                    break;
            }
            return true;
        }

        private static bool CanUseWithWeapon(SkillId id, WeaponType weapon)
        {
            switch (id)
            {
                case SkillId.Cleave:
                case SkillId.Whirlwind:
                    return weapon == WeaponType.Sword || weapon == WeaponType.Greatsword ||
                           weapon == WeaponType.Axe || weapon == WeaponType.Greataxe;
                case SkillId.Pulverize:
                case SkillId.Fissure:
                case SkillId.Aftershock: return weapon == WeaponType.Mace || weapon == WeaponType.Maul;
                case SkillId.ReapingArc: return weapon == WeaponType.Axe || weapon == WeaponType.Greataxe;
                case SkillId.LungingThrust: return weapon == WeaponType.Sword || weapon == WeaponType.Greatsword;
                case SkillId.CrossSlash:
                case SkillId.FangStrike:
                case SkillId.VenomSlash: return weapon == WeaponType.Dagger;
                default: return true;
            }
        }

        private void PlaySkillAnimation(SkillId skill, WeaponType weapon, float rate, bool mirrored = false)
        {
            if (attackAnimator != null)
                attackAnimator.PlaySkillAttack(skill, weapon, rate, mirrored);
        }

        private float MeleeRangeMultiplier => (1f + Mathf.Max(0f, Stat(StatType.MeleeRange) + WeaponBonuses.Get(WeaponSkillEffect.Range)) / 100f);
        private float AreaMultiplier => DefenceMath.RadiusMultiplier(Stat(StatType.AreaOfEffect) + WeaponBonuses.Get(WeaponSkillEffect.Area));

        // ------------------------------------------------------------------ the moves

        // A 120-degree sweep in front of the player, without rotating their facing.
        private void Cleave(SkillDefinition skill, int level)
        {
            float reach = MeleeSkillReach();
            SkillEffects.Cone(transform.position, transform.forward, reach, CleaveHalfAngle,
                skill.Color, 0.3f, SkillEffects.DamagePattern(skill));
            Record(skill, level, reach);

            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level);
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, reach))
            {
                Vector3 to = enemy.transform.position - transform.position;
                to.y = 0f;
                if (Vector3.Angle(transform.forward, to) <= CleaveHalfAngle)
                    Hit(enemy, damage, CombatText.PhysicalColor, attack: true, melee: true, skillId: skill.Id);
            }
        }

        private IEnumerator CommittedStrike(SkillId skill, float windup, WeaponType animationWeapon, float rate,
            Action release, float recovery = StrikeRecovery)
        {
            controller.SetSkillCommit(true);
            PlaySkillAnimation(skill, animationWeapon, rate);
            yield return new WaitForSeconds(windup / rate);
            // Slams read the hammer position after the arm and body have applied their strike pose.
            if (skill == SkillId.Pulverize || skill == SkillId.Fissure || skill == SkillId.Aftershock)
                yield return new WaitForEndOfFrame();
            PlaySkillSound(skill, transform.position);
            release();
            yield return new WaitForSeconds(recovery / rate);
            controller.SetSkillCommit(false);
        }

        // How far Lunging Thrust carries the player at most, and how close it stops to its target.
        private const float LungeStep = 2.4f;
        private const float LungeStopShort = 1.4f;

        private IEnumerator Lunge(SkillDefinition skill, int level, float rate)
        {
            controller.SetSkillCommit(true);
            PlaySkillAnimation(skill.Id, CurrentWeapon(), rate);
            yield return new WaitForSeconds(0.34f / rate);

            // Closes the gap to what it's aimed at (up to LungeStep), never past it.
            float reach = 3f * MeleeRangeMultiplier;
            EnemyHealth target = AimedEnemy(reach + LungeStep) ?? EnemyInDirection(transform.forward, reach + LungeStep);
            float step = LungeStep * BonusesFor(skill.Id).RangeMultiplier;
            if (target != null)
            {
                Vector3 to = target.transform.position - transform.position;
                to.y = 0f;
                Face(to);
                step = Mathf.Clamp(to.magnitude - LungeStopShort, 0f, LungeStep * BonusesFor(skill.Id).RangeMultiplier);
            }
            if (step > 0.05f)
                controller.Dash(transform.forward, step, 0.16f / rate);
            yield return new WaitForSeconds(0.16f / rate);

            PlaySkillSound(skill.Id, transform.position);
            LungingThrust(skill, level, reach, target);
            yield return new WaitForSeconds(0.16f / rate);
            controller.SetSkillCommit(false);
        }

        // Cross Slash: two quick cuts, the second mirroring the first, each along one arm of the X.
        private const float CrossCutStrike = 0.2f;
        private const float CrossCutGap = 0.08f;
        private const float CrossCutRecovery = 0.08f;
        private const float CrossHalfWidth = 0.8f;

        private IEnumerator CrossSlash(SkillDefinition skill, int level, float rate)
        {
            controller.SetSkillCommit(true);
            WeaponType weapon = CurrentWeapon();
            for (int cut = 0; cut < 2; cut++)
            {
                PlaySkillAnimation(skill.Id, weapon, rate, mirrored: cut == 1);
                yield return new WaitForSeconds(CrossCutStrike / rate);
                PlaySkillSound(skill.Id, transform.position);
                CrossCut(skill, level, Quaternion.Euler(0f, cut == 0 ? 45f : -45f, 0f) * transform.forward);
                yield return new WaitForSeconds((cut == 0 ? CrossCutGap : CrossCutRecovery) / rate);
            }
            controller.SetSkillCommit(false);
        }

        // ------------------------------------------------------------------ the blows

        // Where a slam lands: under the weapon's head, else at the end of its reach.
        private Vector3 SlamPoint()
        {
            Vector3 impact = transform.position + transform.forward * CharacterAttackAnimator.AttackRange(CurrentWeapon());
            EquipmentVisuals visuals = GetComponentInChildren<EquipmentVisuals>();
            if (visuals != null && visuals.TryGetMainHandHeadPosition(out Vector3 head))
                impact = new Vector3(head.x, transform.position.y, head.z);
            return impact;
        }

        private void Pulverize(SkillDefinition skill, int level)
        {
            float radius = 3.6f * AreaMultiplier * MeleeRangeMultiplier;
            Vector3 impact = SlamPoint();
            SkillEffects.Shockwave(impact, radius, skill.Color, 0.42f, type: SkillEffects.DamagePattern(skill));
            // Match the impact treatment of a basic maul slam: ground burst plus a brief camera jolt.
            PoeClone.CameraSystem.CameraFollow.Shake(0.12f, 0.18f);
            Record(skill, level, radius, at: impact);
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level);
            foreach (EnemyHealth enemy in EnemiesWithin(impact, radius))
                Hit(enemy, damage, CombatText.PhysicalColor, attack: true, melee: true, skillId: skill.Id);
        }

        // How far a fissure runs (Melee Range lengthens it) and how wide it is (Area of Effect).
        private const float FissureLength = 9f;
        private const float FissureHalfWidth = 1f;

        // The ground splits from the slam straight ahead; each enemy along it is hit once, as the
        // crack reaches it.
        private void Fissure(SkillDefinition skill, int level)
        {
            float halfWidth = FissureHalfWidth * AreaMultiplier;
            float length = FissureLength * MeleeRangeMultiplier;
            Vector3 start = SlamPoint();
            Vector3 direction = transform.forward;
            PoeClone.CameraSystem.CameraFollow.Shake(0.08f, 0.14f);
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level);
            var struck = new HashSet<EnemyHealth>();
            SkillEffects.Fissure(start, direction, length, halfWidth, skill.Color, (from, to) =>
            {
                if (this == null || stats == null || stats.IsDead)
                    return;
                foreach (EnemyHealth enemy in EnemiesWithin(to, halfWidth))
                    if (struck.Add(enemy))
                        Hit(enemy, damage, CombatText.PhysicalColor, attack: true, melee: true, skillId: skill.Id);
            });
            Record(skill, level, halfWidth, points: new[] { start, start + direction * length });
        }

        // Aftershock's tremors: the first one's radius, how much wider each next one is, and the
        // pause between them.
        private const float AftershockFirstRadius = 1.3f;
        private const float AftershockGrowth = 1.3f;
        private const float AftershockStep = 0.13f;

        private static float[] AftershockRadii(float first, int count)
        {
            var radii = new float[count];
            for (int k = 0; k < count; k++)
                radii[k] = first * Mathf.Pow(AftershockGrowth, k);
            return radii;
        }

        // Tremors roll out ahead of the slam, each further and wider than the last and barely
        // overlapping it; each hits everything it lands on.
        private void Aftershock(SkillDefinition skill, int level)
        {
            int count = 3 + Mathf.RoundToInt(BonusesFor(skill.Id).Get(WeaponSkillEffect.ExtraWaves));
            float[] radii = AftershockRadii(AftershockFirstRadius * AreaMultiplier * Mathf.Sqrt(BonusesFor(skill.Id).RangeMultiplier), count);
            var centers = new Vector3[count];
            centers[0] = SlamPoint();
            for (int k = 1; k < count; k++)
                centers[k] = centers[k - 1] + transform.forward * (radii[k - 1] + radii[k]) * 0.85f;
            PoeClone.CameraSystem.CameraFollow.Shake(0.1f, 0.16f);
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level);
            SkillEffects.Ripple(centers, radii, AftershockStep, skill.Color, k =>
            {
                if (this == null || stats == null || stats.IsDead)
                    return;
                foreach (EnemyHealth enemy in EnemiesWithin(centers[k], radii[k]))
                    Hit(enemy, damage, CombatText.PhysicalColor, attack: true, melee: true, skillId: skill.Id);
            });
            Record(skill, level, radii[0], points: centers);
        }

        private void ReapingArc(SkillDefinition skill, int level)
        {
            float reach = 4.6f * MeleeRangeMultiplier * AreaMultiplier;
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level);
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, reach))
            {
                Vector3 to = enemy.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) <= 72f)
                    Hit(enemy, damage, CombatText.PhysicalColor, attack: true, melee: true, skillId: skill.Id);
            }
            SkillEffects.Cone(transform.position, transform.forward, reach, 72f, skill.Color, 0.34f);
            Record(skill, level, reach);
        }

        private void LungingThrust(SkillDefinition skill, int level, float reach, EnemyHealth target)
        {
            if (target == null || target.IsDead || !Within(target, reach))
                target = AimedEnemy(reach) ?? EnemyInDirection(transform.forward, reach);
            SkillEffects.Arc(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * reach, skill.Color, 0.25f);
            if (target != null)
                Hit(target, WeaponDamage() * skill.WeaponMultiplierAt(level), CombatText.PhysicalColor, attack: true, melee: true, skillId: skill.Id);
            Record(skill, level, reach);
        }

        // One arm of Cross Slash's X: a line through the player, as far behind as in front.
        private void CrossCut(SkillDefinition skill, int level, Vector3 along)
        {
            along.y = 0f;
            along.Normalize();
            float area = AreaMultiplier;
            float reach = (CharacterAttackAnimator.AttackRange(CurrentWeapon()) + 1.4f) * MeleeRangeMultiplier * area;
            float halfWidth = CrossHalfWidth * area;
            Vector3 center = transform.position;
            Vector3 from = center - along * reach;
            Vector3 to = center + along * reach;
            SkillEffects.Slash(from, to, halfWidth * 2f, skill.Color);
            Record(skill, level, halfWidth * 2f, points: new[] { from, to });

            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level);
            foreach (EnemyHealth enemy in EnemiesWithin(center, reach + halfWidth))
            {
                Vector3 offset = enemy.transform.position - center;
                offset.y = 0f;
                float along2 = Vector3.Dot(offset, along);
                // Slack for the enemy's own body (more for a big one).
                float body = PlayerCombat.DamageableReach(enemy, 0f);
                if (Mathf.Abs(along2) <= reach + body && (offset - along * along2).magnitude <= halfWidth + 0.4f + body)
                    Hit(enemy, damage, CombatText.PhysicalColor, attack: true, melee: true, skillId: skill.Id);
            }
        }

        private void FangStrike(SkillDefinition skill, int level)
        {
            float reach = 2.4f * MeleeRangeMultiplier;
            EnemyHealth target = AimedEnemy(reach) ?? EnemyInDirection(transform.forward, reach);
            Record(skill, level, reach);
            if (target != null)
            {
                Hit(target, WeaponDamage() * skill.WeaponMultiplierAt(level), skill.Color, true, melee: true, skillId: skill.Id);
                WeaponVenom.Apply(transform, target, WeaponDamage(), SkillBook.FangStrikePoison, 0f, inventory.Stats,
                    melee: true, durationMultiplier: BonusesFor(skill.Id).DurationMultiplier, radiusMultiplier: BonusesFor(skill.Id).AreaMultiplier, tree: BonusesFor(skill.Id));
            }
        }

        // Venom Slash: a short, wide cut in front that poisons everything it reaches.
        private const float VenomSlashReach = 2.8f;
        private const float VenomSlashHalfAngle = 75f;

        private void VenomSlash(SkillDefinition skill, int level)
        {
            float reach = VenomSlashReach * MeleeRangeMultiplier * AreaMultiplier;
            Vector3 center = transform.position;
            SkillEffects.Cone(center, transform.forward, reach, VenomSlashHalfAngle, skill.Color, 0.25f, SkillEffects.DamagePattern(skill));
            Record(skill, level, reach);
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level);
            float poison = SkillBook.VenomSlashPoison;
            foreach (EnemyHealth enemy in EnemiesWithin(center, reach))
            {
                Vector3 to = enemy.transform.position - center;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > VenomSlashHalfAngle)
                    continue;
                Hit(enemy, damage, skill.Color, attack: true, melee: true, skillId: skill.Id);
                if (!enemy.IsDead)
                    WeaponVenom.Apply(transform, enemy, WeaponDamage(), poison, 0f, inventory.Stats, melee: true, durationMultiplier: BonusesFor(skill.Id).DurationMultiplier, radiusMultiplier: BonusesFor(skill.Id).AreaMultiplier, tree: BonusesFor(skill.Id));
            }
        }
    }
}

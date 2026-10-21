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
        private const float ComboQueueWindow = 0.22f;
        private int meleeComboSwing;
        private int meleeComboMovement;
        private float meleeComboReadyAt;
        private float meleeComboBraceAt;
        private float meleeComboInterval;
        private float meleeComboRestartAt;
        private bool meleeComboQueued;
        private Coroutine meleeAction;
        private bool meleeAttackDelivered;
        private float meleeManaCost;
        private ItemData meleeComboWeapon;
        private bool meleeFinisher;
        private bool meleeCleaveAccent;
        private bool CleaveDoubleSlash => ActiveMeleeSkill == SkillId.Cleave && WeaponBonuses.Get(WeaponSkillEffect.CleaveDoubleSlash) > 0f;
        private bool CleaveExplosiveFinisher => ActiveMeleeSkill == SkillId.Cleave && meleeFinisher && WeaponBonuses.Get(WeaponSkillEffect.CleaveExplosion) > 0f;
        private bool MeleeImpactFinisher => meleeFinisher && (ActiveMeleeSkill != SkillId.Cleave || CleaveExplosiveFinisher);
        private float meleeComboDamage = 1f;
        private float ComboDamage => meleeComboDamage * (meleeFinisher ? WeaponBonuses.ComboDamageMultiplier : 1f);
        private float ComboArea => !meleeFinisher || CleaveDoubleSlash ? 1f : ActiveMeleeSkill == SkillId.Cleave && !CleaveExplosiveFinisher
            ? 1.15f : 1.35f * DefenceMath.RadiusMultiplier(WeaponBonuses.Get(WeaponSkillEffect.ComboArea));
        private float ComboKnockback => meleeFinisher ? WeaponBonuses.Get(WeaponSkillEffect.ComboKnockback) : 0f;
        public bool MeleeFollowupReady => meleeComboQueued && Time.time >= meleeComboReadyAt;
        public bool MeleeComboRestartReady => Time.time >= meleeComboRestartAt;
        private float meleeStepDistance;
        private const float CursorSlashStandOff = 1.5f;
        private GroundTelegraph.Decal cleaveFinisherPreview;

        private void ClearCleaveFinisherPreview()
        {
            if (cleaveFinisherPreview.Root != null) Destroy(cleaveFinisherPreview.Root);
            cleaveFinisherPreview = default;
        }

        public void ResetMeleeCombo()
        {
            ClearCleaveFinisherPreview();
            // Breaking a delivered Cleave starts a full recovery; an unreleased swing is free to cancel.
            // Completed finishers retain the normal cooldown already owned by PlayerCombat.
            if (meleeComboSwing > 0 && meleeAttackDelivered && !meleeFinisher && !CleaveDoubleSlash && meleeComboWeapon?.MainSkillGrant == StatType.GrantCleave)
                meleeComboRestartAt = Mathf.Max(meleeComboRestartAt, Time.time + meleeComboInterval);
            meleeComboSwing = 0;
            meleeComboQueued = false;
            meleeComboWeapon = null;
            controller?.SetComboBrace(false);
            if (attackAnimator != null)
            {
                attackAnimator.ComboBracing = false;
                attackAnimator.CleaveComboPose = false;
            }
        }

        /// <summary>Buffer one follow-up near the next swing, including a held attack.</summary>
        public bool QueueMeleeFollowup(bool attackHeld, bool freshPress = false)
        {
            // A finisher cannot buffer another fast follow-up or keep the old facing locked.
            // PlayerCombat retains its normal cooldown before the next opening attack.
            if (meleeComboSwing == 0 || meleeFinisher) return false;
            // Movement can break the opener during the first two-thirds of the link.
            if (meleeComboSwing == 1 && Time.time < meleeComboBraceAt &&
                controller.InputDirection().sqrMagnitude > 0.001f)
            {
                bool delivered = meleeAttackDelivered;
                if (meleeAction != null) StopCoroutine(meleeAction);
                meleeAction = null;
                ResetMeleeCombo();
                controller.SetSkillCommit(false);
                GetComponent<PlayerCombat>().CancelMeleeAttack(delivered);
                return false;
            }
            if (controller.MovementRevision != meleeComboMovement || MainWeapon != meleeComboWeapon ||
                (!meleeComboQueued && !controller.IsSkillCommitted && Time.time > meleeComboReadyAt + ComboQueueWindow))
            {
                ResetMeleeCombo();
                return false;
            }
            if (!meleeComboQueued && attackHeld && (freshPress || Time.time >= meleeComboReadyAt - ComboQueueWindow))
                meleeComboQueued = true;

            // Buffer early clicks without bracing until the final third of this link.
            // Recheck queued input each frame so the lock starts even without another click.
            if (meleeComboQueued && !controller.IsComboBraced && Time.time >= meleeComboBraceAt)
            {
                controller.SetComboBrace(true);
                if (attackAnimator != null) attackAnimator.ComboBracing = true;
            }
            return meleeComboQueued;
        }

        private void BeginMeleeCombo(SkillDefinition skill, float interval)
        {
            bool continuing = QueueMeleeFollowup(false) && Time.time >= meleeComboReadyAt;
            if (attackAnimator != null) attackAnimator.ComboBracing = false;
            meleeComboSwing = continuing ? meleeComboSwing + 1 : 1;
            int length = WeaponSkillRules.ComboLength(skill.Grant, CleaveDoubleSlash);
            meleeFinisher = length > 0 && meleeComboSwing % length == 0;
            meleeCleaveAccent = skill.Id == SkillId.Cleave && !CleaveDoubleSlash && meleeComboSwing % length == 2;
            meleeComboDamage = WeaponSkillRules.ComboHitMultiplier(skill.Grant, meleeComboSwing,
                explosiveCleave: WeaponBonuses.Get(WeaponSkillEffect.CleaveExplosion) > 0f, doubleSlash: CleaveDoubleSlash);
            float maxStep = meleeFinisher && !CleaveDoubleSlash ? 1.65f : 1.1f;
            meleeStepDistance = continuing ? maxStep : 0f;
            Vector3? cursorDirection = null;
            if (!TouchMode.Active)
            {
                // Sample each slash independently; never push through a close cursor or target.
                meleeStepDistance = 0f;
                if (PlayerCombat.CursorAimPoint(transform.position, out Vector3 cursor))
                {
                    Vector3 toCursor = cursor - transform.position;
                    toCursor.y = 0f;
                    meleeStepDistance = Mathf.Clamp(toCursor.magnitude - CursorSlashStandOff, 0f, maxStep);
                    if (toCursor.sqrMagnitude > 0.001f) cursorDirection = toCursor;
                }
            }
            controller.BeginComboAttack(continuing, cursorDirection);
            meleeAttackDelivered = false;
            meleeComboQueued = false;
            meleeComboWeapon = MainWeapon;
            meleeComboMovement = controller.MovementRevision;
            meleeComboBraceAt = Time.time + interval * (2f / 3f);
            meleeComboReadyAt = Time.time + interval;
        }

        // Move through the same obstacle and collision checks as walking, during the strike.
        private IEnumerator MeleeWindup(float seconds)
        {
            float elapsed = 0f;
            float stepped = 0f;
            float distance = meleeStepDistance;
            while (elapsed < seconds)
            {
                elapsed = Mathf.Min(seconds, elapsed + Time.deltaTime);
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(seconds * 0.45f, seconds, elapsed));
                float next = distance * progress;
                controller.MoveAttackStep(transform.forward * (next - stepped));
                stepped = next;
                if (cleaveFinisherPreview.Root != null)
                {
                    cleaveFinisherPreview.Follow(SlamPoint() + transform.forward * (distance - stepped), MeleeSkillReach() * ComboArea);
                    cleaveFinisherPreview.SetProgress(elapsed / Mathf.Max(0.001f, seconds));
                }
                yield return null;
            }
        }

        private void PlayMeleeSound(SkillId skill, bool accent = true)
        {
            if (skill != SkillId.Cleave || !CleaveExplosiveFinisher) PlaySkillSound(skill, transform.position);
            if (!accent) return;
            PlayComboImpactSound(skill, MeleeImpactFinisher, CleaveExplosiveFinisher ? SlamPoint() : transform.position);
            if (MeleeImpactFinisher)
                PoeClone.CameraSystem.CameraFollow.Shake(skill == SkillId.Cleave ? 0.22f : 0.16f, 0.16f);
            else if (meleeCleaveAccent)
                PoeClone.CameraSystem.CameraFollow.Shake(0.08f, 0.1f);
        }

        private EnemyHealth MeleeAimedEnemy(float reach) => controller.IsComboLocked
            ? EnemyInDirection(transform.forward, reach)
            : AimedEnemy(reach) ?? EnemyInDirection(transform.forward, reach);

        private void MeleeHit(EnemyHealth enemy, float damage, Color color, SkillId skill, float knockback)
        {
            float lifeBefore = enemy != null ? enemy.CurrentHealth : 0f;
            Hit(enemy, damage, color, attack: true, melee: true, skillId: skill);
            // Repeat the hit once; the repeat cannot trigger another echo.
            float echo = skill == SkillId.Cleave ? WeaponBonuses.Get(WeaponSkillEffect.CleaveEcho) : 0f;
            if (echo > 0 && enemy != null && !enemy.IsDead && enemy.CurrentHealth < lifeBefore &&
                UnityEngine.Random.value * 100f < echo)
                Hit(enemy, damage, color, attack: true, melee: true, skillId: skill);
            if (skill == SkillId.Cleave && CleaveExplosiveFinisher && enemy != null && !enemy.IsDead &&
                enemy.CurrentHealth < lifeBefore && !EnemyKinds.Get(enemy.KindIndex).IsBoss)
                enemy.GetComponent<PoeClone.Combat.Stagger>()?.Stun(2f);
            if (enemy != null && !enemy.IsDead && knockback > 0f)
                enemy.GetComponent<EnemyController>()?.Knockback(enemy.transform.position - transform.position, knockback);
        }

        private void FinishMeleeAction()
        {
            meleeAction = null;
            if (meleeFinisher) ResetMeleeCombo();
            else if (meleeComboSwing > 0)
                // The input window must survive the actual animation/recovery, including slow frames.
                meleeComboReadyAt = Mathf.Max(meleeComboReadyAt, Time.time);
            controller.SetSkillCommit(false);
        }

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
        internal const float CleaveFinisherHalfAngle = 180f;
        private float CleaveAttackHalfAngle => CleaveExplosiveFinisher ? CleaveFinisherHalfAngle : CleaveHalfAngle;
        // Skill reach belongs to the skill, independent of weapon model size or handedness.
        private const float CleaveBaseReach = 4.48f;
        private const float CrossSlashBaseReach = 3.1f;
        private const float SlamImpactDistance = 2.2f;

        /// <summary>Seconds between uses of a melee skill with the weapon in hand and the current attack speed.</summary>
        public float MeleeInterval(SkillId id) => 1f / Mathf.Max(0.01f,
            CharacterAttackAnimator.BaseAttackSpeed(CurrentWeapon()) * SkillAttackRate(id) * MeleeSpeed(id));

        /// <summary>Target selection uses the same specialized reach as the actual attack.</summary>
        public float MeleeSkillReach()
        {
            switch (ActiveMeleeSkill)
            {
                case SkillId.Cleave: return CleaveBaseReach * AreaMultiplier;
                case SkillId.Pulverize: return SlamImpactDistance + 3.6f * AreaMultiplier;
                case SkillId.ReapingArc: return 4.6f * AreaMultiplier;
                case SkillId.LungingThrust: return (3f + LungeStep) * AreaMultiplier;
                case SkillId.Whirlwind: return WhirlReach();
                case SkillId.Fissure: return FissureLength * AreaMultiplier;
                case SkillId.Aftershock: return 8f * AreaMultiplier;
                case SkillId.CrossSlash: return CrossSlashBaseReach * AreaMultiplier;
                case SkillId.FangStrike: return 2.4f * AreaMultiplier;
                case SkillId.VenomSlash: return VenomSlashReach * AreaMultiplier;
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
        /// The attack, with the weapon's melee skill: checks affordability and pays on release (Whirlwind:
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
            SkillDefinition skill = SkillBook.Get(id.Value);
            float cost = CostOf(id.Value, level);
            bool affordable = skill.Channelled ? stats.TrySpendMana(cost) : stats.CanAffordSkill(cost);
            if (!affordable)
            {
                ResetMeleeCombo();
                return false;
            }

            controller.CancelWalk();
            if (skill.Channelled)
            {
                ResetMeleeCombo();
                meleeFinisher = false;
                BeginWhirlwind(level);
                return true;
            }

            interval = MeleeInterval(id.Value);
            // Keep the normal cooldown between combos; fit each pose and hit into its fast link.
            float comboSpeed = WeaponSkillRules.ComboFollowupSpeed(skill.Grant, CleaveDoubleSlash);
            float comboInterval = interval / comboSpeed;
            float rate = Mathf.Max(1f, MeleeSeconds(id.Value) / interval) * comboSpeed;
            BeginMeleeCombo(skill, comboInterval);
            meleeManaCost = cost;
            meleeComboInterval = interval;
            switch (skill.Id)
            {
                case SkillId.Cleave:
                    // Fit every hit, including the explosion, into the same combo link.
                    rate = (CleaveWindup + CleaveRecovery) / comboInterval;
                    if (CleaveDoubleSlash)
                    {
                        meleeAction = StartCoroutine(CleaveDoubleCut(skill, level, rate * 2f));
                        break;
                    }
                    meleeAction = StartCoroutine(CommittedStrike(skill.Id, CleaveWindup, CurrentWeapon(), rate,
                        () => Cleave(skill, level), CleaveRecovery));
                    break;
                case SkillId.Pulverize:
                    meleeAction = StartCoroutine(CommittedStrike(skill.Id, SlamWindup, CurrentWeapon(), rate, () => Pulverize(skill, level)));
                    break;
                case SkillId.Fissure:
                    meleeAction = StartCoroutine(CommittedStrike(skill.Id, SlamWindup, CurrentWeapon(), rate, () => Fissure(skill, level)));
                    break;
                case SkillId.Aftershock:
                    meleeAction = StartCoroutine(CommittedStrike(skill.Id, SlamWindup, CurrentWeapon(), rate, () => Aftershock(skill, level)));
                    break;
                case SkillId.ReapingArc:
                    meleeAction = StartCoroutine(CommittedStrike(skill.Id, 0.58f, CurrentWeapon(), rate, () => ReapingArc(skill, level)));
                    break;
                case SkillId.LungingThrust:
                    meleeAction = StartCoroutine(Lunge(skill, level, rate));
                    break;
                case SkillId.CrossSlash:
                    meleeAction = StartCoroutine(CrossSlash(skill, level, rate));
                    break;
                case SkillId.FangStrike:
                    meleeAction = StartCoroutine(CommittedStrike(skill.Id, 0.12f, WeaponType.Dagger, rate, () => FangStrike(skill, level)));
                    break;
                case SkillId.VenomSlash:
                    meleeAction = StartCoroutine(CommittedStrike(skill.Id, CrossCutStrike, WeaponType.Dagger, rate, () => VenomSlash(skill, level)));
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
            {
                attackAnimator.PlaySkillAttack(skill, weapon, rate, mirrored, finisher: MeleeImpactFinisher);
                attackAnimator.CleaveComboPose = skill == SkillId.Cleave && !CleaveExplosiveFinisher;
            }
        }

        private float AreaMultiplier => DefenceMath.RadiusMultiplier(Stat(StatType.AreaOfEffect) + WeaponBonuses.Get(WeaponSkillEffect.Area));

        // ------------------------------------------------------------------ the moves

        // Normal cuts sweep in front; the explosive finisher radiates from the ground strike.
        private void Cleave(SkillDefinition skill, int level, int cut = -1)
        {
            float reach = MeleeSkillReach() * ComboArea;
            Vector3 center = CleaveExplosiveFinisher ? SlamPoint() : transform.position;
            // Short-lived footprints keep the two rapid cuts from merging into one fan.
            SkillEffects.Cone(center, transform.forward, reach, CleaveAttackHalfAngle,
                skill.Color, cut >= 0 ? 0.1f : 0.3f, SkillEffects.DamagePattern(skill));
            Record(skill, level, reach, count: cut + 1, at: center);

            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level) * ComboDamage;
            float knockback = ComboKnockback;
            foreach (EnemyHealth enemy in EnemiesWithin(center, reach))
            {
                Vector3 to = enemy.transform.position - center;
                to.y = 0f;
                if (Vector3.Angle(transform.forward, to) <= CleaveAttackHalfAngle)
                    MeleeHit(enemy, damage, CombatText.PhysicalColor, skill.Id, knockback);
            }
        }

        // Multi-hit attacks pay once, when their first hit actually releases.
        private bool TryReleaseMeleeAttack()
        {
            if (meleeAttackDelivered) return true;
            if (stats.TrySpendMana(meleeManaCost))
            {
                meleeAttackDelivered = true;
                return true;
            }

            // Another skill may have spent the available resource during the windup.
            meleeAction = null;
            ResetMeleeCombo();
            controller.SetSkillCommit(false);
            GetComponent<PlayerCombat>().CancelMeleeAttack(false);
            return false;
        }

        private IEnumerator CommittedStrike(SkillId skill, float windup, WeaponType animationWeapon, float rate,
            Action release, float recovery = StrikeRecovery)
        {
            controller.SetSkillCommit(true);
            PlaySkillAnimation(skill, animationWeapon, rate, mirrored: skill == SkillId.Cleave && meleeComboSwing % 2 == 0);
            if (skill == SkillId.Cleave && CleaveExplosiveFinisher)
            {
                ClearCleaveFinisherPreview();
                Color markerColor = new Color(0.65f, 0.25f, 1f);
                cleaveFinisherPreview = GroundTelegraph.ImpactDisc(
                    SlamPoint() + transform.forward * meleeStepDistance, transform.forward,
                    MeleeSkillReach() * ComboArea, CleaveFinisherHalfAngle, markerColor, SkillEffects.DamagePattern(SkillBook.Get(skill)));
                cleaveFinisherPreview.Root.name = "Cleave finisher area";
                cleaveFinisherPreview.Block.SetFloat("_Impact", 0f);
                cleaveFinisherPreview.Block.SetColor("_WarnColor", Color.Lerp(Color.black, markerColor, 0.2f));
                cleaveFinisherPreview.Block.SetColor("_FillColor", markerColor);
                cleaveFinisherPreview.SetProgress(0f);
            }
            yield return MeleeWindup(windup / rate);
            ClearCleaveFinisherPreview();
            if (!TryReleaseMeleeAttack()) yield break;
            PlayMeleeSound(skill);
            release();
            yield return new WaitForSeconds(recovery / rate);
            FinishMeleeAction();
        }

        // Two complete alternating swing profiles, with a separate release, sound and
        // damage event per cut. One pair is one input/commitment; the second pair is optional.
        private IEnumerator CleaveDoubleCut(SkillDefinition skill, int level, float rate)
        {
            controller.SetSkillCommit(true);
            for (int cut = 0; cut < 2; cut++)
            {
                PlaySkillAnimation(skill.Id, CurrentWeapon(), rate, mirrored: cut == 1);
                if (cut == 0) yield return MeleeWindup(CleaveWindup / rate);
                else yield return new WaitForSeconds(CleaveWindup / rate);
                if (!TryReleaseMeleeAttack()) yield break;
                PlayMeleeSound(skill.Id, accent: false);
                Cleave(skill, level, cut);
                yield return new WaitForSeconds(CleaveRecovery / rate);
            }
            FinishMeleeAction();
        }

        // How far Lunging Thrust carries the player at most, and how close it stops to its target.
        private const float LungeStep = 2.4f;
        private const float LungeStopShort = 1.4f;

        private IEnumerator Lunge(SkillDefinition skill, int level, float rate)
        {
            controller.SetSkillCommit(true);
            PlaySkillAnimation(skill.Id, CurrentWeapon(), rate);
            yield return MeleeWindup(0.34f / rate);

            // Closes the gap to what it's aimed at (up to LungeStep), never past it.
            float reach = 3f * AreaMultiplier * ComboArea;
            float step = LungeStep * AreaMultiplier;
            EnemyHealth target = MeleeAimedEnemy(reach + step);
            if (target != null)
            {
                Vector3 to = target.transform.position - transform.position;
                to.y = 0f;
                Face(to);
                step = Mathf.Clamp(to.magnitude - LungeStopShort, 0f, LungeStep * AreaMultiplier);
            }
            if (step > 0.05f)
                controller.Dash(transform.forward, step, 0.16f / rate, meleeAttack: true);
            yield return new WaitForSeconds(0.16f / rate);

            if (!TryReleaseMeleeAttack()) yield break;
            PlayMeleeSound(skill.Id);
            LungingThrust(skill, level, reach, target);
            yield return new WaitForSeconds(0.16f / rate);
            FinishMeleeAction();
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
                if (cut == 0) yield return MeleeWindup(CrossCutStrike / rate);
                else yield return new WaitForSeconds(CrossCutStrike / rate);
                if (!TryReleaseMeleeAttack()) yield break;
                PlayMeleeSound(skill.Id, accent: cut == 0);
                CrossCut(skill, level, Quaternion.Euler(0f, cut == 0 ? 45f : -45f, 0f) * transform.forward);
                yield return new WaitForSeconds((cut == 0 ? CrossCutGap : CrossCutRecovery) / rate);
            }
            FinishMeleeAction();
        }

        // ------------------------------------------------------------------ the blows

        // A fixed impact offset keeps slam placement identical across weapon models.
        private Vector3 SlamPoint() => transform.position + transform.forward * SlamImpactDistance;

        private void Pulverize(SkillDefinition skill, int level)
        {
            float radius = 3.6f * AreaMultiplier * ComboArea;
            Vector3 impact = SlamPoint();
            SkillEffects.Shockwave(impact, radius, skill.Color, 0.42f, type: SkillEffects.DamagePattern(skill));
            // Match the impact treatment of a basic maul slam: ground burst plus a brief camera jolt.
            PoeClone.CameraSystem.CameraFollow.Shake(0.12f, 0.18f);
            Record(skill, level, radius, at: impact);
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level) * ComboDamage;
            float knockback = ComboKnockback;
            foreach (EnemyHealth enemy in EnemiesWithin(impact, radius))
                MeleeHit(enemy, damage, CombatText.PhysicalColor, skill.Id, knockback);
        }

        // Increased area scales both the fissure's length and width.
        private const float FissureLength = 9f;
        private const float FissureHalfWidth = 1f;

        // The ground splits from the slam straight ahead; each enemy along it is hit once, as the
        // crack reaches it.
        private void Fissure(SkillDefinition skill, int level)
        {
            float halfWidth = FissureHalfWidth * AreaMultiplier * ComboArea;
            float length = FissureLength * AreaMultiplier * ComboArea;
            Vector3 start = SlamPoint();
            Vector3 direction = transform.forward;
            PoeClone.CameraSystem.CameraFollow.Shake(0.08f, 0.14f);
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level) * ComboDamage;
            float knockback = ComboKnockback;
            var struck = new HashSet<EnemyHealth>();
            SkillEffects.Fissure(start, direction, length, halfWidth, skill.Color, (from, to) =>
            {
                if (this == null || stats == null || stats.IsDead)
                    return;
                foreach (EnemyHealth enemy in EnemiesWithin(to, halfWidth))
                    if (struck.Add(enemy))
                        MeleeHit(enemy, damage, CombatText.PhysicalColor, skill.Id, knockback);
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
            float[] radii = AftershockRadii(AftershockFirstRadius * AreaMultiplier * ComboArea, count);
            var centers = new Vector3[count];
            centers[0] = SlamPoint();
            for (int k = 1; k < count; k++)
                centers[k] = centers[k - 1] + transform.forward * (radii[k - 1] + radii[k]) * 0.85f;
            PoeClone.CameraSystem.CameraFollow.Shake(0.1f, 0.16f);
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level) * ComboDamage;
            float knockback = ComboKnockback;
            SkillEffects.Ripple(centers, radii, AftershockStep, skill.Color, k =>
            {
                if (this == null || stats == null || stats.IsDead)
                    return;
                foreach (EnemyHealth enemy in EnemiesWithin(centers[k], radii[k]))
                    MeleeHit(enemy, damage, CombatText.PhysicalColor, skill.Id, knockback);
            });
            Record(skill, level, radii[0], points: centers);
        }

        private void ReapingArc(SkillDefinition skill, int level)
        {
            float reach = 4.6f * AreaMultiplier * ComboArea;
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level) * ComboDamage;
            float knockback = ComboKnockback;
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, reach))
            {
                Vector3 to = enemy.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) <= 72f)
                    MeleeHit(enemy, damage, CombatText.PhysicalColor, skill.Id, knockback);
            }
            SkillEffects.Cone(transform.position, transform.forward, reach, 72f, skill.Color, 0.34f);
            Record(skill, level, reach);
        }

        private void LungingThrust(SkillDefinition skill, int level, float reach, EnemyHealth target)
        {
            if (target == null || target.IsDead || !Within(target, reach))
                target = MeleeAimedEnemy(reach);
            SkillEffects.Arc(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * reach, skill.Color, 0.25f);
            foreach (EnemyHealth enemy in MeleeThrustTargets(target, reach, skill))
                MeleeHit(enemy, WeaponDamage() * skill.WeaponMultiplierAt(level) * ComboDamage, CombatText.PhysicalColor, skill.Id, ComboKnockback);
            Record(skill, level, reach);
        }

        // Single-target thrusts open into a crowd-clearing cone on their combo finisher.
        private IEnumerable<EnemyHealth> MeleeThrustTargets(EnemyHealth primary, float reach, SkillDefinition skill)
        {
            if (!meleeFinisher)
            {
                if (primary != null) yield return primary;
                yield break;
            }
            SkillEffects.Cone(transform.position, transform.forward, reach, CleaveHalfAngle, skill.Color, 0.3f, SkillEffects.DamagePattern(skill));
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, reach))
            {
                Vector3 to = enemy.transform.position - transform.position;
                to.y = 0f;
                if (enemy == primary || Vector3.Angle(transform.forward, to) <= CleaveHalfAngle)
                    yield return enemy;
            }
        }

        // One arm of Cross Slash's X: a line through the player, as far behind as in front.
        private void CrossCut(SkillDefinition skill, int level, Vector3 along)
        {
            along.y = 0f;
            along.Normalize();
            float area = AreaMultiplier * ComboArea;
            float reach = CrossSlashBaseReach * area;
            float halfWidth = CrossHalfWidth * area;
            Vector3 center = transform.position;
            Vector3 from = center - along * reach;
            Vector3 to = center + along * reach;
            SkillEffects.Slash(from, to, halfWidth * 2f, skill.Color);
            Record(skill, level, halfWidth * 2f, points: new[] { from, to });

            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level) * ComboDamage;
            float knockback = ComboKnockback;
            foreach (EnemyHealth enemy in EnemiesWithin(center, reach + halfWidth))
            {
                Vector3 offset = enemy.transform.position - center;
                offset.y = 0f;
                float along2 = Vector3.Dot(offset, along);
                // Slack for the enemy's own body (more for a big one).
                float body = PlayerCombat.DamageableReach(enemy, 0f);
                if (Mathf.Abs(along2) <= reach + body && (offset - along * along2).magnitude <= halfWidth + 0.4f + body)
                    MeleeHit(enemy, damage, CombatText.PhysicalColor, skill.Id, knockback);
            }
        }

        private void FangStrike(SkillDefinition skill, int level)
        {
            float reach = 2.4f * AreaMultiplier * ComboArea;
            EnemyHealth target = MeleeAimedEnemy(reach);
            Record(skill, level, reach);
            foreach (EnemyHealth enemy in MeleeThrustTargets(target, reach, skill))
            {
                MeleeHit(enemy, WeaponDamage() * skill.WeaponMultiplierAt(level) * ComboDamage, skill.Color, skill.Id, ComboKnockback);
                WeaponVenom.Apply(transform, enemy, WeaponDamage() * ComboDamage, SkillBook.FangStrikePoison, 0f, inventory.Stats,
                    melee: true, durationMultiplier: BonusesFor(skill.Id).DurationMultiplier, radiusMultiplier: BonusesFor(skill.Id).AreaMultiplier, tree: BonusesFor(skill.Id));
            }
        }

        // Venom Slash: a short, wide cut in front that poisons everything it reaches.
        private const float VenomSlashReach = 2.8f;
        private const float VenomSlashHalfAngle = 75f;

        private void VenomSlash(SkillDefinition skill, int level)
        {
            float reach = VenomSlashReach * AreaMultiplier * ComboArea;
            Vector3 center = transform.position;
            SkillEffects.Cone(center, transform.forward, reach, VenomSlashHalfAngle, skill.Color, 0.25f, SkillEffects.DamagePattern(skill));
            Record(skill, level, reach);
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level) * ComboDamage;
            float knockback = ComboKnockback;
            float poison = SkillBook.VenomSlashPoison;
            foreach (EnemyHealth enemy in EnemiesWithin(center, reach))
            {
                Vector3 to = enemy.transform.position - center;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > VenomSlashHalfAngle)
                    continue;
                MeleeHit(enemy, damage, skill.Color, skill.Id, knockback);
                if (!enemy.IsDead)
                    WeaponVenom.Apply(transform, enemy, WeaponDamage(), poison, 0f, inventory.Stats, melee: true, durationMultiplier: BonusesFor(skill.Id).DurationMultiplier, radiusMultiplier: BonusesFor(skill.Id).AreaMultiplier, tree: BonusesFor(skill.Id));
            }
        }
    }
}

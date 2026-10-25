using System.Collections;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.UI;

namespace PoeClone.Skills
{
    /// <summary>
    /// Channelling: a skill that keeps running while its button is held (Whirlwind, Pulverize's
    /// Cataclysm). Channelling is a general keyword, so tree effects that name it apply to every
    /// channelled skill: damage taken while channelling, and immunity to the stagger that otherwise
    /// interrupts a channel. Cataclysm itself replaces Pulverize's combo: holding the attack raises
    /// the weapon and fills a charge, and letting go slams with damage and radius from that charge.
    /// </summary>
    public partial class PlayerSkills
    {
        private bool cataclysmCharging;
        private float cataclysmStart;
        private float cataclysmFullAfter;
        private int cataclysmLevel;
        private bool cataclysmFullShown;
        // The releasing slam's radius and impact presentation; 1 and false for ordinary slams.
        private float cataclysmRadius = 1f;
        private bool cataclysmImpact;

        /// <summary>A channelled skill is running (Whirlwind spinning, Cataclysm charging).</summary>
        public bool IsChannelling => whirling || cataclysmCharging;

        /// <summary>Channelling with "cannot be stunned or staggered while channelling".</summary>
        public bool ChannelStunImmune => IsChannelling && WeaponBonuses.Has(WeaponSkillEffect.ChannelUnbroken);

        /// <summary>Damage taken from enemies while channelling (1 when not channelling).</summary>
        public float ChannelTakenMultiplier => IsChannelling ? WeaponBonuses.ChannelTakenMultiplier : 1f;

        private bool PulverizeCataclysm => ActiveMeleeSkill == SkillId.Pulverize && WeaponBonuses.Has(WeaponSkillEffect.PulverizeCataclysm);

        private float CataclysmCharge => Mathf.Clamp01((Time.time - cataclysmStart) / Mathf.Max(0.01f, cataclysmFullAfter));
        private float CataclysmPreviewRadius => 3.6f * AreaMultiplier * WeaponSkillRules.CataclysmRadius(CataclysmCharge);

        /// <summary>A stagger interrupts a channel and needs a fresh attack press, unless the channel is unbroken.</summary>
        public void InterruptChannel()
        {
            if (!IsChannelling || ChannelStunImmune) return;
            channelRequiresRelease = true;
            if (whirling) EndWhirlwind(released: false);
            CancelCataclysm();
        }

        // TryStartMeleeSkill already paid the whole charge.
        private void BeginCataclysm(int level)
        {
            cataclysmCharging = true;
            cataclysmLevel = level;
            cataclysmStart = Time.time;
            cataclysmFullShown = false;
            cataclysmFullAfter = MeleeInterval(SkillId.Pulverize) * WeaponSkillRules.CataclysmChargeIntervals *
                Mathf.Max(0.2f, 1f + WeaponBonuses.Get(WeaponSkillEffect.ChargeTime) / 100f);
            controller.SetChannelling(true);
            // The weapon rises over the whole charge and holds at the top of the swing.
            if (attackAnimator != null)
                attackAnimator.HoldSkillPose(SkillId.Pulverize, CurrentWeapon(), SlamWindup / cataclysmFullAfter);
            ClearMeleeFinisherPreview();
            SkillDefinition skill = SkillBook.Get(SkillId.Pulverize);
            Color marker = new Color(0.65f, 0.25f, 1f);
            meleeFinisherPreview = GroundTelegraph.ImpactDisc(SlamPoint(), transform.forward, CataclysmPreviewRadius,
                CleaveFinisherHalfAngle, marker, SkillEffects.DamagePattern(skill));
            meleeFinisherPreview.Root.name = "Cataclysm charge area";
            meleeFinisherPreview.Block.SetFloat("_Impact", 0f);
            meleeFinisherPreview.Block.SetColor("_WarnColor", Color.Lerp(Color.black, marker, 0.2f));
            meleeFinisherPreview.Block.SetColor("_FillColor", marker);
            meleeFinisherPreview.SetProgress(0f);
        }

        private void UpdateCataclysm(bool free)
        {
            if (!PulverizeCataclysm || !free)
            {
                CancelCataclysm();
                return;
            }
            Face(AimDirection());
            float charge = CataclysmCharge;
            if (meleeFinisherPreview.Root != null)
            {
                meleeFinisherPreview.Follow(SlamPoint(), CataclysmPreviewRadius);
                meleeFinisherPreview.SetProgress(charge);
            }
            if (charge >= 1f && !cataclysmFullShown)
            {
                cataclysmFullShown = true;
                var audio = PoeClone.Audio.AudioManager.Instance;
                audio?.PlayAtPoint(audio.Sfx("piercing_charge_ready"), transform.position);
                SkillEffects.Shockwave(transform.position, 0.9f, UiKit.Gold, 0.18f);
            }
            if (!AttackHeld)
            {
                CancelCataclysm(keepPose: true);
                meleeAction = StartCoroutine(CataclysmSlam(charge));
            }
        }

        private void CancelCataclysm(bool keepPose = false)
        {
            if (!cataclysmCharging) return;
            cataclysmCharging = false;
            ClearMeleeFinisherPreview();
            if (controller != null) controller.SetChannelling(false);
            if (!keepPose && attackAnimator != null) attackAnimator.CancelAttack();
        }

        private IEnumerator CataclysmSlam(float charge)
        {
            controller.SetSkillCommit(true);
            float recovery = MeleeInterval(SkillId.Pulverize) * WeaponSkillRules.CataclysmRecoveryIntervals;
            if (attackAnimator != null)
            {
                // The held pose strikes at once and recovers over the rest of the action.
                attackAnimator.ReleaseHold();
                attackAnimator.SetSkillRate(StrikeRecovery / Mathf.Max(0.05f, recovery));
            }
            yield return null;
            SkillDefinition skill = SkillBook.Get(SkillId.Pulverize);
            meleeComboDamage = WeaponSkillRules.CataclysmDamage(charge);
            cataclysmRadius = WeaponSkillRules.CataclysmRadius(charge);
            cataclysmImpact = charge >= 0.5f;
            PlaySkillSound(SkillId.Pulverize, transform.position);
            if (cataclysmImpact)
            {
                PlayComboImpactSound(SkillId.Pulverize, true, SlamPoint());
                PoeClone.CameraSystem.CameraFollow.Shake(0.1f + 0.15f * charge, 0.2f);
            }
            Pulverize(skill, cataclysmLevel);
            meleeComboDamage = 1f;
            cataclysmRadius = 1f;
            cataclysmImpact = false;
            meleeNextReadyAt = Time.time + recovery;
            yield return new WaitForSeconds(recovery);
            FinishMeleeAction();
        }
    }
}

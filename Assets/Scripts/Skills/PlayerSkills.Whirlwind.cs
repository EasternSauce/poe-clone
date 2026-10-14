using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Skills
{
    /// <summary>
    /// Whirlwind, the channelled melee skill: on a weapon it is the weapon's attack like the other
    /// melee skills, and holding the attack (left click / the aim stick) spins the player, hitting
    /// everything in reach several times a second, passing through enemies and drifting towards the
    /// cursor (faster the further it is, sprinting speed at <see cref="WhirlFullSpeedDiameters"/>
    /// body widths away; on touch, the joystick or the aim stick). It costs mana every
    /// <see cref="ChannelTick"/> seconds, paid up front, and stops when the attack is let go or the
    /// mana runs out.
    /// </summary>
    public partial class PlayerSkills
    {
        /// <summary>Seconds between a channelled skill's mana payments.</summary>
        public const float ChannelTick = 0.5f;

        private const float WhirlMinTurnsPerSecond = 2.5f;
        private const float WhirlMaxTurnsPerSecond = 6f;
        // Whirlwind's hit circle covers 30% less area than the weapon's reach would give.
        private static readonly float WhirlAreaScale = DefenceMath.RadiusMultiplier(-30f);
        // Hits per second with a sword and no increased attack speed. Other weapons hit as much
        // faster or slower as their own swings are, so a maul's big hits don't come at a sword's rate.
        private const float WhirlSwordHitsPerSecond = 4f;
        private const float WhirlFullSpeedDiameters = 5f;

        private bool whirling;
        private int whirlLevel;
        private float whirlNextMana;
        private float whirlNextHit;
        private Vector3 whirlFacing;
        // Enemies whose colliders the player passes through while spinning, restored when it ends.
        private readonly Dictionary<EnemyHealth, Collider[]> whirlIgnored = new Dictionary<EnemyHealth, Collider[]>();
        private CharacterController body;

        /// <summary>Whirlwind is spinning.</summary>
        public bool IsWhirling => whirling;

        /// <summary>Where the player is headed: while spinning, the way Whirlwind drifts (or the facing it began with), not the spin.</summary>
        public Vector3 Heading => whirling ? whirlFacing : transform.forward;

        /// <summary>Signed yaw speed for replicas, so fast spins keep their direction between snapshots.</summary>
        public float WhirlYawSpeed => whirling ? -360f * Mathf.Clamp(WhirlHitsPerSecond(), WhirlMinTurnsPerSecond, WhirlMaxTurnsPerSecond) : 0f;

        /// <summary>Whether the attack is held right now (set by <see cref="PlayerCombat"/>): Whirlwind spins while it is.</summary>
        public bool AttackHeld { get; set; }

        // TryStartMeleeSkill already paid the first tick.
        private void BeginWhirlwind(int level)
        {
            whirling = true;
            whirlLevel = level;
            whirlFacing = transform.forward;
            whirlNextMana = Time.time + ChannelTick;
            whirlNextHit = Time.time;
            controller.CancelWalk();
            controller.SetChannelling(true);
            PlaySkillSound(SkillId.Whirlwind, transform.position);
        }

        private void EndWhirlwind()
        {
            if (!whirling)
                return;
            whirling = false;
            foreach (Collider[] colliders in whirlIgnored.Values)
                IgnoreEnemy(colliders, false);
            whirlIgnored.Clear();
            HeldArea.Release(transform);
            if (controller != null)
                controller.SetChannelling(false);
            if (attackAnimator != null)
                attackAnimator.ReleaseHold();
        }

        private void UpdateWhirlwind(bool free)
        {
            if (!free || !AttackHeld || ActiveMeleeSkill != SkillId.Whirlwind)
            {
                EndWhirlwind();
                return;
            }

            if (Time.time >= whirlNextMana)
            {
                if (!stats.TrySpendMana(CostOf(SkillId.Whirlwind, ActiveAttackLevel)))
                {
                    CombatText.Show(transform.position + Vector3.up * 2f, "Not enough " + CostResource, CombatText.ColdColor, 0.8f);
                    EndWhirlwind();
                    return;
                }
                whirlNextMana += ChannelTick;
                PlaySkillSound(SkillId.Whirlwind, transform.position);
            }

            if (attackAnimator != null && !attackAnimator.IsAttacking)
                attackAnimator.HoldSkillPose(SkillId.Whirlwind, CurrentWeapon());
            IgnoreEnemiesWhileWhirling();
            Vector3 whirlSteer = Vector3.ClampMagnitude(WhirlSteer(), 1f);
            if (whirlSteer.sqrMagnitude > 0.01f)
                whirlFacing = whirlSteer.normalized;
            controller.SteerChannel(whirlSteer);
            // Counter-clockwise, so the sword arm sweeps forward and the blade leads (a forehand
            // cut); a turn per hit, so a heavy weapon spins heavier.
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + WhirlYawSpeed * Time.deltaTime, 0f);

            // The hit circle stays on the ground, centred on the player, while the spin lasts.
            SkillDefinition skill = SkillBook.Get(SkillId.Whirlwind);
            float reach = WhirlReach();
            HeldArea.Hold(transform, transform.position, reach, skill.Color, SkillEffects.DamagePattern(skill), linger: 0.1f);

            if (Time.time >= whirlNextHit)
            {
                WhirlHit(skill, whirlLevel, reach);
                whirlNextHit = Time.time + 1f / WhirlHitsPerSecond();
            }
        }

        private float WhirlHitsPerSecond() => WhirlSwordHitsPerSecond * SkillAttackRate(SkillId.Whirlwind) *
            CharacterAttackAnimator.BaseAttackSpeed(CurrentWeapon()) / CharacterAttackAnimator.BaseAttackSpeed(WeaponType.Sword);

        // Newly met enemies included: one that spawns mid-spin can't block it either.
        private void IgnoreEnemiesWhileWhirling()
        {
            foreach (EnemyHealth enemy in EnemyHealth.Active)
            {
                if (enemy == null || whirlIgnored.ContainsKey(enemy))
                    continue;
                Collider[] colliders = enemy.GetComponentsInChildren<Collider>();
                whirlIgnored[enemy] = colliders;
                IgnoreEnemy(colliders, true);
            }
        }

        private void IgnoreEnemy(Collider[] colliders, bool ignore)
        {
            if (body == null)
                body = GetComponent<CharacterController>();
            if (body == null)
                return;
            foreach (Collider c in colliders)
                if (c != null)
                    Physics.IgnoreCollision(body, c, ignore);
        }

        private float WhirlReach()
        {
            float melee = 1f + Mathf.Max(0f, Stat(StatType.MeleeRange) + WeaponBonuses.Get(WeaponSkillEffect.Range)) / 100f;
            return Mathf.Max(2.4f, (CharacterAttackAnimator.AttackRange(CurrentWeapon()) + 0.4f) * melee) *
                   SkillArea(SkillId.Whirlwind) * WhirlAreaScale;
        }

        private void WhirlHit(SkillDefinition skill, int level, float reach)
        {
            Vector3 center = transform.position;
            Record(skill, level, reach, at: center);
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level);
            foreach (EnemyHealth enemy in EnemiesWithin(center, reach))
                Hit(enemy, damage, CombatText.PhysicalColor, attack: true, melee: true, skillId: skill.Id);
        }

        // Towards the cursor, as a share of full speed that grows with its distance. On touch: the
        // joystick, else the aim stick, by how far each is pushed.
        private Vector3 WhirlSteer()
        {
            if (TouchMode.Active)
            {
                Vector2 stick = VirtualInput.Move.sqrMagnitude > 0.0001f ? VirtualInput.Move : VirtualInput.Aim;
                return PlayerController.CameraRelativeDirection(stick) * Mathf.Clamp01(stick.magnitude);
            }

            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null)
                return Vector3.zero;
            Ray ray = cam.ScreenPointToRay(ScreenPointer.Position);
            if (!new Plane(Vector3.up, transform.position).Raycast(ray, out float enter))
                return Vector3.zero;
            Vector3 offset = ray.GetPoint(enter) - transform.position;
            offset.y = 0f;
            if (body == null)
                body = GetComponent<CharacterController>();
            float fullSpeedAt = WhirlFullSpeedDiameters * 2f * (body != null ? body.radius : 0.4f);
            return offset / fullSpeedAt;
        }

    }
}

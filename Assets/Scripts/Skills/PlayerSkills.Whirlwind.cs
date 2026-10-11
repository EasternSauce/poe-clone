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
    /// Whirlwind, the channelled melee skill: while its button is held the player spins, hitting
    /// everything in reach several times a second, passes through enemies, and drifts towards the cursor (faster the further
    /// it is, sprinting speed at <see cref="WhirlFullSpeedDiameters"/> body widths away; on touch,
    /// the joystick or the aim stick). It costs mana every <see cref="ChannelTick"/> seconds, paid
    /// up front, and stops when the button is let go or the mana runs out.
    /// </summary>
    public partial class PlayerSkills
    {
        /// <summary>Seconds between a channelled skill's mana payments.</summary>
        public const float ChannelTick = 0.5f;

        private const float WhirlTurnsPerSecond = 2.5f;
        private const float WhirlHitInterval = 0.2f;    // at no increased attack speed
        private const float WhirlFullSpeedDiameters = 5f;

        private int whirlSlot = -1;
        private int whirlLevel;
        private float whirlNextMana;
        private float whirlNextHit;
        // Enemies whose colliders the player passes through while spinning, restored when it ends.
        private readonly Dictionary<EnemyHealth, Collider[]> whirlIgnored = new Dictionary<EnemyHealth, Collider[]>();
        private CharacterController body;

        /// <summary>Whirlwind is spinning.</summary>
        public bool IsWhirling => whirlSlot >= 0;

        // TryUse already paid the first tick.
        private void BeginWhirlwind(int slot, int level)
        {
            whirlSlot = slot;
            whirlLevel = level;
            whirlNextMana = Time.time + ChannelTick;
            whirlNextHit = Time.time;
            controller.CancelWalk();
            controller.SetChannelling(true);
            PlaySkillSound(SkillId.Whirlwind, transform.position);
        }

        private void EndWhirlwind()
        {
            if (whirlSlot < 0)
                return;
            whirlSlot = -1;
            foreach (Collider[] colliders in whirlIgnored.Values)
                IgnoreEnemy(colliders, false);
            whirlIgnored.Clear();
            if (controller != null)
                controller.SetChannelling(false);
            if (attackAnimator != null)
                attackAnimator.ReleaseHold();
        }

        private void UpdateWhirlwind(bool free)
        {
            if (!free || !SlotHeld(whirlSlot) || Slot(whirlSlot) != SkillId.Whirlwind || LevelAt(whirlSlot) <= 0 ||
                !CanUseWithWeapon(SkillId.Whirlwind, CurrentWeapon()))
            {
                EndWhirlwind();
                return;
            }

            if (Time.time >= whirlNextMana)
            {
                if (!stats.TrySpendMana(ManaCostAt(whirlSlot)))
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
            controller.SteerChannel(WhirlSteer());
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + 360f * WhirlTurnsPerSecond * Time.deltaTime, 0f);

            if (Time.time >= whirlNextHit)
            {
                WhirlHit(SkillBook.Get(SkillId.Whirlwind), whirlLevel);
                whirlNextHit = Time.time + WhirlHitInterval / AttackRateMultiplier;
            }
        }

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

        private float AttackRateMultiplier => (1f + Stat(StatType.AttackSpeed) / 100f) *
            (controller != null && controller.HasOnslaught ? PlayerController.OnslaughtMore : 1f);

        private void WhirlHit(SkillDefinition skill, int level)
        {
            WeaponType weapon = CurrentWeapon();
            float melee = 1f + Mathf.Max(0f, Stat(StatType.MeleeRange)) / 100f;
            float reach = Mathf.Max(2.4f, (CharacterAttackAnimator.AttackRange(weapon) + 0.4f) * melee) *
                          DefenceMath.RadiusMultiplier(Stat(StatType.AreaOfEffect));
            SkillEffects.Shockwave(transform.position, reach, skill.Color, 0.22f);
            Record(skill, level, reach);
            float damage = WeaponDamage() * skill.WeaponMultiplierAt(level);
            foreach (EnemyHealth enemy in EnemiesWithin(transform.position, reach))
                Hit(enemy, damage, CombatText.PhysicalColor, attack: true, melee: true);
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
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (!new Plane(Vector3.up, transform.position).Raycast(ray, out float enter))
                return Vector3.zero;
            Vector3 offset = ray.GetPoint(enter) - transform.position;
            offset.y = 0f;
            if (body == null)
                body = GetComponent<CharacterController>();
            float fullSpeedAt = WhirlFullSpeedDiameters * 2f * (body != null ? body.radius : 0.4f);
            return offset / fullSpeedAt;
        }

        // Whether the button for a bar slot is still held down.
        private static bool SlotHeld(int slot)
        {
            if (VirtualInput.SkillHeld == slot)
                return true;
            if (TouchMode.Active)
                return false;
            Keyboard keyboard = Keyboard.current;
            if (slot < SlotKeys.Length)
                return keyboard != null && keyboard[SlotKeys[slot]].isPressed;
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return false;
            switch (slot)
            {
                case 8: return mouse.rightButton.isPressed;
                case 9: return mouse.backButton.isPressed;
                case 10: return mouse.forwardButton.isPressed;
                default: return false;
            }
        }
    }
}

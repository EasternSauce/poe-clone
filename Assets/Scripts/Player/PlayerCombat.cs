using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Combat;
using PoeClone.Inventory;
using PoeClone.Visuals;

namespace PoeClone.Player
{
    /// <summary>
    /// Turns a mouse click into an attack: unarmed if nothing is in the main hand, otherwise
    /// whatever is equipped there. Damage and pace both read from the character's stat sheet, so
    /// gear and attributes matter, and the swing itself is handed to <see cref="CharacterAttackAnimator"/>
    /// so each weapon type plays its own animation.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerCombat : MonoBehaviour
    {
        [Tooltip("Percent damage increase granted per point of Strength.")]
        [SerializeField] private float damagePercentPerStrength = 1f;

        [SerializeField] private float attackRange = 1.6f;
        [SerializeField] private float attackRadius = 1.1f;
        [SerializeField] private float attackHeight = 1f;

        private PlayerStats stats;
        private PlayerInventory inventory;
        private CharacterAttackAnimator attackAnimator;

        private float cooldownTimer;
        private float pendingDamage;
        private readonly Collider[] hitBuffer = new Collider[16];

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<PlayerInventory>();
            attackAnimator = GetComponentInChildren<CharacterAttackAnimator>();
        }

        private void Start()
        {
            if (attackAnimator != null)
                attackAnimator.StrikeFrame += PerformHit;
        }

        private void OnDestroy()
        {
            if (attackAnimator != null)
                attackAnimator.StrikeFrame -= PerformHit;
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;

            Mouse mouse = Mouse.current;
            bool attackPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (attackPressed && cooldownTimer <= 0f && attackAnimator != null && !attackAnimator.IsAttacking)
                StartAttack();
        }

        private void StartAttack()
        {
            ItemData weapon = inventory.Equipment.Get(EquipSlot.MainHand);
            WeaponType weaponType = weapon != null ? weapon.WeaponType : WeaponType.Unarmed;

            pendingDamage = ComputeDamage();

            float attacksPerSecond = ComputeAttacksPerSecond(weaponType);
            cooldownTimer = attacksPerSecond > 0f ? 1f / attacksPerSecond : 1f;

            attackAnimator.PlayAttack(weaponType);
        }

        // The stat sheet's PhysicalDamage already covers the unarmed base (PlayerStatsLink sets it
        // even with nothing equipped) plus whatever the weapon and other gear add, so this reads
        // right whether or not a weapon is in hand.
        private float ComputeDamage()
        {
            float baseDamage = inventory.Stats.Total(StatType.PhysicalDamage);
            float strengthMultiplier = 1f + stats.Strength * damagePercentPerStrength / 100f;
            return baseDamage * strengthMultiplier;
        }

        private float ComputeAttacksPerSecond(WeaponType weaponType)
        {
            float baseAttacksPerSecond = CharacterAttackAnimator.BaseAttackSpeed(weaponType);
            float increasedPercent = inventory.Stats.Total(StatType.AttackSpeed);
            return baseAttacksPerSecond * (1f + increasedPercent / 100f);
        }

        private void PerformHit()
        {
            Vector3 origin = transform.position + Vector3.up * attackHeight + transform.forward * attackRange;

            int count = Physics.OverlapSphereNonAlloc(origin, attackRadius, hitBuffer);
            var hitAlready = new HashSet<IDamageable>();

            for (int i = 0; i < count; i++)
            {
                IDamageable target = hitBuffer[i].GetComponentInParent<IDamageable>();
                if (target == null || target == (object)stats)
                    continue;

                if (hitAlready.Add(target))
                    target.TakeDamage(pendingDamage);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position + Vector3.up * attackHeight + transform.forward * attackRange;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(origin, attackRadius);
        }
    }
}

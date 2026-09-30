using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Audio;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Visuals;

namespace PoeClone.Player
{
    /// <summary>
    /// Turns a mouse click into an attack: unarmed if nothing is in the main hand, otherwise
    /// whatever is equipped there. Damage and pace both read from the character's stat sheet, so
    /// gear and attributes matter, and the swing itself is handed to <see cref="CharacterAttackAnimator"/>
    /// so each weapon type plays its own animation.
    ///
    /// Also drives the melee aim-assist: every frame it highlights whichever nearby enemy is
    /// closest to the cursor (in range, white outline via <see cref="Outline"/>), purely as aiming
    /// feedback. The swing itself always damages every valid target inside a forward cone,
    /// regardless of which one is highlighted.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerCombat : MonoBehaviour
    {
        [Tooltip("Percent damage increase granted per point of Strength.")]
        [SerializeField] private float damagePercentPerStrength = 1f;

        [Tooltip("Half-angle, in degrees, of the forward cone a swing needs to reach a target in.")]
        [SerializeField] private float coneHalfAngle = 35f;

        private PlayerStats stats;
        private PlayerInventory inventory;
        private CharacterAttackAnimator attackAnimator;

        private float cooldownTimer;
        private float pendingDamage;
        private readonly Collider[] hitBuffer = new Collider[16];

        private EnemyHealth highlighted;

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

            stats.Died += OnDied;
            stats.Revived += OnRevived;
        }

        private void OnDestroy()
        {
            if (attackAnimator != null)
                attackAnimator.StrikeFrame -= PerformHit;

            if (stats != null)
            {
                stats.Died -= OnDied;
                stats.Revived -= OnRevived;
            }
        }

        // No more input handling, highlighting, or hits once dead: PlayerStats has already
        // stopped movement and played the death collapse, so combat just gets out of the way too.
        private void OnDied()
        {
            SetHighlight(null);
            enabled = false;
        }

        private void OnRevived()
        {
            cooldownTimer = 0f;
            enabled = true;
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;

            UpdateAimHighlight();

            Mouse mouse = Mouse.current;
            bool attackPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (attackPressed && cooldownTimer <= 0f && attackAnimator != null && !attackAnimator.IsAttacking)
                StartAttack();
        }

        private WeaponType CurrentWeaponType()
        {
            ItemData weapon = inventory.Equipment.Get(EquipSlot.MainHand);
            return weapon != null ? weapon.WeaponType : WeaponType.Unarmed;
        }

        private void StartAttack()
        {
            WeaponType weaponType = CurrentWeaponType();

            FaceAimPoint();

            pendingDamage = ComputeDamage();

            float attacksPerSecond = ComputeAttacksPerSecond(weaponType);
            cooldownTimer = attacksPerSecond > 0f ? 1f / attacksPerSecond : 1f;

            attackAnimator.PlayAttack(weaponType);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayRandomAtPoint(AudioManager.Instance.playerSwing, transform.position);
        }

        // Instantly snaps the player to face wherever the mouse is pointing, on the ground plane
        // through the player's feet, before the swing plays. Without this the swing would play
        // facing whatever way WASD movement last pointed, not the clicked target.
        private void FaceAimPoint()
        {
            if (TryGetAimPoint(out Vector3 point))
            {
                Vector3 direction = point - transform.position;
                direction.y = 0f;

                if (direction.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private bool TryGetAimPoint(out Vector3 point)
        {
            point = Vector3.zero;

            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null)
                return false;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Plane ground = new Plane(Vector3.up, transform.position);

            if (!ground.Raycast(ray, out float enter))
                return false;

            point = ray.GetPoint(enter);
            return true;
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

        // Damages everything the weapon actually reaches: a forward cone out to the weapon's
        // range. Intentionally independent of the aim highlight, which only ever picks one target.
        private void PerformHit()
        {
            float range = CharacterAttackAnimator.AttackRange(CurrentWeaponType());
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, hitBuffer);
            var hitAlready = new HashSet<IDamageable>();

            for (int i = 0; i < count; i++)
            {
                IDamageable target = hitBuffer[i].GetComponentInParent<IDamageable>();
                if (target == null || target == (object)stats)
                    continue;

                if (!IsInCone(hitBuffer[i].transform.position, range, transform.forward))
                    continue;

                if (hitAlready.Add(target))
                    target.TakeDamage(pendingDamage);
            }
        }

        private bool IsInCone(Vector3 worldPosition, float range, Vector3 forward)
        {
            Vector3 toTarget = worldPosition - transform.position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            if (distance > range)
                return false;

            if (distance <= 0.001f)
                return true;

            return Vector3.Angle(forward, toTarget) <= coneHalfAngle;
        }

        // Pure aiming feedback: highlights whichever in-range enemy is closest to the cursor on
        // screen. Must only ever highlight a target that PerformHit would actually hit right now,
        // so it re-runs the exact same distance+cone check PerformHit uses -- against the aim
        // direction FaceAimPoint would snap to on attack, not the player's current facing, since
        // that's what the cone will actually be measured from by the time the swing lands.
        // Without this, OverlapSphere's collider-inclusive test could highlight an enemy whose
        // actual center is farther than the weapon's range, or outside the swing's forward cone,
        // so the swing would visibly miss despite the highlight.
        private void UpdateAimHighlight()
        {
            Camera cam = Camera.main;
            Mouse mouse = Mouse.current;
            if (cam == null || mouse == null || !TryGetAimPoint(out Vector3 aimPoint))
            {
                SetHighlight(null);
                return;
            }

            Vector3 aimDirection = aimPoint - transform.position;
            aimDirection.y = 0f;
            aimDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : transform.forward;

            float range = CharacterAttackAnimator.AttackRange(CurrentWeaponType());
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, hitBuffer);

            Vector2 cursor = mouse.position.ReadValue();
            var seen = new HashSet<EnemyHealth>();
            EnemyHealth best = null;
            float bestDistanceSq = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = hitBuffer[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || !seen.Add(enemy))
                    continue;

                if (!IsInCone(enemy.transform.position, range, aimDirection))
                    continue;

                Vector3 screen = cam.WorldToScreenPoint(enemy.transform.position + Vector3.up);
                if (screen.z <= 0f)
                    continue;

                float distanceSq = ((Vector2)screen - cursor).sqrMagnitude;
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    best = enemy;
                }
            }

            SetHighlight(best);
        }

        private void SetHighlight(EnemyHealth enemy)
        {
            if (enemy == highlighted)
                return;

            if (highlighted != null)
                GetOutline(highlighted).SetHighlighted(false);

            if (enemy != null)
                GetOutline(enemy).SetHighlighted(true);

            highlighted = enemy;
        }

        private static Outline GetOutline(EnemyHealth enemy)
        {
            Outline outline = enemy.GetComponent<Outline>();
            if (outline == null)
                outline = enemy.gameObject.AddComponent<Outline>();
            return outline;
        }

        private void OnDrawGizmosSelected()
        {
            float range = Application.isPlaying ? CharacterAttackAnimator.AttackRange(CurrentWeaponType()) : 1.6f;

            Gizmos.color = Color.red;
            Vector3 left = Quaternion.AngleAxis(-coneHalfAngle, Vector3.up) * transform.forward;
            Vector3 right = Quaternion.AngleAxis(coneHalfAngle, Vector3.up) * transform.forward;
            Gizmos.DrawLine(transform.position, transform.position + left * range);
            Gizmos.DrawLine(transform.position, transform.position + right * range);
            Gizmos.DrawWireSphere(transform.position, range);
        }
    }
}

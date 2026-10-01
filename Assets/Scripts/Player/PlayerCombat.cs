using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PoeClone.Audio;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.UI;
using PoeClone.Visuals;
using PoeClone.World;

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
    ///
    /// Touch has no cursor, so there the attack button aims itself: at the nearest enemy in reach,
    /// or straight ahead when nothing is.
    ///
    /// A bow shoots instead (<see cref="PlayerArrow"/>, no ammo); its "reach" is how far arrows fly,
    /// so the same aiming and highlighting work for it unchanged.
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
        private PlayerController controller;

        private float cooldownTimer;
        private float pendingDamage;
        private readonly Collider[] hitBuffer = new Collider[16];

        private EnemyHealth highlighted;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<PlayerInventory>();
            controller = GetComponent<PlayerController>();
            attackAnimator = GetComponentInChildren<CharacterAttackAnimator>();
        }

        private void Start()
        {
            if (attackAnimator != null)
            {
                attackAnimator.StrikeFrame += PerformHit;
                attackAnimator.AttackCancelled += OnAttackCancelled;
            }

            stats.Died += OnDied;
            stats.Revived += OnRevived;
        }

        private void OnDestroy()
        {
            if (attackAnimator != null)
            {
                attackAnimator.StrikeFrame -= PerformHit;
                attackAnimator.AttackCancelled -= OnAttackCancelled;
            }

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

        // A staggered swing never landed, so it shouldn't cost a full attack interval. Without the
        // refund the victim is still on cooldown when the stagger ends, the attacker's next hit lands
        // first, and whoever opens a 1v1 keeps the other locked in stagger.
        private void OnAttackCancelled()
        {
            cooldownTimer = 0f;
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

            bool attackPressed;
            if (TouchMode.Active)
            {
                // Mouse input is ignored on touch: browsers turn taps into mouse clicks too, so
                // every tap on a button would also swing.
                attackPressed = VirtualInput.AttackHeld;
            }
            else
            {
                Mouse mouse = Mouse.current;
                // A focused UI text field (e.g. the chat box) should consume the click, not the attack,
                // and so does an item on the ground (LootPicker picks it up instead).
                attackPressed = mouse != null && mouse.leftButton.wasPressedThisFrame && !PlayerController.IsUiFocused() &&
                                LootPicker.PickableAt(mouse.position.ReadValue()) == null &&
                                !HoldingInventoryItem();
            }

            if (attackPressed && cooldownTimer <= 0f && attackAnimator != null && !attackAnimator.IsAttacking)
                StartAttack();
        }

        private InventoryUI inventoryUI;

        // While an item is on the inventory cursor, a click in the world throws it away instead.
        private bool HoldingInventoryItem()
        {
            if (inventoryUI == null)
                inventoryUI = FindAnyObjectByType<InventoryUI>();
            return inventoryUI != null && inventoryUI.IsOpen && inventoryUI.IsHoldingItem;
        }

        private WeaponType CurrentWeaponType()
        {
            ItemData weapon = inventory.Equipment.Get(EquipSlot.MainHand);
            return weapon != null ? weapon.WeaponType : WeaponType.Unarmed;
        }

        private void StartAttack()
        {
            WeaponType weaponType = CurrentWeaponType();

            // Attacking means the player has changed their mind about walking to an item.
            if (controller != null)
                controller.CancelWalk();

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

            if (TouchMode.Active)
                return TryGetAutoAimPoint(out point);

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

        // Touch aim: the nearest living enemy the swing can reach. Uses the same centre-distance
        // test as IsInCone, so the target picked is always one PerformHit will hit once faced.
        // No target means no aim point, and the swing goes wherever the player already faces.
        private bool TryGetAutoAimPoint(out Vector3 point)
        {
            point = Vector3.zero;

            float range = CharacterAttackAnimator.AttackRange(CurrentWeaponType());
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, hitBuffer);
            float bestDistanceSq = range * range;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                EnemyHealth enemy = hitBuffer[i].GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead)
                    continue;

                Vector3 toEnemy = enemy.transform.position - transform.position;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude <= bestDistanceSq)
                {
                    bestDistanceSq = toEnemy.sqrMagnitude;
                    point = enemy.transform.position;
                    found = true;
                }
            }

            return found;
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
        // A bow looses an arrow instead.
        private void PerformHit()
        {
            // Disabled while dead, and on a spectator's puppet player: the strike event still fires
            // there (the swing is replayed), but it must not damage anything.
            if (!enabled)
                return;

            WeaponType weaponType = CurrentWeaponType();
            float range = CharacterAttackAnimator.AttackRange(weaponType);

            if (CharacterAttackAnimator.IsRanged(weaponType))
            {
                PlayerArrow.Launch(transform, range, pendingDamage);
                return;
            }

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
                {
                    target.TakeDamage(pendingDamage);

                    Transform hit = ((Component)target).transform;
                    CombatText.Show(hit.position + Vector3.up * 1.6f * hit.localScale.y,
                        Mathf.Max(1, Mathf.RoundToInt(pendingDamage)).ToString(), CombatText.PhysicalColor);
                }
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
            bool touch = TouchMode.Active;
            if (cam == null || (!touch && mouse == null) || !TryGetAimPoint(out Vector3 aimPoint))
            {
                SetHighlight(null);
                return;
            }

            Vector3 aimDirection = aimPoint - transform.position;
            aimDirection.y = 0f;
            aimDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : transform.forward;

            float range = CharacterAttackAnimator.AttackRange(CurrentWeaponType());
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, hitBuffer);

            // On touch the "cursor" is the auto-aim target itself, so that is what gets outlined.
            Vector2 cursor = touch
                ? (Vector2)cam.WorldToScreenPoint(aimPoint + Vector3.up)
                : mouse.position.ReadValue();
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

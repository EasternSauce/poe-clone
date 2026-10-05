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
    /// Touch has no cursor, so instead of a button, attacking uses a second stick
    /// (<see cref="VirtualInput.Aim"/>, drawn by TouchControlsUI next to the movement stick):
    /// holding it off-centre swings/shoots repeatedly towards wherever it points, exactly like
    /// holding the mouse button down while pointing it, so aiming is always explicit - there is no
    /// automatic targeting.
    ///
    /// A bow shoots instead (<see cref="PlayerArrow"/>, no ammo); its "reach" is how far arrows fly,
    /// so the same aiming and highlighting work for it unchanged. A staff casts its spell instead
    /// (<see cref="PoeClone.Skills.PlayerSkills.MainSkill"/>), or is swung like a mace when there's no mana for it.
    ///
    /// Where a shot or swing goes is decided when it leaves - the strike frame - not when the
    /// wind-up starts: the player turns to the cursor (or the aim stick) at that moment.
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
        private PoeClone.Skills.PlayerSkills skills;

        private float cooldownTimer;
        private float pendingDamage;
        // True between this script starting a swing and its strike frame. Skills replay the same
        // arm animations (Fire Bolt, Chain Lightning, Cleave), and their strike frames must not
        // also land a weapon hit or loose an arrow.
        private bool swingPending;
        // The pending swing is the staff's spell rather than a weapon blow.
        private bool castPending;
        // The bow skill the pending shot is (the one on when the draw started), if any.
        private PoeClone.Skills.SkillId? bowSkillPending;
        private int bowLevelPending;

        // Degrees between the arrows of a volley (extra arrows fan out round the aim).
        public const float ArrowSpreadDegrees = 9f;

        // What the swing was aimed at when it started: the outlined enemy if there was one, else
        // the ground point. A bow re-aims at it when the arrow leaves, since the player may have
        // moved sideways while drawing (and a parallel shot from the new spot would miss).
        private EnemyHealth aimEnemy;
        private Vector3 aimPoint;
        private bool hasAimPoint;
        private readonly Collider[] hitBuffer = new Collider[256]; // roomy: scenery shares it with the enemies

        private EnemyHealth highlighted;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<PlayerInventory>();
            controller = GetComponent<PlayerController>();
            attackAnimator = GetComponentInChildren<CharacterAttackAnimator>();
        }

        private PoeClone.Skills.PlayerSkills SkillSet
        {
            get
            {
                if (skills == null)
                    skills = GetComponent<PoeClone.Skills.PlayerSkills>();
                return skills;
            }
        }

        // The staff's spell, when there is one: the attack casts it.
        private bool HasMainSkill => SkillSet != null && SkillSet.MainSkill != null;

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
            swingPending = false;
            castPending = false;
            bowSkillPending = null;
            bowLevelPending = 0;
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
                // Keep turning to face the stick while it's held, even mid-cooldown, so the swing
                // that finally lands is aimed at wherever it's currently pointing, not wherever it
                // happened to point when the cooldown last ended.
                if (attackPressed)
                    FaceAimPoint();
            }
            else
            {
                Mouse mouse = Mouse.current;
                // A focused UI text field (e.g. the chat box) should consume the click, not the attack,
                // and so does an item on the ground (LootPicker picks it up instead).
                attackPressed = mouse != null && mouse.leftButton.wasPressedThisFrame && !PlayerController.IsUiFocused() &&
                                !PlayerController.IsPointerOverUi() &&
                                !PlayerController.ClickConsumed &&
                                LootPicker.PickableAt(mouse.position.ReadValue()) == null &&
                                NpcInteractor.TalkableAt(mouse.position.ReadValue()) == null &&
                                AreaGate.AtScreen(mouse.position.ReadValue()) == null &&
                                !UI.DialogueUI.IsOpen &&
                                !HoldingInventoryItem();
            }

            if (attackPressed && cooldownTimer <= 0f && (controller == null || !controller.IsSkillCommitted) &&
                attackAnimator != null && !attackAnimator.IsAttacking)
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

        // How far an attack reaches: the weapon's range, longer for melee with increased Melee Range;
        // for a staff with its spell, how far the spell flies.
        private float Reach(WeaponType weaponType)
        {
            if (HasMainSkill)
                return SkillSet.MainReach();
            float range = CharacterAttackAnimator.AttackRange(weaponType);
            if (CharacterAttackAnimator.IsRanged(weaponType))
                return range;
            return range * (1f + Mathf.Max(0f, inventory.Stats.Total(StatType.MeleeRange)) / 100f);
        }

        private void StartAttack()
        {
            WeaponType weaponType = CurrentWeaponType();

            // Attacking means the player has changed their mind about walking to an item.
            if (controller != null)
                controller.CancelWalk();

            FaceAimPoint();
            aimEnemy = highlighted;
            hasAimPoint = TryGetAimPoint(out aimPoint);

            pendingDamage = ComputeDamage();
            if (weaponType == WeaponType.Bow)
                pendingDamage *= 1f + inventory.Stats.Total(StatType.BowDamage) / 100f;

            // A staff casts its spell; without the mana for it, it's swung instead.
            castPending = HasMainSkill && SkillSet.TrySpendMain();
            if (castPending)
            {
                cooldownTimer = SkillSet.MainInterval();
                swingPending = true;
                attackAnimator.PlayAttack(WeaponType.Unarmed);
                return;
            }
            if (HasMainSkill)
                CombatText.Show(transform.position + Vector3.up * 2f, "Not enough mana", CombatText.ColdColor, 0.6f);

            // A bow skill that's on replaces the plain shot: its own draw, and its own pace.
            bowSkillPending = CharacterAttackAnimator.IsRanged(weaponType) && SkillSet != null ? SkillSet.ActiveBowSkill : null;
            bowLevelPending = bowSkillPending != null ? SkillSet.ActiveBowLevel : 0;

            float attacksPerSecond = ComputeAttacksPerSecond(weaponType);
            if (bowSkillPending != null)
                attacksPerSecond *= PoeClone.Skills.PlayerSkills.BowSpeed(bowSkillPending.Value);
            cooldownTimer = attacksPerSecond > 0f ? 1f / attacksPerSecond : 1f;

            swingPending = true;
            if (bowSkillPending != null)
                attackAnimator.PlayBow(PoeClone.Skills.PlayerSkills.BowStyleOf(bowSkillPending.Value));
            else
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
            {
                Vector3 direction = PlayerController.CameraRelativeDirection(VirtualInput.Aim);
                if (direction.sqrMagnitude < 0.0001f)
                    return false;

                point = transform.position + direction * 10f;
                return true;
            }

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
            float onslaught = controller != null && controller.HasOnslaught ? PlayerController.OnslaughtMore : 1f;
            return baseAttacksPerSecond * (1f + increasedPercent / 100f) * onslaught;
        }

        // Damages everything the weapon actually reaches: a forward cone out to the weapon's
        // range. Intentionally independent of the aim highlight, which only ever picks one target.
        // A bow looses an arrow instead.
        private void PerformHit()
        {
            // Disabled while dead, and on a spectator's puppet player: the strike event still fires
            // there (the swing is replayed), but it must not damage anything.
            if (!enabled || !swingPending)
                return;
            swingPending = false;

            // Aimed now, as it leaves: wherever the cursor (or aim stick) points at this moment.
            ReaimAtRelease();

            if (castPending)
            {
                castPending = false;
                SkillSet.ReleaseMain();
                return;
            }

            WeaponType weaponType = CurrentWeaponType();
            float range = Reach(weaponType);

            if (CharacterAttackAnimator.IsRanged(weaponType))
            {
                int arrows = ArrowCount();
                if (bowSkillPending != null && SkillSet != null)
                {
                    SkillSet.ReleaseBow(bowSkillPending.Value, bowLevelPending, pendingDamage, range, arrows,
                        BowTarget(range * PlayerArrow.BowRangeMultiplier));
                    bowSkillPending = null;
                    bowLevelPending = 0;
                    return;
                }
                var volley = PlayerArrow.NewVolley();
                foreach (Vector3 direction in HitEffects.Spread(transform.forward, arrows, ArrowSpreadDegrees))
                    PlayerArrow.Launch(transform, range, pendingDamage, direction, volley);
                return;
            }

            // Great weapons land with weight: a burst of dust where the head comes down, and a jolt.
            if (SlotRules.IsTwoHandedMelee(weaponType))
            {
                bool slam = weaponType != WeaponType.Greatsword;
                Vector3 impact = transform.position + transform.forward * range * (slam ? 0.7f : 0.5f);
                PoeClone.Skills.SkillEffects.Shockwave(impact, slam ? 1.5f : range * 0.9f, new Color(0.72f, 0.64f, 0.5f, 1f), 0.3f);
                CameraSystem.CameraFollow.Shake(slam ? 0.12f : 0.05f, 0.18f);
            }

            int count = Physics.OverlapSphereNonAlloc(transform.position, range, hitBuffer);
            var hitAlready = new HashSet<IDamageable>();

            // A sceptre's blow puts Death Mark on what it strikes (the aimed enemy if it's among them).
            bool marks = weaponType == WeaponType.Sceptre;
            EnemyHealth toMark = null;

            for (int i = 0; i < count; i++)
            {
                IDamageable target = hitBuffer[i].GetComponentInParent<IDamageable>();
                if (target == null || target == (object)stats)
                    continue;

                if (!IsInCone(hitBuffer[i].transform.position, range, transform.forward))
                    continue;

                if (hitAlready.Add(target))
                {
                    if (target is EnemyHealth enemy)
                    {
                        HitEffects.Deal(transform, enemy, pendingDamage, attack: true, CombatText.PhysicalColor, melee: true);
                        if (marks && !enemy.IsDead && (toMark == null || enemy == aimEnemy))
                            toMark = enemy;
                        continue;
                    }

                    target.TakeDamage(pendingDamage);

                    Transform hit = ((Component)target).transform;
                    if (target is SerpentPursuit serpent)
                        CombatText.Show(serpent.MouthPosition,
                            Mathf.Max(1, Mathf.RoundToInt(serpent.LastSharedDamage)).ToString(), CombatText.PhysicalColor);
                    else
                        CombatText.Show(hit.position + Vector3.up * 1.6f * hit.localScale.y,
                            Mathf.Max(1, Mathf.RoundToInt(pendingDamage)).ToString(), CombatText.PhysicalColor);
                }
            }

            if (toMark != null)
                PoeClone.Skills.Minion.Mark(toMark, transform);
        }

        // Arrows in a bow shot: one, plus Additional Arrows, plus maybe one more from the
        // extra-arrow chance (bows and quivers).
        private int ArrowCount()
        {
            int arrows = 1 + Mathf.Max(0, Mathf.RoundToInt(inventory.Stats.Total(StatType.AdditionalArrows)));
            float chance = inventory.Stats.Total(StatType.ExtraArrowChance);
            if (chance > 0f && Random.value * 100f < chance)
                arrows++;
            return arrows;
        }

        // Where a shot aimed at a spot (Rain of Arrows) comes down: the cursor on desktop; on touch
        // the aimed enemy, else a little ahead. Never further than the bow reaches.
        private Vector3 BowTarget(float range)
        {
            Vector3 target;
            if (!TouchMode.Active && TryGetAimPoint(out Vector3 cursor))
                target = cursor;
            else if (highlighted != null && !highlighted.IsDead)
                target = highlighted.transform.position;
            else if (aimEnemy != null && !aimEnemy.IsDead)
                target = aimEnemy.transform.position;
            else
                target = transform.position + transform.forward * range * 0.6f;

            Vector3 offset = target - transform.position;
            offset.y = 0f;
            if (offset.magnitude > range)
                offset = offset.normalized * range;
            return transform.position + offset;
        }

        // Turns to wherever the player is aiming right now. On touch, with the aim stick already let
        // go, it falls back to what was aimed at when the swing started (the enemy, else the point).
        private void ReaimAtRelease()
        {
            Vector3 target;
            if (TryGetAimPoint(out Vector3 now))
                target = now;
            else if (aimEnemy != null && !aimEnemy.IsDead)
                target = aimEnemy.transform.position;
            else if (hasAimPoint)
                target = aimPoint;
            else
                return;

            Vector3 direction = target - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.25f)
                transform.rotation = Quaternion.LookRotation(direction);
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

            float cone = CharacterAttackAnimator.ConeHalfAngle(CurrentWeaponType());
            return Vector3.Angle(forward, toTarget) <= (cone > 0f ? cone : coneHalfAngle);
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

            float range = Reach(CurrentWeaponType());
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, hitBuffer);

            // On touch the "cursor" is the point the aim stick is pointing at, so that is what
            // gets outlined.
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

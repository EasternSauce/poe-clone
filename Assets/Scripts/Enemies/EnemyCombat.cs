using UnityEngine;
using PoeClone.Combat;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Enemy-side mirror of <see cref="PoeClone.Player.PlayerCombat"/>: attacks the player once
    /// they're in range and off cooldown. Melee kinds hit at the strike frame if still in reach;
    /// ranged kinds cast a bolt (<see cref="EnemyProjectile"/>) there instead. Numbers come from the
    /// enemy's <see cref="EnemyKind"/>. Self-provisions the <see cref="CharacterAttackAnimator"/> on
    /// the model (the enemy prefab doesn't have one yet) so no prefab edit is needed. Added to every
    /// enemy by <see cref="EnemyController"/>.
    /// </summary>
    public class EnemyCombat : MonoBehaviour
    {
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private float damage = 6f;
        [SerializeField] private float attackCooldown = 1.4f;

        // Melee reach is checked again when the blow lands; a little slack so a player who is
        // backing off at the very edge still gets hit by a swing that visibly connected.
        private const float ReachSlack = 1.15f;

        private EnemyKind kind = EnemyKinds.Get(0);
        private CharacterAttackAnimator attackAnimator;
        private EnemyController controller;
        private PlayerStats playerStats;
        private Stagger stagger;

        private float cooldownTimer;
        private BossAbilities boss;

        // True from this script starting a swing to its strike: a boss's own moves play swings on
        // the same animator, and those strikes are theirs to resolve, not a plain blow's.
        private bool swingPending;

        private void Awake()
        {
            Transform model = transform.Find("Model");
            Transform host = model != null ? model : transform;

            attackAnimator = host.GetComponent<CharacterAttackAnimator>();
            if (attackAnimator == null)
                attackAnimator = host.gameObject.AddComponent<CharacterAttackAnimator>();

            stagger = GetComponent<Stagger>();
            if (stagger == null)
                stagger = gameObject.AddComponent<Stagger>();
        }

        /// <summary>Takes on a kind's attack (see <see cref="EnemyKinds.Apply"/>).</summary>
        public void Configure(EnemyKind enemyKind, int level = 1)
        {
            kind = enemyKind;
            attackRange = enemyKind.AttackRange;
            damage = enemyKind.Damage * EnemyKinds.DamageScale(level, enemyKind);
            attackCooldown = enemyKind.AttackCooldown;
        }

        /// <summary>
        /// Where a bolt or arrow starts: a caster's staff orb, or else in front of the chest,
        /// scaled with the enemy. The orb is placed where <see cref="UprightStaff"/> holds it, worked
        /// out here rather than read back from the bones, since this runs mid-swing in the attack
        /// animator's Update, before the staff is straightened for the frame.
        /// </summary>
        public static Vector3 BoltOrigin(Transform enemy)
        {
            foreach (Transform t in enemy.GetComponentsInChildren<Transform>())
            {
                // A creature spits from its mouth.
                if (t.name == CreatureAnimator.MouthName && t.GetComponentInParent<CreatureAnimator>() != null)
                    return t.position;
                if (t.name != EnemyKinds.StaffOrbName)
                    continue;
                Transform staff = t.parent;
                float reach = Vector3.Distance(t.position, staff.position);
                return staff.position + UprightStaff.Up(enemy) * reach;
            }

            float scale = enemy.localScale.y;
            return enemy.position + Vector3.up * 0.5f * scale + enemy.forward * 0.6f * scale;
        }

        private void Start()
        {
            playerStats = FindAnyObjectByType<PlayerStats>();
            controller = GetComponent<EnemyController>();
            boss = GetComponent<BossAbilities>();
            attackAnimator.StrikeFrame += OnStrikeFrame;
            attackAnimator.AttackCancelled += OnAttackCancelled;
        }

        private void OnDestroy()
        {
            if (attackAnimator != null)
            {
                attackAnimator.StrikeFrame -= OnStrikeFrame;
                attackAnimator.AttackCancelled -= OnAttackCancelled;
            }
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
                cooldownTimer -= Time.deltaTime;

            if (playerStats == null)
            {
                playerStats = FindAnyObjectByType<PlayerStats>();
                return;
            }

            if (playerStats.IsDead)
                return;
            PlayerMotion.Track(playerStats);

            if (cooldownTimer > 0f || attackAnimator.IsAttacking || stagger.IsStaggered)
                return;

            if (boss == null)
                boss = GetComponent<BossAbilities>();
            if (boss != null && boss.Busy)
                return;

            if (DistanceToPlayer() > attackRange)
                return;

            FacePlayer();
            attackAnimator.PlaybackSpeed = (controller != null ? controller.AttackSpeedMultiplier : 1f) * kind.Tempo;
            // Archers draw their bow like the player does; armed brutes swing their weapon;
            // everyone else swipes.
            swingPending = true;
            if (kind.Bow)
                attackAnimator.PlayAttack(WeaponType.Bow);
            else if (kind.Weapon != WeaponType.Unarmed && !kind.IsCreature)
                attackAnimator.PlayAttack(kind.Weapon);
            else if (kind.IsCreature)
                attackAnimator.PlayCreatureAttack(kind.IsRanged);
            else
                attackAnimator.PlayClawAttack();
            if (kind.IsCreature)
                EnemySounds.Play(kind, EnemySounds.Event.Attack, transform.position);
            cooldownTimer = attackCooldown / ((controller != null ? controller.AttackSpeedMultiplier : 1f) * kind.Tempo);
        }

        // Same rule as the player: a swing cut short by a stagger refunds its cooldown.
        private void OnAttackCancelled()
        {
            if (!swingPending)
                return;
            swingPending = false;
            cooldownTimer = 0f;
        }

        private void OnStrikeFrame()
        {
            if (!swingPending)
                return;
            swingPending = false;
            if (playerStats == null || playerStats.IsDead)
                return;

            if (kind.IsRanged)
            {
                // Enraged, it leads its shot: aims where the player will be when the bolt gets there.
                Vector3 from = BoltOrigin(transform);
                if (controller != null && controller.IsEnraged)
                    EnemyProjectile.LaunchAt(from, PlayerMotion.Intercept(playerStats, from, kind.ProjectileSpeed), playerStats, kind, RollDamage());
                else
                    EnemyProjectile.Launch(from, playerStats, kind, RollDamage());
                return;
            }

            // A great weapon comes down with weight: dust where it lands, and a jolt.
            if (SlotRules.IsTwoHandedMelee(kind.Weapon))
            {
                Vector3 impact = transform.position + transform.forward * attackRange * 0.75f;
                Skills.SkillEffects.Shockwave(impact, 1.2f * transform.localScale.x, new Color(0.72f, 0.64f, 0.5f, 1f), 0.35f);
                CameraSystem.CameraFollow.Shake(0.1f, 0.2f);
            }

            if (DistanceToPlayer() <= attackRange * ReachSlack)
                playerStats.TakeHit(RollDamage(), kind.DamageType);
        }

        // Lightning is famously swingy in PoE; everything else varies a little.
        private float RollDamage()
        {
            float spread = kind.DamageType == DamageType.Lightning ? 0.6f : 0.15f;
            float rage = controller != null ? controller.DamageMultiplier : 1f;
            return damage * rage * Random.Range(1f - spread, 1f + spread);
        }

        private float DistanceToPlayer()
        {
            Vector3 toPlayer = playerStats.transform.position - transform.position;
            toPlayer.y = 0f;
            return toPlayer.magnitude;
        }

        private void FacePlayer()
        {
            Vector3 toPlayer = playerStats.transform.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toPlayer);
        }
    }
}

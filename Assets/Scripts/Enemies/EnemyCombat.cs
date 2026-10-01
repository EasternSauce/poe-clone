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
        private PlayerStats playerStats;
        private Stagger stagger;

        private float cooldownTimer;

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
        public void Configure(EnemyKind enemyKind)
        {
            kind = enemyKind;
            attackRange = enemyKind.AttackRange;
            damage = enemyKind.Damage;
            attackCooldown = enemyKind.AttackCooldown;
        }

        /// <summary>Where a caster's bolt starts: in front of the chest, scaled with the enemy.</summary>
        public static Vector3 BoltOrigin(Transform enemy)
        {
            float scale = enemy.localScale.y;
            return enemy.position + Vector3.up * 0.5f * scale + enemy.forward * 0.6f * scale;
        }

        private void Start()
        {
            playerStats = FindAnyObjectByType<PlayerStats>();
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

            if (cooldownTimer > 0f || attackAnimator.IsAttacking || stagger.IsStaggered)
                return;

            if (DistanceToPlayer() > attackRange)
                return;

            FacePlayer();
            // Archers draw their bow like the player does; everyone else swipes.
            if (kind.Bow)
                attackAnimator.PlayAttack(WeaponType.Bow);
            else
                attackAnimator.PlayClawAttack();
            cooldownTimer = attackCooldown;
        }

        // Same rule as the player: a swing cut short by a stagger refunds its cooldown.
        private void OnAttackCancelled()
        {
            cooldownTimer = 0f;
        }

        private void OnStrikeFrame()
        {
            if (playerStats == null || playerStats.IsDead)
                return;

            if (kind.IsRanged)
            {
                EnemyProjectile.Launch(BoltOrigin(transform), playerStats, kind, RollDamage());
                return;
            }

            if (DistanceToPlayer() <= attackRange * ReachSlack)
                playerStats.TakeHit(RollDamage(), kind.DamageType);
        }

        // Lightning is famously swingy in PoE; everything else varies a little.
        private float RollDamage()
        {
            float spread = kind.DamageType == DamageType.Lightning ? 0.6f : 0.15f;
            return damage * Random.Range(1f - spread, 1f + spread);
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

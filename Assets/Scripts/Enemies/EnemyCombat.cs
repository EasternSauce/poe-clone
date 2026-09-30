using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Enemy-side mirror of <see cref="PoeClone.Player.PlayerCombat"/>: swings a claw at the
    /// player once they're in range and off cooldown. Self-provisions the
    /// <see cref="CharacterAttackAnimator"/> on the model (the enemy prefab doesn't have one yet)
    /// so no prefab edit is needed. Added to every enemy by <see cref="EnemyController"/>.
    /// </summary>
    public class EnemyCombat : MonoBehaviour
    {
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private float damage = 6f;
        [SerializeField] private float attackCooldown = 1.4f;

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

        private void Start()
        {
            playerStats = FindAnyObjectByType<PlayerStats>();
            attackAnimator.StrikeFrame += OnStrikeFrame;
        }

        private void OnDestroy()
        {
            if (attackAnimator != null)
                attackAnimator.StrikeFrame -= OnStrikeFrame;
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
            attackAnimator.PlayClawAttack();
            cooldownTimer = attackCooldown;
        }

        private void OnStrikeFrame()
        {
            if (playerStats != null && DistanceToPlayer() <= attackRange)
                playerStats.TakeDamage(damage);
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

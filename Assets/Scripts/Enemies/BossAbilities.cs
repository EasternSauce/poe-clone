using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// What a boss does besides its plain attacks, on timers while the player is close:
    /// a ground slam round itself (the ground glows first, so it can be dodged), and its own
    /// trick - the Gravelord raises zombies round him, the Warlord rains fire where the player
    /// stands. Added by <see cref="World.BossLair"/> when it places the boss.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class BossAbilities : MonoBehaviour
    {
        private const float EngageRange = 16f;
        private const float SlamRadius = 5f;
        private const float SlamWindUp = 1.1f;
        private const float SlamEvery = 7f;
        private const float TrickEvery = 11f;
        private const int MaxMinions = 4;

        private EnemyHealth health;
        private EnemyKind kind;
        private int level;
        private GameObject minionPrefab;
        private PlayerStats player;
        private float nextSlam;
        private float nextTrick;
        private readonly List<EnemyHealth> minions = new List<EnemyHealth>();

        public void Configure(EnemyKind bossKind, int monsterLevel, GameObject enemyPrefab)
        {
            kind = bossKind;
            level = monsterLevel;
            minionPrefab = enemyPrefab;
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            health.Died += OnBossDied;
        }

        // The raised dead fall with their master.
        private void OnBossDied()
        {
            foreach (EnemyHealth minion in minions)
            {
                if (minion != null && !minion.IsDead)
                    minion.TakeDamage(minion.CurrentHealth + 1f);
            }
            minions.Clear();
        }

        private void OnEnable()
        {
            // First moves come a little after the fight starts, not on the first frame.
            nextSlam = Time.time + 4f;
            nextTrick = Time.time + 6f;
        }

        private void OnDestroy()
        {
            // Raised dead crumble with their master's lair being cleared away.
            foreach (EnemyHealth minion in minions)
            {
                if (minion != null && !minion.IsDead)
                    Destroy(minion.gameObject);
            }
        }

        private void Update()
        {
            if (kind == null || health.IsDead)
                return;

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerStats>();
                if (player == null)
                    return;
            }

            if (player.IsDead || Flat(player.transform.position - transform.position).magnitude > EngageRange)
            {
                // Out of the fight: the timers wait.
                nextSlam = Mathf.Max(nextSlam, Time.time + 2f);
                nextTrick = Mathf.Max(nextTrick, Time.time + 3f);
                return;
            }

            if (Time.time >= nextSlam)
            {
                nextSlam = Time.time + SlamEvery;
                StartCoroutine(Blast(transform.position, SlamRadius, SlamWindUp, kind.Damage * 1.5f, kind.DamageType));
            }
            else if (Time.time >= nextTrick)
            {
                nextTrick = Time.time + TrickEvery;
                if (kind.Boss == BossStyle.Gravelord)
                    RaiseDead();
                else if (kind.Boss == BossStyle.Warlord)
                    RainDown(DamageType.Fire, 4);
                else if (kind.Boss == BossStyle.FrostQueen)
                    RainDown(DamageType.Cold, 5);
            }
        }

        private void RaiseDead()
        {
            minions.RemoveAll(m => m == null || m.IsDead);
            int count = Mathf.Min(2, MaxMinions - minions.Count);
            if (minionPrefab == null || count <= 0)
                return;

            for (int k = 0; k < count; k++)
            {
                float a = (k / (float)count) * Mathf.PI * 2f + Random.value;
                Vector3 at = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3.5f;
                at.y = 1.1f;
                GameObject zombie = Instantiate(minionPrefab, at, Quaternion.LookRotation(Flat(transform.position - at) + Vector3.forward * 0.001f), transform.parent);
                EnemyKinds.Apply(zombie, 0, level);
                minions.Add(zombie.GetComponent<EnemyHealth>());
                Skills.SkillEffects.Shockwave(at, 1.4f, new Color(0.3f, 0.9f, 0.4f), 0.5f);
            }
        }

        // Patches round the player (the first right under them) that burst after a beat.
        private void RainDown(DamageType type, int count)
        {
            // Enraged, the first patch comes down where the player is heading.
            EnemyController controller = GetComponent<EnemyController>();
            Vector3 target = controller != null && controller.IsEnraged
                ? PlayerMotion.Predict(player, 1.0f)
                : player.transform.position;
            for (int k = 0; k < count; k++)
            {
                Vector2 scatter = Random.insideUnitCircle * 4f;
                Vector3 at = k == 0 ? target : target + new Vector3(scatter.x, 0f, scatter.y);
                StartCoroutine(Blast(at, 2.3f, 1.3f + k * 0.25f, kind.Damage * 1.2f, type));
            }
        }

        // A glowing patch that fills in over the wind-up, then hurts the player if they're still on it.
        private IEnumerator Blast(Vector3 center, float radius, float windUp, float damage, DamageType type)
        {
            return GroundTelegraph.Run(center, radius, windUp, type, at =>
            {
                if (this == null || (health.IsDead && type != DamageType.Fire))
                    return;
                if (player != null && !player.IsDead && Flat(player.transform.position - at).magnitude <= radius)
                    player.TakeHit(damage * EnemyKinds.DamageScale(level), type);
            });
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}

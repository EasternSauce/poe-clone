using UnityEngine;
using PoeClone.Audio;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Basic enemy: stands still until the player gets close, then chases
    /// at a fraction of the player's speed. If the player gets far enough
    /// away, the enemy loses interest and stops. Ranged kinds stop at shooting range
    /// instead of closing in, stand their ground, and only every few shots take a
    /// short step back if the player is close (so they can't be pinned, but don't kite
    /// endlessly either). Attacking itself is <see cref="EnemyCombat"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyController : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Chasing
        }

        [Header("Chasing")]
        [SerializeField] private float aggroRange = 12f;
        [SerializeField] private float loseInterestRange = 18f;
        [SerializeField] private float stopDistance = 1.8f;

        [Header("Movement")]
        [SerializeField, Range(0.1f, 1.5f)] private float speedRatioToPlayer = 0.5f;
        [SerializeField] private float fallbackSpeed = 3f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private PlayerController player;
        private PlayerStats playerStats;
        private State state = State.Idle;
        private float verticalVelocity;
        private int avoidSide;
        private float lastBlockedTime = -10f;
        private CharacterAttackAnimator attackAnimator;
        private Stagger stagger;
        private EnemySkills skills;
        private EnemyHealth health;
        private EnemyKind kind;

        // How far an aggroing enemy's shout carries to wake up idle neighbours (one hop only - the
        // neighbours it wakes don't themselves wake further neighbours, so a fight doesn't
        // eventually summon the whole area).
        private const float AlertRadius = 10f;

        // How long the enemy sticks to an avoidance side after the direct path was last blocked.
        private const float AvoidCommitTime = 1.0f;

        [Header("Ranged Repositioning")]
        [Tooltip("Ranged kinds back off after this many attacks (0 = never).")]
        [SerializeField] private int attacksBeforeRetreat = 0;
        [Tooltip("...but only if the player is at least this close.")]
        [SerializeField] private float retreatTriggerDistance = 6f;
        [SerializeField] private float retreatSeconds = 1.2f;

        private int attacksAtLastRetreat;
        private float retreatUntil = -1f;

        [Header("Obstacle Avoidance")]
        [SerializeField] private float lookAhead = 1.8f;

        public State CurrentState => state;

        private float chilledUntil = -1f;

        /// <summary>Slowed to half speed for a while (frost skills).</summary>
        public void Chill(float seconds)
        {
            chilledUntil = Mathf.Max(chilledUntil, Time.time + seconds);
        }

        private float MoveSpeed =>
            (player != null
                ? player.MoveSpeed * speedRatioToPlayer
                : fallbackSpeed) * (Time.time < chilledUntil ? 0.5f : 1f) * (IsEnraged ? EnrageSpeed : 1f);

        // Enrage: hit from a distance (an arrow, a spell), an enemy now and then flies into a
        // rage - faster and harder-hitting for a while - so standing back and picking things off
        // while backing away isn't free. Not every hit: once it calms down it can't rage again
        // for a while.
        private const float EnrageHitDistance = 6f;
        private const float EnrageSeconds = 5f;
        private const float EnrageCooldown = 12f;
        private const float EnrageSpeed = 1.6f;
        private const float EnrageDamage = 1.35f;
        private static readonly Color EnrageColor = new Color(1f, 0.15f, 0.08f);

        private float enragedUntil = -1f;
        private float nextEnrageAt;
        private float nextEnragePulse;

        public bool IsEnraged => Time.time < enragedUntil;

        /// <summary>What its hits are multiplied by right now (an enraged enemy hits harder).</summary>
        public float DamageMultiplier => IsEnraged ? EnrageDamage : 1f;

        /// <summary>How much faster it attacks right now.</summary>
        public float AttackSpeedMultiplier => IsEnraged ? 1.3f : 1f;

        private void Enrage()
        {
            enragedUntil = Time.time + EnrageSeconds;
            nextEnrageAt = enragedUntil + EnrageCooldown;
            nextEnragePulse = 0f;

            float size = transform.localScale.y;
            SkillEffects.Shockwave(transform.position, 2.2f * size, EnrageColor, 0.45f);
            CombatText.Show(transform.position + Vector3.up * (health != null ? health.BarHeight : 2.3f) * size,
                "ENRAGED", EnrageColor, 1.2f);
            EnemySounds.Play(kind ?? EnemyKinds.Get(0), EnemySounds.Event.Aggro, transform.position);
        }

        // A red pulse at its feet for as long as the rage lasts.
        private void UpdateEnrage()
        {
            if (!IsEnraged || Time.time < nextEnragePulse)
                return;
            nextEnragePulse = Time.time + 0.45f;
            SkillEffects.Shockwave(transform.position, 1.1f * transform.localScale.y, EnrageColor, 0.35f);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            stagger = GetComponent<Stagger>();
            if (stagger == null)
                stagger = gameObject.AddComponent<Stagger>();

            if (GetComponent<EnemyCombat>() == null)
                gameObject.AddComponent<EnemyCombat>();
        }

        /// <summary>Takes on a kind's pace and preferred distance (see <see cref="EnemyKinds.Apply"/>).</summary>
        public void Configure(EnemyKind kind)
        {
            this.kind = kind;
            speedRatioToPlayer = kind.SpeedRatio;
            // Backing off isn't enough to shake them: they follow a long way.
            loseInterestRange = Mathf.Max(loseInterestRange, 26f);

            if (kind.IsRanged)
            {
                // Stop a little inside casting range so a step back by the player doesn't
                // immediately put them out of it again.
                stopDistance = kind.AttackRange * 0.8f;
                attacksBeforeRetreat = 3;
                aggroRange = Mathf.Max(aggroRange, kind.AttackRange + 3f);
                loseInterestRange = Mathf.Max(loseInterestRange, aggroRange + 6f);
            }
            else
            {
                stopDistance = kind.AttackRange * 0.9f;
            }
        }

        private void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            playerStats = FindAnyObjectByType<PlayerStats>();

            // Deferred to Start so it runs after EnemyCombat.Awake has had a chance to add the
            // attack animator to the model.
            attackAnimator = GetComponentInChildren<CharacterAttackAnimator>();
            skills = GetComponent<EnemySkills>();

            health = GetComponent<EnemyHealth>();
            if (health != null)
                health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (health != null)
                health.Damaged -= OnDamaged;
        }

        // A hit always aggroes, even from outside aggroRange (an arrow/spell from off-screen),
        // instead of only ever noticing the player by proximity.
        private void OnDamaged()
        {
            Aggro();

            if (health == null || health.IsDead || player == null || Time.time < nextEnrageAt)
                return;
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude >= EnrageHitDistance)
                Enrage();
        }

        /// <summary>Starts chasing the player now (a slime's offspring, born angry).</summary>
        public void Alert()
        {
            Aggro();
        }

        private void Aggro()
        {
            if (state == State.Chasing)
                return;
            state = State.Chasing;
            PlayAggroSound();
            AlertNearby();
        }

        private void PlayAggroSound()
        {
            EnemySounds.Play(kind ?? EnemyKinds.Get(0), EnemySounds.Event.Aggro, transform.position);
        }

        private void AlertNearby()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, AlertRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                EnemyController other = hit.GetComponentInParent<EnemyController>();
                if (other != null && other != this && other.state == State.Idle)
                {
                    other.state = State.Chasing;
                    other.PlayAggroSound();
                }
            }
        }

private void Update()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            if (playerStats == null)
            {
                playerStats = FindAnyObjectByType<PlayerStats>();
            }

            UpdateEnrage();

            // A skill winding up or charging moves (or holds) the enemy itself.
            if (skills != null && skills.enabled && skills.Busy)
                return;

            bool staggered = stagger != null && stagger.IsStaggered;
            bool attacking = attackAnimator != null && attackAnimator.IsAttacking;
            bool playerDead = playerStats != null && playerStats.IsDead;

            Vector3 horizontal = Vector3.zero;
            Vector3 facing = Vector3.zero;

            // A mini-stun: frozen in place, no tracking, until it wears off. A dead player is
            // treated the same as no target: stop chasing/facing rather than stand there tracking a corpse.
            if (player != null && !staggered && !playerDead)
            {
                Vector3 toPlayer = player.transform.position - transform.position;
                toPlayer.y = 0f;
                float distance = toPlayer.magnitude;

                UpdateState(distance);

                if (state == State.Chasing)
                {
                    facing = toPlayer;

                    // A retreat runs its course first, even once it's carried the enemy past its
                    // stop distance; then it turns back to face the player and shoot.
                    if (!attacking && distance > 0.001f && ShouldRetreat(distance))
                    {
                        horizontal = Steer(-toPlayer / distance);
                        facing = horizontal;
                    }
                    // Still allowed to face the player mid-swing, just not to keep closing in.
                    else if (distance > stopDistance && !attacking)
                    {
                        horizontal = Steer(toPlayer / distance);
                        facing = horizontal;
                    }
                }
            }

            if (facing.sqrMagnitude > 0.001f)
            {
                Quaternion look = Quaternion.LookRotation(facing);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    look,
                    rotationSpeed * Time.deltaTime
                );
            }

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = horizontal * MoveSpeed;
            velocity.y = verticalVelocity;

            // Come down off another character rather than ride on it.
            velocity += slideOff * SlideOffSpeed;
            slideOff = Vector3.zero;

            controller.Move(velocity * Time.deltaTime);
        }

        // Standing on another character (a leap that came down on a packmate, or a low creature
        // that climbed one in a crowd): the way off it, picked up from this frame's move.
        private Vector3 slideOff;
        private const float SlideOffSpeed = 4f;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y < 0.05f || !(hit.collider is CharacterController) || hit.collider.gameObject == gameObject)
                return;

            Vector3 away = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (away.sqrMagnitude < 0.0025f)
            {
                Vector2 r = Random.insideUnitCircle;
                away = new Vector3(r.x, 0f, r.y);
            }
            slideOff = away.normalized;
        }

        // Picks a walking direction: straight at the player if the way is clear,
        // otherwise the smallest turn to either side that is clear.
private Vector3 Steer(Vector3 desired)
        {
            if (!IsBlocked(desired))
            {
                // Only forget the chosen side once the way has stayed clear for a while,
                // otherwise a brief false "clear" makes the enemy flip sides and dither.
                if (Time.time - lastBlockedTime > AvoidCommitTime)
                {
                    avoidSide = 0;
                }

                return desired;
            }

            lastBlockedTime = Time.time;

            float[] angles = { 30f, 60f, 90f, 120f };
            int preferred = avoidSide != 0 ? avoidSide : 1;

            foreach (float angle in angles)
            {
                for (int i = 0; i < 2; i++)
                {
                    int side = i == 0 ? preferred : -preferred;
                    Vector3 candidate = Quaternion.AngleAxis(angle * side, Vector3.up) * desired;

                    if (!IsBlocked(candidate))
                    {
                        avoidSide = side;
                        return candidate;
                    }
                }
            }

            return desired;
        }

private bool IsBlocked(Vector3 direction)
        {
            float radius = controller.radius * 0.9f;

            // Start the cast slightly behind us. Unity ignores colliders the cast starts
            // inside of, so without this a wall we're pressed against looks "clear".
            const float backOff = 0.4f;
            Vector3 origin = transform.position - direction * backOff;

            if (!Physics.SphereCast(
                    origin,
                    radius,
                    direction,
                    out RaycastHit hit,
                    lookAhead + backOff,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Ignore ourselves and the player (the player is the target, not an obstacle).
            if (hit.collider.transform.IsChildOf(transform))
            {
                return false;
            }

            return player == null || hit.collider.gameObject != player.gameObject;
        }

        // Every few attacks, if the player has closed in, a short burst of backing off; the rest of
        // the time a ranged enemy holds its ground and keeps shooting.
        private bool ShouldRetreat(float distance)
        {
            if (Time.time < retreatUntil)
                return true;

            if (attacksBeforeRetreat <= 0 || attackAnimator == null)
                return false;

            if (attackAnimator.AttackCount - attacksAtLastRetreat < attacksBeforeRetreat)
                return false;

            attacksAtLastRetreat = attackAnimator.AttackCount;
            if (distance > retreatTriggerDistance)
                return false;

            retreatUntil = Time.time + retreatSeconds;
            return true;
        }

        private void UpdateState(float distance)
        {
            if (state == State.Idle && distance <= aggroRange)
            {
                Aggro();
            }
            else if (state == State.Chasing && distance > loseInterestRange)
            {
                state = State.Idle;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 feet = transform.position + Vector3.down * 1f;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(feet, aggroRange);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(feet, loseInterestRange);
        }
    }
}

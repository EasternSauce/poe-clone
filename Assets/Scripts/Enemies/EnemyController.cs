using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Basic enemy: stands still until the player gets close, then chases
    /// at a fraction of the player's speed. If the player gets far enough
    /// away, the enemy loses interest and stops. No attacks yet.
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
        [SerializeField, Range(0.1f, 1f)] private float speedRatioToPlayer = 0.5f;
        [SerializeField] private float fallbackSpeed = 3f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private PlayerController player;
        private State state = State.Idle;
        private float verticalVelocity;
        private int avoidSide;
        private float lastBlockedTime = -10f;
        private CharacterAttackAnimator attackAnimator;
        private Stagger stagger;

        // How long the enemy sticks to an avoidance side after the direct path was last blocked.
        private const float AvoidCommitTime = 1.0f;

        [Header("Obstacle Avoidance")]
        [SerializeField] private float lookAhead = 1.8f;

        public State CurrentState => state;

        private float MoveSpeed =>
            player != null
                ? player.MoveSpeed * speedRatioToPlayer
                : fallbackSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            stagger = GetComponent<Stagger>();
            if (stagger == null)
                stagger = gameObject.AddComponent<Stagger>();

            if (GetComponent<EnemyCombat>() == null)
                gameObject.AddComponent<EnemyCombat>();
        }

        private void Start()
        {
            player = FindAnyObjectByType<PlayerController>();

            // Deferred to Start so it runs after EnemyCombat.Awake has had a chance to add the
            // attack animator to the model.
            attackAnimator = GetComponentInChildren<CharacterAttackAnimator>();
        }

private void Update()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            bool staggered = stagger != null && stagger.IsStaggered;
            bool attacking = attackAnimator != null && attackAnimator.IsAttacking;

            Vector3 horizontal = Vector3.zero;
            Vector3 facing = Vector3.zero;

            // A mini-stun: frozen in place, no tracking, until it wears off.
            if (player != null && !staggered)
            {
                Vector3 toPlayer = player.transform.position - transform.position;
                toPlayer.y = 0f;
                float distance = toPlayer.magnitude;

                UpdateState(distance);

                if (state == State.Chasing)
                {
                    facing = toPlayer;

                    // Still allowed to face the player mid-swing, just not to keep closing in.
                    if (distance > stopDistance && !attacking)
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

            controller.Move(velocity * Time.deltaTime);
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

        private void UpdateState(float distance)
        {
            if (state == State.Idle && distance <= aggroRange)
            {
                state = State.Chasing;
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

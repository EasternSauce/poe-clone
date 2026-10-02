using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using PoeClone.Combat;
using PoeClone.Visuals;

namespace PoeClone.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float sprintMultiplier = 1.7f;

        [Tooltip("Speed while chilled by a cold hit, as a fraction of normal.")]
        [SerializeField] private float chilledSpeed = 0.7f;

        private CharacterController controller;
        private Vector3 verticalVelocity;
        private CharacterAttackAnimator attackAnimator;
        private Stagger stagger;
        private float chilledUntil = -1f;

        // Walking somewhere on its own (to an item the player clicked), until it arrives, the player
        // steers or attacks, or it gives up (stuck behind something).
        private const float WalkTimeout = 6f;
        private bool walking;
        private Vector3 walkTarget;
        private float walkArriveDistance;
        private float walkGiveUpAt;

        public bool IsWalkingToTarget => walking;

        // A dash: moves the character itself for a moment, ignoring input.
        private Vector3 dashVelocity;
        private float dashTimeLeft;

        public bool IsDashing => dashTimeLeft > 0f;

        /// <summary>Rushes the character along a flat direction, covering the distance in the time.</summary>
        public void Dash(Vector3 direction, float distance, float seconds)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = transform.forward;
            direction.Normalize();

            walking = false;
            dashTimeLeft = Mathf.Max(0.05f, seconds);
            dashVelocity = direction * (distance / dashTimeLeft);
            transform.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>The direction the movement input currently points, in world space (zero when idle).</summary>
        public Vector3 InputDirection()
        {
            return CameraRelativeDirection(ReadMovementInput());
        }

        /// <summary>
        /// Turns a screen-relative stick input (y = up the screen, as from a joystick or WASD) into
        /// a flat world direction, relative to the camera's current yaw - so "up" on any stick always
        /// points up the screen no matter how the camera is rotated. Zero input gives zero back.
        /// </summary>
        public static Vector3 CameraRelativeDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            Vector3 forward = Vector3.forward;
            Camera cam = Camera.main;
            if (cam != null)
            {
                forward = cam.transform.forward;
                forward.y = 0f;
                forward.Normalize();
            }
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            return (right * input.x + forward * input.y).normalized;
        }

        public void WalkTo(Vector3 target, float arriveDistance)
        {
            walking = true;
            walkTarget = target;
            walkArriveDistance = arriveDistance;
            walkGiveUpAt = Time.time + WalkTimeout;
        }

        public void CancelWalk()
        {
            walking = false;
        }

        // Walking speed. Enemies scale off this, so sprinting does not change their speed.
        public float MoveSpeed => moveSpeed;

        private float speedMultiplier = 1f;

        // Movement speed from gear. Enemies scale off MoveSpeed (the base), so this does not speed them up.
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public float SpeedMultiplier => speedMultiplier;

        public bool IsChilled => Time.time < chilledUntil;

        /// <summary>Slowed for a moment by a cold hit (PoE's chill).</summary>
        public void Chill(float seconds)
        {
            chilledUntil = Mathf.Max(chilledUntil, Time.time + seconds);
        }

        // Hold Shift (or toggle the on-screen run button) to sprint. No stamina or mana cost.
        private bool IsSprinting()
        {
            if (VirtualInput.Sprint)
                return true;

            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            attackAnimator = GetComponentInChildren<CharacterAttackAnimator>();

            stagger = GetComponent<Stagger>();
            if (stagger == null)
                stagger = gameObject.AddComponent<Stagger>();

            if (GetComponent<LootPicker>() == null)
                gameObject.AddComponent<LootPicker>();
            if (GetComponent<Skills.PlayerSkills>() == null)
                gameObject.AddComponent<Skills.PlayerSkills>();
            if (GetComponent<PlayerPotions>() == null)
                gameObject.AddComponent<PlayerPotions>();
            if (GetComponent<NpcInteractor>() == null)
                gameObject.AddComponent<NpcInteractor>();
            if (GetComponent<Quests.QuestLog>() == null)
                gameObject.AddComponent<Quests.QuestLog>();
            if (GetComponent<PlayerPassives>() == null)
                gameObject.AddComponent<PlayerPassives>();
            if (GetComponent<TownPortal>() == null)
                gameObject.AddComponent<TownPortal>();
            if (GetComponent<PlayerLight>() == null)
                gameObject.AddComponent<PlayerLight>();
        }

private void Update()
        {
            if (dashTimeLeft > 0f)
            {
                dashTimeLeft -= Time.deltaTime;
                controller.Move(dashVelocity * Time.deltaTime);
                ApplyGravity();
                return;
            }

            Vector2 input = ReadMovementInput();

            // Move relative to the camera so W is always "up the screen",
            // no matter what yaw the camera has.
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            Camera cam = Camera.main;
            if (cam != null)
            {
                forward = cam.transform.forward;
                forward.y = 0f;
                forward.Normalize();
                right = new Vector3(forward.z, 0f, -forward.x);
            }

            Vector3 movement = right * input.x + forward * input.y;

            movement = Vector3.ClampMagnitude(
                movement,
                1f
            );

            // Steering by hand always wins over walking to a clicked item.
            if (movement.sqrMagnitude > 0.001f)
                walking = false;
            else if (walking)
                movement = WalkDirection();

            bool staggered = stagger != null && stagger.IsStaggered;
            if (staggered)
                movement = Vector3.zero;

            float speed = (IsSprinting() ? moveSpeed * sprintMultiplier : moveSpeed) * speedMultiplier;
            if (IsChilled)
                speed *= chilledSpeed;

            controller.Move(
                movement *
                speed *
                Time.deltaTime
            );

            // Skip the movement-direction turn while a swing is in progress, otherwise holding a
            // movement key fights the instant snap-to-click facing PlayerCombat just applied.
            bool attacking = attackAnimator != null && attackAnimator.IsAttacking;

            if (movement.sqrMagnitude > 0.001f && !attacking)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(movement);

                transform.rotation =
                    Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        rotationSpeed * Time.deltaTime
                    );
            }

            ApplyGravity();
        }

        private Vector3 WalkDirection()
        {
            Vector3 toTarget = walkTarget - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= walkArriveDistance || Time.time > walkGiveUpAt)
            {
                walking = false;
                return Vector3.zero;
            }

            return toTarget.normalized;
        }

        private Vector2 ReadMovementInput()
        {
            Keyboard keyboard = Keyboard.current;

            // A focused UI text field (e.g. the chat box) should consume WASD as text, not movement.
            if (IsUiFocused())
                return Vector2.zero;

            // The touch joystick keeps its analog length, so a light push walks slower.
            if (VirtualInput.Move.sqrMagnitude > 0.0001f)
                return Vector2.ClampMagnitude(VirtualInput.Move, 1f);

            if (keyboard == null)
                return Vector2.zero;

            Vector2 input = Vector2.zero;

            if (keyboard.wKey.isPressed ||
                keyboard.upArrowKey.isPressed)
            {
                input.y += 1f;
            }

            if (keyboard.sKey.isPressed ||
                keyboard.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }

            if (keyboard.dKey.isPressed ||
                keyboard.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }

            if (keyboard.aKey.isPressed ||
                keyboard.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }

            return input.normalized;
        }

        private static int clickConsumedFrame = -1;

        /// <summary>
        /// Marks this frame's click as used (an item picked up, someone talked to, a gate walked to),
        /// so the attack doesn't also fire on it, whichever script happens to run first.
        /// </summary>
        public static void ConsumeClick()
        {
            clickConsumedFrame = Time.frameCount;
        }

        public static bool ClickConsumed => clickConsumedFrame == Time.frameCount;

        /// <summary>The mouse is over a clickable on-screen panel (skills, chat...), not the world.</summary>
        internal static bool IsPointerOverUi()
        {
            EventSystem es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        internal static bool IsUiFocused()
        {
            EventSystem es = EventSystem.current;
            return es != null && es.currentSelectedGameObject != null;
        }

        private void ApplyGravity()
        {
            if (controller.isGrounded &&
                verticalVelocity.y < 0f)
            {
                verticalVelocity.y = -2f;
            }

            verticalVelocity.y +=
                gravity * Time.deltaTime;

            controller.Move(
                verticalVelocity *
                Time.deltaTime
            );
        }
    }
}

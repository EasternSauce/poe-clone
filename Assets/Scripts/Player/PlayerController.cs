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

        private CharacterController controller;
        private Vector3 verticalVelocity;
        private CharacterAttackAnimator attackAnimator;
        private Stagger stagger;

        // Walking speed. Enemies scale off this, so sprinting does not change their speed.
        public float MoveSpeed => moveSpeed;

        private float speedMultiplier = 1f;

        // Movement speed from gear. Enemies scale off MoveSpeed (the base), so this does not speed them up.
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public float SpeedMultiplier => speedMultiplier;

        private float CurrentSpeed => (IsSprinting() ? moveSpeed * sprintMultiplier : moveSpeed) * speedMultiplier;

        // Hold Shift (or toggle the on-screen run button) to sprint. No stamina.
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
        }

private void Update()
        {
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

            bool staggered = stagger != null && stagger.IsStaggered;
            if (staggered)
                movement = Vector3.zero;

            controller.Move(
                movement *
                CurrentSpeed *
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

using UnityEngine;
using UnityEngine.InputSystem;

namespace PoeClone.CameraSystem
{
    /// <summary>
    /// Dev view for inspecting the character model up close: the camera stays put and turns to
    /// watch the player walk around it, so every side of the model comes into view. Movement stays
    /// camera-relative. Middle-mouse drag or [ / ] orbits, PageUp / PageDown tilts and scroll zooms,
    /// each re-placing the camera around the character's current position.
    /// </summary>
    public sealed class CharacterCloseupCamera : MonoBehaviour
    {
        // The player root already sits at mid-body, so look slightly above it (upper chest).
        private const float FocusHeight = 0.25f;
        private const float MinDistance = 1.5f, MaxDistance = 12f;
        private const float MinPitch = -10f, MaxPitch = 80f;

        private CameraFollow follow;
        private float yaw = 45f, pitch = 15f, distance = 5f;
        private float lastYaw, lastPitch, lastDistance;
        private bool placed;
        private float savedNear;

        public static CharacterCloseupCamera Open()
        {
            Camera cam = Camera.main;
            if (!Application.isPlaying || cam == null) return null;
            var mode = cam.GetComponent<CharacterCloseupCamera>();
            if (mode == null) mode = cam.gameObject.AddComponent<CharacterCloseupCamera>();
            mode.enabled = true;
            return mode;
        }

        public static void Close()
        {
            var mode = Camera.main != null ? Camera.main.GetComponent<CharacterCloseupCamera>() : null;
            if (mode != null) Destroy(mode);
        }

        private void OnEnable()
        {
            follow = GetComponent<CameraFollow>();
            if (follow != null) follow.enabled = false;
            var cam = GetComponent<Camera>();
            savedNear = cam.nearClipPlane;
            cam.nearClipPlane = 0.05f;
        }

        private void OnDisable()
        {
            GetComponent<Camera>().nearClipPlane = savedNear;
            if (follow != null)
            {
                follow.enabled = true;
                follow.SnapToTarget();
            }
        }

        private void LateUpdate()
        {
            Transform target = follow != null ? follow.Target : null;
            if (target == null) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;
            if (keyboard != null)
            {
                if (keyboard.leftBracketKey.isPressed) yaw += 90f * dt;
                if (keyboard.rightBracketKey.isPressed) yaw -= 90f * dt;
                if (keyboard.pageUpKey.isPressed) pitch += 45f * dt;
                if (keyboard.pageDownKey.isPressed) pitch -= 45f * dt;
            }
            if (mouse != null)
            {
                if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * 0.3f;
                    pitch -= delta.y * 0.3f;
                }
                distance *= Mathf.Exp(-CameraFollow.WheelNotches(mouse) * 0.15f);
            }
            Vector3 focus = target.position + Vector3.up * FocusHeight;
            if (placed && yaw == lastYaw && pitch == lastPitch && distance == lastDistance)
            {
                // Held in place: only turn to keep the character in view as they walk around.
                Vector3 toFocus = focus - transform.position;
                if (toFocus.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(toFocus);
                Vector3 look = toFocus.normalized;
                yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                pitch = Mathf.Asin(-look.y) * Mathf.Rad2Deg;
                distance = toFocus.magnitude;
            }
            else
            {
                pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
                distance = Mathf.Clamp(distance, MinDistance, MaxDistance);
                Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
                transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
                placed = true;
            }
            lastYaw = yaw;
            lastPitch = pitch;
            lastDistance = distance;
        }
    }
}

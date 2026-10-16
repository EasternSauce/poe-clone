using UnityEngine;
using UnityEngine.InputSystem;

namespace PoeClone.CameraSystem
{
    /// <summary>
    /// Dev view for inspecting the character model up close while still walking around: the camera
    /// orbits the player at chest height instead of the fixed gameplay angle. Movement stays
    /// camera-relative, so WASD follows whichever way the view faces.
    /// Middle-mouse drag or [ / ] orbits, PageUp / PageDown tilts, scroll zooms.
    /// </summary>
    public sealed class CharacterCloseupCamera : MonoBehaviour
    {
        // The player root already sits at mid-body, so look slightly above it (upper chest).
        private const float FocusHeight = 0.25f;
        private const float MinDistance = 1.5f, MaxDistance = 12f;
        private const float MinPitch = -10f, MaxPitch = 80f;

        private CameraFollow follow;
        private float yaw = 45f, pitch = 15f, distance = 4f;
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
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    distance *= Mathf.Exp(-Mathf.Clamp(scroll / 120f, -3f, 3f) * 0.15f);
            }
            pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
            distance = Mathf.Clamp(distance, MinDistance, MaxDistance);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + Vector3.up * FocusHeight;
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}

using PoeClone.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PoeClone.CameraSystem
{
    public class CameraFollow : MonoBehaviour, PoeClone.Audio.IWorldAudioListener
    {
        [SerializeField] private Transform target;

        /// <summary>The character being viewed, also the listening position for world sounds.</summary>
        public Transform Target => target;

        [Header("Angle (PoE style)")]
        [SerializeField, Range(30f, 80f)] private float pitch = 55f;
        [SerializeField] private float yaw = 45f;

        [Header("Framing")]
        [SerializeField] private float distance = 22f;
        [SerializeField] private float fieldOfView = 30f;
        // Phones sit closer in: less of the area on screen, but characters and item names
        // (world-space labels) come out big enough to read on a small screen.
        [SerializeField] private float touchDistanceScale = 0.7f;

        [SerializeField]
        private float followSpeed = 10f;

        // A short jolt for heavy impacts (a maul slam, a boss stomp): strength in metres, fading out.
        private static float shakeStrength;
        private static float shakeSeconds;
        private static float shakeLeft;
        /// <summary>Steady camera tremor while a large encounter hazard is present.</summary>
        public static float SustainedShake;

        private Vector3 appliedShake;

        /// <summary>
        /// How far the view pulls back (1 = normal), for a fight against something too big to fit
        /// otherwise (the Shepherd's giant form). Whoever sets it puts it back to 1 when done; the
        /// camera eases between the two.
        /// </summary>
        public static float Zoom = 1f;

        private float zoom = 1f;
        private float playerZoom = 1f;
        private float pinchDistance;
        private int pinchIdA, pinchIdB;
        private PoeClone.UI.TouchControlsUI touchControls;
        private const float ClosestDistanceScale = 0.6f;
        private const float FarthestDistanceScale = 1.3f;

        private void UpdatePlayerZoom()
        {
            if (!TouchMode.Active)
            {
                pinchDistance = 0f;
                Mouse mouse = Mouse.current;
                if (mouse == null || UiKit.IsTextPromptOpen || UiKit.IsTypingInTextField() ||
                    (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) ChangePlayerZoom(Mathf.Exp(-Mathf.Clamp(scroll / 120f, -3f, 3f) * 0.1f));
                return;
            }

            if (touchControls == null)
                touchControls = FindAnyObjectByType<PoeClone.UI.TouchControlsUI>();
            // A joystick owns its finger until release, including drags beyond its bounds.
            if (touchControls != null && touchControls.JoystickInUse)
            {
                pinchDistance = 0f;
                return;
            }

            TouchControl first = null, second = null;
            if (Touchscreen.current != null)
                foreach (TouchControl touch in Touchscreen.current.touches)
                {
                    if (!touch.press.isPressed) continue;
                    Vector2 at = touch.position.ReadValue();
                    if (TouchMode.IsOverBlocker(at) || TouchMode.IsOverMenuBlocker(at)) continue;
                    if (first == null) first = touch;
                    else if (second == null) second = touch;
                    else { pinchDistance = 0f; return; }
                }
            if (first == null || second == null || UiKit.IsTextPromptOpen)
            {
                pinchDistance = 0f;
                return;
            }
            float current = Vector2.Distance(first.position.ReadValue(), second.position.ReadValue());
            int idA = first.touchId.ReadValue(), idB = second.touchId.ReadValue();
            if (pinchDistance > 0f && current > 0f && idA == pinchIdA && idB == pinchIdB)
                ChangePlayerZoom(Mathf.Pow(pinchDistance / current, 0.7f));
            pinchDistance = current;
            pinchIdA = idA;
            pinchIdB = idB;
        }

        private void ChangePlayerZoom(float factor)
        {
            float baseline = TouchMode.Active ? touchDistanceScale : 1f;
            playerZoom = Mathf.Clamp(playerZoom * factor, ClosestDistanceScale / baseline, FarthestDistanceScale / baseline);
        }

        /// <summary>Shakes the view for a moment. A stronger shake overrides a weaker one already running.</summary>
        public static void Shake(float strength, float seconds)
        {
            float current = shakeLeft > 0f ? shakeStrength * shakeLeft / shakeSeconds : 0f;
            if (strength < current)
                return;
            shakeStrength = strength;
            shakeSeconds = Mathf.Max(0.01f, seconds);
            shakeLeft = shakeSeconds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            shakeLeft = 0f;
            SustainedShake = 0f;
            Zoom = 1f;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            UpdatePlayerZoom();

            // Rotation is fixed; only the position follows the player.
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            zoom = Mathf.MoveTowards(zoom, Mathf.Max(0.5f, Zoom), 0.8f * Time.deltaTime);

            // The shake rides on top of the follow and is taken off again next frame, so it never
            // drags the camera away from where it should be.
            transform.position -= appliedShake;
            transform.position = Vector3.Lerp(
                transform.position,
                DesiredPosition(),
                1f - Mathf.Exp(-followSpeed * Time.deltaTime)
            );

            appliedShake = Vector3.zero;
            float amount = Mathf.Max(0f, SustainedShake);
            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.deltaTime;
                amount += shakeStrength * Mathf.Clamp01(shakeLeft / shakeSeconds);
            }
            if (amount > 0f)
            {
                appliedShake = transform.rotation * new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * amount;
                transform.position += appliedShake;
            }
        }

        private void Start()
        {
            ApplyLens();
            SnapToTarget();
        }

        private Vector3 DesiredPosition()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            float framing = Mathf.Clamp((TouchMode.Active ? touchDistanceScale : 1f) * playerZoom,
                ClosestDistanceScale, FarthestDistanceScale);
            float d = distance * framing * zoom;
            // Upright screens otherwise crop the fight to a thin strip around the player.
            if (TouchMode.Portrait)
                d *= Mathf.Min(2f, (float)Screen.height / Mathf.Max(1, Screen.width));
            return target.position + rotation * new Vector3(0f, 0f, -d);
        }

        private void ApplyLens()
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null)
                return;

            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
        }

        public void SnapToTarget()
        {
            if (target == null)
                return;

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = DesiredPosition();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SnapToTarget();
        }
    }
}

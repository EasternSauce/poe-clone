using UnityEngine;

namespace PoeClone.CameraSystem
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Angle (PoE style)")]
        [SerializeField, Range(30f, 80f)] private float pitch = 55f;
        [SerializeField] private float yaw = 45f;

        [Header("Framing")]
        [SerializeField] private float distance = 22f;
        [SerializeField] private float fieldOfView = 30f;

        [SerializeField]
        private float followSpeed = 10f;

        // A short jolt for heavy impacts (a maul slam, a boss stomp): strength in metres, fading out.
        private static float shakeStrength;
        private static float shakeSeconds;
        private static float shakeLeft;

        private Vector3 appliedShake;

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
        }

private void LateUpdate()
        {
            if (target == null)
                return;

            // Rotation is fixed; only the position follows the player.
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            // The shake rides on top of the follow and is taken off again next frame, so it never
            // drags the camera away from where it should be.
            transform.position -= appliedShake;
            transform.position = Vector3.Lerp(
                transform.position,
                DesiredPosition(),
                followSpeed * Time.deltaTime
            );

            appliedShake = Vector3.zero;
            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.deltaTime;
                float amount = shakeStrength * Mathf.Clamp01(shakeLeft / shakeSeconds);
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
            return target.position + rotation * new Vector3(0f, 0f, -distance);
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

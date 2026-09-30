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

private void LateUpdate()
        {
            if (target == null)
                return;

            // Rotation is fixed; only the position follows the player.
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            transform.position = Vector3.Lerp(
                transform.position,
                DesiredPosition(),
                followSpeed * Time.deltaTime
            );
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

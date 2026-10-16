using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Very simple, non-cloth "cape physics": tilts and sways the cape mesh(es)
    /// as a single rigid piece based on the player's horizontal movement and
    /// turning, plus a gentle idle wind flutter. No bones/cloth simulation -
    /// just a couple of spring-damped angles applied on top of each part's
    /// resting local rotation. Each part hinges at its top edge and only ever
    /// swings back or sideways, so it trails behind the legs instead of through them.
    /// Cheap enough to run every frame with no setup.
    /// </summary>
    public class SimpleCapePhysics : MonoBehaviour
    {
        [Header("Targets")]
        public Transform[] capeParts; // e.g. Mantle, CloakBack

        [Header("Movement Response")]
        public float pitchPerSpeed = 3f;       // trail backward when moving forward, degrees per m/s
        public float maxPitchForward = 35f;
        public float restPitch = 4f;          // hangs slightly away from the body even when still
        public float rollPerStrafeSpeed = 14f; // sway sideways with lateral velocity
        public float maxRoll = 25f;

        [Header("Spring")]
        public float springSpeed = 8f;  // how fast the cape catches up to its target angle
        public float damping = 0.85f;   // per-frame velocity damping so it settles instead of oscillating forever

        [Header("Idle Wind")]
        public float windAmplitude = 4f;
        public float windSpeed = 1.3f;

        private CharacterController controller;
        private Vector3 lastPosition;
        private float currentPitch, pitchVel;
        private float currentRoll, rollVel;
        private Quaternion[] restLocalRotations;
        private Vector3[] restLocalPositions, hinges;
        private float[] weights;

        private void Start()
        {
            WorldActivity.TrackCosmetic(this);
            controller = GetComponentInParent<CharacterController>();
            lastPosition = transform.position;

            if (capeParts != null)
            {
                restLocalRotations = new Quaternion[capeParts.Length];
                restLocalPositions = new Vector3[capeParts.Length];
                hinges = new Vector3[capeParts.Length];
                weights = new float[capeParts.Length];
                for (int i = 0; i < capeParts.Length; i++)
                {
                    Transform part = capeParts[i];
                    if (part == null) continue;
                    restLocalRotations[i] = part.localRotation;
                    restLocalPositions[i] = part.localPosition;
                    Mesh mesh = part.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                    float top = mesh != null ? mesh.bounds.max.y * part.localScale.y : 0f;
                    hinges[i] = part.localPosition + part.localRotation * (Vector3.up * top);
                    // A short shoulder piece (the mantle) only stirs; the long back panel trails.
                    weights[i] = mesh != null && mesh.bounds.size.y * part.localScale.y < 0.4f ? 0.2f : 1f;
                }
            }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || capeParts == null) return;

            // Estimate this frame's velocity, preferring the CharacterController
            // (accurate even against collisions) and falling back to a simple
            // position-delta if there isn't one.
            Vector3 worldVel = controller != null ? controller.velocity : (transform.position - lastPosition) / dt;
            lastPosition = transform.position;

            Vector3 localVel = transform.InverseTransformDirection(worldVel);

            // Moving forward lets the hem trail back; it never swings forward into the legs.
            float targetPitch = restPitch + Mathf.Clamp(localVel.z * pitchPerSpeed, 0f, maxPitchForward);
            float targetRoll = Mathf.Clamp(-localVel.x * rollPerStrafeSpeed, -maxRoll, maxRoll);

            // Gentle idle flutter so the cape is never perfectly static even when standing still.
            targetPitch += (Mathf.Sin(Time.time * windSpeed) * 0.5f + 0.5f) * windAmplitude * 0.5f;
            targetRoll += Mathf.Sin(Time.time * windSpeed * 1.3f + 1.7f) * windAmplitude;

            currentPitch = SpringTowards(currentPitch, targetPitch, ref pitchVel, dt);
            currentRoll = SpringTowards(currentRoll, targetRoll, ref rollVel, dt);

            for (int i = 0; i < capeParts.Length; i++)
            {
                if (capeParts[i] == null) continue;
                Quaternion sway = Quaternion.Euler(Mathf.Max(0f, currentPitch) * weights[i], 0f, currentRoll * weights[i]);
                capeParts[i].localRotation = sway * restLocalRotations[i];
                capeParts[i].localPosition = hinges[i] + sway * (restLocalPositions[i] - hinges[i]);
            }
        }

        private float SpringTowards(float current, float target, ref float velocity, float dt)
        {
            float accel = (target - current) * springSpeed;
            velocity = (velocity + accel * dt) * damping;
            return current + velocity * dt;
        }
    }
}

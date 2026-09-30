using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>
    /// Very simple, non-cloth "cape physics": tilts and sways the cape mesh(es)
    /// as a single rigid piece based on the player's horizontal movement and
    /// turning, plus a gentle idle wind flutter. No bones/cloth simulation -
    /// just a couple of spring-damped angles applied on top of each part's
    /// resting local rotation. Cheap enough to run every frame with no setup.
    /// </summary>
    public class SimpleCapePhysics : MonoBehaviour
    {
        [Header("Targets")]
        public Transform[] capeParts; // e.g. Mantle, CloakBack

        [Header("Movement Response")]
        public float pitchPerSpeed = 10f;      // billow backward when moving forward, degrees per m/s
        public float maxPitchForward = 35f;
        public float maxPitchBackward = 12f;
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

        private void Start()
        {
            controller = GetComponentInParent<CharacterController>();
            lastPosition = transform.position;

            if (capeParts != null)
            {
                restLocalRotations = new Quaternion[capeParts.Length];
                for (int i = 0; i < capeParts.Length; i++)
                    if (capeParts[i] != null) restLocalRotations[i] = capeParts[i].localRotation;
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

            // Moving forward pushes the cape back and up (billow); moving
            // backward gives it a smaller forward lift so it doesn't flip
            // unrealistically far the other way.
            float maxPitch = localVel.z >= 0f ? maxPitchForward : maxPitchBackward;
            float targetPitch = Mathf.Clamp(-localVel.z * pitchPerSpeed, -maxPitchBackward, maxPitch);
            float targetRoll = Mathf.Clamp(-localVel.x * rollPerStrafeSpeed, -maxRoll, maxRoll);

            // Gentle idle flutter so the cape is never perfectly static even when standing still.
            targetPitch += Mathf.Sin(Time.time * windSpeed) * windAmplitude * 0.5f;
            targetRoll += Mathf.Sin(Time.time * windSpeed * 1.3f + 1.7f) * windAmplitude;

            currentPitch = SpringTowards(currentPitch, targetPitch, ref pitchVel, dt);
            currentRoll = SpringTowards(currentRoll, targetRoll, ref rollVel, dt);

            Quaternion sway = Quaternion.Euler(currentPitch, 0f, currentRoll);
            for (int i = 0; i < capeParts.Length; i++)
            {
                if (capeParts[i] == null) continue;
                capeParts[i].localRotation = restLocalRotations[i] * sway;
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

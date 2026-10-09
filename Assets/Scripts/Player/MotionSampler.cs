using UnityEngine;

namespace PoeClone.Player
{
    /// <summary>
    /// Measures how fast something moved over the last frame, from where it ended up rather than
    /// from a CharacterController (whose velocity only covers its last Move call, and which a
    /// spectator's puppet player doesn't use). Projectiles read it through <see cref="VelocityOf"/>
    /// to carry their shooter's movement.
    /// </summary>
    public class MotionSampler : MonoBehaviour
    {
        // Anything faster than this was a teleport or a snapped pose, not movement.
        private const float MaxSpeed = 40f;

        private Vector3 lastPosition;
        private bool hasPosition;

        public Vector3 Velocity { get; private set; }

        private void LateUpdate()
        {
            Vector3 position = transform.position;
            if (hasPosition && Time.deltaTime > 0f)
            {
                Vector3 velocity = (position - lastPosition) / Time.deltaTime;
                Velocity = velocity.sqrMagnitude <= MaxSpeed * MaxSpeed ? velocity : Vector3.zero;
            }
            lastPosition = position;
            hasPosition = true;
        }

        /// <summary>How fast the shooter is moving across the ground right now.</summary>
        public static Vector3 VelocityOf(Transform shooter)
        {
            if (shooter == null)
                return Vector3.zero;

            MotionSampler sampler = shooter.GetComponent<MotionSampler>();
            if (sampler == null)
                sampler = shooter.gameObject.AddComponent<MotionSampler>();
            Vector3 velocity = sampler.Velocity;
            velocity.y = 0f;
            return velocity;
        }
    }
}

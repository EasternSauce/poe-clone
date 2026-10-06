using UnityEngine;
using PoeClone.Player;

namespace PoeClone.Enemies
{
    /// <summary>
    /// How the player is moving, for enemies that lead their aim (an enraged archer shoots where
    /// the player is going, an enraged caster drops its blast there). Measured from the player's
    /// position once per frame - their controller's own velocity reads zero - and smoothed so a
    /// single odd frame doesn't throw the aim.
    /// </summary>
    public static class PlayerMotion
    {
        // Long guesses miss the player entirely; aim at their launch position instead.
        private const float MaxLeadSeconds = 0.7f;

        private static PlayerStats tracked;
        private static Vector3 velocity;
        private static Vector3 lastPosition;
        private static float lastTime;
        private static int lastFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            tracked = null;
            velocity = Vector3.zero;
            lastFrame = -1;
        }

        /// <summary>
        /// Updates the measurement (callers do this every frame they care; the first call in a
        /// frame does the work).
        /// </summary>
        public static void Track(PlayerStats player)
        {
            if (player == null || Time.frameCount == lastFrame)
                return;

            Vector3 at = player.transform.position;
            float dt = Time.time - lastTime;
            if (player == tracked && lastFrame >= 0 && dt > 0f && dt < 0.25f)
            {
                Vector3 step = at - lastPosition;
                step.y = 0f;
                step /= dt;
                if (step.magnitude > 30f)
                    step = Vector3.zero; // a teleport or a dash, not a run
                velocity = Vector3.Lerp(velocity, step, 1f - Mathf.Exp(-10f * dt));
            }
            else
            {
                velocity = Vector3.zero;
            }

            tracked = player;
            lastPosition = at;
            lastTime = Time.time;
            lastFrame = Time.frameCount;
        }

        /// <summary>The player's measured ground velocity.</summary>
        public static Vector3 Velocity(PlayerStats player)
        {
            Track(player);
            return player == tracked ? velocity : Vector3.zero;
        }

        /// <summary>Lead short attacks; longer attacks aim at the player's current position.</summary>
        public static Vector3 Predict(PlayerStats player, float seconds)
        {
            return seconds > MaxLeadSeconds
                ? player.transform.position
                : player.transform.position + Velocity(player) * Mathf.Max(0f, seconds);
        }

        /// <summary>Where a projectile of this speed fired from <paramref name="from"/> now meets the player.</summary>
        public static Vector3 Intercept(PlayerStats player, Vector3 from, float speed)
        {
            Vector3 at = player.transform.position;
            if (speed <= 0.1f)
                return at;
            float t = 0f;
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = Predict(player, t);
                p.y = from.y;
                t = Vector3.Distance(from, p) / speed;
            }
            return Predict(player, t);
        }
    }
}

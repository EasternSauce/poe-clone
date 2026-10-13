using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>Accumulates elapsed time while visual poses are skipped; simulation keeps its own clock.</summary>
    public struct AnimationBudget
    {
        private float elapsed, next;
        private bool initialized;

        public bool Sample(Transform model, bool important, out float dt, bool force = false)
        {
            elapsed += Time.deltaTime;
            float distance = WorldActivity.DistanceSquared(model.position);
            float near = WorldActivity.ScreenRadius * 0.8f;
            // Player and nearby combat poses follow every rendered frame on every device.
            int rate = important || distance <= near * near ? 60 :
                distance > WorldActivity.ScreenRadius * WorldActivity.ScreenRadius * 2.25f ? 8 :
                15;
            float interval = 1f / rate;
            if (!initialized)
            {
                initialized = true;
                next = Time.time + Mathf.Abs(model.GetHashCode() % 29) / 29f * interval;
            }
            dt = elapsed;
            if (!force && rate < 60 && Time.time < next) return false;
            elapsed = 0f;
            // Sample the current pose once, never loop over skipped frames.
            next = Mathf.Max(next + interval, Time.time);
            return dt > 0f;
        }
    }
}

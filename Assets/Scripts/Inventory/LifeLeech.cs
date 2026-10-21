using System;
using System.Collections.Generic;

namespace PoeClone.Inventory
{
    /// <summary>Independent hit recovery effects sharing one life recovery rate limit.</summary>
    public sealed class LifeLeech
    {
        public const float RatePerEffect = 0.02f;
        public const float MaximumTotalRate = 0.20f;
        public const float MaximumPerEffect = 0.10f;

        private readonly List<float> remaining = new List<float>();

        public void Clear() => remaining.Clear();

        public void Add(float amount, float maximumLife)
        {
            if (amount > 0f && maximumLife > 0f)
                remaining.Add(Math.Min(amount, maximumLife * MaximumPerEffect));
        }

        public float Tick(float seconds, float maximumLife)
        {
            if (seconds <= 0f || maximumLife <= 0f)
                return 0f;

            float rate = maximumLife * RatePerEffect;
            float recovered = 0f;
            // Split at effect expirations so short hits and long frames obey the same cap.
            // Excess recovery is spent, never queued for after the rate drops below the cap.
            while (seconds > 0f && remaining.Count > 0)
            {
                float step = seconds;
                foreach (float amount in remaining)
                    step = Math.Min(step, amount / rate);

                recovered += Math.Min(rate * remaining.Count, maximumLife * MaximumTotalRate) * step;
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    if (remaining[i] / rate <= step)
                        remaining.RemoveAt(i);
                    else
                        remaining[i] -= rate * step;
                }
                seconds -= step;
            }
            return recovered;
        }
    }
}

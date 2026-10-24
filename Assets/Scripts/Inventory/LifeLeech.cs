using System;
using System.Collections.Generic;

namespace PoeClone.Inventory
{
    /// <summary>Independent hit recovery effects sharing one life recovery rate limit.</summary>
    public sealed class LifeLeech
    {
        public const float RatePerEffect = 0.04f;
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
                int expiring = -1;
                for (int i = 0; i < remaining.Count; i++)
                {
                    float untilExpiry = remaining[i] / rate;
                    if (untilExpiry <= step)
                    {
                        step = untilExpiry;
                        expiring = i;
                    }
                }

                recovered += Math.Min(rate * remaining.Count, maximumLife * MaximumTotalRate) * step;
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    // Mono can compare the division at higher precision than the stored
                    // step. Always retire the selected effect, even if step rounded to zero.
                    // Each iteration therefore finishes the frame or removes an effect.
                    if (i == expiring || remaining[i] / rate <= step)
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

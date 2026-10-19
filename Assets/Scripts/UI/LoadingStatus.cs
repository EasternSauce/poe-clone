using UnityEngine;

namespace PoeClone.UI
{
    /// <summary>
    /// What is being loaded right now, shown under the loading screens. Step timings
    /// are recorded by PerformanceDiagnostics without adding console messages.
    /// </summary>
    public static class LoadingStatus
    {
        public static string Step { get; private set; } = string.Empty;
        private static float stepStart;

        public static void Set(string step)
        {
            float now = Time.realtimeSinceStartup;
            if (Step.Length > 0)
                PerformanceDiagnostics.LoadingCompleted(Step, (now - stepStart) * 1000d);
            Step = step ?? string.Empty;
            stepStart = now;
        }

        public static void Clear() => Set(null);
    }
}

using UnityEngine;

namespace PoeClone.UI
{
    /// <summary>
    /// What is being loaded right now, shown under the loading screens. When a step ends its
    /// time goes to the console ("[Loading] ..."), so slow steps are easy to find.
    /// </summary>
    public static class LoadingStatus
    {
        public static string Step { get; private set; } = string.Empty;
        private static float stepStart;

        public static void Set(string step)
        {
            float now = Time.realtimeSinceStartup;
            if (Step.Length > 0)
                Debug.Log($"[Loading] {Step}: {(now - stepStart) * 1000f:0} ms");
            Step = step ?? string.Empty;
            stepStart = now;
        }

        public static void Clear() => Set(null);
    }
}

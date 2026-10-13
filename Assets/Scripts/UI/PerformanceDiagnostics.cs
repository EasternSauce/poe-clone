using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace PoeClone.UI
{
    // Measurements surround synchronous work only: coroutine waits are never charged as CPU work.
    public static class PerformanceDiagnostics
    {
        private sealed class Work
        {
            public string Name;
            public double Total, Peak;
        }
        private static readonly List<Work> work = new List<Work>();
        private static readonly Dictionary<Type, string> routineNames = new Dictionary<Type, string>();
        private static double frameWork, largestStep;
        private static string frameStage = "unmeasured";
        public static double PreviousWork { get; private set; }
        public static string PreviousStage { get; private set; } = "unmeasured";
        public static string LastLoading { get; private set; } = "No completed loading steps";
        public static string SlowestLoading { get; private set; } = "No completed loading steps";
        private static double slowestLoading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            work.Clear(); routineNames.Clear(); frameWork = largestStep = slowestLoading = 0;
            PreviousWork = 0; frameStage = PreviousStage = "unmeasured";
            LastLoading = SlowestLoading = "No completed loading steps";
        }

        public static void ResetWork()
        {
            work.Clear(); frameWork = largestStep = 0;
            PreviousWork = 0; frameStage = PreviousStage = "unmeasured";
        }

        public static long Begin() => Stopwatch.GetTimestamp();
        public static void End(string name, long started)
        {
            double ms = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            Work entry = null;
            for (int i = 0; i < work.Count; i++) if (work[i].Name == name) { entry = work[i]; break; }
            if (entry == null) { entry = new Work { Name = name }; work.Add(entry); }
            entry.Total += ms;
            entry.Peak = Math.Max(entry.Peak, ms);
            frameWork += ms;
            if (ms > largestStep) { largestStep = ms; frameStage = name; }
        }

        public static string RoutineName(System.Collections.IEnumerator routine)
        {
            Type type = routine.GetType();
            if (!routineNames.TryGetValue(type, out string name))
            {
                name = type.Name;
                int end = name.IndexOf('>');
                if (name.StartsWith("<") && end > 1) name = name.Substring(1, end - 1);
                routineNames.Add(type, name);
            }
            return name;
        }

        public static void NextFrame()
        {
            PreviousWork = frameWork; PreviousStage = frameStage;
            frameWork = largestStep = 0; frameStage = "unmeasured";
        }

        public static void LoadingCompleted(string step, double milliseconds)
        {
            LastLoading = $"{step}: {milliseconds:0} ms";
            if (milliseconds > slowestLoading)
            {
                slowestLoading = milliseconds;
                SlowestLoading = LastLoading;
            }
        }

        public static void AppendWork(StringBuilder text)
        {
            work.Sort((a, b) => b.Total.CompareTo(a.Total));
            text.AppendLine("Build CPU: session total / longest step");
            int shown = 0;
            foreach (var entry in work)
            {
                if (entry.Total > 0.05 && shown++ < 3)
                    text.AppendLine($"{entry.Name}: {entry.Total:0.0} / {entry.Peak:0.0} ms");
            }
            if (shown == 0) text.AppendLine("No measured construction work");
        }
    }
}

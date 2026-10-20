using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using PoeClone.World;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Starts the production balance arena in Play, and queues automated class gauntlet
    /// runs: one Play session per build and level, against frozen enemies, then a combined table
    /// in GauntletResults/. From the MCP bridge:
    /// <code>
    /// return PoeClone.EditorTools.GauntletLauncher.Manual();
    /// return PoeClone.EditorTools.GauntletLauncher.Auto();                                   // the four default builds, every level
    /// return PoeClone.EditorTools.GauntletLauncher.Auto("pyromancer", "14,21", rolls: 15);   // rerun one build after editing it
    /// return PoeClone.EditorTools.GauntletLauncher.AutoStatus();
    /// </code>
    /// The character is never saved: the gauntlet runs in the stripped minimal arena.
    /// </summary>
    public static class GauntletLauncher
    {
        private const string DevQueryKey = "PoeClone.DevQuery";
        private const string ActiveKey = "PoeClone.Gauntlet.Active";
        private const string PreviousQueryKey = "PoeClone.Gauntlet.PreviousQuery";
        private const string QueueKey = "PoeClone.Gauntlet.Queue";
        private const string RowsKey = "PoeClone.Gauntlet.Rows";
        private const string OptionsKey = "PoeClone.Gauntlet.Options";
        private const string StatusKey = "PoeClone.Gauntlet.Status";
        private const string RunStartedKey = "PoeClone.Gauntlet.RunStarted";
        private const string SummaryPendingKey = "PoeClone.Gauntlet.SummaryPending";
        private const double RunTimeoutSeconds = 30 * 60;

        [MenuItem("PoeClone/Balance Test Arena", priority = 35)]
        private static void ManualMenu()
        {
            // Let the native menu close and release input before entering Play mode.
            EditorApplication.delayCall -= LaunchManualFromMenu;
            EditorApplication.delayCall += LaunchManualFromMenu;
        }

        private static void LaunchManualFromMenu() => Debug.Log(Manual());

        [MenuItem("PoeClone/Balance/Class Gauntlet (Automated)", priority = 36)]
        private static void AutoMenu() => Debug.Log(Auto());

        [MenuItem("PoeClone/Balance/Class Gauntlet (Stop Automated)", priority = 37)]
        private static void StopMenu() => Debug.Log(StopAuto());

        [InitializeOnLoadMethod]
        private static void Install()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= Advance;
            EditorApplication.update += Advance;
        }

        /// <summary>The same balance arena and build selector as the production arena=1 entry.</summary>
        public static string Manual()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Stop Play before starting the gauntlet.";
            Launch("", arena: true);
            return "Starting the production balance test arena. Stop Play to end it; your character is never saved.";
        }

        /// <summary>
        /// Queues automated runs: each build (comma-separated ids, default the four automated builds)
        /// at each level, with <paramref name="rolls"/> rares per slot, at <paramref name="speed"/>x game speed.
        /// The same seed gives every build the same enemies and bosses.
        /// </summary>
        public static string Auto(string builds = "", string levels = "1,7,14,21,28,34", int rolls = 15, float speed = 3f, int seed = 1234,
            float distance = 0f, bool coreSkillsOnly = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Stop Play before starting automated runs.";
            var ids = string.IsNullOrWhiteSpace(builds) ? new List<string>(GauntletBuilds.AutomatedDefaults) : Split(builds);
            foreach (string id in ids)
                if (GauntletBuilds.Get(id) == null) return "unknown build '" + id + "'";
            var queue = new List<string>();
            foreach (string id in ids)
                foreach (string l in Split(levels))
                    queue.Add(id + ":" + l);
            SessionState.SetString(QueueKey, string.Join(";", queue.ToArray()));
            SessionState.SetString(RowsKey, "");
            SessionState.SetString(OptionsKey, "&rolls=" + rolls + "&speed=" + speed.ToString(CultureInfo.InvariantCulture) + "&seed=" + seed +
                "&distance=" + distance.ToString(CultureInfo.InvariantCulture) + "&coreonly=" + (coreSkillsOnly ? "1" : "0"));
            SessionState.SetString(StatusKey, "queued " + queue.Count + " runs");
            SessionState.SetBool(SummaryPendingKey, true);
            return "Queued " + queue.Count + " automated gauntlet runs (" + string.Join(", ", ids.ToArray()) + " at levels " + levels + ").";
        }

        public static string StopAuto()
        {
            SessionState.SetString(QueueKey, "");
            if (EditorApplication.isPlaying && SessionState.GetBool(ActiveKey, false)) EditorApplication.isPlaying = false;
            return "Automated gauntlet stopped; " + AutoStatus();
        }

        public static string AutoStatus()
        {
            string queue = SessionState.GetString(QueueKey, "");
            int left = queue.Length == 0 ? 0 : queue.Split(';').Length;
            GauntletMode g = GauntletMode.Instance;
            string now = g != null && g.Build != null ? g.Build.Id + " L" + g.Level + ": " + g.Status + " (" + g.Elapsed.ToString("0") + "s game time)" : "";
            return SessionState.GetString(StatusKey, "idle") + "; " + left + " queued" + (now.Length > 0 ? "; now " + now : "");
        }

        private static List<string> Split(string list)
        {
            var result = new List<string>();
            foreach (string part in list.Split(','))
                if (part.Trim().Length > 0) result.Add(part.Trim());
            return result;
        }

        private static void Launch(string extra, bool arena = false)
        {
            if (!SessionState.GetBool(ActiveKey, false))
                SessionState.SetString(PreviousQueryKey, PlayerPrefs.GetString(DevQueryKey, ""));
            SessionState.SetBool(ActiveKey, true);
            PlayerPrefs.SetString(DevQueryKey, (arena ? "?arena=1" : "?minimal=1&gauntlet=1&drops=0&quests=0") + extra);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(ActiveKey, false))
                CollectRow();
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveKey, false)) return;
            // Put the dev query back as it was before the gauntlet.
            string previous = SessionState.GetString(PreviousQueryKey, "");
            if (previous.Length > 0) PlayerPrefs.SetString(DevQueryKey, previous);
            else PlayerPrefs.DeleteKey(DevQueryKey);
            PlayerPrefs.Save();
            SessionState.SetBool(ActiveKey, false);
        }

        // ------------------------------------------------------------------ the automated queue

        private static void Advance()
        {
            string queue = SessionState.GetString(QueueKey, "");
            if (EditorApplication.isPlaying)
            {
                if (!SessionState.GetBool(ActiveKey, false)) return;
                GauntletMode g = GauntletMode.Instance;
                bool timedOut = EditorApplication.timeSinceStartup - SessionState.GetFloat(RunStartedKey, 0f) > RunTimeoutSeconds;
                if ((g != null && g.Automated && g.Finished) || (timedOut && queue.Length > 0))
                    EditorApplication.isPlaying = false;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (queue.Length == 0)
            {
                // Every queued run is done: the combined table.
                if (!SessionState.GetBool(SummaryPendingKey, false)) return;
                SessionState.SetBool(SummaryPendingKey, false);
                string path = WriteCombined();
                SessionState.SetString(StatusKey, "done: " + path);
                Debug.Log("Automated gauntlet finished: " + path);
                return;
            }

            string[] runs = queue.Split(';');
            SessionState.SetString(QueueKey, string.Join(";", runs, 1, runs.Length - 1));
            string[] run = runs[0].Split(':');
            SessionState.SetString(StatusKey, "running " + runs[0] + " (" + (runs.Length - 1) + " after it)");
            SessionState.SetFloat(RunStartedKey, (float)EditorApplication.timeSinceStartup);
            Launch("&auto=1&build=" + run[0] + "&level=" + run[1] + SessionState.GetString(OptionsKey, ""));
        }

        // One line per finished automated run, kept until the combined table is written.
        private static void CollectRow()
        {
            GauntletMode g = GauntletMode.Instance;
            if (g == null || !g.Automated || g.Recorder == null) return;
            if (!g.Finished) g.Finish(false);
            GauntletRecorder.Report d = g.Recorder.Data;
            string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            var bosses = new StringBuilder();
            foreach (GauntletRecorder.BossRecord b in d.bossFights)
                bosses.Append(b.name).Append(' ').Append(b.timedOut ? "timeout" : F(b.timeToKill) + "s/" + F(b.dps)).Append("; ");
            string row = string.Join("|", new[]
            {
                d.buildName, d.level.ToString(), d.completed ? "yes" : "NO", F(d.encounterDps),
                d.normalEnemies.oneShots + "/" + d.normalEnemies.count, F(d.normalEnemies.averageTimeToKill), F(d.normalEnemies.averageDps), F(d.normalEnemies.pooledDps),
                F(d.eliteEnemies.averageTimeToKill), F(d.eliteEnemies.averageDps),
                F(d.bosses.averageTimeToKill), F(d.bosses.averageDps), bosses.ToString().TrimEnd(' ', ';'),
                F(d.mana.maxManaAtStart), F(d.mana.poolPercentPerCombatSecond), F(d.mana.manaSpentPerKill),
                F(d.mana.averageManaPercent), d.mana.manaPotionsUsed.ToString(), F(d.mana.manaPotionsPerCombatMinute),
                d.mana.manaPotionRefills.ToString(), d.mana.castsRefusedForCost.ToString(), F(d.combatSeconds), d.buildFingerprint
            });
            string rows = SessionState.GetString(RowsKey, "");
            SessionState.SetString(RowsKey, rows + (rows.Length > 0 ? "\n" : "") + row);
        }

        private static string WriteCombined()
        {
            string[] header =
            {
                "Build", "Lvl", "Done", "Encounter DPS", "Normal one-shots", "Normal TTK s", "Normal DPS", "Normal pooled DPS", "Elite TTK s", "Elite DPS",
                "Boss TTK s", "Boss DPS", "Bosses (TTK/DPS)", "Mana pool", "Pool %/s", "Mana/kill", "Avg mana %",
                "Mana pots", "Pots/min", "Pot refills", "Refused casts", "Combat s", "Build hash"
            };
            var md = new StringBuilder();
            md.AppendLine("# Automated class gauntlet, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            md.AppendLine();
            md.AppendLine("Enemies frozen (no AI), " + SessionState.GetString(OptionsKey, "").Replace("&", " ").Trim() +
                ". TTK = first hit to death; DPS = enemy life / TTK, averaged per enemy (one-hit kills left out); pooled DPS = total life / total TTK; " +
                "encounter DPS = life of everything killed in the 20 encounters / their time from spawn to clear.");
            md.AppendLine();
            md.AppendLine("| " + string.Join(" | ", header) + " |");
            md.AppendLine("|" + string.Concat(System.Linq.Enumerable.Repeat("---|", header.Length)));
            var csv = new StringBuilder(string.Join(",", header) + "\n");
            foreach (string row in SessionState.GetString(RowsKey, "").Split('\n'))
            {
                if (row.Length == 0) continue;
                string[] cells = row.Split('|');
                md.AppendLine("| " + string.Join(" | ", cells) + " |");
                var quoted = new List<string>();
                foreach (string c in cells) quoted.Add("\"" + c + "\"");
                csv.AppendLine(string.Join(",", quoted.ToArray()));
            }
            string folder = GauntletRecorder.ResultsFolder;
            Directory.CreateDirectory(folder);
            string stem = Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "_automated_summary");
            File.WriteAllText(stem + ".md", md.ToString());
            File.WriteAllText(stem + ".csv", csv.ToString());
            return stem + ".md";
        }
    }
}

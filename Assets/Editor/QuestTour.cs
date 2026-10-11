using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PoeClone.Quests;
using PoeClone.World;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Guided objective demo for a DevTest Play session: sets the quest book up for each quest prop
    /// in story order, puts the player beside the next unfinished prop, waits for the player to
    /// finish it, then moves on five seconds later.
    /// <code>
    /// return PoeClone.EditorTools.QuestTour.Start();            // or Start("relics") to begin later
    /// return PoeClone.EditorTools.QuestTour.Status();
    /// return PoeClone.EditorTools.QuestTour.Skip();             // go to the next objective now
    /// return PoeClone.EditorTools.QuestTour.Stop();
    /// </code>
    /// </summary>
    public static class QuestTour
    {
        private static readonly string[] Order =
            { "totems", "captive", "patrol", "caravan", "relics", "deep_road", "supplies", "rite", "sunstones", "scouts", "door" };
        private const float Pause = 5f;
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static int step = -1;
        private static QuestProp target;
        private static double moveAt;       // realtime: when to go to the next objective
        private static double arriveAfter;  // realtime: area switch settling
        private static string state = "idle";

        public static string Start(string from = null)
        {
            if (!Application.isPlaying || QuestLog.Instance == null)
                return "start a DevTest Play session first";
            step = from == null ? 0 : System.Array.IndexOf(Order, from);
            if (step < 0)
                return "unknown quest " + from + "; one of " + string.Join(",", Order);
            DevTest.God();
            BeginQuest();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            return state;
        }

        public static string Status() => state;

        public static string Skip()
        {
            if (step < 0) return "not running";
            moveAt = EditorApplication.timeSinceStartup;
            return "skipping " + Describe(target);
        }

        public static string Stop()
        {
            EditorApplication.update -= Tick;
            step = -1;
            target = null;
            state = "stopped";
            return state;
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) Stop();
        }

        private static void BeginQuest()
        {
            QuestLog log = QuestLog.Instance;
            if (step > 0) MarkDone(log, Order[step - 1]);
            DevTest.Quest(Order[step]);
            target = null;
            moveAt = 0;
            NextProp();
        }

        private static void MarkDone(QuestLog log, string id)
        {
            var done = (HashSet<string>)typeof(QuestLog).GetField("done", Any).GetValue(log);
            var active = (Dictionary<string, int>)typeof(QuestLog).GetField("active", Any).GetValue(log);
            active.Remove(id);
            done.Add(id);
        }

        private static void NextProp()
        {
            QuestLog log = QuestLog.Instance;
            QuestProp next = null;
            int nextIndex = int.MaxValue;
            foreach (QuestProp prop in Object.FindObjectsByType<QuestProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (prop.QuestId != Order[step] || log.PropUsed(prop.PropId)) continue;
                int index = int.Parse(prop.PropId.Substring(prop.PropId.IndexOf('#') + 1));
                if (index < nextIndex) { next = prop; nextIndex = index; }
            }
            if (next == null || log.State(QuestBook.Get(Order[step])) != QuestState.Active)
            {
                if (++step >= Order.Length)
                {
                    MarkDone(log, Order[step - 1]);
                    Stop();
                    state = "tour finished";
                    return;
                }
                BeginQuest();
                return;
            }
            target = next;
            int area = QuestBook.Get(Order[step]).Area;
            if (AreaManager.Instance.CurrentAreaIndex != area)
            {
                AreaManager.Instance.EnterArea(area, null);
                arriveAfter = double.MaxValue;
                state = "travelling to " + Describe(target);
            }
            else
            {
                Place(target);
            }
        }

        private static void Tick()
        {
            if (step < 0 || !Application.isPlaying || QuestLog.Instance == null) return;
            double now = EditorApplication.timeSinceStartup;
            int area = QuestBook.Get(Order[step]).Area;

            if (arriveAfter == double.MaxValue)
            {
                if (AreaManager.Instance.CurrentAreaIndex == area && Time.timeScale > 0f) arriveAfter = now + 0.5;
                return;
            }
            if (arriveAfter > 0 && now >= arriveAfter)
            {
                arriveAfter = 0;
                Place(target);
                return;
            }
            if (arriveAfter > 0) return;

            if (moveAt == 0 && target != null && QuestLog.Instance.PropUsed(target.PropId))
            {
                moveAt = now + Pause;
                state = "done: " + Describe(target) + "; next objective in " + Pause + "s";
            }
            if (moveAt > 0 && now >= moveAt)
            {
                if (target != null && !QuestLog.Instance.PropUsed(target.PropId))
                    QuestLog.Instance.RecordPropStage(target.PropId); // skipped
                moveAt = 0;
                NextProp();
            }
        }

        private static void Place(QuestProp prop)
        {
            var stats = Object.FindAnyObjectByType<Player.PlayerStats>();
            int area = QuestBook.Get(prop.QuestId).Area;
            Vector3 at = prop.transform.position;
            Vector3 spot = WorldBuilder.Shape(area).NearestOpen(at + Vector3.back * 4f, 1.5f);
            spot.y = at.y + 1.1f;
            var cc = stats.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            stats.transform.position = spot;
            Vector3 face = at - spot;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) stats.transform.rotation = Quaternion.LookRotation(face);
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraSystem.CameraFollow>() : null;
            if (cam != null) cam.SnapToTarget();
            state = "at " + Describe(prop) + " (" + QuestLog.Instance.EncounterObjective(QuestBook.Get(prop.QuestId)) + ")";
        }

        private static string Describe(QuestProp prop)
        {
            return prop == null ? "-" : QuestBook.Get(prop.QuestId).Title + " " + prop.PropId;
        }
    }
}

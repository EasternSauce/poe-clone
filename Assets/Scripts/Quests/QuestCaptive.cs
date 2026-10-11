using UnityEngine;
using PoeClone.World;

namespace PoeClone.Quests
{
    /// <summary>
    /// A person held in a cage until a guarded <see cref="QuestProp"/> (the cage) is opened: silent,
    /// unlabelled and unclickable while caged, then steps out to a nearby spot and talks like anyone
    /// else. Characters who never took the rescue still see them caged; it stays available content.
    /// Built by World.WorldBuilder (QuestSites, OutpostFolk).
    /// </summary>
    public class QuestCaptive : MonoBehaviour
    {
        private string questId;
        private string cagePropId;
        private Vector3 freedAt;
        private Npc npc;
        private QuestLog log;
        private bool freed;

        public static void Attach(Npc npc, string questId, Vector3 freedAt)
        {
            QuestCaptive captive = npc.gameObject.AddComponent<QuestCaptive>();
            captive.npc = npc;
            captive.questId = questId;
            captive.cagePropId = questId + "#0";
            captive.freedAt = freedAt;
            captive.Apply(false);
        }

        private void OnDestroy()
        {
            if (log != null) log.Changed -= Refresh;
        }

        private void Update()
        {
            if (log != null || QuestLog.Instance == null || !QuestLog.Instance.enabled) return;
            log = QuestLog.Instance;
            log.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            QuestDefinition quest = QuestBook.Get(questId);
            QuestState state = quest != null ? log.State(quest) : QuestState.Locked;
            bool open = log.PropUsed(cagePropId) || state == QuestState.Complete || state == QuestState.Done;
            if (open != freed) Apply(open);
        }

        private void Apply(bool open)
        {
            freed = open;
            npc.enabled = open;
            npc.SetLabelVisible(open);
            if (open)
                transform.position = new Vector3(freedAt.x, transform.position.y, freedAt.z);
        }
    }
}

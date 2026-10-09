using UnityEngine;
using PoeClone.Audio;
using PoeClone.World;

namespace PoeClone.Quests
{
    /// <summary>
    /// A way shut until a quest opens it: a wall (the ice over the Ashen Ruins' north gate) that
    /// stands until the quest's goal is reached, then sinks into the ground, and the gate it
    /// blocks starts working. Already open on arrival for a character who opened it before.
    /// Built by World.WorldBuilder (QuestSites).
    /// </summary>
    public class QuestBarrier : MonoBehaviour
    {
        private const float SinkSeconds = 2.5f;

        private string questId;
        private AreaGate gate;
        private QuestLog log;
        private bool open;
        private float sinkStarted = -1f;
        private float height = 5f;
        private GameObject gateLabel;
        private bool gateLabelWasActive;

        public static QuestBarrier Create(GameObject wall, string questId, AreaGate gate, string sealedMessage, float height)
        {
            QuestBarrier barrier = wall.AddComponent<QuestBarrier>();
            barrier.questId = questId;
            barrier.gate = gate;
            barrier.height = height;
            if (gate != null)
            {
                gate.Locked = () => !barrier.open;
                gate.LockedMessage = sealedMessage;
                WorldLabel label = gate.GetComponentInChildren<WorldLabel>(true);
                if (label != null)
                {
                    barrier.gateLabel = label.gameObject;
                    barrier.gateLabelWasActive = label.gameObject.activeSelf;
                    label.gameObject.SetActive(false);
                }
            }
            return barrier;
        }

        private bool ShouldBeOpen()
        {
            QuestDefinition quest = QuestBook.Get(questId);
            if (log == null || quest == null)
                return false;
            QuestState state = log.State(quest);
            return state == QuestState.Complete || state == QuestState.Done;
        }

        private void OnDestroy()
        {
            if (log != null)
                log.Changed -= OnQuestsChanged;
        }

        private void RevealGate()
        {
            if (gateLabel != null)
                gateLabel.SetActive(gateLabelWasActive);
        }

        private void OnQuestsChanged()
        {
            if (!open && ShouldBeOpen())
            {
                open = true;
                sinkStarted = Time.time;
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayAtPoint(AudioManager.Instance.Sfx("shatter"), transform.position, 0.6f);
            }
        }

        private void Update()
        {
            if (log == null && QuestLog.Instance != null && QuestLog.Instance.enabled)
            {
                log = QuestLog.Instance;
                log.Changed += OnQuestsChanged;
                // Opened in an earlier visit: gone already.
                if (ShouldBeOpen())
                {
                    open = true;
                    RevealGate();
                    gameObject.SetActive(false);
                    return;
                }
            }

            if (sinkStarted < 0f)
                return;
            float t = (Time.time - sinkStarted) / SinkSeconds;
            if (t >= 1f)
            {
                RevealGate();
                gameObject.SetActive(false);
                return;
            }
            Vector3 p = transform.localPosition;
            p.y = -height * t * t;
            transform.localPosition = p;
        }
    }
}

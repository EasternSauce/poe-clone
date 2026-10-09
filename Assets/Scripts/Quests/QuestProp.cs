using System.Collections.Generic;
using UnityEngine;
using PoeClone.Audio;
using PoeClone.Enemies;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.World;

namespace PoeClone.Quests
{
    public enum PropAction
    {
        Smash,  // a totem, a reliquary: breaks apart
        Light,  // a sunstone, a brazier: catches fire and stays lit
        Take,   // a cache, a scout's body: what the quest wants is taken from it
        Read,   // an inscription: read, nothing changes
        Rite    // a ritual circle: hold it against what comes until the rite is done
    }

    /// <summary>
    /// Something out in the world a <see cref="QuestGoal.Use"/> quest has the player deal with:
    /// clicked (or tapped) like a townsperson - it's an <see cref="Npc"/> with the QuestProp role -
    /// it shows what the player finds there and what they can do about it. Using it counts towards
    /// the quest, may call an ambush down on the player, and leaves the prop changed for good
    /// (smashed, lit, emptied). Only clickable, labelled and on the minimap while its quest is under
    /// way. A rite runs for a while instead: waves come at the circle, and leaving it breaks the rite.
    /// Built by World.WorldBuilder (QuestSites).
    /// </summary>
    public class QuestProp : MonoBehaviour
    {
        public static readonly Color LabelColor = new Color(1f, 0.66f, 0.28f);

        private const float RiteSeconds = 30f;
        private const float RiteRadius = 6.5f;
        private const float RiteWaveEvery = 6f;
        private const float RiteGrace = 3f;  // seconds outside the circle before the rite breaks

        public string QuestId { get; private set; }
        public string PropId { get; private set; }

        private PropAction action;
        private string displayName;
        private string description;
        private string actionLabel;
        private string aftermath;
        private string[] ambushKinds;
        private int ambushCount;

        private GameObject whole;   // shown until used
        private GameObject spent;   // shown once used
        private Light glow;         // on once used (a lit sunstone) / during a rite
        private Npc npc;
        private QuestLog log;
        private bool used;

        // A rite under way.
        private bool riteRunning;
        private float riteStarted;
        private float nextWaveAt;
        private float outsideSince = -1f;
        private int wave;
        private PlayerStats player;

        public static QuestProp Attach(GameObject go, string questId, int index, PropAction action, string displayName,
            string description, string actionLabel, string aftermath, float labelHeight,
            string[] ambushKinds = null, int ambushCount = 0)
        {
            QuestProp prop = go.AddComponent<QuestProp>();
            prop.QuestId = questId;
            prop.PropId = questId + "#" + index;
            prop.action = action;
            prop.displayName = displayName;
            prop.description = description;
            prop.actionLabel = actionLabel;
            prop.aftermath = aftermath;
            prop.ambushKinds = ambushKinds;
            prop.ambushCount = ambushCount;
            prop.npc = Npc.CreateFixed(go, NpcRole.QuestProp, displayName, labelHeight);
            prop.npc.Rename(displayName, LabelColor);
            return prop;
        }

        /// <summary>The parts that change: what stands there before, what's left after, and its light.</summary>
        public void SetLooks(GameObject before, GameObject after, Light light)
        {
            whole = before;
            spent = after;
            glow = light;
            Refresh();
        }

        private QuestDefinition Quest => QuestBook.Get(QuestId);

        private void Start()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            if (log != null)
                log.Changed -= Refresh;
        }

        private void Refresh()
        {
            QuestDefinition quest = Quest;
            QuestState state = log != null && quest != null ? log.State(quest) : QuestState.Locked;
            used = log != null && (log.PropUsed(PropId) || state == QuestState.Complete || state == QuestState.Done);
            bool usable = state == QuestState.Active && !used && !riteRunning;

            if (npc != null)
            {
                npc.enabled = usable;
                npc.SetLabelVisible(usable || riteRunning);
            }
            if (whole != null)
                whole.SetActive(!used || action == PropAction.Read || action == PropAction.Light || action == PropAction.Rite);
            if (spent != null)
                spent.SetActive(used);
            if (glow != null)
                glow.enabled = riteRunning || (used && (action == PropAction.Light || action == PropAction.Rite));
        }

        private void Update()
        {
            if (log == null && QuestLog.Instance != null && QuestLog.Instance.enabled)
            {
                log = QuestLog.Instance;
                log.Changed += Refresh;
                Refresh();
            }

            if (riteRunning)
                UpdateRite();
        }

        // ------------------------------------------------------------------ using it

        /// <summary>The player reached it (see NpcDialogues.Open): what's there, and what to do.</summary>
        public void Interact(Npc speaker)
        {
            if (used || riteRunning)
                return;
            var options = new List<DialogueOption>
            {
                new DialogueOption("<b>" + actionLabel + "</b>", Use),
                new DialogueOption("Leave it for now", DialogueUI.Close)
            };
            DialogueUI.Show(speaker, description, options);
        }

        private void Use()
        {
            DialogueUI.Close();
            QuestDefinition quest = Quest;
            if (log == null || quest == null || log.State(quest) != QuestState.Active)
                return;

            if (action == PropAction.Rite)
            {
                StartRite();
                return;
            }

            log.UseProp(quest, PropId);
            Effect();
            if (!string.IsNullOrEmpty(aftermath))
                CombatText.Show(transform.position + Vector3.up * 2.6f, aftermath, LabelColor, 0.8f);
            Ambush(ambushCount);
        }

        private void Effect()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null)
                return;
            switch (action)
            {
                case PropAction.Smash:
                    audio.PlayAtPoint(audio.Sfx("shatter"), transform.position);
                    break;
                case PropAction.Light:
                    audio.PlayAtPoint(audio.Sfx("corpse_explosion"), transform.position, 1.3f);
                    break;
                case PropAction.Take:
                    audio.PlayUI(audio.uiItemPickup);
                    break;
                default:
                    audio.PlayUI(audio.Sfx("ui_click"));
                    break;
            }
        }

        private void Ambush(int count)
        {
            if (count <= 0 || ambushKinds == null || ambushKinds.Length == 0)
                return;
            QuestDefinition quest = Quest;
            EnemySpawner spawner = WorldBuilder.Instance != null && quest != null ? WorldBuilder.Instance.SpawnerFor(quest.Area) : null;
            if (spawner == null)
                return;
            for (int k = 0; k < count; k++)
                spawner.SpawnAmbush(transform.position, ambushKinds[Random.Range(0, ambushKinds.Length)], 1);
        }

        // ------------------------------------------------------------------ rites

        private void StartRite()
        {
            riteRunning = true;
            riteStarted = Time.time;
            nextWaveAt = Time.time + 2f;
            outsideSince = -1f;
            wave = 0;
            Refresh();
            CombatText.Show(transform.position + Vector3.up * 2.6f, "The rite begins - stay in the circle!", LabelColor, 0.9f);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayAtPoint(AudioManager.Instance.Sfx("corpse_explosion"), transform.position, 0.7f);
        }

        private void UpdateRite()
        {
            if (player == null)
                player = FindAnyObjectByType<PlayerStats>();
            if (player == null)
                return;

            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            bool inside = offset.magnitude <= RiteRadius;
            if (inside)
                outsideSince = -1f;
            else if (outsideSince < 0f)
                outsideSince = Time.time;

            if (player.IsDead || (outsideSince >= 0f && Time.time - outsideSince > RiteGrace))
            {
                EndRite(false);
                return;
            }

            float t = Mathf.Clamp01((Time.time - riteStarted) / RiteSeconds);
            if (npc != null)
                npc.Rename(inside ? "The rite: " + Mathf.RoundToInt(t * 100f) + "%" : "Back into the circle!",
                    inside ? Color.Lerp(LabelColor, Color.white, t) : new Color(1f, 0.4f, 0.3f));
            if (glow != null)
            {
                glow.color = Color.Lerp(new Color(1f, 0.45f, 0.15f), Color.white, t);
                glow.intensity = 3f + 5f * t;
            }

            if (Time.time >= nextWaveAt && t < 0.9f)
            {
                nextWaveAt = Time.time + RiteWaveEvery;
                wave++;
                Ambush(Mathf.Min(1 + wave, 3));
            }

            if (t >= 1f)
                EndRite(true);
        }

        private void EndRite(bool success)
        {
            riteRunning = false;
            if (npc != null)
                npc.Rename(displayName, LabelColor);
            QuestDefinition quest = Quest;
            if (success && log != null && quest != null)
            {
                log.UseProp(quest, PropId);
                CombatText.Show(transform.position + Vector3.up * 2.6f, aftermath, Color.white, 1f);
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayUI(AudioManager.Instance.playerLevelUp);
            }
            else
            {
                CombatText.Show(transform.position + Vector3.up * 2.6f, "The rite is broken - begin it again", new Color(1f, 0.4f, 0.3f), 0.9f);
            }
            if (glow != null)
                glow.color = Color.white;
            Refresh();
        }
    }
}

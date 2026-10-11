using System;
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
    /// it shows what the player finds there and what they can do about it. Clearing its encounter
    /// counts towards the quest and leaves the prop changed for good
    /// (smashed, lit, emptied). Only clickable, labelled and on the minimap while its quest is under
    /// way. A rite runs for a while instead: waves come at the circle, and leaving it breaks the rite.
    /// Built by World.WorldBuilder (QuestSites).
    /// </summary>
    public class QuestProp : MonoBehaviour
    {
        public static readonly Color LabelColor = new Color(1f, 0.66f, 0.28f);

        private const float RiteSeconds = 30f;
        private const float RiteRadius = 6.5f;
        private const float RiteWaveEvery = 2f;
        private const int RiteEnemyCap = 12;
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
        private bool sequential;
        private int index;
        private bool guarded;
        private Vector3 guardSpawnPoint;
        private string guardObjective;
        private string carrierName;
        private EnemyHealth attackTarget;
        private int queuedWaves;
        private float nextGroupAt;

        private bool StageCleared => log != null && log.PropUsed(PropId + ":guards");
        private bool CurrentStage => !sequential || index == 0 || (log != null && log.PropUsed(QuestId + "#" + (index - 1)));

        // An encounter under way. Only completed props are saved; unfinished encounters restart.
        private bool riteRunning;
        private float riteStarted;
        private float nextWaveAt;
        private float outsideSince = -1f;
        private bool encounterRunning;
        private int pendingEnemies;
        private int spawnedEnemies;
        private float nextSpawnAt;
        private bool combatStarted;

        private sealed class EncounterEnemy
        {
            public EnemyHealth Health;
            public Action Died;
            public bool Defeated;
        }

        private readonly List<EncounterEnemy> enemies = new List<EncounterEnemy>();
        private readonly List<EnemyHealth> spawned = new List<EnemyHealth>();
        private PlayerStats player;

        public static QuestProp Attach(GameObject go, string questId, int index, PropAction action, string displayName,
            string description, string actionLabel, string aftermath, float labelHeight,
            string[] ambushKinds = null, int ambushCount = 0)
        {
            QuestProp prop = go.AddComponent<QuestProp>();
            prop.QuestId = questId;
            prop.index = index;
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

        public void SetSequence() { sequential = true; }

        /// <summary>Station guards on quest acceptance. Clearing them enables a separate final interaction.</summary>
        public void SetGuarded(Vector3 spawnPoint, string objective, string namedCarrier = null)
        {
            guarded = true;
            guardSpawnPoint = spawnPoint;
            guardObjective = objective;
            carrierName = namedCarrier;
        }

        public void SetAttackTarget(EnemyHealth target)
        {
            attackTarget = target;
            target.IsQuestObject = true;
            target.Died += Use;
            Refresh();
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
            if (attackTarget != null) attackTarget.Died -= Use;
            ClearEncounter();
        }

        private void Refresh()
        {
            QuestDefinition quest = Quest;
            QuestState state = log != null && quest != null ? log.State(quest) : QuestState.Locked;
            used = log != null && (log.PropUsed(PropId) || state == QuestState.Complete || state == QuestState.Done);
            bool activeStage = state == QuestState.Active && !used && CurrentStage;
            bool usable = activeStage && !encounterRunning && (!guarded || StageCleared);
            if (attackTarget != null)
            {
                attackTarget.enabled = activeStage && !encounterRunning;
                attackTarget.Immune = !activeStage || encounterRunning;
            }

            if (npc != null)
            {
                npc.enabled = (usable || encounterRunning) && (attackTarget == null || attackTarget.IsDead);
                npc.SetLabelVisible(activeStage || encounterRunning);
            }
            bool changed = used || (encounterRunning && !guarded);
            if (whole != null)
                whole.SetActive(!changed || action == PropAction.Read || action == PropAction.Light || action == PropAction.Rite);
            if (spent != null)
                spent.SetActive(changed);
            if (glow != null)
                glow.enabled = (used || encounterRunning) && (action == PropAction.Light || action == PropAction.Rite);
            if (!encounterRunning && log != null)
            {
                string objective = activeStage && (sequential || guarded || attackTarget != null)
                    ? (guarded && StageCleared ? actionLabel : attackTarget != null ? "Attack " + displayName : actionLabel) + " - " + QuestBook.Location(quest)
                    : null;
                log.SetEncounterObjective(PropId, objective);
            }
            EnsureGuards();
        }

        private void EnsureGuards()
        {
            if (!guarded || used || encounterRunning || StageCleared || !CurrentStage || log == null || Quest == null ||
                log.State(Quest) != QuestState.Active) return;
            if (player == null) player = log.GetComponent<PlayerStats>();
            if (player == null || player.IsDead) return;
            BeginEncounter();
        }

        private void Update()
        {
            if (log == null && QuestLog.Instance != null && QuestLog.Instance.enabled)
            {
                log = QuestLog.Instance;
                log.Changed += Refresh;
                Refresh();
            }

            EnsureGuards();
            if (encounterRunning) UpdateEncounter();
        }

        // ------------------------------------------------------------------ using it

        /// <summary>The player reached it (see NpcDialogues.Open): what's there, and what to do.</summary>
        public void Interact(Npc speaker)
        {
            if (used || encounterRunning || !CurrentStage || (guarded && !StageCleared) ||
                (attackTarget != null && !attackTarget.IsDead)) return;
            var options = new List<DialogueOption>
            {
                new DialogueOption("<b>" + actionLabel + "</b>", Use),
                new DialogueOption("Leave it for now", DialogueUI.Close)
            };
            string warning = action == PropAction.Rite
                ? "\n\nHold the circle for 30 seconds, then defeat all remaining enemies. Leaving during the rite or dying resets this encounter."
                : guarded ? "\n\nThe assigned guards are defeated. Finish this interaction to secure the objective."
                : ambushCount > 0 ? "\n\nDefeat every enemy this draws to secure the objective. In Greenwood they arrive in groups of three; clear each group before the next. Dying or leaving the area resets an unfinished encounter." : "";
            DialogueUI.Show(speaker, description + warning, options);
        }

        private void Use()
        {
            DialogueUI.Close();
            QuestDefinition quest = Quest;
            if (used || encounterRunning || !CurrentStage || (guarded && !StageCleared) ||
                (attackTarget != null && !attackTarget.IsDead) || log == null || quest == null || log.State(quest) != QuestState.Active)
                return;

            if (!guarded && (action == PropAction.Rite || ambushCount > 0))
            {
                BeginEncounter();
                Effect();
                ShowMessage(riteRunning ? "The rite begins - hold the circle!" : aftermath);
                return;
            }

            Effect();
            log.UseProp(quest, PropId);
            ShowMessage(aftermath);
        }

        private void BeginEncounter()
        {
            encounterRunning = true;
            riteRunning = action == PropAction.Rite;
            riteStarted = Time.time;
            nextWaveAt = Time.time;
            nextSpawnAt = Time.time;
            outsideSince = -1f;
            combatStarted = false;
            bool waves = !guarded && !riteRunning && Quest.Area == WorldBuilder.Greenwood;
            pendingEnemies = riteRunning ? 0 : waves ? Mathf.Min(3, ambushCount) : ambushCount;
            queuedWaves = waves ? ambushCount - pendingEnemies : 0;
            spawnedEnemies = 0;
            Refresh();
            SpawnPending();
            PublishObjective();
        }

        private void ShowMessage(string message)
        {
            if (!string.IsNullOrEmpty(message))
                CombatText.Show(transform.position + Vector3.up * 2.6f, message, LabelColor, 0.9f);
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

        // Only these enemies belong to this prop. Area kills cannot substitute for them.
        private int LivingEnemies
        {
            get
            {
                int living = 0;
                foreach (EncounterEnemy enemy in enemies)
                    if (!enemy.Defeated) living++;
                return living;
            }
        }

        private void SpawnPending()
        {
            if (pendingEnemies <= 0 || Time.time < nextSpawnAt) return;
            nextSpawnAt = Time.time + 1f;
            QuestDefinition quest = Quest;
            EnemySpawner spawner = WorldBuilder.Instance != null && quest != null ? WorldBuilder.Instance.SpawnerFor(quest.Area) : null;
            if (spawner == null || ambushKinds == null || ambushKinds.Length == 0) return;

            // Failed placement remains pending and is retried; an empty spawn never completes a quest.
            int attempts = pendingEnemies;
            for (int k = 0; k < attempts; k++)
            {
                spawned.Clear();
                string kind = ambushKinds[spawnedEnemies % ambushKinds.Length];
                spawner.SpawnAmbush(guarded ? guardSpawnPoint : transform.position, kind, 1,
                    guarded ? 2f : riteRunning ? 8f : 5f, guarded ? 7f : 13f, spawned, alert: !guarded);
                foreach (EnemyHealth health in spawned)
                {
                    if (spawnedEnemies == 0 && !string.IsNullOrEmpty(carrierName)) health.BossName = carrierName;
                    var enemy = new EncounterEnemy { Health = health };
                    enemy.Died = () => enemy.Defeated = true;
                    health.Died += enemy.Died;
                    enemies.Add(enemy);
                    spawnedEnemies++;
                    pendingEnemies--;
                    combatStarted = true;
                }
            }
        }

        private void UpdateEncounter()
        {
            if (player == null && log != null) player = log.GetComponent<PlayerStats>();
            QuestDefinition quest = Quest;
            if (player == null || quest == null) return;
            bool leftArea = AreaManager.Instance != null && AreaManager.Instance.CurrentAreaIndex != quest.Area;
            if ((player.IsDead && (!guarded || !leftArea)) || (leftArea && !guarded) || log.State(quest) != QuestState.Active)
            {
                ResetEncounter(leftArea ? null : "Encounter reset - try again when you are ready");
                return;
            }

            // Destruction without a death event is not a kill. Reset so missing enemies cannot softlock a quest.
            foreach (EncounterEnemy enemy in enemies)
                if (!enemy.Defeated && enemy.Health == null)
                {
                    ResetEncounter("Encounter interrupted - begin again");
                    return;
                }

            if (riteRunning)
            {
                Vector3 offset = player.transform.position - transform.position;
                offset.y = 0f;
                bool inside = offset.sqrMagnitude <= RiteRadius * RiteRadius;
                if (inside) outsideSince = -1f;
                else if (outsideSince < 0f) outsideSince = Time.time;
                if (outsideSince >= 0f && Time.time - outsideSince > RiteGrace)
                {
                    ResetEncounter("The rite is broken - begin it again");
                    return;
                }

                // Start the clock only once the first wave actually finds room to spawn.
                if (!combatStarted) riteStarted = Time.time;
                float elapsed = Time.time - riteStarted;
                if (elapsed < RiteSeconds && Time.time >= nextWaveAt)
                {
                    nextWaveAt = Time.time + RiteWaveEvery;
                    pendingEnemies += Mathf.Min(3, Mathf.Max(0, RiteEnemyCap - LivingEnemies - pendingEnemies));
                }
                SpawnPending();
                if (glow != null)
                {
                    float t = Mathf.Clamp01(elapsed / RiteSeconds);
                    glow.color = Color.Lerp(new Color(1f, 0.45f, 0.15f), Color.white, t);
                    glow.intensity = 3f + 5f * t;
                }
                if (elapsed >= RiteSeconds && combatStarted)
                {
                    riteRunning = false;
                    outsideSince = -1f;
                    ShowMessage("The rite is held - defeat the remaining enemies!");
                }
            }
            else
            {
                if (pendingEnemies == 0 && LivingEnemies == 0 && queuedWaves > 0)
                {
                    if (nextGroupAt <= 0f) nextGroupAt = Time.time + 1.5f;
                    if (Time.time >= nextGroupAt)
                    {
                        pendingEnemies = Mathf.Min(3, queuedWaves);
                        queuedWaves -= pendingEnemies;
                        nextGroupAt = 0f;
                    }
                }
                SpawnPending();
            }

            if (!riteRunning && queuedWaves == 0 && pendingEnemies == 0 && LivingEnemies == 0 && combatStarted)
            {
                CompleteEncounter();
                return;
            }
            PublishObjective();
        }

        private void PublishObjective()
        {
            int remaining = LivingEnemies + pendingEnemies + queuedWaves;
            string stage;
            if (riteRunning)
            {
                int seconds = Mathf.CeilToInt(Mathf.Max(0f, RiteSeconds - (Time.time - riteStarted)));
                stage = "Hold " + displayName + " (" + seconds + "s; " + remaining + " enemies)";
                string label = outsideSince >= 0f ? "Back into the circle!" : displayName + ": " + seconds + "s";
                if (npc != null && npc.DisplayName != label)
                    npc.Rename(label, outsideSince >= 0f ? new Color(1f, 0.4f, 0.3f) : LabelColor);
            }
            else
            {
                stage = (guarded ? guardObjective : "Defeat " + displayName + "'s summoned enemies") + " (" + remaining + " remaining)";
                if (queuedWaves > 0) stage += "; groups of 3";
                string label = displayName + ": " + remaining + " enemies";
                if (npc != null && npc.DisplayName != label) npc.Rename(label, LabelColor);
            }
            log.SetEncounterObjective(PropId, stage + " - " + QuestBook.Location(Quest));
        }

        private void CompleteEncounter()
        {
            encounterRunning = false;
            riteRunning = false;
            ReleaseEnemies(false);
            // Award only once, after the timer (if any), successful spawning and actual deaths.
            if (guarded) log.RecordPropStage(PropId + ":guards");
            else log.UseProp(Quest, PropId);
            if (!guarded) log.SetEncounterObjective(PropId, null);
            if (npc != null) npc.Rename(displayName, LabelColor);
            if (glow != null) glow.color = Color.white;
            Refresh();
            ShowMessage(guarded ? "Guards defeated - " + actionLabel : action == PropAction.Rite ? "The ward is broken!" : "Encounter cleared - objective secured");
        }

        private void ResetEncounter(string message)
        {
            ClearEncounter();
            if (attackTarget != null) attackTarget.RestoreQuestObject();
            if (npc != null) npc.Rename(displayName, LabelColor);
            if (glow != null) glow.color = Color.white;
            Refresh();
            ShowMessage(message);
        }

        private void ClearEncounter()
        {
            encounterRunning = false;
            riteRunning = false;
            pendingEnemies = 0;
            queuedWaves = 0;
            nextGroupAt = 0f;
            ReleaseEnemies(true);
            if (log != null) log.SetEncounterObjective(PropId, null);
        }

        private void ReleaseEnemies(bool removeLiving)
        {
            foreach (EncounterEnemy enemy in enemies)
            {
                if (enemy.Health == null) continue;
                enemy.Health.Died -= enemy.Died;
                if (removeLiving && !enemy.Defeated)
                {
                    enemy.Health.gameObject.SetActive(false);
                    Destroy(enemy.Health.gameObject);
                }
            }
            enemies.Clear();
        }
    }
}

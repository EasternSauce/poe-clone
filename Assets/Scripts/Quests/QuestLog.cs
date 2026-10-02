using System;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;
using PoeClone.World;

namespace PoeClone.Quests
{
    /// <summary>
    /// The player's quests: which are taken, how far along they are, and handing them in for the
    /// reward. Kills (bosses too) come from <see cref="KillRewards.EnemyKilled"/>, arrivals
    /// from the <see cref="AreaManager"/>. Self-added by
    /// <see cref="PlayerController"/>; lives for the session (nothing is saved).
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class QuestLog : MonoBehaviour
    {
        public static QuestLog Instance { get; private set; }

        private readonly HashSet<string> done = new HashSet<string>();
        private readonly Dictionary<string, int> active = new Dictionary<string, int>(); // id -> progress
        private readonly HashSet<int> visited = new HashSet<int>();

        private PlayerStats stats;
        private AreaManager areas;

        /// <summary>Any quest taken, advanced, completed or handed in.</summary>
        public event Action Changed;

        private void Awake()
        {
            Instance = this;
            stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            KillRewards.EnemyKilled += OnEnemyKilled;
        }

        private void OnDisable()
        {
            KillRewards.EnemyKilled -= OnEnemyKilled;
        }

        private void OnDestroy()
        {
            if (areas != null)
                areas.AreaChanged -= OnAreaChanged;
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (areas == null && AreaManager.Instance != null)
            {
                areas = AreaManager.Instance;
                areas.AreaChanged += OnAreaChanged;
                if (areas.CurrentAreaIndex >= 0)
                    OnAreaChanged(areas.CurrentAreaIndex);
            }
        }

        // ------------------------------------------------------------------ state

        public QuestState State(QuestDefinition quest)
        {
            if (done.Contains(quest.Id))
                return QuestState.Done;
            if (active.TryGetValue(quest.Id, out int progress))
                return progress >= quest.Count ? QuestState.Complete : QuestState.Active;
            if (quest.After == null || done.Contains(quest.After))
                return QuestState.Available;
            return QuestState.Locked;
        }

        /// <summary>For saving: what's done, what's under way (and how far), where the player has been.</summary>
        public void Export(List<string> doneIds, Dictionary<string, int> activeProgress, List<int> visitedAreas)
        {
            doneIds.AddRange(done);
            foreach (var pair in active)
                activeProgress[pair.Key] = pair.Value;
            visitedAreas.AddRange(visited);
        }

        /// <summary>A saved character's quests (unknown quest ids are skipped).</summary>
        public void Import(IEnumerable<string> doneIds, IEnumerable<KeyValuePair<string, int>> activeProgress, IEnumerable<int> visitedAreas)
        {
            foreach (string id in doneIds)
            {
                if (QuestBook.Get(id) != null)
                    done.Add(id);
            }
            foreach (var pair in activeProgress)
            {
                if (QuestBook.Get(pair.Key) != null && !done.Contains(pair.Key))
                    active[pair.Key] = pair.Value;
            }
            foreach (int area in visitedAreas)
                visited.Add(area);
            Changed?.Invoke();
        }

        /// <summary>Been to this area this session (waystones only go where the player has been).</summary>
        public bool HasVisited(int area)
        {
            return visited.Contains(area);
        }

        public int Progress(QuestDefinition quest)
        {
            return active.TryGetValue(quest.Id, out int progress) ? Mathf.Min(progress, quest.Count) : 0;
        }

        /// <summary>
        /// What this NPC has for the player right now, most pressing first: a finished quest to hand
        /// in, one under way, or a new one to offer. Null if they have nothing.
        /// </summary>
        public QuestDefinition CurrentFrom(NpcRole giver)
        {
            QuestDefinition underWay = null;
            QuestDefinition offer = null;
            foreach (QuestDefinition q in QuestBook.All)
            {
                if (q.Giver != giver)
                    continue;
                QuestState s = State(q);
                if (s == QuestState.Complete)
                    return q;
                if (s == QuestState.Active && underWay == null)
                    underWay = q;
                else if (s == QuestState.Available && offer == null)
                    offer = q;
            }
            return underWay ?? offer;
        }

        /// <summary>The quests taken and not handed in yet, in book order.</summary>
        public List<QuestDefinition> Taken()
        {
            var list = new List<QuestDefinition>();
            foreach (QuestDefinition q in QuestBook.All)
            {
                if (active.ContainsKey(q.Id))
                    list.Add(q);
            }
            return list;
        }

        // ------------------------------------------------------------------ actions

        public void Accept(QuestDefinition quest)
        {
            if (State(quest) != QuestState.Available)
                return;

            // Somewhere already been counts straight away.
            active[quest.Id] = quest.Goal == QuestGoal.ReachArea && visited.Contains(quest.Area) ? quest.Count : 0;
            Changed?.Invoke();
        }

        /// <summary>Hands a finished quest in; returns the reward lines to show ("+60 gold", ...).</summary>
        public List<string> HandIn(QuestDefinition quest)
        {
            var lines = new List<string>();
            if (State(quest) != QuestState.Complete)
                return lines;

            active.Remove(quest.Id);
            done.Add(quest.Id);
            GrantRewards(quest, lines);

            // A bounty board starts over once its last bounty is in.
            if (quest.Repeatable && !HasFollowUp(quest))
            {
                foreach (QuestDefinition q in QuestBook.All)
                {
                    if (q.Repeatable && q.Giver == quest.Giver)
                        done.Remove(q.Id);
                }
            }

            Changed?.Invoke();
            return lines;
        }

        private static bool HasFollowUp(QuestDefinition quest)
        {
            foreach (QuestDefinition q in QuestBook.All)
            {
                if (q.After == quest.Id)
                    return true;
            }
            return false;
        }

        private void GrantRewards(QuestDefinition quest, List<string> lines)
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (quest.RewardGold > 0 && inventory != null)
            {
                inventory.AddGold(quest.RewardGold);
                lines.Add("+" + quest.RewardGold + " gold");
            }

            if (quest.RewardExperience > 0)
            {
                stats.GainExperience(quest.RewardExperience);
                lines.Add("+" + quest.RewardExperience + " experience");
            }

            PlayerPotions potions = GetComponent<PlayerPotions>();
            if (quest.RewardHealthPotions > 0 && potions != null)
            {
                int got = potions.Add(health: true, quest.RewardHealthPotions);
                if (got > 0)
                    lines.Add("+" + got + " Health Potion" + (got > 1 ? "s" : ""));
            }

            if (quest.RewardItem != null && inventory != null)
            {
                var rng = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));
                ItemData item = ItemGenerator.Generate(rng, quest.RewardItemLevel, quest.RewardItem.Value);
                if (item != null)
                {
                    GiveItem(inventory, item);
                    lines.Add("<color=#" + UiKit.Hex(UiKit.RarityColor(item.Rarity)) + ">" + item.Name + "</color>");
                }
            }
        }

        /// <summary>Into the bag if there's room, otherwise onto the ground at the player's feet.</summary>
        public static void GiveItem(PlayerInventory inventory, ItemData item)
        {
            if (!inventory.Grid.TryAutoPlace(item))
                inventory.ThrowAway(item);
        }

        // ------------------------------------------------------------------ progress

        private void OnEnemyKilled(EnemyKind kind, int monsterLevel)
        {
            int area = areas != null ? areas.CurrentAreaIndex : -1;
            Advance(q =>
                (q.Goal == QuestGoal.KillInArea && (q.Area < 0 || q.Area == area)) ||
                (q.Goal == QuestGoal.KillKind && q.Target == kind.Name) ||
                (q.Goal == QuestGoal.KillBoss && q.Target == kind.Name));
        }

        private void OnAreaChanged(int index)
        {
            visited.Add(index);
            Advance(q => q.Goal == QuestGoal.ReachArea && q.Area == index);
        }

        private readonly List<string> scratch = new List<string>();

        private void Advance(Predicate<QuestDefinition> counts)
        {
            scratch.Clear();
            scratch.AddRange(active.Keys);
            bool changed = false;
            foreach (string id in scratch)
            {
                QuestDefinition q = QuestBook.Get(id);
                int progress = active[id];
                if (q == null || progress >= q.Count || !counts(q))
                    continue;

                active[id] = progress + 1;
                changed = true;
                if (progress + 1 >= q.Count)
                    Announce(q);
            }

            if (changed)
                Changed?.Invoke();
        }

        private void Announce(QuestDefinition quest)
        {
            Npc giver = Npc.Find(quest.Giver);
            string who = giver != null ? giver.DisplayName : quest.Giver.ToString();
            CombatText.Show(transform.position + Vector3.up * 2.4f, quest.Title + " complete - return to " + who,
                new Color(1f, 0.85f, 0.35f), 0.9f);
            if (Audio.AudioManager.Instance != null)
                Audio.AudioManager.Instance.PlayUI(Audio.AudioManager.Instance.playerLevelUp, 0.35f);
        }
    }
}

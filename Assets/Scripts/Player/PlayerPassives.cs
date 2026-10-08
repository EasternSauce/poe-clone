using System;
using UnityEngine;
using PoeClone.Inventory;

namespace PoeClone.Player
{
    /// <summary>
    /// The player's passive tree: one point per level after the first, spent in
    /// <see cref="UI.PassiveTreeUI"/>; the passives' stats are counted with the gear's
    /// (<see cref="PlayerInventory.SetPassiveModifiers"/>). Self-added by <see cref="PlayerController"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerPassives : MonoBehaviour
    {
        public PassiveAllocation Allocation { get; } = new PassiveAllocation();

        private PlayerStats stats;
        private PlayerInventory inventory;

        /// <summary>Points changed: a passive taken or given back, or a level gained.</summary>
        public event Action Changed;

        public int Level => stats != null ? stats.Level : 1;
        public int Unspent => Allocation.Unspent(Level);
        public int RegretOrbs => inventory != null && inventory.Grid != null ? inventory.Grid.CountItem(ItemData.RegretId) : 0;

        public int RefundCost(PassiveAllocation draft)
        {
            int count = 0;
            if (draft != null)
                foreach (string id in Allocation.Taken)
                    if (id != PassiveTree.OriginId && !draft.Has(id)) count++;
            return count;
        }

        /// <summary>Full tree resets left. Starts at 1; more are earned from major quests.</summary>
        public int RespecCharges { get; private set; } = 1;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            inventory = GetComponent<PlayerInventory>();
            Allocation.Changed += Apply;
        }

        private void OnEnable()
        {
            stats.LeveledUp += OnLeveledUp;
        }

        private void OnDisable()
        {
            stats.LeveledUp -= OnLeveledUp;
        }

        private void OnLeveledUp(int level)
        {
            Changed?.Invoke();
        }

        public bool Take(string id) => Allocation.Take(id, Level);

        /// <summary>Restore bought passives, including paths predating added travel nodes.</summary>
        public void Restore(System.Collections.Generic.IList<string> ids)
        {
            Allocation.RestoreSaved(ids, Level);
        }

        public bool Refund(string id) => Allocation.Refund(id);

        /// <summary>Spends a respec charge to clear the whole tree; false (nothing spent) if there's
        /// nothing to reset or no charges left.</summary>
        public bool ResetAll()
        {
            if (RespecCharges <= 0 || Allocation.Spent <= 0)
                return false;
            RespecCharges--;
            Allocation.ResetAll();
            return true;
        }

        /// <summary>Apply a reviewed draft once, charging a reset only on confirmation.</summary>
        public bool ConfirmDraft(PassiveAllocation draft, bool reset, bool respec = false)
        {
            if (draft == null || draft.Spent > PassiveAllocation.PointsForLevel(Level) ||
                (reset && RespecCharges <= 0)) return false;
            // Validate new purchases against the current graph; keep legacy saved nodes intact.
            var check = new PassiveAllocation();
            if (!reset) check.RestoreSaved(new System.Collections.Generic.List<string>(Allocation.Taken), Level);
            int cost = reset ? 0 : RefundCost(draft);
            if (cost > 0 && (!respec || RegretOrbs < cost)) return false;
            // Replay removals to reject drafts that would sever an existing connected path.
            var removals = new System.Collections.Generic.List<string>();
            foreach (string id in check.Taken) if (!draft.Has(id)) removals.Add(id);
            while (removals.Count > 0)
            {
                bool progress = false;
                for (int i = removals.Count - 1; i >= 0; i--)
                    if (check.Refund(removals[i])) { removals.RemoveAt(i); progress = true; }
                if (!progress) return false;
            }
            var remaining = new System.Collections.Generic.List<string>();
            foreach (string id in draft.Taken) if (!check.Has(id)) remaining.Add(id);
            while (remaining.Count > 0)
            {
                bool progress = false;
                for (int i = remaining.Count - 1; i >= 0; i--)
                    if (check.Take(remaining[i], Level)) { remaining.RemoveAt(i); progress = true; }
                if (!progress) return false;
            }
            if (reset) RespecCharges--;
            if (cost > 0 && !inventory.Grid.TryConsume(ItemData.RegretId, cost)) return false;
            Allocation.ReplaceWith(draft.Taken);
            return true;
        }

        /// <summary>A major quest's reward: more full respecs.</summary>
        public void GrantRespec(int amount)
        {
            RespecCharges += amount;
            Changed?.Invoke();
        }

        /// <summary>Restoring a save.</summary>
        public void SetRespecCharges(int value) => RespecCharges = Mathf.Max(0, value);

        private void Apply()
        {
            if (inventory != null)
                inventory.SetPassiveModifiers(Allocation.Modifiers());
            Changed?.Invoke();
        }
    }
}

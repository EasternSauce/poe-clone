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

        /// <summary>A saved character's passives (in any order: each pass takes what connects).</summary>
        public void Restore(System.Collections.Generic.IList<string> ids)
        {
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (string id in ids)
                {
                    if (!Allocation.Has(id) && Allocation.Take(id, Level))
                        progress = true;
                }
            }
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

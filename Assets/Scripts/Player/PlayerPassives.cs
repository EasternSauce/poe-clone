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
        public bool Refund(string id) => Allocation.Refund(id);
        public void ResetAll() => Allocation.ResetAll();

        private void Apply()
        {
            if (inventory != null)
                inventory.SetPassiveModifiers(Allocation.Modifiers());
            Changed?.Invoke();
        }
    }
}

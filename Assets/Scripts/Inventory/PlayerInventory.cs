using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Inventory
{
    /// <summary>
    /// Holds the player's bag (a 12x5 grid, empty at the start: the first gear lies on the ground,
    /// see StarterLoot), the stash in Haven (a 12x12 grid, opened at its chest), worn equipment,
    /// and the resulting stat sheet.
    /// Whatever drives the character's base stats calls <see cref="SetBaseStats"/>; the sheet is rebuilt
    /// whenever that changes or gear is equipped/unequipped, and <see cref="StatsChanged"/> fires.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private int gridWidth = 12;
        [SerializeField] private int gridHeight = 5;

        private BaseStats baseStats = new BaseStats();

        public const int StashSize = 12;

        public InventoryGrid Grid { get; private set; }
        public InventoryGrid Stash { get; private set; }
        public EquipmentSet Equipment { get; private set; }
        public StatSheet Stats { get; private set; }

        // PlayerStats lives in a different assembly (this one compiles first and can't reference
        // it), so PlayerStatsLink forwards the death notification here -- the same bridging
        // pattern it already uses for base stats flowing in and gear bonuses flowing out.
        public bool IsPlayerDead { get; private set; }

        public event Action StatsChanged;

        /// <summary>Gold in the purse (kills pay it, the merchant takes it).</summary>
        public int Gold { get; private set; }

        public event Action GoldChanged;

        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;
            Gold += amount;
            GoldChanged?.Invoke();
        }

        /// <summary>Pays gold if there's enough; false (nothing taken) otherwise.</summary>
        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount)
                return false;
            Gold -= amount;
            GoldChanged?.Invoke();
            return true;
        }
        // ------------------------------------------------------------------ potions

        /// <summary>Most potions of each kind the potion slots hold.</summary>
        public const int MaxPotions = 10;

        public int HealthPotions { get; private set; } = 3;
        public int ManaPotions { get; private set; } = 1;

        /// <summary>The potion counts changed (the potion slots redraw).</summary>
        public event Action PotionsChanged;

        /// <summary>A potion slot was clicked in the inventory: true for health (the player drinks it).</summary>
        public event Action<bool> PotionSlotClicked;

        public int Potions(bool health) => health ? HealthPotions : ManaPotions;

        public void SetPotions(int health, int mana)
        {
            HealthPotions = Math.Max(0, Math.Min(MaxPotions, health));
            ManaPotions = Math.Max(0, Math.Min(MaxPotions, mana));
            PotionsChanged?.Invoke();
        }

        /// <summary>Adds potions (capped); returns how many actually fit.</summary>
        public int AddPotions(bool health, int count)
        {
            int added = Math.Max(0, Math.Min(MaxPotions - Potions(health), count));
            if (added == 0)
                return 0;
            if (health)
                HealthPotions += added;
            else
                ManaPotions += added;
            PotionsChanged?.Invoke();
            return added;
        }

        /// <summary>Takes one potion out of its slot; false if there is none.</summary>
        public bool UsePotion(bool health)
        {
            if (Potions(health) <= 0)
                return false;
            if (health)
                HealthPotions--;
            else
                ManaPotions--;
            PotionsChanged?.Invoke();
            return true;
        }

        public void ClickPotionSlot(bool health)
        {
            PotionSlotClicked?.Invoke(health);
        }

        public event Action PlayerDied;
        public event Action PlayerRevived;

        /// <summary>An item thrown out of the bag (InventoryUI); whoever listens puts it on the ground.</summary>
        public event Action<ItemData> ItemThrown;

        public void ThrowAway(ItemData item)
        {
            if (item != null)
                ItemThrown?.Invoke(item);
        }

        private void Awake()
        {
            Grid = new InventoryGrid(gridWidth, gridHeight);
            Stash = new InventoryGrid(StashSize, StashSize);
            Equipment = new EquipmentSet();
            Equipment.Changed += (slot, item) => Recalculate();
            Stats = StatSheet.Build(baseStats, Equipment);
        }

        public void SetBaseStats(BaseStats stats)
        {
            baseStats = stats ?? new BaseStats();
            Recalculate();
        }

        private List<StatModifier> passives = new List<StatModifier>();

        /// <summary>The stat lines from the passive tree, counted with the gear's.</summary>
        public void SetPassiveModifiers(List<StatModifier> modifiers)
        {
            passives = modifiers ?? new List<StatModifier>();
            Recalculate();
        }

        public void Recalculate()
        {
            Stats = StatSheet.Build(baseStats, Equipment, passives);
            StatsChanged?.Invoke();
        }

        public void NotifyPlayerDied()
        {
            if (IsPlayerDead)
                return;

            IsPlayerDead = true;
            PlayerDied?.Invoke();
        }

        public void NotifyPlayerRevived()
        {
            if (!IsPlayerDead)
                return;

            IsPlayerDead = false;
            PlayerRevived?.Invoke();
        }
    }
}

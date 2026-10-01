using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;

namespace PoeClone.World
{
    /// <summary>
    /// What a kill pays besides experience and item drops: gold straight into the purse (no
    /// pickup), and sometimes a potion. Shown as floating text where the enemy fell.
    /// </summary>
    public static class KillRewards
    {
        public static readonly Color GoldColor = new Color(1f, 0.84f, 0.3f);

        private const float HealthPotionChance = 0.12f;
        private const float ManaPotionChance = 0.07f;

        public static void Grant(EnemyKind kind, int monsterLevel, Vector3 at)
        {
            PlayerStats player = Object.FindAnyObjectByType<PlayerStats>();
            if (player == null)
                return;

            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                float levelScale = 1f + 0.3f * (Mathf.Max(1, monsterLevel) - 1);
                int gold = Mathf.Max(1, Mathf.RoundToInt(kind.Experience * 0.3f * levelScale * Random.Range(0.6f, 1.4f)));
                inventory.AddGold(gold);
                CombatText.Show(at + Vector3.up * 1.2f, "+" + gold + " gold", GoldColor, 0.75f);
            }

            PlayerPotions potions = player.GetComponent<PlayerPotions>();
            if (potions == null)
                return;

            if (Random.value < HealthPotionChance && potions.Add(health: true, 1) > 0)
                CombatText.Show(at + Vector3.up * 1.8f, "+1 Health Potion", new Color(1f, 0.4f, 0.4f), 0.8f);
            else if (Random.value < ManaPotionChance && potions.Add(health: false, 1) > 0)
                CombatText.Show(at + Vector3.up * 1.8f, "+1 Mana Potion", new Color(0.5f, 0.6f, 1f), 0.8f);
        }
    }
}

using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;

namespace PoeClone.World
{
    /// <summary>
    /// What a kill pays besides experience and item drops: a pile of gold, and sometimes a potion,
    /// both dropped on the ground like items (see LootDrop: gold is picked up by walking over it).
    /// </summary>
    public static class KillRewards
    {
        public static readonly Color GoldColor = new Color(1f, 0.84f, 0.3f);

        private const float HealthPotionChance = 0.12f;
        private const float ManaPotionChance = 0.07f;

        private static PlayerStats rewardPlayer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlayerCache() => rewardPlayer = null;

        // Share the lookup between XP and ground rewards. Unity's destroyed-object check
        // invalidates this cache when the player is replaced or its scene is unloaded.
        internal static PlayerStats Player
        {
            get
            {
                if (rewardPlayer == null || !rewardPlayer.gameObject.activeInHierarchy)
                    rewardPlayer = Object.FindAnyObjectByType<PlayerStats>();
                return rewardPlayer;
            }
        }

        /// <summary>A monster the player killed (quests count these).</summary>
        public static event System.Action<EnemyKind, int> EnemyKilled;

        public static void Grant(EnemyKind kind, int monsterLevel, Vector3 at, bool dropLoot = true)
        {
            EnemyKilled?.Invoke(kind, monsterLevel);

            if (!dropLoot)
                return;

            PlayerStats player = Player;
            if (player == null)
                return;

            float levelScale = 1f + 0.3f * (Mathf.Max(1, monsterLevel) - 1);
            int gold = Mathf.Max(1, Mathf.RoundToInt(kind.Experience * 0.3f * levelScale * Random.Range(0.6f, 1.4f)));
            LootDrop.DropGold(gold, at);

            if (Random.value < HealthPotionChance)
                LootDrop.DropPotion(health: true, at);
            else if (Random.value < ManaPotionChance)
                LootDrop.DropPotion(health: false, at);
        }
    }
}

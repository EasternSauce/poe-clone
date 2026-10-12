using UnityEngine;
using PoeClone.Combat;
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

        /// <summary>Maximum horizontal distance from a kill for local or shared experience.</summary>
        public const float ExperienceRange = 60f;

        public static bool CanReceiveExperience(PlayerStats player, Vector3 at)
        {
            if (player == null || player.IsDead)
                return false;
            AreaManager areas = AreaManager.Instance;
            if (areas != null && (areas.IsSwitching ||
                Network.CoopSession.AreaAt(at) != areas.CurrentAreaIndex))
                return false;
            Vector3 offset = player.transform.position - at;
            offset.y = 0f;
            return offset.sqrMagnitude <= ExperienceRange * ExperienceRange;
        }

        private const float HealthPotionChance = 0.12f;
        private const float ManaPotionChance = 0.07f;

        /// <summary>A monster the player killed (quests count these).</summary>
        public static event System.Action<EnemyKind, int> EnemyKilled;

        public static void Grant(EnemyKind kind, int monsterLevel, Vector3 at, bool dropLoot = true)
        {
            EnemyKilled?.Invoke(kind, monsterLevel);

            if (!dropLoot)
                return;

            PlayerStats player = Object.FindAnyObjectByType<PlayerStats>();
            if (player == null)
                return;

            float levelScale = 1f + 0.3f * (Mathf.Max(1, monsterLevel) - 1);
            float loot = Party.LootScale;
            int gold = Mathf.Max(1, Mathf.RoundToInt(kind.Experience * 0.3f * levelScale * loot * Random.Range(0.6f, 1.4f)));
            LootDrop.DropGold(gold, at);

            if (Random.value < HealthPotionChance * loot)
                LootDrop.DropPotion(health: true, at);
            else if (Random.value < ManaPotionChance * loot)
                LootDrop.DropPotion(health: false, at);
        }
    }
}

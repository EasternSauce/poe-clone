using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.UI;

namespace PoeClone.Player
{
    /// <summary>
    /// The player's damage reaching an enemy, and what the special stats do on top: life leech and
    /// chill on hit (attacks only), culling strike, and life on kill. Every player hit on an enemy
    /// goes through <see cref="Deal"/>: melee swings, arrows, bolts and the other skills.
    /// </summary>
    public static class HitEffects
    {
        private const float ChillSeconds = 2f;

        /// <summary>Damages the enemy, shows the number, and applies the attacker's on-hit stats.</summary>
        public static void Deal(Transform attacker, EnemyHealth enemy, float damage, bool attack, Color color)
        {
            if (enemy == null || enemy.IsDead)
                return;

            enemy.TakeDamage(damage);
            CombatText.Show(enemy.transform.position + Vector3.up * 1.6f * enemy.transform.localScale.y,
                Mathf.Max(1, Mathf.RoundToInt(damage)).ToString(), color);

            PlayerStats stats = attacker != null ? attacker.GetComponent<PlayerStats>() : null;
            PlayerInventory inventory = attacker != null ? attacker.GetComponent<PlayerInventory>() : null;
            if (stats == null || inventory == null || inventory.Stats == null || stats.IsDead)
                return;
            StatSheet sheet = inventory.Stats;

            if (attack)
            {
                float leech = sheet.Total(StatType.LifeLeech);
                if (leech > 0f)
                    stats.Heal(damage * leech / 100f);

                float chill = sheet.Total(StatType.ChillOnHit);
                if (chill > 0f && !enemy.IsDead && Random.value * 100f < chill)
                {
                    EnemyController ai = enemy.GetComponent<EnemyController>();
                    if (ai != null)
                        ai.Chill(ChillSeconds);
                }
            }

            if (!enemy.IsDead && sheet.Total(StatType.CullingStrike) > 0f && !EnemyKinds.Get(enemy.KindIndex).IsBoss &&
                enemy.CurrentHealth < enemy.MaxHealth * DefenceMath.CullThreshold)
            {
                enemy.TakeDamage(enemy.CurrentHealth + 1f);
                CombatText.Show(enemy.transform.position + Vector3.up * 2.2f * enemy.transform.localScale.y, "Culled",
                    CombatText.AvoidColor, 0.8f);
            }

            if (enemy.IsDead)
            {
                float onKill = sheet.Total(StatType.LifeOnKill);
                if (onKill > 0f)
                    stats.Heal(onKill);
            }
        }

        /// <summary>A stat total for the player behind this transform (0 if it isn't one).</summary>
        public static float Stat(Transform player, StatType stat)
        {
            PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
            return inventory != null && inventory.Stats != null ? inventory.Stats.Total(stat) : 0f;
        }

        /// <summary>The flat directions of a fan of projectiles around a heading, this many degrees apart.</summary>
        public static Vector3[] Spread(Vector3 forward, int count, float degreesApart)
        {
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            count = Mathf.Max(1, count);
            var directions = new Vector3[count];
            float first = -degreesApart * (count - 1) * 0.5f;
            for (int k = 0; k < count; k++)
                directions[k] = Quaternion.AngleAxis(first + k * degreesApart, Vector3.up) * forward;
            return directions;
        }
    }
}

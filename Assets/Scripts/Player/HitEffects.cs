using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Skills;
using PoeClone.UI;

namespace PoeClone.Player
{
    /// <summary>
    /// The player's damage reaching an enemy, and what the special stats do on top: the damage
    /// bonuses that depend on the hit or the moment (elements, attacks, low life, chilled or
    /// shocked targets, critical strikes), the ailments (ignite, shock, chill), life leech, culling
    /// strike, and everything that happens on a kill (life/mana, Onslaught, corpse explosions,
    /// shatter). Every player hit on an enemy goes through <see cref="Deal"/>: melee swings,
    /// arrows, bolts and the other skills.
    /// </summary>
    public static class HitEffects
    {
        private const float ChillSeconds = 2f;
        private const float ShockSeconds = 4f;
        public const float ShockedMore = 1.25f;
        private const float IgniteShare = 0.6f;
        private const float IgniteSeconds = 3f;
        private const float BaseCritMultiplier = 1.5f;
        public const float OnslaughtSeconds = 4f;
        private const float ExplosionRadius = 3.2f;
        private const float ExplosionShare = 1f / 6f;
        private const float ShatterRadius = 3f;
        private const float ShatterShare = 0.12f;
        private const float StormbladeShare = 0.5f;
        private const float StormbladeJump = 6f;

        private static readonly Color ExplosionColor = new Color(1f, 0.45f, 0.12f);
        private static readonly Color CritColor = new Color(1f, 0.85f, 0.25f);

        /// <summary>
        /// Damages the enemy, shows the number, and applies the attacker's on-hit stats.
        /// secondary: the hit is itself a passive's effect (an explosion, an arc), so it can't set
        /// off more of them (otherwise one kill could chain through a whole pack forever).
        /// </summary>
        public static void Deal(Transform attacker, EnemyHealth enemy, float damage, bool attack, Color color,
            DamageType type = DamageType.Physical, bool secondary = false)
        {
            if (enemy == null || enemy.IsDead)
                return;

            PlayerStats stats = attacker != null ? attacker.GetComponent<PlayerStats>() : null;
            PlayerInventory inventory = attacker != null ? attacker.GetComponent<PlayerInventory>() : null;
            StatSheet sheet = stats != null && inventory != null && !stats.IsDead ? inventory.Stats : null;
            EnemyController ai = enemy.GetComponent<EnemyController>();
            bool wasChilled = ai != null && ai.IsChilled;

            bool crit = false;
            if (sheet != null)
            {
                damage *= Increase(sheet, stats, attack, type, wasChilled);
                float critChance = sheet.Total(StatType.CriticalChance);
                if (critChance > 0f && Random.value * 100f < critChance)
                {
                    crit = true;
                    damage *= BaseCritMultiplier + sheet.Total(StatType.CriticalMultiplier) / 100f;
                }
            }
            if (enemy.IsShocked)
                damage *= ShockedMore;
            damage *= Skills.Curse.TakenMultiplier(enemy);

            Vector3 at = enemy.transform.position;
            float scale = enemy.transform.localScale.y;
            float enemyMaxLife = enemy.MaxHealth;
            enemy.TakeDamage(damage);
            string number = Mathf.Max(1, Mathf.RoundToInt(damage)).ToString();
            CombatText.Show(at + Vector3.up * 1.6f * scale, crit ? number + "!" : number, crit ? CritColor : color, crit ? 1.35f : 1f);

            if (sheet == null)
                return;

            if (attack)
            {
                float leech = sheet.Total(StatType.LifeLeech);
                if (leech > 0f)
                    stats.Heal(damage * leech / 100f);

                float chill = sheet.Total(StatType.ChillOnHit);
                if (chill > 0f && !enemy.IsDead && ai != null && Random.value * 100f < chill)
                    ai.Chill(ChillSeconds);

                if (crit && !secondary && Random.value * 100f < sheet.Total(StatType.Stormblade))
                    Stormblade(attacker, enemy, damage * StormbladeShare);
            }

            if (!enemy.IsDead)
            {
                if (type == DamageType.Fire && Roll(sheet, StatType.IgniteChance))
                {
                    if (!enemy.IsBurning)
                        CombatText.Show(at + Vector3.up * 2.1f * scale, "Ignited", CombatText.FireColor, 0.7f);
                    enemy.Ignite(damage * IgniteShare, IgniteSeconds);
                }
                if (type == DamageType.Lightning && Roll(sheet, StatType.ShockChance))
                {
                    if (!enemy.IsShocked)
                        CombatText.Show(at + Vector3.up * 2.1f * scale, "Shocked", CombatText.LightningColor, 0.7f);
                    enemy.Shock(ShockSeconds);
                }
            }

            if (!enemy.IsDead && sheet.Total(StatType.CullingStrike) > 0f && !EnemyKinds.Get(enemy.KindIndex).IsBoss &&
                enemy.CurrentHealth < enemy.MaxHealth * DefenceMath.CullThreshold)
            {
                enemy.TakeDamage(enemy.CurrentHealth + 1f);
                CombatText.Show(at + Vector3.up * 2.2f * scale, "Culled", CombatText.AvoidColor, 0.8f);
            }

            if (enemy.IsDead)
                OnKill(attacker, stats, sheet, enemy, at, enemyMaxLife, wasChilled || (ai != null && ai.IsChilled), secondary);
        }

        // Everything that adds up as "increased damage" for this hit, as one multiplier.
        private static float Increase(StatSheet sheet, PlayerStats stats, bool attack, DamageType type, bool chilled)
        {
            float percent = sheet.Total(StatType.Damage);
            if (attack)
                percent += sheet.Total(StatType.AttackDamage);
            switch (type)
            {
                case DamageType.Fire: percent += sheet.Total(StatType.FireDamage); break;
                case DamageType.Cold: percent += sheet.Total(StatType.ColdDamage); break;
                case DamageType.Lightning: percent += sheet.Total(StatType.LightningDamage); break;
            }
            if (stats.MaxHealth > 0f && stats.CurrentHealth < stats.MaxHealth * 0.5f)
                percent += sheet.Total(StatType.DamageWhileLowLife);
            if (chilled)
                percent += sheet.Total(StatType.DamageVsChilled);
            return Mathf.Max(0.1f, 1f + percent / 100f);
        }

        private static bool Roll(StatSheet sheet, StatType chance)
        {
            float percent = sheet.Total(chance);
            return percent > 0f && Random.value * 100f < percent;
        }

        private static void OnKill(Transform attacker, PlayerStats stats, StatSheet sheet, EnemyHealth enemy, Vector3 at,
            float enemyMaxLife, bool chilled, bool secondary)
        {
            float life = sheet.Total(StatType.LifeOnKill);
            if (life > 0f)
                stats.Heal(life);
            float mana = sheet.Total(StatType.ManaOnKill);
            if (mana > 0f)
                stats.RestoreMana(mana);

            if (Roll(sheet, StatType.OnslaughtOnKill))
            {
                PlayerController controller = attacker.GetComponent<PlayerController>();
                if (controller != null)
                    controller.GrantOnslaught(OnslaughtSeconds);
            }

            if (secondary)
                return;

            if (Roll(sheet, StatType.ExplodeOnKill))
                Burst(attacker, enemy, at, ExplosionRadius, enemyMaxLife * ExplosionShare, DamageType.Fire, ExplosionColor, null);
            else if (chilled && Roll(sheet, StatType.Shatter))
                Burst(attacker, enemy, at, ShatterRadius, enemyMaxLife * ShatterShare, DamageType.Cold, CombatText.ColdColor, "Shatter");
        }

        // A corpse explosion or a shatter: damages everything around the dead enemy (and a shatter chills it).
        private static void Burst(Transform attacker, EnemyHealth dead, Vector3 at, float radius, float damage,
            DamageType type, Color color, string label)
        {
            SkillEffects.Shockwave(at, radius, color, 0.35f);
            if (label != null)
                CombatText.Show(at + Vector3.up * 2.2f, label, color, 0.9f);

            foreach (EnemyHealth other in EnemiesNear(at, radius, dead))
            {
                if (type == DamageType.Cold)
                {
                    EnemyController ai = other.GetComponent<EnemyController>();
                    if (ai != null)
                        ai.Chill(ChillSeconds);
                }
                Deal(attacker, other, damage, false, color, type, secondary: true);
            }
        }

        // Stormblade: lightning leaps from the struck enemy to up to three others close by.
        private static void Stormblade(Transform attacker, EnemyHealth from, float damage)
        {
            var struck = new HashSet<EnemyHealth> { from };
            Vector3 origin = from.transform.position + Vector3.up * 0.8f;
            for (int k = 0; k < 3; k++)
            {
                EnemyHealth next = null;
                float best = StormbladeJump * StormbladeJump;
                foreach (EnemyHealth e in EnemyHealth.Active)
                {
                    if (e == null || e.IsDead || struck.Contains(e))
                        continue;
                    float d = (e.transform.position - origin).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        next = e;
                    }
                }
                if (next == null)
                    return;
                Vector3 to = next.transform.position + Vector3.up * 0.8f;
                SkillEffects.Arc(origin, to, CombatText.LightningColor);
                struck.Add(next);
                Deal(attacker, next, damage, false, CombatText.LightningColor, DamageType.Lightning, secondary: true);
                origin = to;
            }
        }

        private static List<EnemyHealth> EnemiesNear(Vector3 at, float radius, EnemyHealth except)
        {
            var found = new List<EnemyHealth>();
            float r2 = radius * radius;
            foreach (EnemyHealth e in EnemyHealth.Active)
            {
                if (e == null || e == except || e.IsDead)
                    continue;
                Vector3 offset = e.transform.position - at;
                offset.y = 0f;
                if (offset.sqrMagnitude <= r2)
                    found.Add(e);
            }
            return found;
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

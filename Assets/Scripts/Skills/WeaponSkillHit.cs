using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;
using E = PoeClone.Inventory.WeaponSkillEffect;

namespace PoeClone.Skills
{
    /// <summary>Skill-local effects. Secondary bursts never recurse.</summary>
    public static class WeaponSkillHit
    {
        public static DamageType Element(WeaponSkillBonuses b, DamageType fallback)
        {
            if (b == null) return fallback;
            if (b.Get(E.Fire) > 0) return DamageType.Fire;
            if (b.Get(E.Cold) > 0) return DamageType.Cold;
            if (b.Get(E.Lightning) > 0) return DamageType.Lightning;
            return fallback;
        }
        public static void Apply(Transform owner, EnemyHealth enemy, float dealt, WeaponSkillBonuses b,
            DamageType type, bool wasChilled, bool minion = false, bool melee = false)
        {
            if (b == null || owner == null || enemy == null || dealt <= 0) return;
            var inventory = owner.GetComponent<PlayerInventory>();
            var stats = owner.GetComponent<PlayerStats>();
            float seconds = 2f * b.DurationMultiplier;
            var ai = enemy.GetComponent<EnemyController>();
            if (!enemy.IsDead)
            {
                if (ai != null && Roll(b.Get(E.Chill))) ai.Chill(seconds);
                if (Roll(b.Get(E.Shock))) enemy.Shock(4f * b.DurationMultiplier);
                if (type == DamageType.Fire && Roll(b.Get(E.Ignite))) enemy.Ignite(dealt * 5f, 3f * b.DurationMultiplier);
                if (b.Get(E.CleaveBleed) > 0f)
                    enemy.Bleed(dealt * b.Get(E.CleaveBleed) / 100f, WeaponSkillRules.CleaveBleedSeconds);
                // These effects are a share of the final hit; do not apply owner damage increases twice.
                if (b.Get(E.Poison) > 0 || b.Get(E.Cloud) > 0)
                    WeaponVenom.Apply(owner, enemy, dealt, b.Get(E.Poison), b.Get(E.Cloud), inventory?.Stats, minionDamage: true, melee: melee, durationMultiplier: b.DurationMultiplier, radiusMultiplier: b.AreaMultiplier, tree: b);
                float cull = b.CullPercent(EnemyKinds.Get(enemy.KindIndex).IsBoss);
                if (cull > 0 && enemy.CurrentHealth < enemy.MaxHealth * cull / 100f)
                    enemy.TakeDamage(enemy.CurrentHealth + enemy.MaxHealth + 1f);
            }
            if (stats != null && !stats.IsDead) stats.Heal(b.Get(E.LifeOnHit));
            bool shatter = enemy.IsDead && (wasChilled || ai != null && ai.IsChilled) && Roll(b.Get(E.Shatter));
            bool explode = enemy.IsDead && Roll(b.Get(E.Explode));
            float splash = b.Get(E.Splash);
            if (!shatter && !explode && splash <= 0) return;
            Vector3 at = enemy.transform.position;
            float radius = (shatter || explode ? 3f : 2.2f) * b.AreaMultiplier;
            DamageType burstType = shatter ? DamageType.Cold : explode ? DamageType.Fire : type;
            Color color = CombatText.ColorFor(burstType);
            SkillEffects.Shockwave(at, radius, color, 0.3f, type: burstType);
            if (shatter || explode) owner.GetComponent<PlayerSkills>()?.RecordBurst(shatter ? PlayerSkills.ShatterCast : PlayerSkills.CorpseExplosionCast, at, radius);
            float amount = shatter || explode ? enemy.MaxHealth * 0.15f : dealt * splash / 100f;
            foreach (EnemyHealth other in EnemyHealth.Active.ToArray())
            {
                if (other == null || other == enemy || other.IsDead || Vector3.Distance(at, other.transform.position) > radius) continue;
                other.TakeDamage(amount, burstType, 0f, 0f, hitOrigin: at);
                if (shatter) other.GetComponent<EnemyController>()?.Chill(seconds);
            }
        }
        private static bool Roll(float chance) => chance > 0 && Random.value * 100f < chance;
    }
}

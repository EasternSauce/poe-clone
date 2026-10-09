using System.Collections.Generic;
using UnityEngine;
using PoeClone.Audio;
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
        private const float CurseOnHitSeconds = 4f;
        private const float SplashRadius = 2.4f;
        private const float RetaliationRadius = 3.5f;

        private static readonly Color ExplosionColor = new Color(1f, 0.45f, 0.12f);
        private static readonly Color CritColor = new Color(1f, 0.85f, 0.25f);

        /// <summary>
        /// Damages the enemy, shows the number, and applies the attacker's on-hit stats.
        /// secondary: the hit is itself a passive's effect (an explosion, an arc), so it can't set
        /// off more of them (otherwise one kill could chain through a whole pack forever).
        /// igniteBonus: % chance to ignite on top of the attacker's own (a fire hit's skill, e.g. Burning Arrow).
        /// </summary>
        public static void Deal(Transform attacker, EnemyHealth enemy, float damage, bool attack, Color color,
            DamageType type = DamageType.Physical, bool secondary = false, float igniteBonus = 0f, Vector3? displayAt = null,
            bool throughExposedHead = false, bool melee = false, float projectileDistance = -1f, int venomArrowLevel = 0,
            Vector3? hitOrigin = null)
        {
            if (enemy == null || enemy.IsDead)
                return;

            PlayerStats stats = attacker != null ? attacker.GetComponent<PlayerStats>() : null;
            PlayerInventory inventory = attacker != null ? attacker.GetComponent<PlayerInventory>() : null;
            StatSheet sheet = stats != null && inventory != null && !stats.IsDead ? inventory.Stats : null;
            EnemyController ai = enemy.GetComponent<EnemyController>();
            bool wasChilled = ai != null && ai.IsChilled;

            if (sheet != null && attack && type == DamageType.Physical)
            {
                DamageType converted = ConvertedAttackType(sheet);
                if (converted != DamageType.Physical)
                {
                    type = converted;
                    color = CombatText.ColorFor(type);
                }
            }
            // Extra damage from the mana pool joins the hit's base, so every bonus scales it.
            if (sheet != null && !secondary)
                damage += stats.MaxMana * Mathf.Max(0f, sheet.Total(StatType.ManaAsDamage)) / 100f;

            bool crit = false;
            float baseDamage = damage;
            if (sheet != null)
            {
                damage *= Increase(sheet, stats, attack, type, wasChilled, spell: !secondary);
                damage *= 1f + stats.RampageStacks * Mathf.Max(0f, sheet.Total(StatType.Rampage)) / 100f;
                if (projectileDistance >= 0f && sheet.Total(StatType.PointBlank) > 0f)
                    damage *= StatSheet.PointBlankMultiplier(projectileDistance);
                float critChance = sheet.AttackCriticalChance;
                if (attack && !secondary && critChance > 0f && Random.value * 100f < critChance)
                {
                    crit = true;
                    damage *= BaseCritMultiplier + sheet.Total(StatType.CriticalMultiplier) / 100f;
                }
            }
            if (enemy.IsShocked)
                damage *= ShockedMore;
            damage *= Skills.Curse.TakenMultiplier(enemy);
            float ailmentBaseDamage = sheet != null ? baseDamage * sheet.DamageMultiplier(false, false,
                stats.CurrentHealth < stats.MaxHealth * 0.5f, wasChilled, StatType.FireDamage, damageOverTime: true) : baseDamage;
            if (enemy.IsShocked) ailmentBaseDamage *= ShockedMore;
            ailmentBaseDamage *= Skills.Curse.TakenMultiplier(enemy);

            Vector3 at = enemy.transform.position;
            float scale = enemy.transform.localScale.y;
            float enemyMaxLife = enemy.MaxHealth;
            float armourPenetration = sheet != null ? sheet.Total(StatType.ArmourPenetration) : 0f;
            float elementalPenetration = sheet != null ? sheet.Total(StatType.ElementalPenetration) : 0f;
            if (sheet != null)
            {
                if (type == DamageType.Fire) elementalPenetration += sheet.Total(StatType.FirePenetration);
                else if (type == DamageType.Cold) elementalPenetration += sheet.Total(StatType.ColdPenetration);
                else if (type == DamageType.Lightning) elementalPenetration += sheet.Total(StatType.LightningPenetration);
                else if (type == DamageType.Poison) elementalPenetration += sheet.Total(StatType.PoisonPenetration);
            }
            damage = enemy.TakeDamage(damage, type, armourPenetration, elementalPenetration, throughExposedHead,
                canEnrage: !melee, hitOrigin: hitOrigin ?? (attacker != null ? (Vector3?)attacker.position : null));
            // A warded hit cannot apply ailments, cull, or grant on-hit recovery.
            if (damage <= 0f) return;
            string number = Mathf.Max(1, Mathf.RoundToInt(damage)).ToString();
            CombatText.Show(displayAt ?? (at + Vector3.up * 1.6f * scale), crit ? number + "!" : number, crit ? CritColor : color, crit ? 1.35f : 1f);

            if (sheet == null)
                return;

            if (attack && !secondary && (!enemy.Immune || throughExposedHead))
            {
                float poison = sheet.Total(StatType.PoisonOnHit);
                float cloud = sheet.Total(StatType.VenomCloudOnHit);
                if (venomArrowLevel > 0)
                {
                    poison += 90f + 5f * (venomArrowLevel - 1);
                    cloud += 25f;
                }
                WeaponVenom.Apply(attacker, enemy, baseDamage, poison, cloud, sheet, canEnrage: !melee);
            }

            if (!attack && !secondary)
            {
                float spellLeech = sheet.Total(StatType.SpellLeech);
                if (spellLeech > 0f)
                    stats.Heal(damage * spellLeech / 100f);
            }

            if (!secondary && !enemy.IsDead && Roll(sheet, StatType.CurseOnHit))
            {
                if (Skills.Curse.TakenMultiplier(enemy) <= 1f)
                    CombatText.Show(at + Vector3.up * 2.1f * scale, "Rotting", Skills.Curse.RotColor, 0.7f);
                Skills.Curse.Apply(enemy, CurseOnHitSeconds, 0.2f, 0.15f);
            }

            if (attack)
            {
                // Each enemy actually struck grants flat recovery, including killing blows.
                // Passive effects and blocked/immune hits cannot generate extra recovery.
                if (!secondary && damage > 0f)
                    stats.Heal(sheet.Total(StatType.LifeOnAttackHit));

                float leech = sheet.Total(StatType.LifeLeech);
                if (leech > 0f)
                    stats.Heal(damage * leech / 100f);

                float chill = sheet.Total(StatType.ChillOnHit);
                if (chill > 0f && !enemy.IsDead && ai != null && Random.value * 100f < chill)
                    ai.Chill(ChillSeconds);

                if (crit && !secondary && Random.value * 100f < sheet.Total(StatType.Stormblade))
                    Stormblade(attacker, enemy, baseDamage * StormbladeShare);
            }

            if (!enemy.IsDead)
            {
                if (type == DamageType.Fire && Random.value * 100f < sheet.Total(StatType.IgniteChance) + igniteBonus)
                {
                    if (!enemy.IsBurning)
                        CombatText.Show(at + Vector3.up * 2.1f * scale, "Ignited", CombatText.FireColor, 0.7f);
                    enemy.Ignite(ailmentBaseDamage * IgniteShare, IgniteSeconds);
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
        private static float Increase(StatSheet sheet, PlayerStats stats, bool attack, DamageType type, bool chilled, bool spell)
        {
            StatType element = StatType.PhysicalDamage;
            switch (type)
            {
                case DamageType.Fire: element = StatType.FireDamage; break;
                case DamageType.Cold: element = StatType.ColdDamage; break;
                case DamageType.Lightning: element = StatType.LightningDamage; break;
                case DamageType.Poison: element = StatType.PoisonDamage; break;
            }
            return sheet.DamageMultiplier(attack, attack && sheet.Weapon == WeaponType.Bow,
                stats.MaxHealth > 0f && stats.CurrentHealth < stats.MaxHealth * 0.5f, chilled, element, spell: spell);
        }

        // An item that turns attacks into an element: the first one worn wins.
        private static DamageType ConvertedAttackType(StatSheet sheet)
        {
            if (sheet.Total(StatType.PhysicalToFire) > 0f) return DamageType.Fire;
            if (sheet.Total(StatType.PhysicalToCold) > 0f) return DamageType.Cold;
            if (sheet.Total(StatType.PhysicalToLightning) > 0f) return DamageType.Lightning;
            return DamageType.Physical;
        }

        /// <summary>
        /// Melee Splash: a basic melee blow also hurts the enemies around the one it struck, for a
        /// share of the swing's damage (enemies the swing hit directly are left out).
        /// </summary>
        public static void Splash(Transform attacker, EnemyHealth struck, float damage, ICollection<EnemyHealth> alreadyHit)
        {
            float share = Stat(attacker, StatType.MeleeSplash);
            if (share <= 0f || struck == null)
                return;
            Vector3 at = struck.transform.position;
            foreach (EnemyHealth other in EnemiesNear(at, SplashRadius, struck))
            {
                if (alreadyHit != null && alreadyHit.Contains(other)) continue;
                Deal(attacker, other, damage * share / 100f, true, CombatText.PhysicalColor, secondary: true, melee: true, hitOrigin: at);
            }
        }

        /// <summary>Block Retaliation: a block answers with a burst of physical damage around the player.</summary>
        public static void Retaliate(Transform player, StatSheet sheet)
        {
            float share = sheet != null ? sheet.Total(StatType.BlockRetaliation) : 0f;
            float damage = Mathf.Max(0f, sheet != null ? sheet.Total(StatType.Armour) : 0f) * share / 100f;
            if (damage <= 0f || player == null)
                return;
            Vector3 at = player.position;
            SkillEffects.Shockwave(at, RetaliationRadius, CombatText.BlockColor, 0.3f);
            foreach (EnemyHealth enemy in EnemiesNear(at, RetaliationRadius, null))
            {
                if (Graveward.Blocks(enemy, at)) continue;
                Deal(player, enemy, damage, false, CombatText.PhysicalColor, DamageType.Physical, secondary: true, hitOrigin: at);
            }
        }

        private static bool Roll(StatSheet sheet, StatType chance)
        {
            float percent = sheet.Total(chance);
            return percent > 0f && Random.value * 100f < percent;
        }

        private static void OnKill(Transform attacker, PlayerStats stats, StatSheet sheet, EnemyHealth enemy, Vector3 at,
            float enemyMaxLife, bool chilled, bool secondary)
        {
            float life = sheet.Total(StatType.LifeOnKill) + stats.MaxHealth * Mathf.Max(0f, sheet.Total(StatType.LifePercentOnKill)) / 100f;
            if (life > 0f)
                stats.Heal(life);
            if (sheet.Total(StatType.Rampage) > 0f)
                stats.RecordKill();
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
                Burst(attacker, enemy, at, ExplosionRadius, enemyMaxLife * ExplosionShare, DamageType.Fire, ExplosionColor);
            else if (chilled && Roll(sheet, StatType.Shatter))
                Burst(attacker, enemy, at, ShatterRadius, enemyMaxLife * ShatterShare, DamageType.Cold, CombatText.ColdColor);
        }

        /// <summary>A corpse explosion's (or shatter's) look and sound, without the damage.</summary>
        public static void PlayBurstVisual(Vector3 at, float radius, bool shatter)
        {
            Color color = shatter ? CombatText.ColdColor : ExplosionColor;
            SkillEffects.Shockwave(at, radius, color, 0.35f);
            SkillEffects.Blast(at + Vector3.up * 0.7f, radius * 0.4f, color, 0.3f);
            if (shatter)
                CombatText.Show(at + Vector3.up * 2.2f, "Shatter", color, 0.9f);
            AudioManager audio = AudioManager.Instance;
            if (audio != null)
                {
                float pitch = Random.Range(0.92f, 1.08f);
                // Frequent freeze shatters use their own quieter recordings rather than the quest shatter.
                if (shatter) audio.PlayEffect("combat.shatter", at, pitch: pitch);
                else audio.PlayAtPoint(audio.Sfx("corpse_explosion"), at, pitch);
            }
        }

        // A corpse explosion or a shatter: damages everything around the dead enemy (and a shatter chills it).
        private static void Burst(Transform attacker, EnemyHealth dead, Vector3 at, float radius, float damage,
            DamageType type, Color color)
        {
            bool shatter = type == DamageType.Cold;
            PlayBurstVisual(at, radius, shatter);
            // Logged with the skill casts, so spectators see (and hear) it too.
            PlayerSkills skills = attacker != null ? attacker.GetComponent<PlayerSkills>() : null;
            if (skills != null)
                skills.RecordBurst(shatter ? PlayerSkills.ShatterCast : PlayerSkills.CorpseExplosionCast, at, radius);

            foreach (EnemyHealth other in EnemiesNear(at, radius, dead))
            {
                if (Graveward.Blocks(other, at)) continue;
                if (type == DamageType.Cold)
                {
                    EnemyController ai = other.GetComponent<EnemyController>();
                    if (ai != null)
                        ai.Chill(ChillSeconds);
                }
                Deal(attacker, other, damage, false, color, type, secondary: true, hitOrigin: at);
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
                Deal(attacker, next, damage, false, CombatText.LightningColor, DamageType.Lightning, secondary: true, hitOrigin: origin);
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

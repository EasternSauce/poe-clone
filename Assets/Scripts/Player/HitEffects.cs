using System.Collections.Generic;
using UnityEngine;
using PoeClone.Network.Replication;
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
    /// strike, and everything that happens on a kill (life/mana, Onslaught, corpse explosions).
    /// Every player hit on an enemy goes through <see cref="Deal"/>: melee swings,
    /// arrows, bolts and the other skills.
    /// </summary>
    public static class HitEffects
    {
        private const float ChillSeconds = 2f;
        private const float ShockSeconds = 4f;
        public const float ShockedMore = 1.25f;
        private const float IgniteShare = 5f; // total base damage over the full burn
        private const float IgniteSeconds = 3f;
        private const float BaseCritMultiplier = 1.5f;
        public const float OnslaughtSeconds = 4f;
        private const float ExplosionRadius = 3.2f;
        private const float ExplosionShare = 2f;
        private const float ChainDelay = 0.1f;
        private const float CurseOnHitSeconds = 4f;
        private const float RetaliationRadius = 3.5f;

        private static readonly Color ExplosionColor = new Color(1f, 0.45f, 0.12f);
        private static readonly Color CritColor = new Color(1f, 0.85f, 0.25f);

        /// <summary>
        /// Damages the enemy, shows the number, and applies the attacker's on-hit stats.
        /// secondary: the hit is itself a passive's effect (an explosion, an arc), so it can't set
        /// off more of them (otherwise one kill could chain through a whole pack forever).
        /// fromExplosion: the hit is a corpse explosion's. Its kills may explode in turn, a moment
        /// later, so a chain ripples through a pack (it ends when a kill fails its roll or no one is left).
        /// igniteBonus: % chance to ignite on top of the attacker's own (a fire hit's skill, e.g. Burning Arrow).
        /// manaDamageMultiplier: scales added mana damage with a skill's chain falloff.
        /// </summary>
        public static void Deal(Transform attacker, EnemyHealth enemy, float damage, bool attack, Color color,
            DamageType type = DamageType.Physical, bool secondary = false, float igniteBonus = 0f, Vector3? displayAt = null,
            bool throughExposedHead = false, bool melee = false, float projectileDistance = -1f, int venomArrowLevel = 0,
            Vector3? hitOrigin = null, bool fromExplosion = false, int rollSeed = 0, float manaDamageMultiplier = 1f, WeaponSkillBonuses tree = null, float skillIncreased = 0f, bool flinch = true, float sourceDamageMultiplier = 1f, string impactSound = null, bool stunningHit = false)
        {
            // Utility effects must never become hits through added mana damage or on-hit stats.
            if (enemy == null || enemy.IsDead || damage <= 0f)
                return;

            if (rollSeed == 0) rollSeed = EventRandom.NextSeed();
            PlayerStats stats = attacker != null ? attacker.GetComponent<PlayerStats>() : null;
            PlayerInventory inventory = attacker != null ? attacker.GetComponent<PlayerInventory>() : null;
            StatSheet sheet = stats != null && inventory != null && !stats.IsDead ? inventory.Stats : null;
            EnemyController ai = enemy.GetComponent<EnemyController>();
            bool wasChilled = ai != null && ai.IsChilled;
            float skillMore = !secondary && tree != null ? tree.MoreDamageMultiplier *
                (enemy.CurrentHealth >= enemy.MaxHealth * 0.999f ? 1f + tree.Get(WeaponSkillEffect.FirstStrike) / 100f : 1f) : 1f;
            if (!secondary && tree != null)
            {
                type = WeaponSkillHit.Element(tree, type);
                color = CombatText.ColorFor(type);
            }

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
                damage += stats.MaxMana * Mathf.Max(0f, sheet.Total(StatType.ManaAsDamage)) / 100f * manaDamageMultiplier;

            damage *= skillMore * sourceDamageMultiplier;
            bool crit = false;
            float baseDamage = damage;
            if (sheet != null)
            {
                damage *= Increase(sheet, stats, attack, type, wasChilled, spell: !secondary, skillIncreased: !secondary && tree != null ?
                    skillIncreased + tree.Get(WeaponSkillEffect.Damage) + (wasChilled ? tree.Get(WeaponSkillEffect.DamageVsChilled) : 0f) : 0f);
                damage *= 1f + stats.RampageStacks * Mathf.Max(0f, sheet.Total(StatType.Rampage)) / 100f;
                if (projectileDistance >= 0f && sheet.Total(StatType.PointBlank) > 0f)
                    damage *= StatSheet.PointBlankMultiplier(projectileDistance);
                float critChance = sheet.AttackCriticalChance;
                if (attack && !secondary && critChance > 0f && EventRandom.Value(rollSeed, EventRandom.Critical) * 100f < critChance)
                {
                    crit = true;
                    damage *= BaseCritMultiplier + sheet.Total(StatType.CriticalMultiplier) / 100f;
                }
            }
            if (enemy.IsShocked)
                damage *= ShockedMore;
            damage *= Skills.Curse.TakenMultiplier(enemy);
            float ailmentBaseDamage = baseDamage;
            if (sheet != null && type == DamageType.Fire)
            {
                ailmentBaseDamage *= sheet.DamageMultiplier(false, false,
                    stats.CurrentHealth < stats.MaxHealth * 0.5f, wasChilled, StatType.FireDamage, damageOverTime: true, skillIncreased: tree?.Get(WeaponSkillEffect.Damage) ?? 0f);
                if (enemy.IsShocked) ailmentBaseDamage *= ShockedMore;
                ailmentBaseDamage *= Skills.Curse.TakenMultiplier(enemy);
            }

            Vector3 at = enemy.transform.position;
            float scale = enemy.transform.localScale.y;
            float enemyMaxLife = enemy.MaxHealth;
            float armourPenetration = (sheet != null ? sheet.Total(StatType.ArmourPenetration) : 0f) + (!secondary && tree != null ? tree.Get(WeaponSkillEffect.ArmourPenetration) : 0f);
            float elementalPenetration = sheet != null ? sheet.Total(StatType.ElementalPenetration) : 0f;
            if (sheet != null)
            {
                if (type == DamageType.Fire) elementalPenetration += sheet.Total(StatType.FirePenetration);
                else if (type == DamageType.Cold) elementalPenetration += sheet.Total(StatType.ColdPenetration);
                else if (type == DamageType.Lightning) elementalPenetration += sheet.Total(StatType.LightningPenetration);
                else if (type == DamageType.Poison) elementalPenetration += sheet.Total(StatType.PoisonPenetration);
            }
            damage = enemy.TakeDamage(damage, type, armourPenetration, elementalPenetration, throughExposedHead,
                flinch: flinch, hitOrigin: hitOrigin ?? (attacker != null ? (Vector3?)attacker.position : null), rollSeed: rollSeed, impactSound: impactSound, shredArmour: !secondary && tree != null && tree.Get(WeaponSkillEffect.ArmourShred) > 0f, stunningHit: stunningHit);
            // A warded hit cannot apply ailments, cull, or grant on-hit recovery.
            if (damage <= 0f) return;
            if (!secondary) WeaponSkillHit.Apply(attacker, enemy, damage, tree, type, wasChilled, melee: melee);
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
                    poison += SkillBook.VenomArrowPoison;
                    cloud += SkillBook.VenomArrowCloud;
                }
                WeaponVenom.Apply(attacker, enemy, baseDamage, poison, cloud, sheet, melee: melee, durationMultiplier: tree?.DurationMultiplier ?? 1f, radiusMultiplier: tree?.AreaMultiplier ?? 1f, tree: tree);
            }

            if (!attack && !secondary)
            {
                float spellLeech = sheet.Total(StatType.SpellLeech);
                if (spellLeech > 0f)
                    stats.Leech(damage * spellLeech / 100f);
            }

            if (!secondary && !enemy.IsDead && Roll(sheet, StatType.CurseOnHit, rollSeed))
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
                    stats.Leech(damage * leech / 100f);

                if (!enemy.IsDead && ai != null && Roll(sheet, StatType.ChillOnHit, rollSeed))
                    ai.Chill(ChillSeconds);
            }

            if (!enemy.IsDead)
            {
                if (type == DamageType.Fire && EventRandom.Value(rollSeed, EventRandom.StatPurposeOffset + (int)StatType.IgniteChance) * 100f < sheet.Total(StatType.IgniteChance) + igniteBonus)
                {
                    if (!enemy.IsBurning)
                        CombatText.Show(at + Vector3.up * 2.1f * scale, "Ignited", CombatText.FireColor, 0.7f);
                    enemy.Ignite(ailmentBaseDamage * IgniteShare, IgniteSeconds * (tree?.DurationMultiplier ?? 1f));
                }
                if (type == DamageType.Lightning && Roll(sheet, StatType.ShockChance, rollSeed))
                {
                    if (!enemy.IsShocked)
                        CombatText.Show(at + Vector3.up * 2.1f * scale, "Shocked", CombatText.LightningColor, 0.7f);
                    enemy.Shock(ShockSeconds * (tree?.DurationMultiplier ?? 1f));
                }
            }

            if (!enemy.IsDead && sheet.Total(StatType.CullingStrike) > 0f && !EnemyKinds.Get(enemy.KindIndex).IsBoss &&
                enemy.CurrentHealth < enemy.MaxHealth * DefenceMath.CullThreshold)
            {
                enemy.TakeDamage(enemy.CurrentHealth + 1f);
                CombatText.Show(at + Vector3.up * 2.2f * scale, "Culled", CombatText.AvoidColor, 0.8f);
            }

            if (enemy.IsDead)
                OnKill(attacker, stats, sheet, enemy, at, enemyMaxLife, secondary, fromExplosion, melee, rollSeed);
        }

        // Everything that adds up as "increased damage" for this hit, as one multiplier.
        private static float Increase(StatSheet sheet, PlayerStats stats, bool attack, DamageType type, bool chilled, bool spell, float skillIncreased = 0f)
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
                stats.MaxHealth > 0f && stats.CurrentHealth < stats.MaxHealth * 0.5f, chilled, element, spell: spell, skillIncreased: skillIncreased);
        }

        // An item that turns attacks into an element: the first one worn wins.
        private static DamageType ConvertedAttackType(StatSheet sheet)
        {
            if (sheet.Total(StatType.PhysicalToFire) > 0f) return DamageType.Fire;
            if (sheet.Total(StatType.PhysicalToCold) > 0f) return DamageType.Cold;
            if (sheet.Total(StatType.PhysicalToLightning) > 0f) return DamageType.Lightning;
            return DamageType.Physical;
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
                Deal(player, enemy, damage, false, CombatText.PhysicalColor, DamageType.Physical, secondary: true, hitOrigin: at);
            }
        }

        private static bool Roll(StatSheet sheet, StatType chance, int rollSeed)
        {
            float percent = sheet.Total(chance);
            return percent > 0f && EventRandom.Value(rollSeed, EventRandom.StatPurposeOffset + (int)chance) * 100f < percent;
        }

        // melee: the kill came from a melee hit (or what one set off), so what it sets off is melee
        // too.
        private static void OnKill(Transform attacker, PlayerStats stats, StatSheet sheet, EnemyHealth enemy, Vector3 at,
            float enemyMaxLife, bool secondary, bool fromExplosion, bool melee, int rollSeed)
        {
            float life = sheet.Total(StatType.LifeOnKill) + stats.MaxHealth * Mathf.Max(0f, sheet.Total(StatType.LifePercentOnKill)) / 100f;
            if (life > 0f)
                stats.Heal(life);
            if (sheet.Total(StatType.Rampage) > 0f)
                stats.RecordKill();
            float mana = sheet.Total(StatType.ManaOnKill);
            if (mana > 0f)
                stats.RestoreMana(mana);

            if (Roll(sheet, StatType.OnslaughtOnKill, rollSeed))
            {
                PlayerController controller = attacker.GetComponent<PlayerController>();
                if (controller != null)
                    controller.GrantOnslaught(OnslaughtSeconds);
            }

            if (fromExplosion)
            {
                // A kill by an explosion can explode too, a beat later so the chain is seen spreading.
                if (Roll(sheet, StatType.ExplodeOnKill, rollSeed))
                    stats.StartCoroutine(ChainBurst(attacker, enemy, at, enemyMaxLife * ExplosionShare, melee));
                return;
            }
            if (secondary)
                return;

            if (Roll(sheet, StatType.ExplodeOnKill, rollSeed))
                Burst(attacker, enemy, at, ExplosionRadius, enemyMaxLife * ExplosionShare, melee);
        }

        /// <summary>A corpse explosion's (or shatter's) look and sound, without the damage.</summary>
        public static void PlayBurstVisual(Vector3 at, float radius, bool shatter)
        {
            Color color = shatter ? CombatText.ColdColor : ExplosionColor;
            if (shatter)
            {
                SkillEffects.Shockwave(at, radius, color, 0.35f, type: DamageType.Cold);
                SkillEffects.Blast(at + Vector3.up * 0.7f, radius * 0.4f, color, 0.3f);
                CombatText.Show(at + Vector3.up * 2.2f, "Shatter", color, 0.9f);
            }
            else
                SkillEffects.Explosion(at, radius, color);
            AudioManager audio = AudioManager.Instance;
            if (audio != null)
                {
                float pitch = Random.Range(0.92f, 1.08f);
                // Frequent freeze shatters use their own quieter recordings rather than the quest shatter.
                if (shatter) audio.PlayEffect("combat.shatter", at, pitch: pitch);
                else audio.PlayAtPoint(audio.Sfx("corpse_explosion"), at, pitch);
            }
        }

        // Corpse explosions damage nearby enemies and can set off further explosions.
        private static void Burst(Transform attacker, EnemyHealth dead, Vector3 at, float radius, float damage, bool melee)
        {
            PlayBurstVisual(at, radius, false);
            // Logged with the skill casts, so spectators see (and hear) it too.
            PlayerSkills skills = attacker != null ? attacker.GetComponent<PlayerSkills>() : null;
            if (skills != null)
                skills.RecordBurst(PlayerSkills.CorpseExplosionCast, at, radius);

            foreach (EnemyHealth other in EnemiesNear(at, radius, dead))
            {
                Deal(attacker, other, damage, false, ExplosionColor, DamageType.Fire, secondary: true, hitOrigin: at, fromExplosion: true, melee: melee);
            }
        }

        private static System.Collections.IEnumerator ChainBurst(Transform attacker, EnemyHealth dead, Vector3 at, float damage, bool melee)
        {
            yield return new WaitForSeconds(ChainDelay);
            if (attacker != null)
                Burst(attacker, dead, at, ExplosionRadius, damage, melee);
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

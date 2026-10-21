using System;
using System.Linq;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.Skills;
using E = PoeClone.Inventory.WeaponSkillEffect;

namespace PoeClone.World
{
    /// <summary>Shared stationary single-target damage approximation for the survey and arena picker.
    /// Includes complete successful melee combos, damage over time, a full minion army and repeatable buffs.
    /// Excludes opening hits, low/full-life and on-kill buffs, mana downtime and movement.</summary>
    public static class SingleTargetDamage
    {
        private static readonly EnemyKind[] benchmarkBosses = EnemyKinds.All.Where(e => e.IsBoss &&
            (e.Boss == BossStyle.Gravelord || e.Boss == BossStyle.Warlord || e.Boss == BossStyle.FrostQueen)).ToArray();
        private static float PiercingInterval(float attackRate, float chargeTimePercent) =>
            Mathf.Max(.65f, 1.6f * (1 + chargeTimePercent / 100) / attackRate) + .35f / attackRate;
        private static DamageType BaseElement(SkillDefinition skill) => skill.MeleeAttack || skill.Bow ?
            skill.Id == SkillId.BurningArrow ? DamageType.Fire : DamageType.Physical : SkillEffects.DamagePattern(skill);
        public static float Estimate(SkillDefinition skill, ItemData weapon, StatSheet sheet, WeaponSkillBonuses tree, out float hitDps, out float dotDps, out float uses, bool includeNonBossCull = false)
        {
            int steps = Mathf.Max(1, WeaponSkillRules.ComboLength(skill.Grant, tree.Get(E.CleaveDoubleSlash) > 0f));
            hitDps = dotDps = uses = 0f;
            // Mitigate each hit before averaging: armour absorbs a smaller share of heavy blows.
            // Repeat complete successful combos, including the normal interval after each finisher.
            for (int step = 1; step <= steps; step++)
            {
                EstimateStep(skill, weapon, sheet, tree, step, out float hit, out float dot, out uses, includeNonBossCull);
                hitDps += hit / steps;
                dotDps += dot / steps;
            }
            return Mathf.Max(0, hitDps + dotDps);
        }

        private static float EstimateStep(SkillDefinition skill, ItemData weapon, StatSheet sheet, WeaponSkillBonuses tree,
            int comboStep, out float hitDps, out float dotDps, out float uses, bool includeNonBossCull)
        {
            float speed = Mathf.Max(.5f, 1 + ((skill.MeleeAttack || skill.Bow ? sheet.Total(StatType.AttackSpeed) : sheet.Total(StatType.CastSpeed)) + tree.Get(E.Speed)) / 100);
            uses = skill.Spell ? 1f / Mathf.Max(.01f, skill.UseInterval(speed, sheet.CooldownRecoveryMultiplier)) :
                SlotRules.BaseAttacksPerSecond(weapon.WeaponType) * speed * (skill.Bow ? PlayerSkills.BowSpeed(skill.Id) : PlayerSkills.MeleeSpeed(skill.Id));
            if (skill.Channelled) uses = WeaponSkillRules.WhirlwindHitsPerSecond(weapon.WeaponType, speed);
            if (skill.Id == SkillId.PiercingShot) uses = 1 / PiercingInterval(speed, tree.Get(E.ChargeTime));
            if (comboStep > 0 && WeaponSkillRules.ComboLength(skill.Grant) > 0)
                uses *= WeaponSkillRules.ComboSustainedSpeed(skill.Grant, doubleSlash: tree.Get(E.CleaveDoubleSlash) > 0f);
            float baseHit = skill.MeleeAttack || skill.Bow ? sheet.Total(StatType.PhysicalDamage) * skill.WeaponMultiplierAt(1) : skill.DamageForCharacter(sheet.Level);
            bool doubleSlash = skill.Id == SkillId.Cleave && tree.Get(E.CleaveDoubleSlash) > 0f;
            float combo = WeaponSkillRules.ComboHitMultiplier(skill.Grant, comboStep, tree.ComboDamageMultiplier,
                tree.Get(E.CleaveExplosion) > 0f, doubleSlash);
            baseHit *= combo;
            var element = WeaponSkillHit.Element(tree, BaseElement(skill));
            bool attack = skill.MeleeAttack || skill.Bow;
            if (element == DamageType.Physical && attack)
            {
                if (sheet.Total(StatType.PhysicalToFire) > 0) element = DamageType.Fire;
                else if (sheet.Total(StatType.PhysicalToCold) > 0) element = DamageType.Cold;
                else if (sheet.Total(StatType.PhysicalToLightning) > 0) element = DamageType.Lightning;
            }
            // Each double-slash cut is mitigated individually, then both hits contribute.
            float rate = uses, hits = doubleSlash ? 2 : 1, multiplier;
            if (skill.Summon)
            {
                var kind = PlayerSkills.MinionKindOf(skill.Id);
                int cap = Minion.CapFor(kind, tree, sheet);
                float interval = kind == MinionKind.Warrior ? 1.1f : kind == MinionKind.Mage ? 1.6f : kind == MinionKind.Wolf ? .8f : kind == MinionKind.Golem ? 1.5f : 1.15f;
                float hordeSpeed = Minion.UsesProjectileSpell(kind) ? 0 : Mathf.Max(0, sheet.Total(StatType.MinionHordeSpeed)) * cap;
                rate = Mathf.Max(.2f, 1 + (Minion.ActionSpeedIncreaseFor(kind, sheet, tree) + hordeSpeed) / 100) / interval;
                // Regular resummoning maintains the four-second rally, a separate multiplier in Minion.ApplySpeed.
                if (kind == MinionKind.Mage) rate *= 1 + tree.Get(E.MageCastSpeedAfterSummon) / 100;
                baseHit = Minion.DamageFor(kind, 1, sheet, tree);
                if (kind == MinionKind.Wolf) baseHit *= (100 + sheet.Total(StatType.MinionDamage) + sheet.Total(StatType.Intelligence) * .5f + tree.Get(E.Damage) + Math.Min(4, cap - 1) * tree.Get(E.PackHunt)) /
                    Mathf.Max(1, 100 + sheet.Total(StatType.MinionDamage) + sheet.Total(StatType.Intelligence) * .5f + tree.Get(E.Damage));
                hits = cap * (kind == MinionKind.Golem && tree.Get(E.GolemAftershock) > 0 ? 1.4f : 1);
                multiplier = 1;
            }
            else
            {
                if (skill.Id == SkillId.PiercingShot) baseHit *= 3;
                if (skill.Id == SkillId.CrossSlash) hits = 2;
                // Aftershock centres barely overlap: a stationary target receives one wave.
                if (skill.Id == SkillId.RainOfArrows)
                {
                    float spread = PlayerSkills.RainRadius * Mathf.Max(.25f, 1 + tree.Get(E.RainSpread) / 100);
                    float radius = PlayerSkills.RainHitRadius * DefenceMath.RadiusMultiplier(sheet.Total(StatType.AreaOfEffect) + tree.Get(E.Area));
                    hits = PlayerSkills.ExpectedRainHits(1 + tree.ExtraProjectiles + sheet.ExtraSkillProjectiles(false) +
                        Mathf.Max(0, Mathf.RoundToInt(sheet.Total(StatType.AdditionalArrows))),
                        1 + Mathf.RoundToInt(tree.Get(E.RainSalvos)), spread, radius);
                }
                baseHit += sheet.Total(StatType.MaxMana) * sheet.Total(StatType.ManaAsDamage) / 100;
                float chilled = skill.Id == SkillId.IceShard ? 1 : ChanceUptime(tree.Get(E.Chill) + (attack ? sheet.Total(StatType.ChillOnHit) : 0), rate, 2 * tree.DurationMultiplier);
                multiplier = sheet.DamageMultiplier(attack, skill.Bow, false, false, DamageStat(element), spell: true,
                    skillIncreased: tree.Get(E.Damage) + (skill.Id == SkillId.PiercingShot ? tree.Get(E.ChargeDamage) : 0) + chilled * (tree.Get(E.DamageVsChilled) + sheet.Total(StatType.DamageVsChilled))) * tree.MoreDamageMultiplier;
                if (attack) multiplier *= 1 + sheet.AttackCriticalChance / 100 * (.5f + sheet.Total(StatType.CriticalMultiplier) / 100);
                if (skill.Bow && sheet.Total(StatType.PointBlank) > 0) multiplier *= StatSheet.PointBlankMultiplier(6);
            }
            float shock = ChanceUptime(tree.Get(E.Shock) + (element == DamageType.Lightning && !skill.Summon ? sheet.Total(StatType.ShockChance) : 0), rate * hits, 4 * tree.DurationMultiplier);
            // Minion hits do not trigger the owner's CurseOnHit stat (Minion.Deal).
            float curse = skill.Summon ? 0 : ChanceUptime(sheet.Total(StatType.CurseOnHit), rate * hits, 4);
            float taken = (1 + .25f * shock) * (1 + .15f * curse);
            float dealt = AgainstEnemy(baseHit * multiplier * taken, element, sheet.Level, sheet, tree, skill.Summon);
            hitDps = dealt * rate * hits;
            // Bleeding stores the tree's percentage of each final hit; armour and damage modifiers
            // have already applied, and each hit contributes an independent stack.
            dotDps = skill.Id == SkillId.Cleave ? hitDps * tree.Get(E.CleaveBleed) / 100f : 0f;
            float poison = tree.Get(E.Poison) / 100;
            float cloud = tree.Get(E.Cloud) / 100;
            float duration = Mathf.Max(1, Mathf.Round(6 * tree.DurationMultiplier)) / 6;
            if (skill.Id == SkillId.SummonViper) poison += .45f;
            dotDps += AgainstEnemy(dealt * poison * duration, DamageType.Poison, sheet.Level, sheet, tree, true) * rate * hits;
            dotDps += AgainstEnemy(dealt * cloud, DamageType.Poison, sheet.Level, sheet, tree, true) * Mathf.Min(1, rate * hits) * 3 * duration;
            if (!skill.Summon)
            {
                float builtPoison = skill.Id == SkillId.FangStrike ? SkillBook.FangStrikePoison / 100 : skill.Id == SkillId.VenomSlash ? SkillBook.VenomSlashPoison / 100 : skill.Id == SkillId.VenomSpout ? 1 : 0;
                float dotBase = builtPoison > 0 ? (skill.MeleeAttack ? sheet.Total(StatType.PhysicalDamage) : skill.DamageForCharacter(sheet.Level)) : baseHit;
                float extraPoison = attack ? sheet.Total(StatType.PoisonOnHit) / 100 + (skill.Id == SkillId.VenomArrow ? SkillBook.VenomArrowPoison / 100 : 0) : 0;
                float extraCloud = attack ? sheet.Total(StatType.VenomCloudOnHit) / 100 + (skill.Id == SkillId.VenomArrow ? SkillBook.VenomArrowCloud / 100 : 0) : 0;
                float dotMultiplier = sheet.DamageMultiplier(false, false, false, false, StatType.PoisonDamage, damageOverTime: true, skillIncreased: tree.Get(E.Damage));
                // Fang Strike explicitly scales its built-in poison with the combo; Venom Slash does not.
                float builtPoisonBase = dotBase * (skill.Id == SkillId.FangStrike ? combo : 1f);
                dotDps += AgainstEnemy((builtPoisonBase * builtPoison + baseHit * extraPoison) * dotMultiplier * duration, DamageType.Poison, sheet.Level, sheet, tree, false) * rate * hits;
                dotDps += AgainstEnemy(baseHit * extraCloud * dotMultiplier, DamageType.Poison, sheet.Level, sheet, tree, false) * Mathf.Min(1, rate * hits) * 3 * duration;
            }
            if (element == DamageType.Fire)
            {
                float treeIgnite = ChanceUptime(tree.Get(E.Ignite), rate * hits, 3 * tree.DurationMultiplier);
                float ownerIgnite = skill.Summon ? 0 : ChanceUptime(sheet.Total(StatType.IgniteChance) + (skill.Id == SkillId.BurningArrow ? 20 : 0), rate * hits, 3 * tree.DurationMultiplier);
                float ownerBurn = baseHit * taken * tree.MoreDamageMultiplier * sheet.DamageMultiplier(false, false, false, false, StatType.FireDamage, damageOverTime: true, skillIncreased: tree.Get(E.Damage)) * 5 / (3 * tree.DurationMultiplier);
                // Ignites refresh/replace, never stack per projectile or per minion.
                dotDps += AgainstEnemy(Mathf.Max(dealt * 5 / (3 * tree.DurationMultiplier) * treeIgnite, ownerBurn * ownerIgnite), DamageType.Fire, sheet.Level, sheet, tree, true);
            }
            // Culling skips the final fraction of life; use the strongest threshold, never add them.
            float cull = tree.CullPercent(boss: !includeNonBossCull) / 100;
            if (includeNonBossCull && !skill.Summon && sheet.Total(StatType.CullingStrike) > 0)
                cull = Mathf.Max(cull, DefenceMath.CullThreshold);
            if (cull > 0)
            {
                float effective = 1 / Mathf.Max(.01f, 1 - Mathf.Clamp01(cull));
                hitDps *= effective;
                dotDps *= effective;
            }
            return Mathf.Max(0, hitDps + dotDps);
        }
        public static float ChanceUptime(float chancePercent, float rate, float duration) =>
            1 - Mathf.Pow(1 - Mathf.Clamp01(chancePercent / 100), Mathf.Max(0, rate * duration));
        private static StatType DamageStat(DamageType type) => type == DamageType.Fire ? StatType.FireDamage : type == DamageType.Cold ? StatType.ColdDamage :
            type == DamageType.Lightning ? StatType.LightningDamage : type == DamageType.Poison ? StatType.PoisonDamage : StatType.PhysicalDamage;
        /// <summary>Uses the same armour and resistance rules as EnemyHealth.TakeDamage.</summary>
        public static float BossMitigatedHit(float damage, float armour, float resistance, float penetration, bool physical)
        {
            return physical ? damage * (1 - DefenceMath.ArmourReduction(armour * (1 - Mathf.Clamp01(penetration / 100)), damage)) :
                DefenceMath.AfterResistance(damage, resistance - penetration);
        }
        private static float AgainstEnemy(float damage, DamageType type, int level, StatSheet sheet, WeaponSkillBonuses tree, bool minion)
        {
            float total = 0;
            foreach (var enemy in benchmarkBosses)
            {
                bool physical = type == DamageType.Physical;
                float resistance = type == DamageType.Fire ? enemy.FireResistance : type == DamageType.Cold ? enemy.ColdResistance :
                    type == DamageType.Lightning ? enemy.LightningResistance : enemy.PoisonResistance;
                float penetration = minion ? 0 : physical ? sheet.Total(StatType.ArmourPenetration) + tree.Get(E.ArmourPenetration) :
                    type == DamageType.Poison ? sheet.Total(StatType.PoisonPenetration) : sheet.Total(StatType.ElementalPenetration) +
                    sheet.Total(type == DamageType.Fire ? StatType.FirePenetration : type == DamageType.Cold ? StatType.ColdPenetration : StatType.LightningPenetration);
                total += BossMitigatedHit(damage, enemy.Armour * EnemyKinds.ArmourScale(level), resistance, penetration, physical);
            }
            return total / benchmarkBosses.Length;
        }
    }
}

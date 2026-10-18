using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Enemies;
using PoeClone.Inventory;
using PoeClone.Player;
using PoeClone.UI;

namespace PoeClone.Skills
{
    public partial class PlayerSkills
    {
        private void PlaceSpellEffigy(SkillDefinition skill, int level, bool asAttack)
        {
            Vector3 direction = asAttack ? transform.forward : AimDirection();
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
            direction.Normalize();
            float distance = Mathf.Clamp(AimDistance() ?? 4f, 1f, 8f);
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            Vector3? ground = null;
            for (float d = distance; d >= 0.75f && !ground.HasValue; d -= 0.5f)
                foreach (float offset in new[] { 0f, 1.1f, -1.1f, 2.2f, -2.2f })
                {
                    Vector3 candidate = transform.position + direction * d + side * offset;
                    if (Minion.All.Exists(m => m != null && !m.IsDead && m.Kind == MinionKind.Effigy &&
                        Vector3.Distance(m.transform.position, candidate) < 0.9f)) continue;
                    ground = OpenGround(candidate);
                    if (ground.HasValue) break;
                }
            if (!ground.HasValue)
            {
                CombatText.Show(transform.position + Vector3.up * 2f, "No room for an effigy", UiKit.DimText, 0.8f);
                return;
            }
            Face(direction);
            Vector3 at = ground.Value;
            at.y = Visuals.Debris.GroundBelow(at + Vector3.up * 3f) + 0.03f;
            Minion.PlaceEffigy(transform, skill.Id, level, at);
            PlaySkillSound(skill.Id, ground.Value);
        }

        public float EffigyReach(SkillId id, WeaponSkillBonuses tree)
        {
            switch (id)
            {
                case SkillId.FireBolt: return BoltRange * tree.RangeMultiplier * SkillArea(id);
                case SkillId.IceShard: return ShardRange * tree.RangeMultiplier * SkillArea(id);
                case SkillId.ChainLightning: return ChainReach * tree.RangeMultiplier;
                case SkillId.FrostNova: return 5f * SkillArea(id);
                case SkillId.WarCry: return 4f * SkillArea(id);
                default: return 12f * tree.RangeMultiplier;
            }
        }

        public float EffigyInterval(SkillId id, WeaponSkillBonuses tree) =>
            SkillBook.Get(id).UseInterval(EffigyRules.CastRate(inventory.Stats, tree.Get(WeaponSkillEffect.Speed),
                controller != null && controller.HasOnslaught ? PlayerController.OnslaughtMore : 1f), CooldownRecoveryMultiplier);

        public void CastFromEffigy(SpellEffigy effigy, EnemyHealth target)
        {
            if (effigy == null || target == null || target.IsDead || stats == null || stats.IsDead) return;
            var skill = SkillBook.Get(effigy.Skill);
            var tree = effigy.Tree;
            float damage = skill.DamageForCharacter(stats.Level);
            float multiplier = EffigyRules.DamageMultiplier(inventory.Stats);
            float area = SkillArea(skill.Id);
            Vector3 at = effigy.transform.position;
            Vector3 facing = effigy.transform.forward;
            PlaySkillSound(skill.Id, at);
            void HitFromEffigy(EnemyHealth enemy, float amount, DamageType type, float manaScale = 1f)
            {
                HitEffects.Deal(transform, enemy, amount, false, CombatText.ColorFor(type), type,
                    hitOrigin: at, tree: tree, manaDamageMultiplier: manaScale, sourceDamageMultiplier: multiplier);
            }
            switch (skill.Id)
            {
                case SkillId.FireBolt:
                case SkillId.IceShard:
                {
                    var volley = PlayerArrow.NewVolley();
                    int count = 1 + tree.ExtraProjectiles + inventory.Stats.ExtraSkillProjectiles(true);
                    var flights = new List<Vector3>();
                    float range = (skill.Id == SkillId.FireBolt ? BoltRange : ShardRange) * tree.RangeMultiplier * area;
                    foreach (Vector3 direction in HitEffects.Spread(facing, count, skill.Id == SkillId.FireBolt ? 12f : 7f))
                    {
                        var projectile = skill.Id == SkillId.FireBolt
                            ? PlayerArrow.LaunchBolt(transform, range, damage, skill.Color, 1.6f * area, direction, volley: volley)
                            : PlayerArrow.LaunchShard(transform, range, damage, skill.Color, 1.5f, direction, volley: volley);
                        projectile.WithSkill(tree).From(effigy.transform, multiplier);
                        flights.Add(direction * range);
                    }
                    Record(skill, effigy.Level, skill.Id == SkillId.FireBolt ? 1.6f * area : 0f, count, flights.ToArray(), at, facing);
                    if (skill.Id == SkillId.FireBolt && tree.Get(WeaponSkillEffect.FireOrbit) > 0f &&
                        ++effigy.CastUses >= tree.Get(WeaponSkillEffect.FireOrbit))
                    {
                        effigy.CastUses = 0;
                        StartCoroutine(EffigyFireWheel(effigy, damage, area, multiplier));
                    }
                    break;
                }
                case SkillId.ChainLightning:
                {
                    var struck = new HashSet<EnemyHealth>();
                    Vector3 from = at + Vector3.up * 1.6f;
                    var points = new List<Vector3> { from };
                    float manaScale = 1f;
                    for (int jump = 0; target != null && jump < 1 + Mathf.RoundToInt(tree.Get(WeaponSkillEffect.Chains)); jump++)
                    {
                        Vector3 to = target.transform.position + Vector3.up * 0.4f * target.transform.localScale.y;
                        SkillEffects.Arc(from, to, skill.Color);
                        points.Add(to);
                        HitFromEffigy(target, damage, DamageType.Lightning, manaScale);
                        struck.Add(target);
                        from = to;
                        damage *= Mathf.Clamp(0.6f + tree.Get(WeaponSkillEffect.ChainRetention) / 100f, 0.1f, 1f);
                        manaScale *= 0.6f;
                        target = Nearest(target.transform.position, ChainJump * tree.RangeMultiplier, struck);
                    }
                    Record(skill, effigy.Level, points: points.ToArray(), at: at, facing: facing);
                    break;
                }
                case SkillId.FrostNova:
                case SkillId.WarCry:
                case SkillId.VenomSpout:
                {
                    Vector3 center = skill.Id == SkillId.VenomSpout ? target.transform.position : at;
                    float radius = (skill.Id == SkillId.FrostNova ? 5f : skill.Id == SkillId.WarCry ? 4f : 2.6f) * area;
                    DamageType element = skill.Id == SkillId.FrostNova ? DamageType.Cold : skill.Id == SkillId.WarCry ? DamageType.Physical : DamageType.Poison;
                    SkillEffects.Shockwave(center, radius, skill.Color, 0.4f, type: element);
                    foreach (var enemy in EnemiesWithin(center, radius))
                    {
                        if (skill.Id == SkillId.FrostNova) enemy.GetComponent<EnemyController>()?.Chill(3f);
                        HitFromEffigy(enemy, damage, element);
                        if (skill.Id == SkillId.VenomSpout)
                            WeaponVenom.Apply(transform, enemy, damage * multiplier, 100f, 0f, inventory.Stats,
                                durationMultiplier: tree.DurationMultiplier, radiusMultiplier: tree.AreaMultiplier, tree: tree);
                    }
                    if (skill.Id == SkillId.WarCry) controller?.GrantOnslaught(4f);
                    Record(skill, effigy.Level, radius, points: skill.Id == SkillId.VenomSpout ? new[] { center } : null, at: at, facing: facing);
                    break;
                }

            }
        }

        private IEnumerator EffigyFireWheel(SpellEffigy effigy, float damage, float area, float multiplier)
        {
            var volley = PlayerArrow.NewVolley();
            var skill = SkillBook.Get(SkillId.FireBolt);
            for (int i = 0; i < 8; i++)
            {
                if (effigy == null || stats == null || stats.IsDead) yield break;
                Vector3 direction = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                float range = BoltRange * effigy.Tree.RangeMultiplier * area;
                PlayerArrow.LaunchBolt(transform, range, damage * 0.4f, skill.Color, 1.6f * area, direction, volley: volley)
                    .WithSkill(effigy.Tree).From(effigy.transform, multiplier);
                Record(skill, effigy.Level, 1.6f * area, 1, new[] { direction * range }, effigy.transform.position, direction);
                yield return new WaitForSeconds(0.07f);
            }
        }
    }
}

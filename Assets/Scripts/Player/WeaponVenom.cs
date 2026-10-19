using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Skills;
using PoeClone.Visuals;
using PoeClone.Inventory;

namespace PoeClone.Player
{
    public class WeaponVenom : MonoBehaviour
    {
        private float nextCloud;
        private readonly System.Collections.Generic.Dictionary<WeaponSkillBonuses, float> skillCloudTimes = new System.Collections.Generic.Dictionary<WeaponSkillBonuses, float>();
        private readonly System.Collections.Generic.List<GameObject> clouds = new System.Collections.Generic.List<GameObject>();
        private static readonly Color Green = new Color(0.38f, 0.72f, 0.15f);
        private sealed class PoisonStack
        {
            public PoisonStackClock Clock;
            public float Penetration;
            public bool Melee, MinionDamage;
            public WeaponSkillBonuses Tree;
            public long Count = 1;
            public PoisonStack Copy() => new PoisonStack { Clock = Clock.Copy(), Penetration = Penetration,
                Melee = Melee, MinionDamage = MinionDamage, Tree = Tree, Count = Count };
        }
        private sealed class PoisonTarget
        {
            public EnemyHealth Enemy;
            public readonly List<PoisonStack> Stacks = new List<PoisonStack>();
            public Action OnDied;
        }
        private readonly Dictionary<EnemyHealth, PoisonTarget> poisoned = new Dictionary<EnemyHealth, PoisonTarget>();
        private float nextPoisonTick = float.PositiveInfinity;
        public static void Apply(Transform attacker, EnemyHealth enemy, float hit, float poison, float cloud, StatSheet sheet = null,
            bool melee = false, bool minionDamage = false, float durationMultiplier = 1f, float radiusMultiplier = 1f, WeaponSkillBonuses tree = null)
        {
            var venom = attacker.GetComponent<WeaponVenom>();
            if (venom == null) venom = attacker.gameObject.AddComponent<WeaponVenom>();
            PlayerStats stats = attacker.GetComponent<PlayerStats>();
            Enemies.EnemyController ai = enemy.GetComponent<Enemies.EnemyController>();
            float multiplier = minionDamage ? 1f : sheet?.DamageMultiplier(false, false,
                stats != null && stats.CurrentHealth < stats.MaxHealth * 0.5f,
                ai != null && ai.IsChilled, StatType.PoisonDamage, damageOverTime: true, skillIncreased: tree?.Get(WeaponSkillEffect.Damage) ?? 0f) ?? 1f;
            if (poison > 0f && !enemy.IsDead)
                venom.AddPoison(enemy, new PoisonStack {
                    Clock = new PoisonStackClock(hit * poison / 100f * multiplier / 6f, Mathf.Max(1, Mathf.RoundToInt(6f * durationMultiplier)), Time.time),
                    Penetration = sheet?.Total(StatType.PoisonPenetration) ?? 0f,
                    Melee = melee, Tree = tree, MinionDamage = minionDamage });
            if (cloud > 0f && Time.time >= (tree != null && venom.skillCloudTimes.TryGetValue(tree, out float next) ? next : tree == null ? venom.nextCloud : 0f))
            {
                if (tree != null) venom.skillCloudTimes[tree] = Time.time + 1f;
                else venom.nextCloud = Time.time + 1f;
                venom.StartCoroutine(venom.Cloud(enemy.transform.position, hit * cloud / 100f * multiplier, sheet?.Total(StatType.PoisonPenetration) ?? 0f, melee, durationMultiplier, radiusMultiplier, tree, attacker, minionDamage));
            }
        }
        private void AddPoison(EnemyHealth enemy, PoisonStack stack)
        {
            nextPoisonTick = Mathf.Min(nextPoisonTick, stack.Clock.NextTick);
            if (!poisoned.TryGetValue(enemy, out var target))
            {
                target = new PoisonTarget { Enemy = enemy };
                target.OnDied = () => SpreadPoison(target);
                poisoned.Add(enemy, target);
                enemy.Died += target.OnDied;
            }
            // Copies with identical potency and deadlines can share storage without losing stacks.
            var matching = target.Stacks.Find(s => s.Clock.Matches(stack.Clock) && s.Penetration == stack.Penetration &&
                s.Melee == stack.Melee && s.MinionDamage == stack.MinionDamage && s.Tree == stack.Tree);
            if (matching != null) matching.Count += stack.Count;
            else target.Stacks.Add(stack);
        }

        private void RemovePoison(PoisonTarget target)
        {
            if (target.Enemy != null) target.Enemy.Died -= target.OnDied;
            poisoned.Remove(target.Enemy);
        }

        private void SpreadPoison(PoisonTarget target)
        {
            RemovePoison(target);
            if ((GetComponent<PlayerInventory>()?.Stats.Total(StatType.Contagion) ?? 0f) <= 0f) return;
            var stacks = target.Stacks.Where(s => s.Clock.CanSpread(Time.time)).ToArray();
            if (stacks.Length == 0) return;
            Vector3 at = target.Enemy.transform.position;
            foreach (var enemy in EnemyHealth.Active.ToArray())
            {
                if (enemy == null || enemy.IsDead || enemy.IsAlly || enemy == target.Enemy || enemy.IsQuestObject) continue;
                Vector3 offset = enemy.transform.position - at;
                offset.y = 0f;
                if (offset.sqrMagnitude > 25f) continue;
                foreach (var stack in stacks) AddPoison(enemy, stack.Copy());
            }
            SkillEffects.Shockwave(at, 5f, Green, 0.35f, type: Combat.DamageType.Poison);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || Time.time < nextPoisonTick) return;
            nextPoisonTick = float.PositiveInfinity;
            foreach (var target in poisoned.Values.ToArray())
            {
                var enemy = target.Enemy;
                if (enemy == null || enemy.IsDead) { RemovePoison(target); continue; }
                foreach (var stack in target.Stacks.ToArray())
                {
                    while (!enemy.IsDead && stack.Clock.TryTick(Time.time, out float amount))
                    {
                        float dealt = enemy.TakeDamageOverTime(amount * stack.Count, Combat.DamageType.Poison,
                            stack.Penetration);
                        if (dealt > 0f && enemy.IsDead && stack.Tree != null)
                            WeaponSkillHit.Apply(transform, enemy, dealt, stack.Tree, Combat.DamageType.Poison,
                                enemy.GetComponent<EnemyController>()?.IsChilled == true, minion: stack.MinionDamage, melee: stack.Melee);
                    }
                    if (stack.Clock.Finished) target.Stacks.Remove(stack);
                    else nextPoisonTick = Mathf.Min(nextPoisonTick, stack.Clock.NextTick);
                    if (enemy.IsDead) break;
                }
                if (!enemy.IsDead && target.Stacks.Count == 0) RemovePoison(target);
            }
        }
        private IEnumerator Cloud(Vector3 at, float perSecond, float penetration, bool melee, float durationMultiplier, float radiusMultiplier, WeaponSkillBonuses tree, Transform owner, bool minionDamage)
        {
            at.y = Debris.GroundBelow(at + Vector3.up * 3f) + 0.1f;
            var visual = RuntimePrimitives.Create(PrimitiveType.Cylinder, null, Green);
            visual.name = "Weapon venom cloud";
            clouds.Add(visual);
            visual.transform.position = at;
            visual.transform.localScale = new Vector3(6f * radiusMultiplier, 0.025f, 6f * radiusMultiplier);
            SkillEffects.Shockwave(at, 3f * radiusMultiplier, Green, 0.5f);
            for (int i = 0; i < Mathf.Max(1, Mathf.RoundToInt(6f * durationMultiplier)); i++)
            {
                yield return new WaitForSeconds(0.5f);
                foreach (EnemyHealth enemy in EnemyHealth.Active.ToArray())
                {
                    Vector3 d = enemy.transform.position - at; d.y = 0f;
                    if (enemy.IsDead || d.sqrMagnitude > 9f * radiusMultiplier * radiusMultiplier) continue;
                    float dealt = enemy.TakeDamageOverTime(perSecond * 0.5f, Combat.DamageType.Poison, penetration);
                    if (dealt > 0f && enemy.IsDead && tree != null)
                        WeaponSkillHit.Apply(owner, enemy, dealt, tree, PoeClone.Combat.DamageType.Poison, enemy.GetComponent<EnemyController>()?.IsChilled == true, minion: minionDamage, melee: melee);
                }
            }
            Destroy(visual);
            clouds.Remove(visual);
        }
        private void OnDestroy()
        {
            foreach (var target in poisoned.Values.ToArray()) RemovePoison(target);
            foreach (GameObject cloud in clouds) if (cloud != null) Destroy(cloud);
        }
    }
}

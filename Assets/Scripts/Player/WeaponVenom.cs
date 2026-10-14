using System.Collections;
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
        public static void Apply(Transform attacker, EnemyHealth enemy, float hit, float poison, float cloud, StatSheet sheet = null,
            bool canEnrage = true, bool minionDamage = false, float durationMultiplier = 1f, float radiusMultiplier = 1f, WeaponSkillBonuses tree = null)
        {
            var venom = attacker.GetComponent<WeaponVenom>();
            if (venom == null) venom = attacker.gameObject.AddComponent<WeaponVenom>();
            PlayerStats stats = attacker.GetComponent<PlayerStats>();
            Enemies.EnemyController ai = enemy.GetComponent<Enemies.EnemyController>();
            float multiplier = minionDamage ? 1f : sheet?.DamageMultiplier(false, false,
                stats != null && stats.CurrentHealth < stats.MaxHealth * 0.5f,
                ai != null && ai.IsChilled, StatType.PoisonDamage, damageOverTime: true, skillIncreased: tree?.Get(WeaponSkillEffect.Damage) ?? 0f) ?? 1f;
            if (poison > 0f && !enemy.IsDead) venom.StartCoroutine(venom.Poison(enemy, hit * poison / 100f * multiplier, sheet?.Total(StatType.PoisonPenetration) ?? 0f, canEnrage, durationMultiplier, tree, attacker, minionDamage));
            if (cloud > 0f && Time.time >= (tree != null && venom.skillCloudTimes.TryGetValue(tree, out float next) ? next : tree == null ? venom.nextCloud : 0f))
            {
                if (tree != null) venom.skillCloudTimes[tree] = Time.time + 1f;
                else venom.nextCloud = Time.time + 1f;
                venom.StartCoroutine(venom.Cloud(enemy.transform.position, hit * cloud / 100f * multiplier, sheet?.Total(StatType.PoisonPenetration) ?? 0f, canEnrage, durationMultiplier, radiusMultiplier, tree, attacker, minionDamage));
            }
        }
        private IEnumerator Poison(EnemyHealth enemy, float total, float penetration, bool canEnrage, float durationMultiplier, WeaponSkillBonuses tree, Transform owner, bool minionDamage)
        {
            for (int i = 0; i < Mathf.Max(1, Mathf.RoundToInt(6f * durationMultiplier)); i++)
            {
                yield return new WaitForSeconds(0.5f);
                if (enemy == null || enemy.IsDead) yield break;
                float dealt = enemy.TakeDamage(total / 6f, PoeClone.Combat.DamageType.Poison, 0f, penetration, canEnrage: canEnrage, flinch: false);
                if (dealt > 0f && enemy.IsDead && tree != null)
                    WeaponSkillHit.Apply(owner, enemy, dealt, tree, PoeClone.Combat.DamageType.Poison, enemy.GetComponent<EnemyController>()?.IsChilled == true, minion: minionDamage, melee: !canEnrage);
                if (dealt > 0f)
                    UI.CombatText.Show(enemy.transform.position + Vector3.up * 2f, Mathf.CeilToInt(dealt).ToString(), Green, 0.6f);
            }
        }
        private IEnumerator Cloud(Vector3 at, float perSecond, float penetration, bool canEnrage, float durationMultiplier, float radiusMultiplier, WeaponSkillBonuses tree, Transform owner, bool minionDamage)
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
                    float dealt = enemy.TakeDamage(perSecond * 0.5f, PoeClone.Combat.DamageType.Poison, 0f, penetration, canEnrage: canEnrage, flinch: false);
                    if (dealt > 0f && enemy.IsDead && tree != null)
                        WeaponSkillHit.Apply(owner, enemy, dealt, tree, PoeClone.Combat.DamageType.Poison, enemy.GetComponent<EnemyController>()?.IsChilled == true, minion: minionDamage, melee: !canEnrage);
                }
            }
            Destroy(visual);
            clouds.Remove(visual);
        }
        private void OnDestroy() { foreach (GameObject cloud in clouds) if (cloud != null) Destroy(cloud); }
    }
}

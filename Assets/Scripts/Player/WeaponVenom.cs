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
        private readonly System.Collections.Generic.List<GameObject> clouds = new System.Collections.Generic.List<GameObject>();
        private static readonly Color Green = new Color(0.38f, 0.72f, 0.15f);
        public static void Apply(Transform attacker, EnemyHealth enemy, float hit, float poison, float cloud, StatSheet sheet = null,
            bool canEnrage = true)
        {
            var venom = attacker.GetComponent<WeaponVenom>();
            if (venom == null) venom = attacker.gameObject.AddComponent<WeaponVenom>();
            if (poison > 0f && !enemy.IsDead) venom.StartCoroutine(venom.Poison(enemy, hit * poison / 100f * (1f + ((sheet?.Total(StatType.PoisonDamage) ?? 0f) + (sheet?.Total(StatType.DamageOverTime) ?? 0f)) / 100f), sheet?.Total(StatType.PoisonPenetration) ?? 0f, canEnrage));
            if (cloud > 0f && Time.time >= venom.nextCloud)
            {
                venom.nextCloud = Time.time + 1f;
                venom.StartCoroutine(venom.Cloud(enemy.transform.position, hit * cloud / 100f, sheet?.Total(StatType.PoisonPenetration) ?? 0f, canEnrage));
            }
        }
        private IEnumerator Poison(EnemyHealth enemy, float total, float penetration, bool canEnrage)
        {
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.5f);
                if (enemy == null || enemy.IsDead) yield break;
                float dealt = enemy.TakeDamage(total / 6f, PoeClone.Combat.DamageType.Poison, 0f, penetration, canEnrage: canEnrage);
                if (dealt > 0f)
                    UI.CombatText.Show(enemy.transform.position + Vector3.up * 2f, Mathf.CeilToInt(dealt).ToString(), Green, 0.6f);
            }
        }
        private IEnumerator Cloud(Vector3 at, float perSecond, float penetration, bool canEnrage)
        {
            at.y = Debris.GroundBelow(at + Vector3.up * 3f) + 0.1f;
            var visual = RuntimePrimitives.Create(PrimitiveType.Cylinder, null, Green);
            visual.name = "Weapon venom cloud";
            clouds.Add(visual);
            visual.transform.position = at;
            visual.transform.localScale = new Vector3(6f, 0.025f, 6f);
            SkillEffects.Shockwave(at, 3f, Green, 0.5f);
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.5f);
                foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
                {
                    Vector3 d = enemy.transform.position - at; d.y = 0f;
                    if (!enemy.IsDead && d.sqrMagnitude <= 9f) enemy.TakeDamage(perSecond * 0.5f, PoeClone.Combat.DamageType.Poison, 0f, penetration, canEnrage: canEnrage);
                }
            }
            Destroy(visual);
            clouds.Remove(visual);
        }
        private void OnDestroy() { foreach (GameObject cloud in clouds) if (cloud != null) Destroy(cloud); }
    }
}

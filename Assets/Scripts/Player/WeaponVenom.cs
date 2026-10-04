using System.Collections;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Player
{
    public class WeaponVenom : MonoBehaviour
    {
        private float nextCloud;
        private readonly System.Collections.Generic.List<GameObject> clouds = new System.Collections.Generic.List<GameObject>();
        private static readonly Color Green = new Color(0.38f, 0.72f, 0.15f);
        public static void Apply(Transform attacker, EnemyHealth enemy, float hit, float poison, float cloud)
        {
            var venom = attacker.GetComponent<WeaponVenom>();
            if (venom == null) venom = attacker.gameObject.AddComponent<WeaponVenom>();
            if (poison > 0f && !enemy.IsDead) venom.StartCoroutine(venom.Poison(enemy, hit * poison / 100f));
            if (cloud > 0f && Time.time >= venom.nextCloud)
            {
                venom.nextCloud = Time.time + 1f;
                venom.StartCoroutine(venom.Cloud(enemy.transform.position, hit * cloud / 100f));
            }
        }
        private IEnumerator Poison(EnemyHealth enemy, float total)
        {
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.5f);
                if (enemy == null || enemy.IsDead) yield break;
                enemy.TakeDamage(total / 6f);
                UI.CombatText.Show(enemy.transform.position + Vector3.up * 2f, Mathf.CeilToInt(total / 6f).ToString(), Green, 0.6f);
            }
        }
        private IEnumerator Cloud(Vector3 at, float perSecond)
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
                    if (!enemy.IsDead && d.sqrMagnitude <= 9f) enemy.TakeDamage(perSecond * 0.5f);
                }
            }
            Destroy(visual);
            clouds.Remove(visual);
        }
        private void OnDestroy() { foreach (GameObject cloud in clouds) if (cloud != null) Destroy(cloud); }
    }
}

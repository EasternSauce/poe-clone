using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>A placed ember owns its fuse, so killing its caster cannot disarm it.</summary>
    public sealed class EnemyOffering : MonoBehaviour
    {
        private const float Radius = 6f;
        private const float Fuse = 2.4f;

        private void Update()
        {
            transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 10f) * 0.12f);
        }

        public static void Place(Vector3 at, EnemyKind kind, float damage, PlayerStats target)
        {
            var root = new GameObject("PenitentOffering");
            root.transform.position = new Vector3(at.x, 0.32f, at.z);
            GameObject ember = RuntimePrimitives.Create(PrimitiveType.Sphere, root.transform, kind.Eyes);
            ember.transform.localScale = new Vector3(0.3f, 0.4f, 0.3f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                GameObject crust = RuntimePrimitives.Create(PrimitiveType.Cube, root.transform, kind.Skin);
                crust.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.13f, -0.06f, Mathf.Sin(a) * 0.13f);
                crust.transform.localScale = new Vector3(0.15f, 0.2f, 0.15f);
                crust.transform.localRotation = Quaternion.Euler(15f, i * 90f, 25f);
            }
            // Lifetime also covers a scene teardown or interrupted coroutine.
            Destroy(root, Fuse + 0.5f);
            EnemyOffering offering = root.AddComponent<EnemyOffering>();
            offering.StartCoroutine(offering.Detonate(at, kind, damage, target));
        }

        private IEnumerator Detonate(Vector3 at, EnemyKind kind, float damage, PlayerStats target)
        {
            yield return GroundTelegraph.Run(at, Radius, Fuse, DamageType.Fire, center =>
            {
                SkillEffects.Blast(center, Radius, kind.Eyes, 0.4f);
                if (target == null || target.IsDead || Sanctuary.Contains(target.transform.position, 1f)) return;
                Vector3 offset = target.transform.position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude <= Radius * Radius)
                    target.TakeHit(damage, DamageType.Fire, attack: false);
            });
            Destroy(gameObject);
        }
    }
}

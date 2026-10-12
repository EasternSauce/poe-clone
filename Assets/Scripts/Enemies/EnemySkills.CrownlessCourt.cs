using System;
using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    public partial class EnemySkills
    {
        private IEnumerator CourtSkill(Vector3 target, float damage)
        {
            int interrupted = stagger != null ? stagger.TriggerCount : 0;
            busyUntil = Time.time + 2;
            bool hit = false;
            yield return CourtPattern(this, kind, transform, target, (at, radius) =>
            {
                if (hit || !CastAlive(interrupted) || !CanSpecialHit() ||
                    Flat(player.transform.position - at).magnitude > radius) return;
                hit = true;
                if (player.TakeHit(damage, kind.DamageType) && kind.Skill == EnemySkill.JarShatter)
                    player.Poison(damage * 0.35f, 3);
            });
        }

        // Shared by local combat, co-op and spectator warnings.
        private static IEnumerator CourtPattern(MonoBehaviour host, EnemyKind kind, Transform body,
            Vector3 target, Action<Vector3, float> hit)
        {
            if (host == null || body == null) yield break;
            Vector3 from = body.position;
            Vector3 axis = FissureDirection(from, target);
            body.GetComponentInChildren<CreatureAnimator>()?.Crouch(1.05f);
            bool jar = kind.Skill == EnemySkill.JarShatter;
            Color color = jar ? new Color(0.64f, 0.73f, 0.25f) : new Color(0.36f, 0.85f, 0.74f);
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = jar ? target + Quaternion.Euler(0, i * 72, 0) * Vector3.forward * 3.6f :
                    from + axis * (i + 1) * 2.7f;
                float radius = jar ? 1.65f : 1.35f;
                float delay = jar ? 1.05f : 1.05f + i * 0.1f;
                PoeClone.Visuals.DistanceActivity.StartTracked(host, GroundTelegraph.Run(at, radius, delay, kind.DamageType, point =>
                {
                    if (body == null || !body.gameObject.activeInHierarchy) return;
                    var alive = body.GetComponent<EnemyHealth>();
                    if (alive != null && alive.IsDead) return;
                    SkillEffects.Shockwave(point, radius, color, 0.35f);
                    hit?.Invoke(point, radius);
                }));
            }
            yield return new WaitForSeconds(1.8f);
        }
    }
}

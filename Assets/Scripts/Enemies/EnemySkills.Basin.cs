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
        private const float BasinWindup = 0.95f;

        private IEnumerator BasinSkill(Vector3 target, float damage)
        {
            int interrupted = stagger != null ? stagger.TriggerCount : 0;
            busyUntil = Time.time + BasinWindup + 0.7f;
            bool hit = false;
            yield return BasinPattern(this, kind, transform, target, (at, radius) =>
            {
                // Overlapping patches from one cast can hit each local player only once.
                if (hit || !CastAlive(interrupted) || !CanSpecialHit() ||
                    Flat(player.transform.position - at).magnitude > radius) return;
                hit = true;
                if (player.TakeHit(damage, DamageType.Physical) && kind.Skill == EnemySkill.BrineSpit)
                    player.Poison(damage * 0.45f, 3f);
            });
        }

        // Shared geometry keeps spectator warnings identical to the damaging cast.
        private static IEnumerator BasinPattern(MonoBehaviour host, EnemyKind kind, Transform body, Vector3 target,
            Action<Vector3, float> hit)
        {
            if (body == null || host == null) yield break;
            Vector3 from = body.position;
            Vector3 axis = FissureDirection(from, target);
            body.GetComponentInChildren<CreatureAnimator>()?.Crouch(BasinWindup);
            Vector3[] points;
            float radius;
            if (kind.Skill == EnemySkill.SaltBurst)
            {
                points = new[] { from };
                radius = 3.2f;
            }
            else if (kind.Skill == EnemySkill.BrineSpit)
            {
                Vector3 side = Vector3.Cross(axis, Vector3.up) * 3.1f;
                points = new[] { target - side, target, target + side };
                radius = 1.8f;
            }
            else
            {
                points = new[] { from + axis * 2.5f, from + axis * 5, from + axis * 7.5f };
                radius = 1.4f;
            }
            foreach (Vector3 point in points)
            {
                float patchRadius = radius;
                PoeClone.Visuals.DistanceActivity.StartTracked(host, GroundTelegraph.Run(point, radius, BasinWindup, DamageType.Physical, at =>
                {
                    if (body == null || !body.gameObject.activeInHierarchy) return;
                    EnemyHealth alive = body.GetComponent<EnemyHealth>();
                    if (alive != null && alive.IsDead) return;
                    SkillEffects.Shockwave(at, patchRadius, kind.Skill == EnemySkill.BrineSpit
                        ? new Color(0.45f, 0.70f, 0.44f) : new Color(0.95f, 0.91f, 0.76f), 0.4f);
                    hit?.Invoke(at, patchRadius);
                }));
            }
            yield return new WaitForSeconds(BasinWindup + 0.4f);
        }
    }
}

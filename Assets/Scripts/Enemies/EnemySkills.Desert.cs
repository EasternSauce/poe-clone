using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Player;
using PoeClone.Skills;
using PoeClone.Visuals;
using PoeClone.World;

namespace PoeClone.Enemies
{
    public partial class EnemySkills
    {
        private float desertBraceUntil;
        private GameObject activeSandstorm;
        private Transform burrowModel;
        private Vector3 burrowScale;
        private bool burrowing;

        public float DesertBraceMultiplier(Vector3? hitOrigin)
        {
            if (kind == null || kind.IsCreature || kind.Skill != EnemySkill.DesertCharge ||
                Time.time >= desertBraceUntil || !hitOrigin.HasValue) return 1f;
            Vector3 direction = Flat(hitOrigin.Value - transform.position).normalized;
            return Vector3.Dot(transform.forward, direction) > 0.5f ? 0.35f : 1f;
        }

        private void OnDisable()
        {
            RestoreBurrow();
            desertBraceUntil = 0;
            busyUntil = -1;
            if (activeSandstorm != null) Destroy(activeSandstorm);
        }

        private void RestoreBurrow()
        {
            if (!burrowing) return;
            if (burrowModel != null) burrowModel.localScale = burrowScale;
            burrowing = false;
        }

        private IEnumerator DesertRush(Vector3 target, float damage)
        {
            const float windup = 0.65f, duration = 0.65f, speed = 15f;
            Vector3 from = transform.position;
            Vector3 axis = FissureDirection(from, target);
            int interrupted = stagger != null ? stagger.TriggerCount : 0;
            busyUntil = Time.time + windup + duration + 0.65f;
            desertBraceUntil = Time.time + windup;
            GetComponentInChildren<CreatureAnimator>()?.Crouch(windup);
            PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.RunLine(from, axis, duration * speed, 2, windup, DamageType.Physical, null));
            yield return new WaitForSeconds(windup);
            if (!CastAlive(interrupted)) yield break;
            bool hit = false;
            for (float t = 0; t < duration && CastAlive(interrupted); t += Time.deltaTime)
            {
                Vector3 before = transform.position;
                if (!mirror && body != null && body.enabled)
                    body.Move(axis * (speed * Time.deltaTime) + Vector3.down * 2f * Time.deltaTime);
                if (!hit && CanSpecialHit() && aimedAtLocal && SegmentDistance(before, transform.position + axis * 0.8f, player.transform.position) < 1.3f)
                {
                    player.TakeHit(damage, DamageType.Physical);
                    hit = true;
                }
                if (!mirror && t > 0.05f && Flat(transform.position - before).sqrMagnitude < 0.0001f) break;
                yield return null;
            }
        }

        private IEnumerator DesertSting(Vector3 target, float damage)
        {
            const float windup = 0.85f;
            Vector3 from = transform.position;
            Vector3 axis = FissureDirection(from, target);
            int interrupted = stagger != null ? stagger.TriggerCount : 0;
            busyUntil = Time.time + windup + 0.65f;
            GetComponentInChildren<CreatureAnimator>()?.Crouch(windup);
            PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.RunLine(from, axis, 4, 1.3f, windup, DamageType.Physical, null));
            yield return new WaitForSeconds(windup);
            if (!CastAlive(interrupted)) yield break;
            SkillEffects.Shockwave(from + axis * 2.4f, 0.8f, new Color(0.65f, 0.72f, 0.22f), 0.25f);
            if (CanSpecialHit() && InsideDesertLine(from, axis, 4, 1.3f, player.transform.position) &&
                player.TakeHit(damage * 0.8f, DamageType.Physical))
                player.Poison(damage * 0.65f, 3f);
        }

        private IEnumerator DesertBurrow(Vector3 target, float damage)
        {
            const float travel = 1.1f, warning = 0.7f;
            int interrupted = stagger != null ? stagger.TriggerCount : 0;
            busyUntil = Time.time + travel + warning + 1f;
            Vector3 from = transform.position;
            burrowModel = transform.Find("Model");
            if (burrowModel != null)
            {
                burrowScale = burrowModel.localScale;
                burrowing = true;
                burrowModel.localScale = new Vector3(burrowScale.x, 0.08f, burrowScale.z);
            }
            try
            {
                float nextRipple = 0;
                for (float t = 0; t < travel && CastAlive(interrupted); t += Time.deltaTime)
                {
                    Vector3 destination = Vector3.Lerp(from, new Vector3(target.x, from.y, target.z), Mathf.Clamp01((t + Time.deltaTime) / travel));
                    if (!mirror && body != null && body.enabled)
                        body.Move(destination - transform.position + Vector3.down * Time.deltaTime);
                    if (t >= nextRipple)
                    {
                        nextRipple += 0.12f;
                        SkillEffects.Shockwave(transform.position, 1.1f, DustColor, 0.22f);
                    }
                    yield return null;
                }
                if (!CastAlive(interrupted)) yield break;
                Vector3 eruption = transform.position;
                PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.Run(eruption, 2.4f, warning, DamageType.Physical, null));
                yield return new WaitForSeconds(warning);
                if (!CastAlive(interrupted)) yield break;
                RestoreBurrow();
                SkillEffects.Shockwave(eruption, 2.4f, DustColor, 0.4f);
                HitIfInside(eruption, 2.4f, damage);
            }
            finally { RestoreBurrow(); }
        }

        private IEnumerator DesertFan(Vector3 target, float damage)
        {
            int interrupted = stagger != null ? stagger.TriggerCount : 0;
            busyUntil = Time.time + 1.65f;
            PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.RunCone(transform.position, FissureDirection(transform.position, target),
                kind.AttackRange, 24, 0.8f, () => CastAlive(interrupted)));
            yield return new WaitForSeconds(0.8f);
            if (CastAlive(interrupted)) Volley(transform, kind, target, aimedAtLocal ? player : null, damage);
        }

        private IEnumerator CastSandstorm(Vector3 target, float damage)
        {
            if (activeSandstorm != null) yield break;
            const float windup = 0.9f;
            int interrupted = stagger != null ? stagger.TriggerCount : 0;
            busyUntil = Time.time + windup + 0.4f;
            SkillEffects.Rise(transform, new Color(0.75f, 0.52f, 0.26f), windup);
            for (float t = 0; t < windup; t += Time.deltaTime)
            {
                if (!CastAlive(interrupted)) yield break;
                if (aim != null) target = aim.position;
                Vector3 axis = FissureDirection(transform.position, target);
                transform.rotation = Quaternion.LookRotation(axis);
                yield return null;
            }
            activeSandstorm = RollingSandstorm.Launch(transform.position, FissureDirection(transform.position, target),
                damage, player, health);
        }

        // Cycles through three committed moves; ordinary attacks fill the gaps.
        private IEnumerator TollkeeperMove(Vector3 target, float damage)
        {
            int move = (UseCount - 1) % 3;
            if (move == 1) { yield return DesertRush(target, damage * 1.25f); yield break; }
            const float windup = 0.9f;
            int interrupted = stagger != null ? stagger.TriggerCount : 0;
            busyUntil = Time.time + windup + 0.8f;
            Vector3 from = transform.position;
            Vector3 axis = FissureDirection(from, target);
            if (move == 0)
                PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.RunCone(from, axis, 4.8f, 65, windup, () => CastAlive(interrupted)));
            else
                PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.RunLine(from, axis, 13, 2.3f, windup, DamageType.Physical, null));
            yield return new WaitForSeconds(windup);
            if (!CastAlive(interrupted)) yield break;
            if (move == 0)
            {
                SkillEffects.Shockwave(from + axis * 2, 3, DustColor, 0.3f);
                if (CanSpecialHit())
                {
                    Vector3 offset = Flat(player.transform.position - from);
                    if (offset.magnitude < 4.8f && Vector3.Angle(axis, offset) < 65)
                        player.TakeHit(damage * 1.3f, DamageType.Physical);
                }
            }
            else
            {
                bool hit = false;
                for (int i = 1; i <= 6 && CastAlive(interrupted); i++)
                {
                    Vector3 p = from + axis * i * 2;
                    SkillEffects.Shockwave(p, 1.2f, DustColor, 0.25f);
                    if (!hit && CanSpecialHit() && SegmentDistance(p - axis, p + axis, player.transform.position) < 1.2f)
                    {
                        player.TakeHit(damage * 1.2f, DamageType.Physical);
                        hit = true;
                    }
                    yield return new WaitForSeconds(0.07f);
                }
            }
        }

        private static IEnumerator DesertVisual(Transform body, Vector3 target, EnemyKind kind, int useCount)
        {
            Vector3 from = body.position;
            Vector3 axis = FissureDirection(from, target);
            switch (kind.Skill)
            {
                case EnemySkill.DesertCharge:
                    yield return GroundTelegraph.RunLine(from, axis, 9.75f, 2, 0.65f, DamageType.Physical, null);
                    break;
                case EnemySkill.ScorpionSting:
                    body.GetComponentInChildren<CreatureAnimator>()?.Crouch(0.85f);
                    yield return GroundTelegraph.RunLine(from, axis, 4, 1.3f, 0.85f, DamageType.Physical, null);
                    break;
                case EnemySkill.SandBurrow:
                    for (float t = 0; t < 1.1f && body != null; t += 0.12f)
                    {
                        SkillEffects.Shockwave(body.position, 1.1f, DustColor, 0.22f);
                        yield return new WaitForSeconds(0.12f);
                    }
                    if (body != null) yield return GroundTelegraph.Run(body.position, 2.4f, 0.7f, DamageType.Physical, null);
                    break;
                case EnemySkill.JavelinFan:
                    yield return GroundTelegraph.RunCone(from, axis, kind.AttackRange, 24, 0.8f);
                    if (body != null) Volley(body, kind, target, null, 0);
                    break;
                case EnemySkill.RollingSandstorm:
                    SkillEffects.Rise(body, new Color(0.75f, 0.52f, 0.26f), 0.9f);
                    yield return new WaitForSeconds(0.9f);
                    if (body != null) RollingSandstorm.Launch(body.position, body.forward, 0, null, null, true);
                    break;
                case EnemySkill.Tollkeeper:
                    int move = (useCount - 1) % 3;
                    if (move == 0) yield return GroundTelegraph.RunCone(from, axis, 4.8f, 65, 0.9f);
                    else if (move == 1) yield return GroundTelegraph.RunLine(from, axis, 9.75f, 2, 0.65f, DamageType.Physical, null);
                    else
                    {
                        yield return GroundTelegraph.RunLine(from, axis, 13, 2.3f, 0.9f, DamageType.Physical, null);
                        for (int i = 1; i <= 6; i++)
                        {
                            SkillEffects.Shockwave(from + axis * i * 2, 1.2f, DustColor, 0.25f);
                            yield return new WaitForSeconds(0.07f);
                        }
                    }
                    break;
            }
        }

        private static float SegmentDistance(Vector3 from, Vector3 to, Vector3 p)
        {
            Vector3 axis = Flat(to - from);
            float f = Mathf.Clamp01(Vector3.Dot(Flat(p - from), axis) / Mathf.Max(0.001f, axis.sqrMagnitude));
            return Flat(p - from - axis * f).magnitude;
        }

        private static bool InsideDesertLine(Vector3 from, Vector3 axis, float length, float width, Vector3 p)
        {
            Vector3 offset = Flat(p - from);
            float along = Vector3.Dot(offset, axis);
            return along >= 0 && along <= length && Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(axis, Vector3.up))) <= width * 0.5f;
        }
    }
}

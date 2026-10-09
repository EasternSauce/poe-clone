using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// The Sunforged Idol, a four-armed statue holding up a sun. It doesn't walk: it shoots
    /// sunfire, sweeps a beam of sunlight round itself (two at half life, opposite ways), brings
    /// stone hands down out of the sky on the player, flares when they stand too close, and when
    /// they keep away it sinks into the ground and rises again beside them.
    /// </summary>
    public partial class BossAbilities
    {
        private static readonly Color Sunlight = new Color(1f, 0.75f, 0.25f);
        private static readonly Color SunCore = new Color(1f, 0.97f, 0.8f);
        private static readonly Color Sandstone = new Color(0.78f, 0.66f, 0.48f);

        private float nextRelocate;

        private void SetUpSunIdol()
        {
            engageRange = 26f;
            closer = SinkAndRise;
            closerRange = 15f;
            closerEvery = 9f;
            moveEvery = 3.2f;
            moveEveryEnraged = 2.3f;
            Add("Beam", () => Sunbeam(secondPhase ? 2 : 1), 0f, 22f);
            Add("Hands", StoneHands, 0f, 26f);
            Add("Flare", SolarFlare, 0f, 8f);
            Add("Relocate", SinkAndRise, 0f, 26f, allowed: () => Time.time >= nextRelocate);
            enrage = SolarFlare;
        }

        // Light gathers in the sun it holds up, a line marks where the beam will start, then the
        // beam sweeps round, burning whoever it crosses.
        private IEnumerator Sunbeam(int beams)
        {
            float warn = 1.0f / T;
            float sweep = 2.6f / T;
            const float length = 22f;
            const float width = 1.6f;
            Vector3 toPlayer = ToPlayer();
            float sign = Random.value < 0.5f ? 1f : -1f;
            float start = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg - sign * 70f;
            Act(CreatureAnimator.BossAct.Cast, warn + sweep);
            Vector3 center = transform.position;
            for (int b = 0; b < beams; b++)
            {
                Vector3 dir = Quaternion.Euler(0f, start + b * 180f, 0f) * Vector3.forward;
                StartCoroutine(GroundTelegraph.RunLine(center, dir, length, width, warn, DamageType.Fire, null));
            }
            yield return new WaitForSeconds(warn);

            var rays = new Transform[beams];
            for (int b = 0; b < beams; b++)
            {
                GameObject ray = Prop("Sunbeam");
                leftovers.Add(ray);
                Piece(ray.transform, PrimitiveType.Cube, Sunlight, new Vector3(0f, 0f, length * 0.5f), new Vector3(width * 0.8f, 0.9f, length));
                Piece(ray.transform, PrimitiveType.Cube, SunCore, new Vector3(0f, 0f, length * 0.5f), new Vector3(width * 0.35f, 1.05f, length + 0.1f));
                rays[b] = ray.transform;
            }
            float nextBurn = 0f;
            float groundY = Debris.GroundBelow(center + Vector3.up) + 0.6f;
            for (float t = 0f; t < sweep; t += Time.deltaTime)
            {
                float turned = sign * 140f * (t / sweep);
                for (int b = 0; b < beams; b++)
                {
                    // The second beam starts opposite and sweeps the other way.
                    float a = b == 0 ? start + turned : start + 180f - turned;
                    Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                    rays[b].SetPositionAndRotation(new Vector3(center.x, groundY, center.z), Quaternion.LookRotation(dir));
                    if (Time.time >= nextBurn && InStrip(center, dir, length, width))
                    {
                        nextBurn = Time.time + 0.35f;
                        player.TakeHit(DamageOf(0.6f), DamageType.Fire, attack: false);
                    }
                }
                if (Mathf.Repeat(t, 0.2f) < Time.deltaTime)
                    foreach (Transform r in rays)
                        SkillEffects.Shockwave(r.position + r.forward * Random.Range(4f, length), 1f, Sunlight, 0.25f);
                yield return null;
            }
            foreach (Transform r in rays)
                if (r != null)
                    Destroy(r.gameObject);
            yield return Pause(0.3f);
        }

        // Stone hands fall out of the sky one after another onto wherever the player is.
        private IEnumerator StoneHands()
        {
            Act(CreatureAnimator.BossAct.Slam, 1.0f / T);
            int count = secondPhase ? 5 : 3;
            const float radius = 2.6f;
            for (int k = 0; k < count; k++)
            {
                Vector3 at = OpenNear(PlayerAt);
                float windUp = 1.0f / T;
                StartCoroutine(FallingHand(at, windUp));
                StartCoroutine(Eruption(at, radius, windUp, DamageType.Physical, 1.3f, burst: spot => CameraSystem.CameraFollow.Shake(0.2f, 0.25f)));
                yield return new WaitForSeconds(0.45f / T);
            }
            yield return Pause(0.4f);
        }

        private IEnumerator FallingHand(Vector3 at, float windUp)
        {
            const float fall = 0.28f;
            yield return new WaitForSeconds(Mathf.Max(0f, windUp - fall));
            GameObject hand = Prop("StoneHand");
            leftovers.Add(hand);
            Piece(hand.transform, PrimitiveType.Cube, Sandstone, Vector3.zero, new Vector3(2.2f, 0.7f, 2.4f));
            for (int f = 0; f < 4; f++)
                Piece(hand.transform, PrimitiveType.Cube, Sandstone, new Vector3(-0.8f + f * 0.53f, -0.1f, 1.7f), new Vector3(0.42f, 0.5f, 1.2f));
            Piece(hand.transform, PrimitiveType.Cube, Sandstone, new Vector3(1.3f, -0.1f, 0.2f), new Vector3(0.45f, 0.5f, 1.1f), new Vector3(0f, -40f, 0f));
            Piece(hand.transform, PrimitiveType.Cube, new Color(0.95f, 0.75f, 0.25f), new Vector3(0f, 0.36f, -1.1f), new Vector3(2.0f, 0.1f, 0.3f));
            hand.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float ground = Debris.GroundBelow(at + Vector3.up);
            Vector3 from = new Vector3(at.x, ground + 14f, at.z);
            Vector3 to = new Vector3(at.x, ground + 0.35f, at.z);
            for (float t = 0f; t < fall && hand != null; t += Time.deltaTime)
            {
                hand.transform.position = Vector3.Lerp(from, to, t / fall);
                yield return null;
            }
            if (hand == null)
                yield break;
            hand.transform.position = to;
            yield return new WaitForSeconds(0.7f);
            for (float t = 0f; t < 0.6f && hand != null; t += Time.deltaTime)
            {
                hand.transform.position = to + Vector3.down * 1.5f * (t / 0.6f);
                yield return null;
            }
            if (hand != null)
                Destroy(hand);
        }

        // Too close: the sun flares, burning the ground all round the statue.
        private IEnumerator SolarFlare()
        {
            float windUp = 1.0f / T;
            float radius = 3f + 1.5f * Size;
            Act(CreatureAnimator.BossAct.Cast, windUp + 0.3f);
            yield return Eruption(transform.position, radius, windUp, DamageType.Fire, 1.4f, burst: at =>
            {
                SkillEffects.Blast(transform.position + Vector3.up * 3f * Size, 3f, Sunlight, 0.4f);
                CameraSystem.CameraFollow.Shake(0.25f, 0.35f);
            });
            yield return Pause(0.3f);
        }

        // Sinks into the ground, plinth and all, and rises again near the player, throwing them back.
        private IEnumerator SinkAndRise()
        {
            nextRelocate = Time.time + 14f;
            health.Immune = true;
            float sink = 1.0f / T;
            Act(CreatureAnimator.BossAct.Raise, sink);
            for (float t = 0f; t < sink; t += Time.deltaTime)
            {
                if (anim != null) anim.Sunk = t / sink;
                if (Mathf.Repeat(t, 0.2f) < Time.deltaTime)
                    SkillEffects.Shockwave(transform.position, 2f + Size, Dust, 0.35f);
                yield return null;
            }
            SetVisible(false);
            Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 target = OpenNear(PlayerAt + dir * 7f);
            Teleport(target);
            float radius = 2.4f + 1.2f * Size;
            bool risen = false;
            StartCoroutine(GroundTelegraph.Run(transform.position, radius, 1.1f / T, DamageType.Fire, at =>
            {
                if (this == null || health.IsDead)
                    return;
                risen = true;
                if (HitIfInside(at, radius, 1.4f, DamageType.Fire))
                    StartCoroutine(KnockPlayer(player.transform.position - at, 4f));
                SkillEffects.Shockwave(at, radius, Dust, 0.45f);
                CameraSystem.CameraFollow.Shake(0.35f, 0.45f);
            }, kind));
            while (!risen && !health.IsDead)
                yield return null;
            SetVisible(true);
            health.Immune = false;
            float rise = 0.5f / T;
            for (float t = 0f; t < rise; t += Time.deltaTime)
            {
                if (anim != null) anim.Sunk = 1f - t / rise;
                yield return null;
            }
            if (anim != null) anim.Sunk = 0f;
            Face(ToPlayer());
            yield return Pause(0.3f);
        }
    }
}

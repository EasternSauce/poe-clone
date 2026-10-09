using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Rimeheart, a heart of ice floating in a cage of shards. It keeps its distance and shoots;
    /// its moves: a pulse of frost rolling out round it in a ring, with gaps to slip through;
    /// shards spiralling out of it; mirrors of ice that rise round the player and shatter; ice
    /// lances falling where the player stands. At half life it pulses twice and its brood of ice
    /// crawlers starts to hatch.
    /// </summary>
    public partial class BossAbilities
    {
        private static readonly Color Rime = new Color(0.6f, 0.88f, 1f);
        private static readonly Color DeepRime = new Color(0.25f, 0.5f, 0.95f);

        private void SetUpRimeheart()
        {
            engageRange = 21f;
            moveEvery = 3.2f;
            moveEveryEnraged = 2.2f;
            calmMaxMinions = 0;
            enragedMaxMinions = 3;
            Add("Pulse", () => GlacialPulse(secondPhase ? 2 : 1), 0f, 17f);
            Add("Spiral", ShardSpiral, 0f, 16f);
            Add("Mirrors", IceMirrors, 0f, 19f);
            Add("Lances", FrostLances, 4f, 21f);
            Add("Brood", HatchBrood, 0f, 21f, secondPhaseOnly: true, allowed: () => LiveMinions() < CurrentMaxMinions);
            enrage = () => GlacialPulse(2);
        }

        private Vector3 HeartAt => EnemyCombat.BoltOrigin(transform);

        // Gathers the cold into itself, then lets it out: rings of frost roll outwards, each with
        // two gaps to slip through.
        private IEnumerator GlacialPulse(int rings)
        {
            float windUp = 0.9f / T;
            Act(CreatureAnimator.BossAct.Cast, windUp + 0.4f);
            SkillEffects.Shockwave(transform.position, 2.5f, Rime, windUp);
            yield return new WaitForSeconds(windUp);
            for (int r = 0; r < rings; r++)
            {
                float first = Random.Range(0f, 360f);
                float[] gaps = { first, first + Random.Range(120f, 240f) };
                StartCoroutine(Wave(transform.position, 17f, 6.5f, 1.4f, gaps, 50f, DamageType.Cold, 1.2f, Rime,
                    p => p.GetComponent<Player.PlayerController>()?.Chill(2.5f)));
                if (r + 1 < rings)
                    yield return new WaitForSeconds(1.0f / T);
            }
            yield return Pause(0.4f);
        }

        // Shards spiral out of it in arms that turn as it spins.
        private IEnumerator ShardSpiral()
        {
            float seconds = 1.6f / T;
            int arms = secondPhase ? 4 : 3;
            Act(CreatureAnimator.BossAct.Cast, seconds + 0.3f);
            float start = Random.Range(0f, 360f);
            float turn = Random.value < 0.5f ? 200f : -200f;
            float every = seconds / 10f;
            int volley = 0;
            for (float t = 0f; t < seconds; t += every)
            {
                for (int a = 0; a < arms; a++)
                {
                    float angle = start + a * 360f / arms + turn * (t / seconds);
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    GameObject shard = Prop("FrostShard");
                    Piece(shard.transform, PrimitiveType.Cube, volley % 2 == 0 ? Rime : DeepRime, Vector3.zero, new Vector3(0.18f, 0.18f, 0.7f), new Vector3(0f, 0f, 45f));
                    Vector3 from = Flat(HeartAt) + Vector3.up * (transform.position.y + 0.2f);
                    StartCoroutine(Missile(shard, from, dir, 9f, 17f, 0.75f, DamageType.Cold, 0.55f,
                        p => p.GetComponent<Player.PlayerController>()?.Chill(1.2f)));
                }
                volley++;
                yield return new WaitForSeconds(every);
            }
            yield return Pause(0.3f);
        }

        // Ice rises out of the ground round the player in the shape of the heart, and shatters.
        private IEnumerator IceMirrors()
        {
            Act(CreatureAnimator.BossAct.Cast, 0.8f / T);
            int count = secondPhase ? 3 : 2;
            float windUp = 1.8f / T;
            const float radius = 3f;
            float baseAngle = Random.Range(0f, 360f);
            Vector3 center = PlayerAt;
            for (int k = 0; k < count; k++)
            {
                Vector3 at = OpenNear(center + Quaternion.Euler(0f, baseAngle + k * 360f / count, 0f) * Vector3.forward * 4.5f);
                GameObject mirror = Mirror(at);
                StartCoroutine(GroundTelegraph.Run(at, radius, windUp, DamageType.Cold, spot =>
                {
                    if (mirror != null)
                        Destroy(mirror);
                    if (this == null || health.IsDead)
                        return;
                    SkillEffects.Blast(spot + Vector3.up, 2f, Rime, 0.35f);
                    if (HitIfInside(spot, radius, 1.3f, DamageType.Cold))
                        playerMotion?.Chill(2f);
                }, kind));
            }
            yield return Pause(0.6f);
        }

        // A tall crystal shaped like the heart, rising out of the ground.
        private GameObject Mirror(Vector3 at)
        {
            GameObject mirror = Prop("IceMirror");
            leftovers.Add(mirror);
            at.y = Debris.GroundBelow(at + Vector3.up);
            mirror.transform.position = at;
            Piece(mirror.transform, PrimitiveType.Cube, Rime, new Vector3(0f, 1.1f, 0f), new Vector3(0.7f, 2.2f, 0.7f), new Vector3(0f, 45f, 0f));
            Piece(mirror.transform, PrimitiveType.Sphere, DeepRime, new Vector3(0f, 2.4f, 0f), new Vector3(0.9f, 1.0f, 0.8f));
            Piece(mirror.transform, PrimitiveType.Sphere, new Color(0.7f, 1f, 1f), new Vector3(0f, 2.4f, 0.3f), Vector3.one * 0.35f);
            for (int i = 0; i < 4; i++)
                Piece(mirror.transform, PrimitiveType.Cube, Rime, new Vector3(Mathf.Cos(i * 1.57f) * 0.5f, 0.5f, Mathf.Sin(i * 1.57f) * 0.5f),
                    new Vector3(0.25f, 1f, 0.25f), new Vector3(Mathf.Sin(i * 1.57f) * 25f, 0f, -Mathf.Cos(i * 1.57f) * 25f));
            StartCoroutine(RiseUp(mirror.transform, 0.5f));
            return mirror;
        }

        private IEnumerator RiseUp(Transform thing, float seconds)
        {
            if (thing == null)
                yield break;
            Vector3 top = thing.position;
            for (float t = 0f; t < seconds && thing != null; t += Time.deltaTime)
            {
                thing.position = top + Vector3.down * 2.5f * (1f - t / seconds);
                yield return null;
            }
            if (thing != null)
                thing.position = top;
        }

        // Lances of ice fall one after another round where the player stands.
        private IEnumerator FrostLances()
        {
            Act(CreatureAnimator.BossAct.Raise, 0.8f / T);
            int count = secondPhase ? 8 : 6;
            Vector3 target = PlayerAt;
            for (int k = 0; k < count; k++)
            {
                float windUp = (0.95f + k * 0.14f) / T;
                Vector2 scatter = Random.insideUnitCircle * 3.2f;
                Vector3 at = k == 0 ? target : OpenNear(target + new Vector3(scatter.x, 0f, scatter.y));
                const float radius = 1.9f;
                StartCoroutine(FallingLance(at, windUp));
                StartCoroutine(Eruption(at, radius, windUp, DamageType.Cold, 1.0f));
            }
            yield return Pause(0.5f);
        }

        private IEnumerator FallingLance(Vector3 at, float windUp)
        {
            const float fall = 0.22f;
            yield return new WaitForSeconds(Mathf.Max(0f, windUp - fall));
            GameObject lance = Prop("IceLance");
            leftovers.Add(lance);
            Piece(lance.transform, PrimitiveType.Cube, Rime, Vector3.zero, new Vector3(0.35f, 2.4f, 0.35f), new Vector3(0f, 45f, 0f));
            Piece(lance.transform, PrimitiveType.Cube, DeepRime, new Vector3(0f, -1.3f, 0f), new Vector3(0.2f, 0.5f, 0.2f), new Vector3(0f, 45f, 0f));
            float ground = Debris.GroundBelow(at + Vector3.up);
            Vector3 from = new Vector3(at.x, ground + 12f, at.z);
            Vector3 to = new Vector3(at.x, ground + 0.9f, at.z);
            for (float t = 0f; t < fall && lance != null; t += Time.deltaTime)
            {
                lance.transform.position = Vector3.Lerp(from, to, t / fall);
                yield return null;
            }
            if (lance != null)
            {
                lance.transform.position = to;
                lance.transform.rotation = Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 90f), Random.Range(-12f, 12f));
                Destroy(lance, 1.2f);
            }
        }

        private IEnumerator HatchBrood()
        {
            Act(CreatureAnimator.BossAct.Roar, 0.8f / T);
            yield return Pause(0.8f);
            Summon(EnemyKinds.IndexOf("Ice Crawler"), 2, Rime);
        }
    }
}

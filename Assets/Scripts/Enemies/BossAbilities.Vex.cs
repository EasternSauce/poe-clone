using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Vex, the Tunnel King: small, quick, and never where you left him. He throws a smoke bomb at
    /// his feet and comes out of it behind the player, knife first; scatters tripwire snares that
    /// burst and hold whoever steps on them; lobs spore mushrooms that leave slowing clouds; throws
    /// fans of knives. At half life he rings himself with snares and his tricks come faster.
    /// </summary>
    public partial class BossAbilities
    {
        private static readonly Color Smoke = new Color(0.45f, 0.45f, 0.48f);
        private static readonly Color Copper = new Color(0.82f, 0.55f, 0.24f);
        private static readonly Color Spore = new Color(0.62f, 0.5f, 0.72f);
        private static readonly Color Knife = new Color(0.7f, 0.72f, 0.76f);

        private const int MaxTraps = 7;
        private const float TrapLife = 14f;

        private sealed class Snare
        {
            public GameObject Go;
            public float ArmedAt;
            public float GoneAt;
        }

        private readonly List<Snare> snares = new List<Snare>();

        private void SetUpVex()
        {
            closer = SmokeStep;
            closerRange = 7f;
            closerEvery = 4.5f;
            moveEvery = 2.4f;
            moveEveryEnraged = 1.6f;
            Add("Backstab", SmokeBackstab, 0f, 14f);
            Add("Snares", () => Snares(3, PlayerAt, 4.5f), 0f, 14f, allowed: () => snares.Count <= MaxTraps - 3);
            Add("Spores", SporeLob, 3f, 15f);
            Add("Knives", () => KnifeFan(secondPhase ? 7 : 5), 2.5f, 15f);
            enrage = VexRings;
        }

        private IEnumerator VexRings()
        {
            yield return Snares(4, transform.position, 3.5f);
        }

        private void SmokeBomb(Vector3 at)
        {
            GameObject smoke = Prop("SmokeBomb");
            leftovers.Add(smoke);
            at.y = Debris.GroundBelow(at + Vector3.up);
            smoke.transform.position = at;
            for (int i = 0; i < 7; i++)
            {
                Vector2 r = Random.insideUnitCircle * 1.1f;
                Piece(smoke.transform, PrimitiveType.Sphere, i % 2 == 0 ? Smoke : Color.Lerp(Smoke, Color.white, 0.25f),
                    new Vector3(r.x, 0.6f + Random.value * 0.8f, r.y), Vector3.one * Random.Range(0.9f, 1.5f));
            }
            StartCoroutine(Billow(smoke.transform, 1.6f));
            SkillEffects.Shockwave(at, 2f, Smoke, 0.4f);
        }

        private IEnumerator Billow(Transform smoke, float seconds)
        {
            for (float t = 0f; t < seconds && smoke != null; t += Time.deltaTime)
            {
                float f = t / seconds;
                smoke.localScale = Vector3.one * (1f + f * 0.8f) * (f > 0.7f ? 1f - (f - 0.7f) / 0.3f : 1f);
                smoke.position += Vector3.up * Time.deltaTime * 0.4f;
                yield return null;
            }
            if (smoke != null)
                Destroy(smoke.gameObject);
        }

        // Vanishes in smoke and steps out of it beside the player (his way of closing in).
        private IEnumerator SmokeStep()
        {
            yield return Vanish(0.5f);
            Vector3 side = Vector3.Cross(Vector3.up, ToPlayer()) * (Random.value < 0.5f ? 1f : -1f);
            yield return Reappear(PlayerAt + side * 2.6f);
            yield return Pause(0.2f);
        }

        private IEnumerator Vanish(float seconds)
        {
            SmokeBomb(transform.position);
            health.Immune = true;
            SetVisible(false);
            yield return new WaitForSeconds(seconds / T);
        }

        private IEnumerator Reappear(Vector3 at)
        {
            float gap = (body != null ? body.radius * Size : 0.6f) + 0.7f;
            Teleport(OpenNear(KeepClear(at, gap)));
            Face(ToPlayer());
            SmokeBomb(transform.position);
            SetVisible(true);
            health.Immune = false;
            yield return null;
        }

        // Smoke, then he is behind the player with his knives raised: get out of the ring.
        private IEnumerator SmokeBackstab()
        {
            yield return Vanish(secondPhase ? 0.6f : 0.85f);
            Transform victim = aim != null ? aim : player != null ? player.transform : null;
            Vector3 behind = PlayerAt - (victim != null ? Flat(victim.forward) : Vector3.forward) * 2.2f;
            yield return Reappear(behind);
            float windUp = 0.5f / T;
            const float radius = 2.4f;
            Act(CreatureAnimator.BossAct.Slam, windUp * 1.15f);
            yield return Eruption(transform.position, radius, windUp, DamageType.Physical, 1.4f,
                burst: at => SkillEffects.Shockwave(at, radius, Knife, 0.25f));
            yield return Pause(0.3f);
        }

        // Tosses snares round a point; each arms after a moment and bursts when stepped on.
        private IEnumerator Snares(int count, Vector3 around, float spread)
        {
            Act(CreatureAnimator.BossAct.Throw, 0.5f / T);
            yield return new WaitForSeconds(0.35f / T);
            float baseAngle = Random.Range(0f, 360f);
            for (int k = 0; k < count; k++)
            {
                Vector3 at = OpenNear(around + Quaternion.Euler(0f, baseAngle + k * 360f / count, 0f) * Vector3.forward * Random.Range(spread * 0.5f, spread));
                GameObject trap = SnareProp();
                StartCoroutine(Lob(trap, transform.position + Vector3.up, at, 0.5f / T, 1.1f, DamageType.Physical, spot =>
                {
                    trap.transform.position = spot + Vector3.up * 0.05f;
                    trap.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    snares.Add(new Snare { Go = trap, ArmedAt = Time.time + 0.6f, GoneAt = Time.time + TrapLife });
                }, keep: true));
            }
            yield return Pause(0.25f);
        }

        private GameObject SnareProp()
        {
            GameObject trap = Prop("Snare");
            Piece(trap.transform, PrimitiveType.Cylinder, new Color(0.15f, 0.12f, 0.1f), Vector3.zero, new Vector3(0.8f, 0.04f, 0.8f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Piece(trap.transform, PrimitiveType.Cube, Copper, new Vector3(Mathf.Cos(a) * 0.35f, 0.12f, Mathf.Sin(a) * 0.35f), new Vector3(0.05f, 0.22f, 0.05f),
                    new Vector3(Mathf.Sin(a) * 30f, 0f, -Mathf.Cos(a) * 30f));
            }
            Piece(trap.transform, PrimitiveType.Sphere, new Color(1f, 0.3f, 0.15f), Vector3.up * 0.08f, Vector3.one * 0.14f);
            return trap;
        }

        private void TickVex()
        {
            for (int i = snares.Count - 1; i >= 0; i--)
            {
                Snare s = snares[i];
                if (s.Go == null)
                {
                    snares.RemoveAt(i);
                    continue;
                }
                if (Time.time >= s.GoneAt || health.IsDead)
                {
                    Destroy(s.Go);
                    snares.RemoveAt(i);
                    continue;
                }
                if (Time.time < s.ArmedAt || player == null || player.IsDead)
                    continue;
                if (Flat(player.transform.position - s.Go.transform.position).magnitude > 1.1f)
                    continue;
                // Sprung.
                Vector3 at = s.Go.transform.position;
                Destroy(s.Go);
                snares.RemoveAt(i);
                SkillEffects.Blast(at + Vector3.up * 0.3f, 1.4f, Copper, 0.3f);
                Audio.AudioManager.Instance?.PlayEffect("combat.ground." + DamageType.Physical, at);
                if (HitIfInside(at, 2f, 1.0f, DamageType.Physical))
                {
                    playerMotion?.Root(0.9f);
                    CombatText.Show(player.transform.position + Vector3.up * 2.2f, "Snared!", Copper, 1f);
                }
            }
        }

        // Lobs spore mushrooms round the player; where they burst a slowing cloud lingers.
        private IEnumerator SporeLob()
        {
            Act(CreatureAnimator.BossAct.Throw, 0.55f / T);
            Face(ToPlayer());
            yield return new WaitForSeconds(0.4f / T);
            Vector3 center = PlayerAt;
            int count = secondPhase ? 4 : 3;
            for (int k = 0; k < count; k++)
            {
                Vector2 scatter = Random.insideUnitCircle * 3.5f;
                Vector3 at = k == 0 ? center : OpenNear(center + new Vector3(scatter.x, 0f, scatter.y));
                GameObject shroom = Prop("SporeShroom");
                Piece(shroom.transform, PrimitiveType.Cylinder, new Color(0.85f, 0.82f, 0.7f), Vector3.zero, new Vector3(0.14f, 0.15f, 0.14f));
                Piece(shroom.transform, PrimitiveType.Sphere, new Color(0.7f, 0.2f, 0.45f), new Vector3(0f, 0.17f, 0f), new Vector3(0.42f, 0.22f, 0.42f));
                const float radius = 2.3f;
                StartCoroutine(Lob(shroom, transform.position + Vector3.up, at, (0.7f + k * 0.1f) / T, radius, DamageType.Physical, spot =>
                {
                    HitIfInside(spot, radius, 0.5f, DamageType.Physical);
                    SkillEffects.Blast(spot + Vector3.up * 0.4f, 1.6f, Spore, 0.4f);
                    Patch(spot, radius, 4.5f, Spore, p =>
                    {
                        p.GetComponent<Player.PlayerController>()?.Chill(0.7f);
                        p.TakeHit(DamageOf(0.15f), DamageType.Physical, attack: false);
                    });
                }));
            }
            yield return Pause(0.3f);
        }

        // A flick of both wrists: a fan of knives.
        private IEnumerator KnifeFan(int count)
        {
            float windUp = 0.45f / T;
            Vector3 dir = ToPlayer();
            Face(dir);
            Act(CreatureAnimator.BossAct.Throw, windUp);
            StartCoroutine(GroundTelegraph.RunCone(transform.position, dir, 6f, 28f, windUp, () => !health.IsDead));
            yield return new WaitForSeconds(windUp);
            for (int k = 0; k < count; k++)
            {
                float angle = (k - (count - 1) * 0.5f) * (56f / Mathf.Max(1, count - 1));
                GameObject knife = Prop("ThrownKnife");
                Piece(knife.transform, PrimitiveType.Cube, Knife, Vector3.zero, new Vector3(0.06f, 0.03f, 0.45f));
                Piece(knife.transform, PrimitiveType.Cube, new Color(0.3f, 0.2f, 0.12f), new Vector3(0f, 0f, -0.28f), new Vector3(0.06f, 0.06f, 0.14f));
                StartCoroutine(Missile(knife, transform.position + Vector3.up * 0.3f, Quaternion.Euler(0f, angle, 0f) * dir, 17f, 15f, 0.6f,
                    DamageType.Physical, 0.75f));
            }
            yield return Pause(0.3f);
        }
    }
}

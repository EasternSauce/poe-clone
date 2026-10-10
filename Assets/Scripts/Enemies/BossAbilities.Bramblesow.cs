using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Bramblesow, a boar the size of a cart with a thicket growing on its back. Greenwood's boss,
    /// so its moves are few and plain: it paws the ground and charges down a marked line (if it
    /// rams a tree or a wall it is dazed a moment, wide open); roots burst out of the ground round
    /// the player and hold them; it shakes itself and thorns fly off its back. At half life it
    /// charges twice in a row.
    /// </summary>
    public partial class BossAbilities
    {
        private static readonly Color Root = new Color(0.36f, 0.25f, 0.15f);
        private static readonly Color Thorn = new Color(0.45f, 0.36f, 0.22f);
        private static readonly Color Leaf = new Color(0.3f, 0.5f, 0.2f);

        private void SetUpBramblesow()
        {
            closer = () => Charge(secondPhase ? 2 : 1);
            closerRange = 8f;
            closerEvery = 6f;
            moveEvery = 3.4f;
            moveEveryEnraged = 2.6f;
            Add("Charge", () => Charge(secondPhase ? 2 : 1), 5f, 20f);
            Add("Roots", RootBurst, 0f, 16f);
            Add("Thorns", ThornShake, 0f, 7f);
        }

        // Paws the ground with its head low while its path is marked, then charges down it.
        private IEnumerator Charge(int times)
        {
            for (int n = 0; n < times; n++)
            {
                if (!AimAlive)
                    yield break;
                Vector3 dir = ToPlayer();
                Face(dir);
                float distance = Flat(AimPosition - transform.position).magnitude;
                float length = Mathf.Clamp(distance + 6f, 10f, 24f);
                float width = 1.4f * Size;
                float windUp = (n == 0 ? 1.1f : 0.75f) / T;
                Act(CreatureAnimator.BossAct.Paw, windUp);
                StartCoroutine(GroundTelegraph.RunLine(transform.position, dir, length, width, windUp, DamageType.Physical, null));
                for (float t = 0f; t < windUp; t += Time.deltaTime)
                {
                    if (Mathf.Repeat(t, 0.3f) < Time.deltaTime)
                        SkillEffects.Shockwave(transform.position + dir * Size * 0.8f, 0.8f, Dust, 0.25f);
                    yield return null;
                }

                Act(CreatureAnimator.BossAct.Charge, 2f);
                const float speed = 19f;
                bool hit = false;
                bool rammed = false;
                Vector3 start = transform.position;
                float giveUp = Time.time + length / speed + 1f;
                while (Flat(transform.position - start).magnitude < length && Time.time < giveUp)
                {
                    Vector3 before = transform.position;
                    Vector3 step = dir * speed * Time.deltaTime;
                    // It bowls the player aside, never climbs over them.
                    float clear = (body != null ? body.radius * Size : 1f) + 0.7f;
                    if (hit && player != null && Flat(player.transform.position - (before + step)).magnitude < clear)
                        break;
                    MoveTo(before + step);
                    float moved = Flat(transform.position - before).magnitude;
                    if (!hit && player != null && !player.IsDead && Flat(player.transform.position - transform.position).magnitude < width * 0.5f + 0.9f)
                    {
                        hit = true;
                        if (player.TakeHit(DamageOf(1.8f), DamageType.Physical, attack: true))
                        {
                            Vector3 side = Vector3.Cross(Vector3.up, dir);
                            if (Vector3.Dot(Flat(player.transform.position - transform.position), side) < 0f)
                                side = -side;
                            StartCoroutine(KnockPlayer(side + dir * 0.5f, 3.5f));
                        }
                    }
                    // Stopped short by something solid (not the player it is bowling over).
                    bool byPlayer = (player != null && Flat(player.transform.position - transform.position).magnitude < width * 0.5f + 2.5f) ||
                                    (aim != null && Flat(aim.position - transform.position).magnitude < width * 0.5f + 2.5f);
                    if (Time.deltaTime > 0f && moved < step.magnitude * 0.3f && !byPlayer)
                    {
                        rammed = true;
                        break;
                    }
                    if (Mathf.Repeat(Time.time, 0.12f) < Time.deltaTime)
                        SkillEffects.Shockwave(transform.position, 1.2f, Dust, 0.25f);
                    yield return null;
                }
                if (anim != null)
                    anim.EndAct();

                if (rammed)
                {
                    // Rammed something solid: dazed, wide open.
                    CameraSystem.CameraFollow.Shake(0.35f, 0.4f);
                    SkillEffects.Shockwave(transform.position + dir * Size, 2.5f, Dust, 0.45f);
                    CombatText.Show(transform.position + Vector3.up * health.BarHeight * Size, "Dazed!", new Color(1f, 0.9f, 0.5f), 1.2f);
                    StartCoroutine(DazedStars(2.2f / T));
                    yield return new WaitForSeconds(2.2f / T);
                    yield break;
                }
                yield return Pause(0.35f);
            }
        }

        private IEnumerator DazedStars(float seconds)
        {
            GameObject stars = Prop("DazedStars");
            leftovers.Add(stars);
            for (int i = 0; i < 4; i++)
                Piece(stars.transform, PrimitiveType.Sphere, new Color(1f, 0.92f, 0.4f), new Vector3(Mathf.Cos(i * 1.57f), 0f, Mathf.Sin(i * 1.57f)) * 0.6f, Vector3.one * 0.18f);
            for (float t = 0f; t < seconds && stars != null; t += Time.deltaTime)
            {
                stars.transform.position = transform.position + Vector3.up * (health.BarHeight * Size * 0.8f);
                stars.transform.rotation = Quaternion.Euler(0f, t * 300f, 0f);
                yield return null;
            }
            if (stars != null)
                Destroy(stars);
        }

        // Stamps, and roots burst up round the player one after another; any that catch them hold on.
        private IEnumerator RootBurst()
        {
            Act(CreatureAnimator.BossAct.Roar, 0.7f / T);
            int count = secondPhase ? 6 : 4;
            Vector3 target = PlayerAt;
            for (int k = 0; k < count; k++)
            {
                Vector2 scatter = Random.insideUnitCircle * 3.5f;
                Vector3 at = k == 0 ? target : OpenNear(target + new Vector3(scatter.x, 0f, scatter.y));
                const float radius = 1.8f;
                StartCoroutine(GroundTelegraph.Run(at, radius, (1.0f + k * 0.18f) / T, DamageType.Physical, spot =>
                {
                    if (this == null || health.IsDead)
                        return;
                    Roots(spot);
                    if (HitIfInside(spot, radius, 1.1f, DamageType.Physical))
                    {
                        playerMotion?.Root(0.8f);
                        CombatText.Show(player.transform.position + Vector3.up * 2.2f, "Rooted!", Leaf, 1f);
                    }
                }, kind));
            }
            yield return Pause(0.6f);
        }

        private void Roots(Vector3 at)
        {
            GameObject roots = Prop("Roots");
            leftovers.Add(roots);
            at.y = Debris.GroundBelow(at + Vector3.up);
            roots.transform.position = at;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f + Random.value;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(0.3f, 1.3f);
                float h = Random.Range(0.9f, 1.8f);
                Piece(roots.transform, PrimitiveType.Cube, i % 3 == 0 ? Thorn : Root, p + Vector3.up * h * 0.4f, new Vector3(0.18f, h, 0.18f),
                    new Vector3(Random.Range(-35f, 35f), Random.Range(0f, 90f), Random.Range(-35f, 35f)));
            }
            Piece(roots.transform, PrimitiveType.Sphere, Leaf, Vector3.up * 0.2f, new Vector3(1.2f, 0.4f, 1.2f));
            StartCoroutine(RiseUp(roots.transform, 0.15f));
            Destroy(roots, 1.3f);
        }

        // Shakes itself like a wet dog: thorns fly off its back all round.
        private IEnumerator ThornShake()
        {
            float windUp = 0.9f / T;
            float radius = 2.4f + 1.6f * Size;
            Act(CreatureAnimator.BossAct.Shake, windUp + 0.3f / T);
            StartCoroutine(Eruption(transform.position, radius, windUp, DamageType.Physical, 1.2f));
            yield return new WaitForSeconds(windUp);
            int count = secondPhase ? 16 : 10;
            float offset = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, offset + i * 360f / count, 0f) * Vector3.forward;
                GameObject thorn = Prop("Thorn");
                Piece(thorn.transform, PrimitiveType.Cube, Thorn, Vector3.zero, new Vector3(0.12f, 0.12f, 0.8f));
                Piece(thorn.transform, PrimitiveType.Sphere, Leaf, new Vector3(0f, 0f, -0.35f), Vector3.one * 0.18f);
                StartCoroutine(Missile(thorn, transform.position + Vector3.up * 0.2f, dir, 13f, 13f, 0.65f, DamageType.Physical, 0.5f));
            }
            yield return Pause(0.4f);
        }
    }
}

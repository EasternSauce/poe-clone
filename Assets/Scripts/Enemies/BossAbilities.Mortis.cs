using System.Collections;
using UnityEngine;
using PoeClone.Combat;
using PoeClone.Skills;
using PoeClone.UI;
using PoeClone.Visuals;

namespace PoeClone.Enemies
{
    /// <summary>
    /// Gravelord Mortis, the gravedigger: burrows under the ground and bursts up beneath the
    /// player; opens graves under them whose hands hold them fast; throws the coffin off his back
    /// (the dead climb out of it); drags his shovel to split the ground in a line; sweeps it round.
    /// At half life the dead rise round him and graves open all about.
    /// </summary>
    public partial class BossAbilities
    {
        private static readonly Color GraveLight = new Color(0.3f, 0.9f, 0.4f);
        private static readonly Color Earth = new Color(0.30f, 0.23f, 0.16f);
        private static readonly Color DeadHand = new Color(0.55f, 0.6f, 0.5f);

        private bool coffinOnBack = true;

        private void SetUpMortis()
        {
            closer = Burrow;
            closerRange = 7f;
            closerEvery = 5.5f;
            calmMaxMinions = 2;
            enragedMaxMinions = 4;
            Add("Grave", () => OpenGraves(secondPhase ? 3 : 1), 0f, 14f);
            Add("Coffin", CoffinToss, 3f, 16f, allowed: () => coffinOnBack && LiveMinions() < CurrentMaxMinions);
            Add("Exhume", Exhume, 0f, 12f);
            Add("Sweep", ShovelSweep, 0f, 5f);
            enrage = MortisRises;
        }

        private IEnumerator MortisRises()
        {
            Summon(0, 3, GraveLight);
            yield return OpenGraves(3);
        }

        // Digs in: drives the shovel into the ground, flings a spadeful over his shoulder and dives
        // into the hole. A mound races to where the player stood, his shovel blade cutting the
        // earth ahead of it like a fin, and he bursts up out of the ground there.
        private IEnumerator Burrow()
        {
            Face(ToPlayer());
            float started = Time.time;
            anim?.PlayClip(GravediggerClips.Burrow, T);
            Vector3 blade = transform.position + transform.forward * 1.1f * Size;

            yield return UntilClip(started, GravediggerClips.BurrowPlunge);
            SkillEffects.Shockwave(blade, 0.6f * Size, Earth, 0.3f);
            Clods(blade, 6, 3f, Vector3.zero);
            CameraSystem.CameraFollow.Shake(0.12f, 0.2f);

            yield return UntilClip(started, GravediggerClips.BurrowFling);
            Clods(blade, 10, 5f, -transform.forward + transform.right * 0.4f);

            yield return UntilClip(started, GravediggerClips.BurrowDive);
            health.Immune = true;
            Clods(blade, 12, 4f, Vector3.zero);
            SkillEffects.Shockwave(blade, 0.9f * Size, Earth, 0.35f);
            float sinkFrom = Time.time;
            float sink = (GravediggerClips.BurrowUnder - GravediggerClips.BurrowDive) / T;
            for (float t = 0f; t < sink; t = Time.time - sinkFrom)
            {
                if (anim != null) anim.Sunk = t / sink;
                yield return null;
            }
            SetVisible(false);
            anim?.StopClip();

            Vector3 target = OpenNear(PlayerAt);
            float travel = Mathf.Clamp(Flat(target - transform.position).magnitude / 16f, 0.35f, 1.1f);
            float radius = 3f;
            bool risen = false;
            PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.Run(target, radius, travel + 0.55f / T, DamageType.Physical, at =>
            {
                if (this == null || health.IsDead)
                    return;
                risen = true;
                HitIfInside(at, radius, 1.5f, DamageType.Physical);
                SkillEffects.Shockwave(at, radius, Earth, 0.45f);
                CameraSystem.CameraFollow.Shake(0.3f, 0.4f);
            }, kind));

            Molehill mound = BurrowMound();
            Vector3 from = Flat(transform.position);
            Vector3 heading = Flat(target - transform.position).sqrMagnitude > 0.01f ? Flat(target - transform.position).normalized : transform.forward;
            mound.Root.rotation = Quaternion.LookRotation(heading);
            float nextSpray = 0f, nextFurrow = 0f;
            for (float t = 0f; t < travel; t += Time.deltaTime)
            {
                Vector3 p = Vector3.Lerp(from, Flat(target), t / travel);
                mound.Place(p, t, 1f);
                if (t >= nextSpray)
                {
                    nextSpray = t + 0.07f;
                    Clods(mound.Root.position, 2, 3f, Quaternion.Euler(0f, Roll(-60f, 60f) + (Cosmetic.Next(2) == 0 ? 100f : -100f), 0f) * heading, 0.7f);
                }
                if (t >= nextFurrow)
                {
                    nextFurrow = t + 0.05f;
                    Furrow(mound.Root.position, heading);
                }
                yield return null;
            }

            // Under the spot: the ground heaves, swelling and churning faster, and spits earth until he breaks out.
            float gap = (body != null ? body.radius * Size : 1f) + 0.6f;
            Teleport(KeepClear(target, gap));
            Vector3 under = mound.Root.position;
            nextSpray = 0f;
            for (float t = 0f; !risen && !health.IsDead; t += Time.deltaTime)
            {
                mound.Place(Flat(under), travel + t * 2.5f, 1f + t * 0.8f);
                if (t >= nextSpray)
                {
                    nextSpray = t + 0.1f;
                    Clods(under, 2, 2.5f, Vector3.zero, 0.6f);
                }
                yield return null;
            }
            Destroy(mound.Root.gameObject);

            // Bursts up: overshoots clear of the ground, then lands crouched.
            SetVisible(true);
            health.Immune = false;
            Face(ToPlayer());
            Clods(transform.position, 18, 6f, Vector3.zero, 1.3f);
            anim?.PlayClip(GravediggerClips.Erupt, T);
            float rose = Time.time;
            for (float t = 0f; t < GravediggerClips.EruptLand; t = (Time.time - rose) * T)
            {
                if (anim != null)
                    anim.Sunk = t < GravediggerClips.EruptTop
                        ? Mathf.Lerp(1f, -0.18f, Mathf.Sin(t / GravediggerClips.EruptTop * Mathf.PI * 0.5f))
                        : Mathf.Lerp(-0.18f, 0f, Mathf.SmoothStep(0f, 1f, (t - GravediggerClips.EruptTop) / (GravediggerClips.EruptLand - GravediggerClips.EruptTop)));
                yield return null;
            }
            if (anim != null) anim.Sunk = 0f;
            SkillEffects.Shockwave(transform.position, 1.2f * Size, Dust, 0.3f);
            yield return UntilClip(rose, GravediggerClips.Erupt.Duration);
        }

        // Loose, freshly turned soil: lighter than the packed Earth, in a few shades.
        private static readonly Color Soil = new Color(0.47f, 0.35f, 0.22f);

        private static Color SoilShade() => Color.Lerp(Soil, Roll(0f, 1f) < 0.5f ? new Color(0.3f, 0.22f, 0.14f) : new Color(0.58f, 0.46f, 0.32f), Roll(0f, 0.6f));

        // The ground heaving over him as he tunnels: a low, lopsided heap of loose soil, crumbs
        // and clods jostling all over it, a stone or two, grave-light in the cracks.
        private sealed class Molehill
        {
            public Transform Root;
            public readonly System.Collections.Generic.List<Transform> Crumbs = new System.Collections.Generic.List<Transform>();
            public readonly System.Collections.Generic.List<Vector3> Rest = new System.Collections.Generic.List<Vector3>();
            public readonly System.Collections.Generic.List<Transform> Cracks = new System.Collections.Generic.List<Transform>();
            public Vector3 CrackSize;
            public float Hop;

            public void Place(Vector3 at, float t, float swell)
            {
                Root.position = new Vector3(at.x, Debris.GroundBelow(at + Vector3.up * 3f) - 0.05f, at.z);
                Root.localScale = new Vector3(swell, swell * (1f + Mathf.Sin(t * 11f) * 0.06f), swell);
                for (int i = 0; i < Crumbs.Count; i++)
                {
                    float bounce = Mathf.Max(0f, Mathf.Sin(t * (9f + (i % 5) * 2.3f) + i * 1.9f));
                    Crumbs[i].localPosition = Rest[i] + Vector3.up * bounce * Hop;
                    Crumbs[i].localRotation = Quaternion.Euler(i * 37f + bounce * 25f, i * 53f, i * 21f);
                }
                for (int i = 0; i < Cracks.Count; i++)
                    Cracks[i].localScale = new Vector3(CrackSize.x, CrackSize.y, CrackSize.z * (0.7f + 0.3f * Mathf.Abs(Mathf.Sin(t * 4f + i * 1.7f))));
            }
        }

        private Molehill BurrowMound()
        {
            var mound = new Molehill { Root = Prop("BurrowMound").transform };
            leftovers.Add(mound.Root.gameObject);
            float k = Size * 0.7f;
            mound.Hop = 0.07f * k;
            // The heap: a lumpy mound of soil, a second smaller one shouldering out of it off-centre.
            DirtHeap(mound.Root, Soil, new Vector3(0f, 0f, 0.05f) * k, new Vector3(1.6f, 0.5f, 1.8f) * k, Roll(0f, 360f));
            DirtHeap(mound.Root, SoilShade(), new Vector3(Roll(-0.35f, 0.35f), 0f, Roll(0.1f, 0.45f)) * k, new Vector3(0.9f, 0.4f, 0.8f) * k, Roll(0f, 360f));
            // Crumbs and clods all over its surface, small and uneven.
            for (int i = 0; i < 22; i++)
            {
                float a = Roll(0f, Mathf.PI * 2f), r = Mathf.Sqrt(Roll(0f, 1f)) * 0.75f;
                Vector3 at = new Vector3(Mathf.Sin(a) * r * 0.85f, 0f, Mathf.Cos(a) * r + 0.05f);
                at.y = 0.24f * Mathf.Sqrt(Mathf.Max(0f, 1f - r * r / 0.6f)) + 0.02f;
                float size = Roll(0.06f, 0.17f);
                Transform crumb = Piece(mound.Root, PrimitiveType.Cube, SoilShade(), at * k,
                    new Vector3(size, size * Roll(0.5f, 0.9f), size * Roll(0.7f, 1.3f)) * k).transform;
                mound.Crumbs.Add(crumb);
                mound.Rest.Add(crumb.localPosition);
            }
            for (int i = 0; i < 2; i++)
            {
                Transform stone = Piece(mound.Root, PrimitiveType.Cube, new Color(0.36f, 0.33f, 0.29f), new Vector3(i == 0 ? 0.25f : -0.3f, 0.2f, i == 0 ? 0.3f : -0.15f) * k,
                    new Vector3(0.15f, 0.1f, 0.12f) * k).transform;
                mound.Crumbs.Add(stone);
                mound.Rest.Add(stone.localPosition);
            }
            // Thin cracks across the top with grave-light in them.
            mound.CrackSize = new Vector3(0.035f, 0.02f, 0.45f) * k;
            for (int i = 0; i < 3; i++)
            {
                float a = 25f + i * 62f;
                mound.Cracks.Add(Piece(mound.Root, PrimitiveType.Cube, GraveLight, new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.1f, 0.2f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.12f) * k,
                    mound.CrackSize, new Vector3(0f, a, 0f)).transform);
            }
            return mound;
        }

        // Earth turned up behind the mound like a mole's run: a flat, ragged strip of broken soil
        // with crumbs scattered along its edges; it settles back into the ground after a moment.
        private void Furrow(Vector3 at, Vector3 heading)
        {
            float k = Size * 0.6f;
            float ground = Debris.GroundBelow(at + Vector3.up);
            Vector3 side = Vector3.Cross(Vector3.up, heading);
            var pieces = new System.Collections.Generic.List<GameObject>();
            pieces.Add(DirtHeap(null, Color.Lerp(Soil, new Color(0.3f, 0.22f, 0.14f), Roll(0.2f, 0.5f)), new Vector3(at.x, ground - 0.02f, at.z),
                new Vector3(Roll(1.1f, 1.4f), Roll(0.12f, 0.2f), Roll(1.0f, 1.3f)) * k, Roll(0f, 360f)));
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = at + side * (i % 2 == 0 ? 1f : -1f) * Roll(0.35f, 0.65f) * k + heading * Roll(-0.4f, 0.4f) * k;
                float size = Roll(0.08f, 0.2f);
                GameObject crumb = Piece(null, PrimitiveType.Cube, SoilShade(), Vector3.zero,
                    new Vector3(size, size * Roll(0.5f, 0.9f), size) * k, new Vector3(Roll(0f, 40f), Roll(0f, 360f), Roll(0f, 40f)));
                crumb.transform.position = new Vector3(p.x, ground + size * k * 0.3f, p.z);
                pieces.Add(crumb);
            }
            foreach (GameObject piece in pieces)
            {
                piece.name = "Furrow";
                leftovers.Add(piece);
                PoeClone.Visuals.DistanceActivity.StartTracked(this, Settle(piece.transform, 1.6f, 0.9f));
            }
        }

        private static readonly Mesh[] dirtShapes = new Mesh[5];

        // A heap of soil this big (one of a few shapes, turned to a random yaw), its base at "at".
        private static GameObject DirtHeap(Transform parent, Color color, Vector3 at, Vector3 size, float yaw)
        {
            GameObject heap = RuntimePrimitives.CreateMesh(DirtShape(Cosmetic.Next(dirtShapes.Length)), parent, color, "Dirt");
            heap.transform.localPosition = at;
            heap.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            heap.transform.localScale = size;
            return heap;
        }

        // A lumpy, faceted heap of loose soil about a metre across and half a metre high: a ragged
        // outline, an uneven top, every face flat so it catches the light like broken earth.
        private static Mesh DirtShape(int variant)
        {
            if (dirtShapes[variant] != null)
                return dirtShapes[variant];
            var rng = new System.Random(variant * 7919 + 13);
            float R() => (float)rng.NextDouble();
            const int rings = 4, segments = 14;
            var edge = new float[segments];
            for (int s = 0; s < segments; s++)
                edge[s] = 0.7f + R() * 0.4f;
            var points = new Vector3[rings + 1, segments];
            for (int r = 1; r <= rings; r++)
            {
                float f = r / (float)rings;
                for (int s = 0; s < segments; s++)
                {
                    float a = (s + R() * 0.3f) / segments * Mathf.PI * 2f;
                    float radius = f * edge[s] * 0.5f * (r == rings ? 1f : 0.85f + R() * 0.3f);
                    float height = r == rings ? -0.04f : 0.5f * Mathf.Pow(1f - f * f, 0.6f) * (0.65f + R() * 0.6f);
                    points[r, s] = new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
                }
            }
            Vector3 top = new Vector3((R() - 0.5f) * 0.12f, 0.45f + R() * 0.12f, (R() - 0.5f) * 0.12f);

            var vertices = new System.Collections.Generic.List<Vector3>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                // Each face gets its own corners (flat-shaded), wound to face up and out.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), Vector3.up) < 0f)
                    (b, c) = (c, b);
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
            }
            for (int s = 0; s < segments; s++)
            {
                int n = (s + 1) % segments;
                Tri(top, points[1, s], points[1, n]);
                for (int r = 1; r < rings; r++)
                {
                    Tri(points[r, s], points[r + 1, s], points[r + 1, n]);
                    Tri(points[r, s], points[r + 1, n], points[r, n]);
                }
            }
            var triangles = new int[vertices.Count];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = i;
            var mesh = new Mesh { name = "Dirt" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            dirtShapes[variant] = mesh;
            return mesh;
        }

        // Waits, then sinks a piece of earth back into the ground and is rid of it.
        private static IEnumerator Settle(Transform piece, float after, float over)
        {
            yield return new WaitForSeconds(after);
            if (piece == null)
                yield break;
            Vector3 start = piece.position, size = piece.localScale;
            for (float t = 0f; t < over && piece != null; t += Time.deltaTime)
            {
                float f = t / over;
                piece.position = start + Vector3.down * size.y * f;
                piece.localScale = new Vector3(size.x, size.y * (1f - f), size.z);
                yield return null;
            }
            if (piece != null)
                Destroy(piece.gameObject);
        }

        // Waits until a clip started at that moment reaches this time (seconds at tempo 1).
        private IEnumerator UntilClip(float started, float at)
        {
            while ((Time.time - started) * T < at && !health.IsDead)
                yield return null;
        }

        // Cosmetic dice for flying earth: kept off Unity's random so a co-op guest's copy of a
        // move rolls the same spots as the host's however many clods each frame rate throws.
        private static readonly System.Random Cosmetic = new System.Random();

        private static float Roll(float min, float max) => min + (float)Cosmetic.NextDouble() * (max - min);

        // Lumps of earth thrown up from a spot: all round, or mostly along a direction.
        private void Clods(Vector3 at, int count, float speed, Vector3 toward, float size = 1f)
        {
            at.y = Debris.GroundBelow(at + Vector3.up) + 0.3f;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = toward.sqrMagnitude > 0.001f
                    ? Quaternion.Euler(0f, Roll(-25f, 25f), 0f) * Flat(toward).normalized
                    : Quaternion.Euler(0f, Roll(0f, 360f), 0f) * Vector3.forward;
                Vector3 velocity = dir * speed * Roll(0.5f, 1f) + Vector3.up * speed * Roll(1f, 1.6f);
                float lump = Roll(0.12f, 0.3f) * size * Size * 0.5f;
                GameObject clod = Piece(null, PrimitiveType.Cube, SoilShade(), Vector3.zero,
                    new Vector3(lump, lump * Roll(0.5f, 0.9f), lump * Roll(0.7f, 1.2f)), new Vector3(Roll(0f, 360f), Roll(0f, 360f), 0f));
                clod.name = "Clod";
                clod.transform.position = at + Flat(dir) * Roll(0f, 0.4f) * Size;
                Debris.Throw(clod, velocity, new Vector3(Roll(-400f, 400f), Roll(-400f, 400f), Roll(-400f, 400f)), Roll(0.8f, 1.4f));
            }
        }

        // Strikes the ground with the shovel: graves open under the player, and the hands in them
        // hold whoever is still standing there.
        private IEnumerator OpenGraves(int count)
        {
            Act(CreatureAnimator.BossAct.Slam, 0.8f / T);
            float windUp = 1.1f / T;
            const float radius = 2.2f;
            Vector3 center = PlayerAt;
            for (int k = 0; k < count; k++)
            {
                Vector3 at = k == 0 ? center : OpenNear(center + Quaternion.Euler(0f, k * 120f + Random.Range(-20f, 20f), 0f) * Vector3.forward * 4.5f);
                PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.Run(at, radius, windUp + k * 0.15f / T, DamageType.Physical, spot =>
                {
                    if (this == null || health.IsDead)
                        return;
                    GraveHands(spot, radius);
                    if (HitIfInside(spot, radius, 1.0f, DamageType.Physical))
                    {
                        playerMotion?.Root(1.0f);
                        CombatText.Show(player.transform.position + Vector3.up * 2.2f, "Grasped!", DeadHand, 1f);
                    }
                }, kind));
            }
            yield return Pause(0.8f);
        }

        // An open pit with dead hands clawing up out of it, gone after a moment.
        private void GraveHands(Vector3 at, float radius)
        {
            GameObject grave = Prop("OpenGrave");
            leftovers.Add(grave);
            at.y = Debris.GroundBelow(at + Vector3.up);
            grave.transform.position = at;
            Piece(grave.transform, PrimitiveType.Cylinder, new Color(0.08f, 0.06f, 0.05f), Vector3.up * 0.05f, new Vector3(radius * 1.3f, 0.02f, radius * 0.8f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f + Random.value * 0.5f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * Random.Range(0.3f, 0.75f);
                Transform hand = Piece(grave.transform, PrimitiveType.Cube, DeadHand, p + Vector3.up * 0.4f, new Vector3(0.12f, 0.8f, 0.12f),
                    new Vector3(Random.Range(-25f, 25f), 0f, Random.Range(-25f, 25f))).transform;
                for (int f = 0; f < 3; f++)
                    Piece(hand, PrimitiveType.Cube, DeadHand, new Vector3((f - 1) * 0.35f, 0.6f, 0f), new Vector3(0.25f, 0.3f, 0.6f), new Vector3(0f, 0f, (f - 1) * 15f));
            }
            Destroy(grave, 1.6f);
        }

        // Swings the coffin off his back and hurls it: it lands with a crash, then the dead break out of it.
        private IEnumerator CoffinToss()
        {
            float windUp = 0.7f / T;
            Act(CreatureAnimator.BossAct.Throw, windUp);
            Face(ToPlayer());
            yield return new WaitForSeconds(windUp * 0.78f);

            Transform onBack = anim != null ? anim.Prop : null;
            if (onBack == null)
                yield break;
            coffinOnBack = false;
            foreach (Renderer r in onBack.GetComponentsInChildren<Renderer>())
                r.enabled = false;
            GameObject coffin = Prop("ThrownCoffin");
            CreatureBuilder.BuildCoffin(coffin.transform);
            coffin.transform.localScale = Vector3.one * Size;
            coffin.transform.SetPositionAndRotation(onBack.position, Quaternion.LookRotation(ToPlayer()) * Quaternion.Euler(90f, 0f, 0f));
            Vector3 target = PlayerAt;
            const float radius = 2.4f;
            PoeClone.Visuals.DistanceActivity.StartTracked(this, Lob(coffin, onBack.position, target, 0.85f / T, radius, DamageType.Physical, at =>
            {
                HitIfInside(at, radius, 1.5f, DamageType.Physical);
                SkillEffects.Shockwave(at, radius, Dust, 0.4f);
                CameraSystem.CameraFollow.Shake(0.25f, 0.35f);
                PoeClone.Visuals.DistanceActivity.StartTracked(this, CoffinOpens(coffin, at));
            }, keep: true, spin: 0f));
            yield return Pause(0.4f);
            PoeClone.Visuals.DistanceActivity.StartTracked(this, CoffinBack(onBack));
        }

        private IEnumerator CoffinOpens(GameObject coffin, Vector3 at)
        {
            yield return new WaitForSeconds(1.0f / T);
            if (coffin != null)
            {
                SkillEffects.Blast(coffin.transform.position + Vector3.up * 0.5f, 1.4f, GraveLight, 0.4f);
                Destroy(coffin);
            }
            if (this != null && !health.IsDead)
                Summon(0, 2, GraveLight, at, 1.4f);
        }

        // A while later he has dug another one out from somewhere.
        private IEnumerator CoffinBack(Transform onBack)
        {
            yield return new WaitForSeconds(5f);
            if (onBack == null)
                yield break;
            foreach (Renderer r in onBack.GetComponentsInChildren<Renderer>())
                r.enabled = true;
            coffinOnBack = true;
        }

        // Drives the shovel in and drags it: the ground splits towards the player, the dead clawing at the crack.
        private IEnumerator Exhume()
        {
            float windUp = 0.75f / T;
            Vector3 dir = ToPlayer();
            Face(dir);
            Act(CreatureAnimator.BossAct.Slam, windUp * 1.15f);
            Vector3 origin = transform.position + dir * (1.2f * Size);
            EruptionLine(origin, dir, secondPhase ? 9 : 7, 1.6f, 1.5f, windUp, 0.08f, DamageType.Physical, 1.25f,
                at => GraveHands(at, 1.1f));
            yield return new WaitForSeconds(windUp);
            CameraSystem.CameraFollow.Shake(0.2f, 0.3f);
            SkillEffects.Shockwave(origin, 1.8f, Earth, 0.35f);
            yield return Pause(0.35f);
        }

        // Swings the shovel all round himself at the height of a man's knees.
        private IEnumerator ShovelSweep()
        {
            float windUp = 0.7f / T;
            float radius = 3.2f + 0.8f * Size;
            Act(CreatureAnimator.BossAct.Spin, windUp + 0.3f / T);
            bool swept = false;
            PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.Run(transform.position, radius, windUp, DamageType.Physical, at =>
            {
                if (this == null || health.IsDead)
                    return;
                swept = true;
                HitIfInside(at, radius, 1.3f, DamageType.Physical, attack: true);
                SkillEffects.Shockwave(at, radius, Dust, 0.3f);
            }, kind));
            yield return new WaitForSeconds(windUp);
            float yaw = transform.eulerAngles.y;
            float spin = 0.35f / T;
            for (float t = 0f; t < spin && swept; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Euler(0f, yaw + 360f * (t / spin), 0f);
                yield return null;
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            yield return Pause(0.2f);
        }
    }
}

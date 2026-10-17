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

        // The graveside rite: he plants the shovel and bows over it, then drives it into the earth,
        // and graves open under the player (and round them, later in the fight). Dead arms burst
        // up out of each and hold whoever is still standing there.
        private IEnumerator OpenGraves(int count)
        {
            Face(ToPlayer());
            float started = Time.time;
            anim?.PlayClip(GravediggerClips.Grave, T);
            float windUp = GravediggerClips.GraveStrike / T;
            const float radius = 2.2f;
            Vector3 center = PlayerAt;
            for (int k = 0; k < count; k++)
            {
                Vector3 at = k == 0 ? center : OpenNear(center + Quaternion.Euler(0f, k * 120f + Random.Range(-20f, 20f), 0f) * Vector3.forward * 4.5f);
                float yaw = Random.Range(0f, 360f);
                PoeClone.Visuals.DistanceActivity.StartTracked(this, GroundTelegraph.Run(at, radius, windUp + k * 0.15f / T, DamageType.Physical, spot =>
                {
                    if (this == null || health.IsDead)
                        return;
                    PoeClone.Visuals.DistanceActivity.StartTracked(this, OpenGrave(spot, radius, yaw));
                    if (HitIfInside(spot, radius, 1.0f, DamageType.Physical))
                    {
                        playerMotion?.Root(1.0f);
                        CombatText.Show(player.transform.position + Vector3.up * 2.2f, "Grasped!", DeadHand, 1f);
                        PoeClone.Visuals.DistanceActivity.StartTracked(this, Grasp(player.transform.position));
                    }
                }, kind));
            }
            Vector3 blade = transform.position + transform.forward * 1.3f * Size;
            yield return UntilClip(started, GravediggerClips.GraveStrike);
            CameraSystem.CameraFollow.Shake(0.2f, 0.3f);
            SkillEffects.Shockwave(blade, 0.8f * Size, Soil, 0.35f);
            Clods(blade, 8, 4f, Vector3.zero);
            yield return UntilClip(started, GravediggerClips.Grave.Duration);
        }

        private static readonly Color Headstone = new Color(0.46f, 0.46f, 0.44f);
        private static readonly Color Rag = new Color(0.16f, 0.15f, 0.13f);

        // A grave opening: the ground splits into a dark pit, grave-light down in it, soil heaped
        // along its sides; a cracked headstone shoulders up at its head, and dead arms claw up out
        // of it. After a moment the arms sink and the earth closes over it again.
        private IEnumerator OpenGrave(Vector3 at, float radius, float yaw)
        {
            Transform grave = Prop("OpenGrave").transform;
            leftovers.Add(grave.gameObject);
            at.y = Debris.GroundBelow(at + Vector3.up);
            grave.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            float length = radius * 1.25f, width = radius * 0.62f;

            Piece(grave, PrimitiveType.Cube, new Color(0.05f, 0.04f, 0.035f), new Vector3(0f, 0.03f, 0f), new Vector3(width, 0.02f, length));
            Transform glow = Piece(grave, PrimitiveType.Cube, Color.Lerp(GraveLight, Color.black, 0.45f), new Vector3(0f, 0.04f, 0f), new Vector3(width * 0.6f, 0.02f, length * 0.75f)).transform;
            var heaps = new System.Collections.Generic.List<Transform>();
            for (int side = -1; side <= 1; side += 2)
                heaps.Add(DirtHeap(grave, SoilShade(), new Vector3(side * width * 0.62f, 0f, Roll(-0.2f, 0.2f)), new Vector3(0.9f, 0.5f, length * 1.05f), (side < 0 ? 180f : 0f) + Roll(-8f, 8f)).transform);
            heaps.Add(DirtHeap(grave, SoilShade(), new Vector3(0f, 0f, -length * 0.58f), new Vector3(width * 1.2f, 0.4f, 0.8f), Roll(0f, 360f)).transform);

            // The headstone: a slab with a rounded top, an engraved cross, a crack, leaning.
            Transform stone = new GameObject("Headstone").transform;
            stone.SetParent(grave, false);
            stone.localPosition = new Vector3(Roll(-0.15f, 0.15f), 0f, length * 0.58f);
            Quaternion lean = Quaternion.Euler(Roll(-14f, -6f), Roll(-12f, 12f), Roll(-10f, 10f));
            Piece(stone, PrimitiveType.Cube, Headstone, new Vector3(0f, 0.55f, 0f), new Vector3(0.95f, 1.1f, 0.2f));
            Piece(stone, PrimitiveType.Cylinder, Headstone, new Vector3(0f, 1.1f, 0f), new Vector3(0.95f, 0.1f, 0.95f), new Vector3(90f, 0f, 0f)).transform.localScale = new Vector3(0.95f, 0.1f, 0.6f);
            Color carved = Color.Lerp(Headstone, Color.black, 0.5f);
            Piece(stone, PrimitiveType.Cube, carved, new Vector3(0f, 0.75f, -0.105f), new Vector3(0.08f, 0.5f, 0.02f));
            Piece(stone, PrimitiveType.Cube, carved, new Vector3(0f, 0.88f, -0.105f), new Vector3(0.32f, 0.08f, 0.02f));
            Piece(stone, PrimitiveType.Cube, carved, new Vector3(0.25f, 0.4f, -0.105f), new Vector3(0.03f, 0.45f, 0.02f), new Vector3(0f, 0f, 25f));
            Piece(stone, PrimitiveType.Cube, Color.Lerp(Headstone, new Color(0.3f, 0.4f, 0.2f), 0.5f), new Vector3(-0.3f, 0.1f, -0.1f), new Vector3(0.3f, 0.2f, 0.04f));

            var arms = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = new Vector3(Roll(-0.3f, 0.3f) * width, 0f, (i / 4f - 0.5f) * length * 0.75f + Roll(-0.15f, 0.15f));
                arms.Add(RisingArm(grave, p, Roll(0f, 360f), i % 2 == 0, Roll(1.4f, 1.8f)));
            }
            Clods(at, 14, 5f, Vector3.zero, 1.1f);

            // Bursts open: the earth heaves up, the stone shoulders up, then the arms.
            const float open = 0.18f;
            for (float t = 0f; t <= 1.7f && grave != null; t += Time.deltaTime)
            {
                float rise = Mathf.Clamp01(t / open);
                foreach (Transform heap in heaps)
                    heap.localScale = new Vector3(heap.localScale.x, Mathf.Lerp(0.05f, 0.5f, rise), heap.localScale.z);
                stone.localPosition = new Vector3(stone.localPosition.x, Mathf.Lerp(-1.2f, 0f, Mathf.Clamp01(t / 0.3f)), stone.localPosition.z);
                stone.localRotation = Quaternion.Slerp(Quaternion.identity, lean, Mathf.Clamp01(t / 0.3f));
                glow.localScale = new Vector3(width * 0.6f, 0.02f, length * 0.75f) * (0.85f + 0.15f * Mathf.Sin(t * 9f));
                ClawArms(arms, t - 0.08f, 1.25f);
                yield return null;
            }
            if (grave == null)
                yield break;
            // The earth closes over it.
            for (float t = 0f; t < 0.5f && grave != null; t += Time.deltaTime)
            {
                grave.position = at + Vector3.down * (t / 0.5f) * 1.2f;
                yield return null;
            }
            if (grave != null)
                Destroy(grave.gameObject);
        }

        // Two dead hands burst up at the player's feet and grip their ankles while they're held.
        private IEnumerator Grasp(Vector3 at)
        {
            Transform root = Prop("Grasp").transform;
            leftovers.Add(root.gameObject);
            at.y = Debris.GroundBelow(at + Vector3.up);
            root.position = at;
            var arms = new System.Collections.Generic.List<Transform>();
            for (int side = -1; side <= 1; side += 2)
            {
                // Low, facing in towards the ankles they close on.
                arms.Add(RisingArm(root, new Vector3(side * 0.4f, 0f, Roll(-0.2f, 0.2f)), -side * 90f + Roll(-20f, 20f), side < 0, 0.7f));
                DirtHeap(root, SoilShade(), new Vector3(side * 0.35f, 0f, 0f), new Vector3(0.5f, 0.15f, 0.5f), Roll(0f, 360f));
            }
            for (float t = 0f; t <= 1.3f && root != null; t += Time.deltaTime)
            {
                ClawArms(arms, t, 1.0f, grip: true);
                yield return null;
            }
            if (root != null)
                Destroy(root.gameObject);
        }

        // Dead arms bursting up out of the ground (Exhume's crack): a heap of broken soil each,
        // the arms clawing, then gone.
        private void GraveHands(Vector3 at, float radius)
        {
            PoeClone.Visuals.DistanceActivity.StartTracked(this, BurstOfArms(at, radius));
        }

        private IEnumerator BurstOfArms(Vector3 at, float radius)
        {
            Transform root = Prop("GraveHands").transform;
            leftovers.Add(root.gameObject);
            at.y = Debris.GroundBelow(at + Vector3.up);
            root.position = at;
            var arms = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + Roll(0f, 0.6f);
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * Roll(0.15f, 0.55f);
                arms.Add(RisingArm(root, p, Roll(0f, 360f), i % 2 == 0, Roll(1.2f, 1.5f)));
                DirtHeap(root, SoilShade(), p, new Vector3(0.6f, 0.2f, 0.6f), Roll(0f, 360f));
            }
            Clods(at, 5, 3.5f, Vector3.zero, 0.8f);
            for (float t = 0f; t <= 1.3f && root != null; t += Time.deltaTime)
            {
                ClawArms(arms, t, 0.95f);
                yield return null;
            }
            if (root != null)
                Destroy(root.gameObject);
        }

        // A dead arm reaching up out of the ground: a grey forearm in a rotted sleeve, the
        // player's open hand mesh at its end, fingers up. Returns the part that moves.
        private static Transform RisingArm(Transform parent, Vector3 at, float yaw, bool left, float size)
        {
            Transform root = new GameObject("DeadArm").transform;
            root.SetParent(parent, false);
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform arm = new GameObject("Arm").transform;
            arm.SetParent(root, false);
            arm.localPosition = Vector3.down * size;
            Piece(arm, PrimitiveType.Cube, DeadHand, new Vector3(0f, 0.38f, 0f) * size, new Vector3(0.1f, 0.7f, 0.09f) * size);
            Piece(arm, PrimitiveType.Cube, Rag, new Vector3(0f, 0.12f, 0f) * size, new Vector3(0.16f, 0.24f, 0.14f) * size, new Vector3(0f, 25f, 6f));
            Piece(arm, PrimitiveType.Cube, Rag, new Vector3(0.03f, 0.27f, 0.02f) * size, new Vector3(0.05f, 0.12f, 0.12f) * size, new Vector3(0f, 0f, -20f));
            BossMeshes meshes = BossMeshes.Get();
            if (meshes != null)
            {
                GameObject hand = RuntimePrimitives.CreateMesh(left ? meshes.HandL : meshes.HandR, arm, DeadHand, "Hand");
                hand.transform.localPosition = new Vector3(0f, 0.8f, 0f) * size;
                hand.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                hand.transform.localScale = new Vector3(0.135f, 0.16f, 0.16f) * size * 1.6f;
            }
            return arm;
        }

        // The arms shoot up out of the ground one after another, claw and sway (or close on the
        // ankles they grip), and sink away when the time's up.
        private static void ClawArms(System.Collections.Generic.List<Transform> arms, float t, float until, bool grip = false)
        {
            for (int i = 0; i < arms.Count; i++)
            {
                Transform arm = arms[i];
                if (arm == null)
                    continue;
                float own = t - i * 0.06f;
                float up = Mathf.Clamp01(own / 0.16f) * (1f - Mathf.Clamp01((own - until) / 0.3f));
                float height = arm.GetChild(0).localScale.y / 0.7f;
                arm.localPosition = Vector3.down * height * (1f - Mathf.Sin(up * Mathf.PI * 0.5f));
                float sway = grip ? 0f : Mathf.Sin(own * 7f + i * 1.3f);
                float clutch = grip ? Mathf.Clamp01((own - 0.15f) / 0.2f) * 55f : 0f;
                arm.localRotation = Quaternion.Euler(-15f + sway * 14f + clutch, 0f, Mathf.Sin(own * 5f + i) * 10f);
            }
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

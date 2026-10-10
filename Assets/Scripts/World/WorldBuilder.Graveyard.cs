using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private enum YardFeature { Mausoleum, Angel, OldTree }

        // Walled churchyards (area-local centre, radii, what stands in the middle, iron railing on the wall).
        private static readonly (Vector2 at, Vector2 radii, YardFeature feature, bool railing)[] Churchyards =
        {
            (new Vector2(-45f, 20f), new Vector2(9f, 7f), YardFeature.Angel, true),
            (new Vector2(32f, 25f), new Vector2(11f, 8.5f), YardFeature.Mausoleum, false),
            (new Vector2(-106f, -48f), new Vector2(11f, 9f), YardFeature.OldTree, false),
            (new Vector2(100f, -30f), new Vector2(11.5f, 9f), YardFeature.Mausoleum, true),
            (new Vector2(72f, 72f), new Vector2(11f, 8f), YardFeature.Angel, false),
            (new Vector2(14f, -96f), new Vector2(10f, 7f), YardFeature.OldTree, true),
            (new Vector2(-102f, 78f), new Vector2(8f, 7f), YardFeature.OldTree, false)
        };

        // Graves the dead have climbed out of, and the iron spirit lanterns along the track.
        private static readonly Vector2[] OpenGraves =
        {
            new Vector2(-56f, 6f), new Vector2(46f, 16f), new Vector2(-88f, -28f), new Vector2(84f, -52f),
            new Vector2(54f, 56f), new Vector2(-6f, -80f), new Vector2(-94f, 90f)
        };

        private static readonly Vector2[] SpiritLanterns =
        {
            new Vector2(-92f, -9f), new Vector2(-65f, 9.5f), new Vector2(-33f, -9.5f), new Vector2(32f, 10.5f),
            new Vector2(63f, -9.5f), new Vector2(92f, 8f), new Vector2(-84f, 84f), new Vector2(-68f, 84f)
        };

        private static readonly Color SpiritBody = new Color(0.25f, 0.9f, 0.4f);
        private static readonly Color SpiritCore = new Color(0.8f, 1f, 0.75f);

        // The Haunted Graveyard: walled churchyards, open graves, gnarled dead trees, spirit
        // lanterns along the track, and the crypt dug into a barrow at the back of the north-west clearing.
        private void BuildGraveyard()
        {
            Begin(Graveyard, 202);
            Vector3 c = Centers[Graveyard];
            Transform t = Group("Graveyard");

            // Worn winding track, still inside the broad central corridor.
            WindingPath(t, "GraveyardTrack", Graveyard, 3.6f, MudFloor,
                AreaLayouts.GateLocal(Graveyard, false), AreaLayouts.GateApproachLocal(Graveyard, Cave), Flat(-65, 5), Flat(-33, -5),
                Flat(0, 0), Flat(32, 6), Flat(63, -5), AreaLayouts.GateApproachLocal(Graveyard, Ruins), AreaLayouts.GateLocal(Graveyard, true));
            Claim(c + new Vector3(-30f, 0f, 0f), 3f);
            Claim(c + new Vector3(0f, 0f, 0f), 3f);
            Claim(c + new Vector3(30f, 0f, 0f), 3f);

            Vector3 crypt = c + AreaLayouts.BossLocal(Graveyard);
            BarrowCrypt(t, crypt);
            Spots["Crypt"] = crypt + new Vector3(0f, 0f, -6f);

            AreaShape shape = Shape(Graveyard);
            foreach (var yard in Churchyards)
                Churchyard(t, shape.NearestOpen(c + Flat(yard.at.x, yard.at.y), Mathf.Max(yard.radii.x, yard.radii.y) + 2f),
                    yard.radii, yard.feature, yard.railing);
            foreach (Vector2 at in OpenGraves)
            {
                Vector3 p = shape.NearestOpen(c + Flat(at.x, at.y), 3f);
                OpenGrave(t, p, R(200f, 250f));
                Claim(p, 2.8f);
            }
            foreach (Vector2 at in SpiritLanterns)
            {
                Vector3 p = c + Flat(at.x, at.y);
                SpiritLantern(t, p, R(0f, 360f));
                Claim(p, 1f);
            }

            Scatter(t, 16, 6f, 46f, p => DeadTree(t, p, kit.Mat("DeadWood")), 1.8f);
            Scatter(t, 9, 8f, 46f, p => LoneGraves(t, p), 1.8f);
            Scatter(t, 10, 8f, 46f, p => Prefab(kit.rock, t, p, R(0f, 360f), Vector3.one * R(0.8f, 1.4f)), 1.5f);
        }

        // ------------------------------------------------------------------ the crypt

        private Material barrowTurf;
        private Material BarrowTurf => barrowTurf != null ? barrowTurf : barrowTurf =
            FloorMaterial("BarrowTurf", GroundTexture(Graveyard), 7f, new Color(0.9f, 0.92f, 0.9f));

        // Grave mounds: the track's mud, lighter, so they read against the dark ground.
        private Material graveEarth;
        private Material GraveEarth => graveEarth != null ? graveEarth : graveEarth =
            FloorMaterial("GraveEarth", MudFloor.GetTexture("_BaseMap"), 4f, new Color(1.12f, 1.1f, 1.08f));

        private Material VoidMaterial => GlowMaterial(new Color(0.01f, 0.012f, 0.014f), new Color(0.03f, 0.04f, 0.04f));

        // A grassed barrow with a stone portal let into its south face between two retaining walls:
        // steps up to a dark doorway behind an iron gate, spirit fire in braziers either side.
        private void BarrowCrypt(Transform t, Vector3 crypt)
        {
            const float rx = 13f, rz = 9.5f, peak = 6.5f, back = 8.5f;
            Transform barrow = Holder(t, "Barrow", crypt, Quaternion.identity);

            // The mound, with a notch cut out of its front for the portal's forecourt.
            float Notch(float z) => z < 2f ? 3.4f + (2f - z) * 0.85f : 0f;
            // Steep at the foot (too steep to climb), rounded over the top.
            float Height(float x, float z)
            {
                float e = 1f - (x / rx) * (x / rx) - (z - back) / rz * ((z - back) / rz);
                if (e <= 0f || Mathf.Abs(x) < Notch(z)) return 0f;
                float lumpy = 1f + 0.32f * (Mathf.PerlinNoise(x * 0.21f + 3.1f, z * 0.21f + 7.7f) - 0.5f);
                return peak * Mathf.Pow(e, 0.65f) * lumpy;
            }
            // Laid out in rings round the crown, so the foot is a smooth oval rather than a jagged grid edge.
            const int rings = 14, spokes = 56;
            Vector3 Ground(int ring, int spoke)
            {
                float r = (float)ring / rings, a = spoke * Mathf.PI * 2f / spokes;
                float x = Mathf.Cos(a) * rx * r, z = back + Mathf.Sin(a) * rz * r;
                float h = ring == rings ? 0f : Height(x, z);
                return new Vector3(x, h > 0f ? h : -0.05f, z);
            }
            var turf = new PieceMesh();
            int grass = turf.Slot(BarrowTurf);
            for (int ring = 0; ring < rings; ring++)
                for (int spoke = 0; spoke < spokes; spoke++)
                {
                    Vector3 a = Ground(ring, spoke), b = Ground(ring, spoke + 1), d = Ground(ring + 1, spoke), e = Ground(ring + 1, spoke + 1);
                    if (a.y + b.y + d.y + e.y <= -0.19f) continue;
                    if (ring > 0) turf.Tri(grass, a, b, e, Vector3.up);
                    turf.Tri(grass, a, e, d, Vector3.up);
                }
            // A few slabs standing on the crest.
            int crest = turf.Slot(kit.Mat("TombstoneDark"));
            foreach (Vector3 slab in new[] { new Vector3(-3f, 0f, 9f), new Vector3(2.5f, 0f, 10.5f), new Vector3(0.2f, 0f, 12.8f) })
            {
                Quaternion lean = Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-8f, 8f));
                float tall = R(1.5f, 2.3f);
                turf.Block(crest, slab + Vector3.up * (Height(slab.x, slab.z) - 0.3f) + lean * Vector3.up * tall * 0.5f,
                    lean * Vector3.right * 0.28f, lean * Vector3.up * tall * 0.5f, lean * Vector3.forward * 0.18f);
            }
            GameObject mound = turf.Build(barrow, "Mound");
            mound.AddComponent<MeshCollider>().sharedMesh = mound.GetComponent<MeshFilter>().sharedMesh;

            // Boulders half-buried round its foot and a pair of dead trees on its back.
            for (int k = 0; k < 14; k++)
            {
                float a = Mathf.Lerp(-35f, 215f, k / 13f) * Mathf.Deg2Rad;
                float x = Mathf.Cos(a) * rx * R(0.8f, 0.95f), z = back + Mathf.Sin(a) * rz * R(0.8f, 0.95f);
                if (z < 2.5f && Mathf.Abs(x) < 8f) continue;
                GameObject boulder = Prefab(kit.rock, barrow, crypt + new Vector3(x, Height(x, z) * 0.5f - 0.3f, z), R(0f, 360f), Vector3.one * R(1.2f, 2.1f));
                NoShadows(boulder);
            }
            foreach (Vector2 at in new[] { new Vector2(-7f, 8f), new Vector2(6f, 12f) })
                DeadTree(barrow, crypt + new Vector3(at.x, Height(at.x, at.y) - 0.2f, at.y), kit.Mat("DeadWood"), 1.2f);

            // Retaining walls either side of the forecourt, stepping down as they come forward.
            var batch = new PrimitiveBatch();
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 from = new Vector3(side * 3.55f, 0f, 2f), to = new Vector3(side * 6.9f, 0f, -2f);
                Vector3 along = (to - from).normalized;
                float length = Vector3.Distance(from, to);
                Quaternion facing = Quaternion.LookRotation(along);
                const float course = 0.42f;
                for (int row = 0; row * course < 4.2f; row++)
                {
                    float y = row * course;
                    // The wall's top falls from 4.2 m at the portal to 0.6 m at its end.
                    float reach = Mathf.Clamp01((4.2f - y) / 3.6f) * length;
                    // Stop short of the end: comparing against it exactly can spin forever on rounding.
                    for (float cursor = row % 2 == 0 ? 0f : -0.3f; cursor < reach - 0.12f; )
                    {
                        float start = Mathf.Max(cursor, 0f);
                        float stone = Mathf.Min(R(0.55f, 0.95f), reach - start);
                        if (stone > 0.12f)
                            batch.Add(PrimitiveType.Cube, Coin() ? kit.Mat("Tombstone") : kit.Mat("TombstoneDark"),
                                crypt + from + along * (start + stone * 0.5f) + Vector3.up * (y + course * 0.5f),
                                facing * Quaternion.Euler(R(-2f, 2f), R(-3f, 3f), R(-2f, 2f)),
                                new Vector3(0.75f * R(0.92f, 1f), course - 0.03f, stone - 0.04f));
                        cursor = start + stone;
                    }
                }
                var wall = new GameObject("RetainingWall").transform;
                wall.SetParent(barrow, false);
                wall.SetPositionAndRotation(crypt + (from + to) * 0.5f + Vector3.up * 1.4f, facing);
                wall.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.8f, 2.8f, length);
            }
            batch.Build(barrow, "WallStones", shadows: true);

            // The portal face, its frame, pediment and steps (door facing south, -z).
            var portal = new PieceMesh();
            int dark = portal.Slot(kit.Mat("TombstoneDark"));
            int pale = portal.Slot(kit.Mat("Tombstone"));
            int iron = portal.Slot(kit.Mat("Iron"));
            int gloom = portal.Slot(VoidMaterial);
            portal.Block(dark, new Vector3(0f, 2.4f, 2.05f), Vector3.right * 3.7f, Vector3.up * 2.4f, Vector3.forward * 0.55f);
            foreach (float y in new[] { 1.25f, 2.5f, 3.75f })
                portal.Block(pale, new Vector3(0f, y, 1.48f), Vector3.right * 3.7f, Vector3.up * 0.045f, Vector3.forward * 0.04f);
            for (int side = -1; side <= 1; side += 2)
                portal.Block(pale, new Vector3(side * 1.38f, 1.75f, 1.36f), Vector3.right * 0.3f, Vector3.up * 1.3f, Vector3.forward * 0.15f);
            portal.Block(pale, new Vector3(0f, 3.32f, 1.34f), Vector3.right * 1.8f, Vector3.up * 0.27f, Vector3.forward * 0.17f);
            Prism(portal, pale, new[] { new Vector2(-1.95f, 0f), new Vector2(1.95f, 0f), new Vector2(0f, 1.05f) }, 0.26f,
                new Vector3(0f, 3.59f, 1.38f), Quaternion.Euler(0f, 180f, 0f));
            Skull(portal, pale, gloom, new Vector3(0f, 3.92f, 1.22f), Quaternion.Euler(-8f, 180f, 0f), 1.9f);
            portal.Block(gloom, new Vector3(0f, 1.75f, 1.47f), Vector3.right * 1.08f, Vector3.up * 1.3f, Vector3.forward * 0.02f);
            // Steps up to the threshold.
            for (int k = 0; k < 3; k++)
                portal.Block(pale, new Vector3(0f, 0.075f + k * 0.15f, -0.05f + k * 0.6f), Vector3.right * (2f - k * 0.25f),
                    Vector3.up * 0.075f, Vector3.forward * 0.3f);
            // An iron gate: the west leaf shut, the east one hanging open.
            for (int leaf = -1; leaf <= 1; leaf += 2)
            {
                Vector3 hinge = new Vector3(leaf * 1.06f, 0f, 1.4f);
                Vector3 across = leaf < 0 ? Vector3.right : Quaternion.Euler(0f, 40f, 0f) * Vector3.left;
                for (float s = 0.06f; s < 1.06f; s += 0.17f)
                    portal.Block(iron, hinge + across * s + Vector3.up * 1.72f, across * 0.02f, Vector3.up * 1.25f, Vector3.Cross(across, Vector3.up) * 0.02f);
                foreach (float y in new[] { 0.62f, 2.8f })
                    portal.Block(iron, hinge + across * 0.53f + Vector3.up * y, across * 0.53f, Vector3.up * 0.03f, Vector3.Cross(across, Vector3.up) * 0.025f);
            }
            // Spirit-fire braziers on squat pillars either side of the steps.
            int pillarStone = portal.Slot(kit.Mat("Stone"));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 at = new Vector3(side * 2.75f, 0f, -0.4f);
                portal.Frustum(pillarStone, at, at + Vector3.up * 1.3f, 0.34f, 0.27f, 6, Vector3.forward);
                portal.Frustum(iron, at + Vector3.up * 1.3f, at + Vector3.up * 1.58f, 0.2f, 0.42f, 8, Vector3.forward);
                Flame(barrow, crypt + at + Vector3.up * 1.8f, 0.3f, GlowMaterial(SpiritBody, SpiritCore), 1.6f, 0.2f);
                Glow(barrow, crypt + at + Vector3.up * 2.3f, SpiritLight, 10f, 4f, flicker: true);
                var pillar = new GameObject("Brazier").transform;
                pillar.SetParent(barrow, false);
                pillar.position = crypt + at + Vector3.up * 0.8f;
                pillar.gameObject.AddComponent<CapsuleCollider>().radius = 0.42f;
            }
            portal.Build(barrow, "Portal");
            var face = new GameObject("PortalFace").transform;
            face.SetParent(barrow, false);
            face.position = crypt + new Vector3(0f, 2.4f, 2.05f);
            face.gameObject.AddComponent<BoxCollider>().size = new Vector3(7.4f, 4.8f, 1.1f);
            // Something stirs below: a faint green breath at the doorway.
            Glow(barrow, crypt + new Vector3(0f, 1.2f, 1f), SpiritLight, 5f, 1.6f, flicker: true);

            Bones(barrow, crypt + new Vector3(-1.7f, 0f, -1.2f));
            Bones(barrow, crypt + new Vector3(2f, 0f, -2.4f));
            Candles(barrow, crypt + new Vector3(-1.2f, 0.45f, 1.05f));
            Candles(barrow, crypt + new Vector3(1.3f, 0.3f, 0.5f));

            Claim(crypt + new Vector3(0f, 0f, back), 15f);
            Claim(crypt + new Vector3(0f, 0f, -6f), 7f);
        }

        // ------------------------------------------------------------------ churchyards

        // A low drystone wall on a wobbling oval, a gate facing the area's middle, graves in loose
        // rows inside and a mausoleum, an angel or an old tree in the middle.
        private void Churchyard(Transform t, Vector3 center, Vector2 radii, YardFeature feature, bool railing)
        {
            Vector3 middle = Centers[Graveyard];
            Quaternion frame = Quaternion.Euler(0f, R(-25f, 25f), 0f);
            Vector3 toGate = middle - center;
            toGate.y = 0f;
            toGate = toGate.sqrMagnitude > 1f ? toGate.normalized : Vector3.back;

            int n = Mathf.CeilToInt(Mathf.PI * (radii.x + radii.y) / 1.3f);
            float wobbleA = R(0f, 6.28f), wobbleB = R(0f, 6.28f);
            var ring = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float wobble = 1f + 0.07f * Mathf.Sin(3f * a + wobbleA) + 0.05f * Mathf.Cos(5f * a + wobbleB);
                ring[i] = center + frame * new Vector3(Mathf.Cos(a) * radii.x * wobble, 0f, Mathf.Sin(a) * radii.y * wobble);
            }
            int gate = 0;
            float best = -2f;
            for (int i = 0; i < n; i++)
            {
                float facing = Vector3.Dot((ring[(i + 1) % n] - center).normalized, toGate);
                if (facing > best) { best = facing; gate = i; }
            }
            // The gateway spans two segments; one stretch further round has tumbled.
            int collapsed = (gate + n / 3 + rng.Next(Mathf.Max(1, n / 3))) % n;
            Material Fieldstone() { double pick = rng.NextDouble(); return kit.Mat(pick < 0.5 ? "RockDark" : pick < 0.8 ? "TombstoneDark" : "Rock"); }

            for (int run = 2; run < n; run += 5)
            {
                var batch = new PrimitiveBatch();
                var wall = new GameObject("YardWall").transform;
                wall.SetParent(t, false);
                wall.position = ring[(gate + run) % n];
                for (int s = run; s < Mathf.Min(run + 5, n); s++)
                {
                    int i = (gate + s) % n;
                    Vector3 a = ring[i], b = ring[(i + 1) % n];
                    Vector3 along = (b - a).normalized;
                    float length = Vector3.Distance(a, b);
                    Quaternion facing = Quaternion.LookRotation(along);
                    bool tumbled = i == collapsed || i == (collapsed + 1) % n;
                    if (tumbled)
                    {
                        for (int k = 0; k < 4; k++)
                            batch.Add(PrimitiveType.Cube, Fieldstone(), Vector3.Lerp(a, b, R(0f, 1f)) + Vector3.Cross(along, Vector3.up) * R(-0.7f, 0.7f) + Vector3.up * 0.1f,
                                Quaternion.Euler(R(-15f, 15f), R(0f, 360f), R(-15f, 15f)), new Vector3(R(0.3f, 0.6f), R(0.18f, 0.3f), R(0.35f, 0.6f)));
                        continue;
                    }
                    // Two courses of rough stones under a line of flatter capstones; corners overlap.
                    for (int row = 0; row < 3; row++)
                    {
                        bool cap = row == 2;
                        float height = cap ? 0.14f : 0.3f, y = row * 0.3f + height * 0.5f;
                        float end = length + 0.08f;
                        for (float cursor = -0.08f + (row == 1 ? 0.25f : 0f); cursor < end - 0.1f; )
                        {
                            float stoneLength = Mathf.Min(R(0.38f, 0.7f), end - cursor);
                            if (stoneLength > 0.1f)
                                batch.Add(PrimitiveType.Cube, Fieldstone(), a + along * (cursor + stoneLength * 0.5f) + Vector3.up * y,
                                    facing * Quaternion.Euler(R(-3f, 3f), R(-4f, 4f), R(-3f, 3f)),
                                    new Vector3((cap ? 0.6f : 0.5f) * R(0.9f, 1.05f), height * R(0.85f, 1f), stoneLength - 0.03f));
                            cursor += stoneLength;
                        }
                    }
                    if (rng.NextDouble() < 0.12)
                        batch.Add(PrimitiveType.Sphere, kit.Mat("Moss"), Vector3.Lerp(a, b, R(0.2f, 0.8f)) + Vector3.up * 0.7f,
                            facing, new Vector3(0.62f, 0.07f, R(0.7f, 1.2f)));
                    if (railing)
                    {
                        Material ironMat = kit.Mat("Iron");
                        for (float u = 0f; u < length - 0.05f; u += length / 3f)
                        {
                            Vector3 post = a + along * u + Vector3.up * 1.13f;
                            batch.Add(PrimitiveType.Cube, ironMat, post, facing, new Vector3(0.04f, 0.75f, 0.04f));
                            batch.Add(PrimitiveType.Cube, ironMat, post + Vector3.up * 0.4f, facing * Quaternion.Euler(45f, 0f, 45f), Vector3.one * 0.06f);
                        }
                        foreach (float y in new[] { 0.95f, 1.38f })
                            batch.Add(PrimitiveType.Cube, ironMat, (a + b) * 0.5f + Vector3.up * y, facing, new Vector3(0.03f, 0.03f, length));
                    }
                    var blocker = new GameObject("Wall").transform;
                    blocker.SetParent(wall, false);
                    blocker.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * 0.5f, facing);
                    blocker.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.6f, 1f, length + 0.1f);
                }
                batch.Build(wall, "Stones", shadows: true);
            }

            // Gateposts with ball finials, and one iron leaf swung inward.
            Vector3 left = ring[gate], right = ring[(gate + 2) % n];
            var gateway = new PieceMesh();
            int pillar = gateway.Slot(kit.Mat("TombstoneDark"));
            int iron = gateway.Slot(kit.Mat("Iron"));
            Transform gateHolder = Holder(t, "YardGate", (left + right) * 0.5f, Quaternion.identity);
            foreach (Vector3 p in new[] { left, right })
            {
                Vector3 local = p - gateHolder.position;
                gateway.Block(pillar, local + Vector3.up * 0.8f, Vector3.right * 0.27f, Vector3.up * 0.8f, Vector3.forward * 0.27f);
                gateway.Block(pillar, local + Vector3.up * 1.66f, Vector3.right * 0.34f, Vector3.up * 0.06f, Vector3.forward * 0.34f);
                gateway.Lump(pillar, local + Vector3.up * 1.88f, Vector3.one * 0.17f, 6, 4, Quaternion.identity);
                var blocker = new GameObject("Gatepost").transform;
                blocker.SetParent(gateHolder, false);
                blocker.position = p + Vector3.up * 0.8f;
                blocker.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.55f, 1.6f, 0.55f);
            }
            Vector3 span = right - left;
            Vector3 leaf = Quaternion.AngleAxis(Vector3.Dot(Vector3.Cross(span, center - left), Vector3.up) > 0f ? -70f : 70f, Vector3.up) * span.normalized;
            Vector3 hinge = left + span.normalized * 0.3f - gateHolder.position;
            for (float s = 0.1f; s < 1.2f; s += 0.16f)
                gateway.Block(iron, hinge + leaf * s + Vector3.up * 0.7f, leaf * 0.02f, Vector3.up * 0.6f, Vector3.Cross(leaf, Vector3.up) * 0.02f);
            foreach (float y in new[] { 0.2f, 1.2f })
                gateway.Block(iron, hinge + leaf * 0.6f + Vector3.up * y, leaf * 0.6f, Vector3.up * 0.03f, Vector3.Cross(leaf, Vector3.up) * 0.025f);
            gateway.Build(gateHolder, "Gateway");

            // A trodden path from outside the gate to the feature in the middle.
            Vector3 gateMid = (left + right) * 0.5f;
            Vector3 outside = gateMid + toGate * 2.5f;
            WindingPath(t, "YardPath", Graveyard, 1.6f, MudFloor, outside - middle, gateMid - middle, center - middle);

            float clearance;
            float featureYaw = Quaternion.LookRotation(gateMid - center).eulerAngles.y;
            switch (feature)
            {
                case YardFeature.Mausoleum: Mausoleum(t, center, featureYaw); clearance = 3.2f; break;
                case YardFeature.Angel: Angel(t, center, featureYaw); clearance = 2.2f; break;
                default:
                    DeadTree(t, center, kit.Mat("DeadWood"), 1.6f);
                    Candles(t, center + Flat(R(-1f, 1f), -1.3f));
                    clearance = 2.2f;
                    break;
            }

            // Graves in loose, staggered rows, their stones turned towards the viewer.
            float rowYaw = 225f + R(-15f, 15f);
            Quaternion rows = Quaternion.Euler(0f, rowYaw, 0f);
            Vector3 forward = rows * Vector3.forward;
            float extent = Mathf.Max(radii.x, radii.y);
            bool Inside(Vector3 p)
            {
                Vector3 local = Quaternion.Inverse(frame) * (p - center);
                float ex = local.x / (radii.x - 1f), ez = local.z / (radii.y - 1f);
                return ex * ex + ez * ez <= 1f;
            }
            float PathDistance(Vector3 p)
            {
                Vector3 d = center - gateMid;
                float u = Mathf.Clamp01(Vector3.Dot(p - gateMid, d) / d.sqrMagnitude);
                Vector3 off = p - (gateMid + d * u);
                off.y = 0f;
                return off.magnitude;
            }
            for (float z = -extent; z <= extent; z += 2.5f)
            {
                float shift = R(0f, 0.9f);
                for (float x = -extent; x <= extent; x += 1.5f)
                {
                    Vector3 p = center + rows * new Vector3(x + shift + R(-0.2f, 0.2f), 0f, z + R(-0.25f, 0.25f));
                    Vector3 foot = p + forward * 1.5f;
                    if (!Inside(p) || !Inside(foot) || rng.NextDouble() < 0.1) continue;
                    if (Vector3.Distance(p, center) < clearance + 0.6f || Vector3.Distance(foot, center) < clearance + 0.6f) continue;
                    if (PathDistance(p) < 1.2f || PathDistance(foot) < 1.2f) continue;
                    GameObject grave = Grave(t, p, rowYaw + R(-8f, 8f));
                    if (rng.NextDouble() < 0.12)
                        Candles(t, grave.transform.TransformPoint(new Vector3(0.45f, 0f, 0.3f)));
                }
            }
            Claim(center, extent + 1.5f);
        }

        // A small stone family tomb with a slate gable roof, its barred door facing the yard's gate.
        private void Mausoleum(Transform t, Vector3 p, float yaw)
        {
            Transform tomb = Holder(t, "Mausoleum", p, Quaternion.Euler(0f, yaw, 0f));
            var mesh = new PieceMesh();
            int stone = mesh.Slot(kit.Mat("Stone"));
            int pale = mesh.Slot(kit.Mat("Tombstone"));
            int dark = mesh.Slot(kit.Mat("TombstoneDark"));
            int slate = mesh.Slot(kit.Mat("SlateDark"));
            int iron = mesh.Slot(kit.Mat("Iron"));
            int gloom = mesh.Slot(VoidMaterial);
            mesh.Block(stone, Vector3.up * 0.15f, Vector3.right * 1.8f, Vector3.up * 0.15f, Vector3.forward * 2.1f);
            mesh.Block(pale, Vector3.up * 1.45f, Vector3.right * 1.5f, Vector3.up * 1.15f, Vector3.forward * 1.8f);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    mesh.Block(dark, new Vector3(x * 1.5f, 1.45f, z * 1.8f), Vector3.right * 0.2f, Vector3.up * 1.15f, Vector3.forward * 0.2f);
            mesh.Block(dark, Vector3.up * 2.67f, Vector3.right * 1.65f, Vector3.up * 0.07f, Vector3.forward * 1.95f);
            // Pediments front and back, under a ridged slate roof.
            foreach (float z in new[] { 1.85f, -1.85f })
                Prism(mesh, pale, new[] { new Vector2(-1.6f, 0f), new Vector2(1.6f, 0f), new Vector2(0f, 0.8f) }, 0.12f,
                    new Vector3(0f, 2.74f, z), Quaternion.identity);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 slope = new Vector3(side * 1.85f, -0.85f, 0f);
                Vector3 normal = new Vector3(side * 0.85f, 1.85f, 0f).normalized;
                mesh.Block(slate, new Vector3(0f, 3.6f, 0f) + slope * 0.5f + normal * 0.06f, slope * 0.5f, normal * 0.06f, Vector3.forward * 2.05f);
            }
            mesh.Block(dark, Vector3.up * 3.62f, Vector3.right * 0.09f, Vector3.up * 0.06f, Vector3.forward * 2.1f);
            // The barred doorway and a step before it.
            mesh.Block(gloom, new Vector3(0f, 1.25f, 1.81f), Vector3.right * 0.55f, Vector3.up * 0.95f, Vector3.forward * 0.02f);
            for (int side = -1; side <= 1; side += 2)
                mesh.Block(dark, new Vector3(side * 0.7f, 1.2f, 1.86f), Vector3.right * 0.13f, Vector3.up * 0.95f, Vector3.forward * 0.07f);
            mesh.Block(dark, new Vector3(0f, 2.22f, 1.86f), Vector3.right * 0.84f, Vector3.up * 0.1f, Vector3.forward * 0.08f);
            for (float x = -0.42f; x <= 0.43f; x += 0.21f)
                mesh.Block(iron, new Vector3(x, 1.25f, 1.86f), Vector3.right * 0.02f, Vector3.up * 0.95f, Vector3.forward * 0.02f);
            mesh.Block(stone, new Vector3(0f, 0.08f, 2.4f), Vector3.right * 0.9f, Vector3.up * 0.08f, Vector3.forward * 0.3f);
            // A cross over the front gable.
            mesh.Block(dark, new Vector3(0f, 3.85f, 1.9f), Vector3.right * 0.05f, Vector3.up * 0.28f, Vector3.forward * 0.05f);
            mesh.Block(dark, new Vector3(0f, 3.93f, 1.9f), Vector3.right * 0.17f, Vector3.up * 0.05f, Vector3.forward * 0.05f);
            mesh.Build(tomb, "Tomb");
            var blocker = tomb.gameObject.AddComponent<BoxCollider>();
            blocker.center = Vector3.up * 1.4f;
            blocker.size = new Vector3(3.6f, 2.8f, 4.2f);
        }

        // A robed, bowed angel with folded wings on a plinth, facing the yard's gate.
        private void Angel(Transform t, Vector3 p, float yaw)
        {
            Transform statue = Holder(t, "Angel", p, Quaternion.Euler(0f, yaw, 0f));
            statue.localScale = Vector3.one * 1.4f;
            var mesh = new PieceMesh();
            int dark = mesh.Slot(kit.Mat("TombstoneDark"));
            int pale = mesh.Slot(kit.Mat("Stone"));
            int moss = mesh.Slot(kit.Mat("Moss"));
            mesh.Block(dark, Vector3.up * 0.35f, Vector3.right * 0.5f, Vector3.up * 0.35f, Vector3.forward * 0.5f);
            mesh.Block(dark, Vector3.up * 0.74f, Vector3.right * 0.58f, Vector3.up * 0.05f, Vector3.forward * 0.58f);
            mesh.Frustum(pale, Vector3.up * 0.79f, Vector3.up * 1.95f, 0.4f, 0.17f, 8, Vector3.forward);
            mesh.Lump(pale, Vector3.up * 2f, new Vector3(0.22f, 0.14f, 0.16f), 8, 4, Quaternion.identity);
            mesh.Lump(pale, new Vector3(0f, 2.2f, 0.07f), new Vector3(0.11f, 0.13f, 0.12f), 7, 4, Quaternion.Euler(20f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                mesh.Frustum(pale, new Vector3(side * 0.2f, 1.98f, 0f), new Vector3(side * 0.06f, 1.72f, 0.2f), 0.06f, 0.05f, 6, Vector3.up);
                // A folded wing, swept back from the shoulder.
                var outline = new[]
                {
                    new Vector2(0f, 0f), new Vector2(side * 0.2f, -0.75f), new Vector2(side * 0.5f, -0.4f),
                    new Vector2(side * 0.72f, 0.3f), new Vector2(side * 0.62f, 0.95f), new Vector2(side * 0.2f, 0.6f)
                };
                Prism(mesh, pale, outline, 0.08f, new Vector3(side * 0.12f, 1.75f, -0.18f), Quaternion.Euler(0f, side * 32f, 0f));
            }
            mesh.Lump(moss, new Vector3(0.3f, 0.79f, 0.25f), new Vector3(0.25f, 0.06f, 0.2f), 6, 3, Quaternion.identity);
            mesh.Build(statue, "Statue");
            var blocker = statue.gameObject.AddComponent<CapsuleCollider>();
            blocker.center = Vector3.up * 1.2f;
            blocker.radius = 0.6f;
            blocker.height = 2.4f;
        }

        // ------------------------------------------------------------------ graves

        /// <summary>A grave of the kind the older parts of the game place one at a time.</summary>
        private void Tombstone(Transform t, Vector3 p)
        {
            Grave(t, p, R(200f, 250f));
        }

        // One grave: a headstone of one of several cuts (some leaning, some broken) and a sunken
        // mound before it. Its stone faces along yaw.
        private GameObject Grave(Transform t, Vector3 p, float yaw, bool mound = true)
        {
            Transform grave = Holder(t, "Grave", p, Quaternion.Euler(0f, yaw, 0f));
            var mesh = new PieceMesh();
            int stone = mesh.Slot(Coin() ? kit.Mat("Tombstone") : kit.Mat("TombstoneDark"));
            Quaternion lean = Quaternion.Euler(R(-9f, 5f), 0f, R(-5f, 5f));
            Vector3 right = lean * Vector3.right, up = lean * Vector3.up, front = lean * Vector3.forward;
            float height = 1f;
            double kind = rng.NextDouble();
            if (kind < 0.5)
            {
                float w = R(0.28f, 0.4f);
                height = R(0.8f, 1.2f);
                mesh.Block(stone, Vector3.up * 0.06f, Vector3.right * (w + 0.08f), Vector3.up * 0.06f, Vector3.forward * 0.15f);
                Prism(mesh, stone, kind < 0.32 ? ArchOutline(w, height) : PointedOutline(w, height), 0.15f, Vector3.up * 0.08f, lean);
            }
            else if (kind < 0.74)
            {
                // A cross, a quarter of them with the ring of a wheel-head.
                height = R(1.2f, 1.5f);
                mesh.Block(stone, Vector3.up * 0.1f, Vector3.right * 0.24f, Vector3.up * 0.1f, Vector3.forward * 0.2f);
                mesh.Block(stone, up * (height * 0.5f + 0.1f), right * 0.07f, up * height * 0.5f, front * 0.07f);
                Vector3 hub = up * (height * 0.78f + 0.1f);
                mesh.Block(stone, hub, right * 0.3f, up * 0.07f, front * 0.07f);
                if (kind > 0.68)
                    for (int k = 0; k < 10; k++)
                    {
                        float a0 = k * 36f * Mathf.Deg2Rad, a1 = (k + 1) * 36f * Mathf.Deg2Rad;
                        mesh.Beam(stone, hub + (right * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * 0.2f,
                            hub + (right * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * 0.2f, 0.035f, front * 0.05f);
                    }
            }
            else if (kind < 0.84)
            {
                height = R(1.5f, 1.9f);
                mesh.Block(stone, Vector3.up * 0.15f, Vector3.right * 0.3f, Vector3.up * 0.15f, Vector3.forward * 0.3f);
                Vector3 across = new Vector3(1f, 0f, 1f);
                mesh.Frustum(stone, Vector3.up * 0.3f, up * (height - 0.25f), 0.24f, 0.16f, 4, across);
                mesh.Frustum(stone, up * (height - 0.25f), up * height, 0.16f, 0.005f, 4, across);
            }
            else
            {
                // Snapped: a jagged stump, the top lying on the grave.
                float w = R(0.3f, 0.38f);
                height = 0.5f;
                Prism(mesh, stone, new[] { new Vector2(-w, 0f), new Vector2(w, 0f), new Vector2(w, 0.34f), new Vector2(0.1f, 0.44f),
                    new Vector2(-0.12f, 0.3f), new Vector2(-w, 0.4f) }, 0.15f, Vector3.zero, lean);
                Prism(mesh, stone, ArchOutline(w, 0.6f), 0.15f, new Vector3(R(-0.1f, 0.1f), 0.2f, 0.75f),
                    Quaternion.Euler(-82f, R(-25f, 25f), 0f));
            }
            if (mound)
            {
                int soil = mesh.Slot(GraveEarth);
                mesh.Lump(soil, new Vector3(0f, -0.06f, 0.9f), new Vector3(0.46f, 0.22f, 0.82f), 10, 4, Quaternion.Euler(0f, R(-4f, 4f), 0f));
                if (rng.NextDouble() < 0.4)
                {
                    int moss = mesh.Slot(kit.Mat("Moss"));
                    mesh.Lump(moss, new Vector3(R(-0.15f, 0.15f), 0.08f, R(0.6f, 1.2f)), new Vector3(0.2f, 0.06f, 0.26f), 6, 3, Quaternion.identity);
                }
            }
            GameObject built = mesh.Build(grave, "Stone");
            built.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var blocker = grave.gameObject.AddComponent<BoxCollider>();
            blocker.center = Vector3.up * (height * 0.5f);
            blocker.size = new Vector3(0.7f, height, 0.3f);
            return grave.gameObject;
        }

        private static Vector2[] ArchOutline(float w, float height)
        {
            var outline = new List<Vector2> { new Vector2(-w, 0f), new Vector2(w, 0f) };
            for (int k = 0; k <= 6; k++)
            {
                float a = k * Mathf.PI / 6f;
                outline.Add(new Vector2(Mathf.Cos(a) * w, height - w + Mathf.Sin(a) * w));
            }
            return outline.ToArray();
        }

        private static Vector2[] PointedOutline(float w, float height)
        {
            return new[] { new Vector2(-w, 0f), new Vector2(w, 0f), new Vector2(w, height * 0.72f),
                new Vector2(w * 0.55f, height * 0.92f), new Vector2(0f, height), new Vector2(-w * 0.55f, height * 0.92f), new Vector2(-w, height * 0.72f) };
        }

        // A flat stone cut to a star-shaped outline (x across, y up), `thickness` deep along forward.
        private static void Prism(PieceMesh mesh, int slot, Vector2[] outline, float thickness, Vector3 origin, Quaternion rotation)
        {
            Vector2 mid = Vector2.zero;
            foreach (Vector2 v in outline) mid += v;
            mid /= outline.Length;
            float half = thickness * 0.5f;
            Vector3 P(Vector2 v, float z) => origin + rotation * new Vector3(v.x, v.y, z);
            Vector3 forward = rotation * Vector3.forward;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                mesh.Tri(slot, P(mid, half), P(a, half), P(b, half), forward);
                mesh.Tri(slot, P(mid, -half), P(a, -half), P(b, -half), -forward);
                Vector2 edge = (a + b) * 0.5f - mid;
                mesh.Quad(slot, P(a, -half), P(b, -half), P(b, half), P(a, half), rotation * new Vector3(edge.x, edge.y, 0f));
            }
        }

        // One to three old graves out on their own, off the yards.
        private GameObject LoneGraves(Transform t, Vector3 p)
        {
            GameObject last = null;
            int count = rng.Next(1, 4);
            for (int k = 0; k < count; k++)
                last = Grave(t, p + Flat(k * 1.5f - (count - 1) * 0.75f, R(-0.4f, 0.4f)), R(195f, 255f));
            return last;
        }

        // A grave the dead have climbed out of: the pit, the spoil heap with a spade in it, the
        // coffin lid thrown aside, the headstone knocked flat and bones about the edge.
        private void OpenGrave(Transform t, Vector3 p, float yaw)
        {
            Transform grave = Holder(t, "OpenGrave", p, Quaternion.Euler(0f, yaw, 0f));
            var mesh = new PieceMesh();
            int soil = mesh.Slot(MudFloor);
            int gloom = mesh.Slot(VoidMaterial);
            int wood = mesh.Slot(kit.Mat("Wood"));
            int iron = mesh.Slot(kit.Mat("Iron"));
            int stone = mesh.Slot(kit.Mat("TombstoneDark"));
            mesh.Block(gloom, Vector3.up * 0.015f, Vector3.right * 0.42f, Vector3.up * 0.01f, Vector3.forward * 0.95f);
            for (int side = -1; side <= 1; side += 2)
            {
                mesh.Block(soil, new Vector3(side * 0.5f, 0.05f, 0f), Vector3.right * 0.1f, Vector3.up * 0.06f, Vector3.forward * 1.05f);
                mesh.Block(soil, new Vector3(0f, 0.05f, side * 1.03f), Vector3.right * 0.6f, Vector3.up * 0.06f, Vector3.forward * 0.1f);
            }
            mesh.Lump(soil, new Vector3(1.3f, 0f, 0.1f), new Vector3(0.75f, 0.5f, 1.15f), 8, 4, Quaternion.Euler(0f, 8f, 0f));
            mesh.Beam(wood, new Vector3(1.15f, 0.3f, -0.2f), new Vector3(0.95f, 1.45f, -0.5f), 0.03f, Vector3.forward * 0.03f);
            mesh.Block(iron, new Vector3(1.17f, 0.22f, -0.17f), Vector3.right * 0.12f, Vector3.up * 0.15f, Vector3.forward * 0.015f);
            Quaternion thrown = Quaternion.Euler(0f, 0f, -18f);
            mesh.Block(wood, new Vector3(-0.85f, 0.2f, 0.1f), thrown * Vector3.right * 0.3f, thrown * Vector3.up * 0.03f, Vector3.forward * 0.95f);
            Prism(mesh, stone, ArchOutline(0.34f, 0.95f), 0.15f, new Vector3(0.1f, 0.08f, -1.25f), Quaternion.Euler(-80f, R(-20f, 20f), 0f));
            mesh.Build(grave, "Pit");
            var blocker = grave.gameObject.AddComponent<BoxCollider>();
            blocker.center = new Vector3(1.3f, 0.35f, 0.1f);
            blocker.size = new Vector3(1.3f, 0.7f, 2.1f);
            Bones(grave, grave.TransformPoint(new Vector3(-0.3f, 0f, 1.5f)));
            if (Coin()) Bones(grave, grave.TransformPoint(new Vector3(0.6f, 0f, -1.9f)));
        }

        // An iron crook on a post with a lantern hanging from it, burning with green spirit fire.
        private void SpiritLantern(Transform t, Vector3 p, float yaw)
        {
            Transform lantern = Holder(t, "SpiritLantern", p, Quaternion.Euler(0f, yaw, 0f));
            var mesh = new PieceMesh();
            int iron = mesh.Slot(kit.Mat("Iron"));
            mesh.Frustum(iron, Vector3.zero, Vector3.up * 2.5f, 0.07f, 0.045f, 6, Vector3.forward);
            mesh.Frustum(iron, Vector3.up * 2.42f, new Vector3(0.6f, 2.62f, 0f), 0.04f, 0.035f, 5, Vector3.forward);
            mesh.Frustum(iron, new Vector3(0.6f, 2.62f, 0f), new Vector3(0.62f, 2.45f, 0f), 0.025f, 0.02f, 5, Vector3.forward);
            Vector3 cage = new Vector3(0.62f, 2.15f, 0f);
            mesh.Frustum(iron, cage + Vector3.up * 0.16f, cage + Vector3.up * 0.3f, 0.17f, 0.04f, 4, new Vector3(1f, 0f, 1f));
            mesh.Block(iron, cage - Vector3.up * 0.15f, Vector3.right * 0.13f, Vector3.up * 0.02f, Vector3.forward * 0.13f);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    mesh.Block(iron, cage + new Vector3(x * 0.11f, 0f, z * 0.11f), Vector3.right * 0.015f, Vector3.up * 0.16f, Vector3.forward * 0.015f);
            mesh.Build(lantern, "Lantern");
            Vector3 flame = lantern.TransformPoint(cage);
            Flame(lantern, flame, 0.07f, GlowMaterial(SpiritBody, SpiritCore), 1.6f, 0.2f);
            Glow(lantern, flame, SpiritLight, 8f, 2.4f, flicker: true);
            var blocker = lantern.gameObject.AddComponent<CapsuleCollider>();
            blocker.center = Vector3.up * 1.25f;
            blocker.radius = 0.15f;
            blocker.height = 2.5f;
        }

        // ------------------------------------------------------------------ shared props

        private GameObject Candles(Transform t, Vector3 p)
        {
            var group = new GameObject("Candles").transform;
            group.SetParent(t, false);
            group.position = p;
            int n = rng.Next(2, 5);
            for (int k = 0; k < n; k++)
            {
                Vector3 o = new Vector3(R(-0.3f, 0.3f), 0f, R(-0.3f, 0.3f));
                float h = R(0.15f, 0.4f);
                LocalCyl(group, o + Vector3.up * h * 0.5f, 0.05f, h, kit.Mat("Candle"), solid: false);
                LocalFlame(group, o + Vector3.up * (h + 0.04f), 0.035f, kit.Mat("Ember"));
            }
            Glow(group, p + Vector3.up * 0.6f, CandleLight, 4.5f, 2.5f, flicker: true);
            return group.gameObject;
        }

        // A gnarled dead tree: a leaning, kinked trunk on flared roots, splitting into crooked
        // branches that fork and taper to points. One mesh.
        private GameObject DeadTree(Transform t, Vector3 p, Material wood, float size = 1f)
        {
            Transform tree = Holder(t, "DeadTree", p, Quaternion.Euler(0f, R(0f, 360f), 0f));
            var mesh = new PieceMesh();
            int bark = mesh.Slot(wood);
            float height = R(3.4f, 5.2f) * size, radius = R(0.2f, 0.27f) * size;

            int roots = rng.Next(3, 5);
            for (int k = 0; k < roots; k++)
            {
                Vector3 out_ = Quaternion.Euler(0f, k * 360f / roots + R(-20f, 20f), 0f) * Vector3.forward;
                mesh.Frustum(bark, Vector3.up * 0.4f * size, out_ * R(0.7f, 1.1f) * size + Vector3.down * 0.06f, radius * 0.7f, 0.03f, 5, Vector3.up);
            }

            Vector3 at = Vector3.zero, dir = Vector3.up;
            Vector3 lean = Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward;
            float r = radius * 1.15f;
            const int segments = 4;
            for (int s = 0; s < segments; s++)
            {
                dir = (dir + lean * R(0.05f, 0.2f) + Flat(R(-0.15f, 0.15f), R(-0.15f, 0.15f))).normalized;
                Vector3 next = at + dir * height / segments;
                float nr = Mathf.Lerp(radius, radius * 0.35f, (s + 1f) / segments);
                mesh.Frustum(bark, at, next, r, nr, 6, Across(dir));
                if (s >= 1 && s < segments - 1 && rng.NextDouble() < 0.8)
                    Branch(mesh, bark, next, Bend(dir, R(35f, 60f)), height * R(0.25f, 0.35f), nr * 0.8f, 1);
                at = next;
                r = nr;
            }
            Branch(mesh, bark, at, Bend(dir, R(12f, 28f)), height * 0.32f, r, 2);
            Branch(mesh, bark, at, Bend(dir, R(25f, 45f)), height * 0.28f, r * 0.85f, 2);
            mesh.Build(tree, "Wood");

            var trunk = tree.gameObject.AddComponent<CapsuleCollider>();
            trunk.radius = radius * 1.4f;
            trunk.height = height;
            trunk.center = Vector3.up * height * 0.5f;
            return tree.gameObject;
        }

        // A crooked limb in two pieces with a kink, forking again until depth runs out.
        private void Branch(PieceMesh mesh, int slot, Vector3 from, Vector3 dir, float length, float radius, int depth)
        {
            Vector3 mid = from + dir * length * 0.5f;
            Vector3 dir2 = (dir + Flat(R(-0.3f, 0.3f), R(-0.3f, 0.3f)) + Vector3.up * R(-0.1f, 0.15f)).normalized;
            Vector3 end = mid + dir2 * length * 0.5f;
            float rm = radius * 0.7f, re = depth == 0 ? 0.008f : radius * 0.45f;
            mesh.Frustum(slot, from, mid, radius, rm, 5, Across(dir));
            mesh.Frustum(slot, mid, end, rm, re, 5, Across(dir2));
            if (depth == 0) return;
            if (rng.NextDouble() < 0.5)
                Branch(mesh, slot, mid, Bend(dir2, R(30f, 55f)), length * 0.5f, rm * 0.7f, depth - 1);
            Branch(mesh, slot, end, Bend(dir2, R(10f, 35f)), length * 0.65f, re, depth - 1);
            if (rng.NextDouble() < 0.5)
                Branch(mesh, slot, end, Bend(dir2, R(30f, 50f)), length * 0.55f, re * 0.8f, depth - 1);
        }

        private static Vector3 Across(Vector3 dir) => Mathf.Abs(dir.y) > 0.9f ? Vector3.forward : Vector3.up;

        // Turns dir by some degrees about a random axis across it, never letting a limb droop below level.
        private Vector3 Bend(Vector3 dir, float degrees)
        {
            Vector3 axis = Vector3.Cross(dir, Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward);
            if (axis.sqrMagnitude < 0.01f) axis = Vector3.right;
            Vector3 bent = Quaternion.AngleAxis(degrees, axis.normalized) * dir;
            if (bent.y < 0.1f) bent = new Vector3(bent.x, 0.1f, bent.z);
            return bent.normalized;
        }

        // A skull and a long bone or two lying in the dirt, sometimes with a ribcage.
        private GameObject Bones(Transform t, Vector3 p)
        {
            Transform group = Holder(t, "Bones", p, Quaternion.Euler(0f, R(0f, 360f), 0f));
            var mesh = new PieceMesh();
            int bone = mesh.Slot(kit.Mat("Bone"));
            int hollow = mesh.Slot(kit.Mat("Charred"));
            Skull(mesh, bone, hollow, new Vector3(0f, 0.09f, 0f), Quaternion.Euler(R(-15f, 10f), R(-40f, 40f), R(-20f, 20f)), 1f);
            int count = rng.Next(1, 3);
            for (int k = 0; k < count; k++)
            {
                Vector3 along = Quaternion.Euler(0f, R(0f, 180f), 0f) * Vector3.forward;
                Vector3 middle = Flat(R(-0.45f, 0.45f), R(-0.45f, 0.45f)) + Vector3.up * 0.03f;
                float length = R(0.35f, 0.5f);
                Vector3 a = middle - along * length * 0.5f, b = middle + along * length * 0.5f;
                mesh.Frustum(bone, a, b, 0.022f, 0.02f, 5, Vector3.up);
                mesh.Lump(bone, a, new Vector3(0.04f, 0.033f, 0.04f), 5, 3, Quaternion.identity);
                mesh.Lump(bone, b, new Vector3(0.036f, 0.03f, 0.036f), 5, 3, Quaternion.identity);
            }
            if (Coin())
            {
                // A ribcage on its back: the spine and four pairs of curved ribs.
                Vector3 chest = Flat(R(-0.3f, 0.3f), R(0.25f, 0.5f));
                mesh.Frustum(bone, chest + new Vector3(-0.25f, 0.03f, 0f), chest + new Vector3(0.25f, 0.03f, 0f), 0.022f, 0.018f, 5, Vector3.up);
                for (int k = 0; k < 4; k++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 r0 = chest + new Vector3(-0.18f + k * 0.11f, 0.03f, 0f);
                        Vector3 r1 = r0 + new Vector3(0f, 0.08f, side * 0.1f);
                        Vector3 r2 = r0 + new Vector3(0.02f, 0.07f, side * 0.2f);
                        Vector3 r3 = r0 + new Vector3(0.04f, 0.01f, side * 0.25f);
                        mesh.Frustum(bone, r0, r1, 0.012f, 0.012f, 4, Vector3.right);
                        mesh.Frustum(bone, r1, r2, 0.012f, 0.011f, 4, Vector3.right);
                        mesh.Frustum(bone, r2, r3, 0.011f, 0.008f, 4, Vector3.right);
                    }
            }
            NoShadows(mesh.Build(group, "Remains"));
            return group.gameObject;
        }

        // A skull facing along the rotation's forward: cranium, jaw, eye sockets and nose.
        private static void Skull(PieceMesh mesh, int bone, int hollow, Vector3 c, Quaternion rotation, float scale)
        {
            mesh.Lump(bone, c, new Vector3(0.12f, 0.1f, 0.13f) * scale, 7, 4, rotation);
            mesh.Lump(bone, c + rotation * new Vector3(0f, -0.06f, 0.07f) * scale, new Vector3(0.08f, 0.05f, 0.07f) * scale, 6, 3, rotation);
            for (int side = -1; side <= 1; side += 2)
                mesh.Lump(hollow, c + rotation * new Vector3(side * 0.045f, 0f, 0.11f) * scale, Vector3.one * 0.032f * scale, 5, 3, rotation);
            mesh.Lump(hollow, c + rotation * new Vector3(0f, -0.035f, 0.125f) * scale, Vector3.one * 0.016f * scale, 4, 2, rotation);
        }
    }
}

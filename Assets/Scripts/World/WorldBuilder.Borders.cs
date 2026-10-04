using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.World
{
    // Area outlines: each area is an irregular blob (AreaShape) instead of a square. Its ground
    // is cut to the shape with darkness beyond, an invisible wall follows the edge, and the edge
    // itself is dressed in stretches of things you plausibly can't cross: dense forest, rivers,
    // cliffs of boulders, lava, ice.
    public partial class WorldBuilder
    {
        private enum EdgeKind { Forest, DeadForest, FrostForest, Rocks, IceRocks, River, Lava, IceRiver }

        // Base radius per area (the outline wobbles round it), and what its edge is made of.
        private static readonly float[] BaseRadii = { 86f, 80f, 86f, 88f, 86f };

        private static readonly EdgeKind[][] EdgeKinds =
        {
            new[] { EdgeKind.Forest, EdgeKind.Forest, EdgeKind.Rocks, EdgeKind.River },        // Greenwood
            new[] { EdgeKind.Forest, EdgeKind.River, EdgeKind.Rocks },                         // Haven
            new[] { EdgeKind.DeadForest, EdgeKind.Rocks, EdgeKind.River },                     // Graveyard
            new[] { EdgeKind.Rocks, EdgeKind.Lava, EdgeKind.DeadForest },                      // Ruins
            new[] { EdgeKind.FrostForest, EdgeKind.IceRocks, EdgeKind.IceRiver }               // Frozen
        };

        // The scattered scenery fills the bigger areas: wide scatters place this many times more.
        private const float ScatterScale = 2.2f;
        // Gates sit this far inside the edge, with a gap of this radius left in the border round them.
        private const float GateInset = 9f;
        private const float GateGap = 9f;

        private static AreaShape[] shapes;
        private readonly List<(int area, Vector3 position)> gatePoints = new List<(int, Vector3)>();

        /// <summary>An area's outline.</summary>
        public static AreaShape Shape(int area)
        {
            if (area == ActArena) return new AreaShape(ActArenaCenter, 40f, 705);
            if (shapes == null)
                InitShapes();
            return shapes[Mathf.Clamp(area, 0, shapes.Length - 1)];
        }

        private static void InitShapes()
        {
            shapes = new AreaShape[Centers.Length];
            for (int a = 0; a < Centers.Length; a++)
                shapes[a] = new AreaShape(Centers[a], BaseRadii[a], 700 + a);
        }

        // Domain reload is off in this project, so statics must be reset per play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShapes()
        {
            shapes = null;
        }

        // ------------------------------------------------------------------ ground and walls

        // Cuts a ground object to its area's outline (a little past the edge, under the border
        // dressing), lays darkness under everything beyond, and walls the edge.
        private void ShapeGround(GameObject ground, int area)
        {
            AreaShape shape = Shape(area);
            Mesh mesh = shape.BuildGroundMesh(18f);
            ground.transform.position = new Vector3(shape.Center.x, ground.transform.position.y, shape.Center.z);
            ground.transform.rotation = Quaternion.identity;
            ground.transform.localScale = Vector3.one;
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = ground.GetComponent<MeshCollider>();
            if (collider != null)
                collider.sharedMesh = mesh;

            // Beyond the ground: black, so the edge fades into darkness instead of showing the sky.
            GameObject dark = RuntimePrimitives.Create(PrimitiveType.Cylinder, root, Color.black);
            dark.name = "Dark_" + AreaNames[area];
            float size = (AreaShape.MaxRadius + 24f) * 2f;
            dark.transform.position = new Vector3(shape.Center.x, ground.transform.position.y - 0.08f, shape.Center.z);
            dark.transform.localScale = new Vector3(size, 0.02f, size);

            // The wall: short boxes end to end round the outline, just past the walkable edge.
            var bounds = new GameObject("Bounds_" + AreaNames[area]).transform;
            bounds.SetParent(root, false);
            const int segments = 96;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 p0 = shape.Center + AreaShape.Direction(a0) * (shape.RadiusAt(a0) + 0.6f);
                Vector3 p1 = shape.Center + AreaShape.Direction(a1) * (shape.RadiusAt(a1) + 0.6f);
                var wall = new GameObject("Wall");
                wall.transform.SetParent(bounds, false);
                wall.transform.SetPositionAndRotation((p0 + p1) * 0.5f + Vector3.up * 5f, Quaternion.LookRotation(p1 - p0));
                wall.AddComponent<BoxCollider>().size = new Vector3(1f, 10f, Vector3.Distance(p0, p1) + 0.4f);
            }
        }

        // A spot for a gate: inside the edge in the given direction from the area's centre.
        private Vector3 GatePoint(int area, Vector3 direction)
        {
            return Shape(area).EdgePoint(direction, GateInset);
        }

        private bool NearGate(int area, Vector3 p, float radius)
        {
            foreach ((int a, Vector3 g) in gatePoints)
            {
                if (a != area)
                    continue;
                float dx = g.x - p.x;
                float dz = g.z - p.z;
                if (dx * dx + dz * dz < radius * radius)
                    return true;
            }
            return false;
        }

        // A dirt road from the town plaza out to each of Haven's gates.
        private void BuildHavenRoads()
        {
            Vector3 c = Centers[Haven];
            Transform t = Group("Haven");
            foreach ((int a, Vector3 g) in gatePoints)
            {
                if (a != Haven)
                    continue;
                Vector3 from = c + (g - c).normalized * 11f;
                Vector3 span = g - from;
                span.y = 0f;
                Box(t, (from + g) * 0.5f + Vector3.up * 0.025f, new Vector3(3.6f, 0.05f, span.magnitude), kit.Mat("TanDark"),
                    solid: false, euler: new Vector3(0f, Mathf.Atan2(span.x, span.z) * Mathf.Rad2Deg, 0f));
            }
        }

        // ------------------------------------------------------------------ border dressing

        private void BuildBorders()
        {
            for (int area = 0; area < Centers.Length; area++)
            {
                Begin(area, 900 + area);
                Transform t = Group("Border_" + AreaNames[area]);
                EdgeKind[] kinds = EdgeKinds[area];

                // Split the edge into a few stretches of different kinds.
                int arcs = rng.Next(4, 7);
                var weights = new float[arcs];
                float total = 0f;
                for (int k = 0; k < arcs; k++)
                    total += weights[k] = R(0.6f, 1.4f);

                float angle = R(0f, Mathf.PI * 2f);
                int previous = -1;
                for (int k = 0; k < arcs; k++)
                {
                    float span = weights[k] / total * Mathf.PI * 2f;
                    int pick = rng.Next(kinds.Length);
                    if (pick == previous && kinds.Length > 1)
                        pick = (pick + 1) % kinds.Length;
                    previous = pick;
                    DressArc(area, t, kinds[pick], angle, angle + span);
                    angle += span;
                }

                Physics.SyncTransforms(); // the spurs check for what's already standing
                Spurs(area, t, kinds);
            }
        }

        private void DressArc(int area, Transform t, EdgeKind kind, float from, float to)
        {
            switch (kind)
            {
                case EdgeKind.Forest:
                case EdgeKind.FrostForest:
                    Trees(area, t, kind, from, to, rows: 3, spacing: 3.2f, offset: 0f);
                    break;
                case EdgeKind.DeadForest:
                    // Bare trunks hide nothing, so a dead wood needs far more of them (and rubble
                    // between) to read as a wall rather than a few sticks to walk between.
                    Trees(area, t, kind, from, to, rows: 4, spacing: 1.9f, offset: 0f);
                    break;
                case EdgeKind.Rocks:
                case EdgeKind.IceRocks:
                    Boulders(area, t, kind == EdgeKind.IceRocks, from, to);
                    break;
                default:
                    River(area, t, kind, from, to);
                    break;
            }
        }

        // Rows of trees along the edge, jittered so the line is ragged.
        private void Trees(int area, Transform t, EdgeKind kind, float from, float to, int rows, float spacing, float offset)
        {
            AreaShape shape = Shape(area);
            for (float a = from; a < to;)
            {
                float r = shape.RadiusAt(a);
                for (int row = 0; row < rows; row++)
                {
                    if (row == rows - 1 && rng.NextDouble() < 0.3)
                        continue;
                    float aa = a + R(-0.5f, 0.5f) * spacing / r;
                    float d = shape.RadiusAt(aa) + offset + R(-1.5f, 1.5f) + row * 3.4f;
                    Vector3 p = shape.Center + AreaShape.Direction(aa) * d;
                    if (NearGate(area, p, GateGap))
                        continue;
                    PlaceTree(area, t, kind, p);
                }
                // Charred stumps and rubble fill the gaps along the front of a dead wood.
                if (kind == EdgeKind.DeadForest && kit.rock != null)
                {
                    Vector3 p = shape.Center + AreaShape.Direction(a + R(-0.5f, 0.5f) * spacing / r) * (r + offset + R(-0.5f, 2.5f));
                    if (!NearGate(area, p, GateGap))
                    {
                        GameObject rubble = Prefab(kit.rock, t, p, R(0f, 360f), new Vector3(R(1.0f, 1.8f), R(0.6f, 1.2f), R(1.0f, 1.8f)));
                        Tint(rubble, area == Ruins ? new Color(0.30f, 0.26f, 0.24f) : new Color(0.45f, 0.45f, 0.48f));
                        NoShadows(rubble);
                    }
                }

                // A few bushes in front soften the line.
                if (kind == EdgeKind.Forest && rng.NextDouble() < 0.35)
                {
                    Vector3 p = shape.Center + AreaShape.Direction(a) * (r + offset - R(1f, 2.5f));
                    if (!NearGate(area, p, GateGap))
                        NoShadows(Prefab(kit.bushSmall, t, p, R(0f, 360f), Vector3.one * R(0.4f, 0.7f)));
                }
                a += spacing / r;
            }
        }

        // Big boulders in two ragged rows (ice spikes among them in the Frozen Hollow).
        private void Boulders(int area, Transform t, bool ice, float from, float to)
        {
            AreaShape shape = Shape(area);
            const float spacing = 4.2f;
            for (float a = from; a < to;)
            {
                float r = shape.RadiusAt(a);
                for (int row = 0; row < 2; row++)
                {
                    float aa = a + R(-0.4f, 0.4f) * spacing / r;
                    float d = shape.RadiusAt(aa) + R(-0.5f, 1.5f) + row * 4f;
                    Vector3 p = shape.Center + AreaShape.Direction(aa) * d;
                    if (NearGate(area, p, GateGap))
                        continue;
                    PlaceBoulder(t, ice, p);
                }
                if (rng.NextDouble() < 0.4)
                {
                    Vector3 p = shape.Center + AreaShape.Direction(a) * (r - R(1f, 2.5f));
                    if (!NearGate(area, p, GateGap))
                        NoShadows(Prefab(kit.rockSmall != null ? kit.rockSmall : kit.rock, t, p, R(0f, 360f), Vector3.one * R(0.6f, 1.1f)));
                }
                a += spacing / r;
            }
        }

        private void PlaceTree(int area, Transform t, EdgeKind kind, Vector3 p)
        {
            GameObject tree;
            if (kind == EdgeKind.DeadForest)
                tree = DeadTree(t, p, kit.Mat(area == Ruins ? "Charred" : "DeadWood"));
            else if (kind == EdgeKind.FrostForest)
                tree = FrostedPine(t, p);
            else
                tree = Prefab(Coin() ? kit.pine : kit.oak, t, p, R(0f, 360f), Vector3.one * R(1.1f, 1.6f));
            NoShadows(tree);
        }

        private void PlaceBoulder(Transform t, bool ice, Vector3 p)
        {
            GameObject rock;
            if (ice && rng.NextDouble() < 0.45)
                rock = IceSpike(t, p, R(3f, 6f), new Vector3(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)));
            else
            {
                rock = Prefab(kit.rock, t, p, R(0f, 360f), new Vector3(R(2.2f, 3.6f), R(1.6f, 3.2f), R(2.2f, 3.6f)));
                if (ice)
                    Tint(rock, new Color(0.80f, 0.88f, 0.95f));
            }
            NoShadows(rock);
        }

        // Ridges of the edge's own forest or rock reaching in from the edge, so an area is a few
        // pockets to walk round rather than one open field. They stop short of the middle, keep
        // clear of gates, arrival spots, the waystone and what's already built, and leave a gap
        // round their tip, so everywhere stays reachable.
        private void Spurs(int area, Transform t, EdgeKind[] kinds)
        {
            AreaShape shape = Shape(area);
            List<Vector3> keepClear = SafeSpots(area);
            keepClear.Add(WaystoneSpot(area));
            foreach (Vector3 spot in Spots.Values)
                keepClear.Add(spot);

            int count = area == Haven ? 2 : rng.Next(3, 5);
            for (int s = 0, attempts = 0; s < count && attempts < 30; attempts++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                if (NearGateAngle(area, angle, 0.5f))
                    continue;
                EdgeKind kind = kinds[rng.Next(kinds.Length)];
                bool rocky = kind == EdgeKind.Rocks || kind == EdgeKind.IceRocks || kind == EdgeKind.Lava;
                bool ice = kind == EdgeKind.IceRocks || kind == EdgeKind.IceRiver;
                if (kind == EdgeKind.River || kind == EdgeKind.IceRiver)
                    kind = area == Frozen ? EdgeKind.FrostForest : area == Graveyard ? EdgeKind.DeadForest : EdgeKind.Forest;

                // Inwards from the edge, bending a little as it goes.
                float r = shape.RadiusAt(angle);
                float length = R(0.35f, 0.5f) * r;
                Vector3 p = shape.Center + AreaShape.Direction(angle) * (r + 1f);
                Vector3 heading = -AreaShape.Direction(angle);
                float bend = R(-0.25f, 0.25f);
                for (float walked = 0f; walked < length; walked += rocky ? 3.2f : 2.6f)
                {
                    heading = Quaternion.Euler(0f, bend * 12f, 0f) * heading;
                    p += heading * (rocky ? 3.2f : 2.6f);
                    if (Vector3.Distance(p, shape.Center) < 28f)
                        break;
                    if (Near(keepClear, p, 10f) || NearGate(area, p, GateGap + 4f) || HitsScenery(p + Vector3.up * 1.2f, 1.2f))
                        continue;
                    Vector3 jitter = Flat(R(-0.8f, 0.8f), R(-0.8f, 0.8f));
                    if (rocky)
                        PlaceBoulder(t, ice, p + jitter);
                    else
                    {
                        PlaceTree(area, t, kind, p + jitter);
                        // Two trees wide, so it reads as a belt of woods (three for bare dead trees).
                        Vector3 side = Vector3.Cross(heading, Vector3.up);
                        PlaceTree(area, t, kind, p + side * R(1.8f, 2.6f) + jitter);
                        if (kind == EdgeKind.DeadForest)
                            PlaceTree(area, t, kind, p - side * R(1.2f, 1.8f) + jitter);
                    }
                }
                s++;
            }
        }

        private bool NearGateAngle(int area, float angle, float within)
        {
            Vector3 c = Centers[area];
            foreach ((int a, Vector3 g) in gatePoints)
            {
                if (a != area)
                    continue;
                float gateAngle = AreaShape.AngleOf(g - c);
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, gateAngle * Mathf.Rad2Deg)) < within * Mathf.Rad2Deg)
                    return true;
            }
            return false;
        }

        // A quad strip between the previous step's (lo, hi) edge and the current step's, in both
        // windings - it reads from above whichever way round the arc runs.
        private static void AddStrip(List<int> triangles, int prevLo, int prevHi, int curLo, int curHi)
        {
            triangles.AddRange(new[] { prevLo, curLo, prevHi, prevHi, curLo, curHi });
            triangles.AddRange(new[] { prevLo, prevHi, curLo, prevHi, curHi, curLo });
        }

        private static bool Near(List<Vector3> spots, Vector3 p, float radius)
        {
            foreach (Vector3 spot in spots)
            {
                float dx = spot.x - p.x;
                float dz = spot.z - p.z;
                if (dx * dx + dz * dz < radius * radius)
                    return true;
            }
            return false;
        }

        // A river (or lava, or frozen river) just past the edge, with a far bank of trees or rocks.
        // Raised rocky banks on both sides of the water (the ground mesh beneath is a single flat
        // sheet per area with no hole cut for the river, so sinking the water below it would just
        // hide it) - the water sits low between two levees, reading as a crossing you can't walk
        // through instead of a flat decal floating on the grass.
        private void River(int area, Transform t, EdgeKind kind, float from, float to)
        {
            AreaShape shape = Shape(area);
            string material = kind == EdgeKind.Lava ? "Lava" : kind == EdgeKind.IceRiver ? "Ice" : "Water";
            string bankMaterial = kind == EdgeKind.Lava ? "Charred" : "RockDark";
            const float waterY = 0.04f;
            const float bankRise = 0.85f;
            const float bankWidth = 2.2f;

            int steps = Mathf.Max(4, Mathf.CeilToInt((to - from) * shape.RadiusAt(from) / 2f));
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var bankTriangles = new List<int>();
            var waterTriangles = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)steps);
                float r = shape.RadiusAt(a);
                // Narrows to nothing at both ends, so it slips under the neighbouring stretch.
                float ends = Mathf.Clamp01(Mathf.Min(i, steps - i) / (steps * 0.08f));
                float wobble = Mathf.Sin(a * 9f + area) * 0.8f;
                float inner = r + 0.8f + wobble * 0.5f;
                float outer = inner + (5.5f + wobble) * ends;
                Vector3 dir = AreaShape.Direction(a);
                float bw = bankWidth * ends;
                float rise = bankRise * ends;

                // The slopes' normals (ignoring the curve along the river, which the toon shading
                // hides anyway): tilted up and in the direction the bank descends/ascends.
                Vector3 normalNear = (dir * rise + Vector3.up * bw).normalized;
                Vector3 normalFar = (-dir * rise + Vector3.up * bw).normalized;

                // Six vertices per step: bank top, then the water edge duplicated once per side so
                // each side can keep its own normal (flat for the water, sloped for the bank).
                vertices.Add(shape.Center + dir * (inner - bw) + Vector3.up * rise);            // 0 bank top, near
                vertices.Add(shape.Center + dir * inner + Vector3.up * waterY);                 // 1 water edge, near (bank side)
                vertices.Add(shape.Center + dir * inner + Vector3.up * waterY);                 // 2 water edge, near (water side)
                vertices.Add(shape.Center + dir * outer + Vector3.up * waterY);                 // 3 water edge, far (water side)
                vertices.Add(shape.Center + dir * outer + Vector3.up * waterY);                 // 4 water edge, far (bank side)
                vertices.Add(shape.Center + dir * (outer + bw) + Vector3.up * rise);            // 5 bank top, far
                normals.Add(normalNear);
                normals.Add(normalNear);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(normalFar);
                normals.Add(normalFar);

                if (i > 0)
                {
                    int p = vertices.Count - 12;
                    int c = vertices.Count - 6;
                    AddStrip(bankTriangles, p + 0, p + 1, c + 0, c + 1);
                    AddStrip(waterTriangles, p + 2, p + 3, c + 2, c + 3);
                    AddStrip(bankTriangles, p + 4, p + 5, c + 4, c + 5);
                }
            }

            var mesh = new Mesh { name = "River", vertices = vertices.ToArray(), normals = normals.ToArray() };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(bankTriangles, 0);
            mesh.SetTriangles(waterTriangles, 1);
            mesh.RecalculateBounds();

            var river = new GameObject(kind.ToString());
            river.transform.SetParent(t, false);
            river.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = river.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Only a flowing river scrolls; lava and frozen rivers (and anything else sharing these
            // materials, like ponds and the town well) stay still, so it gets its own material
            // instance rather than the shared one.
            if (kind == EdgeKind.River)
            {
                var flowing = new Material(kit.Mat(material));
                renderer.sharedMaterials = new[] { kit.Mat(bankMaterial), flowing };
                river.AddComponent<RiverFlow>().material = flowing;
            }
            else
            {
                renderer.sharedMaterials = new[] { kit.Mat(bankMaterial), kit.Mat(material) };
            }

            // Lava glows.
            if (kind == EdgeKind.Lava)
            {
                for (float a = from + 0.1f; a < to - 0.05f; a += 22f / shape.RadiusAt(a))
                    Glow(t, shape.Center + AreaShape.Direction(a) * (shape.RadiusAt(a) + 3.5f) + Vector3.up * 0.6f, LavaLight, 7f, 3f, flicker: true);
            }

            // The far bank: trees (or rocks by lava) past the water, some rocks on the near bank.
            EdgeKind bank = kind == EdgeKind.IceRiver ? EdgeKind.FrostForest : area == Graveyard ? EdgeKind.DeadForest : EdgeKind.Forest;
            if (kind == EdgeKind.Lava)
                Boulders(area, t, false, from, to);
            else
                Trees(area, t, bank, from, to, rows: 2, spacing: 4f, offset: 8.5f);
        }

        // Scrolls one river's own water texture so it reads as flowing; attached only to rivers, so
        // the same material shared by ponds and the town well stays still.
        private class RiverFlow : MonoBehaviour
        {
            public Material material;
            private static readonly Vector2 Speed = new Vector2(0f, 0.08f);

            private void Update()
            {
                material.mainTextureOffset += Speed * Time.deltaTime;
            }
        }

        private static void NoShadows(GameObject go)
        {
            if (go == null)
                return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void Tint(GameObject go, Color color)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                r.SetPropertyBlock(block);
            }
        }

        // ------------------------------------------------------------------ Greenwood's outskirts

        // Greenwood is the scene's original 100 m forest; the larger outline around it gets trees,
        // rocks and bushes in the same style, kept off the scene's own scenery.
        private void BuildGreenwoodOutskirts()
        {
            Begin(Greenwood, 606);
            AreaShape shape = Shape(Greenwood);
            Transform t = Group("Outskirts");
            for (int k = 0, attempts = 0; k < 120 && attempts < 1500; attempts++)
            {
                Vector3 p = shape.Center + Flat(R(-AreaShape.MaxRadius, AreaShape.MaxRadius), R(-AreaShape.MaxRadius, AreaShape.MaxRadius));
                if (Mathf.Abs(p.x) < 47f && Mathf.Abs(p.z) < 47f)
                    continue; // the scene's own forest
                if (!shape.Contains(p, 5f) || !Free(p, 2.4f) || HitsScenery(p + Vector3.up * 1.2f, 1.5f))
                    continue;

                double roll = rng.NextDouble();
                if (roll < 0.6)
                    Prefab(Coin() ? kit.pine : kit.oak, t, p, R(0f, 360f), Vector3.one * R(0.9f, 1.3f));
                else if (roll < 0.8)
                    Prefab(kit.rock, t, p, R(0f, 360f), Vector3.one * R(0.8f, 1.5f));
                else
                    Prefab(kit.bushSmall, t, p, R(0f, 360f), Vector3.one * R(0.3f, 0.5f));
                Claim(p, 2.4f);
                k++;
            }
        }
    }
}

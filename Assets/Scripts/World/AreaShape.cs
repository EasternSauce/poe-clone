using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.World
{
    /// <summary>Fixed rooms and routes shared by ground, walls, content placement and minimaps.</summary>
    public sealed class AreaShape
    {
        public const float MaxRadius = 170f;
        private const float Cell = 2f;
        public Vector3 Center { get; }
        public Vector2 Size { get; }
        public bool IsCave { get; }
        public bool IsCliff { get; }
        private readonly List<Region> rooms = new List<Region>();
        public readonly List<(Vector2 center, Vector2 size, float yaw)> Bridges = new List<(Vector2, Vector2, float)>();
        private readonly List<Region> waters = new List<Region>();
        private readonly List<int> sources = new List<int>();
        private bool ocean;
        private readonly List<Region> cuts = new List<Region>();
        public readonly List<(Vector3 a, Vector3 b)> Boundary = new List<(Vector3, Vector3)>();
        private Mesh groundMesh, landMesh;
        private float[,] landSamples, waterSamples;
        private readonly List<(Vector3 a, Vector3 b)> landBoundary = new List<(Vector3, Vector3)>();
        private float[,] gapSamples;
        private bool[,] filledGaps;

        private struct Region
        {
            public Vector2 a, b, radii;
            public bool corridor;
            // Water only: a non-zero seed bends lake outlines into lobes and makes river banks
            // meander and change width. Start is the river's length before this segment.
            public float seed, start;
            public float Distance(Vector2 p)
            {
                if (!corridor)
                {
                    Vector2 q = new Vector2((p.x - a.x) / radii.x, (p.y - a.y) / radii.y);
                    return (Edge(Mathf.Atan2(q.y, q.x)) - q.magnitude) * Mathf.Min(radii.x, radii.y);
                }
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                Vector2 closest = a + ab * t;
                if (seed == 0f) return radii.x - Vector2.Distance(p, closest);
                float length = ab.magnitude, along = start + t * length;
                Vector2 axis = ab / Mathf.Max(0.001f, length), side = new Vector2(-axis.y, axis.x);
                Vector2 offset = p - closest;
                return BankWidth(along) - new Vector2(Vector2.Dot(offset, side) - Sway(along), Vector2.Dot(offset, axis)).magnitude;
            }
            /// <summary>Lake outline radius (1 = the authored ellipse) in the given direction.</summary>
            public float Edge(float angle) => seed == 0f ? 1f : 1f + 0.13f * Mathf.Sin(2f * angle + seed) +
                0.08f * Mathf.Sin(3f * angle + seed * 1.7f) + 0.05f * Mathf.Sin(5f * angle + seed * 2.9f);
            public float Sway(float along) => radii.x * (0.4f * Mathf.Sin(along * 0.055f + seed) + 0.14f * Mathf.Sin(along * 0.16f + seed * 3f));
            public float BankWidth(float along) => radii.x * (1f + 0.2f * Mathf.Sin(along * 0.083f + seed * 2f) + 0.1f * Mathf.Sin(along * 0.23f + seed));
        }

        public AreaShape(Vector3 center, Vector2 size, bool cave = false, bool cliff = false)
        {
            Center = new Vector3(center.x, 0f, center.z);
            Size = size; IsCave = cave; IsCliff = cliff;
        }

        // Compatibility for simple outlines. The seed no longer generates layout variation.
        public AreaShape(Vector3 center, float radius, int seed) : this(center, new Vector2(radius * 2f, radius * 1.7f))
        { Room(0, 0, radius, radius * 0.85f); }

        public AreaShape Room(float x, float z, float rx, float rz)
        {
            gapSamples = null;
            rooms.Add(new Region { a = new Vector2(x, z), radii = new Vector2(rx, rz) });
            return this;
        }

        public AreaShape Route(float width, params float[] points)
        {
            gapSamples = null;
            for (int i = 0; i + 3 < points.Length; i += 2)
                rooms.Add(new Region { a = new Vector2(points[i], points[i + 1]), b = new Vector2(points[i + 2], points[i + 3]), radii = Vector2.one * (width * 0.5f), corridor = true });
            return this;
        }

        public AreaShape Exclude(float x, float z, float rx, float rz)
        {
            cuts.Add(new Region { a = new Vector2(x, z), radii = new Vector2(rx, rz) });
            return this;
        }

        public AreaShape Lake(float x, float z, float rx, float rz)
        {
            waters.Add(new Region { a = new Vector2(x, z), radii = new Vector2(rx, rz), seed = WaterSeed(x, z) });
            return this;
        }

        /// <summary>A river through the given knots. It flows from the first knot to the last;
        /// the first lies off the area's edge, where the river falls in.</summary>
        public AreaShape River(float width, params float[] points)
        {
            float seed = WaterSeed(points[0], points[1]), along = 0f;
            sources.Add(waters.Count);
            for (int i = 0; i + 3 < points.Length; i += 2)
            {
                var segment = new Region { a = new Vector2(points[i], points[i + 1]), b = new Vector2(points[i + 2], points[i + 3]),
                    radii = Vector2.one * width * 0.5f, corridor = true, seed = seed, start = along };
                waters.Add(segment);
                along += Vector2.Distance(segment.a, segment.b);
            }
            return this;
        }
        private static float WaterSeed(float x, float z) => 0.5f + Mathf.Repeat(x * 0.37f + z * 0.61f, 6f);

        /// <summary>Each river's source: where it first meets land on either bank (local position,
        /// downstream direction and the channel's half-width there).</summary>
        public IEnumerable<(Vector2 point, Vector2 downstream, float halfWidth)> RiverSources()
        {
            foreach (int first in sources)
                for (int i = first; i < waters.Count && waters[i].corridor && waters[i].seed == waters[first].seed; i++)
                {
                    Region segment = waters[i];
                    Vector2 axis = (segment.b - segment.a).normalized, side = new Vector2(-axis.y, axis.x);
                    float length = Vector2.Distance(segment.a, segment.b), found = -1f;
                    for (float d = 0f; d < length && found < 0f; d += 1f)
                    {
                        float half = segment.BankWidth(segment.start + d);
                        Vector2 middle = segment.a + axis * d + side * segment.Sway(segment.start + d);
                        if (Mathf.Max(OpenDistance(middle + side * (half + 3f)), OpenDistance(middle - side * (half + 3f))) > 2f)
                            found = d;
                    }
                    if (found < 0f) continue;
                    float along = segment.start + found;
                    yield return (segment.a + axis * found + side * segment.Sway(along), axis, segment.BankWidth(along));
                    break;
                }
        }

        /// <summary>Points round every lake's shore (local), each with its outward direction.</summary>
        public IEnumerable<(Vector2 point, Vector2 outward)> LakeShores(float spacing)
        {
            foreach (Region lake in waters)
            {
                if (lake.corridor) continue;
                int count = Mathf.Max(8, Mathf.RoundToInt(Mathf.PI * (lake.radii.x + lake.radii.y) / spacing));
                for (int k = 0; k < count; k++)
                {
                    float angle = (k + 0.5f) * Mathf.PI * 2f / count;
                    Vector2 round = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 point = lake.a + Vector2.Scale(round, lake.radii) * lake.Edge(angle);
                    yield return (point, new Vector2(round.x / lake.radii.x, round.y / lake.radii.y).normalized);
                }
            }
        }

        public AreaShape Ocean() { ocean = true; return this; }
        public bool HasWater => ocean || waters.Count > 0;
        public bool HasOcean => ocean;
        public float WaterDistance(Vector3 world) => WaterDistance(new Vector2(world.x - Center.x, world.z - Center.z));
        // Keep environmental audio aligned to the same authored water used by the ground and bridges.
        public float RiverSoundDistance(Vector3 world) => WaterKindDistance(world, true);
        public float LakeSoundDistance(Vector3 world) => WaterKindDistance(world, false);
        public float OceanSoundDistance(Vector3 world) => ocean
            ? -94f + Mathf.Sin((world.x - Center.x) * 0.045f) * 4f - (world.z - Center.z) : -10000f;

        private float WaterKindDistance(Vector3 world, bool river)
        {
            Vector2 p = new Vector2(world.x - Center.x, world.z - Center.z);
            float distance = -10000f;
            foreach (Region water in waters)
                if (water.corridor == river) distance = Mathf.Max(distance, water.Distance(p) + Roughness(p) * 0.4f);
            return distance;
        }
        private float WaterDistance(Vector2 p)
        {
            float d = ocean ? -94f + Mathf.Sin(p.x * 0.045f) * 4f - p.y : -10000f;
            foreach (Region water in waters) d = Mathf.Max(d, water.Distance(p) + Roughness(p) * 0.4f);
            return d;
        }

        /// <summary>XY is downstream direction, Z is distance along the river, W is current strength.
        /// Rivers flow from their first authored knot to their last; lakes have no current.</summary>
        public Vector4 WaterMotion(Vector3 local)
        {
            Vector2 p = new Vector2(local.x, local.z);
            Vector2 direction = Vector2.zero;
            float weightedDistance = 0, weights = 0, along = 0, riverDepth = -10000, lakeDepth = -10000;
            foreach (Region water in waters)
            {
                if (!water.corridor) { lakeDepth = Mathf.Max(lakeDepth, water.Distance(p)); continue; }
                Vector2 segment = water.b - water.a;
                float length = segment.magnitude;
                float t = Mathf.Clamp01(Vector2.Dot(p - water.a, segment) / Mathf.Max(0.001f, segment.sqrMagnitude));
                float distance = Vector2.Distance(p, water.a + segment * t);
                riverDepth = Mathf.Max(riverDepth, water.radii.x - distance);
                float weight = 1f / Mathf.Pow(0.5f + distance, 4);
                direction += segment.normalized * weight;
                weightedDistance += (along + t * length) * weight;
                weights += weight; along += length;
            }
            float strength = Mathf.Clamp01((riverDepth - lakeDepth + 1) * 0.5f);
            if (weights <= 0 || riverDepth < -1) return Vector4.zero;
            direction.Normalize();
            return new Vector4(direction.x, direction.y, weightedDistance / weights, strength);
        }

        public AreaShape Bridge(float x, float z, float length, float width, float yaw = 0f)
        {
            Bridges.Add((new Vector2(x, z), new Vector2(length, width), yaw));
            return this;
        }
        private float BridgeDistance(Vector2 p)
        {
            float d = -10000f;
            foreach (var bridge in Bridges)
            {
                Vector2 offset = p - bridge.center;
                float angle = bridge.yaw * Mathf.Deg2Rad, cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                Vector2 delta = new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
                d = Mathf.Max(d, Mathf.Min(bridge.size.x * 0.5f - Mathf.Abs(delta.x), bridge.size.y * 0.5f - Mathf.Abs(delta.y)));
            }
            return d;
        }
        public bool IsBridge(Vector3 world, float margin = 0f) =>
            BridgeDistance(new Vector2(world.x - Center.x, world.z - Center.z)) >= -margin;

        private float Distance(Vector2 p)
        {
            if (gapSamples == null) FillSmallGaps();
            float d = OpenDistance(p);
            Vector2 grid = p + Size * 0.5f;
            int x = Mathf.FloorToInt(grid.x), z = Mathf.FloorToInt(grid.y);
            if (x >= 0 && z >= 0 && x + 1 < gapSamples.GetLength(0) && z + 1 < gapSamples.GetLength(1) &&
                (filledGaps[x, z] || filledGaps[x + 1, z] || filledGaps[x, z + 1] || filledGaps[x + 1, z + 1]))
            {
                float corrected = Mathf.Lerp(Mathf.Lerp(gapSamples[x, z], gapSamples[x + 1, z], grid.x - x),
                    Mathf.Lerp(gapSamples[x, z + 1], gapSamples[x + 1, z + 1], grid.x - x), grid.y - z);
                d = Mathf.Max(d, corrected);
            }
            // Authored exclusions remain blocked even when a room/corridor gap is filled.
            foreach (Region cut in cuts) d = Mathf.Min(d, -cut.Distance(p) + Roughness(p));
            return Mathf.Min(d, Mathf.Max(-WaterDistance(p), BridgeDistance(p)));
        }

        private float Roughness(Vector2 p) => Mathf.Sin(p.x * 0.23f + p.y * 0.11f) *
            Mathf.Sin(p.y * 0.19f - p.x * 0.08f) * (IsCave ? 0.6f : 0.9f);

        private float OpenDistance(Vector2 p)
        {
            float bounds = Mathf.Min(Size.x * 0.5f - Mathf.Abs(p.x), Size.y * 0.5f - Mathf.Abs(p.y));
            float open = -10000f;
            foreach (Region room in rooms) open = Mathf.Max(open, room.Distance(p));
            return Mathf.Min(bounds, Mathf.Min(bounds, open) + Roughness(p));
        }

        private void FillSmallGaps()
        {
            // One-metre samples identify tiny closed pockets at overlapping room/route joins.
            // Keep large islands and exterior space; fill only enclosed gaps up to 64 m².
            int nx = Mathf.CeilToInt(Size.x) + 1, nz = Mathf.CeilToInt(Size.y) + 1;
            gapSamples = new float[nx, nz];
            filledGaps = new bool[nx, nz];
            var visited = new bool[nx, nz];
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                    gapSamples[x, z] = OpenDistance(new Vector2(x, z) - Size * 0.5f);
            var pocket = new List<Vector2Int>();
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    if (visited[x, z] || gapSamples[x, z] >= 0f) continue;
                    pocket.Clear();
                    pocket.Add(new Vector2Int(x, z));
                    visited[x, z] = true;
                    bool exterior = false;
                    void Visit(int px, int pz)
                    {
                        if (px < 0 || pz < 0 || px >= nx || pz >= nz) { exterior = true; return; }
                        if (visited[px, pz] || gapSamples[px, pz] >= 0f) return;
                        visited[px, pz] = true;
                        pocket.Add(new Vector2Int(px, pz));
                    }
                    for (int i = 0; i < pocket.Count; i++)
                    {
                        Vector2Int point = pocket[i];
                        Visit(point.x - 1, point.y); Visit(point.x + 1, point.y);
                        Visit(point.x, point.y - 1); Visit(point.x, point.y + 1);
                    }
                    if (exterior || pocket.Count > 64) continue;
                    foreach (Vector2Int point in pocket)
                    {
                        filledGaps[point.x, point.y] = true;
                        gapSamples[point.x, point.y] = 1f;
                    }
                }
        }

        public bool Contains(Vector3 p, float margin = 0f)
        {
            Vector2 local = new Vector2(p.x - Center.x, p.z - Center.z);
            if (Distance(local) < Mathf.Min(margin, 0f)) return false;
            if (margin <= 0f) return true;
            foreach (Region cut in cuts)
                if (cut.Distance(local) > -margin) return false;
            // Clearance round a union, including overlapping room entrances.
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                if (Distance(local + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * margin) < 0.2f) return false;
            }
            return true;
        }

        public Vector3 NearestOpen(Vector3 desired, float margin)
        {
            desired.y = 0f;
            if (Contains(desired, margin)) return desired;
            Vector3 best = Center;
            float score = float.PositiveInfinity;
            for (float z = -Size.y * 0.5f; z <= Size.y * 0.5f; z += 4f)
                for (float x = -Size.x * 0.5f; x <= Size.x * 0.5f; x += 4f)
                {
                    Vector3 p = Center + new Vector3(x, 0f, z);
                    float distance = (p - desired).sqrMagnitude;
                    if (distance < score && Contains(p, margin)) { best = p; score = distance; }
                }
            if (float.IsPositiveInfinity(score)) throw new System.InvalidOperationException("No room with the requested clearance.");
            return best;
        }

        public static float AngleOf(Vector3 offset) => Mathf.Atan2(offset.z, offset.x);
        public static Vector3 Direction(float angle) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        public float RadiusAt(float angle)
        {
            Vector3 direction = Direction(angle);
            for (float r = MaxRadius; r > 0; r -= 1f)
                if (Contains(Center + direction * r)) return r;
            return 0f;
        }
        public Vector3 EdgePoint(Vector3 direction, float inset)
        {
            direction.y = 0; direction.Normalize();
            return NearestOpen(Center + direction * (RadiusAt(AngleOf(direction)) - inset), inset);
        }

        /// <summary>Clip a triangulation against the authored outline, including internal holes.</summary>
        public Mesh BuildGroundMesh(float extra, int segments = 96, bool omitBridges = false)
        {
            if (omitBridges && landMesh != null) return landMesh;
            if (!omitBridges && groundMesh != null) return groundMesh;
            if (omitBridges) landBoundary.Clear(); else Boundary.Clear();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            int nx = Mathf.CeilToInt(Size.x / Cell), nz = Mathf.CeilToInt(Size.y / Cell);
            var samples = new float[nx + 1, nz + 1];
            if (omitBridges) waterSamples = new float[nx + 1, nz + 1];
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    Vector2 point = new Vector2(x * Cell - Size.x * 0.5f, z * Cell - Size.y * 0.5f);
                    float water = WaterDistance(point);
                    samples[x, z] = omitBridges ? Mathf.Min(Distance(point), -water) : Distance(point);
                    if (omitBridges) waterSamples[x, z] = water;
                }
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    Vector3 a = new Vector3(x * Cell - Size.x * 0.5f, 0, z * Cell - Size.y * 0.5f);
                    Vector3 b = a + Vector3.forward * Cell, c = b + Vector3.right * Cell, d = a + Vector3.right * Cell;
                    Clip(a, b, c, samples[x, z], samples[x, z + 1], samples[x + 1, z + 1], vertices, triangles, !omitBridges, omitBridges ? landBoundary : null);
                    Clip(a, c, d, samples[x, z], samples[x + 1, z + 1], samples[x + 1, z], vertices, triangles, !omitBridges, omitBridges ? landBoundary : null);
                }
            if (omitBridges) landSamples = samples;
            Mesh mesh = MeshFrom(omitBridges ? "AuthoredLand" : "AuthoredAreaGround", vertices, triangles);
            if (omitBridges) landMesh = mesh; else groundMesh = mesh;
            return mesh;
        }

        private void Clip(Vector3 a, Vector3 b, Vector3 c, float da, float db, float dc, List<Vector3> vertices, List<int> triangles, bool recordBoundary = true, List<(Vector3 a, Vector3 b)> contour = null)
        {
            if (da < 0 && db < 0 && dc < 0) return;
            if (da >= 0 && db >= 0 && dc >= 0)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                return;
            }
            Vector3[] p = { a, b, c };
            float[] distances = { da, db, dc };
            var polygon = new List<Vector3>(4);
            var crossings = new List<Vector3>(2);
            for (int i = 0; i < 3; i++)
            {
                int next = (i + 1) % 3;
                if (distances[i] >= 0) polygon.Add(p[i]);
                if ((distances[i] >= 0) != (distances[next] >= 0))
                {
                    Vector3 at = Vector3.Lerp(p[i], p[next], distances[i] / (distances[i] - distances[next]));
                    polygon.Add(at); crossings.Add(at);
                }
            }
            if ((recordBoundary || contour != null) && crossings.Count == 2 && (crossings[0] - crossings[1]).sqrMagnitude > 0.00001f)
                (contour ?? Boundary).Add((crossings[0], crossings[1]));
            int first = vertices.Count;
            vertices.AddRange(polygon);
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                if (Vector3.Cross(polygon[i] - polygon[0], polygon[i + 1] - polygon[0]).sqrMagnitude < 0.00000001f) continue;
                triangles.Add(first); triangles.Add(first + i); triangles.Add(first + i + 1);
            }
        }

        // Interpolate the same triangles that render the floor, including its water cutouts.
        // Separate, finer sampling of the analytic outline leaves slits beside the ground mesh.
        private float LandDistance(Vector2 p) => SampleSurface(p, false);
        private float ShorelineDistance(Vector2 p) => SampleSurface(p, true);
        private float SampleSurface(Vector2 p, bool water)
        {
            if (landSamples == null) BuildGroundMesh(0, omitBridges: true);
            Vector2 grid = (p + Size * 0.5f) / Cell;
            int x = Mathf.FloorToInt(grid.x), z = Mathf.FloorToInt(grid.y);
            if (x < 0 || z < 0 || x + 1 >= landSamples.GetLength(0) || z + 1 >= landSamples.GetLength(1))
                return water ? WaterDistance(p) : Mathf.Min(Distance(p), -WaterDistance(p));
            float u = grid.x - x, v = grid.y - z;
            float[,] field = water ? waterSamples : landSamples;
            float a = field[x, z], b = field[x, z + 1];
            float c = field[x + 1, z + 1], d = field[x + 1, z];
            return v >= u ? a * (1f - v) + b * (v - u) + c * u :
                a * (1f - u) + c * v + d * (u - v);
        }

        private float RimHeight(Vector2 p, float top)
        {
            if (!HasWater) return top;
            float t = Mathf.Clamp01(-ShorelineDistance(p) / 4f);
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(Mathf.Min(0.025f, top), top, t);
        }

        public Mesh BuildWalls(float bottom, float top, float outward = 0f)
        {
            BuildGroundMesh(0f);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var normals = new List<Vector3>();
            var joins = new Dictionary<Vector2Int, int>();
            bool collision = outward <= 0f;
            // Share the vertical face at contour joins. The top is triangulated separately:
            // extending these normals into a wide strip crosses faces at concave bends.
            float Surface(Vector2 p) => collision ? Distance(p) : LandDistance(p);
            int Join(Vector3 point)
            {
                var key = new Vector2Int(Mathf.RoundToInt(point.x * 10000f), Mathf.RoundToInt(point.z * 10000f));
                if (joins.TryGetValue(key, out int existing)) return existing;
                Vector2 p = new Vector2(key.x, key.y) / 10000f;
                const float delta = 0.1f;
                Vector3 normal = -new Vector3(
                    Surface(p + Vector2.right * delta) - Surface(p - Vector2.right * delta), 0f,
                    Surface(p + Vector2.up * delta) - Surface(p - Vector2.up * delta)).normalized;
                Vector3 inner = new Vector3(p.x, 0f, p.y);
                int start = vertices.Count;
                vertices.Add(inner + Vector3.up * bottom);
                vertices.Add(inner + Vector3.up * (collision ? top : RimHeight(p, top)));
                normals.Add(-normal); normals.Add(-normal);
                joins.Add(key, start);
                return start;
            }
            void Face(int a, int b, int c, Vector3 facing)
            {
                if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), facing) < 0f)
                { int swap = b; b = c; c = swap; }
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                // Only the invisible collision wall needs both windings. Duplicate
                // coplanar render triangles cause flicker and cancel recalculated normals.
                if (collision) { triangles.Add(a); triangles.Add(c); triangles.Add(b); }
            }
            foreach (var edge in collision ? Boundary : WallContour(outward))
            {
                Vector3 start = edge.a, end = edge.b;
                if (!collision)
                {
                    // Clip at the shore instead of deleting entire wall segments near a river.
                    float da = -ShorelineDistance(new Vector2(start.x, start.z)) - 0.06f;
                    float db = -ShorelineDistance(new Vector2(end.x, end.z)) - 0.06f;
                    if (da <= 0f && db <= 0f) continue;
                    if ((da > 0f) != (db > 0f))
                    {
                        Vector3 crossing = Vector3.Lerp(start, end, da / (da - db));
                        if (da <= 0f) start = crossing; else end = crossing;
                    }
                }
                int a = Join(start), b = Join(end);
                Vector3 facing = normals[a] + normals[b];
                Face(a, a + 1, b, facing);
                Face(b, a + 1, b + 1, facing);
            }
            if (!collision) BuildWallTop(bottom, top, outward, vertices, triangles, normals);
            Mesh mesh = MeshFrom("AuthoredAreaWalls", vertices, triangles);
            // Both triangle windings share collision vertices; explicit normals avoid cancellation.
            mesh.SetNormals(normals);
            return mesh;
        }

        private void BuildWallTop(float bottom, float top, float outward, List<Vector3> vertices, List<int> triangles, List<Vector3> normals)
        {
            const float width = 5f, step = 1f;
            float padding = outward + width + step;
            Vector2 start = -Size * 0.5f - Vector2.one * padding;
            int nx = Mathf.CeilToInt((Size.x + padding * 2f) / step);
            int nz = Mathf.CeilToInt((Size.y + padding * 2f) / step);
            var land = new float[nx + 1, nz + 1];
            var shore = new float[nx + 1, nz + 1];
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    Vector2 p = start + new Vector2(x * step, z * step);
                    land[x, z] = LandDistance(p);
                    shore[x, z] = -ShorelineDistance(p) - 0.06f;
                }
            Vector3 Point(int x, int z) => new Vector3(start.x + x * step, 0f, start.y + z * step);
            int first = vertices.Count;
            var contour = new List<(Vector3 a, Vector3 b)>();
            // Clip each half-plane separately. Sampling their minimum can miss a
            // narrow band entirely, or move its edge away from the vertical wall.
            void RimTriangle(Vector3 a, Vector3 b, Vector3 c, float la, float lb, float lc,
                float sa, float sb, float sc)
            {
                // Most of the grid is inside the floor or beyond the rim. Reject
                // those triangles before allocating clipping polygons.
                if ((la > -outward && lb > -outward && lc > -outward) ||
                    (la < -outward - width && lb < -outward - width && lc < -outward - width) ||
                    (sa < 0f && sb < 0f && sc < 0f)) return;
                var polygon = new List<(Vector3 point, float land, float shore)>
                { (a, la, sa), (b, lb, sb), (c, lc, sc) };
                for (int plane = 0; plane < 3 && polygon.Count > 0; plane++)
                {
                    float Signed(float distance, float water) => plane == 0 ? -distance - outward :
                        plane == 1 ? distance + outward + width : water;
                    var clipped = new List<(Vector3 point, float land, float shore)>();
                    for (int i = 0; i < polygon.Count; i++)
                    {
                        var p = polygon[i];
                        var q = polygon[(i + 1) % polygon.Count];
                        float dp = Signed(p.land, p.shore), dq = Signed(q.land, q.shore);
                        if (dp >= 0f) clipped.Add(p);
                        if ((dp >= 0f) == (dq >= 0f)) continue;
                        float t = dp / (dp - dq);
                        clipped.Add((Vector3.Lerp(p.point, q.point, t), Mathf.Lerp(p.land, q.land, t),
                            Mathf.Lerp(p.shore, q.shore, t)));
                    }
                    polygon = clipped;
                }
                if (polygon.Count < 3) return;
                int at = vertices.Count;
                foreach (var vertex in polygon)
                {
                    Vector3 point = vertex.point;
                    // Compute height at the final edge, matching the wall face below it.
                    point.y = RimHeight(new Vector2(point.x, point.z), top);
                    vertices.Add(point);
                }
                for (int i = 1; i + 1 < polygon.Count; i++)
                {
                    if (Vector3.Cross(vertices[at + i] - vertices[at], vertices[at + i + 1] - vertices[at]).sqrMagnitude < 0.00000001f) continue;
                    triangles.Add(at); triangles.Add(at + i); triangles.Add(at + i + 1);
                }
                for (int i = 0; i < polygon.Count; i++)
                {
                    int next = (i + 1) % polygon.Count;
                    if (Mathf.Abs(polygon[i].shore) < 0.00001f && Mathf.Abs(polygon[next].shore) < 0.00001f)
                        contour.Add((vertices[at + i], vertices[at + next]));
                }
            }
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    Vector3 a = Point(x, z), b = Point(x, z + 1), c = Point(x + 1, z + 1), d = Point(x + 1, z);
                    RimTriangle(a, b, c, land[x, z], land[x, z + 1], land[x + 1, z + 1],
                        shore[x, z], shore[x, z + 1], shore[x + 1, z + 1]);
                    RimTriangle(a, c, d, land[x, z], land[x + 1, z + 1], land[x + 1, z],
                        shore[x, z], shore[x + 1, z + 1], shore[x + 1, z]);
                }
            for (int i = first; i < vertices.Count; i++)
            {
                Vector2 p = new Vector2(vertices[i].x, vertices[i].z);
                const float delta = 0.1f;
                normals.Add(new Vector3(
                    RimHeight(p - Vector2.right * delta, top) - RimHeight(p + Vector2.right * delta, top), 2f * delta,
                    RimHeight(p - Vector2.up * delta, top) - RimHeight(p + Vector2.up * delta, top)).normalized);
            }
            // Close the cut ends of the broad rim where its top reaches the shore.
            foreach (var edge in contour)
            {
                Vector3 mid = (edge.a + edge.b) * 0.5f;
                Vector2 p = new Vector2(mid.x, mid.z);
                if (Mathf.Abs(ShorelineDistance(p) + 0.06f) > 0.02f || LandDistance(p) > -outward - 0.02f) continue;
                const float delta = 0.1f;
                Vector3 facing = new Vector3(
                    ShorelineDistance(p + Vector2.right * delta) - ShorelineDistance(p - Vector2.right * delta), 0,
                    ShorelineDistance(p + Vector2.up * delta) - ShorelineDistance(p - Vector2.up * delta)).normalized;
                int at = vertices.Count;
                vertices.Add(edge.a); vertices.Add(edge.b);
                vertices.Add(new Vector3(edge.a.x, bottom, edge.a.z));
                vertices.Add(new Vector3(edge.b.x, bottom, edge.b.z));
                for (int i = 0; i < 4; i++) normals.Add(facing);
                bool reverse = Vector3.Dot(Vector3.Cross(vertices[at + 2] - vertices[at], vertices[at + 1] - vertices[at]), facing) < 0;
                triangles.Add(at); triangles.Add(at + (reverse ? 1 : 2)); triangles.Add(at + (reverse ? 2 : 1));
                triangles.Add(at + 1); triangles.Add(at + (reverse ? 3 : 2)); triangles.Add(at + (reverse ? 2 : 3));
            }
        }

        private List<(Vector3 a, Vector3 b)> WallContour(float outward)
        {
            const float step = 1f;
            float padding = outward + 1f;
            Vector2 start = -Size * 0.5f - Vector2.one * padding;
            int nx = Mathf.CeilToInt(Size.x + padding * 2f), nz = Mathf.CeilToInt(Size.y + padding * 2f);
            var samples = new float[nx + 1, nz + 1];
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                    samples[x, z] = LandDistance(start + new Vector2(x, z)) + outward;
            var result = new List<(Vector3, Vector3)>();
            void Triangle(Vector3 a, Vector3 b, Vector3 c, float da, float db, float dc)
            {
                int count = 0;
                Vector3 first = default, second = default;
                void Edge(Vector3 p, Vector3 q, float dp, float dq)
                {
                    if ((dp >= 0) == (dq >= 0)) return;
                    Vector3 crossing = Vector3.Lerp(p, q, dp / (dp - dq));
                    if (count++ == 0) first = crossing; else second = crossing;
                }
                Edge(a, b, da, db); Edge(b, c, db, dc); Edge(c, a, dc, da);
                if (count == 2 && (first - second).sqrMagnitude > 0.00001f) result.Add((first, second));
            }
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    Vector3 a = new Vector3(start.x + x, 0, start.y + z);
                    Vector3 b = a + Vector3.forward * step, c = b + Vector3.right * step, d = a + Vector3.right * step;
                    Triangle(a, b, c, samples[x, z], samples[x, z + 1], samples[x + 1, z + 1]);
                    Triangle(a, c, d, samples[x, z], samples[x + 1, z + 1], samples[x + 1, z]);
                }
            return result;
        }

        // Shore strips are clipped from the rendered land triangles, so their outer edges
        // cannot disagree with the floor or break into undersampled narrow-band fragments.
        public Mesh BuildShoreMesh()
        {
            Mesh land = BuildGroundMesh(0, omitBridges: true);
            Vector3[] points = land.vertices;
            int[] indices = land.triangles;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            // The bank narrows and widens along the water rather than running as an even band.
            float Bank(Vector3 v) => (ocean ? 7f : 2f) * (1f + 0.55f * Mathf.Sin(v.x * 0.21f + Mathf.Sin(v.z * 0.13f) * 2f) *
                Mathf.Sin(v.z * 0.17f - v.x * 0.05f + 1f)) + ShorelineDistance(new Vector2(v.x, v.z));
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = points[indices[i]], b = points[indices[i + 1]], c = points[indices[i + 2]];
                Clip(a, b, c, Bank(a), Bank(b), Bank(c), vertices, triangles, false);
            }
            // Close the exposed earth between the water level and the grassy bank.
            foreach (var edge in landBoundary)
            {
                Vector3 localMidpoint = (edge.a + edge.b) * 0.5f;
                if (Mathf.Abs(ShorelineDistance(new Vector2(localMidpoint.x, localMidpoint.z))) > 0.02f) continue;
                int first = vertices.Count;
                vertices.Add(edge.a); vertices.Add(edge.b);
                vertices.Add(edge.a + Vector3.down * 0.48f); vertices.Add(edge.b + Vector3.down * 0.48f);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
                triangles.Add(first + 1); triangles.Add(first + 2); triangles.Add(first + 3);
                Vector3 facing = Vector3.Cross(Vector3.down, edge.b - edge.a).normalized;
                Vector3 midpoint = (edge.a + edge.b) * 0.5f + Center;
                if (WaterDistance(midpoint + facing * 0.1f) < WaterDistance(midpoint - facing * 0.1f))
                {
                    int t = triangles.Count - 6;
                    for (int k = t; k < t + 6; k += 3)
                    { int swap = triangles[k]; triangles[k] = triangles[k + 2]; triangles[k + 2] = swap; }
                }
            }
            return MeshFrom("ContinuousRiverBank", vertices, triangles);
        }

        public Mesh BuildWaterMesh()
        {
            float padding = 12f;
            Vector2 start = -Size * 0.5f - Vector2.one * padding;
            int nx = Mathf.CeilToInt((Size.x + padding * 2f) / Cell);
            int nz = Mathf.CeilToInt((Size.y + padding * 2f) / Cell);
            var samples = new float[nx + 1, nz + 1];
            var falls = new List<(Vector2 point, Vector2 downstream, float halfWidth)>(RiverSources());
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    Vector2 p = start + new Vector2(x * Cell, z * Cell);
                    // A submerged overlap hides corner seams between separately clipped surfaces.
                    // It stays beneath the opaque land and bank; movement uses the authored water field.
                    float sample = WaterDistance(p) + 0.65f;
                    // Above a waterfall the river runs on its ledge, not on at ground level behind it.
                    foreach (var fall in falls)
                    {
                        Vector2 offset = p - fall.point;
                        float across = Mathf.Abs(offset.x * fall.downstream.y - offset.y * fall.downstream.x);
                        sample = Mathf.Min(sample, Mathf.Max(Vector2.Dot(offset, fall.downstream) + 2.5f, across - fall.halfWidth - 14f));
                    }
                    samples[x, z] = sample;
                }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    Vector3 a = new Vector3(start.x + x * Cell, 0, start.y + z * Cell);
                    Vector3 b = a + Vector3.forward * Cell, c = b + Vector3.right * Cell, d = a + Vector3.right * Cell;
                    Clip(a, b, c, samples[x, z], samples[x, z + 1], samples[x + 1, z + 1], vertices, triangles, false);
                    Clip(a, c, d, samples[x, z], samples[x + 1, z + 1], samples[x + 1, z], vertices, triangles, false);
                }
            return MeshFrom("WaterSurface", vertices, triangles);
        }

        private static Mesh MeshFrom(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}

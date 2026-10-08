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
        private readonly List<Region> cuts = new List<Region>();
        public readonly List<(Vector3 a, Vector3 b)> Boundary = new List<(Vector3, Vector3)>();
        private Mesh groundMesh;

        private struct Region
        {
            public Vector2 a, b, radii;
            public bool corridor;
            public float Distance(Vector2 p)
            {
                if (!corridor)
                    return (1f - new Vector2((p.x - a.x) / radii.x, (p.y - a.y) / radii.y).magnitude) * Mathf.Min(radii.x, radii.y);
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                return radii.x - Vector2.Distance(p, a + ab * t);
            }
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
            rooms.Add(new Region { a = new Vector2(x, z), radii = new Vector2(rx, rz) });
            return this;
        }

        public AreaShape Route(float width, params float[] points)
        {
            for (int i = 0; i + 3 < points.Length; i += 2)
                rooms.Add(new Region { a = new Vector2(points[i], points[i + 1]), b = new Vector2(points[i + 2], points[i + 3]), radii = Vector2.one * (width * 0.5f), corridor = true });
            return this;
        }

        public AreaShape Exclude(float x, float z, float rx, float rz)
        {
            cuts.Add(new Region { a = new Vector2(x, z), radii = new Vector2(rx, rz) });
            return this;
        }

        private float Distance(Vector2 p)
        {
            float d = Mathf.Min(Size.x * 0.5f - Mathf.Abs(p.x), Size.y * 0.5f - Mathf.Abs(p.y));
            float open = -10000f;
            foreach (Region room in rooms) open = Mathf.Max(open, room.Distance(p));
            d = Mathf.Min(d, open);
            foreach (Region cut in cuts) d = Mathf.Min(d, -cut.Distance(p));
            // Fixed wall undulations soften room/corridor joins without ever rerolling the layout.
            float roughness = IsCave ? 0.6f : 0.9f;
            d += Mathf.Sin(p.x * 0.23f + p.y * 0.11f) * Mathf.Sin(p.y * 0.19f - p.x * 0.08f) * roughness;
            d = Mathf.Min(d, Mathf.Min(Size.x * 0.5f - Mathf.Abs(p.x), Size.y * 0.5f - Mathf.Abs(p.y)));
            return d;
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
        public Mesh BuildGroundMesh(float extra, int segments = 96)
        {
            if (groundMesh != null) return groundMesh;
            Boundary.Clear();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            int nx = Mathf.CeilToInt(Size.x / Cell), nz = Mathf.CeilToInt(Size.y / Cell);
            var samples = new float[nx + 1, nz + 1];
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                    samples[x, z] = Distance(new Vector2(x * Cell - Size.x * 0.5f, z * Cell - Size.y * 0.5f));
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    Vector3 a = new Vector3(x * Cell - Size.x * 0.5f, 0, z * Cell - Size.y * 0.5f);
                    Vector3 b = a + Vector3.forward * Cell, c = b + Vector3.right * Cell, d = a + Vector3.right * Cell;
                    Clip(a, b, c, samples[x, z], samples[x, z + 1], samples[x + 1, z + 1], vertices, triangles);
                    Clip(a, c, d, samples[x, z], samples[x + 1, z + 1], samples[x + 1, z], vertices, triangles);
                }
            groundMesh = MeshFrom("AuthoredAreaGround", vertices, triangles);
            return groundMesh;
        }

        private void Clip(Vector3 a, Vector3 b, Vector3 c, float da, float db, float dc, List<Vector3> vertices, List<int> triangles, bool recordBoundary = true)
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
            if (recordBoundary && crossings.Count == 2 && (crossings[0] - crossings[1]).sqrMagnitude > 0.00001f)
                Boundary.Add((crossings[0], crossings[1]));
            int first = vertices.Count;
            vertices.AddRange(polygon);
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                if (Vector3.Cross(polygon[i] - polygon[0], polygon[i + 1] - polygon[0]).sqrMagnitude < 0.00000001f) continue;
                triangles.Add(first); triangles.Add(first + i); triangles.Add(first + i + 1);
            }
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
            int Join(Vector3 point)
            {
                var key = new Vector2Int(Mathf.RoundToInt(point.x * 10000f), Mathf.RoundToInt(point.z * 10000f));
                if (joins.TryGetValue(key, out int existing)) return existing;
                Vector2 p = new Vector2(key.x, key.y) / 10000f;
                const float delta = 0.1f;
                Vector3 normal = -new Vector3(
                    Distance(p + Vector2.right * delta) - Distance(p - Vector2.right * delta), 0f,
                    Distance(p + Vector2.up * delta) - Distance(p - Vector2.up * delta)).normalized;
                Vector3 inner = new Vector3(p.x, 0f, p.y);
                int start = vertices.Count;
                vertices.Add(inner + Vector3.up * bottom);
                vertices.Add(inner + Vector3.up * top);
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
                int a = Join(edge.a), b = Join(edge.b);
                Vector3 facing = normals[a] + normals[b];
                Face(a, a + 1, b, facing);
                Face(b, a + 1, b + 1, facing);
            }
            if (!collision) BuildWallTop(top, outward, vertices, triangles, normals);
            Mesh mesh = MeshFrom("AuthoredAreaWalls", vertices, triangles);
            // Both triangle windings share collision vertices; explicit normals avoid cancellation.
            mesh.SetNormals(normals);
            return mesh;
        }

        private void BuildWallTop(float top, float outward, List<Vector3> vertices, List<int> triangles, List<Vector3> normals)
        {
            const float width = 5f, step = 1f;
            float padding = outward + width + step;
            Vector2 start = -Size * 0.5f - Vector2.one * padding;
            int nx = Mathf.CeilToInt((Size.x + padding * 2f) / step);
            int nz = Mathf.CeilToInt((Size.y + padding * 2f) / step);
            var samples = new float[nx + 1, nz + 1];
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    float distance = Distance(start + new Vector2(x * step, z * step));
                    samples[x, z] = Mathf.Min(-distance - outward, distance + outward + width);
                }
            // Clip a non-overlapping grid to the solid band outside the playable outline.
            // Holes and merging room edges use the same field as the floor, with no long
            // triangles connecting unrelated contour normals. Preserve the floor Boundary.
            int first = vertices.Count;
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    Vector3 a = new Vector3(start.x + x * step, top, start.y + z * step);
                    Vector3 b = a + Vector3.forward * step, c = b + Vector3.right * step, d = a + Vector3.right * step;
                    Clip(a, b, c, samples[x, z], samples[x, z + 1], samples[x + 1, z + 1], vertices, triangles, false);
                    Clip(a, c, d, samples[x, z], samples[x + 1, z + 1], samples[x + 1, z], vertices, triangles, false);
                }
            for (int i = first; i < vertices.Count; i++) normals.Add(Vector3.up);
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
                    samples[x, z] = Distance(start + new Vector2(x, z)) + outward;
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

        private static Mesh MeshFrom(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}

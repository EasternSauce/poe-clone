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

        private void Clip(Vector3 a, Vector3 b, Vector3 c, float da, float db, float dc, List<Vector3> vertices, List<int> triangles)
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
            if (crossings.Count == 2 && (crossings[0] - crossings[1]).sqrMagnitude > 0.00001f)
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
            foreach (var edge in Boundary)
            {
                Vector3 a = edge.a, b = edge.b;
                Vector3 normal = Vector3.Cross(b - a, Vector3.up).normalized;
                if (Contains(Center + (a + b) * 0.5f + normal * 0.4f)) normal = -normal;
                a += normal * outward; b += normal * outward;
                int i = vertices.Count;
                vertices.Add(a + Vector3.up * bottom); vertices.Add(b + Vector3.up * bottom);
                vertices.Add(a + Vector3.up * top); vertices.Add(b + Vector3.up * top);
                for (int n = 0; n < 4; n++) normals.Add(-normal);
                triangles.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3, i, i + 1, i + 2, i + 1, i + 3, i + 2 });
                if (outward > 0)
                {
                    int roof = vertices.Count;
                    vertices.Add(a + Vector3.up * top);
                    vertices.Add(b + Vector3.up * top);
                    vertices.Add(a + normal * 5f + Vector3.up * top);
                    vertices.Add(b + normal * 5f + Vector3.up * top);
                    for (int n = 0; n < 4; n++) normals.Add(Vector3.up);
                    triangles.AddRange(new[] { roof, roof + 2, roof + 1, roof + 1, roof + 2, roof + 3,
                        roof, roof + 1, roof + 2, roof + 1, roof + 3, roof + 2 });
                }
            }
            Mesh mesh = MeshFrom("AuthoredAreaWalls", vertices, triangles);
            // Both triangle windings share collision vertices; explicit normals avoid cancellation.
            mesh.SetNormals(normals);
            return mesh;
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

using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.Visuals
{
    /// <summary>Shared faceted geometry matching the desktop primitives' scale and origin.</summary>
    public static class MobileCreatureMeshes
    {
        private static Mesh sphere, cylinder, capsule, cone;
        public static Mesh Sphere => sphere != null ? sphere : sphere = MakeSphere();
        public static Mesh Cone => cone != null ? cone : cone = MakeCone();

        public static Mesh ForPrimitive(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.Sphere: return Sphere;
                case PrimitiveType.Cylinder:
                    return cylinder != null ? cylinder : cylinder = Lathe("MobileCylinder", new[] { -1f, 1f }, new[] { 0.5f, 0.5f }, 6, true);
                case PrimitiveType.Capsule:
                    return capsule != null ? capsule : capsule = Lathe("MobileCapsule",
                        new[] { -1f, -0.8535534f, -0.5f, 0.5f, 0.8535534f, 1f },
                        new[] { 0f, 0.3535534f, 0.5f, 0.5f, 0.3535534f, 0f }, 8, false);
                default: return null; // Cubes, quads and planes are already simple.
            }
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 center)
            {
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude < 1e-10f) return;
                if (Vector3.Dot(cross, (a + b + c) / 3f - center) < 0f) { var swap = b; b = c; c = swap; }
                int first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            }
            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }

        private static Mesh MakeSphere()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] points = {
                new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
                new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
                new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1)
            };
            int[] faces = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            // Match the unit sphere's bounding box, so the body and its hitbox still align.
            float largest = 0;
            for (int i = 0; i < points.Length; i++)
            {
                points[i].Normalize();
                largest = Mathf.Max(largest, Mathf.Max(Mathf.Abs(points[i].x), Mathf.Max(Mathf.Abs(points[i].y), Mathf.Abs(points[i].z))));
            }
            for (int i = 0; i < points.Length; i++) points[i] *= 0.5f / largest;
            var builder = new Builder();
            for (int i = 0; i < faces.Length; i += 3)
                builder.Triangle(points[faces[i]], points[faces[i + 1]], points[faces[i + 2]], Vector3.zero);
            return builder.Finish("MobileSphere");
        }

        private static Mesh MakeCone()
        {
            var builder = new Builder();
            Vector3 center = Vector3.up * 0.25f;
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5, b = (i + 1) * Mathf.PI * 2f / 5;
                Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), q = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                builder.Triangle(p, q, Vector3.up, center);
                builder.Triangle(p, q, Vector3.zero, center);
            }
            return builder.Finish("MobileCone");
        }

        private static Mesh Lathe(string name, float[] heights, float[] radii, int sides, bool caps)
        {
            var builder = new Builder();
            Vector3 Point(int ring, int side)
            {
                float angle = side * Mathf.PI * 2f / sides;
                return new Vector3(Mathf.Cos(angle) * radii[ring], heights[ring], Mathf.Sin(angle) * radii[ring]);
            }
            for (int ring = 0; ring + 1 < heights.Length; ring++)
                for (int side = 0; side < sides; side++)
                {
                    Vector3 a = Point(ring, side), b = Point(ring, side + 1), c = Point(ring + 1, side), d = Point(ring + 1, side + 1);
                    builder.Triangle(a, b, d, Vector3.zero); builder.Triangle(a, d, c, Vector3.zero);
                }
            if (caps)
                for (int side = 0; side < sides; side++)
                {
                    builder.Triangle(Point(0, side), Point(0, side + 1), Vector3.up * heights[0], Vector3.zero);
                    int top = heights.Length - 1;
                    builder.Triangle(Point(top, side), Point(top, side + 1), Vector3.up * heights[top], Vector3.zero);
                }
            return builder.Finish(name);
        }
    }
}

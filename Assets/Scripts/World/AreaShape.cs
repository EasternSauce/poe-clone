using UnityEngine;

namespace PoeClone.World
{
    /// <summary>
    /// The outline of an area: a blob round its centre whose radius varies with direction (a few
    /// low waves from a fixed seed, so every client builds the same shape). Walkable ground is
    /// inside <see cref="RadiusAt"/>; rivers, cliffs and forest dress the edge (see WorldBuilder).
    /// </summary>
    public sealed class AreaShape
    {
        /// <summary>No area reaches further than this from its centre (keeps neighbours apart).</summary>
        public const float MaxRadius = 110f;

        public Vector3 Center { get; }

        private readonly float baseRadius;
        private readonly float[] amplitude = new float[6];
        private readonly float[] phase = new float[6];

        public AreaShape(Vector3 center, float baseRadius, int seed)
        {
            Center = new Vector3(center.x, 0f, center.z);
            this.baseRadius = baseRadius;

            var rng = new System.Random(seed);
            // Waves 1 (lopsided), 2 (oval), 3 and 5 (lobes and bays).
            int[] waves = { 1, 2, 3, 5 };
            float[] min = { 0.08f, 0.10f, 0.06f, 0.03f };
            float[] max = { 0.12f, 0.16f, 0.10f, 0.05f };
            for (int k = 0; k < waves.Length; k++)
            {
                amplitude[waves[k]] = min[k] + (float)rng.NextDouble() * (max[k] - min[k]);
                phase[waves[k]] = (float)rng.NextDouble() * Mathf.PI * 2f;
            }
        }

        /// <summary>Distance from the centre to the edge in a direction (radians, from +X towards +Z).</summary>
        public float RadiusAt(float angle)
        {
            float r = 1f;
            for (int k = 1; k < amplitude.Length; k++)
            {
                if (amplitude[k] > 0f)
                    r += amplitude[k] * Mathf.Cos(k * angle + phase[k]);
            }
            return Mathf.Min(baseRadius * r, MaxRadius);
        }

        public static float AngleOf(Vector3 offset)
        {
            return Mathf.Atan2(offset.z, offset.x);
        }

        public static Vector3 Direction(float angle)
        {
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        /// <summary>Whether a point is at least <paramref name="margin"/> inside the edge.</summary>
        public bool Contains(Vector3 p, float margin = 0f)
        {
            Vector3 offset = p - Center;
            offset.y = 0f;
            return offset.magnitude <= RadiusAt(AngleOf(offset)) - margin;
        }

        /// <summary>The point <paramref name="inset"/> metres inside the edge in a direction from the centre.</summary>
        public Vector3 EdgePoint(Vector3 direction, float inset)
        {
            direction.y = 0f;
            float angle = AngleOf(direction);
            return Center + Direction(angle) * (RadiusAt(angle) - inset);
        }

        /// <summary>A flat disc mesh following the outline, <paramref name="extra"/> metres beyond it (centred on the origin).</summary>
        public Mesh BuildGroundMesh(float extra, int segments = 96)
        {
            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = Direction(angle) * (RadiusAt(angle) + extra);
            }
            for (int i = 0; i <= segments; i++)
            {
                normals[i] = Vector3.up;
                uvs[i] = new Vector2(vertices[i].x, vertices[i].z) * 0.1f;
            }
            // Counter-clockwise from above is clockwise from below: (centre, next, this) faces up.
            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = "AreaGround", vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.World
{
    public sealed partial class AreaShape
    {
        /// <summary>Only contour metadata is global; no meshes are required to prepare a layout.</summary>
        public IEnumerator PrepareLayout() => PrepareSmallGaps();

        public Mesh BuildChunkGround(Rect region, bool omitBridges, List<(Vector3 a, Vector3 b)> boundary)
        {
            Vector2 start = -Size * 0.5f;
            int nx = Mathf.CeilToInt(Size.x / Cell), nz = Mathf.CeilToInt(Size.y / Cell);
            LimitGrid(ref start, ref nx, ref nz, Cell, region);
            var samples = new float[nx + 1, nz + 1];
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    Vector2 p = start + new Vector2(x * Cell, z * Cell);
                    samples[x, z] = omitBridges ? Mathf.Min(Distance(p), -WaterDistance(p)) : Distance(p);
                }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    Vector3 a = new Vector3(start.x + x * Cell, 0, start.y + z * Cell);
                    Vector3 b = a + Vector3.forward * Cell, c = b + Vector3.right * Cell, d = a + Vector3.right * Cell;
                    // Only the authored contour produces walls, never a chunk's rectangular edges.
                    Clip(a, b, c, samples[x, z], samples[x, z + 1], samples[x + 1, z + 1], vertices, triangles, false, boundary);
                    Clip(a, c, d, samples[x, z], samples[x + 1, z + 1], samples[x + 1, z], vertices, triangles, false, boundary);
                }
            return TrimToRegion(MeshFrom("ChunkGround", vertices, triangles), region);
        }

        private static void LimitGrid(ref Vector2 start, ref int nx, ref int nz, float step, Rect? region)
        {
            if (!region.HasValue) return;
            Rect rect = region.Value;
            int x = Mathf.Clamp(Mathf.FloorToInt((rect.xMin - start.x) / step), 0, nx);
            int z = Mathf.Clamp(Mathf.FloorToInt((rect.yMin - start.y) / step), 0, nz);
            nx = Mathf.Max(0, Mathf.Min(nx, Mathf.CeilToInt((rect.xMax - start.x) / step)) - x);
            nz = Mathf.Max(0, Mathf.Min(nz, Mathf.CeilToInt((rect.yMax - start.y) / step)) - z);
            start += new Vector2(x * step, z * step);
        }

        // Wall grids include fractional outward offsets. Clip their final triangles so adjacent
        // chunks meet exactly without overlapping strips, cracks, or artificial collision walls.
        private static Mesh TrimToRegion(Mesh mesh, Rect? region)
        {
            if (!region.HasValue) return mesh;
            Rect rect = region.Value;
            Vector3[] source = mesh.vertices, sourceNormals = mesh.normals;
            int[] indices = mesh.triangles;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            var polygon = new List<(Vector3 p, Vector3 n)>(7);
            var clipped = new List<(Vector3 p, Vector3 n)>(7);
            for (int t = 0; t < indices.Length; t += 3)
            {
                polygon.Clear();
                for (int k = 0; k < 3; k++) polygon.Add((source[indices[t + k]], sourceNormals[indices[t + k]]));
                for (int plane = 0; plane < 4 && polygon.Count > 0; plane++)
                {
                    float Signed(Vector3 p) => plane == 0 ? p.x - rect.xMin : plane == 1 ? rect.xMax - p.x :
                        plane == 2 ? p.z - rect.yMin : rect.yMax - p.z;
                    clipped.Clear();
                    for (int i = 0; i < polygon.Count; i++)
                    {
                        var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                        float da = Signed(a.p), db = Signed(b.p);
                        if (da >= 0) clipped.Add(a);
                        if ((da >= 0) != (db >= 0))
                        {
                            float u = da / (da - db);
                            clipped.Add((Vector3.Lerp(a.p, b.p, u), Vector3.Lerp(a.n, b.n, u).normalized));
                        }
                    }
                    var swap = polygon; polygon = clipped; clipped = swap;
                }
                int first = vertices.Count;
                foreach (var v in polygon) { vertices.Add(v.p); normals.Add(v.n); }
                for (int i = 1; i + 1 < polygon.Count; i++)
                {
                    if (Vector3.Cross(polygon[i].p - polygon[0].p, polygon[i + 1].p - polygon[0].p).sqrMagnitude < 1e-10f) continue;
                    triangles.Add(first); triangles.Add(first + i); triangles.Add(first + i + 1);
                }
            }
            mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetNormals(normals);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.EditorTools
{
    /// <summary>
    /// Quadric-error edge collapse for baked anatomy. Each collapse moves a vertex onto one of its
    /// neighbours, so every surviving vertex keeps its exact position, normal, UV and pigment.
    /// Open edges (the wrist opening), pigment borders (nails, creases) and sharp normal changes
    /// are never collapsed, and only collapses that keep the surface within maxError are taken.
    /// </summary>
    internal static class AnatomySimplifier
    {
        private struct Quadric
        {
            private double a2, ab, ac, ad, b2, bc, bd, c2, cd, d2;

            public static Quadric Plane(Vector3 n, float d) => new Quadric
            {
                a2 = n.x*n.x, ab = n.x*n.y, ac = n.x*n.z, ad = n.x*d,
                b2 = n.y*n.y, bc = n.y*n.z, bd = n.y*d, c2 = n.z*n.z, cd = n.z*d, d2 = d*d,
            };

            public static Quadric operator +(Quadric q, Quadric r) => new Quadric
            {
                a2 = q.a2+r.a2, ab = q.ab+r.ab, ac = q.ac+r.ac, ad = q.ad+r.ad, b2 = q.b2+r.b2,
                bc = q.bc+r.bc, bd = q.bd+r.bd, c2 = q.c2+r.c2, cd = q.cd+r.cd, d2 = q.d2+r.d2,
            };

            // Sum of squared distances from p to every accumulated face plane.
            public double Error(Vector3 p) =>
                a2*p.x*p.x + 2*ab*p.x*p.y + 2*ac*p.x*p.z + 2*ad*p.x
                + b2*p.y*p.y + 2*bc*p.y*p.z + 2*bd*p.y + c2*p.z*p.z + 2*cd*p.z + d2;
        }

        private struct Candidate
        {
            public double Cost;
            public int From, To, Version;
        }

        public static Mesh Simplify(Mesh source, float maxError)
        {
            Vector3[] positions = source.vertices, normals = source.normals;
            Vector2[] uv = source.uv;
            Color[] colors = source.colors;
            int vertexCount = positions.Length;
            var triangles = new List<int>();
            var submeshOf = new List<int>();
            for (int s = 0; s < source.subMeshCount; s++)
                foreach (int index in source.GetTriangles(s))
                {
                    if (triangles.Count % 3 == 0) submeshOf.Add(s);
                    triangles.Add(index);
                }
            int triangleCount = submeshOf.Count;

            var around = new List<int>[vertexCount];
            for (int v = 0; v < vertexCount; v++) around[v] = new List<int>(8);
            var edges = new Dictionary<long, int>();
            var quadrics = new Quadric[vertexCount];
            for (int t = 0; t < triangleCount; t++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = triangles[t*3+k], b = triangles[t*3+(k+1)%3];
                    around[a].Add(t);
                    long key = a < b ? (long)a*vertexCount+b : (long)b*vertexCount+a;
                    edges.TryGetValue(key, out int count);
                    edges[key] = count+1;
                }
                Vector3 normal = Normal(positions, triangles, t);
                if (normal == Vector3.zero) continue;
                var plane = Quadric.Plane(normal, -Vector3.Dot(normal, positions[triangles[t*3]]));
                for (int k = 0; k < 3; k++) quadrics[triangles[t*3+k]] += plane;
            }
            // Open or non-manifold edges keep both ends: the wrist loop must still meet the forearm.
            var locked = new bool[vertexCount];
            foreach (var edge in edges)
                if (edge.Value != 2)
                {
                    locked[edge.Key / vertexCount] = true;
                    locked[edge.Key % vertexCount] = true;
                }

            var removedTriangle = new bool[triangleCount];
            var removedVertex = new bool[vertexCount];
            var version = new int[vertexCount];
            var heap = new List<Candidate>();
            double limit = (double)maxError*maxError;
            var ring = new HashSet<int>();
            var otherRing = new HashSet<int>();

            void Neighbours(int v, HashSet<int> into)
            {
                into.Clear();
                foreach (int t in around[v])
                    for (int k = 0; k < 3; k++)
                        if (triangles[t*3+k] != v) into.Add(triangles[t*3+k]);
            }

            bool Allowed(int from, int to)
            {
                if (colors.Length > 0)
                {
                    Color a = colors[from], b = colors[to];
                    if (Mathf.Abs(a.r-b.r) + Mathf.Abs(a.g-b.g) + Mathf.Abs(a.b-b.b) + Mathf.Abs(a.a-b.a) > .01f) return false;
                }
                if (Vector3.Dot(normals[from], normals[to]) < .9f) return false;
                // Link condition: the two rings may only share the vertices opposite the edge.
                int opposite = 0;
                foreach (int t in around[from])
                    if (triangles[t*3] == to || triangles[t*3+1] == to || triangles[t*3+2] == to) opposite++;
                Neighbours(from, ring);
                Neighbours(to, otherRing);
                ring.IntersectWith(otherRing);
                if (ring.Count != opposite) return false;
                foreach (int t in around[from])
                {
                    int i0 = triangles[t*3], i1 = triangles[t*3+1], i2 = triangles[t*3+2];
                    if (i0 == to || i1 == to || i2 == to) continue;
                    Vector3 before = Vector3.Cross(positions[i1]-positions[i0], positions[i2]-positions[i0]);
                    Vector3 p0 = positions[i0 == from ? to : i0], p1 = positions[i1 == from ? to : i1], p2 = positions[i2 == from ? to : i2];
                    Vector3 after = Vector3.Cross(p1-p0, p2-p0);
                    if (after.sqrMagnitude < 1e-14f || Vector3.Dot(before.normalized, after.normalized) < .5f) return false;
                }
                return true;
            }

            void Consider(int from)
            {
                if (locked[from] || removedVertex[from]) return;
                var targets = new HashSet<int>();
                Neighbours(from, targets);
                Candidate best = new Candidate { Cost = double.MaxValue };
                foreach (int to in targets)
                {
                    double cost = (quadrics[from]+quadrics[to]).Error(positions[to]);
                    if (cost < best.Cost && cost <= limit && Allowed(from, to))
                        best = new Candidate { Cost = cost, From = from, To = to, Version = version[from] };
                }
                if (best.Cost <= limit) Push(heap, best);
            }

            for (int v = 0; v < vertexCount; v++) Consider(v);
            var touched = new HashSet<int>();
            while (heap.Count > 0)
            {
                Candidate c = Pop(heap);
                if (removedVertex[c.From] || removedVertex[c.To] || c.Version != version[c.From]) continue;
                // The target's quadric or ring may have changed since this was queued.
                if ((quadrics[c.From]+quadrics[c.To]).Error(positions[c.To]) > limit || !Allowed(c.From, c.To))
                {
                    version[c.From]++;
                    Consider(c.From);
                    continue;
                }
                foreach (int t in around[c.From])
                {
                    int i0 = triangles[t*3], i1 = triangles[t*3+1], i2 = triangles[t*3+2];
                    if (i0 == c.To || i1 == c.To || i2 == c.To)
                    {
                        removedTriangle[t] = true;
                        foreach (int k in new[] { i0, i1, i2 })
                            if (k != c.From) around[k].Remove(t);
                    }
                    else
                    {
                        for (int k = 0; k < 3; k++)
                            if (triangles[t*3+k] == c.From) triangles[t*3+k] = c.To;
                        around[c.To].Add(t);
                    }
                }
                around[c.From].Clear();
                removedVertex[c.From] = true;
                quadrics[c.To] += quadrics[c.From];
                Neighbours(c.To, touched);
                touched.Add(c.To);
                foreach (int v in touched)
                {
                    version[v]++;
                    Consider(v);
                }
            }

            var remap = new int[vertexCount];
            for (int v = 0; v < vertexCount; v++) remap[v] = -1;
            var keptPositions = new List<Vector3>();
            var keptNormals = new List<Vector3>();
            var keptUv = new List<Vector2>();
            var keptColors = new List<Color>();
            var keptTriangles = new List<int>[source.subMeshCount];
            for (int s = 0; s < keptTriangles.Length; s++) keptTriangles[s] = new List<int>();
            for (int t = 0; t < triangleCount; t++)
            {
                if (removedTriangle[t]) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = triangles[t*3+k];
                    if (remap[v] < 0)
                    {
                        remap[v] = keptPositions.Count;
                        keptPositions.Add(positions[v]);
                        keptNormals.Add(normals[v]);
                        if (uv.Length > 0) keptUv.Add(uv[v]);
                        if (colors.Length > 0) keptColors.Add(colors[v]);
                    }
                    keptTriangles[submeshOf[t]].Add(remap[v]);
                }
            }

            var mesh = new Mesh { name = source.name, indexFormat = keptPositions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(keptPositions);
            mesh.SetNormals(keptNormals);
            if (keptUv.Count > 0) mesh.SetUVs(0, keptUv);
            if (keptColors.Count > 0) mesh.SetColors(keptColors);
            mesh.subMeshCount = keptTriangles.Length;
            for (int s = 0; s < keptTriangles.Length; s++) mesh.SetTriangles(keptTriangles[s], s);
            if (source.tangents.Length > 0) mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Normal(Vector3[] positions, List<int> triangles, int t)
        {
            Vector3 a = positions[triangles[t*3]];
            Vector3 n = Vector3.Cross(positions[triangles[t*3+1]]-a, positions[triangles[t*3+2]]-a);
            return n.sqrMagnitude < 1e-20f ? Vector3.zero : n.normalized;
        }

        private static void Push(List<Candidate> heap, Candidate c)
        {
            heap.Add(c);
            for (int i = heap.Count-1; i > 0;)
            {
                int parent = (i-1)/2;
                if (heap[parent].Cost <= heap[i].Cost) break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        private static Candidate Pop(List<Candidate> heap)
        {
            Candidate top = heap[0];
            heap[0] = heap[heap.Count-1];
            heap.RemoveAt(heap.Count-1);
            for (int i = 0; ;)
            {
                int left = i*2+1, right = left+1, smallest = i;
                if (left < heap.Count && heap[left].Cost < heap[smallest].Cost) smallest = left;
                if (right < heap.Count && heap[right].Cost < heap[smallest].Cost) smallest = right;
                if (smallest == i) break;
                (heap[smallest], heap[i]) = (heap[i], heap[smallest]);
                i = smallest;
            }
            return top;
        }
    }
}

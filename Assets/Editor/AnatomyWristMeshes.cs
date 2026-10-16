using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.EditorTools
{
    /// <summary>Stitches the open hand wrist onto the forearm, sharing its elbow bone.</summary>
    internal static class AnatomyWristMeshes
    {
        internal static Mesh Join(string name, Mesh arm, Mesh hand)
        {
            const float cut = -.755f;
            var vertices = new List<Vector3>(arm.vertices);
            var normals = new List<Vector3>(arm.normals);
            var uv = new List<Vector2>(arm.uv);
            var colors = new List<Color>(arm.colors);
            var weights = new List<BoneWeight>(arm.boneWeights);
            var sleeve = new List<int>(arm.GetTriangles(0));
            var skin = new List<int>();
            int[] original = arm.GetTriangles(1);
            for (int i = 0; i < original.Length; i += 3)
                if (vertices[original[i]].y >= cut - .00001f && vertices[original[i+1]].y >= cut - .00001f && vertices[original[i+2]].y >= cut - .00001f)
                { skin.Add(original[i]); skin.Add(original[i+1]); skin.Add(original[i+2]); }
            var upperLoop = Boundary(skin, vertices, cut);
            int offset = vertices.Count;
            Vector3 scale = new Vector3(.135f, .16f, .16f);
            Vector3[] hv = hand.vertices, hn = hand.normals;
            Vector2[] hu = hand.uv;
            Color[] hc = hand.colors;
            for (int i = 0; i < hv.Length; i++)
            {
                vertices.Add(Vector3.Scale(hv[i], scale) + Vector3.down * .81f);
                normals.Add(new Vector3(hn[i].x/scale.x, hn[i].y/scale.y, hn[i].z/scale.z).normalized);
                uv.Add(hu[i]); colors.Add(hc[i]);
                weights.Add(new BoneWeight { boneIndex0 = 2, weight0 = 1 });
            }
            int[] ht = hand.triangles;
            var handTriangles = new List<int>(ht.Length);
            foreach (int i in ht) handTriangles.Add(i + offset);
            var lowerLoop = Boundary(handTriangles, vertices, -.76f);
            skin.AddRange(handTriangles);
            if (upperLoop.Count < 3 || lowerLoop.Count < 3)
                throw new System.InvalidOperationException("Missing open wrist loops: " + name);
            foreach (int i in upperLoop) normals[i] = WristNormal(vertices[i]);
            foreach (int i in lowerLoop) normals[i] = WristNormal(vertices[i]);
            // Zipper unequal loops by angular order; every bridge triangle faces outward.
            int a = 0, b = 0;
            while (a < upperLoop.Count || b < lowerLoop.Count)
            {
                float nextA = a + 1 < upperLoop.Count ? Angle(vertices[upperLoop[a+1]]) : Mathf.PI * 2;
                float nextB = b + 1 < lowerLoop.Count ? Angle(vertices[lowerLoop[b+1]]) : Mathf.PI * 2;
                int ia = upperLoop[a % upperLoop.Count], ib = lowerLoop[b % lowerLoop.Count], ic;
                if (a < upperLoop.Count && (b >= lowerLoop.Count || nextA <= nextB))
                    ic = upperLoop[++a % upperLoop.Count];
                else ic = lowerLoop[++b % lowerLoop.Count];
                Vector3 n = Vector3.Cross(vertices[ib]-vertices[ia], vertices[ic]-vertices[ia]);
                if (Vector3.Dot(n, WristNormal((vertices[ia]+vertices[ib]+vertices[ic])/3)) < 0)
                { int swap = ib; ib = ic; ic = swap; }
                skin.Add(ia); skin.Add(ib); skin.Add(ic);
            }
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetColors(colors);
            mesh.boneWeights = weights.ToArray(); mesh.bindposes = arm.bindposes;
            mesh.subMeshCount = 2; mesh.SetTriangles(sleeve, 0); mesh.SetTriangles(skin, 1);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 WristNormal(Vector3 p) => new Vector3(p.x/(.026f*.026f), 0, p.z/(.0265f*.0265f)).normalized;
        private static float Angle(Vector3 p) => Mathf.Repeat(Mathf.Atan2(p.x/.026f, p.z/.0265f), Mathf.PI*2);

        private static List<int> Boundary(List<int> triangles, List<Vector3> vertices, float height)
        {
            var edges = new Dictionary<(int,int),int>();
            for (int i = 0; i < triangles.Count; i += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = triangles[i+k], b = triangles[i+(k+1)%3];
                    var edge = a < b ? (a,b) : (b,a);
                    edges.TryGetValue(edge, out int count); edges[edge] = count + 1;
                }
            var indices = new HashSet<int>();
            foreach (var edge in edges)
                if (edge.Value == 1 && Mathf.Abs(vertices[edge.Key.Item1].y-height) < .00002f && Mathf.Abs(vertices[edge.Key.Item2].y-height) < .00002f)
                { indices.Add(edge.Key.Item1); indices.Add(edge.Key.Item2); }
            var loop = new List<int>(indices);
            loop.Sort((a,b) => Angle(vertices[a]).CompareTo(Angle(vertices[b])));
            for (int i = loop.Count-1; i > 0; i--)
                if ((vertices[loop[i]]-vertices[loop[i-1]]).sqrMagnitude < 1e-10f) loop.RemoveAt(i);
            if (loop.Count > 1 && (vertices[loop[0]]-vertices[loop[loop.Count-1]]).sqrMagnitude < 1e-10f) loop.RemoveAt(loop.Count-1);
            return loop;
        }
    }
}

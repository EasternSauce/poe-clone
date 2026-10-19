using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.EditorTools
{
    public static class NaturalRockBuilder
    {
        [MenuItem("PoeClone/Maintenance/World/Rebuild Natural Rocks")]
        public static void BuildAll()
        {
            var variants = new Mesh[16];
            for (int i = 0; i < variants.Length; i++)
            {
                Mesh mesh = Generate(i);
                string path = $"Assets/Meshes/Stylized/NaturalRock{i}.asset";
                variants[i] = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (variants[i] == null) { AssetDatabase.CreateAsset(mesh, path); variants[i] = mesh; }
                else { EditorUtility.CopySerialized(mesh, variants[i]); Object.DestroyImmediate(mesh); }
            }
            foreach (string name in new[] { "Rock", "RockSmall" })
            {
                string path = $"Assets/Prefabs/Environment/{name}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var rock = root.GetComponent<NaturalRock>() ?? root.AddComponent<NaturalRock>();
                    rock.variants = variants;
                    int index = 0;
                    foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                        filter.sharedMesh = variants[index++];
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        private static Mesh Generate(int variant)
        {
            // A warped, clipped ellipsoid: irregular shoulders, broad faces and a buried base.
            const int sides = 11, rings = 6;
            var random = new System.Random(7919 + variant * 131);
            float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            float phase = Range(0f, 6.28f), skew = Range(-0.2f, 0.2f);
            float crownSlope = Range(-0.28f, 0.28f);
            float flatten = variant % 4 == 0 ? 0.64f : variant % 4 == 1 ? 0.83f : 1f;
            var points = new Vector3[rings * sides];
            for (int r = 0; r < rings; r++)
            {
                float latitude = Mathf.Lerp(-1.18f, 1.24f, r / (float)(rings - 1));
                float y = Mathf.Sin(latitude) * 0.47f;
                for (int s = 0; s < sides; s++)
                {
                    float angle = s * Mathf.PI * 2f / sides + phase;
                    float radius = Mathf.Cos(latitude) * 0.51f *
                        (1f + 0.15f * Mathf.Sin(angle * 3f + phase) + 0.08f * Mathf.Cos(angle * 5f - r * 0.7f));
                    float x = Mathf.Cos(angle) * radius + y * skew;
                    float z = Mathf.Sin(angle) * radius;
                    float top = y + Range(-0.035f, 0.035f);
                    // Slabs, sloping crowns and split shoulders instead of uniformly noisy spheres.
                    top = Mathf.Min(top, 0.34f + x * crownSlope);
                    points[r * sides + s] = new Vector3(x, Mathf.Max(-0.43f, top) * flatten, z);
                }
            }
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var indices = new List<int>();
            void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                foreach (Vector3 v in new[] { a, b, c })
                {
                    indices.Add(vertices.Count); vertices.Add(v);
                    Vector3 rounded = new Vector3(v.x, v.y / (flatten * flatten), v.z).normalized;
                    normals.Add(Vector3.Slerp(normal, rounded, 0.32f).normalized);
                }
            }
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int next = (s + 1) % sides;
                    Vector3 a = points[r * sides + s], b = points[r * sides + next];
                    Vector3 c = points[(r + 1) * sides + s], d = points[(r + 1) * sides + next];
                    Face(a, c, b); Face(b, c, d);
                }
            for (int s = 0; s < sides; s++)
            {
                int next = (s + 1) % sides;
                Face(new Vector3(0f, -0.43f * flatten, 0f), points[s], points[next]);
                Face(new Vector3(skew * 0.3f, (0.34f + skew * 0.3f * crownSlope) * flatten, 0f), points[(rings - 1) * sides + next], points[(rings - 1) * sides + s]);
            }
            var mesh = new Mesh { name = "NaturalRock" + variant };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

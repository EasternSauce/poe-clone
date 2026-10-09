using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.Visuals
{
    /// <summary>Small cloth grid pinned along its top edge, with optional frayed holes.</summary>
    public sealed class WindCloth : MonoBehaviour
    {
        private Mesh mesh;
        private Vector3[] rest, vertices;
        private float height, phase;
        private Renderer clothRenderer;

        public static WindCloth Create(Transform parent, Vector3 top, float width, float height,
            Material material, bool torn = false)
        {
            var go = new GameObject(torn ? "TatteredWindCloth" : "LaundryInWind");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = top;
            var cloth = go.AddComponent<WindCloth>();
            cloth.height = height;
            cloth.phase = top.x * 2.13f + top.z * 1.71f;
            const int nx = 8, ny = 10;
            int faceVertices = (nx + 1) * (ny + 1);
            cloth.rest = new Vector3[faceVertices * 2];
            var uv = new Vector2[cloth.rest.Length];
            var triangles = new System.Collections.Generic.List<int>();
            for (int y = 0; y <= ny; y++)
                for (int x = 0; x <= nx; x++)
                {
                    int i = y * (nx + 1) + x;
                    float rag = torn ? 0.12f * Mathf.Sin(x * 5.7f + cloth.phase) * Mathf.Pow((float)y / ny, 5) : 0;
                    cloth.rest[i] = new Vector3(((float)x / nx - 0.5f) * width, -(float)y / ny * height + rag, 0);
                    uv[i] = new Vector2((float)x / nx, (float)y / ny);
                    // Separate back-face vertices preserve normals under two-sided lighting.
                    cloth.rest[i + faceVertices] = cloth.rest[i];
                    uv[i + faceVertices] = uv[i];
                    if (x == nx || y == ny || (torn && ((y > 6 && x == 6) || (y == 5 && x == 1)))) continue;
                    int a = i, b = i + 1, c = i + nx + 1, d = c + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d,
                        b + faceVertices, c + faceVertices, a + faceVertices,
                        d + faceVertices, c + faceVertices, b + faceVertices });
                }
            cloth.vertices = (Vector3[])cloth.rest.Clone();
            cloth.mesh = new Mesh { name = "PinnedCloth", vertices = cloth.vertices, uv = uv };
            cloth.mesh.SetTriangles(triangles, 0);
            cloth.mesh.RecalculateNormals();
            cloth.mesh.bounds = new Bounds(Vector3.down * height * 0.5f, new Vector3(width + 1, height + 1, height + 1));
            cloth.mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = cloth.mesh;
            cloth.clothRenderer = go.AddComponent<MeshRenderer>();
            cloth.clothRenderer.sharedMaterial = material;
            return cloth;
        }

        private void Update()
        {
            if (clothRenderer != null && !clothRenderer.isVisible) return;
            Vector3 wind = transform.InverseTransformDirection(AmbientWind.At(transform.position));
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = rest[i];
                float loose = Mathf.Clamp01(-p.y / height);
                float ripple = Mathf.Sin(p.x * 5 - Time.time * 4.1f + phase + loose * 4) * 0.08f;
                vertices[i] = p + new Vector3(wind.x * 0.08f, Mathf.Abs(ripple) * 0.25f,
                    wind.z * 0.24f + ripple) * loose * height;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}

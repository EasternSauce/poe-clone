using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.Visuals
{
    /// <summary>Fixed flame envelope with tapered, rising tongues instead of pulsing solid blobs.</summary>
    public sealed class LivingFlame : MonoBehaviour
    {
        private static Mesh flameMesh;
        private static Material flameMaterial;
        private MaterialPropertyBlock properties;
        private Renderer flameRenderer;
        private float windExposure;
        private static readonly int WindId = Shader.PropertyToID("_Wind");

        public static void Attach(GameObject flame, float shelter = 1f)
        {
            if (flame == null || flame.GetComponent<LivingFlame>() != null) return;
            flame.AddComponent<LivingFlame>().Build(shelter);
        }

        private void Build(float shelter)
        {
            windExposure = shelter;
            flameRenderer = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            flameRenderer.GetPropertyBlock(properties);
            Color tint = properties.HasColor("_BaseColor") ? properties.GetColor("_BaseColor")
                : flameRenderer.sharedMaterial.GetColor("_BaseColor");
            // Preserve green spirit fires while ordinary fires use a yellow-white core and orange edge.
            bool spirit = tint.g > tint.r * 1.2f;
            properties.Clear();
            properties.SetColor("_FlameColor", spirit ? tint : new Color(1f, 0.28f, 0.025f));
            properties.SetColor("_CoreColor", spirit ? new Color(0.8f, 1f, 0.72f) : new Color(1f, 0.91f, 0.46f));
            properties.SetFloat("_Seed", transform.position.x * 1.73f + transform.position.z * 2.31f);
            if (flameMaterial == null) flameMaterial = new Material(Shader.Find("PoeClone/LivingFlame"));
            flameRenderer.sharedMaterial = flameMaterial;
            flameRenderer.shadowCastingMode = ShadowCastingMode.Off;
            flameRenderer.receiveShadows = false;
            GetComponent<MeshFilter>().sharedMesh = Mesh();
            flameRenderer.SetPropertyBlock(properties);

            // Enclosed lanterns have no smoke plume escaping through their roof.
            float height = transform.lossyScale.y;
            if (shelter > 0.25f)
                WindborneSmoke.Create(transform, Vector3.up * 0.5f, Mathf.Max(0.04f, height * 0.22f),
                    Mathf.Max(0.25f, height), shelter);
        }

        private static Mesh Mesh()
        {
            if (flameMesh != null) return flameMesh;
            // Three intersecting cards give the flame volume from every camera direction.
            var vertices = new Vector3[12];
            var uv = new Vector2[12];
            var triangles = new int[18];
            for (int i = 0; i < 3; i++)
            {
                Vector3 side = Quaternion.Euler(0, i * 60, 0) * Vector3.right * 0.5f;
                int v = i * 4, t = i * 6;
                vertices[v] = -side - Vector3.up * 0.5f;
                vertices[v + 1] = side - Vector3.up * 0.5f;
                vertices[v + 2] = -side + Vector3.up * 0.5f;
                vertices[v + 3] = side + Vector3.up * 0.5f;
                uv[v] = new Vector2(0, 0); uv[v + 1] = new Vector2(1, 0);
                uv[v + 2] = new Vector2(0, 1); uv[v + 3] = new Vector2(1, 1);
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            flameMesh = new Mesh { name = "CrossedFlameTongues", vertices = vertices, uv = uv, triangles = triangles };
            flameMesh.RecalculateBounds();
            return flameMesh;
        }

        private void Update()
        {
            if (flameRenderer == null || !flameRenderer.isVisible) return;
            Vector3 wind = transform.InverseTransformDirection(AmbientWind.At(transform.position)) * windExposure;
            properties.SetVector(WindId, new Vector4(wind.x, wind.z, 0, 0));
            flameRenderer.SetPropertyBlock(properties);
        }
    }
}
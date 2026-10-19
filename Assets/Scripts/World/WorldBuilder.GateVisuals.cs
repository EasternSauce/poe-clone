using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private readonly List<Object> gateVisualAssets = new List<Object>();

        private static Color GatePortalTint(Color destination)
        {
            Color.RGBToHSV(destination, out float h, out float s, out _);
            return Color.HSVToRGB(h, Mathf.Clamp(s, 0.45f, 0.75f), 0.85f);
        }

        // Shared template geometry and materials: animation runs entirely in the portal shader.
        private static void BuildGateVisual(Transform parent, List<Object> owned)
        {
            Material Alloy(string name, Color color, float metal)
            {
                var m = new Material(Resources.Load<Shader>("Shaders/GateAlloy")) { name = name };
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Metallic", metal);
                owned.Add(m);
                return m;
            }
            var frame = new PieceMesh();
            int iron = frame.Slot(Alloy("Gate - hammered midnight iron", new Color(0.09f, 0.14f, 0.18f), 0.8f));
            int bronze = frame.Slot(Alloy("Gate - aged bronze inlay", new Color(0.52f, 0.31f, 0.12f), 0.7f));
            int recess = frame.Slot(Alloy("Gate - recessed enamel", new Color(0.025f, 0.05f, 0.065f), 0.35f));
            var runeMaterial = new Material(Resources.Load<Shader>("Shaders/SpiritGlow")) { name = "Gate - turquoise inscriptions" };
            runeMaterial.SetColor("_BaseColor", new Color(0.04f, 0.5f, 0.6f));
            runeMaterial.SetColor("_CoreColor", new Color(0.55f, 1.3f, 1.4f));
            owned.Add(runeMaterial);
            int rune = frame.Slot(runeMaterial);

            void Block(int slot, Vector3 c, Vector3 size) => frame.Block(slot, c, Vector3.right * size.x * 0.5f,
                Vector3.up * size.y * 0.5f, Vector3.forward * size.z * 0.5f);
            Vector3 Arch(float angle, float radius, float z) => new Vector3(Mathf.Cos(angle) * radius,
                2.45f + Mathf.Sin(angle) * radius * 0.98f, z);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 2.48f;
                Block(iron, new Vector3(x, 0.19f, 0), new Vector3(1.2f, 0.38f, 1.3f));
                Block(bronze, new Vector3(x, 0.43f, 0), new Vector3(1.03f, 0.12f, 1.12f));
                frame.Frustum(iron, new Vector3(x, 0.49f, 0), new Vector3(x, 2.48f, 0), 0.58f, 0.38f, 6, Vector3.forward);
                Block(bronze, new Vector3(x, 2.4f, 0), new Vector3(0.91f, 0.13f, 0.95f));
                // Recessed faces and glyphs on both sides, readable on either approach.
                for (int face = -1; face <= 1; face += 2)
                {
                    Block(recess, new Vector3(x, 1.45f, face * 0.48f), new Vector3(0.38f, 1.54f, 0.06f));
                    for (int g = 0; g < 4; g++)
                    {
                        Vector3 c = new Vector3(x, 0.86f + g * 0.38f, face * 0.525f);
                        frame.Beam(rune, c + new Vector3(-0.11f, 0.08f, 0), c + new Vector3(0.1f, -0.06f, 0), 0.021f, Vector3.forward * 0.012f);
                        frame.Beam(rune, c + new Vector3(0.1f, -0.06f, 0), c + new Vector3(-0.04f, -0.14f, 0), 0.021f, Vector3.forward * 0.012f);
                        Block(rune, c + Vector3.up * 0.05f, new Vector3(0.035f, 0.22f, 0.025f));
                    }
                }
                var collider = new GameObject("GateButtress").AddComponent<BoxCollider>();
                collider.transform.SetParent(parent, false);
                collider.center = new Vector3(x, 1.4f, 0);
                collider.size = new Vector3(0.9f, 2.8f, 1.05f);
            }
            // Individually cut arch segments, separated by narrow dark joints.
            const int segments = 15;
            for (int k = 0; k < segments; k++)
            {
                float a = k * Mathf.PI / segments + 0.009f;
                float b = (k + 1) * Mathf.PI / segments - 0.009f;
                var outline = new[] { (Vector2)Arch(a, 2.12f, 0), (Vector2)Arch(a, 2.79f, 0),
                    (Vector2)Arch(b, 2.79f, 0), (Vector2)Arch(b, 2.12f, 0) };
                Prism(frame, iron, outline, 0.85f, Vector3.zero, Quaternion.identity);
                for (int face = -1; face <= 1; face += 2)
                {
                    float z = face * 0.445f;
                    frame.Beam(bronze, Arch(a, 2.2f, z), Arch(b, 2.2f, z), 0.045f, Vector3.forward * 0.035f);
                    frame.Beam(bronze, Arch(a, 2.7f, z), Arch(b, 2.7f, z), 0.035f, Vector3.forward * 0.035f);
                    float mid = (a + b) * 0.5f;
                    frame.Beam(rune, Arch(mid, 2.37f, z), Arch(mid, 2.51f, z), 0.028f, Vector3.forward * 0.025f);
                }
            }
            // Faceted crown and suspended crystal, rather than a horizontal lintel.
            Prism(frame, bronze, new[] { new Vector2(-0.37f, 4.92f), new Vector2(0, 5.65f),
                new Vector2(0.37f, 4.92f), new Vector2(0, 4.55f) }, 0.45f, Vector3.zero, Quaternion.identity);
            for (int face = -1; face <= 1; face += 2)
                frame.Lump(rune, new Vector3(0, 5.02f, face * 0.25f), new Vector3(0.17f, 0.34f, 0.16f), 4, 2, Quaternion.identity);
            Block(iron, new Vector3(0, 0.09f, 0), new Vector3(4.5f, 0.18f, 1.12f));
            for (int face = -1; face <= 1; face += 2)
                Block(bronze, new Vector3(0, 0.18f, face * 0.46f), new Vector3(4.35f, 0.045f, 0.065f));
            GameObject built = frame.Build(parent, "RuneboundArch");
            owned.Add(built.GetComponent<MeshFilter>().sharedMesh);

            // A fitted aperture instead of a rectangular slab. Local XY drives its living texture.
            var aperture = new List<Vector3> { new Vector3(-2.13f, 0.18f, 0), new Vector3(2.13f, 0.18f, 0) };
            for (int k = 0; k <= 32; k++) aperture.Add(Arch(k * Mathf.PI / 32, 2.14f, 0));
            var vertices = new List<Vector3> { new Vector3(0, 2.4f, 0) };
            vertices.AddRange(aperture);
            var indices = new List<int>();
            for (int k = 0; k < aperture.Count; k++)
            {
                indices.Add(0); indices.Add(k + 1); indices.Add((k + 1) % aperture.Count + 1);
            }
            var mesh = new Mesh { name = "Arched portal aperture" };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            owned.Add(mesh);
            var portal = new Material(Resources.Load<Shader>("Shaders/GatePortal")) { name = "Gate - living aether" };
            portal.SetColor("_BaseColor", new Color(0.18f, 0.65f, 0.85f));
            owned.Add(portal);
            var panel = new GameObject("PortalPanel");
            panel.transform.SetParent(parent, false);
            panel.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = panel.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = portal;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void BuildWater(int area, float floorY)
        {
            AreaShape shape = Shape(area);
            if (!shape.HasWater) return;
            Transform group = Group("Water_" + AreaNames[area]);
            group.position = shape.Center + Vector3.up * floorY;
            bool frozen = area == Frozen;
            var water = new Material(kit.Mat("Water")) { name = "Water_" + AreaNames[area] };
            Color color = frozen ? new Color(0.40f, 0.67f, 0.78f) :
                area == Graveyard ? new Color(0.13f, 0.29f, 0.28f) :
                shape.HasOcean ? new Color(0.10f, 0.38f, 0.49f) : new Color(0.16f, 0.42f, 0.48f);
            water.SetColor("_BaseColor", color);
            water.SetColor("_ShadowColor", color * 0.6f);
            water.SetFloat("_TriplanarTileSize", frozen ? 5f : 8f);
            water.SetFloat("_RimIntensity", 0.08f);
            water.SetFloat("_TexInfluence", frozen ? 0.24f : 0.12f);
            WaterPart(group, frozen ? "FrozenRiverAndPools" : "RiversAndLakes", shape.BuildWaterMesh(), water, -0.45f);
            var shore = new Material(kit.Mat(frozen ? "Snow" : shape.HasOcean ? "Tan" : "TanDark"));
            shore.SetFloat("_TriplanarTileSize", 3f);
            shore.SetFloat("_TexInfluence", 0.35f);
            WaterPart(group, shape.HasOcean ? "SandyBeach" : "Shore", shape.BuildShoreMesh(), shore, 0.012f);
            var timber = new Material(kit.Mat("Wood")) { name = "BridgeTimber" };
            timber.SetColor("_BaseColor", new Color(0.50f, 0.34f, 0.20f));
            timber.SetFloat("_TexInfluence", 0.28f);
            timber.SetFloat("_TriplanarTileSize", 2f);
            foreach (var bridge in shape.Bridges) BuildTimberBridge(group, shape, bridge.center, bridge.size, bridge.yaw, timber);
        }

        private void BuildTimberBridge(Transform parent, AreaShape shape, Vector2 center, Vector2 size, float yaw, Material timber)
        {
            // Find both banks across the full deck width, including oblique river crossings.
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            float left = 0, right = 0;
            for (float x = -size.x * 0.5f; x <= size.x * 0.5f; x += 0.15f)
                for (int side = -1; side <= 1; side++)
                    if (shape.WaterDistance(shape.Center + new Vector3(center.x, 0, center.y) + rotation * new Vector3(x, 0, side * size.y * 0.5f)) >= 0)
                    { left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
            left -= 1.1f; right += 1.1f;
            float length = right - left;
            Transform deck = new GameObject("TimberBridge").transform;
            deck.SetParent(parent, false);
            deck.localPosition = new Vector3(center.x, 0, center.y);
            deck.localRotation = rotation;
            float Height(float x) => 0.065f + 0.32f * Mathf.Sin(Mathf.Clamp01((x - left) / length) * Mathf.PI);
            int count = Mathf.CeilToInt(length / 0.52f);
            float step = length / count;
            for (int i = 0; i < count; i++)
            {
                float x = left + (i + 0.5f) * step;
                float slope = Mathf.Atan2(Height(x + 0.05f) - Height(x - 0.05f), 0.1f) * Mathf.Rad2Deg;
                float variation = Mathf.Sin(i * 2.71f + center.y) * 0.07f;
                LocalBox(deck, new Vector3(x, Height(x) - 0.055f, variation * 0.35f),
                    new Vector3(step - 0.018f, 0.11f, size.y + variation), timber, false, new Vector3(0, 0, slope));
            }
            // A continuous curved top collider lets feet follow the arch without board seams.
            var points = new Vector3[(count + 1) * 2];
            var indices = new int[count * 6];
            for (int i = 0; i <= count; i++)
            {
                float x = left + i * step;
                points[i * 2] = new Vector3(x, Height(x), -size.y * 0.5f);
                points[i * 2 + 1] = new Vector3(x, Height(x), size.y * 0.5f);
                if (i == count) continue;
                int v = i * 2, t = i * 6;
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v + 2; indices[t + 4] = v + 1; indices[t + 5] = v + 3;
            }
            var collision = new Mesh { name = "ArchedBridgeDeck", vertices = points, triangles = indices };
            collision.RecalculateBounds();
            deck.gameObject.AddComponent<MeshCollider>().sharedMesh = collision;
            foreach (float side in new[] { -1f, 1f })
            {
                float z = side * (size.y * 0.5f + 0.14f);
                int sections = Mathf.CeilToInt(length / 2.8f);
                for (int i = 0; i <= sections; i++)
                {
                    float x = Mathf.Lerp(left + 0.2f, right - 0.2f, (float)i / sections);
                    LocalBox(deck, new Vector3(x, Height(x) + 0.48f, z), new Vector3(0.20f, 1.1f, 0.20f), timber);
                    if (i == sections) continue;
                    float next = Mathf.Lerp(left + 0.2f, right - 0.2f, (float)(i + 1) / sections);
                    Vector3 a = new Vector3(x, Height(x), z), b = new Vector3(next, Height(next), z);
                    float tilt = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                    LocalBox(deck, (a + b) * 0.5f + Vector3.up * 0.9f,
                        new Vector3(Vector3.Distance(a, b) + 0.1f, 0.12f, 0.14f), timber, true, new Vector3(0, 0, tilt));
                    LocalBox(deck, (a + b) * 0.5f - Vector3.up * 0.19f,
                        new Vector3(Vector3.Distance(a, b) + 0.1f, 0.25f, 0.22f), timber, false, new Vector3(0, 0, tilt));
                }
                // Piles and diagonal braces seat the bridge into each bank.
                foreach (float x in new[] { left + 1.3f, right - 1.3f })
                {
                    LocalBox(deck, new Vector3(x, -0.35f, z), new Vector3(0.36f, 1.3f, 0.36f), timber, false);
                    LocalBox(deck, new Vector3(x + (x < 0 ? 0.4f : -0.4f), -0.18f, z),
                        new Vector3(1.25f, 0.16f, 0.18f), timber, false, new Vector3(0, 0, x < 0 ? 35 : -35));
                }
            }
        }

        private static void WaterPart(Transform parent, string name, Mesh mesh, Material material, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * y;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}

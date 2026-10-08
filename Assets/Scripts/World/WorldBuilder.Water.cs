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
            WaterPart(group, frozen ? "FrozenRiverAndPools" : "RiversAndLakes", shape.BuildWaterMesh(), water, -0.45f);
            WaterPart(group, "Shallows", shape.BuildWaterMesh(foam: true),
                frozen ? kit.Mat("Snow") : kit.Mat("Ash"), -0.42f);
            WaterPart(group, shape.HasOcean ? "SandyBeach" : "Shore", shape.BuildWaterMesh(shore: true),
                kit.Mat(frozen ? "Snow" : shape.HasOcean ? "Tan" : "TanDark"), 0.018f);
            foreach (var bridge in shape.Bridges)
            {
                Transform deck = new GameObject("TimberBridge").transform;
                deck.SetParent(group, false);
                deck.localPosition = new Vector3(bridge.center.x, 0, bridge.center.y);
                // The authored ground already supplies a level collision surface. Boards are visual
                // and side rails stay outside its walkable width, avoiding raised steps at either end.
                for (float x = -bridge.size.x * 0.5f + 0.35f; x < bridge.size.x * 0.5f; x += 0.7f)
                    LocalBox(deck, new Vector3(x, 0.015f, 0), new Vector3(0.66f, 0.06f, bridge.size.y), kit.Mat("Wood"), false);
                foreach (float side in new[] { -1f, 1f })
                {
                    float z = side * (bridge.size.y * 0.5f + 0.18f);
                    LocalBox(deck, new Vector3(0, 0.85f, z), new Vector3(bridge.size.x, 0.16f, 0.22f), kit.Mat("Wood"));
                    for (float x = -bridge.size.x * 0.5f; x <= bridge.size.x * 0.5f; x += 3f)
                        LocalBox(deck, new Vector3(x, 0.4f, z), new Vector3(0.25f, 1.5f, 0.25f), kit.Mat("Wood"));
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

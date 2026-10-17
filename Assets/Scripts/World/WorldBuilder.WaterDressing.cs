using System.Collections.Generic;
using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private void DressWaterChannels(Transform parent, AreaShape shape, int area, float floorY, Material timber, Rect? region)
        {
            Rect bounds = region ?? new Rect(-shape.Size * 0.5f, shape.Size);
            // World-aligned samples make dressing stable regardless of streaming order.
            const float spacing = 15f;
            for (float z = Mathf.Ceil(bounds.yMin / spacing) * spacing; z < bounds.yMax; z += spacing)
                for (float x = Mathf.Ceil(bounds.xMin / spacing) * spacing; x < bounds.xMax; x += spacing)
                {
                    rng = new System.Random(unchecked(area * 7919 + (int)x * 131 + (int)z * 1777));
                    Vector3 local = new Vector3(x, 0, z);
                    Vector3 world = shape.Center + local;
                    float wet = shape.WaterDistance(world);
                    if (wet > 2.2f && !shape.IsBridge(world, 2f) && rng.NextDouble() < 0.55)
                        BuildDriftingBranch(parent, shape, local, floorY, timber, bounds);
                    if (shape.IsCave || wet < -7f || wet > -1.5f || !shape.Contains(world, 1.5f) ||
                        shape.IsBridge(world, 5f) || NearGate(area, world, 10f) || rng.NextDouble() > 0.55) continue;
                    bool questSite = false;
                    foreach (string quest in AreaLayouts.QuestGroups(area))
                        foreach (Vector3 anchor in AreaLayouts.QuestAnchors[quest])
                            if ((world - shape.Center - anchor).sqrMagnitude < 64f) questSite = true;
                    if (questSite) continue;
                    // Keep the bank walkable: tall silhouettes stand a few metres inland.
                    world.y = floorY;
                    if (area == Greenwood || area == Haven || area == Graveyard)
                    {
                        GameObject tree = area == Graveyard ? DeadTree(parent, world, kit.Mat("DeadWood"), R(1.35f, 1.8f))
                            : Prefab(BiomeTree(area, world), parent, world, R(0, 360), Vector3.one * R(1.0f, 1.3f));
                        tree.name = area == Graveyard ? "WatersideSnag" : "WatersideWillow";
                    }
                    else
                    {
                        Transform pillar = Holder(parent, "WatersideRuinedPillar", world, Quaternion.Euler(0, R(0, 360), 0));
                        float height = R(5.5f, 8f);
                        LocalBox(pillar, Vector3.up * 0.2f, new Vector3(1.4f, 0.4f, 1.4f), kit.Mat("RockDark"));
                        LocalBox(pillar, Vector3.up * height * 0.5f, new Vector3(0.65f, height, 0.8f), kit.Mat("RockDark"),
                            true, new Vector3(3, 0, -4));
                        LocalBox(pillar, Vector3.up * height, new Vector3(1.05f, 0.35f, 1.1f), kit.Mat("RockDark"));
                    }
                }
        }

        private void BuildDriftingBranch(Transform parent, AreaShape shape, Vector3 local, float floorY, Material timber, Rect bounds)
        {
            var route = new List<Vector3>();
            // Follow the same flow field as the shader, with a bankward gradient pushing debris offshore.
            for (int i = 0; i < 70; i++)
            {
                Vector3 world = shape.Center + local;
                if (!bounds.Contains(new Vector2(local.x, local.z)) || shape.WaterDistance(world) < 1.4f ||
                    shape.IsBridge(world, 1.5f)) break;
                Vector4 flow = shape.WaterMotion(local);
                if (flow.w < 0.4f) break;
                world.y = floorY - 0.43f;
                route.Add(world);
                Vector3 gradient = new Vector3(
                    shape.WaterDistance(world + Vector3.right * 0.4f) - shape.WaterDistance(world - Vector3.right * 0.4f), 0,
                    shape.WaterDistance(world + Vector3.forward * 0.4f) - shape.WaterDistance(world - Vector3.forward * 0.4f));
                Vector3 direction = new Vector3(flow.x, 0, flow.y);
                if (shape.WaterDistance(world) < 3.5f) direction += gradient.normalized * 0.65f;
                local += direction.normalized * 0.6f;
            }
            if (route.Count < 12) return;
            Transform branch = Holder(parent, "DriftingForkedBranch", route[0], Quaternion.identity);
            var mesh = new PieceMesh();
            int bark = mesh.Slot(timber);
            float length = R(1.4f, 2.5f);
            Vector3 tail = Vector3.back * length * 0.5f, tip = Vector3.forward * length * 0.5f;
            mesh.Frustum(bark, tail, Vector3.zero, 0.11f, 0.09f, 6, Vector3.up);
            mesh.Frustum(bark, Vector3.zero, tip, 0.09f, 0.035f, 6, Vector3.up);
            mesh.Frustum(bark, Vector3.zero, new Vector3(length * 0.35f, 0.035f, length * 0.3f), 0.065f, 0.012f, 5, Vector3.up);
            mesh.Frustum(bark, tail * 0.4f, new Vector3(-length * 0.22f, 0.06f, length * 0.12f), 0.045f, 0.008f, 5, Vector3.up);
            mesh.Build(branch, "WaterloggedWood");
            branch.gameObject.AddComponent<DriftingWaterBranch>().Initialize(route.ToArray(), R(0, 1), R(0.4f, 0.7f));
        }
    }
}

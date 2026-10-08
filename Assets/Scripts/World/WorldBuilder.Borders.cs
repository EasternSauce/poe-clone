using System.Collections.Generic;
using UnityEngine;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private const float ScatterScale = 3.2f;
        private static AreaShape[] shapes;
        private readonly List<(int area, Vector3 position)> gatePoints = new List<(int, Vector3)>();
        public static AreaShape Shape(int area)
        {
            if (shapes == null) InitShapes();
            return shapes[Mathf.Clamp(area, 0, shapes.Length - 1)];
        }
        private static void InitShapes()
        {
            shapes = new AreaShape[Centers.Length];
            for (int a = 0; a < Centers.Length; a++) shapes[a] = AreaLayouts.Create(a, Centers[a]);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShapes() { shapes = null; }

        private void ShapeGround(GameObject ground, int area)
        {
            AreaShape shape = Shape(area);
            shape.BuildGroundMesh(0f); // Keep the walkable contour for boundary collision.
            Mesh mesh = shape.BuildGroundMesh(0f, omitBridges: true);
            ground.transform.position = new Vector3(shape.Center.x, ground.transform.position.y, shape.Center.z);
            ground.transform.rotation = Quaternion.identity;
            ground.transform.localScale = Vector3.one;
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = ground.GetComponent<MeshCollider>();
            if (collider == null) collider = ground.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            BuildLayoutWalls(area, ground.transform.position.y);
            BuildWater(area, ground.transform.position.y);
        }

        private Vector3 GatePoint(int area, Vector3 direction)
        {
            bool forward = Mathf.Abs(direction.z) > Mathf.Abs(direction.x) ? direction.z > 0 : direction.x > 0;
            return Center(area) + AreaLayouts.GateLocal(area, forward);
        }
        private bool NearGate(int area, Vector3 p, float radius)
        {
            foreach (var gate in gatePoints)
                if (gate.area == area && (gate.position - p).sqrMagnitude < radius * radius) return true;
            return false;
        }

        private void BuildLayoutWalls(int area, float floorY)
        {
            AreaShape shape = Shape(area);
            Transform group = Group("Bounds_" + AreaNames[area]);
            group.position = shape.Center + Vector3.up * floorY;
            var wall = new GameObject("Wall");
            wall.transform.SetParent(group, false);
            // Vertical, continuous collision round every outer edge and internal hole.
            wall.AddComponent<MeshCollider>().sharedMesh = shape.BuildWalls(-1f, 10f);
            var rim = new GameObject(shape.IsCliff ? "CliffFaces" : "RockBanks");
            rim.transform.SetParent(group, false);
            rim.AddComponent<MeshFilter>().sharedMesh = shape.BuildWalls(shape.IsCliff ? -12f : -0.5f,
                shape.IsCave ? 3.2f : shape.IsCliff ? 0.25f : 1.3f, 0.025f);
            rim.AddComponent<MeshRenderer>().sharedMaterial = LayoutRockMaterial(area);
            // Backdrops are visual only: the enclosing rectangle and cliff bottoms cannot be walked on.
            Box(group, shape.Center + new Vector3(0, floorY - (shape.IsCliff ? 12.5f : 1.5f), 0),
                new Vector3(shape.Size.x + 24, 0.1f, shape.Size.y + 24), kit.Mat(shape.IsCliff ? "Lava" : "Charred"), false).name = "Backdrop";
        }

        private Material LayoutRockMaterial(int area)
        {
            // Broad rock strata suit continuous banks better than tiny repeated flecks.
            // Frozen walls are pale rock with ice tint, rather than the blue Steel copy
            // used for small ice props and water elsewhere in the world.
            var material = new Material(kit.Mat("RockDark")) { name = "LayoutRock_" + AreaNames[area] };
            Color color = area == Frozen ? new Color(0.65f, 0.73f, 0.78f) :
                area == Ruins ? new Color(0.32f, 0.27f, 0.24f) : new Color(0.34f, 0.33f, 0.31f);
            material.SetColor("_BaseColor", color);
            material.SetColor("_ShadowColor", new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.55f));
            material.SetFloat("_TriplanarTileSize", 3.2f);
            material.SetFloat("_TexInfluence", 0.6f);
            material.SetFloat("_RimIntensity", 0.04f);
            return material;
        }

        private void BuildBorders()
        {
            foreach (int area in WorldAreas)
            {
                Begin(area, 900 + area);
                Transform group = Group("Border_" + AreaNames[area]);
                AreaShape shape = Shape(area);
                float walked = 0f;
                foreach (var edge in shape.Boundary)
                {
                    walked += Vector3.Distance(edge.a, edge.b);
                    if (walked < 6f) continue;
                    walked = 0f;
                    Vector3 p = shape.Center + (edge.a + edge.b) * 0.5f;
                    Vector3 outward = Vector3.Cross(edge.b - edge.a, Vector3.up).normalized;
                    if (shape.Contains(p + outward * 0.4f)) outward = -outward;
                    p += outward * 4.5f;
                    if (NearGate(area, p, 12f) || shape.Contains(p, -3f) || shape.WaterDistance(p) > -9f) continue;
                    GameObject prop;
                    if (shape.IsCave || shape.IsCliff || rng.NextDouble() < 0.3)
                    {
                        prop = Prefab(kit.rock, group, p, R(0, 360), new Vector3(R(2, 3), R(1.7f, 2.6f), R(2, 3)));
                        if (area == Frozen) Tint(prop, new Color(0.65f, 0.78f, 0.9f));
                    }
                    else if (area == Graveyard) prop = DeadTree(group, p, kit.Mat("DeadWood"));
                    else prop = Prefab(Coin() ? kit.pine : kit.oak, group, p, R(0, 360), Vector3.one * R(1.1f, 1.5f));
                    NoShadows(prop);
                }
            }
        }

        private void BuildCave()
        {
            Begin(Cave, 660);
            Transform group = Group("Cave");
            // Only roomy chambers receive solid scenery; passages keep their full clearance.
            Scatter(group, 12, 16, 170, p => Prefab(kit.rock, group, p, R(0, 360), Vector3.one * R(0.7f, 1.1f)), 1.4f);
            Scatter(group, 12, 12, 170, p => Bones(group, p), 0.5f);
            foreach (Vector3 local in new[] { new Vector3(-126, 0, -74), new Vector3(124, 0, 84), new Vector3(47, 0, -64), new Vector3(-64, 0, 62) })
            {
                Vector3 p = Center(Cave) + local;
                Glowshrooms(group, p + new Vector3(10, 0, 6));
                Glow(group, p + Vector3.up * 2, ShroomLight, 16, 4);
            }
        }

        private void ConfigureCaveLighting(Transform player)
        {
            foreach (int area in new[] { Cave, Frozen, ActArena })
            {
                AreaShape shape = Shape(area);
                Shader.SetGlobalVector(area == Cave ? "_CaveBounds" : area == Frozen ? "_HollowBounds" : "_SanctuaryBounds",
                    new Vector4(shape.Center.x, shape.Center.z, shape.Size.x * 0.5f + 10, shape.Size.y * 0.5f + 10));
            }
            var light = Glow(player, player.position + Vector3.up * 2.5f, new Color(0.60f, 0.70f, 0.80f), 13, 8);
            light.gameObject.name = "CaveVisibility";
            light.gameObject.AddComponent<CaveVisibility>();
        }

        private void RelocateOriginalScenery()
        {
            Begin(Greenwood, 640);
            AreaShape shape = Shape(Greenwood);
            var props = new List<(Transform prop, float radius)>();
            foreach (string name in new[] { "Trees", "Rocks", "Bushes", "Houses", "Ruins" })
            {
                GameObject group = GameObject.Find(name);
                if (group == null) continue;
                foreach (Transform prop in group.transform)
                {
                    float radius = name == "Houses" ? 8f : 4f;
                    props.Add((prop, radius));
                    if (shape.Contains(prop.position, radius + 1f) && !shape.IsBridge(prop.position, radius + 3f)) Claim(prop.position, radius);
                }
            }
            // Scene torches were authored beside houses/pillars. Move them with their nearest prop.
            var attachedTorches = new Dictionary<Transform, Transform>();
            GameObject torches = GameObject.Find("Torches");
            if (torches != null)
                foreach (Transform torch in torches.transform)
                {
                    Transform closest = null;
                    float distance = 36f;
                    foreach (var prop in props)
                    {
                        if (prop.prop.parent.name != "Houses" && prop.prop.parent.name != "Ruins") continue;
                        float d = (prop.prop.position - torch.position).sqrMagnitude;
                        if (d < distance) { distance = d; closest = prop.prop; }
                    }
                    if (closest != null) attachedTorches[torch] = closest;
                }
            foreach (var entry in props)
            {
                Transform prop = entry.prop;
                Vector3 old = prop.position;
                if (shape.Contains(old, entry.radius + 1f) && !shape.IsBridge(old, entry.radius + 3f)) continue;
                Vector3 target = shape.NearestOpen(old, entry.radius + 2f);
                for (int attempt = 0; attempt < 500; attempt++)
                {
                    if (Free(target, entry.radius) && (target - shape.Center).sqrMagnitude > 196f &&
                        !HitsScenery(target + Vector3.up, entry.radius)) break;
                    target = shape.Center + Flat(R(-150, 150), R(-120, 120));
                    if (!shape.Contains(target, entry.radius + 2f)) { target = shape.NearestOpen(target, entry.radius + 2f); }
                }
                prop.position = new Vector3(target.x, old.y, target.z);
                Vector3 delta = prop.position - old;
                foreach (var torch in attachedTorches)
                    if (torch.Value == prop) torch.Key.position += delta;
                Claim(target, entry.radius);
                Physics.SyncTransforms();
            }
        }
        // A dirt road from the town plaza out to each of Haven's gates.
        private void BuildHavenRoads(Transform t)
        {
            Material dirt = kit.Mat("TanDark");
            WindingPath(t, "GateLane", Haven, 4.2f, dirt,
                Flat(10, 0), Flat(28, 3), Flat(48, -8), Flat(70, -5),
                Flat(92, 8), Flat(113, 5), AreaLayouts.GateLocal(Haven, true));
            WindingPath(t, "SouthLane", Haven, 3.6f, dirt,
                Flat(0, -10), Flat(-3, -24), Flat(4, -39), Flat(-9, -58), Flat(-20, -80));
            WindingPath(t, "NorthLane", Haven, 3.4f, dirt,
                Flat(0, 10), Flat(-4, 25), Flat(8, 41), Flat(28, 55), Flat(42, 70));
            WindingPath(t, "WestLane", Haven, 3.4f, dirt,
                Flat(-10, 1), Flat(-27, 6), Flat(-44, 0), Flat(-63, 12), Flat(-85, 25));
        }

        private static void NoShadows(GameObject go)
        {
            if (go == null)
                return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void Tint(GameObject go, Color color)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                r.SetPropertyBlock(block);
            }
        }

        private void BuildGreenwoodOutskirts()
        {
            Begin(Greenwood, 606);
            AreaShape shape = Shape(Greenwood);
            Transform t = Group("Outskirts");
            for (int k = 0, attempts = 0; k < 120 && attempts < 1500; attempts++)
            {
                Vector3 p = shape.Center + Flat(R(-AreaShape.MaxRadius, AreaShape.MaxRadius), R(-AreaShape.MaxRadius, AreaShape.MaxRadius));
                if (Mathf.Abs(p.x) < 47f && Mathf.Abs(p.z) < 47f)
                    continue; // the scene's own forest
                if (!shape.Contains(p, 5f) || !Free(p, 2.4f) || HitsScenery(p + Vector3.up * 1.2f, 1.5f))
                    continue;

                double roll = rng.NextDouble();
                if (roll < 0.6)
                    Prefab(Coin() ? kit.pine : kit.oak, t, p, R(0f, 360f), Vector3.one * R(0.9f, 1.3f));
                else if (roll < 0.8)
                    Prefab(kit.rock, t, p, R(0f, 360f), Vector3.one * R(0.8f, 1.5f));
                else
                    Prefab(kit.bushSmall, t, p, R(0f, 360f), Vector3.one * R(0.3f, 0.5f));
                Claim(p, 2.4f);
                k++;
            }
        }
    }
}

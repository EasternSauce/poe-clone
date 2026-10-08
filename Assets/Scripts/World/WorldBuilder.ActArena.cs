using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Quests;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        public const int ActArena = 5;
        public static readonly Vector3 ActArenaCenter = new Vector3(1680f, 0f, 0f);
        public static Vector3 SanctuaryDoorSpot => Center(Frozen) + new Vector3(0f, 0f, 128f);
        public static Quaternion SanctuaryDoorRotation => Quaternion.Euler(0f, -25f, 0f);
        private AreaDefinition BuildActArena()
        {
            Transform arena = Group("The Shed Sanctuary");
            Vector3 c = ActArenaCenter;
            var floor = new GameObject("Ground_Sanctuary");
            floor.transform.SetParent(arena, false);
            floor.transform.position = c;
            Mesh mesh = Shape(ActArena).BuildGroundMesh(0f);
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = kit.Mat("RockDark");
            floor.AddComponent<MeshCollider>().sharedMesh = mesh;
            BuildLayoutWalls(ActArena, 0f);
            BuildSanctuaryLair(arena, c);
            Transform entry = Marker("SanctuaryEntry", c + new Vector3(0f, 1.1f, -31f), 0f);
            Vector3 door = SanctuaryDoorSpot + SanctuaryDoorRotation * new Vector3(0f, 1.1f, -3f);
            Transform outside = Marker("SanctuaryReturn", door + SanctuaryDoorRotation * Vector3.back * 6f, 155f);
            MakeActGate(arena, "Sanctuary entrance", door, Frozen, ActArena, entry, true);
            MakeActGate(arena, "Sanctuary exit", c + new Vector3(0f, 1.1f, -37f), ActArena, Frozen, outside, false);
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            var encounter = arena.gameObject.AddComponent<ActBossArena>();
            encounter.Configure(spawner != null ? spawner.EnemyPrefab : null, outside, c + Vector3.forward * 14f);
            return new AreaDefinition { areaName = "The Shed Sanctuary", monsterLevel = 12, spawnPoint = entry, tintsSharedGround = false };
        }

        private void BuildSanctuaryLair(Transform arena, Vector3 c)
        {
            // Keep the middle and boss spawn clear. All dressing is visual only, including the gate.
            Transform dressing = Holder(arena, "ShepherdLair", c, Quaternion.identity);
            float[] offsets = { -27f, -15f, -3f, 10f, 23f, 31f };
            float[] heights = { 7.2f, 9.8f, 11.4f, 10.2f, 8.6f, 6.1f };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < offsets.Length; i++)
                {
                    float z = offsets[i] + (side > 0 ? 2.3f : 0f);
                    float x = side * (44f - (z + 27f) * 0.19f + Mathf.Sin(i * 1.8f) * 1.3f);
                    Transform rib = Holder(dressing, "BuriedRib", c + new Vector3(x, -0.45f, z),
                        Quaternion.Euler(7f + i % 3 * 4f, side * (18f + i * 3f), side * -9f));
                    float h = heights[i] * (side > 0 ? 0.93f : 1f);
                    SanctuaryCurve(rib, "ContinuousRib", Vector3.zero,
                        new Vector3(-side * 0.5f, h * 0.48f, 0.4f),
                        new Vector3(-side * 3.2f, h, 1.1f),
                        new Vector3(-side * 6.3f, h * 0.91f, 1.6f), 0.92f, kit.Mat("Bone"));
                    // Flattened, uneven earth collars anchor the bones instead of rectangular moss pads.
                    LocalBall(rib, new Vector3(0f, 0.35f, 0f), 1f, kit.Mat("TanDark"))
                        .transform.localScale = new Vector3(3.8f, 0.7f, 2.8f);
                    SanctuaryOffering(dressing, c + new Vector3(x - side * 5.5f, 0f, z - 2f),
                        side * (24f + i * 19f), i);
                }
            }

            // A readable reverse face of the Shepherd's doorway, aligned with the existing exit trigger.
            Transform gate = Holder(dressing, "SanctuaryInnerGate", c + new Vector3(0f, 0f, -38f),
                Quaternion.Euler(0f, -18f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                LocalBox(gate, new Vector3(side * 4.3f, 3.7f, 0f), new Vector3(1.4f, 7.4f, 1.5f),
                    kit.Mat("TombstoneDark"), false, new Vector3(0f, 0f, side * -3f));
                SanctuaryCurve(gate, "GateAntler", new Vector3(side * 1.6f, 7.6f, 0f),
                    new Vector3(side * 2.2f, 8.8f, 0f), new Vector3(side * 3.8f, 9.9f, 0.1f),
                    new Vector3(side * 4.5f, 9.4f, 0.1f), 0.35f, kit.Mat("Bone"));
                SanctuaryCurve(gate, "AntlerTine", new Vector3(side * 2.7f, 9f, 0f),
                    new Vector3(side * 2.7f, 9.4f, 0f), new Vector3(side * 2.6f, 10.1f, 0f),
                    new Vector3(side * 2.8f, 10.5f, 0f), 0.18f, kit.Mat("Bone"));
                SanctuaryOffering(dressing, c + new Vector3(side * 7f, 0f, -34f), side * 30f, 2);
            }
            LocalBox(gate, new Vector3(0f, 7.5f, 0f), new Vector3(10f, 1f, 1.6f), kit.Mat("TombstoneDark"), false);
            LocalBox(gate, new Vector3(0f, 0.025f, 0f), new Vector3(7.2f, 0.05f, 3f), kit.Mat("TanDark"), false);
            SanctuarySkull(gate, new Vector3(0f, 7.65f, 0.92f), 0.72f);
        }

        private void SanctuaryOffering(Transform parent, Vector3 at, float yaw, int variant)
        {
            Transform offering = Holder(parent, "ShedOffering", at, Quaternion.Euler(0f, yaw, 0f));
            // A low, curled discarded hide, loose bones and wax: nothing to obstruct movement or shots.
            Transform hide = Holder(offering, "ShedHide", at, offering.rotation);
            hide.localScale = new Vector3(1f, 0.14f, 1f);
            SanctuaryCurve(hide, "CurledHide", new Vector3(-0.7f, 0.4f, -0.8f),
                new Vector3(-1.2f, 1f, 0f), new Vector3(1.1f, 0.6f, 0.7f),
                new Vector3(0.65f, 0.8f, 1.4f), 0.65f, kit.Mat("Leather"));
            SanctuarySkull(offering, new Vector3(0.5f, 0.25f, -0.3f), 0.28f);
            SanctuaryCurve(offering, "LooseBone", new Vector3(-0.8f, 0.12f, 0.3f),
                new Vector3(-0.3f, 0.16f, 0.5f), new Vector3(0.4f, 0.17f, 0.3f),
                new Vector3(0.9f, 0.12f, 0.65f), 0.09f, kit.Mat("Bone"));
            for (int j = 0; j < 2 + variant % 2; j++)
            {
                Vector3 p = new Vector3(-1f + j * 0.32f, 0f, -0.65f - j % 2 * 0.25f);
                float h = 0.28f + (j + variant) % 3 * 0.12f;
                LocalCyl(offering, p + Vector3.up * h * 0.5f, 0.075f, h, kit.Mat("Candle"), false);
                LocalBall(offering, p + Vector3.up * (h + 0.05f), 0.06f, kit.Mat("Lantern"));
            }
        }

        private void SanctuarySkull(Transform parent, Vector3 p, float radius)
        {
            LocalBall(parent, p, radius, kit.Mat("Bone")).transform.localScale =
                new Vector3(radius * 1.8f, radius * 1.65f, radius * 2.5f);
            LocalBall(parent, p + new Vector3(0f, -radius * 0.35f, radius * 0.9f), radius * 0.55f, kit.Mat("Bone"));
            for (int side = -1; side <= 1; side += 2)
                LocalBall(parent, p + new Vector3(side * radius * 0.55f, radius * 0.12f, radius * 0.86f),
                    radius * 0.27f, kit.Mat("RockDark"));
        }

        // One watertight tapered tube along a cubic curve: no intersecting box joints or colliders.
        private void SanctuaryCurve(Transform parent, string name, Vector3 a, Vector3 b, Vector3 c,
            Vector3 d, float radius, Material material)
        {
            const int rings = 18, sides = 9;
            var vertices = new Vector3[(rings + 1) * sides + 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[rings * sides * 6 + sides * 6];
            int index = 0;
            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings, u = 1f - t;
                Vector3 center = u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
                Vector3 tangent = (3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c)).normalized;
                Vector3 normal = Vector3.Cross(tangent, Vector3.forward).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                float width = radius * Mathf.Lerp(1f, 0.045f, Mathf.Pow(t, 1.35f)) * (1f + 0.045f * Mathf.Sin(t * 19f));
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides;
                    int v = ring * sides + side;
                    vertices[v] = center + width * (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle) * 0.8f);
                    uv[v] = new Vector2(side / (float)sides, t * 3f);
                    if (ring == rings) continue;
                    int next = ring * sides + (side + 1) % sides;
                    triangles[index++] = v; triangles[index++] = next; triangles[index++] = v + sides;
                    triangles[index++] = next; triangles[index++] = next + sides; triangles[index++] = v + sides;
                }
            }
            int bottom = vertices.Length - 2, top = vertices.Length - 1;
            vertices[bottom] = a; vertices[top] = d;
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                triangles[index++] = bottom; triangles[index++] = next; triangles[index++] = side;
                triangles[index++] = top; triangles[index++] = rings * sides + side; triangles[index++] = rings * sides + next;
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        private void MakeActGate(Transform parent, string name, Vector3 at, int from, int to, Transform arrival, bool sealedDoor)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = at;
            if (sealedDoor) go.transform.rotation = SanctuaryDoorRotation;
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(6f, 4f, 2f);
            var gate = go.AddComponent<AreaGate>(); gate.fromAreaIndex = from; gate.targetAreaIndex = to; gate.arrival = arrival;
            if (sealedDoor)
            {
                gate.Locked = () => !ActBossArena.DoorOpen;
                gate.LockedMessage = "Finish the main questline and speak to Elder Maren";
            }
        }
    }
}

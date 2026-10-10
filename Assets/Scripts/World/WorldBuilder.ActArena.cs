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
        private static Vector3 SanctuaryInnerGateSpot => ActArenaCenter + new Vector3(0f, 0f, -42f);
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
            MakeActGate(arena, "Sanctuary exit", SanctuaryInnerGateSpot + new Vector3(0f, 1.1f, 1f), ActArena, Frozen, outside, false);
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            var encounter = arena.gameObject.AddComponent<ActBossArena>();
            encounter.Configure(spawner != null ? spawner.EnemyPrefab : null, outside, c + Vector3.forward * 14f);
            return new AreaDefinition { areaName = "The Shed Sanctuary", monsterLevel = MonsterLevels[ActArena], spawnPoint = entry, tintsSharedGround = false };
        }

        private void BuildSanctuaryLair(Transform arena, Vector3 c)
        {
            // Tall scenery stays at the sides; low floor remains are scattered throughout the arena.
            // All dressing is visual only, including the gate.
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

            // Seat the reverse face against the south wall, with the exit trigger just inside.
            Transform gate = Holder(dressing, "SanctuaryInnerGate", SanctuaryInnerGateSpot, Quaternion.identity);
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
            LocalBox(gate, new Vector3(0f, 0.025f, 0f), new Vector3(7.2f, 0.05f, 3f), DirtFloor, false);
            SanctuarySkull(gate, new Vector3(0f, 7.65f, 0.92f), 0.72f);
            ScatterSanctuaryRemains(dressing, c);
            // Unevenly scattered moults follow the perimeter, leaving the fighting floor open.
            SanctuaryShedSkin(dressing, c + new Vector3(-46f, 0f, -14f), 25f, 0);
            SanctuaryShedSkin(dressing, c + new Vector3(-15f, 0f, 36f), -78f, 2);
            SanctuaryShedSkin(dressing, c + new Vector3(49f, 0f, 9f), 18f, 1);
            SanctuaryShedSkin(dressing, c + new Vector3(20f, 0f, -32f), 105f, 3);
        }

        private void ScatterSanctuaryRemains(Transform parent, Vector3 c)
        {
            // A separate fixed seed keeps the lair identical for every client without changing world RNG.
            var random = new System.Random(51873);
            var placed = new System.Collections.Generic.List<Vector2>();
            for (int attempt = 0; attempt < 800 && placed.Count < 66; attempt++)
            {
                var p = new Vector2((float)random.NextDouble() * 104f - 52f,
                    (float)random.NextDouble() * 70f - 35f);
                // Leave the arrival and exact boss spawn readable, but dress the rest of the fighting floor.
                if ((p - new Vector2(0f, -31f)).sqrMagnitude < 36f ||
                    (p - new Vector2(0f, 14f)).sqrMagnitude < 16f) continue;
                bool crowded = false;
                foreach (Vector2 other in placed)
                    if ((p - other).sqrMagnitude < 36f) { crowded = true; break; }
                if (crowded) continue;
                int variant = placed.Count % 6;
                placed.Add(p);
                Vector3 at = c + new Vector3(p.x, 0f, p.y);
                float yaw = (float)random.NextDouble() * 360f;
                if (variant == 3)
                {
                    SanctuaryOffering(parent, at, yaw, placed.Count);
                    continue;
                }
                Transform remains = Holder(parent, variant == 0 ? "DiscardedCorpse" :
                    variant == 1 ? "ScatteredSkeleton" : "LairFloorRemains", at, Quaternion.Euler(0f, yaw, 0f));
                remains.localScale = Vector3.one * (0.8f + (float)random.NextDouble() * 0.35f);
                if (variant < 2)
                    SanctuaryBody(remains, variant == 1, placed.Count);
                else if (variant == 2)
                {
                    SanctuarySkull(remains, new Vector3(-0.3f, 0.21f, 0.1f), 0.24f);
                    for (int bone = 0; bone < 4; bone++)
                    {
                        Vector3 start = new Vector3((float)random.NextDouble() * 1.4f - 0.7f, 0.09f,
                            (float)random.NextDouble() * 1.4f - 0.7f);
                        Vector3 end = start + Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * Vector3.forward * 0.7f;
                        SanctuaryLimb(remains, start, end, 0.065f, kit.Mat("Bone"));
                    }
                }
                else if (variant == 4)
                {
                    SanctuaryCurve(remains, "BrokenAntler", new Vector3(-0.9f, 0.12f, -0.4f),
                        new Vector3(-0.6f, 0.3f, 0.1f), new Vector3(0.4f, 0.22f, 0.6f),
                        new Vector3(1f, 0.1f, 0.2f), 0.16f, kit.Mat("Bone"));
                    SanctuaryLimb(remains, new Vector3(-0.25f, 0.2f, 0.2f), new Vector3(-0.5f, 0.12f, 0.85f), 0.08f, kit.Mat("Bone"));
                    SanctuaryLimb(remains, new Vector3(0.2f, 0.2f, 0.4f), new Vector3(0.6f, 0.12f, 1f), 0.065f, kit.Mat("Bone"));
                }
                else
                {
                    // A flattened discarded shroud with a fallen votive candle.
                    LocalBall(remains, new Vector3(0f, 0.045f, 0f), 1f, kit.Mat("Cloth"))
                        .transform.localScale = new Vector3(1.1f, 0.09f, 1.8f);
                    LocalBall(remains, new Vector3(0.3f, 0.075f, -0.3f), 1f, kit.Mat("Leather"))
                        .transform.localScale = new Vector3(0.65f, 0.1f, 0.8f);
                    SanctuaryLimb(remains, new Vector3(0.7f, 0.09f, 0.3f), new Vector3(1f, 0.09f, 0.6f), 0.085f, kit.Mat("Candle"));
                }
            }
        }

        private void SanctuaryBody(Transform body, bool skeleton, int variant)
        {
            Material material = kit.Mat(skeleton ? "Bone" : "Cloth");
            float armZ = variant % 2 == 0 ? 0.55f : -0.1f;
            if (skeleton)
            {
                SanctuarySkull(body, new Vector3(0f, 0.23f, 1.05f), 0.24f);
                SanctuaryLimb(body, new Vector3(0f, 0.12f, -0.35f), new Vector3(0f, 0.17f, 0.7f), 0.065f, material);
                for (int rib = 0; rib < 3; rib++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float z = 0.2f + rib * 0.16f;
                        SanctuaryCurve(body, "SmallRib", new Vector3(0f, 0.15f, z),
                            new Vector3(side * 0.25f, 0.34f, z + 0.04f), new Vector3(side * 0.45f, 0.23f, z - 0.02f),
                            new Vector3(side * 0.32f, 0.09f, z - 0.15f), 0.045f, material);
                    }
            }
            else
            {
                LocalBall(body, new Vector3(0f, 0.17f, 0.3f), 1f, material)
                    .transform.localScale = new Vector3(0.7f, 0.34f, 1.1f);
                LocalBall(body, new Vector3(0.08f, 0.2f, 1.03f), 0.22f, kit.Mat("TanDark"));
                LocalBall(body, new Vector3(0f, 0.15f, -0.32f), 1f, kit.Mat("Leather"))
                    .transform.localScale = new Vector3(0.6f, 0.3f, 0.45f);
            }
            float thickness = skeleton ? 0.07f : 0.13f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 elbow = new Vector3(side * 0.62f, 0.12f, armZ);
                SanctuaryLimb(body, new Vector3(side * 0.25f, 0.17f, 0.55f), elbow, thickness, material);
                SanctuaryLimb(body, elbow, new Vector3(side * 0.88f, 0.09f, armZ - side * 0.35f), thickness * 0.8f, material);
                Vector3 knee = new Vector3(side * 0.3f, 0.12f, -0.9f);
                SanctuaryLimb(body, new Vector3(side * 0.18f, 0.13f, -0.35f), knee, thickness, material);
                SanctuaryLimb(body, knee, new Vector3(side * 0.48f, 0.09f, -1.5f + side * 0.15f), thickness * 0.85f, material);
            }
        }

        private void SanctuaryLimb(Transform parent, Vector3 start, Vector3 end, float radius, Material material)
        {
            SanctuaryCurve(parent, "FallenFragment", start, Vector3.Lerp(start, end, 0.33f) + Vector3.up * 0.04f,
                Vector3.Lerp(start, end, 0.67f) + Vector3.up * 0.04f, end, radius, material);
        }

        private void SanctuaryOffering(Transform parent, Vector3 at, float yaw, int variant)
        {
            Transform offering = Holder(parent, "ShedOffering", at, Quaternion.Euler(0f, yaw, 0f));
            // Loose bones and wax; shed skins are separate, serpent-sized scenery.
            SanctuarySkull(offering, new Vector3(0.5f, 0.25f, -0.3f), 0.28f);
            SanctuaryCurve(offering, "LooseBone", new Vector3(-0.8f, 0.12f, 0.3f),
                new Vector3(-0.3f, 0.16f, 0.5f), new Vector3(0.4f, 0.17f, 0.3f),
                new Vector3(0.9f, 0.12f, 0.65f), 0.09f, kit.Mat("Bone"));
            for (int j = 0; j < 2 + variant % 2; j++)
            {
                Vector3 p = new Vector3(-1f + j * 0.32f, 0f, -0.65f - j % 2 * 0.25f);
                float h = 0.28f + (j + variant) % 3 * 0.12f;
                LocalCyl(offering, p + Vector3.up * h * 0.5f, 0.075f, h, kit.Mat("Candle"), false);
                LocalFlame(offering, p + Vector3.up * (h + 0.05f), 0.06f, kit.Mat("Lantern"));
            }
        }

        private void SanctuaryShedSkin(Transform parent, Vector3 at, float yaw, int variant)
        {
            Transform root = Holder(parent, "ColossalShedSkin", at, Quaternion.Euler(0f, yaw, 0f));
            // Match the actual attack body's world radius, without applying the boss's
            // root scale again. Collapse the height while retaining the body's width.
            float radius = SerpentPursuit.Radius;
            float length = CarrionSaintLook.SerpentLength * ShepherdLook.Phase2Scale * (0.25f + variant * 0.025f);
            const int rings = 32, sides = 32, stride = sides + 1;
            int surfaceCount = (rings + 1) * stride;
            var vertices = new Vector3[surfaceCount * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[rings * sides * 12];
            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float bend = Mathf.Sin(t * 5f + variant) * 0.65f;
                float width = radius * (1f + 0.06f * Mathf.Sin(t * 19f + variant));
                for (int side = 0; side <= sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides;
                    float across = Mathf.Cos(angle), up = Mathf.Sin(angle);
                    // A low, wrinkled hollow sleeve. Both torn ends retain the full width:
                    // no head, tail taper or end caps that could read as a living snake.
                    float fold = 0.16f * Mathf.Sin(t * 31f + across * 8f + variant);
                    float tear = 0.32f * Mathf.Sin(angle * 7f + variant) + 0.18f * Mathf.Cos(angle * 11f);
                    float endWeight = Mathf.Pow(Mathf.Abs(t * 2f - 1f), 12f);
                    int v = ring * stride + side;
                    vertices[v] = new Vector3(bend + across * width,
                        0.12f + (up + 1f) * (0.38f + fold) + 0.07f * Mathf.Sin(t * 43f + angle * 3f),
                        (t - 0.5f) * length + tear * endWeight);
                    vertices[v + surfaceCount] = vertices[v];
                    // Same twelve scales around the skin and 0.7m row spacing as attack tails.
                    uv[v] = uv[v + surfaceCount] = new Vector2(side * 12f / sides, t * length / 0.7f);
                    if (ring == rings || side == sides) continue;
                    int index = (ring * sides + side) * 12;
                    triangles[index] = v; triangles[index + 1] = v + 1; triangles[index + 2] = v + stride;
                    triangles[index + 3] = v + 1; triangles[index + 4] = v + stride + 1; triangles[index + 5] = v + stride;
                    // Separate inside vertices give the open ends a correctly lit inner surface.
                    for (int corner = 0; corner < 6; corner += 3)
                    {
                        triangles[index + 6 + corner] = triangles[index + corner + 2] + surfaceCount;
                        triangles[index + 7 + corner] = triangles[index + corner + 1] + surfaceCount;
                        triangles[index + 8 + corner] = triangles[index + corner] + surfaceCount;
                    }
                }
            }
            var mesh = new Mesh { name = "DeflatedTornSerpentMoult", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            // Reuse the build-safe runtime material and the serpent's existing scale pattern.
            // Build a render-only object directly: no primitive or collider is created.
            var skin = new GameObject("OpenEndedShedSkin");
            skin.transform.SetParent(root, false);
            skin.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer renderer = skin.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Resources.Load<Material>("RuntimePrimitive");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var surface = new MaterialPropertyBlock();
            var tint = new Color(0.78f, 0.75f, 0.58f);
            surface.SetColor("_BaseColor", tint);
            surface.SetColor("_Color", tint);
            surface.SetFloat("_SerpentScales", 1f);
            surface.SetFloat("_RimIntensity", 0f);
            renderer.SetPropertyBlock(surface);
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
                Vector3 reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.forward)) > 0.95f ? Vector3.up : Vector3.forward;
                Vector3 normal = Vector3.Cross(tangent, reference).normalized;
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

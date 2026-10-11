using System.Collections.Generic;
using UnityEngine;
using PoeClone.Enemies;
using PoeClone.Visuals;

namespace PoeClone.World
{
    public partial class WorldBuilder
    {
        private Material desertStone, desertSand;
        private static Vector3 DesertOrigin => Center(Desert) + new Vector3(-135, 1.1f, -126);

        private void BuildActTwo()
        {
            Begin(Desert, 9917);
            desertStone = new Material(kit.Mat("Stone")) { name = "SunwornSandstone" };
            desertStone.SetColor("_BaseColor", new Color(0.72f, 0.51f, 0.30f));
            desertSand = FloorMaterial("DuneSand", GroundTexture(Desert), 6f);
            spawnPoints[Desert].SetPositionAndRotation(DesertOrigin, Quaternion.Euler(0, 40, 0));
            Sanctuary.Add(DesertOrigin, 14f);
            Sanctuary.Add(WaystoneSpot(Desert), 9f);
            Sanctuary.Add(Center(Sahret), 75f);
            Transform desert = Group("The Sundered Expanse");
            Vector3 c = Center(Desert);

            // The inaccessible descent is scenery, never a gate back to the sanctuary.
            for (int i = 0; i < 9; i++)
                DesertRock(desert, DesertOrigin + new Vector3(-14 + i * 3, -0.5f, -14),
                    new Vector3(R(3, 5), R(4, 8), R(3, 5)));
            for (int i = 0; i < 6; i++)
                DesertRock(desert, c + new Vector3(-155 + i * 6, 0, -104 + i * 3), new Vector3(3, 5, 3));

            // Dunes are continuous curved surfaces outside the playable outline and in its islands.
            AreaShape shape = Shape(Desert);
            for (int z = -190; z <= 190; z += 23)
                for (int x = -190; x <= 190; x += 25)
                {
                    Vector3 p = c + new Vector3(x + R(-7, 7), 0, z + R(-7, 7));
                    if (shape.Contains(p, -14f)) continue;
                    DesertDune(desert, p, new Vector2(R(13, 22), R(9, 16)), R(2.5f, 5.5f));
                }
            foreach (var cut in shape.Cuts)
                DesertDune(desert, c + new Vector3(cut.center.x, 0, cut.center.y), cut.radii * 0.9f, 5f);

            // Exposed paving follows the main road; broken markers are its navigational language.
            Vector3[] road = { new Vector3(-112, 0, -99), new Vector3(-77, 0, -76), new Vector3(-5, 0, -31),
                new Vector3(67, 0, 28), new Vector3(115, 0, 45), new Vector3(137, 0, 74),
                new Vector3(108, 0, 103), new Vector3(129, 0, 123) };
            for (int i = 1; i < road.Length; i++)
            {
                Vector3 axis = (road[i] - road[i - 1]).normalized;
                Vector3 side = Vector3.Cross(axis, Vector3.up);
                float length = Vector3.Distance(road[i], road[i - 1]);
                for (float d = 0; d < length; d += 4.5f)
                {
                    Vector3 p = c + road[i - 1] + axis * d;
                    if (rng.NextDouble() > 0.25)
                        WeatheredSlab(desert, "BuriedRoadStone", p + Vector3.up * 0.025f,
                            new Vector2(R(1.2f, 2), R(1, 1.8f)), 0.055f, desertStone, R(0, 35), false);
                    if ((int)d % 18 < 4)
                        DesertPillar(desert, p + side * 9f, R(1.3f, 2.6f));
                }
            }

            // Caravan's End, around the shrine, with clear ground at its waystone.
            DesertWagon(desert, c + new Vector3(-94, 0, -66), 38);
            DesertWagon(desert, c + new Vector3(-57, 0, -90), -25);
            DesertWagon(desert, c + new Vector3(-80, 0, -98), 103);
            DesertPillar(desert, WaystoneSpot(Desert) + new Vector3(-5, 0, -3), 3.5f);
            DesertPillar(desert, WaystoneSpot(Desert) + new Vector3(5, 0, -3), 2.2f);
            for (int i = 0; i < 8; i++) Bones(desert, c + new Vector3(R(-102, -49), 0, R(-100, -65)));

            // The ruined caravanserai stands beside the road, leaving both routes clear.
            Vector3 ruin = c + new Vector3(-18, 0, -13);
            for (int i = 0; i < 5; i++)
            {
                DesertPillar(desert, ruin + new Vector3(i * 5, 0, 0), i == 2 ? 1.5f : R(3, 5));
                if (i != 2) Box(desert, ruin + new Vector3(i * 5, 0.6f, 5), new Vector3(3, 1.2f, 1.2f), desertStone);
            }
            WindBanner(desert, Desert, new Vector3(9, 0, -13), -15, "ClothRed", true);

            // Optional scorpion basin and buried shrine, both rejoin the road.
            Vector3 nest = c + new Vector3(-116, 0, 18);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2 / 7;
                DesertRock(desert, nest + new Vector3(Mathf.Sin(a) * 23, 0, Mathf.Cos(a) * 19), new Vector3(3, R(2, 5), 3));
                Bones(desert, nest + new Vector3(R(-12, 12), 0, R(-10, 10)));
            }
            Vector3 shrine = c + new Vector3(-43, 0, 111);
            for (int side = -1; side <= 1; side += 2)
                DesertPillar(desert, shrine + new Vector3(side * 7, 0, 4), 5);
            Box(desert, shrine + new Vector3(0, 0.35f, 6), new Vector3(8, 0.7f, 3), desertStone);
            BuildDesertCache(desert, nest + new Vector3(0, 0, 8), "act2.nest_cache");
            BuildDesertCache(desert, shrine, "act2.shrine_cache");

            // A broken aqueduct marks the turn into the Windcut Narrows.
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = c + new Vector3(80 + i * 7, 0, 44);
                DesertPillar(desert, p, 6.5f);
                if (i != 1) Box(desert, p + new Vector3(3.5f, 6.4f, 0), new Vector3(7.4f, 1, 1.7f), desertStone, false);
            }
            for (int i = 0; i < 5; i++)
            {
                DesertRock(desert, c + new Vector3(121 - i * 2, 0, 58 + i * 7), new Vector3(4, R(5, 8), 3));
                DesertRock(desert, c + new Vector3(153 - i * 2, 0, 57 + i * 8), new Vector3(4, R(5, 8), 3));
            }
            WindBanner(desert, Desert, new Vector3(145, 0, 78), 20, "ClothRed", true);

            // Silhouette only: Sahret's walls announce the destination before its actual transition.
            Vector3 wall = c + new Vector3(130, 0, 145);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(desert, wall + new Vector3(side * 22, 5, 0), new Vector3(32, 10, 4), desertStone, false);
                DesertPillar(desert, wall + new Vector3(side * 8, 0, 0), 13);
            }
            Scatter(desert, 12, 20, 170, p => Bones(desert, p), 0.4f);

            // The hub intentionally stops at an empty, safe arrival court.
            Transform city = Group("Sahret - Work in Progress");
            Vector3 town = Center(Sahret);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(city, town + new Vector3(side * 22, 4, -28), new Vector3(24, 8, 4), desertStone);
                DesertPillar(city, town + new Vector3(side * 9, 0, -28), 10);
            }
            Transform notice = Holder(city, "UnfinishedCity", town + new Vector3(0, 0, 17), Quaternion.identity);
            WorldLabel.Create(notice, "Sahret — Work in Progress", new Color(0.96f, 0.82f, 0.55f), 2.5f);
            gameObject.AddComponent<DesertAtmosphere>();
        }

        private void BuildDesertCache(Transform parent, Vector3 at, string id)
        {
            Transform chest = Holder(parent, "BuriedCaravanCache", at, Quaternion.identity);
            LocalBox(chest, new Vector3(0, 0.4f, 0), new Vector3(1.7f, 0.8f, 1.1f), kit.Mat("Wood"), false);
            Transform lid = LocalBox(chest, new Vector3(0, 0.88f, 0), new Vector3(1.8f, 0.18f, 1.2f), kit.Mat("Iron"), false).transform;
            chest.gameObject.AddComponent<DesertCache>().Configure(id, lid);
        }

        private void DesertPillar(Transform parent, Vector3 p, float height)
        {
            WeatheredSlab(parent, "SandstonePlinth", p, new Vector2(1.1f, 1.1f), 0.35f, desertStone, R(0, 20));
            Cyl(parent, p + Vector3.up * height * 0.5f, 0.65f, height, desertStone);
            WeatheredSlab(parent, "BrokenCapital", p + Vector3.up * height, new Vector2(0.9f, 0.85f), 0.35f, desertStone, R(-12, 12));
        }

        private void DesertRock(Transform parent, Vector3 at, Vector3 scale)
        {
            GameObject rock = Prefab(kit.rock, parent, at, R(0, 360), scale);
            Tint(rock, new Color(0.79f, 0.58f, 0.34f));
        }

        private void DesertWagon(Transform parent, Vector3 at, float yaw)
        {
            Transform wagon = Holder(parent, "AbandonedDesertWagon", at, Quaternion.Euler(0, yaw, -8));
            LocalBox(wagon, new Vector3(0, 0.6f, 0), new Vector3(2.8f, 0.24f, 4.2f), kit.Mat("Wood"));
            for (int side = -1; side <= 1; side += 2)
            {
                LocalBox(wagon, new Vector3(side * 1.3f, 1.1f, 0), new Vector3(0.16f, 0.9f, 3.9f), kit.Mat("Wood"));
                for (int end = -1; end <= 1; end += 2)
                {
                    GameObject wheel = LocalCyl(wagon, new Vector3(side * 1.5f, 0.45f, end * 1.4f), 0.7f, 0.2f, kit.Mat("Wood"), false);
                    wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
                LocalCyl(wagon, new Vector3(side * 1.2f, 1.9f, 1), 0.06f, 2.6f, kit.Mat("Wood"), false);
            }
            WindCloth.Create(wagon, new Vector3(0, 3.1f, 1), 2.3f, 1.8f, kit.Mat("ClothYellow"), true);
        }

        private void DesertDune(Transform parent, Vector3 at, Vector2 radii, float height)
        {
            const int rings = 7, sides = 32;
            var vertices = new List<Vector3> { Vector3.up * height };
            var triangles = new List<int>();
            for (int ring = 1; ring <= rings; ring++)
            {
                float u = ring / (float)rings;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    float h = height * Mathf.Pow(1 - u, 1.4f) - 0.2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radii.x * u + (1 - u) * 2, h, Mathf.Sin(a) * radii.y * u));
                    int n = 1 + (ring - 1) * sides + i;
                    int next = 1 + (ring - 1) * sides + (i + 1) % sides;
                    if (ring == 1) triangles.AddRange(new[] { 0, next, n });
                    else triangles.AddRange(new[] { n - sides, next - sides, n, n, next - sides, next });
                }
            }
            var mesh = new Mesh { name = "WindCarvedDune" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var dune = new GameObject("WindCarvedDune");
            dune.transform.SetParent(parent, false); dune.transform.position = at;
            dune.AddComponent<MeshFilter>().sharedMesh = mesh;
            dune.AddComponent<MeshRenderer>().sharedMaterial = desertSand;
        }

        private void SetUpDesertEncounters()
        {
            EnemySpawner source = FindAnyObjectByType<EnemySpawner>();
            if (source == null || source.EnemyPrefab == null) return;
            var go = new GameObject("Spawner_SunderedExpanse");
            go.transform.SetParent(root, false);
            go.transform.position = Center(Desert);
            var spawner = go.AddComponent<EnemySpawner>();
            spawner.Configure(source.EnemyPrefab, Center(Desert), 170, 85, 38, KindWeights[Desert]);
            spawner.SetSafeSpots(SafeSpots(Desert));
            spawner.SetBounds(p => Shape(Desert).Contains(p, 4f));
            float[] creatures = Roster(("Dune Skitter", 3), ("Glassback Scorpion", 2));
            float[] dunes = Roster(("Dune Skitter", 2), ("Glassback Scorpion", 2), ("Sandlurker", 3));
            float[] raiders = Roster(("Dustroad Reaver", 4), ("Dustroad Javelineer", 3), ("Ashwind Caller", 1));
            spawner.SetKindSelector(p =>
            {
                Vector3 local = p - Center(Desert);
                return EnemyKinds.PickIndex(local.z < -65 ? creatures : local.x < -65 ? dunes : local.z < 10 ? KindWeights[Desert] : raiders);
            });
            spawnersByArea[Desert] = spawner;
            spawner.enabled = false;
            go.AddComponent<DesertEncounters>().Configure(spawner);
        }

        private void BuildShepherdPassage(Transform arena)
        {
            // Inside the north wall: an actual breakable panel between rough sandstone jambs.
            Vector3 at = ActArenaCenter + new Vector3(0, 0, 38);
            Transform breach = Holder(arena, "ShepherdWallBreach", at, Quaternion.identity);
            for (int side = -1; side <= 1; side += 2)
                DesertRock(breach, at + new Vector3(side * 5.5f, 0, 0), new Vector3(2.5f, 5, 2));
            Box(breach, at + new Vector3(0, 7.2f, 0), new Vector3(11, 2, 3), desertStone, false);
            GameObject seal = Box(breach, at + new Vector3(0, 3, 0), new Vector3(8, 6, 2), desertStone);
            seal.name = "CrackedSanctuaryWall";
            for (int i = 0; i < 4; i++)
            {
                GameObject crack = Box(seal.transform, at + new Vector3((i % 2) * 0.8f, 1 + i * 1.2f, -1.02f),
                    new Vector3(0.12f, 1.8f, 0.02f), kit.Mat("RockDark"), false);
                crack.transform.rotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 25 : -30);
            }
            var door = new GameObject("Passage to Act II");
            door.transform.SetParent(breach, false); door.transform.position = at + Vector3.up * 1.1f;
            var trigger = door.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(7, 4, 3);
            var gate = door.AddComponent<AreaGate>(); gate.fromAreaIndex = ActArena; gate.targetAreaIndex = Desert;
            gate.arrival = spawnPoints[Desert];
            WorldLabel.Create(door.transform, "Act II — The Sundered Expanse", AreaColor(Desert), 3.7f);
            Glow(door.transform, at + new Vector3(0, 3, -1), new Color(1, 0.78f, 0.40f), 14, 5);
            breach.gameObject.AddComponent<ActTwoPassage>().Configure(seal, gate);
        }
    }
}

using UnityEngine;
using PoeClone.Visuals;

namespace PoeClone.World
{
    /// <summary>
    /// The newer field bosses' lairs: Bramblesow's wallow at Greenwood's north-east dead end, the
    /// Sunforged Idol's Sun Court off the Ashen Ruins' south-west hall, Hrimgar's hunting camp at
    /// the Frozen Hollow's north-east dead end, and two dens of their own behind gates: the Warren
    /// (Vex, through a gate at a dead end of the Lost Hollows) and the Drowned Belfry (the
    /// Bell-Ringer, down a lane off the Haunted Graveyard's north-east plots).
    /// </summary>
    public partial class WorldBuilder
    {
        public const int Warren = 7;
        public const int Belfry = 8;

        private struct FieldLair
        {
            public int Area;
            public string Boss;
            public Vector3 Local;
            public Vector3 Facing;
        }

        private static readonly FieldLair[] FieldLairs =
        {
            new FieldLair { Area = Greenwood, Boss = "Bramblesow", Local = new Vector3(140f, 0f, 102f), Facing = new Vector3(-0.3f, 0f, -1f) },
            new FieldLair { Area = Warren, Boss = "Vex, the Tunnel King", Local = new Vector3(0f, 0f, 10f), Facing = Vector3.back },
            new FieldLair { Area = Belfry, Boss = "The Bell-Ringer", Local = new Vector3(0f, 0f, 10f), Facing = Vector3.back },
            new FieldLair { Area = Ruins, Boss = "The Sunforged Idol", Local = new Vector3(-121f, 0f, -120f), Facing = new Vector3(1f, 0f, 0.9f) },
            new FieldLair { Area = Frozen, Boss = "Hrimgar the Huntress", Local = new Vector3(92f, 0f, 124f), Facing = new Vector3(0.1f, 0f, -1f) },
        };

        // Where the den gates stand (local to their areas), and which way is in.
        private static readonly Vector3 WarrenGateLocal = new Vector3(137f, 0f, -93f);
        private static readonly Vector3 WarrenGateInward = new Vector3(-14f, 0f, 35f);
        private static readonly Vector3 BelfryGateLocal = new Vector3(150f, 0f, 97.5f);
        private static readonly Vector3 BelfryGateInward = new Vector3(-44f, 0f, -10f);
        private static readonly Vector3 DenGateLocal = new Vector3(0f, 0f, -43f);

        private static readonly Color CopperLight = new Color(1f, 0.62f, 0.3f);
        private static readonly Color BellLight = new Color(1f, 0.9f, 0.55f);

        private static float YawOf(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        // Before the borders (which keep clear of gates) and the spawners.
        private void BuildFieldBossLairs()
        {
            BuildWallow();
            BuildSunCourt();
            BuildHuntingCamp();
            ConnectDen(Cave, WarrenGateLocal, WarrenGateInward, Warren);
            ConnectDen(Graveyard, BelfryGateLocal, BelfryGateInward, Belfry);
            DressWarrenGate();
            DressBelfryGate();
        }

        private static Vector3 LairSpot(string boss)
        {
            foreach (FieldLair lair in FieldLairs)
            {
                if (lair.Boss == boss)
                    return Center(lair.Area) + lair.Local;
            }
            return Vector3.zero;
        }

        // A gate at the dead end leading into the den, and the den's own gate back out.
        private void ConnectDen(int area, Vector3 gateLocal, Vector3 inward, int den)
        {
            inward = inward.normalized;
            Vector3 outsideGate = Center(area) + gateLocal;
            Vector3 insideGate = Center(den) + DenGateLocal;
            Transform arriveOutside = Marker("Arrive_" + AreaNames[area] + "_from_" + AreaNames[den], outsideGate + inward * 5f + Vector3.up * 1.1f, YawOf(inward));
            Transform arriveInside = Marker("Arrive_" + AreaNames[den] + "_from_" + AreaNames[area], insideGate + Vector3.forward * 6f + Vector3.up * 1.1f, 0f);
            ClearSpot(arriveOutside.position, 2f);
            Gate(area, outsideGate, den, arriveInside, YawOf(inward));
            Gate(den, insideGate, area, arriveOutside, 0f);
        }

        private Transform Floor(int area, string name, Material material)
        {
            Transform group = Group(name);
            var floor = new GameObject("Ground_" + AreaNames[area]);
            floor.transform.SetParent(group, false);
            floor.transform.position = Center(area);
            Mesh mesh = Shape(area).BuildGroundMesh(0f);
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = material;
            floor.AddComponent<MeshCollider>().sharedMesh = mesh;
            BuildLayoutWalls(area, 0f);
            return group;
        }

        // ------------------------------------------------------------------ Greenwood: Bramblesow's wallow

        // A trampled clearing with a mud wallow, ringed by fallen logs it can ram.
        private void BuildWallow()
        {
            Begin(Greenwood, 712);
            Vector3 at = LairSpot("Bramblesow");
            ClearSite(Greenwood, at, 17f);
            Transform t = Group("BramblesowWallow");
            FloorDisc(t, "TrampledEarth", at + Vector3.up * 0.035f, 11f, DirtFloor);
            Ball(t, at + new Vector3(4f, 0.02f, 5f), 3.4f, MudFloor, flatten: 0.05f, solid: false);
            Cyl(t, at + new Vector3(4f, 0.05f, 5f), 2.6f, 0.03f, kit.Mat("Water"), solid: false);
            for (int k = 0; k < 7; k++)
            {
                float a = k * Mathf.PI * 2f / 7f + 0.4f;
                if (k == 4)
                    continue; // the way in, from the south-west
                Vector3 p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * R(13f, 15f);
                Cyl(t, p + Vector3.up * 0.45f, 0.45f, R(3.5f, 5f), kit.Mat("Bark"), euler: new Vector3(90f, R(0f, 180f), 0f));
                Claim(p, 3f);
            }
            for (int k = 0; k < 5; k++)
            {
                Vector3 p = at + Flat(R(-9f, 9f), R(-9f, 9f));
                if (Vector3.Distance(p, at) < 4f)
                    continue;
                Bones(t, p);
            }
            // Torn-up roots and gouges where it has charged.
            for (int k = 0; k < 4; k++)
                Box(t, at + Flat(R(-8f, 8f), R(-8f, 8f)) + Vector3.up * 0.03f, new Vector3(0.6f, 0.05f, R(3f, 6f)), kit.Mat("Bark"), solid: false,
                    euler: new Vector3(0f, R(0f, 180f), 0f));
            Claim(at, 12f);
        }

        // ------------------------------------------------------------------ the Ashen Ruins: the Sun Court

        // A paved court with a golden sun set in the floor, ringed by pillars and braziers.
        private void BuildSunCourt()
        {
            Begin(Ruins, 713);
            Vector3 at = LairSpot("The Sunforged Idol");
            ClearSite(Ruins, at, 18f);
            Transform t = Group("SunCourt");
            WeatheredSlab(t, "CourtFloor", at, new Vector2(14f, 13f), 0.1f, kit.Mat("Sandstone"), 12f, solid: false);
            Cyl(t, at + Vector3.up * 0.12f, 3.6f, 0.03f, kit.Mat("Gold"), solid: false);
            for (int k = 0; k < 12; k++)
                Box(t, at + Quaternion.Euler(0f, k * 30f, 0f) * Vector3.forward * 5.2f + Vector3.up * 0.12f, new Vector3(0.4f, 0.03f, 2.4f),
                    kit.Mat("Gold"), solid: false, euler: new Vector3(0f, k * 30f, 0f));
            for (int k = 0; k < 10; k++)
            {
                float rad = k * 36f * Mathf.Deg2Rad;
                Vector3 p = at + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 15.5f;
                if (k == 1)
                    continue; // the way in, from the north-east
                if (k % 4 == 3)
                    FallenColumn(t, p, k * 36f + 80f);
                else
                    Prefab(kit.pillar, t, p, R(0f, 360f), new Vector3(1.1f, R(0.7f, 1.3f), 1.1f));
            }
            for (int k = 0; k < 4; k++)
            {
                float rad = (k * 90f + 45f) * Mathf.Deg2Rad;
                Brazier(t, at + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 11.5f);
            }
            Glow(t, at + Vector3.up * 4f, FireLight, 18f, 3f);
            Claim(at, 16f);
        }

        // ------------------------------------------------------------------ the Frozen Hollow: Hrimgar's camp

        // A hide tent, a fire, a drying rack of pelts and spears stood in the snow.
        private void BuildHuntingCamp()
        {
            Begin(Frozen, 714);
            Vector3 at = LairSpot("Hrimgar the Huntress");
            ClearSite(Frozen, at, 15f);
            Transform t = Group("HuntingCamp");
            Vector3 back = at + new Vector3(0f, 0f, 9f);
            Tent(t, back, Quaternion.Euler(0f, 180f, 0f), kit.Mat("Leather"), 1.2f);
            Vector3 fire = at + new Vector3(-6f, 0f, 4f);
            for (int k = 0; k < 4; k++)
                Cyl(t, fire + Vector3.up * 0.15f, 0.12f, 1.6f, kit.Mat("Charred"), solid: false, euler: new Vector3(90f, k * 45f, 0f));
            Flame(t, fire + Vector3.up * 0.35f, 0.4f, kit.Mat("Ember"), flatten: 0.6f);
            Glow(t, fire + Vector3.up * 1.4f, FireLight, 13f, 5f, flicker: true);
            // A drying rack hung with white pelts.
            Vector3 rack = at + new Vector3(7f, 0f, 5f);
            for (int s = -1; s <= 1; s += 2)
                Box(t, rack + new Vector3(s * 1.4f, 1.2f, 0f), new Vector3(0.15f, 2.4f, 0.15f), kit.Mat("Wood"));
            Box(t, rack + new Vector3(0f, 2.3f, 0f), new Vector3(3f, 0.12f, 0.12f), kit.Mat("Wood"), solid: false);
            for (int k = 0; k < 3; k++)
                Box(t, rack + new Vector3(-0.9f + k * 0.9f, 1.6f, 0f), new Vector3(0.7f, 1.3f, 0.05f), kit.Mat(k == 1 ? "Leather" : "Snow"), solid: false);
            // Spears stood in the snow, and the bones of the hunt.
            for (int k = 0; k < 5; k++)
            {
                Vector3 p = at + new Vector3(-9f + k * 1.1f, 0f, -2f + (k % 2) * 1.2f);
                Cyl(t, p + Vector3.up * 1f, 0.05f, 2.2f, kit.Mat("Wood"), solid: false, euler: new Vector3(R(-12f, 12f), 0f, R(-12f, 12f)));
            }
            for (int k = 0; k < 4; k++)
                Bones(t, at + Flat(R(-10f, 10f), R(-8f, 8f)));
            Claim(at, 13f);
        }

        // ------------------------------------------------------------------ the gates out on the map

        private void DressWarrenGate()
        {
            Begin(Cave, 715);
            Vector3 gate = Center(Cave) + WarrenGateLocal;
            Vector3 inward = WarrenGateInward.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            Transform t = Group("WarrenGate");
            // Timber props round the hole, copper lamps, and the tunnel king's junk spilling out.
            for (int s = -1; s <= 1; s += 2)
            {
                Box(t, gate + side * s * 3.2f + Vector3.up * 1.8f, new Vector3(0.4f, 3.6f, 0.4f), kit.Mat("Wood"));
                Glow(t, gate + side * s * 3f + inward * 1f + Vector3.up * 2.4f, CopperLight, 8f, 3f, flicker: true);
                Flame(t, gate + side * s * 3f + inward * 1f + Vector3.up * 2.3f, 0.15f, kit.Mat("Lantern"));
            }
            Box(t, gate + Vector3.up * 3.7f, new Vector3(7f, 0.4f, 0.5f), kit.Mat("Wood"), solid: false, euler: new Vector3(0f, YawOf(side), 0f));
            for (int k = 0; k < 4; k++)
                Box(t, gate + inward * R(3f, 6f) + side * R(-4.5f, 4.5f) + Vector3.up * 0.35f, Vector3.one * 0.7f, kit.Mat("Wood"),
                    euler: new Vector3(0f, R(0f, 90f), 0f));
            Cyl(t, gate + inward * 4f - side * 4f + Vector3.up * 0.05f, 0.7f, 0.1f, kit.Mat("Gold"), solid: false);
        }

        private void DressBelfryGate()
        {
            Begin(Graveyard, 716);
            Vector3 gate = Center(Graveyard) + BelfryGateLocal;
            Vector3 inward = BelfryGateInward.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            Transform t = Group("BelfryGate");
            // A lane of lamps, and an old cracked bell sunk in the mud beside the gate.
            for (int k = 1; k <= 3; k++)
                for (int s = -1; s <= 1; s += 2)
                    Lamp(t, gate + inward * (k * 9f) + side * s * 4.2f);
            var bell = new GameObject("SunkBell").transform;
            bell.SetParent(t, false);
            bell.position = gate + inward * 4f + side * 4.5f + Vector3.up * 0.9f;
            bell.rotation = Quaternion.Euler(18f, R(0f, 360f), 12f);
            bell.localScale = Vector3.one * 1.4f;
            CreatureBuilder.BuildBell(bell, new Color(0.45f, 0.35f, 0.2f), new Color(0.3f, 0.58f, 0.48f), new Color(0.2f, 0.18f, 0.15f));
            Candles(t, gate + inward * 3f - side * 3.5f);
        }

        // ------------------------------------------------------------------ the dens

        // The Warren: Vex's hoard cave. Junk, crates and coin heaped round a throne of crates,
        // timber props round the walls, copper lamps.
        private AreaDefinition BuildWarren()
        {
            Vector3 c = Center(Warren);
            Transform t = Floor(Warren, "The Warren", DirtFloor);
            Begin(Warren, 721);
            Vector3 throne = c + new Vector3(0f, 0f, 22f);
            Box(t, throne + new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 1.6f), kit.Mat("Wood"));
            Box(t, throne + new Vector3(0f, 1.8f, 0.7f), new Vector3(2.4f, 2.6f, 0.4f), kit.Mat("Wood"));
            for (int k = -2; k <= 2; k++)
                Box(t, throne + new Vector3(k * 0.5f, 3.3f + (k % 2 == 0 ? 0.3f : 0f), 0.7f), new Vector3(0.12f, 0.8f, 0.12f), kit.Mat("Gold"), solid: false);
            // Heaps of coin and junk.
            for (int k = 0; k < 6; k++)
            {
                Vector3 p = throne + Flat(R(-7f, 7f), R(-3f, 3f));
                if (Mathf.Abs(p.x - throne.x) < 2f)
                    continue;
                Ball(t, p, R(0.8f, 1.4f), kit.Mat("Gold"), flatten: 0.35f, solid: false);
            }
            for (int k = 0; k < 10; k++)
            {
                float a = R(0f, Mathf.PI * 2f);
                Vector3 p = c + new Vector3(Mathf.Cos(a) * R(22f, 27f), 0f, 4f + Mathf.Sin(a) * R(17f, 22f));
                if (Mathf.Sin(a) < -0.6f)
                    continue; // keep the tunnel mouth clear
                if (Coin())
                    Box(t, p + Vector3.up * 0.45f, Vector3.one * R(0.7f, 1f), kit.Mat("Wood"), euler: new Vector3(0f, R(0f, 90f), 0f));
                else
                    Cyl(t, p + Vector3.up * 0.55f, 0.45f, 1.1f, kit.Mat("Wood"));
            }
            // Timber props along the walls.
            for (int k = 0; k < 8; k++)
            {
                float a = (k * 40f + 20f) * Mathf.Deg2Rad;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * 29f, 0f, 4f + Mathf.Sin(a) * 24f);
                Box(t, p + Vector3.up * 2f, new Vector3(0.4f, 4f, 0.4f), kit.Mat("Wood"));
                Glow(t, p + Vector3.up * 2.6f - Flat(Mathf.Cos(a), Mathf.Sin(a)) * 0.8f, CopperLight, 11f, 3.5f, flicker: true);
                Flame(t, p + Vector3.up * 2.5f - Flat(Mathf.Cos(a), Mathf.Sin(a)) * 0.6f, 0.14f, kit.Mat("Lantern"));
            }
            // An overturned mine cart, bones, glowing mushrooms in the niches.
            Vector3 cart = c + new Vector3(-14f, 0f, -6f);
            Box(t, cart + Vector3.up * 0.6f, new Vector3(1.6f, 1f, 2.2f), kit.Mat("Iron"), euler: new Vector3(0f, 30f, 70f));
            for (int k = 0; k < 6; k++)
                Bones(t, c + Flat(R(-20f, 20f), R(-14f, 22f)));
            Glowshrooms(t, c + new Vector3(-24f, 0f, 18f));
            Glowshrooms(t, c + new Vector3(26f, 0f, 16f));
            for (int k = 0; k < 6; k++)
                Glowshrooms(t, c + new Vector3(R(-26f, 26f), 0f, R(-12f, 26f)));
            // Rubble and stolen junk lying about the floor.
            Scatter(t, 8, 9f, 28f, p => Prefab(kit.rock, t, p, R(0f, 360f), Vector3.one * R(0.5f, 0.9f)), 1.5f);
            for (int k = 0; k < 8; k++)
            {
                Vector3 p = c + new Vector3(R(-24f, 24f), 0f, R(-14f, 26f));
                if (Vector3.Distance(p, c + new Vector3(0f, 0f, 10f)) < 6f)
                    continue;
                Box(t, p + Vector3.up * 0.2f, new Vector3(R(0.4f, 0.9f), 0.4f, R(0.4f, 0.9f)), kit.Mat(Coin() ? "Iron" : "Wood"), solid: false,
                    euler: new Vector3(R(-20f, 20f), R(0f, 90f), R(-20f, 20f)));
            }
            Glow(t, c + new Vector3(0f, 6f, 4f), CopperLight, 30f, 2f);
            Transform entry = Marker("WarrenEntry", c + DenGateLocal + Vector3.forward * 6f + Vector3.up * 1.1f, 0f);
            return new AreaDefinition { areaName = AreaNames[Warren], monsterLevel = MonsterLevels[Warren], spawnPoint = entry };
        }

        // The Drowned Belfry: a flooded chapel yard, its walls broken, its bell tower fallen and
        // its bells sunk in the water. The ringer keeps the last one.
        private AreaDefinition BuildBelfry()
        {
            Vector3 c = Center(Belfry);
            Transform t = Floor(Belfry, "The Drowned Belfry", kit.Mat("Stone"));
            Begin(Belfry, 722);
            // Broken chapel walls round the yard, with gaps.
            for (int k = 0; k < 18; k++)
            {
                float deg = k * 20f;
                if (k == 13 || k == 14)
                    continue; // the way in, from the south
                float rad = deg * Mathf.Deg2Rad;
                Vector3 p = c + new Vector3(Mathf.Cos(rad) * 27f, 0f, 4f + Mathf.Sin(rad) * 26f);
                float h = k % 5 == 0 ? R(0.8f, 1.6f) : R(2.6f, 4.6f);
                Box(t, p + Vector3.up * h * 0.5f, new Vector3(1f, h, 8.6f), kit.Mat("TombstoneDark"), euler: new Vector3(0f, -deg, 0f));
            }
            // The fallen bell tower behind the ringer: its posts, a broken beam, an empty yoke.
            Vector3 tower = c + new Vector3(0f, 0f, 24f);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(t, tower + new Vector3(s * 4f, 5f, 0f), new Vector3(0.7f, 10f, 0.7f), kit.Mat("Wood"));
                Box(t, tower + new Vector3(s * 4f, 5f, -3f), new Vector3(0.7f, 10f, 0.7f), kit.Mat("Wood"), euler: new Vector3(0f, 0f, s * 4f));
            }
            Box(t, tower + new Vector3(0f, 9.6f, -1.5f), new Vector3(9f, 0.6f, 0.6f), kit.Mat("Wood"), solid: false, euler: new Vector3(0f, 0f, 6f));
            Box(t, tower + new Vector3(2f, 0.4f, -6f), new Vector3(7f, 0.6f, 0.6f), kit.Mat("Wood"), solid: false, euler: new Vector3(0f, 35f, 8f));
            // Sunken bells and pools of black water.
            Vector3[] bells = { new Vector3(-16f, 0f, 12f), new Vector3(17f, 0f, 6f), new Vector3(-10f, 0f, -14f) };
            foreach (Vector3 local in bells)
            {
                var bell = new GameObject("SunkBell").transform;
                bell.SetParent(t, false);
                bell.position = c + local + Vector3.up * 1.2f;
                bell.rotation = Quaternion.Euler(R(15f, 35f), R(0f, 360f), R(-15f, 15f));
                bell.localScale = Vector3.one * R(1.8f, 2.4f);
                CreatureBuilder.BuildBell(bell, new Color(0.45f, 0.34f, 0.18f), new Color(0.3f, 0.58f, 0.48f), new Color(0.15f, 0.13f, 0.1f));
                Cyl(t, c + local + Vector3.up * 0.04f, 3.4f, 0.03f, kit.Mat("Water"), solid: false);
            }
            for (int k = 0; k < 6; k++)
                Tombstone(t, c + new Vector3(R(-20f, 20f), 0f, R(-16f, 20f)));
            // Black water pooled all over the yard, and drowned trees.
            for (int k = 0; k < 9; k++)
                Cyl(t, c + new Vector3(R(-22f, 22f), 0.03f, R(-18f, 24f)), R(1.5f, 4f), 0.03f, kit.Mat("Water"), solid: false);
            for (int k = 0; k < 4; k++)
                DeadTree(t, c + new Vector3(R(-22f, 22f), 0f, R(-16f, 22f)), kit.Mat("DeadWood"));
            for (int k = 0; k < 5; k++)
                Candles(t, c + new Vector3(R(-18f, 18f), 0f, R(-14f, 18f)));
            for (int k = 0; k < 2; k++)
                for (int s = -1; s <= 1; s += 2)
                    Lamp(t, c + new Vector3(s * 4f, 0f, -38f + k * 9f));
            Glow(t, c + new Vector3(0f, 7f, 4f), BellLight, 32f, 1.6f);
            Glow(t, tower + Vector3.up * 3f, SpiritLight, 14f, 3f, flicker: true);
            Transform entry = Marker("BelfryEntry", c + DenGateLocal + Vector3.forward * 6f + Vector3.up * 1.1f, 0f);
            return new AreaDefinition { areaName = AreaNames[Belfry], monsterLevel = MonsterLevels[Belfry], spawnPoint = entry };
        }
    }
}
